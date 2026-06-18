using System;
using System.Drawing;
using System.Windows.Forms;

namespace TwoFA.UI
{
    public partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private Panel pnlHeader;
        private Button btnAdd;
        private Button btnClipboardImport;
        private Button btnScreenScan;
        private Button btnMoreMenu;

        // 搜索 Panel
        private Panel pnlSearch;
        private TextBox txtSearch;
        private Label lblSearchIcon;

        // 列表控件
        private DoubleBufferedListBox lstAccounts;

        // 状态栏
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;

        // 右键菜单
        private ContextMenuStrip ctxMenu;
        private ToolStripMenuItem mnuCopyCode;
        private ToolStripMenuItem mnuCopySecret;
        private ToolStripMenuItem mnuEdit;
        private ToolStripMenuItem mnuDelete;
        private ToolStripSeparator mnuSeparator1;
        private ToolStripMenuItem mnuShowQr;

        // 更多设置与导入导出下拉快捷菜单
        private ContextMenuStrip ctxMoreMenu;
        private ToolStripMenuItem mnuImportJson;
        private ToolStripMenuItem mnuExportJson;
        private ToolStripSeparator mnuSeparator2;
        private ToolStripMenuItem mnuImportCsv;
        private ToolStripMenuItem mnuExportCsv;
        private ToolStripSeparator mnuSeparator3;
        private ToolStripMenuItem mnuOpenSettings;

        // 全局刷新定时器
        private Timer tmrRefresh;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            
            // 窗体属性
            this.Text = "2FA 安全验证器";
            this.Size = new Size(374, 660);
            this.MinimumSize = new Size(320, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.BackColor = Color.FromArgb(240, 243, 246);

            // 1. 顶栏 Panel (操作按钮)
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(41, 57, 85) // 深色雅致蓝
            };

            int btnHeight = 34;
            int topOffset = 8;

            btnAdd = CreateFlatButton("新增", new Point(8, topOffset), new Size(75, btnHeight), Color.FromArgb(0, 162, 232));
            btnClipboardImport = CreateFlatButton("导入剪贴板", new Point(88, topOffset), new Size(115, btnHeight), Color.FromArgb(80, 95, 120));
            btnScreenScan = CreateFlatButton("屏幕扫码", new Point(208, topOffset), new Size(110, btnHeight), Color.FromArgb(34, 177, 76));
            btnMoreMenu = CreateFlatButton("⚙", new Point(324, topOffset), new Size(40, btnHeight), Color.FromArgb(80, 95, 120));
            
            btnMoreMenu.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            pnlHeader.Controls.Add(btnAdd);
            pnlHeader.Controls.Add(btnClipboardImport);
            pnlHeader.Controls.Add(btnScreenScan);
            pnlHeader.Controls.Add(btnMoreMenu);

