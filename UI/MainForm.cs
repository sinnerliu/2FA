using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using TwoFA.Core;
using TwoFA.Data;
using TwoFA.Models;
using TwoFA.QrCode;
using ZXing;
using System.Web.Script.Serialization;

namespace TwoFA.UI
{
    public partial class MainForm : Form
    {
        private readonly DataStore _dataStore;
        private readonly List<TotpAccount> _displayAccounts;

        public MainForm()
        {
            _dataStore = new DataStore();
            _displayAccounts = new List<TotpAccount>();

            InitializeComponent();
            RegisterEventHandlers();

            // 动态提取 EXE 自身的主图标，绑定到窗口标题栏和系统任务栏中
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
        }

        private void RegisterEventHandlers()
        {
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;
            
            // 搜索过滤
            txtSearch.TextChanged += TxtSearch_TextChanged;

            // 列表事件
            lstAccounts.DrawItem += LstAccounts_DrawItem;
            lstAccounts.MouseClick += LstAccounts_MouseClick;
            lstAccounts.DoubleClick += LstAccounts_DoubleClick;
            lstAccounts.KeyDown += LstAccounts_KeyDown;

            // 按钮事件
            btnAdd.Click += (s, e) => AddNewAccount();
            btnClipboardImport.Click += (s, e) => ImportFromClipboard();
            btnScreenScan.Click += (s, e) => StartScreenScan();

            // 定时器 Tick
            tmrRefresh.Tick += TmrRefresh_Tick;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                _dataStore.LoadAll();
                
                // 应用配置
                ApplyWindowSettings();
                
                RefreshDisplayList();
                UpdateStatus(string.Format("准备就绪 | 共加载了 {0} 个 2FA 账户", _dataStore.Accounts.Count));
            }
            catch (Exception ex)
            {
                MessageBox.Show("初始化加载数据失败: " + ex.Message, "加载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 保存窗口尺寸和位置
            if (this.WindowState == FormWindowState.Normal)
            {
                _dataStore.Settings.WindowWidth = this.Width;
                _dataStore.Settings.WindowHeight = this.Height;
                _dataStore.Settings.WindowLeft = this.Left;
                _dataStore.Settings.WindowTop = this.Top;
            }

            try
            {
                _dataStore.SaveSettings();
            }
            catch { }
        }

        private void ApplyWindowSettings()
        {
            var settings = _dataStore.Settings;
            if (settings.WindowWidth > 200 && settings.WindowHeight > 200)
            {
                this.Width = settings.WindowWidth;
                this.Height = settings.WindowHeight;
            }

            if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
            {
                // 确保在屏幕内
                this.Left = settings.WindowLeft;
                this.Top = settings.WindowTop;
            }

            this.TopMost = settings.TopMost;
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            RefreshDisplayList();
        }

        private void RefreshDisplayList()
        {
            string keyword = txtSearch.Text.Trim().ToLowerInvariant();
            _displayAccounts.Clear();

            foreach (var acc in _dataStore.Accounts)
            {
                if (string.IsNullOrEmpty(keyword) ||
                    (acc.Issuer != null && acc.Issuer.ToLowerInvariant().Contains(keyword)) ||
                    (acc.Account != null && acc.Account.ToLowerInvariant().Contains(keyword)) ||
                    (acc.Remark != null && acc.Remark.ToLowerInvariant().Contains(keyword)))
                {
                    _displayAccounts.Add(acc);
                }
            }

            // 彻底废除可能会产生同步缓存 Bug 的 DataSource 绑定
            lstAccounts.BeginUpdate();
            lstAccounts.DataSource = null;
            lstAccounts.Items.Clear();
            foreach (var acc in _displayAccounts)
            {
                lstAccounts.Items.Add(acc);
            }
            lstAccounts.EndUpdate();
        }

        private void TmrRefresh_Tick(object sender, EventArgs e)
        {
            // 定时刷新列表视图（每一秒触发重绘以刷新倒计时）
            if (_displayAccounts.Count > 0)
            {
                // 开启定时重绘屏蔽擦除状态，消灭背景闪白
                lstAccounts.IsTimerRefreshing = true;

                // 仅使当前处于可见区域的项的矩形失效重绘，降低系统重绘压力并完美消除闪烁
                for (int i = 0; i < lstAccounts.Items.Count; i++)
                {
                    Rectangle rect = lstAccounts.GetItemRectangle(i);
                    if (lstAccounts.ClientRectangle.IntersectsWith(rect))
                    {
                        lstAccounts.Invalidate(rect);
                    }
                }
            }
        }

        /// <summary>
        /// 自绘制列表项逻辑 (OwnerDrawFixed)
        /// </summary>
        private void LstAccounts_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstAccounts.Items.Count)
            {
                return;
            }

