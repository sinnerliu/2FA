using System;
using System.Drawing;
using System.Windows.Forms;

namespace TwoFA.QrCode
{
    /// <summary>
    /// 屏幕截图与图像处理工具类，支持多显示器全屏捕获及区域裁剪
    /// </summary>
    public static class ScreenCapture
    {
        /// <summary>
        /// 捕获整个虚拟屏幕（包含所有显示器拼合的大图）
        /// </summary>
        /// <returns>返回全屏位图</returns>
        public static Bitmap CaptureVirtualScreen()
        {
            Rectangle bounds = SystemInformation.VirtualScreen;
            
            // 创建大位图
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // 从屏幕中拷贝图像，支持从负坐标（主屏左侧/上方的副屏）开始拷贝
                g.CopyFromScreen(
                    bounds.Left, 
                    bounds.Top, 
                    0, 
                    0, 
                    bounds.Size, 
                    CopyPixelOperation.SourceCopy
                );
            }
            return bitmap;
        }

        /// <summary>
        /// 裁剪指定位图中的某一矩形区域
        /// </summary>
        /// <param name="srcBitmap">原图</param>
        /// <param name="cropRect">裁剪区域（相对于原图坐标系）</param>
        /// <returns>裁剪出的局部位图</returns>
        public static Bitmap CropImage(Bitmap srcBitmap, Rectangle cropRect)
        {
            if (srcBitmap == null)
            {
                throw new ArgumentNullException("srcBitmap");
            }

            // 限制裁剪范围不能超出原图尺寸
            int x = Math.Max(0, Math.Min(cropRect.X, srcBitmap.Width - 1));
            int y = Math.Max(0, Math.Min(cropRect.Y, srcBitmap.Height - 1));
            int width = Math.Max(1, Math.Min(cropRect.Width, srcBitmap.Width - x));
            int height = Math.Max(1, Math.Min(cropRect.Height, srcBitmap.Height - y));

            Rectangle safeRect = new Rectangle(x, y, width, height);

            Bitmap cropBitmap = new Bitmap(safeRect.Width, safeRect.Height);
            using (Graphics g = Graphics.FromImage(cropBitmap))
            {
                g.DrawImage(
                    srcBitmap, 
                    new Rectangle(0, 0, safeRect.Width, safeRect.Height), 
                    safeRect, 
                    GraphicsUnit.Pixel
                );
            }

            return cropBitmap;
        }
    }
}
