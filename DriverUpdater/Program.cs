/*
 * Copyright (c) The LumiaWOA and DuoWOA authors
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using CommandLine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DriverUpdater
{
    internal static class Program
    {
        private static void PrintLogo()
        {
            Logging.Log($"DriverUpdater {Assembly.GetExecutingAssembly().GetName().Version} - Cleans and Installs a new set of drivers onto a Windows Image");
            Logging.Log("Copyright (c) 2017-2023, The LumiaWOA and DuoWOA Authors");
            Logging.Log("https://github.com/WOA-Project/DriverUpdater");
            Logging.Log("");
            Logging.Log("This program comes with ABSOLUTELY NO WARRANTY.");
            Logging.Log("This is free software, and you are welcome to redistribute it under certain conditions.");
            Logging.Log("");
        }

        private static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<CLIOptions>(args).MapResult(
              (CLIOptions opts) =>
              {
                  PrintLogo();
                  DriverUpdaterAction(opts.DefinitionFile, opts.RepositoryPath, opts.PhonePath);
                  return 0;
              },
              errs => 1);
        }

        private static void DriverUpdaterAction(string Definition, string DriverRepo, string DevicePart)
        {
            if (!File.Exists(Definition))
            {
                Logging.Log($"The tool detected one of the provided paths does not exist ({Definition}). Recheck your parameters and try again.", Logging.LoggingLevel.Error);
                Logging.Log("Usage: DriverUpdater <Path to definition> <Path to Driver repository> <Path to Device partition>");
                return;
            }

            if (!Directory.Exists(DriverRepo))
            {
                Logging.Log($"The tool detected one of the provided paths does not exist ({DriverRepo}). Recheck your parameters and try again.", Logging.LoggingLevel.Error);
                Logging.Log("Usage: DriverUpdater <Path to definition> <Path to Driver repository> <Path to Device partition>");
                return;
            }

            if (!string.IsNullOrEmpty(DevicePart))
            {
                if (!Directory.Exists(DevicePart))
                {
                    Logging.Log($"The tool detected one of the provided paths does not exist ({DevicePart}). Recheck your parameters and try again.", Logging.LoggingLevel.Error);
                    Logging.Log("It's also possible the drive {DevicePart} is not the mounted phone but a left over ghosted partition. " +
                        "Try assigning another letter if the drive is ghosted using diskpart. " +
                        "A ghosted drive is a drive still mounted but not pointing to anything and showing errors when opening it.", Logging.LoggingLevel.Warning);
                    Logging.Log("Usage: DriverUpdater <Path to definition> <Path to Driver repository> <Path to Device partition>");
                    return;
                }

                try
                {
                    bool upgrade = ResealForPnPFirstBootUx(DevicePart);
                    // true = first boot completed
                    // false = first boot not completed

                    if (upgrade)
                    {
                        Logging.Log("The device has already been booted once. Reinstalling Board Support Package.");
                    }
                    else
                    {
                        Logging.Log("The device has not been booted yet. Installing Board Support Package for the first time prior to first boot.");
                    }

                    bool result = Install(Definition, DriverRepo, DevicePart, upgrade);

                    if (result)
                    {
                        if (upgrade)
                        {
                            Logging.Log("Fixing potential registry left overs");
                            new RegistryFixer(DevicePart).FixRegistryPaths();
                        }

                        Logging.Log("Enabling Cks");
                        new CksLicensing(DevicePart).SetLicensedState();
                    }
                }
                catch (Exception ex)
                {
                    Logging.Log("Something happened!", Logging.LoggingLevel.Error);
                    Logging.Log(ex.ToString(), Logging.LoggingLevel.Error);
                }
            }
            else
            {
                try
                {
                    _ = OnlineInstall(Definition, DriverRepo);
                }
                catch (Exception ex)
                {
                    Logging.Log("Something happened!", Logging.LoggingLevel.Error);
                    Logging.Log(ex.ToString(), Logging.LoggingLevel.Error);
                }
            }

            Logging.Log("Done!");
        }

        private static bool ResealForPnPFirstBootUxInternal(string DevicePart)
        {
            using FileStream file = File.Open(Path.Combine(DevicePart, "Windows\\System32\\config\\SYSTEM"), FileMode.Open, FileAccess.ReadWrite);
            using DiscUtils.Registry.RegistryHive hive = new(file, DiscUtils.Streams.Ownership.Dispose);
            DiscUtils.Registry.RegistryKey hwconf = hive.Root.OpenSubKey("HardwareConfig");
            if (hwconf != null)
            {
                Logging.Log("Resealing image to PnP FirstBootUx...");
                foreach (string subkey in hwconf.GetSubKeyNames())
                {
                    hwconf.DeleteSubKeyTree(subkey);
                }

                foreach (string subval in hwconf.GetValueNames())
                {
                    hwconf.DeleteValue(subval);
                }

                return true;
            }

            return false;
        }

        private static bool ResealForPnPFirstBootUx(string DevicePart)
        {
            bool result = false;
            try
            {
                result = ResealForPnPFirstBootUxInternal(DevicePart);
            }
            catch (NotImplementedException)
            {
                using Process proc = new()
                {
                    StartInfo = new ProcessStartInfo("reg.exe", $"load HKLM\\DriverUpdater {Path.Combine(DevicePart, "Windows\\System32\\config\\SYSTEM")}")
                    {
                        UseShellExecute = false
                    }
                };
                _ = proc.Start();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    throw new Exception("Couldn't load registry hive");
                }

                using Process proc2 = new()
                {
                    StartInfo = new ProcessStartInfo("reg.exe", "unload HKLM\\DriverUpdater")
                    {
                        UseShellExecute = false
                    }
                };
                _ = proc2.Start();
                proc2.WaitForExit();
                if (proc2.ExitCode != 0)
                {
                    throw new Exception("Couldn't unload registry hive");
                }

                result = ResealForPnPFirstBootUxInternal(DevicePart);
            }

            return result;
        }

        private static bool Install(string Definition, string DriverRepo, string DrivePath, bool IsUpgrade)
        {
            Logging.Log("Reading definition file...");
            DefinitionParser definitionParser = new(Definition);

            bool everythingExists = true;

            // This gets us the list of driver packages to install on the device
            IEnumerable<string> driverPathsToInstall = definitionParser
                .FeatureManifest
                .Drivers
                .BaseDriverPackages
                .DriverPackageFile
                .Select(x => Path.Join(x.Path
                .Replace("$(mspackageroot)", DriverRepo)
                .Replace("$(cputype)", "ARM64") // Hardcoded for now
                .Replace("$(buildtype)", "fre"),
                x.Name));

            // Ensure everything exists
            foreach (string path in driverPathsToInstall)
            {
                if (!File.Exists(path))
                {
                    Logging.Log($"A driver package was not found: {path}", Logging.LoggingLevel.Error);
                    everythingExists = false;
                }
            }

            IEnumerable<(string, string)> appPaths = [];

            if (definitionParser.FeatureManifest.AppX != null)
            {
                // This gets us the list of app packages to install on the device
                appPaths = definitionParser
                    .FeatureManifest
                    .AppX
                    .AppXPackages
                    .PackageFile
                    .Select(x =>
                    {
                        string cleanedPath = x.Path
                                        .Replace("$(mspackageroot)", DriverRepo)
                                        .Replace("$(cputype)", "ARM64") // Hardcoded for now
                                        .Replace("$(buildtype)", "fre");

                        return (Path.Join(cleanedPath, x.Name), !string.IsNullOrEmpty(x.LicenseFile) ? Path.Join(cleanedPath, x.LicenseFile) : "");
                    });

                // Ensure everything exists
                foreach ((string path, string license) in appPaths)
                {
                    if (!File.Exists(path))
                    {
                        Logging.Log($"An app package was not found: {path}", Logging.LoggingLevel.Error);
                        everythingExists = false;
                    }

                    if (!string.IsNullOrEmpty(license) && !File.Exists(license))
                    {
                        Logging.Log($"An app package was not found: {license}", Logging.LoggingLevel.Error);
                        everythingExists = false;
                    }
                }
            }

            if (!everythingExists)
            {
                return false;
            }

            using DismProvider dismProvider = new(DrivePath);

            return dismProvider.InstallDrivers(driverPathsToInstall, IsUpgrade) && dismProvider.InstallDepApps(appPaths) && dismProvider.InstallApps(appPaths);
        }

        private static bool OnlineInstall(string Definition, string DriverRepo)
        {
            Logging.Log("Reading definition file...");
            DefinitionParser definitionParser = new(Definition);

            bool everythingExists = true;

            // This gets us the list of driver packages to install on the device
            IEnumerable<string> driverPathsToInstall = definitionParser
                .FeatureManifest
                .Drivers
                .BaseDriverPackages
                .DriverPackageFile
                .Select(x => Path.Join(x.Path
                .Replace("$(mspackageroot)", DriverRepo)
                .Replace("$(cputype)", "ARM64") // Hardcoded for now
                .Replace("$(buildtype)", "fre"),
                x.Name));

            // Ensure everything exists
            foreach (string path in driverPathsToInstall)
            {
                if (!File.Exists(path))
                {
                    Logging.Log($"A driver package was not found: {path}", Logging.LoggingLevel.Error);
                    everythingExists = false;
                }
            }

            IEnumerable<(string, string)> appPaths = [];

            if (definitionParser.FeatureManifest.AppX != null)
            {
                // This gets us the list of app packages to install on the device
                appPaths = definitionParser
                    .FeatureManifest
                    .AppX
                    .AppXPackages
                    .PackageFile
                    .Select(x =>
                    {
                        string cleanedPath = x.Path
                                        .Replace("$(mspackageroot)", DriverRepo)
                                        .Replace("$(cputype)", "ARM64") // Hardcoded for now
                                        .Replace("$(buildtype)", "fre");

                        return (Path.Join(cleanedPath, x.Name), !string.IsNullOrEmpty(x.LicenseFile) ? Path.Join(cleanedPath, x.LicenseFile) : "");
                    });

                // Ensure everything exists
                foreach ((string path, string license) in appPaths)
                {
                    if (!File.Exists(path))
                    {
                        Logging.Log($"An app package was not found: {path}", Logging.LoggingLevel.Error);
                        everythingExists = false;
                    }

                    if (!string.IsNullOrEmpty(license) && !File.Exists(license))
                    {
                        Logging.Log($"An app package was not found: {license}", Logging.LoggingLevel.Error);
                        everythingExists = false;
                    }
                }
            }

            return everythingExists && OnlineProvider.OnlineInstallDrivers(driverPathsToInstall) && OnlineProvider.OnlineInstallDepApps(appPaths) && OnlineProvider.OnlineInstallApps(appPaths);
        }
    }
}
