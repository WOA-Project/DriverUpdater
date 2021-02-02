using System;
using System.IO;
using System.Linq;

namespace DriverUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Count() < 3)
            {
                Console.WriteLine("Usage: DriverUpdater <Path to definition> <Path to Driver repository> <Path to Device partition>");
                return;
            }

            string Definition = args[0];
            string DriverRepo = args[1];
            string DevicePart = args[2];

            if (!File.Exists(Definition) || !Directory.Exists(DriverRepo) || !Directory.Exists(DevicePart))
            {
                Console.WriteLine("Usage: DriverUpdater <Path to definition> <Path to Driver repository> <Path to Device partition>");
                return;
            }

            try
            {
                Install(Definition, DriverRepo, DevicePart);
                ResealForPnPFirstBootUx(DevicePart);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something happened!");
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine("Done!");
        }

        static void ResealForPnPFirstBootUx(string DevicePart)
        {
            using var hive = new DiscUtils.Registry.RegistryHive(File.Open(Path.Combine(DevicePart, "Windows\\System32\\config\\SYSTEM"), FileMode.Open, FileAccess.ReadWrite), DiscUtils.Streams.Ownership.Dispose);
            var hwconf = hive.Root.OpenSubKey("HardwareConfig");
            if (hwconf != null)
            {
                Console.WriteLine("Resealing image to PnP FirstBootUx...");
                foreach (var subkey in hwconf.GetSubKeyNames())
                    hwconf.DeleteSubKeyTree(subkey);
                foreach (var subval in hwconf.GetValueNames())
                    hwconf.DeleteValue(subval);
            }
        }

        static void Install(string Definition, string DriverRepo, string DevicePart)
        {
            Console.WriteLine("Reading definition file...");
            string[] definitionPaths = File.ReadAllLines(Definition).Where(x => !string.IsNullOrEmpty(x))
                .Union(new string[] { "components\\ANYSOC\\SUPPORT.DESKTOP.POST_UPGRADE_ENABLEMENT" }).ToArray();

            Microsoft.Dism.DismApi.InitializeEx(Microsoft.Dism.DismLogLevel.LogErrorsWarningsInfo);

            Console.WriteLine("Opening Session...");
            using var deviceSession = Microsoft.Dism.DismApi.OpenOfflineSession(DevicePart);

            Console.WriteLine("Enumerating existing drivers...");
            var existingDrivers = Microsoft.Dism.DismApi.GetDrivers(deviceSession, false);

            var orderedExistingDrivers = existingDrivers.OrderBy(driver => $"{driver.ClassDescription}\\{driver.OriginalFileName.Split("\\")[^1]}");

            Console.WriteLine("Uninstalling drivers...");

            foreach (var driver in orderedExistingDrivers)
            {
                Console.WriteLine($"{driver.ClassDescription}\\{driver.OriginalFileName.Split("\\")[^1]} - Version: {driver.Version} - Date: {driver.Date}");
                Microsoft.Dism.DismApi.RemoveDriver(deviceSession, driver.PublishedName);
            }

            Console.WriteLine("Installing new drivers...");

            foreach (var path in definitionPaths)
            {
                int maxAttempts = 3;
                int currentAttempts = 0;
                bool success = false;
                while (!success)
                {
                    try
                    {
                        Console.WriteLine(path);
                        Microsoft.Dism.DismApi.AddDriversEx(deviceSession, $"{DriverRepo}\\{path}", true, true);
                        success = true;
                    }
                    catch
                    {
                        if (currentAttempts == maxAttempts)
                            throw;
                        Console.WriteLine("Retrying...");
                        currentAttempts++;
                    }
                }
            }
        }
    }
}
