using System;
using System.IO;
using System.Linq;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;

namespace WorkerManager.SetupWix.CustomActions
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult CustomAction_BeforeUninstall(Session session)
        {
            session.Log("Begin CustomAction_BeforeUninstall");

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult CustomAction_OnUninstall(Session session)
        {
            session.Log("Begin CustomAction_OnUninstall");

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult CustomAction_BeforeInstall(Session session)
        {
            session.Log("Begin CustomAction_BeforeInstall");

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult CustomAction_OnInstall(Session session)
        {
            session.Log("Begin CustomAction_OnInstall");

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult CustomAction_BeforeUpdate(Session session)
        {
            session.Log("Begin CustomAction_BeforeUpdate");

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult CustomAction_OnUpdate(Session session)
        {
            session.Log("Begin CustomAction_OnUpdate");

            return ActionResult.Success;
        }

        public static bool CheckInstalled(Func<string, bool> nameCheck, out string displayIcon)
        {
            string displayName;

            var registryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            var key = Registry.LocalMachine.OpenSubKey(registryKey);
            if (key != null)
            {
                var keys = key.GetSubKeyNames().Select(keyName => key.OpenSubKey(keyName)).ToList();
                foreach (var subkey in keys)
                {
                    displayName = subkey.GetValue("DisplayName") as string;
                    if (displayName != null && nameCheck(displayName))
                    {
                        displayIcon = subkey.GetValue("DisplayIcon") as string;
                        return true;
                    }
                }
                key.Close();
            }

            registryKey = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
            key = Registry.LocalMachine.OpenSubKey(registryKey);
            if (key != null)
            {
                var keys = key.GetSubKeyNames().Select(keyName => key.OpenSubKey(keyName)).ToList();
                foreach (var subkey in keys)
                {
                    displayName = subkey.GetValue("DisplayName") as string;
                    if (displayName != null && nameCheck(displayName))
                    {
                        displayIcon = subkey.GetValue("DisplayIcon") as string;
                        return true;
                    }
                }
                key.Close();
            }

            displayIcon = null;
            return false;
        }

        // ReSharper disable once UnusedMember.Local
        private static string GetProgramDataDir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Rayys");
        }
    }
}
