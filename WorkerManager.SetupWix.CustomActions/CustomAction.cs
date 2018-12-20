using System;
using System.IO;
using Microsoft.Deployment.WindowsInstaller;

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

        // ReSharper disable once UnusedMember.Local
        private static string GetProgramDataDir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Rayys");
        }
    }
}
