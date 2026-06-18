using System;
using System.Drawing;
using System.Windows.Forms;
using TwoFA.Core;
using TwoFA.Models;

namespace TwoFA.UI
{
    /// <summary>
    /// 手动新增或编辑 2FA 账户属性的对话框窗体
    /// </summary>
    public class AccountEditForm : Form
    {
        private Label lblIssuer;
        private TextBox txtIssuer;
        private Label lblAccount;
        private TextBox txtAccount;
        private Label lblSecret;
        private TextBox txtSecret;
        private Label lblRemark;
        private TextBox txtRemark;

        private GroupBox grpAdvanced;
        private Label lblDigits;
        private ComboBox cmbDigits;
        private Label lblPeriod;
        private ComboBox cmbPeriod;
        private Label lblAlgorithm;
        private ComboBox cmbAlgorithm;

        private Button btnOk;
        private Button btnCancel;

        // 保存编辑的账户实体
        public TotpAccount Account { get; private set; }
        private readonly bool _isEditMode;
        private readonly string _originalSecret;

        public AccountEditForm() : this(null)
        {
        }

        public AccountEditForm(TotpAccount account)
        {
            if (account != null)
            {
                Account = account;
                _isEditMode = true;
                _originalSecret = account.PlainSecret;
            }
            else
            {
                Account = new TotpAccount();
                _isEditMode = false;
                _originalSecret = string.Empty;
            }

            InitializeComponent();
            LoadAccountData();
        }