            Graphics g = e.Graphics;
            TotpAccount acc = lstAccounts.Items[e.Index] as TotpAccount;
            if (acc == null) return;
            
            // 是否处于选中状态
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            
            // 1. 绘制背景色
            Color bgColor = isSelected ? Color.FromArgb(232, 240, 252) : Color.White;
            using (Brush bgBrush = new SolidBrush(bgColor))
            {
                g.FillRectangle(bgBrush, e.Bounds);
            }

            // 2. 绘制分割线 (底部 1px)
            using (Pen borderPen = new Pen(Color.FromArgb(240, 243, 246), 1))
            {
                g.DrawLine(borderPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            // 3. 计算当前的 TOTP 验证码
            string codeText = "ERR CODE";
            int remainingSec = 30;
            int period = acc.Period > 0 ? acc.Period : 30;
            
            try
            {
                if (!string.IsNullOrEmpty(acc.EncryptedSecret))
                {
                    byte[] secretBytes = Base32.Decode(acc.PlainSecret);
                    codeText = Totp.GenerateCode(secretBytes, DateTime.UtcNow, acc.Digits, period, acc.Algorithm);
                    remainingSec = Totp.GetRemainingSeconds(DateTime.UtcNow, period);
                }
            }
            catch
            {
                codeText = "KEY ERROR";
            }

            // 格式化验证码 (例如 123456 -> 123 456)
            if (codeText.Length == 6)
            {
                codeText = codeText.Substring(0, 3) + " " + codeText.Substring(3);
            }
            else if (codeText.Length == 8)
            {
                codeText = codeText.Substring(0, 4) + " " + codeText.Substring(4);
            }

            // 4. 绘制发行者 (Issuer) 和 账号 (Account)
            using (Font issuerFont = new Font("微软雅黑", 10.5F, FontStyle.Bold))
            using (Font accountFont = new Font("微软雅黑", 8.5F, FontStyle.Regular))
            using (Brush issuerBrush = new SolidBrush(Color.FromArgb(51, 51, 51)))
            using (Brush accountBrush = new SolidBrush(Color.FromArgb(128, 128, 128)))
            {
                g.DrawString(acc.Issuer, issuerFont, issuerBrush, e.Bounds.Left + 12, e.Bounds.Top + 10);
                
                string subText = string.IsNullOrEmpty(acc.Account) ? "无账号信息" : acc.Account;
                if (!string.IsNullOrEmpty(acc.Remark))
                {
                    subText += string.Format(" ({0})", acc.Remark);
                }
                g.DrawString(subText, accountFont, accountBrush, e.Bounds.Left + 12, e.Bounds.Top + 35);
            }

            // 5. 绘制验证码
            // 倒计时最后 5 秒变为红色报警
            Color codeColor = remainingSec <= 5 ? Color.FromArgb(231, 76, 60) : Color.FromArgb(0, 102, 204);
            using (Font codeFont = new Font("Consolas", 18F, FontStyle.Bold))
            using (Brush codeBrush = new SolidBrush(codeColor))
            {
                // 绘制在右侧
                SizeF codeSize = g.MeasureString(codeText, codeFont);
                float x = e.Bounds.Right - codeSize.Width - 15;
                float y = e.Bounds.Top + (e.Bounds.Height - codeSize.Height) / 2 - 2;
                g.DrawString(codeText, codeFont, codeBrush, x, y);
            }

            // 6. 绘制底部的倒计时滑动条 (进度条)
            float ratio = (float)remainingSec / period;
            int progressWidth = (int)(e.Bounds.Width * ratio);
            Color barColor = remainingSec <= 5 ? Color.FromArgb(231, 76, 60) : Color.FromArgb(46, 204, 113);
            using (Brush barBrush = new SolidBrush(barColor))
            {
                g.FillRectangle(barBrush, e.Bounds.Left, e.Bounds.Bottom - 4, progressWidth, 4);
            }
        }

        private void LstAccounts_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int index = lstAccounts.IndexFromPoint(e.Location);
                if (index >= 0 && index < _displayAccounts.Count)
                {
                    // 单击时自动复制验证码
                    CopyCode(_displayAccounts[index]);
                }
            }
        }

