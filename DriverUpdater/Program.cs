/*

Copyright (c) 2017-2021, The LumiaWOA Authors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/
using Microsoft.Dism;
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
        private static void Main(string[] args)
        {
            Logging.Log($"DriverUpdater {Assembly.GetExecutingAssembly().GetName().Version} - Cleans and Installs a new set of drivers onto a Windows Image");
            Logging.Log("Copyright (c) 2017-2021, The LumiaWOA Authors");
            Logging.Log("https://github.com/WOA-Project/DriverUpdater");
            Logging.Log("");
            Logging.Log("This program comes with ABSOLUTELY NO WARRANTY.");
            Logging.Log("This is free software, and you are welcome to redistribute it under certain conditions.");
            Logging.Log("");

            if (args.Length < 3)
            {
                Logging.Log("Usage: DriverUpdater <Path to definition> <Path to Driver repository> <Path to Device partition>");
                return;
            }

            string Definition = args[0];
            string DriverRepo = args[1];
            string DevicePart = args[2];
            bool IntegratePostUpgrade = true;
            bool IsARM = false;

            if (args.Length > 3)
            {
                foreach (string arg in args.Skip(3))
                {
                    if (arg == "--NoIntegratePostUpgrade")
                    {
                        IntegratePostUpgrade = false;
                    }
                    else if (arg == "--ARM")
                    {
                        IsARM = true;
                    }
                    else
                    {
                        Logging.Log($"Ignored extra parameter: {arg}", Logging.LoggingLevel.Warning);
                    }
                }
            }

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

        private static void Install(string Definition, string DriverRepo, string DevicePart, bool IntegratePostUpgrade, ProcessorArchitecture processorArchitecture)
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

            Logging.Log("Enumerating existing drivers...");

            uint ntStatus = DismGetInstalledOEMDrivers(DevicePart, out string[] existingDrivers);

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
                Logging.ShowProgress(Progress++, existingDrivers.Count(), startTime, false);

                ntStatus = DismRemoveOfflineDriver(driver, DevicePart);
                if ((ntStatus & 0x80000000) != 0)
                {
                    Logging.Log("");
                    Logging.Log($"RemoveOfflineDriver: ntStatus={ntStatus}", Logging.LoggingLevel.Error);

                    return;
                }
            }
            Logging.ShowProgress(existingDrivers.Count(), existingDrivers.Count(), startTime, false);
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
                        ntStatus = DismAddOfflineDriver(inf, DevicePart, processorArchitecture);

                        /* 
                           Invalid ARG can be thrown when an issue happens with a specific driver inf
                           No investigation done yet, but for now, this will do just fine
                        */
                        if (ntStatus == 0x80070057)
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
                        Logging.Log($"AddOfflineDriver: ntStatus={ntStatus}", Logging.LoggingLevel.Error);

                        return;
                    }
                }
                Logging.ShowProgress(infs.Count(), infs.Count(), startTime, false);
                Logging.Log("");
            }
        }

        private static uint DismGetInstalledOEMDrivers(string DevicePart, out string[] existingDrivers)
        {
            List<string> lexistingDrivers = new();

            uint ntStatus = 0;

            try
            {
                using DismSession session = DismApi.OpenOfflineSession(DevicePart);

                foreach (DismDriverPackage driver in DismApi.GetDrivers(session, false))
                {
                    lexistingDrivers.Add(driver.PublishedName);
                }
            }
            catch (Exception e)
            {
                ntStatus = (uint)e.HResult;
            }

            existingDrivers = lexistingDrivers.ToArray();

            return ntStatus;
        }

        public static uint DismRemoveOfflineDriver(string driverStoreFileName, string DevicePart)
        {
            uint ntStatus = 0;

            try
            {
                using DismSession session = DismApi.OpenOfflineSession(DevicePart);

                DismApi.RemoveDriver(session, driverStoreFileName);
            }
            catch (Exception e)
            {
                ntStatus = (uint)e.HResult;
            }

            return ntStatus;
        }

        public static uint DismAddOfflineDriver(string inf, string DevicePart, ProcessorArchitecture _)
        {
            uint ntStatus = 0;

            try
            {
                using DismSession session = DismApi.OpenOfflineSession(DevicePart);

                DismApi.AddDriver(session, inf, false);
            }
            catch (Exception e)
            {
                ntStatus = (uint)e.HResult;
            }

            return ntStatus;
        }

        /*private static uint GetInstalledOEMDrivers(string DevicePart, out string[] existingDrivers)
        {
            List<string> lexistingDrivers = new();

            uint ntStatus = NativeMethods.DriverStoreOfflineEnumDriverPackage(
                (
                    string DriverPackageInfPath,
                    IntPtr Ptr,
                    IntPtr _
                ) =>
                {
                    NativeMethods.DriverStoreOfflineEnumDriverPackageInfo DriverStoreOfflineEnumDriverPackageInfoW =
                        (NativeMethods.DriverStoreOfflineEnumDriverPackageInfo)Marshal.PtrToStructure(Ptr, typeof(NativeMethods.DriverStoreOfflineEnumDriverPackageInfo));
                    Console.Title = $"Driver Updater - DriverStoreOfflineEnumDriverPackage - {DriverPackageInfPath}";
                    if (DriverStoreOfflineEnumDriverPackageInfoW.InboxInf == 0)
                    {
                        lexistingDrivers.Add(DriverPackageInfPath);
                    }

                    return 1;
                }
            , IntPtr.Zero, $"{DevicePart}\\Windows");

            existingDrivers = lexistingDrivers.ToArray();

            return ntStatus;
        }

        public static uint RemoveOfflineDriver(string driverStoreFileName, string DevicePart)
        {
            return NativeMethods.DriverStoreOfflineDeleteDriverPackage(
                driverStoreFileName,
                0,
                IntPtr.Zero,
                $"{DevicePart}\\Windows",
                DevicePart);
        }

        public static uint AddOfflineDriver(string inf, string DevicePart, ProcessorArchitecture processorArchitecture)
        {
            StringBuilder driverStoreFileName = new(260);
            int cchDestInfPath = driverStoreFileName.Capacity;
            return NativeMethods.DriverStoreOfflineAddDriverPackage(
                inf,
                DriverStoreOfflineAddDriverPackageFlags.None,
                IntPtr.Zero,
                processorArchitecture,
                "en-US",
                driverStoreFileName,
                ref cchDestInfPath,
                $"{DevicePart}\\Windows",
                DevicePart);
        }*/

        /*public static uint RemoveDriver(string driverStoreFileName, IntPtr hDriverStore)
        {
            uint ntStatus = NativeMethods.DriverStoreUnreflectCritical(
                hDriverStore,
                driverStoreFileName,
                0,
                null);
            if ((ntStatus & 0x80000000) != 0)
            {
                goto exit;
            }

            StringBuilder publishedFileName = new(260);
            bool isPublishedFileNameChanged = false;

            ntStatus = NativeMethods.DriverStoreUnpublish(
                hDriverStore,
                driverStoreFileName,
                DriverStoreUnpublishFlag.None,
                publishedFileName,
                publishedFileName.Capacity,
                ref isPublishedFileNameChanged);
            if ((ntStatus & 0x80000000) != 0)
            {
                goto exit;
            }

            ntStatus = NativeMethods.DriverStoreDelete(hDriverStore, driverStoreFileName, 0);
            if ((ntStatus & 0x80000000) != 0)
            {
                goto exit;
            }

        exit:
            return ntStatus;
        }

        public static uint AddDriver(string inf, IntPtr hDriverStore, ProcessorArchitecture processorArchitecture)
        {
            uint ntStatus;

            StringBuilder driverStoreFileName = new(260);

            ntStatus = NativeMethods.DriverStoreImport(
                hDriverStore,
                inf,
                processorArchitecture,
                null,
                DriverStoreImportFlag.None,
                driverStoreFileName,
                driverStoreFileName.Capacity);
            if ((ntStatus & 0x80000000) != 0)
            {
                goto exit;
            }

            StringBuilder publishedFileName = new(260);
            bool isPublishedFileNameChanged = false;

            ntStatus = NativeMethods.DriverStorePublish(
                hDriverStore,
                driverStoreFileName.ToString(),
                DriverStorePublishFlag.None,
                publishedFileName,
                publishedFileName.Capacity,
                ref isPublishedFileNameChanged);
            if ((ntStatus & 0x80000000) != 0)
            {
                goto exit;
            }

            ntStatus = NativeMethods.DriverStoreReflectCritical(
                hDriverStore,
                driverStoreFileName.ToString(),
                DriverStoreReflectCriticalFlag.None,
                null);
            if ((ntStatus & 0x80000000) != 0)
            {
                goto exit;
            }

        exit:
            return ntStatus;
        }*/
    }
}
