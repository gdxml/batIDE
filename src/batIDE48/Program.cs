using System;
using System.Windows.Forms;
using batIDE.UI;

namespace batIDE
{
    /// <summary> 应用入口 / Application entry point. </summary>
    internal static class Program
    {
        /// <summary> 启动主窗体 / Launch the main window. </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var form = new MainForm();

            // batIDE.exe <脚本> 直接打开（命令行、拖到 exe 上、文件关联都用这条路径）
            if (args != null && args.Length > 0 && System.IO.File.Exists(args[0]))
                form.OpenPathOnStartup(args[0]);

            Application.Run(form);
        }
    }
}
