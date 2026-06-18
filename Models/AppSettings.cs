using System;

namespace TwoFA.Models
{
    /// <summary>
    /// 应用全局设置
    /// </summary>
    public class AppSettings
    {
        public bool MinimizeOnStart { get; set; }
        public bool AutoStart { get; set; }
        public bool TopMost { get; set; }
        public bool MinimizeAfterCopy { get; set; }
        public string DataFilePath { get; set; }

        // 窗口位置与大小
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public int WindowLeft { get; set; }
        public int WindowTop { get; set; }

        public AppSettings()
        {
            MinimizeOnStart = false;
            AutoStart = false;
            TopMost = false;
            MinimizeAfterCopy = false;
            DataFilePath = string.Empty; // 默认留空，表示使用程序同级目录的 data.2fa

            WindowWidth = 374;
            WindowHeight = 660;
            WindowLeft = -1;
            WindowTop = -1;
        }
    }
}
