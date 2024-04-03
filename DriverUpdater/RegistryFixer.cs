using DiscUtils.Registry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DriverUpdater
{
    public partial class RegistryFixer
    {
        private static readonly Regex regex = DeviceRegex();
        private static readonly Regex antiRegex = AntiDeviceRegex();

        public static void FixLeftOvers(string DrivePath)
        {
            _ = ModifyRegistryForLeftOvers(Path.Combine(DrivePath, "Windows\\System32\\config\\SYSTEM"), Path.Combine(DrivePath, "Windows\\System32\\config\\SOFTWARE"));
        }

        private static bool IsMatching(string value)
        {
            return regex.IsMatch(value) && !IsAntiMatching(value);
        }

        private static bool IsAntiMatching(string value)
        {
            return antiRegex.IsMatch(value);
        }

        private static Match GetMatch(string value)
        {
            return regex.Match(value);
        }

        private static void FixDriverStorePathsInRegistryValueForLeftOvers(RegistryKey registryKey, string registryValue)
        {
            // TODO:
            // Key: Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\ ... \
            // Val: Owners

            if (registryKey?.GetValueNames().Any(x => x.Equals(registryValue, StringComparison.InvariantCultureIgnoreCase)) == true)
            {
                switch (registryKey.GetValueType(registryValue))
                {
                    case RegistryValueType.String:
                        {
                            string og = (string)registryKey.GetValue(registryValue);

                            if (IsMatching(og))
                            {
                                string currentValue = GetMatch(og).Value;

                                Logging.Log($"Deleting Value (String) {currentValue} in {registryKey.Name}\\{registryValue}");

                                if (registryValue == "")
                                {
                                    registryValue = null;
                                }

                                registryKey.DeleteValue(registryValue);
                            }

                            break;
                        }
                    case RegistryValueType.ExpandString:
                        {
                            string og = (string)registryKey.GetValue(registryValue);

                            if (IsMatching(og))
                            {
                                string currentValue = GetMatch(og).Value;

                                Logging.Log($"Deleting Value (Expand String) {currentValue} in {registryKey.Name}\\{registryValue}");

                                if (registryValue == "")
                                {
                                    registryValue = null;
                                }

                                registryKey.DeleteValue(registryValue);
                            }

                            break;
                        }
                    case RegistryValueType.MultiString:
                        {
                            string[] ogvals = (string[])registryKey.GetValue(registryValue);
                            List<string> newVals = [];

                            bool updated = false;

                            foreach (string og in ogvals)
                            {
                                if (IsMatching(og))
                                {
                                    string currentValue = GetMatch(og).Value;

                                    Logging.Log($"Deleting Value (Multi String) {currentValue} in {registryKey.Name}\\{registryValue}");
                                    updated = true;
                                }
                                else
                                {
                                    newVals.Add(og);
                                }
                            }

                            if (updated)
                            {
                                if (newVals.Count != 0)
                                {
                                    registryKey.SetValue(registryValue, newVals.ToArray(), RegistryValueType.MultiString);
                                }
                                else
                                {
                                    if (registryValue == "")
                                    {
                                        registryValue = null;
                                    }

                                    registryKey.DeleteValue(registryValue);
                                }
                            }

                            break;
                        }
                }
            }
        }

        private static void CrawlInRegistryKeyForLeftOvers(RegistryKey registryKey)
        {
            if (registryKey != null)
            {
                foreach (string subRegistryValue in registryKey.GetValueNames())
                {
                    if (IsMatching(subRegistryValue))
                    {
                        Logging.Log($"Deleting Value {registryKey.Name}\\{subRegistryValue}");
                        registryKey.DeleteValue(subRegistryValue);

                        continue;
                    }

                    if (IsAntiMatching(subRegistryValue))
                    {
                        continue;
                    }

                    FixDriverStorePathsInRegistryValueForLeftOvers(registryKey, subRegistryValue);
                }

                foreach (string subRegistryKey in registryKey.GetSubKeyNames())
                {
                    if (IsMatching(subRegistryKey))
                    {
                        Logging.Log($"Deleting Sub Key Tree {registryKey.Name}\\{subRegistryKey}");
                        registryKey.DeleteSubKeyTree(subRegistryKey);

                        continue;
                    }

                    if (IsAntiMatching(subRegistryKey))
                    {
                        continue;
                    }

                    CrawlInRegistryKeyForLeftOvers(registryKey.OpenSubKey(subRegistryKey));
                }
            }
        }

        internal static bool ModifyRegistryForLeftOvers(string systemHivePath, string softwareHivePath)
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
                    CrawlInRegistryKeyForLeftOvers(hive.Root);
                }

                using (RegistryHive hive = new(
                    File.Open(
                        softwareHivePath,
                        FileMode.Open,
                        FileAccess.ReadWrite
                    ), DiscUtils.Streams.Ownership.Dispose))
                {
                    Logging.Log("Processing SOFTWARE hive");
                    CrawlInRegistryKeyForLeftOvers(hive.Root);
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        [GeneratedRegex("""(.*oem[0-9]+\.inf.*)|(.*(QCOM|MSHW|VEN_QCOM&DEV_|VEN_MSHW&DEV_|qcom|mshw|ven_qcom&dev_|ven_mshw&dev_)[0-9A-Fa-f]{4}.*)|(.*surface.*duo.*inf)|(.*\\qc.*)|(.*\\surface.*)""")]
        private static partial Regex DeviceRegex();

        [GeneratedRegex("""(.*(QCOM|qcom)((24[0-9A-Fa-f][0-9A-Fa-f])|(7002)|((FFE|ffe)[0-9A-Fa-f])).*)|(.*qcap.*)|(.*qcursext.*)|(.*hidspi.*)|(.*ufsstor.*)|(.*sdstor.*)|(.*sdbus.*)|(.*storufs.*)|(.*u..chipidea.*)|(.*u..synopsys.*)|(.*qc.*_i\.inf.*)""")]
        private static partial Regex AntiDeviceRegex();
    }
}