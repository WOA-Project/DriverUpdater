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
            Logging.Log("Copyright (c) 2017-2021, The LumiaWOA and DuoWOA Authors");
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
                  DriverUpdaterAction(opts.DefinitionFile, opts.RepositoryPath, opts.PhonePath, !opts.NoIntegratePostUpgrade, opts.IsARM);
                  return 0;
              },
              errs => 1);
        }

        private static void DriverUpdaterAction(string Definition, string DriverRepo, string DevicePart, bool IntegratePostUpgrade, bool IsARM)
        {
            if (!File.Exists(Definition) || !Directory.Exists(DriverRepo) || !Directory.Exists(DevicePart))
            {
                Logging.Log("The tool detected one of the provided paths does not exist. Recheck your parameters and try again.", Logging.LoggingLevel.Error);
                Logging.Log("Usage: DriverUpdater <Path to definition> <Path to Driver repository> <Path to Device partition>");
                return;
            }

            if (!IntegratePostUpgrade)
            {
                Logging.Log("Not going to perform upgrade enablement.", Logging.LoggingLevel.Warning);
            }

            try
            {
                Install(Definition, DriverRepo, DevicePart, IntegratePostUpgrade, IsARM ? ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM : ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM64);
            }
            catch (Exception ex)
            {
                Logging.Log("Something happened!", Logging.LoggingLevel.Error);
                Logging.Log(ex.ToString(), Logging.LoggingLevel.Error);
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
                proc.Start();
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
                proc2.Start();
                proc2.WaitForExit();
                if (proc2.ExitCode != 0)
                {
                    throw new Exception("Couldn't unload registry hive");
                }

                result = ResealForPnPFirstBootUxInternal(DevicePart);
            }

            return result;
        }

        private static void Install(string Definition, string DriverRepo, string DevicePart, bool IntegratePostUpgrade, ProcessorArchitecture _)
        {
            Logging.Log("Reading definition file...");

            // This gets us the list of driver packages to install on the device
            string[] definitionPaths = File.ReadAllLines(Definition).Where(x => !string.IsNullOrEmpty(x)).ToArray();

            // If we have to perform an upgrade operation, reseal the image
            if (IntegratePostUpgrade)
            {
                IntegratePostUpgrade = ResealForPnPFirstBootUx(DevicePart);
            }

            // If we have to perform an upgrade operation, and the previous action succeeded, queue the post upgrade package
            if (IntegratePostUpgrade)
            {
                if (Directory.Exists($"{DriverRepo}\\components\\ANYSOC\\Support\\Desktop\\SUPPORT.DESKTOP.POST_UPGRADE_ENABLEMENT"))
                {
                    definitionPaths = definitionPaths.Union(new string[] { @"components\ANYSOC\Support\Desktop\SUPPORT.DESKTOP.POST_UPGRADE_ENABLEMENT" }).ToArray();
                }
            }

            // Ensure everything exists
            foreach (string path in definitionPaths)
            {
                if (!Directory.Exists($"{DriverRepo}\\{path}"))
                {
                    Logging.Log($"A component package was not found: {DriverRepo}\\{path}", Logging.LoggingLevel.Error);
                    return;
                }
            }

            using IDriverProvider driverProvider = new DismDriverProvider(DevicePart);

            Logging.Log("Enumerating existing drivers...");

            uint ntStatus = driverProvider.GetInstalledOEMDrivers(out string[] existingDrivers);

            if ((ntStatus & 0x80000000) != 0)
            {
                Logging.Log($"DriverStoreOfflineEnumDriverPackage: ntStatus={ntStatus}", Logging.LoggingLevel.Error);
                return;
            }

            Logging.Log("Uninstalling drivers...");

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            foreach (string driver in existingDrivers)
            {
                Console.Title = $"Driver Updater - RemoveOfflineDriver - {driver}";
                Logging.ShowProgress(Progress++, existingDrivers.Length, startTime, false);

                ntStatus = driverProvider.RemoveOfflineDriver(driver);
                if ((ntStatus & 0x80000000) != 0)
                {
                    Logging.Log("");
                    Logging.Log($"RemoveOfflineDriver: ntStatus={ntStatus}", Logging.LoggingLevel.Error);

                    return;
                }
            }
            Logging.ShowProgress(existingDrivers.Length, existingDrivers.Length, startTime, false);
            Logging.Log("");

            Logging.Log("Installing new drivers...");

            foreach (string path in definitionPaths)
            {
                Logging.Log(path);

                // The where LINQ call is because directory can return .inf_ as well...
                IEnumerable<string> infs = Directory.EnumerateFiles($"{DriverRepo}\\{path}", "*.inf", SearchOption.AllDirectories)
                    .Where(x => x.EndsWith(".inf", StringComparison.InvariantCultureIgnoreCase));

                Progress = 0;
                startTime = DateTime.Now;

                // Install every inf present in the component folder
                foreach (string inf in infs)
                {
                    // First add the driver package to the image
                    Console.Title = $"Driver Updater - DriverStoreImport - {inf}";
                    Logging.ShowProgress(Progress++, infs.Count(), startTime, false);

                    const int maxAttempts = 3;
                    int currentFails = 0;

                    while (currentFails < maxAttempts)
                    {
                        ntStatus = driverProvider.AddOfflineDriver(inf);

                        /* 
                           Invalid ARG can be thrown when an issue happens with a specific driver inf
                           No investigation done yet, but for now, this will do just fine
                        */
                        if ((ntStatus & 0x80000000) != 0)
                        {
                            currentFails++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if ((ntStatus & 0x80000000) != 0)
                    {
                        Logging.Log("");
                        Logging.Log($"AddOfflineDriver: ntStatus={ntStatus}, driverInf={inf}", Logging.LoggingLevel.Error);

                        return;
                    }
                }
                Logging.ShowProgress(infs.Count(), infs.Count(), startTime, false);
                Logging.Log("");
            }

            Logging.Log("Fixing potential registry left overs");
            new RegistryFixer(DevicePart).FixRegistryPaths();
        }
    }
}