            // 2. 搜索框 Panel
            pnlSearch = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.White,
                Padding = new Padding(8, 7, 8, 7)
            };

            lblSearchIcon = new Label
            {
                Text = "🔍",
                Size = new Size(20, 26),
                Location = new Point(12, 9),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray
            };

            txtSearch = new TextBox
            {
                Location = new Point(35, 8),
                Width = 390,
                BorderStyle = BorderStyle.None,
                Font = new Font("微软雅黑", 10F),
                ForeColor = Color.Gray
            };
            // 搜索框宽度自适应
            txtSearch.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            // 绘制底边灰色横线，使其像现代输入框
            pnlSearch.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(220, 224, 230), 1))
                {
                    e.Graphics.DrawLine(pen, 0, pnlSearch.Height - 1, pnlSearch.Width, pnlSearch.Height - 1);
                }
            };

            pnlSearch.Controls.Add(lblSearchIcon);
            pnlSearch.Controls.Add(txtSearch);

            // 3. 数据列表 (双缓冲)
            lstAccounts = new DoubleBufferedListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                ItemHeight = 72,
                DrawMode = DrawMode.OwnerDrawFixed,
                IntegralHeight = false
            };

            // 4. 状态栏
            statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(240, 243, 246)
            };
            lblStatus = new ToolStripStatusLabel
            {
                Text = "准备就绪 | 单击条目复制验证码"
            };
            statusStrip.Items.Add(lblStatus);

            // 5. 右键上下文菜单
            ctxMenu = new ContextMenuStrip(components);
            mnuCopyCode = new ToolStripMenuItem("复制验证码 (Ctrl+C)", null, (s, e) => CopySelectedCode());
            mnuCopySecret = new ToolStripMenuItem("复制原始密钥", null, (s, e) => CopySelectedSecret());
            mnuEdit = new ToolStripMenuItem("编辑账户 (F2)", null, (s, e) => EditSelectedAccount());
            mnuDelete = new ToolStripMenuItem("删除账户 (Delete)", null, (s, e) => DeleteSelectedAccount());
            mnuSeparator1 = new ToolStripSeparator();
            mnuShowQr = new ToolStripMenuItem("显示二维码图片", null, (s, e) => ShowSelectedQr());

            ctxMenu.Items.AddRange(new ToolStripItem[] {
                mnuCopyCode,
                mnuCopySecret,
                mnuSeparator1,
                mnuEdit,
                mnuDelete,
                mnuShowQr
            });
            lstAccounts.ContextMenuStrip = ctxMenu;

            // 6. 更多设置与导入导出下拉快捷菜单
            ctxMoreMenu = new ContextMenuStrip(components);
            mnuImportJson = new ToolStripMenuItem("📥 导入 JSON 数据...", null, (s, e) => ImportJson());
            mnuExportJson = new ToolStripMenuItem("📤 导出 JSON 数据...", null, (s, e) => ExportJson());
            mnuSeparator2 = new ToolStripSeparator();
            mnuImportCsv = new ToolStripMenuItem("📄 导入 CSV 文件...", null, (s, e) => ImportCsv());
            mnuExportCsv = new ToolStripMenuItem("📄 导出 CSV 文件...", null, (s, e) => ExportCsv());
            mnuSeparator3 = new ToolStripSeparator();
            mnuOpenSettings = new ToolStripMenuItem("⚙️ 软件参数设置...", null, (s, e) => OpenSettings());

            ctxMoreMenu.Items.AddRange(new ToolStripItem[] {
                mnuImportJson,
                mnuExportJson,
                mnuSeparator2,
                mnuImportCsv,
                mnuExportCsv,
                mnuSeparator3,
                mnuOpenSettings
            });

            // 绑定齿轮按钮的点击，使其在下方弹出菜单
            btnMoreMenu.Click += (s, e) =>
            {
                ctxMoreMenu.Show(btnMoreMenu, new Point(btnMoreMenu.Width - ctxMoreMenu.Width, btnMoreMenu.Height));
            };

            // 7. 定时器（一秒一跳，更新倒计时和重绘）
            tmrRefresh = new Timer(components)
            {
                Interval = 1000,
                Enabled = true
            };

            // 页面元素添加
            this.Controls.Add(lstAccounts);
            this.Controls.Add(pnlSearch);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(statusStrip);
        }

        /// <summary>
        /// 创建扁平风格的自定义外观按钮
        /// </summary>
        private Button CreateFlatButton(string text, Point loc, Size size, Color backColor)
        {
            Button btn = new Button
            {
                Text = text,
                Location = loc,
                Size = size,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            return btn;
        }
    }

    /// <summary>
    /// 支持双缓冲的 ListBox 控件，防止每秒刷新倒计时时界面闪烁
    /// </summary>
    public class DoubleBufferedListBox : ListBox
    {
        /// <summary>
        /// 是否是由定时器 Tick 触发的倒计时刷新事件中
        /// </summary>
        public bool IsTimerRefreshing { get; set; }

        public DoubleBufferedListBox()
        {
            // 开启控件双缓冲
            this.DoubleBuffered = true;
        }

        protected override void WndProc(ref Message m)
        {
            // 0x0014 是 WM_ERASEBKGND 消息
            if (m.Msg == 0x0014)
            {
                // 只有在倒计时定时刷新期间，才拦截背景擦除以防止闪烁
                if (IsTimerRefreshing)
                {
                    m.Result = (IntPtr)1; // 已手工处理
                    return;
                }
            }

            base.WndProc(ref m);

            // 0x000F 是 WM_PAINT 消息，说明当前重绘动作已完成
            if (m.Msg == 0x000F)
            {
                IsTimerRefreshing = false; // 重置定时刷新状态
            }
        }
    }
}
