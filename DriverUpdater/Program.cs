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
using System.IO;
using System.Linq;
using System.Text;

namespace DriverUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Count() < 3)
            {
                Logging.Log("Usage: DriverUpdater <Path to definition> <Path to Driver repository> <Path to Device partition>");
                return;
            }

            string Definition = args[0];
            string DriverRepo = args[1];
            string DevicePart = args[2];

            if (!File.Exists(Definition) || !Directory.Exists(DriverRepo) || !Directory.Exists(DevicePart))
            {
                Logging.Log("Usage: DriverUpdater <Path to definition> <Path to Driver repository> <Path to Device partition>");
                return;
            }

            try
            {
                Install(Definition, DriverRepo, DevicePart);
            }
            catch (Exception ex)
            {
                Logging.Log("Something happened!", Logging.LoggingLevel.Error);
                Logging.Log(ex.ToString(), Logging.LoggingLevel.Error);
            }

            Logging.Log("Done!");
        }

        static bool ResealForPnPFirstBootUx(string DevicePart)
        {
            try
            {
                using var hive = new DiscUtils.Registry.RegistryHive(File.Open(Path.Combine(DevicePart, "Windows\\System32\\config\\SYSTEM"), FileMode.Open, FileAccess.ReadWrite), DiscUtils.Streams.Ownership.Dispose);
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
            }
            catch { }

            return false;
        }

        static void Install(string Definition, string DriverRepo, string DevicePart)
        {
            Logging.Log("Reading definition file...");
            string[] definitionPaths = File.ReadAllLines(Definition).Where(x => !string.IsNullOrEmpty(x)).ToArray();

            //if (ResealForPnPFirstBootUx(DevicePart))
            {
                definitionPaths = File.ReadAllLines(Definition).Where(x => !string.IsNullOrEmpty(x))
                    .Union(new string[] { "components\\ANYSOC\\SUPPORT.DESKTOP.POST_UPGRADE_ENABLEMENT" }).ToArray();
            }

            Logging.Log("Enumerating existing drivers...");

            List<string> existingDrivers = new List<string>();

            var ntStatus = NativeMethods.DriverStoreOfflineEnumDriverPackageW(
                (
                    string DriverPackageInfPath, 
                    NativeMethods.DriverStoreOfflineEnumDriverPackageInfoW DriverStoreOfflineEnumDriverPackageInfoW, 
                    IntPtr Unknown
                ) =>
                {
                    Console.Title = $"Driver Updater - DriverStoreOfflineEnumDriverPackageW - {DriverPackageInfPath}";
                    if (DriverStoreOfflineEnumDriverPackageInfoW.InboxInf == 0)
                        existingDrivers.Add(DriverPackageInfPath);
                    return 1;
                }
            , IntPtr.Zero, $"{DevicePart}\\Windows");

            if (ntStatus != 0)
            {
                Logging.Log($"DriverStoreOfflineEnumDriverPackageW: ntStatus={ntStatus}", Logging.LoggingLevel.Error);
                return;
            }

            Logging.Log("Uninstalling drivers...");

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            foreach (var driver in existingDrivers)
            {
                Console.Title = $"Driver Updater - DriverStoreOfflineDeleteDriverPackageW - {driver}";
                Logging.ShowProgress(Progress++, existingDrivers.Count, startTime, false);
                ntStatus = NativeMethods.DriverStoreOfflineDeleteDriverPackageW(driver, 0, IntPtr.Zero, $"{DevicePart}\\Windows", DevicePart);

                if (ntStatus != 0)
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

                foreach (var inf in infs)
                {
                    var destinationPath = new StringBuilder(260);
                    int destinationPathLength = 260;

                    Console.Title = $"Driver Updater - DriverStoreOfflineAddDriverPackageW - {inf}";
                    Logging.ShowProgress(Progress++, infs.Count(), startTime, false);
                    ntStatus = NativeMethods.DriverStoreOfflineAddDriverPackageW(inf, 0x00000020 | 0x00000080 | 0x00000100, IntPtr.Zero, 12, "en-US", destinationPath, ref destinationPathLength, $"{DevicePart}\\Windows", DevicePart);

                    if (ntStatus != 0)
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
    }
}
