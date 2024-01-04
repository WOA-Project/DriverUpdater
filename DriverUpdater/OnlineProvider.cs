using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DriverUpdater
{
    public class OnlineProvider
    {
        public static bool OnlineInstallDrivers(IEnumerable<string> infFiles)
        {
            Logging.Log("Installing new drivers...");

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            // Install every inf present in the component folder
            foreach (string inf in infFiles)
            {
                // First add the driver package to the image
                Console.Title = $"Driver Updater - DismApi->AddDriver - {inf}";
                Logging.ShowProgress(Progress++, infFiles.Count(), startTime, false);

                string Command = $"pnputil /add-driver \"{inf}\" /install";

                Process.Start(new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {Command}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }).WaitForExit();
            }
            Logging.ShowProgress(infFiles.Count(), infFiles.Count(), startTime, false);
            Logging.Log("");

            return true;
        }

        public static bool OnlineInstallApps(IEnumerable<(string, string)> appxs)
        {
            Logging.Log("Installing App Packages...");

            IEnumerable<(string, string)> appPackages = appxs.Where(x => !Path.GetDirectoryName(x.Item1).EndsWith(Path.DirectorySeparatorChar + "Frameworks"));

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            foreach ((string, string) app in appPackages)
            {
                Console.Title = $"Driver Updater - Installing App Package - {app.Item1}";
                Logging.ShowProgress(Progress++, appPackages.Count(), startTime, false);

                string Command = $"powershell.exe Add-AppxProvisionedPackage -Online -Path \"{app.Item1}\"";

                if (!string.IsNullOrEmpty(app.Item2))
                {
                    Command += $" -LicensePath \"{app.Item2}\"";
                }

                Process.Start(new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {Command}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }).WaitForExit();
            }

            Logging.ShowProgress(appPackages.Count(), appPackages.Count(), startTime, false);
            Logging.Log("");

            return true;
        }

        public static bool OnlineInstallDepApps(IEnumerable<(string, string)> appxs)
        {
            IEnumerable<(string, string)> appDependencyPackages = appxs.Where(x => x.Item1.Replace(Path.DirectorySeparatorChar + Path.GetFileName(x.Item1), "").EndsWith(Path.DirectorySeparatorChar + "Frameworks"));

            Logging.Log("Installing App Framework Packages...");

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            foreach ((string, string) app in appDependencyPackages)
            {
                Console.Title = $"Driver Updater - Installing App Framework Package - {app.Item1}";
                Logging.ShowProgress(Progress++, appDependencyPackages.Count(), startTime, false);

                string Command = $"powershell.exe Add-AppxProvisionedPackage -Online -Path \"{app.Item1}\"";

                if (!string.IsNullOrEmpty(app.Item2))
                {
                    Command += $" -LicensePath \"{app.Item2}\"";
                }

                Process.Start(new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {Command}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }).WaitForExit();
            }

            Logging.ShowProgress(appDependencyPackages.Count(), appDependencyPackages.Count(), startTime, false);
            Logging.Log("");

            return true;
        }

    }
}