        private void LstAccounts_DoubleClick(object sender, EventArgs e)
        {
            // 双击进入编辑账户
            EditSelectedAccount();
        }

        private void LstAccounts_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedAccount();
            }
            else if (e.KeyCode == Keys.F2)
            {
                EditSelectedAccount();
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelectedCode();
                e.Handled = true;
            }
        }

        #region 操作方法

        private void AddNewAccount()
        {
            using (AccountEditForm form = new AccountEditForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    var newAcc = form.Account;
                    newAcc.SortOrder = _dataStore.Accounts.Count;
                    _dataStore.Accounts.Add(newAcc);
                    
                    SaveAndRefresh();
                    UpdateStatus("新增账户成功！");
                }
            }
        }

        private void EditSelectedAccount()
        {
            var acc = GetSelectedAccount();
            if (acc == null) return;

            using (AccountEditForm form = new AccountEditForm(acc))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    SaveAndRefresh();
                    UpdateStatus("编辑账户成功！");
                }
            }
        }

        private void DeleteSelectedAccount()
        {
            var acc = GetSelectedAccount();
            if (acc == null) return;

            var confirm = MessageBox.Show(
                string.Format("您确定要永久删除“{0}”({1}) 吗？\n该操作不可撤销，请确保已对该密钥进行备份！", acc.Issuer, acc.Account), 
                "确认删除", 
                MessageBoxButtons.YesNo, 
                MessageBoxIcon.Warning
            );

            if (confirm == DialogResult.Yes)
            {
                _dataStore.Accounts.Remove(acc);
                // 重新规划排序序号
                for (int i = 0; i < _dataStore.Accounts.Count; i++)
                {
                    _dataStore.Accounts[i].SortOrder = i;
                }

                SaveAndRefresh();
                UpdateStatus("已删除账户！");
            }
        }

        private void CopyCode(TotpAccount acc)
        {
            if (acc == null) return;

            try
            {
                byte[] secretBytes = Base32.Decode(acc.PlainSecret);
                string code = Totp.GenerateCode(secretBytes, DateTime.UtcNow, acc.Digits, acc.Period, acc.Algorithm);
                
                if (!string.IsNullOrEmpty(code))
                {
                    Clipboard.SetText(code);
                    UpdateStatus(string.Format("验证码 [{0}] 已复制到剪贴板！", code));

                    // 如果启用了复制后自动最小化
                    if (_dataStore.Settings.MinimizeAfterCopy)
                    {
                        this.WindowState = FormWindowState.Minimized;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("生成验证码失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopySelectedCode()
        {
            CopyCode(GetSelectedAccount());
        }

        private void CopySelectedSecret()
        {
            var acc = GetSelectedAccount();
            if (acc == null) return;

            try
            {
                string secret = acc.PlainSecret;
                if (!string.IsNullOrEmpty(secret))
                {
                    Clipboard.SetText(secret);
                    UpdateStatus("原始密钥已复制到剪贴板！请妥善保管。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取密钥失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartScreenScan()
        {
            // 1. 隐藏窗口
            this.Hide();
            
            // 2. 延迟 250ms 等待窗口完全隐藏，防止截图中包含主窗口
            Timer delayTimer = new Timer { Interval = 250 };
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                delayTimer.Dispose();

                try
                {
                    // 3. 截取全屏大图
                    Bitmap screenSnapshot = ScreenCapture.CaptureVirtualScreen();

                    // 4. 显示扫描窗体
                    using (ScreenScanForm scanForm = new ScreenScanForm(screenSnapshot))
                    {
                        scanForm.QrCodeScanned += (uriText) =>
                        {
                            // 扫码成功后的解析回调
                            ImportUri(uriText);
                        };

                        scanForm.ShowDialog();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("启动屏幕截屏失败: " + ex.Message, "截屏失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    // 5. 扫描结束后还原主窗口
                    this.Show();
                    this.Focus();
                }
            };
            delayTimer.Start();
        }

        private void ImportFromClipboard()
        {
            string text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("剪贴板中没有任何文本内容。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ImportUri(text);
        }

        private void ImportUri(string rawText)
        {
            if (string.IsNullOrEmpty(rawText)) return;

            string[] lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            int successCount = 0;
            int failCount = 0;

            foreach (var line in lines)
            {
                string cleanLine = line.Trim();
                if (string.IsNullOrEmpty(cleanLine)) continue;

                try
                {
                    OtpAuthUri parsed = OtpAuthUri.Parse(cleanLine);
                    
                    // 构造新的账户记录
                    TotpAccount acc = new TotpAccount
                    {
                        Issuer = parsed.Issuer ?? "未指定平台",
                        Account = parsed.Account ?? string.Empty,
                        PlainSecret = parsed.Secret,
                        Digits = parsed.Digits,
                        Period = parsed.Period,
                        Algorithm = parsed.Algorithm ?? "SHA1",
                        SortOrder = _dataStore.Accounts.Count
                    };

                    _dataStore.Accounts.Add(acc);
                    successCount++;
                }
                catch
                {
                    failCount++;
                }
            }

            if (successCount > 0)
            {
                SaveAndRefresh();
                UpdateStatus(string.Format("成功导入 {0} 个 2FA 账户！", successCount));
            }

            if (failCount > 0)
            {
                MessageBox.Show(
                    string.Format("导入完成，其中 {0} 条格式正确，{1} 条格式错误解析失败（要求为 otpauth:// 链接）。", successCount, failCount), 
                    "导入结果", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Warning
                );
            }
        }

        private void ShowSelectedQr()
        {
            var acc = GetSelectedAccount();
            if (acc == null) return;

            try
            {
                // 生成 otpauth URI
                OtpAuthUri auth = new OtpAuthUri
                {
                    Issuer = acc.Issuer,
                    Account = acc.Account,
                    Secret = acc.PlainSecret,
                    Digits = acc.Digits,
                    Period = acc.Period,
                    Algorithm = acc.Algorithm
                };

                string uriString = auth.ToUriString();

                // 生成二维码位图
                BarcodeWriter writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.QR_CODE
                };
                writer.Options = new ZXing.Common.EncodingOptions
                {
                    Width = 260,
                    Height = 260,
                    Margin = 1
                };

                using (Bitmap qrBmp = writer.Write(uriString))
                {
                    // 在一个弹出窗口里展示该图片，并提供保存功能
                    ShowQrDialog(acc.Issuer + " - " + acc.Account, qrBmp, uriString);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("生成二维码图片失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowQrDialog(string title, Bitmap qrImage, string uriText)
        {
            Form qrForm = new Form
            {
                Text = "查看二维码 - " + title,
                Size = new Size(320, 400),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.White,
                Font = new Font("微软雅黑", 9F)
            };

            PictureBox pic = new PictureBox
            {
                Image = new Bitmap(qrImage), // 克隆一份，防止释放冲突
                Size = new Size(260, 260),
                Location = new Point(22, 15),
                SizeMode = PictureBoxSizeMode.Zoom
            };

            Button btnSave = new Button
            {
                Text = "保存二维码",
                Location = new Point(20, 290),
                Size = new Size(130, 30),
                BackColor = Color.FromArgb(0, 162, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) =>
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "PNG图片|*.png";
                    sfd.FileName = title.Replace(":", "_").Replace(" ", "") + "_2FA.png";
                    if (sfd.ShowDialog(qrForm) == DialogResult.OK)
                    {
                        try
                        {
                            pic.Image.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                            MessageBox.Show("二维码图片已成功保存！", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("保存失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            };

            Button btnCopyUri = new Button
            {
                Text = "复制otpauth链接",
                Location = new Point(160, 290),
                Size = new Size(137, 30),
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat
            };
            btnCopyUri.FlatAppearance.BorderSize = 0;
            btnCopyUri.Click += (s, e) =>
            {
                Clipboard.SetText(uriText);
                MessageBox.Show("otpauth 链接已成功复制到剪贴板！", "复制成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            qrForm.Controls.Add(pic);
            qrForm.Controls.Add(btnSave);
            qrForm.Controls.Add(btnCopyUri);
            qrForm.ShowDialog(this);
        }

        #endregion

        #region 导入导出数据

        private void ImportJson()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "2FA 数据文件 (*.2fa;*.json)|*.2fa;*.json|所有文件 (*.*)|*.*";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string json = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                        JavaScriptSerializer ser = new JavaScriptSerializer();
                        
                        List<TotpAccount> imported = null;
                        try
                        {
                            imported = ser.Deserialize<List<TotpAccount>>(json);
                        }
                        catch { }

                        // 兼容某些工具直接导出数组或嵌套对象的情形
                        if (imported == null)
                        {
                            // 尝试作为包装对象解析（某些软件的备份格式）
                            var wrapper = ser.Deserialize<Dictionary<string, object>>(json);
                            if (wrapper != null && wrapper.ContainsKey("accounts"))
                            {
                                string accountsJson = ser.Serialize(wrapper["accounts"]);
                                imported = ser.Deserialize<List<TotpAccount>>(accountsJson);
                            }
                        }

                        if (imported == null || imported.Count == 0)
                        {
                            MessageBox.Show("无法从所选文件中解析出有效的 2FA 账户列表。", "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // 检测重复或合并
                        int addedCount = 0;
                        foreach (var imp in imported)
                        {
                            if (string.IsNullOrEmpty(imp.Issuer) || string.IsNullOrEmpty(imp.EncryptedSecret))
                            {
                                continue;
                            }

                            // 重新生成 ID 防冲突
                            imp.Id = Guid.NewGuid().ToString();
                            imp.SortOrder = _dataStore.Accounts.Count;
                            
                            _dataStore.Accounts.Add(imp);
                            addedCount++;
                        }

                        if (addedCount > 0)
                        {
                            SaveAndRefresh();
                            UpdateStatus(string.Format("成功合并导入了 {0} 个账户！", addedCount));
                        }
                        else
                        {
                            MessageBox.Show("文件中未包含任何符合标准的账户配置信息。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("导入 JSON 数据文件时发生错误: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportJson()
        {
            if (_dataStore.Accounts.Count == 0)
            {
                MessageBox.Show("当前没有任何 2FA 账户数据，无需导出。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "2FA 数据文件|*.2fa|JSON文件|*.json";
                sfd.FileName = "Authenticator_Backup.2fa";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        JavaScriptSerializer ser = new JavaScriptSerializer();
                        string json = ser.Serialize(_dataStore.Accounts);
                        
                        File.WriteAllText(sfd.FileName, json, Encoding.UTF8);
                        MessageBox.Show("账户数据已安全导出到本地！\n\n警告：备份文件中包含经过本机安全机制加密的密钥数据，仅支持在本台电脑、本 Windows 账号下重新导入还原。", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("导出数据文件失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ImportCsv()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSV文件 (*.csv)|*.csv";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        int success = 0;
                        using (StreamReader sr = new StreamReader(ofd.FileName, Encoding.Default))
                        {
                            string headerLine = sr.ReadLine(); // 读取表头
                            string line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                if (string.IsNullOrEmpty(line.Trim())) continue;

                                // 简单按逗号切分，不支持处理包含逗号的单元格引号包装，一般密钥是不含逗号的
                                string[] fields = line.Split(',');
                                if (fields.Length >= 3)
                                {
                                    string issuer = fields[0].Trim();
                                    string accountName = fields[1].Trim();
                                    string secret = fields[2].Trim().Replace(" ", "").ToUpperInvariant();
                                    string remark = fields.Length > 3 ? fields[3].Trim() : string.Empty;

                                    if (!string.IsNullOrEmpty(issuer) && !string.IsNullOrEmpty(secret))
                                    {
                                        try
                                        {
                                            // 简单测试 Base32 校验
                                            Base32.Decode(secret);

                                            TotpAccount acc = new TotpAccount
                                            {
                                                Issuer = issuer,
                                                Account = accountName,
                                                PlainSecret = secret,
                                                Remark = remark,
                                                SortOrder = _dataStore.Accounts.Count
                                            };
                                            _dataStore.Accounts.Add(acc);
                                            success++;
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }

                        if (success > 0)
                        {
                            SaveAndRefresh();
                            UpdateStatus(string.Format("成功合并导入了 {0} 个 CSV 账户！", success));
                        }
                        else
                        {
                            MessageBox.Show("未能从该 CSV 中解析出任何有效的 2FA 账户，请确保格式正确（字段顺序：服务名,账号,密钥,备注）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("读取 CSV 文件失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportCsv()
        {
            if (_dataStore.Accounts.Count == 0)
            {
                MessageBox.Show("当前没有任何 2FA 账户数据，无需导出。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV数据文件|*.csv";
                sfd.FileName = "Authenticator_Backup.csv";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("服务商名称,账号,密钥(未加密明文),备注");
                        
                        foreach (var acc in _dataStore.Accounts)
                        {
                            sb.AppendFormat("{0},{1},{2},{3}\r\n", 
                                EscapeCsv(acc.Issuer), 
                                EscapeCsv(acc.Account), 
                                EscapeCsv(acc.PlainSecret), 
                                EscapeCsv(acc.Remark)
                            );
                        }

                        File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        MessageBox.Show("明文数据已导出到 CSV！\n\n⚠️ 重要警示：导出的 CSV 文件中包含未加密的 2FA 明文密钥，任何获取该文件的第三方都将能克隆您的所有验证码！请务必将其妥善封存，阅后即消毁！", "安全警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("写入 CSV 失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private string EscapeCsv(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            if (str.Contains(",") || str.Contains("\"") || str.Contains("\r") || str.Contains("\n"))
            {
                return "\"" + str.Replace("\"", "\"\"") + "\"";
            }
            return str;
        }

        #endregion

        #region 辅助方法

        private void OpenSettings()
        {
            using (SettingsForm form = new SettingsForm(_dataStore.Settings))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    ApplyWindowSettings();
                    _dataStore.SaveSettings();
                    UpdateStatus("全局配置已更新");
                }
            }
        }

        private void SaveAndRefresh()
        {
            _dataStore.SaveAccounts();
            RefreshDisplayList();
        }

        private TotpAccount GetSelectedAccount()
        {
            if (lstAccounts.SelectedIndex >= 0 && lstAccounts.SelectedIndex < lstAccounts.Items.Count)
            {
                return lstAccounts.Items[lstAccounts.SelectedIndex] as TotpAccount;
            }
            return null;
        }

        private void UpdateStatus(string message)
        {
            lblStatus.Text = message;
        }

        #endregion
    }
}
