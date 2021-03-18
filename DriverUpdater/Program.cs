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
    class Program
    {
        static void Main(string[] args)
        {
            Logging.Log($"DriverUpdater {Assembly.GetExecutingAssembly().GetName().Version} - Cleans and Installs a new set of drivers onto a Windows Image");
            Logging.Log("Copyright (c) 2017-2021, The LumiaWOA Authors");
            Logging.Log("https://github.com/WOA-Project/DriverUpdater");
            Logging.Log("");
            Logging.Log("This program comes with ABSOLUTELY NO WARRANTY.");
            Logging.Log("This is free software, and you are welcome to redistribute it under certain conditions.");
            Logging.Log("");

            if (args.Count() < 3)
            {
                Logging.Log("Usage: DriverUpdater <Path to definition> <Path to Driver repository> <Path to Device partition>");
                return;
            }

            string Definition = args[0];
            string DriverRepo = args[1];
            string DevicePart = args[2];
            bool IntegratePostUpgrade = true;
            bool IsARM = false;

            if (args.Count() > 3)
            {
                foreach (var arg in args.Skip(3))
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
                Logging.Log("Not going to perform upgrade enablement.", Logging.LoggingLevel.Warning);

            try
            {
                InstallSafe(Definition, DriverRepo, DevicePart, IntegratePostUpgrade, IsARM);
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
            using var file = File.Open(Path.Combine(DevicePart, "Windows\\System32\\config\\SYSTEM"), FileMode.Open, FileAccess.ReadWrite);
            using var hive = new DiscUtils.Registry.RegistryHive(file, DiscUtils.Streams.Ownership.Dispose);
            var hwconf = hive.Root.OpenSubKey("HardwareConfig");
            if (hwconf != null)
            {
                Logging.Log("Resealing image to PnP FirstBootUx...");
                foreach (var subkey in hwconf.GetSubKeyNames())
                    hwconf.DeleteSubKeyTree(subkey);
                foreach (var subval in hwconf.GetValueNames())
                    hwconf.DeleteValue(subval);

                return true;
            }

            return false;
        }

        static bool ResealForPnPFirstBootUx(string DevicePart)
        {
            bool result = false;
            try
            {
                result = ResealForPnPFirstBootUxInternal(DevicePart);
            }
            catch (NotImplementedException)
            {
                var proc = new Process();
                proc.StartInfo = new ProcessStartInfo("reg.exe", $"load HKLM\\DriverUpdater {Path.Combine(DevicePart, "Windows\\System32\\config\\SYSTEM")}");
                proc.StartInfo.UseShellExecute = false;
                proc.Start();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                    throw new Exception("Couldn't load registry hive");

                proc = new Process();
                proc.StartInfo = new ProcessStartInfo("reg.exe", $"unload HKLM\\DriverUpdater");
                proc.StartInfo.UseShellExecute = false;
                proc.Start();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                    throw new Exception("Couldn't unload registry hive");

                result = ResealForPnPFirstBootUxInternal(DevicePart);
            }

            return result;
        }

        static void InstallSafe(string Definition, string DriverRepo, string DevicePart, bool IntegratePostUpgrade, bool IsARM)
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
            foreach (var path in definitionPaths)
            {
                if (!Directory.Exists($"{DriverRepo}\\{path}"))
                {
                    Logging.Log($"A component package was not found: {DriverRepo}\\{path}", Logging.LoggingLevel.Error);
                    return;
                }
            }

            Logging.Log("Enumerating existing drivers...");

            List<string> existingDrivers = new List<string>();

            int ntStatus = NativeMethods.DriverStoreOfflineEnumDriverPackageW(
                (
                    string DriverPackageInfPath,
                    IntPtr Ptr,
                    IntPtr Unknown
                ) =>
                {
                    NativeMethods.DriverStoreOfflineEnumDriverPackageInfoW DriverStoreOfflineEnumDriverPackageInfoW =
                        (NativeMethods.DriverStoreOfflineEnumDriverPackageInfoW)Marshal.PtrToStructure(Ptr, typeof(NativeMethods.DriverStoreOfflineEnumDriverPackageInfoW));
                    Console.Title = $"Driver Updater - DriverStoreOfflineEnumDriverPackageW - {DriverPackageInfPath}";
                    if (DriverStoreOfflineEnumDriverPackageInfoW.InboxInf == 0)
                        existingDrivers.Add(DriverPackageInfPath);
                    return 1;
                }
            , IntPtr.Zero, $"{DevicePart}\\Windows");

            if (ntStatus < 0)
            {
                Logging.Log($"DriverStoreOfflineEnumDriverPackageW: ntStatus={ntStatus}", Logging.LoggingLevel.Error);
                return;
            }

            Logging.Log("Uninstalling drivers...");

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            foreach (var driver in existingDrivers)
            {
                // Unreflect the modifications done by the driver package first on the target windows image
                Console.Title = $"Driver Updater - DriverStoreOfflineDeleteDriverPackageW - {driver}";
                Logging.ShowProgress(Progress++, existingDrivers.Count, startTime, false);
                ntStatus = NativeMethods.DriverStoreOfflineDeleteDriverPackageW(driver, 0, IntPtr.Zero, $"{DevicePart}\\Windows", DevicePart);
                if (ntStatus < 0)
                {
                    Logging.Log("");
                    Logging.Log($"DriverStoreOfflineDeleteDriverPackageW: ntStatus={ntStatus}", Logging.LoggingLevel.Error);

                    return;
                }
            }
            Logging.ShowProgress(existingDrivers.Count, existingDrivers.Count, startTime, false);
            Logging.Log("");

            Logging.Log("Installing new drivers...");

            foreach (var path in definitionPaths)
            {
                Logging.Log(path);

                // The where LINQ call is because directory can return .inf_ as well...
                var infs = Directory.EnumerateFiles($"{DriverRepo}\\{path}", "*.inf", SearchOption.AllDirectories)
                    .Where(x => x.EndsWith(".inf", StringComparison.InvariantCultureIgnoreCase));

                Progress = 0;
                startTime = DateTime.Now;

                // Install every inf present in the component folder
                foreach (var inf in infs)
                {
                    var destinationPath = new StringBuilder(260);
                    int destinationPathLength = 260;

                    // First add the driver package to the image
                    Console.Title = $"Driver Updater - DriverStoreOfflineAddDriverPackageW - {inf}";
                    Logging.ShowProgress(Progress++, infs.Count(), startTime, false);

                    int maxAttempts = 3;
                    int currentFails = 0;

                    while (currentFails < maxAttempts)
                    {
                        ntStatus = NativeMethods.DriverStoreOfflineAddDriverPackageW(inf, 0x00000020 | 0x00000080 | 0x00000100, IntPtr.Zero, IsARM ? NativeMethods.ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM : NativeMethods.ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM64, "en-US", destinationPath, ref destinationPathLength, $"{DevicePart}\\Windows", DevicePart);

                        /* 
                           Invalid ARG can be thrown when an issue happens with a specific driver inf
                           No investigation done yet, but for now, this will do just fine
                        */
                        if (ntStatus == -2147024809)
                            currentFails++;
                        else
                            break;
                    }

                    if (ntStatus < 0)
                    {
                        Logging.Log("");
                        Logging.Log($"DriverStoreOfflineAddDriverPackageW: ntStatus={ntStatus}, destinationPathLength={destinationPathLength}, destinationPath={destinationPath}", Logging.LoggingLevel.Error);

                        return;
                    }
                }
                Logging.ShowProgress(infs.Count(), infs.Count(), startTime, false);
                Logging.Log("");
            }
        }

        static void Install(string Definition, string DriverRepo, string DevicePart, bool IntegratePostUpgrade, bool IsARM)
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
            foreach (var path in definitionPaths)
            {
                if (!Directory.Exists($"{DriverRepo}\\{path}"))
                {
                    Logging.Log($"A component package was not found: {DriverRepo}\\{path}", Logging.LoggingLevel.Error);
                    return;
                }
            }

            Logging.Log("Enumerating existing drivers...");

            List<string> existingDrivers = new List<string>();

            int ntStatus = NativeMethods.DriverStoreOfflineEnumDriverPackageW(
                (
                    string DriverPackageInfPath,
                    IntPtr Ptr,
                    IntPtr Unknown
                ) =>
                {
                    NativeMethods.DriverStoreOfflineEnumDriverPackageInfoW DriverStoreOfflineEnumDriverPackageInfoW =
                        (NativeMethods.DriverStoreOfflineEnumDriverPackageInfoW)Marshal.PtrToStructure(Ptr, typeof(NativeMethods.DriverStoreOfflineEnumDriverPackageInfoW));
                    Console.Title = $"Driver Updater - DriverStoreOfflineEnumDriverPackageW - {DriverPackageInfPath}";
                    if (DriverStoreOfflineEnumDriverPackageInfoW.InboxInf == 0)
                        existingDrivers.Add(DriverPackageInfPath);
                    return 1;
                }
            , IntPtr.Zero, $"{DevicePart}\\Windows");

            if (ntStatus < 0)
            {
                Logging.Log($"DriverStoreOfflineEnumDriverPackageW: ntStatus={ntStatus}", Logging.LoggingLevel.Error);
                return;
            }

            Logging.Log("Uninstalling drivers...");

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            IntPtr hDriverStore = NativeMethods.DriverStoreOpenW($"{DevicePart}\\Windows", DevicePart, 0, IntPtr.Zero);
            if (hDriverStore == IntPtr.Zero)
            {
                if (ntStatus < 0)
                {
                    Logging.Log("");
                    Logging.Log($"DriverStoreOpenW: ntStatus={Marshal.GetLastWin32Error()}", Logging.LoggingLevel.Error);
                    return;
                }
            }

            foreach (var driver in existingDrivers)
            {
                // Unreflect the modifications done by the driver package first on the target windows image
                Console.Title = $"Driver Updater - DriverStoreUnreflectCriticalW - {driver}";
                Logging.ShowProgress(Progress++, existingDrivers.Count, startTime, false);
                ntStatus = NativeMethods.DriverStoreUnreflectCriticalW(hDriverStore, driver, 0, null);
                if (ntStatus < 0)
                {
                    Logging.Log("");
                    Logging.Log($"DriverStoreUnreflectCriticalW: ntStatus={ntStatus}", Logging.LoggingLevel.Error);
                    NativeMethods.DriverStoreClose(hDriverStore);

                    return;
                }

                // And then remove the driver package from the target image
                Console.Title = $"Driver Updater - DriverStoreDeleteW - {driver}";
                ntStatus = NativeMethods.DriverStoreDeleteW(hDriverStore, driver, 0);
                if (ntStatus < 0)
                {
                    Logging.Log("");
                    Logging.Log($"DriverStoreDeleteW: ntStatus={ntStatus}", Logging.LoggingLevel.Error);
                    NativeMethods.DriverStoreClose(hDriverStore);

                    return;
                }
            }
            Logging.ShowProgress(existingDrivers.Count, existingDrivers.Count, startTime, false);
            Logging.Log("");

            Logging.Log("Installing new drivers...");

            foreach (var path in definitionPaths)
            {
                Logging.Log(path);

                // The where LINQ call is because directory can return .inf_ as well...
                var infs = Directory.EnumerateFiles($"{DriverRepo}\\{path}", "*.inf", SearchOption.AllDirectories)
                    .Where(x => x.EndsWith(".inf", StringComparison.InvariantCultureIgnoreCase));

                Progress = 0;
                startTime = DateTime.Now;

                // Install every inf present in the component folder
                foreach (var inf in infs)
                {
                    var destinationPath = new StringBuilder(260);
                    int destinationPathLength = 260;

                    // First add the driver package to the image
                    Console.Title = $"Driver Updater - DriverStoreImportW - {inf}";
                    Logging.ShowProgress(Progress++, infs.Count(), startTime, false);

                    int maxAttempts = 3;
                    int currentFails = 0;

                    while (currentFails < maxAttempts)
                    {
                        ntStatus = NativeMethods.DriverStoreImportW(hDriverStore, inf, IsARM ? NativeMethods.ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM : NativeMethods.ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM64, "en-us", 0x20 | 0x2000, destinationPath, ref destinationPathLength);

                        /* 
                           Invalid ARG can be thrown when an issue happens with a specific driver inf
                           No investigation done yet, but for now, this will do just fine
                        */
                        if (ntStatus == -2147024809)
                            currentFails++;
                        else
                            break;
                    }

                    if (ntStatus < 0)
                    {
                        Logging.Log("");
                        Logging.Log($"DriverStoreImportW: ntStatus={ntStatus}, destinationPathLength={destinationPathLength}, destinationPath={destinationPath}", Logging.LoggingLevel.Error);
                        NativeMethods.DriverStoreClose(hDriverStore);

                        return;
                    }

                    // And then reflect it into the target image
                    Console.Title = $"Driver Updater - DriverStoreReflectCriticalW - {inf}";
                    maxAttempts = 3;
                    currentFails = 0;

                    while (currentFails < maxAttempts)
                    {
                        ntStatus = NativeMethods.DriverStoreReflectCriticalW(hDriverStore, destinationPath.ToString(), 0, null);

                        /* 
                           Invalid ARG can be thrown when an issue happens with a specific driver inf
                           No investigation done yet, but for now, this will do just fine
                        */
                        if (ntStatus == -2147024809)
                            currentFails++;
                        else
                            break;
                    }

                    if (ntStatus < 0)
                    {
                        Logging.Log("");
                        Logging.Log($"DriverStoreReflectCriticalW: ntStatus={ntStatus}, destinationPathLength={destinationPathLength}, destinationPath={destinationPath}", Logging.LoggingLevel.Error);
                        NativeMethods.DriverStoreClose(hDriverStore);

                        return;
                    }
                }
                Logging.ShowProgress(infs.Count(), infs.Count(), startTime, false);
                Logging.Log("");
            }

            NativeMethods.DriverStoreClose(hDriverStore);
        }
    }
}
