using System;
using System.Threading;
using System.Windows.Forms;

namespace CampusNetAssistant
{
    internal static class Program
    {
        private static Mutex? _mutex;

        [STAThread]
        static void Main()
        {
            const string mutexName = "CampusNetAssistant_SingleInstance";
            _mutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                MessageBox.Show("CUMT校园网助手已在运行中！", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 全局异常捕获，防止闪退
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                MessageBox.Show($"发生错误：{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                    "CUMT校园网助手 - 错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    MessageBox.Show($"发生严重错误：{ex.Message}\n\n{ex.StackTrace}",
                        "CUMT校园网助手 - 严重错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}