using System;
using System.Configuration;
using System.Windows.Forms;

namespace WorkerManager
{
    public static class Utils
    {
        public static bool SafeGet(this Configuration config, string key, bool fallbackValue)
        {
            bool value;
            if (bool.TryParse(config.AppSettings.Settings[key].Value, out value))
            {
                return value;
            }

            config.AppSettings.Settings[key].Value = fallbackValue.ToString().ToLower();
            config.Save(ConfigurationSaveMode.Modified);
            return fallbackValue;
        }

        public static void SafeInvoke(this Control uiElement, Action updater, Action<object> onComplete = null, object p = null, bool forceSynchronous = true)
        {
            if (uiElement == null)
            {
                throw new ArgumentNullException(nameof(uiElement));
            }

            if (uiElement.InvokeRequired)
            {
                if (forceSynchronous)
                {
                    uiElement.Invoke((Action)delegate { SafeInvoke(uiElement, updater, onComplete, p); });
                }
                else
                {
                    uiElement.BeginInvoke((Action)delegate { SafeInvoke(uiElement, updater, onComplete, p, false); });
                }
            }
            else
            {
                if (uiElement.IsDisposed)
                {
                    throw new ObjectDisposedException("Control is already disposed.");
                }

                Update(updater, onComplete, p);
            }
        }

        private static void Update(Action updater, Action<object> onComplete, object p)
        {
            updater();
            onComplete?.Invoke(p);
        }
    }
}