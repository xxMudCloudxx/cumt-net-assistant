using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CampusNetAssistant
{
    internal static class Program
    {

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        [STAThread]
        static void Main()
        {
            const string mutexName = "CampusNetAssistant_SingleInstance";
            using var mutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                // 唤起已运行的进程主窗口
                Process currentProcess = Process.GetCurrentProcess();
                foreach (Process process in Process.GetProcessesByName(currentProcess.ProcessName))
                {
                    if (process.Id != currentProcess.Id)
                    {
                        IntPtr hWnd = process.MainWindowHandle;
                        if (hWnd != IntPtr.Zero)
                        {
                            ShowWindow(hWnd, SW_RESTORE);
                            SetForegroundWindow(hWnd);
                        }
                        break;
                    }
                }
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