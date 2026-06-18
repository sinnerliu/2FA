using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TwoFA.Models;

namespace TwoFA.UI
{
    /// <summary>
    /// 全局设置对话框窗体
    /// </summary>
    public class SettingsForm : Form
    {
        private CheckBox chkMinimizeAfterCopy;
        private CheckBox chkTopMost;
        private CheckBox chkAutoStart;

        private Label lblDataPath;
        private TextBox txtDataPath;
        private Button btnBrowsePath;

        private Button btnSave;
        private Button btnCancel;

        private readonly AppSettings _settings;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            InitializeComponent();
            LoadSettingsData();
        }

        private void InitializeComponent()
        {
            this.Text = "参数设置";
            this.Size = new Size(380, 275);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("微软雅黑", 9F);
            this.BackColor = Color.FromArgb(245, 245, 245);

            int startY = 20;
            int gap = 30;

            // 1. 各项复选框
            chkMinimizeAfterCopy = new CheckBox
            {
                Text = "成功复制验证码后自动最小化窗口",
                Location = new Point(30, startY),
                Size = new Size(300, 22),
                Cursor = Cursors.Hand
            };
            this.Controls.Add(chkMinimizeAfterCopy);

            startY += gap;
            chkTopMost = new CheckBox
            {
                Text = "主窗口始终保持置顶显示",
                Location = new Point(30, startY),
                Size = new Size(300, 22),
                Cursor = Cursors.Hand
            };
            this.Controls.Add(chkTopMost);

            startY += gap;
            chkAutoStart = new CheckBox
            {
                Text = "开机随 Windows 系统自动启动",
                Location = new Point(30, startY),
                Size = new Size(300, 22),
                Cursor = Cursors.Hand
            };
            this.Controls.Add(chkAutoStart);

            // 2. 自定义数据存储目录
            startY += gap + 10;
            lblDataPath = new Label
            {
                Text = "数据存储目录（为空则使用默认程序目录）：",
                Location = new Point(30, startY),
                Size = new Size(300, 18)
            };
            this.Controls.Add(lblDataPath);

            startY += 20;
            txtDataPath = new TextBox
            {
                Location = new Point(30, startY),
                Size = new Size(220, 23),
                ReadOnly = true
            };
            btnBrowsePath = new Button
            {
                Text = "浏览...",
                Location = new Point(260, startY - 1),
                Size = new Size(75, 25),
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat
            };
            btnBrowsePath.FlatAppearance.BorderSize = 0;
            btnBrowsePath.Click += BtnBrowsePath_Click;

            this.Controls.Add(txtDataPath);
            this.Controls.Add(btnBrowsePath);

            // 3. 保存和取消按钮
            startY += 45;
            btnSave = new Button
            {
                Text = "保存设置",
                Location = new Point(165, startY),
                Size = new Size(85, 30),
                BackColor = Color.FromArgb(0, 162, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(260, startY),
                Size = new Size(85, 30),
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void LoadSettingsData()
        {
            if (_settings != null)
            {
                chkMinimizeAfterCopy.Checked = _settings.MinimizeAfterCopy;
                chkTopMost.Checked = _settings.TopMost;
                chkAutoStart.Checked = _settings.AutoStart;
                txtDataPath.Text = _settings.DataFilePath;
            }
        }

        private void BtnBrowsePath_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "请选择 2FA 账户数据文件的存储目录";
                fbd.ShowNewFolderButton = true;
                
                if (!string.IsNullOrEmpty(txtDataPath.Text) && Directory.Exists(txtDataPath.Text))
                {
                    fbd.SelectedPath = txtDataPath.Text;
                }

                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    txtDataPath.Text = fbd.SelectedPath;
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            _settings.MinimizeOnStart = false; // 彻底移除托盘功能
            _settings.MinimizeAfterCopy = chkMinimizeAfterCopy.Checked;
            _settings.TopMost = chkTopMost.Checked;
            _settings.AutoStart = chkAutoStart.Checked;
            _settings.DataFilePath = txtDataPath.Text.Trim();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
