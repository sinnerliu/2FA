using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TwoFA.QrCode;
using ZXing;

namespace TwoFA.UI
{
    /// <summary>
    /// 全屏扫描遮罩窗体，提供半透明背景、鼠标矩形框选和二维码扫描识别功能
    /// </summary>
    public class ScreenScanForm : Form
    {
        private readonly Bitmap _screenSnapshot;
        private Point _startPoint;
        private Point _currentPoint;
        private bool _isDrawing;
        private Rectangle _selectionRect;

        // 扫码成功的回调事件
        public event Action<string> QrCodeScanned;

        public ScreenScanForm(Bitmap snapshot)
        {
            _screenSnapshot = snapshot;
            
            // 基础窗口设置
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Cross;
            
            // 覆盖整个虚拟屏幕（支持多显示器）
            Rectangle bounds = SystemInformation.VirtualScreen;
            this.Bounds = bounds;

            // 监听键盘，按 ESC 退出
            this.KeyPreview = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isDrawing = true;
                _startPoint = e.Location;
                _currentPoint = e.Location;
                _selectionRect = Rectangle.Empty;
            }
            else if (e.Button == MouseButtons.Right)
            {
                // 右键取消扫描
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDrawing)
            {
                _currentPoint = e.Location;
                
                // 计算当前框选的矩形
                int x = Math.Min(_startPoint.X, _currentPoint.X);
                int y = Math.Min(_startPoint.Y, _currentPoint.Y);
                int w = Math.Abs(_startPoint.X - _currentPoint.X);
                int h = Math.Abs(_startPoint.Y - _currentPoint.Y);
                _selectionRect = new Rectangle(x, y, w, h);
                
                this.Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && _isDrawing)
            {
                _isDrawing = false;
                
                // 如果划定的区域大于一定面积，进行扫码
                if (_selectionRect.Width > 10 && _selectionRect.Height > 10)
                {
                    this.Hide(); // 识别前先隐藏，防止遮挡提示
                    ProcessScan();
                }
                else
                {
                    // 只是点击，清除选择
                    _selectionRect = Rectangle.Empty;
                    this.Invalidate();
                }
            }
        }

        private void ProcessScan()
        {
            Bitmap cropped = null;
            try
            {
                // 裁剪出框选的位图
                cropped = ScreenCapture.CropImage(_screenSnapshot, _selectionRect);

                // 解码二维码
                string result = DecodeQrCode(cropped);
                if (!string.IsNullOrEmpty(result))
                {
                    if (QrCodeScanned != null)
                    {
                        QrCodeScanned(result);
                    }
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("该区域未识别到有效的二维码，请重新框选。", "扫描失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.Show(); // 重新显示，允许用户再次尝试
                    _selectionRect = Rectangle.Empty;
                    this.Invalidate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("扫描发生错误: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
            finally
            {
                if (cropped != null)
                {
                    cropped.Dispose();
                }
            }
        }

        private string DecodeQrCode(Bitmap bmp)
        {
            try
            {
                BarcodeReader reader = new BarcodeReader();
                reader.Options.PossibleFormats = new[] { BarcodeFormat.QR_CODE };
                reader.Options.TryHarder = true;
                
                var result = reader.Decode(bmp);
                return result != null ? result.Text : null;
            }
            catch
            {
                return null;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            
            // 1. 绘制截屏底层大图
            if (_screenSnapshot != null)
            {
                g.DrawImage(_screenSnapshot, 0, 0);
            }

            // 2. 绘制半透明黑色遮罩，排除用户选中的矩形区域
            using (Brush maskBrush = new SolidBrush(Color.FromArgb(140, 0, 0, 0)))
            {
                if (_selectionRect != Rectangle.Empty)
                {
                    g.ExcludeClip(_selectionRect);
                }
                g.FillRectangle(maskBrush, this.ClientRectangle);
                g.ResetClip();
            }

            // 3. 如果正在绘制或已画出选区，绘制选区的边框和高亮
            if (_selectionRect != Rectangle.Empty)
            {
                // 绘制精致的亮蓝色选区边框
                using (Pen borderPen = new Pen(Color.FromArgb(0, 162, 232), 2))
                {
                    borderPen.DashStyle = DashStyle.Solid;
                    g.DrawRectangle(borderPen, _selectionRect);
                }

                // 绘制选框的四个角点，让界面看起来像扫描器
                int cornerLen = 15;
                using (Pen cornerPen = new Pen(Color.FromArgb(0, 255, 0), 3)) // 亮绿色角
                {
                    Rectangle r = _selectionRect;
                    // 左上
                    g.DrawLine(cornerPen, r.Left - 1, r.Top - 1, r.Left + cornerLen, r.Top - 1);
                    g.DrawLine(cornerPen, r.Left - 1, r.Top - 1, r.Left - 1, r.Top + cornerLen);
                    // 右上
                    g.DrawLine(cornerPen, r.Right + 1, r.Top - 1, r.Right - cornerLen, r.Top - 1);
                    g.DrawLine(cornerPen, r.Right + 1, r.Top - 1, r.Right + 1, r.Top + cornerLen);
                    // 左下
                    g.DrawLine(cornerPen, r.Left - 1, r.Bottom + 1, r.Left + cornerLen, r.Bottom + 1);
                    g.DrawLine(cornerPen, r.Left - 1, r.Bottom + 1, r.Left - 1, r.Bottom - cornerLen);
                    // 右下
                    g.DrawLine(cornerPen, r.Right + 1, r.Bottom + 1, r.Right - cornerLen, r.Bottom + 1);
                    g.DrawLine(cornerPen, r.Right + 1, r.Bottom + 1, r.Right + 1, r.Bottom - cornerLen);
                }

                // 显示框选大小和提示信息
                string infoText = string.Format("框选大小: {0} x {1} | 松开鼠标开始识别", _selectionRect.Width, _selectionRect.Height);
                using (Font infoFont = new Font("微软雅黑", 9F, FontStyle.Bold))
                using (Brush infoBrush = new SolidBrush(Color.White))
                using (Brush bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                {
                    SizeF textSize = g.MeasureString(infoText, infoFont);
                    int x = _selectionRect.Left;
                    int y = _selectionRect.Bottom + 5;
                    
                    // 如果提示文本超出屏幕下方，绘制在选框上方
                    if (y + textSize.Height > this.Height)
                    {
                        y = _selectionRect.Top - (int)textSize.Height - 5;
                    }
                    
                    g.FillRectangle(bgBrush, x, y, textSize.Width + 8, textSize.Height + 4);
                    g.DrawString(infoText, infoFont, infoBrush, x + 4, y + 2);
                }
            }
            else
            {
                // 绘制全局指引文本
                string helpText = "按住鼠标左键并拖拽以框选屏幕上的 2FA 二维码 | 右键或按 ESC 退出扫描";
                using (Font helpFont = new Font("微软雅黑", 12F, FontStyle.Bold))
                using (Brush helpBrush = new SolidBrush(Color.White))
                using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                {
                    SizeF textSize = g.MeasureString(helpText, helpFont);
                    float x = (this.Width - textSize.Width) / 2;
                    float y = 50; // 顶部偏下位置
                    
                    g.FillRectangle(bgBrush, x - 10, y - 6, textSize.Width + 20, textSize.Height + 12);
                    g.DrawString(helpText, helpFont, helpBrush, x, y);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_screenSnapshot != null)
                {
                    _screenSnapshot.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