        private void InitializeComponent()
        {
            // 窗体基础属性
            this.Text = _isEditMode ? "编辑账户" : "新增账户";
            this.Size = new Size(380, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.BackColor = Color.FromArgb(245, 245, 245);

            int startY = 20;
            int gap = 45;
            int labelWidth = 65;
            int inputWidth = 260;

            // 1. 服务商
            lblIssuer = new Label { Text = "服务商*:", Location = new Point(20, startY), Size = new Size(labelWidth, 25), TextAlign = ContentAlignment.MiddleRight };
            txtIssuer = new TextBox { Location = new Point(20 + labelWidth + 5, startY), Size = new Size(inputWidth, 25) };
            this.Controls.Add(lblIssuer);
            this.Controls.Add(txtIssuer);

            // 2. 账号
            startY += gap;
            lblAccount = new Label { Text = "账号/邮箱:", Location = new Point(20, startY), Size = new Size(labelWidth, 25), TextAlign = ContentAlignment.MiddleRight };
            txtAccount = new TextBox { Location = new Point(20 + labelWidth + 5, startY), Size = new Size(inputWidth, 25) };
            this.Controls.Add(lblAccount);
            this.Controls.Add(txtAccount);

            // 3. 密钥
            startY += gap;
            lblSecret = new Label { Text = "密钥*:", Location = new Point(20, startY), Size = new Size(labelWidth, 25), TextAlign = ContentAlignment.MiddleRight };
            txtSecret = new TextBox { Location = new Point(20 + labelWidth + 5, startY), Size = new Size(inputWidth, 25), CharacterCasing = CharacterCasing.Upper };
            // 输入密钥时自动滤除空格和中划线
            txtSecret.TextChanged += TxtSecret_TextChanged;
            this.Controls.Add(lblSecret);
            this.Controls.Add(txtSecret);

            // 4. 备注
            startY += gap;
            lblRemark = new Label { Text = "备注说明:", Location = new Point(20, startY), Size = new Size(labelWidth, 25), TextAlign = ContentAlignment.MiddleRight };
            txtRemark = new TextBox { Location = new Point(20 + labelWidth + 5, startY), Size = new Size(inputWidth, 25) };
            this.Controls.Add(lblRemark);
            this.Controls.Add(txtRemark);

            // 5. 高级选项折叠框
            startY += gap + 5;
            grpAdvanced = new GroupBox { Text = "高级参数（若不确定请保持默认）", Location = new Point(20, startY), Size = new Size(labelWidth + inputWidth + 5, 120) };
            
            lblDigits = new Label { Text = "位数:", Location = new Point(15, 25), Size = new Size(50, 25), TextAlign = ContentAlignment.MiddleRight };
            cmbDigits = new ComboBox { Location = new Point(70, 25), Size = new Size(80, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDigits.Items.AddRange(new object[] { 6, 7, 8 });
            cmbDigits.SelectedIndex = 0; // 默认6位

            lblPeriod = new Label { Text = "周期(秒):", Location = new Point(160, 25), Size = new Size(60, 25), TextAlign = ContentAlignment.MiddleRight };
            cmbPeriod = new ComboBox { Location = new Point(225, 25), Size = new Size(80, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPeriod.Items.AddRange(new object[] { 15, 30, 60 });
            cmbPeriod.SelectedIndex = 1; // 默认30秒

            lblAlgorithm = new Label { Text = "哈希算法:", Location = new Point(15, 70), Size = new Size(60, 25), TextAlign = ContentAlignment.MiddleRight };
            cmbAlgorithm = new ComboBox { Location = new Point(80, 70), Size = new Size(120, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAlgorithm.Items.AddRange(new object[] { "SHA1", "SHA256", "SHA512" });
            cmbAlgorithm.SelectedIndex = 0; // 默认SHA1

            grpAdvanced.Controls.Add(lblDigits);
            grpAdvanced.Controls.Add(cmbDigits);
            grpAdvanced.Controls.Add(lblPeriod);
            grpAdvanced.Controls.Add(cmbPeriod);
            grpAdvanced.Controls.Add(lblAlgorithm);
            grpAdvanced.Controls.Add(cmbAlgorithm);
            this.Controls.Add(grpAdvanced);

            // 6. 确定 / 取消按钮
            startY += 135;
            btnOk = new Button { Text = "保存", Location = new Point(175, startY), Size = new Size(85, 32), BackColor = Color.FromArgb(0, 162, 232), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += BtnOk_Click;

            btnCancel = new Button { Text = "取消", Location = new Point(275, startY), Size = new Size(85, 32), BackColor = Color.LightGray, FlatStyle = FlatStyle.Flat };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void TxtSecret_TextChanged(object sender, EventArgs e)
        {
            // 自动移除非法字符，不影响光标位置的多余替换
            string input = txtSecret.Text;
            string cleaned = input.Replace(" ", "").Replace("-", "").ToUpperInvariant();
            if (input != cleaned)
            {
                int cursor = txtSecret.SelectionStart - (input.Length - cleaned.Length);
                txtSecret.Text = cleaned;
                txtSecret.SelectionStart = Math.Max(0, cursor);
            }
        }

        private void LoadAccountData()
        {
            if (_isEditMode && Account != null)
            {
                txtIssuer.Text = Account.Issuer;
                txtAccount.Text = Account.Account;
                txtSecret.Text = _originalSecret;
                txtRemark.Text = Account.Remark;

                cmbDigits.SelectedItem = Account.Digits;
                cmbPeriod.SelectedItem = Account.Period;
                cmbAlgorithm.SelectedItem = Account.Algorithm;
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            string issuer = txtIssuer.Text.Trim();
            string accountName = txtAccount.Text.Trim();
            string secret = txtSecret.Text.Trim();
            string remark = txtRemark.Text.Trim();

            // 校验必填项
            if (string.IsNullOrEmpty(issuer))
            {
                MessageBox.Show("“服务商”为必填项！", "数据验证", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtIssuer.Focus();
                return;
            }

            if (string.IsNullOrEmpty(secret))
            {
                MessageBox.Show("“密钥”为必填项！", "数据验证", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSecret.Focus();
                return;
            }

            // 验证 Base32 密钥的合法性
            try
            {
                byte[] tempBytes = Base32.Decode(secret);
                if (tempBytes == null || tempBytes.Length == 0)
                {
                    throw new Exception("解析得到的字节数组为空");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("密钥格式无效，必须是合法的 Base32 字符串（由 A-Z 和 2-7 组成）！\n原因: " + ex.Message, "密钥格式错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtSecret.Focus();
                return;
            }

            // 如果是编辑模式，且修改了密钥，进行提示确认
            if (_isEditMode && !string.Equals(secret, _originalSecret, StringComparison.OrdinalIgnoreCase))
            {
                var confirmResult = MessageBox.Show("您修改了已保存的 2FA 密钥！\n密钥错误会导致您生成的验证码失效而无法登录。您确定要修改吗？", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirmResult != DialogResult.Yes)
                {
                    return;
                }
            }

            // 填充实体
            Account.Issuer = issuer;
            Account.Account = accountName;
            Account.PlainSecret = secret; // 设置明文，内部会自动转换为 EncryptedSecret
            Account.Remark = remark;
            Account.Digits = (int)cmbDigits.SelectedItem;
            Account.Period = (int)cmbPeriod.SelectedItem;
            Account.Algorithm = (string)cmbAlgorithm.SelectedItem;
            Account.UpdatedAt = DateTime.Now;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
