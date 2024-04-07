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
            Logging.LogMilestone("Installing new drivers...");

            Microsoft.Win32.RegistryKey registryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("""Software\Microsoft\Windows\CurrentVersion\Setup""", true);
            bool MinimizeFootprintExists = false;
            if (registryKey != null)
            {
                if (registryKey.GetValue("MinimizeFootprint") != null)
                {
                    MinimizeFootprintExists = true;
                }
                else
                {
                    registryKey.SetValue("MinimizeFootprint", 1, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            // Install every inf present in the component folder
            foreach (string inf in infFiles)
            {
                // First add the driver package to the image
                Console.Title = $"Driver Updater - DismApi->AddDriver - {inf}";
                Logging.ShowProgress(Progress++, infFiles.Count(), startTime, false, "Installing new drivers...", Path.GetFileName(inf));

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
            Logging.ShowProgress(infFiles.Count(), infFiles.Count(), startTime, false, "Installing new drivers...", "");
            Logging.Log("");

            if (registryKey != null)
            {
                if (!MinimizeFootprintExists)
                {
                    registryKey.DeleteValue("MinimizeFootprint");
                }

                registryKey.Dispose();
            }

            return true;
        }

        public static bool OnlineInstallApps(IEnumerable<(string, string)> appxs)
        {
            Logging.LogMilestone("Installing App Packages...");

            IEnumerable<(string, string)> appPackages = appxs.Where(x => !Path.GetDirectoryName(x.Item1).EndsWith(Path.DirectorySeparatorChar + "Frameworks"));

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            foreach ((string, string) app in appPackages)
            {
                Console.Title = $"Driver Updater - Installing App Package - {app.Item1}";
                Logging.ShowProgress(Progress++, appPackages.Count(), startTime, false, "Installing App Packages...", Path.GetFileName(app.Item1));

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

            Logging.ShowProgress(appPackages.Count(), appPackages.Count(), startTime, false, "Installing App Packages...", "");
            Logging.Log("");

            return true;
        }

        public static bool OnlineInstallDepApps(IEnumerable<(string, string)> appxs)
        {
            IEnumerable<(string, string)> appDependencyPackages = appxs.Where(x => x.Item1.Replace(Path.DirectorySeparatorChar + Path.GetFileName(x.Item1), "").EndsWith(Path.DirectorySeparatorChar + "Frameworks"));

            Logging.LogMilestone("Installing App Framework Packages...");

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            foreach ((string, string) app in appDependencyPackages)
            {
                Console.Title = $"Driver Updater - Installing App Framework Package - {app.Item1}";
                Logging.ShowProgress(Progress++, appDependencyPackages.Count(), startTime, false, "Installing App Framework Packages...", Path.GetFileName(app.Item1));

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

            Logging.ShowProgress(appDependencyPackages.Count(), appDependencyPackages.Count(), startTime, false, "Installing App Framework Packages...", "");
            Logging.Log("");

            return true;
        }
    }
}
