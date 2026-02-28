using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CampusNetAssistant
{
    internal static class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        public static EventWaitHandle? WakeupEvent;

        [STAThread]
        static void Main()
        {
            const string mutexName = "CampusNetAssistant_SingleInstance";
            const string eventName = "CampusNetAssistant_WakeupEvent";
            
            using var mutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                // 唤起已运行的进程主窗口（通过 EventWaitHandle 通知它自己显示，这不仅适用于已显示的窗口，也适用于隐藏在系统托盘的窗口）
                try
                {
                    using var evt = EventWaitHandle.OpenExisting(eventName);
                    evt.Set();
                }
                catch { }
                return;
            }

            WakeupEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

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
            var mainForm = new MainForm();
            var _ = mainForm.Handle; // 强制创建句柄确保正常 Invoke

            // 监听唤醒事件的后台任务
            Task.Run(() => 
            {
                while (true)
                {
                    try 
                    {
                        WakeupEvent.WaitOne();
                        if (mainForm.IsDisposed) break;
                        
                        mainForm.Invoke(new Action(() => 
                        {
                            mainForm.Show();
                            if (mainForm.WindowState == FormWindowState.Minimized)
                                mainForm.WindowState = FormWindowState.Normal;
                            mainForm.Activate();
                            SetForegroundWindow(mainForm.Handle);
                        }));
                    }
                    catch { break; }
                }
            });

            Application.Run(mainForm);
            
            WakeupEvent?.Dispose();
        }
    }
}