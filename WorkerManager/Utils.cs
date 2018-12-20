using System;
using System.Windows.Forms;

namespace WorkerManager
{
    public static class Utils
    {
        public static void SafeInvoke(this Control uiElement, Action updater, bool forceSynchronous = true)
        {
            if (uiElement == null)
            {
                throw new ArgumentNullException(nameof(uiElement));
            }

            if (uiElement.InvokeRequired)
            {
                if (forceSynchronous)
                {
                    uiElement.Invoke((Action)delegate { SafeInvoke(uiElement, updater, true); });
                }
                else
                {
                    uiElement.BeginInvoke((Action)delegate { SafeInvoke(uiElement, updater, false); });
                }
            }
            else
            {
                if (uiElement.IsDisposed)
                {
                    throw new ObjectDisposedException("Control is already disposed.");
                }

                updater();
            }
        }
    }
}