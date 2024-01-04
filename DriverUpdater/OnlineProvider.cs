using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DriverUpdater
{
    public class OnlineProvider
    {
        public static bool OnlineInstallDrivers(string DriverRepo, ReadOnlyCollection<string> definitionPaths)
        {
            Logging.Log("Installing new drivers...");

            foreach (string path in definitionPaths)
            {
                Logging.Log(path);

                // The where LINQ call is because directory can return .inf_ as well...
                IEnumerable<string> infFiles = Directory.EnumerateFiles($"{DriverRepo}\\{path}", "*.inf", SearchOption.AllDirectories)
                    .Where(x => x.EndsWith(".inf", StringComparison.InvariantCultureIgnoreCase));

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
            }

            return true;
        }

        public static bool OnlineInstallApps(List<string> deps)
        {
            Logging.Log("Installing App Packages...");

            IEnumerable<string> appPackages = deps.Where(x => !Path.GetDirectoryName(x).EndsWith(Path.DirectorySeparatorChar + "Frameworks"));

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            foreach (string app in appPackages)
            {
                Console.Title = $"Driver Updater - Installing App Package - {app}";
                Logging.ShowProgress(Progress++, appPackages.Count(), startTime, false);

                string Command = $"powershell.exe Add-AppxProvisionedPackage -Online -Path \"{app}\"";

                string appLicense = null;
                if (File.Exists(Path.Combine(Path.GetDirectoryName(app), $"{Path.GetFileNameWithoutExtension(app)}.xml")))
                {
                    appLicense = Path.Combine(Path.GetDirectoryName(app), $"{Path.GetFileNameWithoutExtension(app)}.xml");
                    Command = $"powershell.exe Add-AppxProvisionedPackage -Online -Path \"{app}\" -LicensePath \"{appLicense}\"";

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
            }

            Logging.ShowProgress(appPackages.Count(), appPackages.Count(), startTime, false);
            Logging.Log("");

            return true;
        }

        public static bool OnlineInstallDepApps(List<string> deps)
        {
            IEnumerable<string> appDependencyPackages = deps.Where(x => x.Replace(Path.DirectorySeparatorChar + Path.GetFileName(x), "").EndsWith(Path.DirectorySeparatorChar + "Frameworks"));

            Logging.Log("Installing App Framework Packages...");

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            foreach (string app in appDependencyPackages)
            {
                Console.Title = $"Driver Updater - Installing App Framework Package - {app}";
                Logging.ShowProgress(Progress++, appDependencyPackages.Count(), startTime, false);

                string Command = $"powershell.exe Add-AppxProvisionedPackage -Online -Path \"{app}\"";

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
