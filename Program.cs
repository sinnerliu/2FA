using System;
using System.Threading;
using System.Windows.Forms;
using TwoFA.UI;

namespace TwoFA
{
    static class Program
    {
        private static Mutex _mutex = null;

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 注册程序集解析事件，实现单文件内嵌 DLL 加载
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (args.Name.StartsWith("zxing", StringComparison.OrdinalIgnoreCase))
                {
                    using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("zxing.dll"))
                    {
                        if (stream == null) return null;
                        byte[] assemblyData = new byte[stream.Length];
                        stream.Read(assemblyData, 0, (int)stream.Length);
                        return System.Reflection.Assembly.Load(assemblyData);
                    }
                }
                return null;
            };

            // 通过命名 Mutex 实现单实例运行检测
            const string appName = "2FA_Authenticator_Desktop_App_Mutex";
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("2FA 安全验证器已经在运行中。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            finally
            {
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Close();
                }
            }
        }
    }
}
