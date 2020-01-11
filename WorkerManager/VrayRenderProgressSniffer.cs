using System;
using System.Diagnostics;
using System.Threading;

namespace WorkerManager
{
    public class VrayRenderProgressSniffer : IDisposable
    {
        private readonly Process workerProcess;
        private readonly object sync = new object();
        private readonly Mutex findMutex = new Mutex();
        private readonly Mutex vrayMutex = new Mutex();

        private string progressText;
        private IntPtr progressLabelHwnd;
        private Timer findRenderDialogTimer;
        private Timer getRenderProgressTimer;

        public event EventHandler<string> ProgressChanged;

        public string ProgressText
        {
            get
            {
                lock (this.sync)
                {
                    return this.progressText;
                }
            }
        }

        public VrayRenderProgressSniffer(Process workerProcess)
        {
            this.workerProcess = workerProcess;
            this.StartWaitForRenderTimer();
        }

        private void StartWaitForRenderTimer()
        {
            this.findRenderDialogTimer = new Timer(TryFindRenderingDialog, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
        }

        private void StartWatchProgressTimer()
        {
            this.getRenderProgressTimer = new Timer(TryGetRenderProgress, null, TimeSpan.FromMilliseconds(350), TimeSpan.FromSeconds(1));
        }

        private void TryGetRenderProgress(object state)
        {
            if (!this.vrayMutex.WaitOne(TimeSpan.FromMilliseconds(100)))
            {
                return;
            }

            try
            {
                if (EnumerateOpenedWindows.IsWindowVisible(this.progressLabelHwnd))
                {
                    string changedText = null;

                    lock (this.sync)
                    {
                        var text = EnumerateOpenedWindows.GetWindowText(this.progressLabelHwnd);
                        if (!string.Equals(this.progressText, text))
                        {
                            this.progressText = text;
                            changedText = text;
                        }
                    }

                    if (changedText != null)
                    {
                        ProgressChanged?.Invoke(this, changedText);
                    }
                }
                else
                {
                    this.getRenderProgressTimer?.Dispose();
                    this.getRenderProgressTimer = null;

                    StartWaitForRenderTimer();

                    this.progressText = string.Empty;
                    ProgressChanged?.Invoke(this, this.progressText);
                }
            }
            catch (Exception exc)
            {
                Trace.TraceError(exc.Message);
            }
            finally
            {
                this.vrayMutex.ReleaseMutex();
            }
        }

        private void TryFindRenderingDialog(object state)
        {
            if (!this.findMutex.WaitOne(TimeSpan.FromMilliseconds(100)))
            {
                return;
            }

            try
            {
                var desktopHwnds = EnumerateOpenedWindows.GetDesktopWindows();

                foreach (var hwnd1 in desktopHwnds)
                {
                    uint windowProcessId;
                    EnumerateOpenedWindows.GetWindowThreadProcessId(hwnd1, out windowProcessId);
                    if (this.workerProcess.Id != windowProcessId) continue;

                    var windowText = EnumerateOpenedWindows.GetWindowText(hwnd1);
                    if (windowText.Contains("Rendering"))
                    {
                        var handles = EnumerateOpenedWindows.GetAllChildrenWindowHandles(hwnd1, long.MaxValue);
                        foreach (var ctrlHwnd in handles)
                        {
                            // ReSharper disable once InconsistentNaming
                            const int GWL_ID = -12;
                            var controlId = EnumerateOpenedWindows.GetWindowLongPtr(ctrlHwnd, GWL_ID);
                            if (controlId.ToInt32() == 0x544)
                                // 0x544 corresponds to progressText label near rendering progress
                            {
                                this.progressLabelHwnd = ctrlHwnd;
                                this.findRenderDialogTimer?.Dispose();
                                this.findRenderDialogTimer = null;

                                this.StartWatchProgressTimer();
                            }
                        }

                    }
                }
            }
            catch (Exception exc)
            {
                Trace.TraceError(exc.Message);
            }
            finally
            {
                this.findMutex.ReleaseMutex();
            }
        }

        public void Dispose()
        {
            this.findRenderDialogTimer?.Dispose();
            this.getRenderProgressTimer?.Dispose();
        }
    }
}