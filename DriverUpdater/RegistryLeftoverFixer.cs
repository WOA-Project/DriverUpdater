using DiscUtils.Registry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DriverUpdater
{
    internal partial class RegistryLeftoverFixer
    {
        private static readonly Regex DriverStoreFolderRegex = new(@"([^\s\\/:*?""<>|]+\.inf_[^\s\\/:*?""<>|]+_)([A-Fa-f0-9]{16})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string[] InboxINFElements = ["qcursext.inf", "storufs.inf", "sdstor.inf", "hidspi_km.inf", "sdbus.inf"];

        public static void FixRegistryPaths(string drivePath)
        {
            // Windows DriverStore directory is located at \Windows\System32\DriverStore\FileRepository
            // Drivers are named using the following way acpi.inf_amd64_f8b60f94eae135e9
            // While the first and second parts of these names are straight forward (driver inf name + architecture),
            // The other part is dependent on the driver signature and thus, if the driver changes, will change.
            // Usually this isn't a problem but uninstalling drivers can often leave left overs in the registry, pointing to now incorrect paths
            // More over, some drivers will crash if these paths are unfixed. Let's attempt to fix them here.

            string driverStorePath = Path.Combine(drivePath, @"Windows\System32\DriverStore\FileRepository");
            Dictionary<string, string> driverStoreMap = BuildDriverStoreMap(driverStorePath);
            _ = ModifyRegistry(Path.Combine(drivePath, @"Windows\System32\config\SYSTEM"), Path.Combine(drivePath, @"Windows\System32\config\SOFTWARE"), driverStoreMap);
        }

        private static Dictionary<string, string> BuildDriverStoreMap(string driverStorePath)
        {
            Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);

            foreach (string directory in Directory.EnumerateDirectories(driverStorePath))
            {
                bool hasCatalog = false;

                foreach (string _ in Directory.EnumerateFiles(directory, "*.cat"))
                {
                    hasCatalog = true;
                    break;
                }

                if (!hasCatalog)
                {
                    continue;
                }

                string folderName = Path.GetFileName(directory);
                int lastUnderscore = folderName.LastIndexOf('_');
                if (lastUnderscore <= 0)
                {
                    continue;
                }

                string prefix = folderName[..(lastUnderscore + 1)];

                if (!map.TryAdd(prefix, folderName))
                {
                    string existingFolder = map[prefix];
                    string existingPath = Path.Combine(driverStorePath, existingFolder);

                    DateTime existingTime = Directory.GetLastWriteTimeUtc(existingPath);
                    DateTime currentTime = Directory.GetLastWriteTimeUtc(directory);
                    if (currentTime > existingTime)
                    {
                        Logging.Log($"Duplicate DriverStore prefix '{prefix}', selecting newer folder '{folderName}' over '{existingFolder}'");
                        map[prefix] = folderName;
                    }
                }
            }

            Logging.Log($"Indexed {map.Count} DriverStore prefixes");
            return map;
        }

        private static bool ContainsInboxInf(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (string inf in InboxINFElements)
            {
                if (value.Contains(inf, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FixDriverStoreReference(string value, Dictionary<string, string> driverStoreMap, out bool updated)
        {
            updated = false;

            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (!value.Contains(".inf_", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            if (!value.Contains('_'))
            {
                return value;
            }

            Match firstMatch = DriverStoreFolderRegex.Match(value);

            if (!firstMatch.Success)
            {
                return value;
            }

            StringBuilder builder = new(value.Length + 64);

            int currentPosition = 0;

            for (Match match = firstMatch; match.Success; match = match.NextMatch())
            {
                string prefix = match.Groups[1].Value;
                string currentFolder = match.Value;
                if (!driverStoreMap.TryGetValue(prefix, out string replacement))
                {
                    continue;
                }

                if (string.Equals(currentFolder, replacement, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                builder.Append(value, currentPosition, match.Index - currentPosition);
                builder.Append(replacement);

                currentPosition = match.Index + match.Length;
                updated = true;
                Logging.Log($"Updated {currentFolder} -> {replacement}");
            }

            if (!updated)
            {
                return value;
            }

            builder.Append(value, currentPosition, value.Length - currentPosition);
            return builder.ToString();
        }

        private static void FixRegistryValue(RegistryKey registryKey, string valueName, Dictionary<string, string> driverStoreMap)
        {
            RegistryValueType type = registryKey.GetValueType(valueName);

            switch (type)
            {
                case RegistryValueType.String:
                case RegistryValueType.ExpandString:
                    {
                        string value = registryKey.GetValue(valueName) as string;

                        if (string.IsNullOrEmpty(value))
                        {
                            return;
                        }

                        if (ContainsInboxInf(value))
                        {
                            return;
                        }

                        string fixedValue = FixDriverStoreReference(value, driverStoreMap, out bool updated);

                        if (!updated)
                        {
                            return;
                        }

                        registryKey.SetValue(valueName, fixedValue, type);
                        break;
                    }

                case RegistryValueType.MultiString:
                    {
                        string[] values = registryKey.GetValue(valueName) as string[];

                        if (values == null)
                        {
                            return;
                        }

                        bool updatedAnything = false;

                        for (int i = 0; i < values.Length; i++)
                        {
                            string value =
                                values[i];

                            if (string.IsNullOrEmpty(value))
                            {
                                continue;
                            }

                            if (ContainsInboxInf(value))
                            {
                                continue;
                            }

                            values[i] = FixDriverStoreReference(value, driverStoreMap, out bool updated);

                            updatedAnything |= updated;
                        }

                        if (updatedAnything)
                        {
                            registryKey.SetValue(valueName, values, RegistryValueType.MultiString);
                        }

                        break;
                    }
            }
        }

        private static void CrawlRegistry(RegistryKey registryKey, Dictionary<string, string> driverStoreMap)
        {
            if (registryKey == null)
            {
                return;
            }

            foreach (string valueName in registryKey.GetValueNames())
            {
                if (ContainsInboxInf(valueName))
                {
                    continue;
                }

                FixRegistryValue(registryKey, valueName, driverStoreMap);
            }

            foreach (RegistryKey subKey in registryKey.SubKeys)
            {
                if (subKey == null)
                {
                    continue;
                }

                CrawlRegistry(subKey, driverStoreMap);
            }
        }

        internal static bool ModifyRegistry(string systemHivePath, string softwareHivePath, Dictionary<string, string> driverStoreMap)
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
                    CrawlRegistry(hive.Root, driverStoreMap);
                }

                using (RegistryHive hive = new(
                    File.Open(
                        softwareHivePath,
                        FileMode.Open,
                        FileAccess.ReadWrite
                    ), DiscUtils.Streams.Ownership.Dispose))
                {
                    Logging.Log("Processing SOFTWARE hive");
                    CrawlRegistry(hive.Root, driverStoreMap);
                }
            }
            catch (Exception ex)
            {
                Logging.Log($"Registry processing failed: {ex}");
                return false;
            }
            return true;
        }

        /*[GeneratedRegex("oem[0-9]+.inf")]
        private static partial Regex OEMInfRegex();*/
    }
}
