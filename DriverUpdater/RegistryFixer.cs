using DiscUtils.Registry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DriverUpdater
{
    public partial class RegistryFixer
    {
        public static void FixLeftOvers(string drivePath)
        {
            _ = ModifyRegistryForLeftOvers(Path.Combine(drivePath, @"Windows\System32\config\SYSTEM"), Path.Combine(drivePath, @"Windows\System32\config\SOFTWARE"));
        }

        #region Matching

        private static bool IsLikelyInteresting(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.Contains("QCOM", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("MSHW", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("VEN_QCOM&DEV_", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("VEN_MSHW&DEV_", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("VID_045E&PID_0C01", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("VID_045E&PID_0C1D", StringComparison.OrdinalIgnoreCase)
                   || value.Contains(@"\surface", StringComparison.OrdinalIgnoreCase)
                   || value.Contains(@"\qc", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("surface", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("oem", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSimpleAntiMatch(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.Contains("qcap", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("qcursext", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("hidspi", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("sdstor", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("sdbus", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("storufs", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("chipidea", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("synopsys", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAntiMatching(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (IsSimpleAntiMatch(value))
            {
                return true;
            }

            return AntiDeviceIdRegex().IsMatch(value) || QcInboxRegex().IsMatch(value);
        }

        private static bool TryGetMatch(string value, out Match match)
        {
            match = Match.Empty;
            if (!IsLikelyInteresting(value))
            {
                return false;
            }

            if (IsAntiMatching(value))
            {
                return false;
            }

            match = DeviceIdRegex().Match(value);
            if (match.Success)
            {
                return true;
            }

            match = OemInfRegex().Match(value);
            if (match.Success)
            {
                return true;
            }

            match = SurfaceDuoRegex().Match(value);
            return match.Success;
        }

        #endregion

        private static void FixRegistryValue(RegistryKey registryKey, string registryValue)
        {
            if (registryKey == null)
            {
                return;
            }

            switch (registryKey.GetValueType(registryValue))
            {
                case RegistryValueType.String:
                {
                    string value = registryKey.GetValue(registryValue) as string;
                    if (value == null)
                    {
                        break;
                    }

                    if (TryGetMatch(value, out Match match))
                    {
                        Logging.Log($"Deleting Value (String) {match.Value} in {registryKey.Name}\\{registryValue}");
                        registryKey.DeleteValue(string.IsNullOrEmpty(registryValue) ? null : registryValue);
                    }

                    break;
                }

                case RegistryValueType.ExpandString:
                {
                    string value = registryKey.GetValue(registryValue) as string;
                    if (value == null)
                    {
                        break;
                    }

                    if (TryGetMatch(value, out Match match))
                    {
                        Logging.Log($"Deleting Value (Expand String) {match.Value} in {registryKey.Name}\\{registryValue}");
                        registryKey.DeleteValue(string.IsNullOrEmpty(registryValue) ? null : registryValue);
                    }

                    break;
                }

                case RegistryValueType.MultiString:
                {
                    string[] values = registryKey.GetValue(registryValue) as string[];
                    if (values == null)
                    {
                        break;
                    }

                    List<string> remainingValues = new(values.Length);

                    bool updated = false;
                    foreach (string value in values)
                    {
                        if (value == null)
                        {
                            continue;
                        }

                        if (TryGetMatch(value, out Match match))
                        {
                            Logging.Log($"Deleting Value (Multi String) {match.Value} in {registryKey.Name}\\{registryValue}");
                            updated = true;
                        }
                        else
                        {
                            remainingValues.Add(value);
                        }
                    }

                    if (!updated)
                    {
                        break;
                    }

                    if (remainingValues.Count > 0)
                    {
                        registryKey.SetValue(registryValue, remainingValues.ToArray(), RegistryValueType.MultiString);
                    }
                    else
                    {
                        registryKey.DeleteValue(string.IsNullOrEmpty(registryValue) ? null : registryValue);
                    }

                    break;
                }
            }
        }

        private static void CrawlInRegistryKeyForLeftOvers(RegistryKey registryKey)
        {
            if (registryKey == null)
            {
                return;
            }

            foreach (string valueName in registryKey.GetValueNames())
            {
                if (string.IsNullOrEmpty(valueName))
                {
                    continue;
                }

                if (TryGetMatch(valueName, out _))
                {
                    Logging.Log($"Deleting Value {registryKey.Name}\\{valueName}");
                    registryKey.DeleteValue(valueName);
                    continue;
                }

                if (IsAntiMatching(valueName))
                {
                    continue;
                }

                FixRegistryValue(registryKey, valueName);
            }

            foreach (string subKeyName in registryKey.GetSubKeyNames())
            {
                if (string.IsNullOrEmpty(subKeyName))
                {
                    continue;
                }

                if (TryGetMatch(subKeyName, out _))
                {
                    Logging.Log($"Deleting Sub Key Tree {registryKey.Name}\\{subKeyName}");

                    registryKey.DeleteSubKeyTree(subKeyName);
                    continue;
                }

                if (IsAntiMatching(subKeyName))
                {
                    continue;
                }

                CrawlInRegistryKeyForLeftOvers(registryKey.OpenSubKey(subKeyName));
            }
        }

        internal static bool ModifyRegistryForLeftOvers(string systemHivePath, string softwareHivePath)
        {
            try
            {
                using (RegistryHive hive = new(File.Open(systemHivePath, FileMode.Open, FileAccess.ReadWrite), DiscUtils.Streams.Ownership.Dispose))
                {
                    Logging.Log("Processing SYSTEM hive");

                    RegistryKey enumKey = hive.Root.OpenSubKey(@"ControlSet001\Enum");

                    if (enumKey != null)
                    {
                        foreach (string subKey in enumKey.GetSubKeyNames())
                        {
                            if (subKey.Equals("ROOT", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            Logging.Log($"Deleting Sub Key Tree {enumKey.Name}\\{subKey}");
                            enumKey.DeleteSubKeyTree(subKey);
                        }
                    }

                    if (hive.Root.OpenSubKey("HardwareConfig") != null)
                    {
                        try
                        {
                            hive.Root.DeleteSubKeyTree("HardwareConfig");
                        }
                        catch
                        {
                        }
                    }

                    CrawlInRegistryKeyForLeftOvers(hive.Root);
                }

                using (RegistryHive hive = new(File.Open(softwareHivePath, FileMode.Open, FileAccess.ReadWrite), DiscUtils.Streams.Ownership.Dispose))
                {
                    Logging.Log("Processing SOFTWARE hive");
                    CrawlInRegistryKeyForLeftOvers(hive.Root);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logging.Log($"Registry cleanup failed: {ex}");
                return false;
            }
        }

        [GeneratedRegex(@"oem\d+\.inf", RegexOptions.IgnoreCase)]
        private static partial Regex OemInfRegex();

        [GeneratedRegex(@"(?:QCOM|MSHW|VEN_QCOM&DEV_|VEN_MSHW&DEV_)[0-9A-F]{4}", RegexOptions.IgnoreCase)]
        private static partial Regex DeviceIdRegex();

        [GeneratedRegex(@"QCOM(?:24[0-9A-F]{2}|7002|FFE[0-9A-F])", RegexOptions.IgnoreCase)]
        private static partial Regex AntiDeviceIdRegex();

        [GeneratedRegex(@"surface.*duo.*inf", RegexOptions.IgnoreCase)]
        private static partial Regex SurfaceDuoRegex();

        [GeneratedRegex(@"qc.*_i\.inf", RegexOptions.IgnoreCase)]
        private static partial Regex QcInboxRegex();
    }
}