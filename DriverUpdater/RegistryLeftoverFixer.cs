using DiscUtils.Registry;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DriverUpdater
{
    internal partial class RegistryLeftoverFixer
    {
        public static void FixRegistryPaths(string DrivePath)
        {
            // TODO: Fix perf
            //string[] inboxINFElements = Directory.EnumerateFiles(Path.Combine(DrivePath, "Windows", "INF"), "*.inf").Select(Path.GetFileName).Where(x => !OEMInfRegex().IsMatch(x)).ToArray();
            string[] inboxINFElements = ["qcursext.inf", "storufs.inf", "sdstor.inf", "hidspi_km.inf", "sdbus.inf", ""];

            // Windows DriverStore directory is located at \Windows\System32\DriverStore\FileRepository
            // Drivers are named using the following way acpi.inf_amd64_f8b60f94eae135e9
            // While the first and second parts of these names are straight forward (driver inf name + architecture),
            // The other part is dependent on the driver signature and thus, if the driver changes, will change.
            // Usually this isn't a problem but uninstalling drivers can often leave left overs in the registry, pointing to now incorrect paths
            // More over, some drivers will crash if these paths are unfixed. Let's attempt to fix them here.

            // First start by getting a list of all driver folder names right now.
            string DriverStorePath = Path.Combine(DrivePath, "Windows\\System32\\DriverStore\\FileRepository");
            string[] Folders = Directory.EnumerateDirectories(DriverStorePath).Where(x => Directory.EnumerateFiles(x, "*.cat").Any()).Select(x => x.Split('\\').Last()).ToArray();

            // Now, create a new array of all folder names, but without the hash dependent part.
            string[] folderRegexes = Folders.Select(driverStoreFolderName => $"{string.Join('_', driverStoreFolderName.Split('_')[..driverStoreFolderName.Count(y => y == '_')])}_").ToArray();

            // Now that this is done, process the hives.
            _ = ModifyRegistry(inboxINFElements, Path.Combine(DrivePath, "Windows\\System32\\config\\SYSTEM"), Path.Combine(DrivePath, "Windows\\System32\\config\\SOFTWARE"), Folders, folderRegexes);
        }

        private static bool ContainsNeutralDriverStoreFolderName(string str, string neutralDriverStoreFolderName, out string match)
        {
            match = null;

            if (str == null)
            {
                return false;
            }

            if (str.Contains(neutralDriverStoreFolderName, StringComparison.InvariantCultureIgnoreCase))
            {
                string escapedNeutralDriverStoreFolderName = Regex.Escape(neutralDriverStoreFolderName);
                Regex regex = new($$"""{{escapedNeutralDriverStoreFolderName}}[a-fA-F0-9]{16}""");
                if (regex.IsMatch(str))
                {
                    match = regex.Match(str).Value;
                    return true;
                }
            }

            return false;
        }

        private static void FixDriverStorePathsInRegistryValue(string[] inboxINFElements, RegistryKey registryKey, string registryValue, string[] currentDriverStoreNames, string[] folderRegexes)
        {
            if (registryKey?.GetValueNames().Any(x => x.Equals(registryValue, StringComparison.InvariantCultureIgnoreCase)) == true)
            {
                switch (registryKey.GetValueType(registryValue))
                {
                    case RegistryValueType.String:
                        {
                            string og = (string)registryKey.GetValue(registryValue);

                            if (inboxINFElements.Any(x => og.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                return;
                            }

                            for (int i = 0; i < folderRegexes.Length; i++)
                            {
                                string matchingString = currentDriverStoreNames[i];

                                if (ContainsNeutralDriverStoreFolderName(og, folderRegexes[i], out string currentValue))
                                {
                                    if (currentValue != matchingString)
                                    {
                                        Logging.Log($"Updated {currentValue} to {matchingString} in {registryKey.Name}\\{registryValue}");

                                        og = og.Replace(currentValue, matchingString);
                                        registryKey.SetValue(registryValue, og, RegistryValueType.String);
                                        break;
                                    }
                                }
                            }

                            break;
                        }
                    case RegistryValueType.ExpandString:
                        {
                            string og = (string)registryKey.GetValue(registryValue);

                            if (inboxINFElements.Any(x => og.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                return;
                            }

                            for (int i = 0; i < folderRegexes.Length; i++)
                            {
                                string matchingString = currentDriverStoreNames[i];

                                if (ContainsNeutralDriverStoreFolderName(og, folderRegexes[i], out string currentValue))
                                {
                                    if (currentValue != matchingString)
                                    {
                                        Logging.Log($"Updated {currentValue} to {matchingString} in {registryKey.Name}\\{registryValue}");

                                        og = og.Replace(currentValue, matchingString);
                                        registryKey.SetValue(registryValue, og, RegistryValueType.ExpandString);
                                        break;
                                    }
                                }
                            }

                            break;
                        }
                    case RegistryValueType.MultiString:
                        {
                            string[] ogvals = (string[])registryKey.GetValue(registryValue);

                            bool updated = false;

                            for (int j = 0; j < ogvals.Length; j++)
                            {
                                string og = ogvals[j];

                                if (inboxINFElements.Any(x => og.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
                                {
                                    continue;
                                }

                                for (int i = 0; i < folderRegexes.Length; i++)
                                {
                                    string matchingString = currentDriverStoreNames[i];

                                    if (ContainsNeutralDriverStoreFolderName(og, folderRegexes[i], out string currentValue))
                                    {
                                        if (currentValue != matchingString)
                                        {
                                            Logging.Log($"Updated {currentValue} to {matchingString} in {registryKey.Name}\\{registryValue}");

                                            og = og.Replace(currentValue, matchingString);
                                            ogvals[j] = og;
                                            updated = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (updated)
                            {
                                registryKey.SetValue(registryValue, ogvals, RegistryValueType.MultiString);
                            }

                            break;
                        }
                }
            }
        }

        private static void CrawlInRegistryKey(string[] inboxINFElements, RegistryKey registryKey, string[] currentDriverStoreNames, string[] folderRegexes)
        {
            if (registryKey != null)
            {
                foreach (string subRegistryValue in registryKey.GetValueNames())
                {
                    if (inboxINFElements.Any(x => subRegistryValue.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        continue;
                    }

                    FixDriverStorePathsInRegistryValue(inboxINFElements, registryKey, subRegistryValue, currentDriverStoreNames, folderRegexes);
                }

                foreach (RegistryKey subRegistryKey in registryKey.SubKeys)
                {
                    if (inboxINFElements.Any(x => subRegistryKey.Name.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        continue;
                    }

                    CrawlInRegistryKey(inboxINFElements, subRegistryKey, currentDriverStoreNames, folderRegexes);
                }
            }
        }

        internal static bool ModifyRegistry(string[] inboxINFElements, string systemHivePath, string softwareHivePath, string[] currentDriverStoreNames, string[] folderRegexes)
        {
            try
            {
                using (RegistryHive hive = new(
                    File.Open(
                        systemHivePath,
                        FileMode.Open,
                        FileAccess.ReadWrite
                    ), DiscUtils.Streams.Ownership.Dispose))
                {
                    Logging.Log("Processing SYSTEM hive");
                    CrawlInRegistryKey(inboxINFElements, hive.Root, currentDriverStoreNames, folderRegexes);
                }

                using (RegistryHive hive = new(
                    File.Open(
                        softwareHivePath,
                        FileMode.Open,
                        FileAccess.ReadWrite
                    ), DiscUtils.Streams.Ownership.Dispose))
                {
                    Logging.Log("Processing SOFTWARE hive");
                    CrawlInRegistryKey(inboxINFElements, hive.Root, currentDriverStoreNames, folderRegexes);
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        [GeneratedRegex("oem[0-9]+.inf")]
        private static partial Regex OEMInfRegex();
    }
}
