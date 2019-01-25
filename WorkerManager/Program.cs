using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WorkerManager
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                var exc = (Exception) eventArgs.ExceptionObject;
                Directory.CreateDirectory("C:\\log");
                File.AppendAllLines("C:\\log\\WorkerManager.error.log", new[] {exc.Message});
                File.AppendAllLines("C:\\log\\WorkerManager.error.log", new[] {exc.StackTrace});
            };

            if (args.Length == 1 && args[0] == "/restart")
            {
                StartApp();
                return;
            }

            using (Mutex mutex = new Mutex(false, "Global\\" + appGuid))
            {
                if (!mutex.WaitOne(0, false))
                {
                    // Second app
                    var doneWithInit = new EventWaitHandle(false, EventResetMode.AutoReset, "MyWaitHandle");

                    // Here, the second application initializes what it needs to.
                    // When it's done, it signals the wait handle:
                    doneWithInit.Set();
                    return;
                }

                StartApp();
            }
        }

        private static void StartApp()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        private static string appGuid = "a7e6d09e-bbaa-48bf-a0e2-fc146bcfbd8f";
    }
}
