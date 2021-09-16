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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

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
            bool IsSafe = true;

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
                    else if (arg == "--Unsafe")
                    {
                        IsSafe = false;
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
                if (IsSafe)
                {
                    InstallSafe(Definition, DriverRepo, DevicePart, IntegratePostUpgrade, IsARM);
                }
                else
                {
                    Install(Definition, DriverRepo, DevicePart, IntegratePostUpgrade, IsARM);
                }
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

        private static void InstallSafe(string Definition, string DriverRepo, string DevicePart, bool IntegratePostUpgrade, bool IsARM)
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
                definitionPaths = definitionPaths.Union(new string[] { @"components\ANYSOC\Support\Desktop\SUPPORT.DESKTOP.POST_UPGRADE_ENABLEMENT" }).ToArray();
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

            List<string> existingDrivers = new();

            uint ntStatus = NativeMethods.DriverStoreOfflineEnumDriverPackage(
                (
                    string DriverPackageInfPath,
                    IntPtr Ptr,
                    IntPtr Unknown
                ) =>
                {
                    NativeMethods.DriverStoreOfflineEnumDriverPackageInfo DriverStoreOfflineEnumDriverPackageInfoW =
                        (NativeMethods.DriverStoreOfflineEnumDriverPackageInfo)Marshal.PtrToStructure(Ptr, typeof(NativeMethods.DriverStoreOfflineEnumDriverPackageInfo));
                    Console.Title = $"Driver Updater - DriverStoreOfflineEnumDriverPackage - {DriverPackageInfPath}";
                    if (DriverStoreOfflineEnumDriverPackageInfoW.InboxInf == 0)
                    {
                        existingDrivers.Add(DriverPackageInfPath);
                    }

                    return 1;
                }
            , IntPtr.Zero, $"{DevicePart}\\Windows");

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
                // Unreflect the modifications done by the driver package first on the target windows image
                Console.Title = $"Driver Updater - DriverStoreOfflineDeleteDriverPackage - {driver}";
                Logging.ShowProgress(Progress++, existingDrivers.Count, startTime, false);
                ntStatus = NativeMethods.DriverStoreOfflineDeleteDriverPackage(driver, 0, IntPtr.Zero, $"{DevicePart}\\Windows", DevicePart);
                if ((ntStatus & 0x80000000) != 0)
                {
                    Logging.Log("");
                    Logging.Log($"DriverStoreOfflineDeleteDriverPackage: ntStatus={ntStatus}", Logging.LoggingLevel.Error);

                    return;
                }
            }
            Logging.ShowProgress(existingDrivers.Count, existingDrivers.Count, startTime, false);
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
                    StringBuilder destinationPath = new(260);
                    int destinationPathLength = 260;

                    // First add the driver package to the image
                    Console.Title = $"Driver Updater - DriverStoreOfflineAddDriverPackage - {inf}";
                    Logging.ShowProgress(Progress++, infs.Count(), startTime, false);

                    const int maxAttempts = 3;
                    int currentFails = 0;

                    while (currentFails < maxAttempts)
                    {
                        // 0x00000020: Use hard links when importing to the driver store
                        // 0x00000080: Replace the driver package if it is already present in the driver store
                        // 0x00000100: Force offline reflection regardless of device class when importing to the driver store
                        ntStatus = NativeMethods.DriverStoreOfflineAddDriverPackage(inf, DriverStoreOfflineAddDriverPackageFlags.ReplacePackage, IntPtr.Zero, (ushort)(IsARM ? ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM : ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM64), "en-US", destinationPath, ref destinationPathLength, $"{DevicePart}\\Windows", DevicePart);

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
                        Logging.Log($"DriverStoreOfflineAddDriverPackage: ntStatus={ntStatus}, destinationPathLength={destinationPathLength}, destinationPath={destinationPath}", Logging.LoggingLevel.Error);

                        return;
                    }
                }
                Logging.ShowProgress(infs.Count(), infs.Count(), startTime, false);
                Logging.Log("");
            }
        }

        private static void Install(string Definition, string DriverRepo, string DevicePart, bool IntegratePostUpgrade, bool IsARM)
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
                definitionPaths = definitionPaths.Union(new string[] { @"components\ANYSOC\Support\Desktop\SUPPORT.DESKTOP.POST_UPGRADE_ENABLEMENT" }).ToArray();
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

            List<string> existingDrivers = new();

            uint ntStatus = NativeMethods.DriverStoreOfflineEnumDriverPackage(
                (
                    string DriverPackageInfPath,
                    IntPtr Ptr,
                    IntPtr Unknown
                ) =>
                {
                    NativeMethods.DriverStoreOfflineEnumDriverPackageInfo DriverStoreOfflineEnumDriverPackageInfoW =
                        (NativeMethods.DriverStoreOfflineEnumDriverPackageInfo)Marshal.PtrToStructure(Ptr, typeof(NativeMethods.DriverStoreOfflineEnumDriverPackageInfo));
                    Console.Title = $"Driver Updater - DriverStoreOfflineEnumDriverPackage - {DriverPackageInfPath}";
                    if (DriverStoreOfflineEnumDriverPackageInfoW.InboxInf == 0)
                    {
                        existingDrivers.Add(DriverPackageInfPath);
                    }

                    return 1;
                }
            , IntPtr.Zero, $"{DevicePart}\\Windows");

            if ((ntStatus & 0x80000000) != 0)
            {
                Logging.Log($"DriverStoreOfflineEnumDriverPackage: ntStatus={ntStatus}", Logging.LoggingLevel.Error);
                return;
            }

            Logging.Log("Uninstalling drivers...");

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            IntPtr hDriverStore = NativeMethods.DriverStoreOpen($"{DevicePart}\\Windows", DevicePart, 0, IntPtr.Zero);
            if (hDriverStore == IntPtr.Zero)
            {
                if ((ntStatus & 0x80000000) != 0)
                {
                    Logging.Log("");
                    Logging.Log($"DriverStoreOpen: ntStatus={Marshal.GetLastWin32Error()}", Logging.LoggingLevel.Error);
                    return;
                }
            }

            foreach (string driver in existingDrivers)
            {
                // Unreflect the modifications done by the driver package first on the target windows image
                Console.Title = $"Driver Updater - DriverStoreUnreflectCritical - {driver}";
                Logging.ShowProgress(Progress++, existingDrivers.Count, startTime, false);
                ntStatus = NativeMethods.DriverStoreUnreflectCritical(hDriverStore, driver, 0, null);
                if ((ntStatus & 0x80000000) != 0)
                {
                    Logging.Log("");
                    Logging.Log($"DriverStoreUnreflectCritical: ntStatus={ntStatus}", Logging.LoggingLevel.Error);
                    NativeMethods.DriverStoreClose(hDriverStore);

                    return;
                }

                // And then remove the driver package from the target image
                Console.Title = $"Driver Updater - DriverStoreDelete - {driver}";
                ntStatus = NativeMethods.DriverStoreDelete(hDriverStore, driver, 0);
                if ((ntStatus & 0x80000000) != 0)
                {
                    Logging.Log("");
                    Logging.Log($"DriverStoreDelete: ntStatus={ntStatus}", Logging.LoggingLevel.Error);
                    NativeMethods.DriverStoreClose(hDriverStore);

                    return;
                }
            }
            Logging.ShowProgress(existingDrivers.Count, existingDrivers.Count, startTime, false);
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
                    StringBuilder destinationPath = new(260);
                    const int destinationPathLength = 260;

                    // First add the driver package to the image
                    Console.Title = $"Driver Updater - DriverStoreImport - {inf}";
                    Logging.ShowProgress(Progress++, infs.Count(), startTime, false);

                    int maxAttempts = 3;
                    int currentFails = 0;

                    while (currentFails < maxAttempts)
                    {
                        ntStatus = NativeMethods.DriverStoreImport(hDriverStore, inf, IsARM ? ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM : ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM64, "en-us", DriverStoreImportFlag.Replace | DriverStoreImportFlag.SystemCritical, destinationPath, destinationPathLength);

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
                        Logging.Log($"DriverStoreImport: ntStatus={ntStatus}, destinationPathLength={destinationPathLength}, destinationPath={destinationPath}", Logging.LoggingLevel.Error);
                        NativeMethods.DriverStoreClose(hDriverStore);

                        return;
                    }

                    // And then reflect it into the target image
                    Console.Title = $"Driver Updater - DriverStoreReflectCritical - {inf}";
                    maxAttempts = 3;
                    currentFails = 0;

                    while (currentFails < maxAttempts)
                    {
                        ntStatus = NativeMethods.DriverStoreReflectCritical(hDriverStore, destinationPath.ToString(), 0, null);

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
                        Logging.Log($"DriverStoreReflectCritical: ntStatus={ntStatus}, destinationPathLength={destinationPathLength}, destinationPath={destinationPath}", Logging.LoggingLevel.Error);
                        NativeMethods.DriverStoreClose(hDriverStore);

                        return;
                    }
                }
                Logging.ShowProgress(infs.Count(), infs.Count(), startTime, false);
                Logging.Log("");
            }

            NativeMethods.DriverStoreClose(hDriverStore);
        }

        public static bool IsDriverValid(string inf, bool IsARM)
        {
            try
            {
                IntPtr driverPackage = NativeMethods.DriverPackageOpen(inf, IsARM ? ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM : ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM64, "en-us", DriverPackageOpenFlag.StrictValidation | DriverPackageOpenFlag.PrimaryOnly, IntPtr.Zero);
                NativeMethods.DriverPackageClose(driverPackage);
                return true;
            }
            catch { }
            return false;
        }

        public static uint AddDriver(string inf, string DevicePart, bool IsARM)
        {
            uint ntStatus;
            if (!IsDriverValid(inf, IsARM))
            {
                ntStatus = 0x80000000;
                Logging.Log("");
                Logging.Log($"IsDriverValid: ntStatus={Marshal.GetLastWin32Error()}", Logging.LoggingLevel.Error);
                return ntStatus;
            }

            IntPtr hDriverStore = NativeMethods.DriverStoreOpen($"{DevicePart}\\Windows", DevicePart, 0, IntPtr.Zero);
            if (hDriverStore == IntPtr.Zero)
            {
                ntStatus = 0x80000000;
                Logging.Log("");
                Logging.Log($"DriverStoreOpen: ntStatus={Marshal.GetLastWin32Error()}", Logging.LoggingLevel.Error);
                return ntStatus;
            }

            StringBuilder driverStoreFileName = new(260);
            const DriverStoreImportFlag importFlags =
                DriverStoreImportFlag.SkipTempCopy |
                DriverStoreImportFlag.SkipExternalFileCheck |
                DriverStoreImportFlag.SystemDefaultLocale |
                DriverStoreImportFlag.Hardlink |
                DriverStoreImportFlag.PublishSameName |
                DriverStoreImportFlag.NoRestorePoint;

            ntStatus = NativeMethods.DriverStoreImport(hDriverStore, inf, IsARM ? ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM : ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM64, null, importFlags, driverStoreFileName, driverStoreFileName.Capacity);
            if ((ntStatus & 0x80000000) != 0)
            {
                Logging.Log("");
                Logging.Log($"DriverStoreImport: ntStatus={Marshal.GetLastWin32Error()}", Logging.LoggingLevel.Error);
                return ntStatus;
            }

            StringBuilder publishedFileName = new(260);
            bool isPublishedFileNameChanged = false;

            ntStatus = NativeMethods.DriverStorePublish(hDriverStore, driverStoreFileName.ToString(), DriverStorePublishFlag.None, publishedFileName, publishedFileName.Capacity, ref isPublishedFileNameChanged);
            if ((ntStatus & 0x80000000) != 0)
            {
                Logging.Log("");
                Logging.Log($"DriverStorePublish: ntStatus={Marshal.GetLastWin32Error()}", Logging.LoggingLevel.Error);
                return ntStatus;
            }

            const DriverStoreReflectCriticalFlag reflectFlags = DriverStoreReflectCriticalFlag.Force | DriverStoreReflectCriticalFlag.Configurations;
            ntStatus = NativeMethods.DriverStoreReflectCritical(hDriverStore, driverStoreFileName.ToString(), reflectFlags, null);
            if ((ntStatus & 0x80000000) != 0)
            {
                Logging.Log("");
                Logging.Log($"DriverStoreReflectCritical: ntStatus={Marshal.GetLastWin32Error()}", Logging.LoggingLevel.Error);
                return ntStatus;
            }

            return ntStatus;
        }
    }
}
