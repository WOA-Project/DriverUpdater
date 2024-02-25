using DiscUtils.Registry;
using System.Collections.Generic;
using System.IO;

namespace DriverUpdater
{
    internal class CksLicensing
    {
        private readonly string DrivePath = "";

        public CksLicensing(string DrivePath)
        {
            this.DrivePath = DrivePath;
        }

        private static readonly int[] Empty = [];

        private static int[] Locate(byte[] self, byte[] candidate)
        {
            if (IsEmptyLocate(self, candidate))
            {
                return Empty;
            }

            List<int>? list = [];

            for (int i = 0; i < self.Length; i++)
            {
                if (!IsMatch(self, i, candidate))
                {
                    continue;
                }

                list.Add(i);
            }

            return list.Count == 0 ? Empty : [.. list];
        }

        private static bool IsMatch(byte[] array, int position, byte[] candidate)
        {
            if (candidate.Length > (array.Length - position))
            {
                return false;
            }

            for (int i = 0; i < candidate.Length; i++)
            {
                if (array[position + i] != candidate[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsEmptyLocate(byte[] array, byte[] candidate)
        {
            return array == null
                || candidate == null
                || array.Length == 0
                || candidate.Length == 0
                || candidate.Length > array.Length;
        }

        public void SetLicensedState()
        {
            try
            {
                using RegistryHive hive = new(
                    File.Open(
                        Path.Combine(DrivePath, "Windows\\System32\\config\\SYSTEM"),
                        FileMode.Open,
                        FileAccess.ReadWrite
                    ), DiscUtils.Streams.Ownership.Dispose);

                RegistryKey key = hive.Root.OpenSubKey("ControlSet001\\Control\\ProductOptions");
                byte[] value = (byte[])key.GetValue("ProductPolicy");

                byte[] oldValue =
                [
                    0x43, 0x00, 0x6F, 0x00, 0x64, 0x00, 0x65, 0x00, 0x49, 0x00, 0x6E,
                    0x00, 0x74, 0x00, 0x65, 0x00, 0x67, 0x00, 0x72, 0x00, 0x69, 0x00,
                    0x74, 0x00, 0x79, 0x00, 0x2D, 0x00, 0x41, 0x00, 0x6C, 0x00, 0x6C,
                    0x00, 0x6F, 0x00, 0x77, 0x00, 0x43, 0x00, 0x6F, 0x00, 0x6E, 0x00,
                    0x66, 0x00, 0x69, 0x00, 0x67, 0x00, 0x75, 0x00, 0x72, 0x00, 0x61,
                    0x00, 0x62, 0x00, 0x6C, 0x00, 0x65, 0x00, 0x50, 0x00, 0x6F, 0x00,
                    0x6C, 0x00, 0x69, 0x00, 0x63, 0x00, 0x79, 0x00, 0x2D, 0x00, 0x43,
                    0x00, 0x75, 0x00, 0x73, 0x00, 0x74, 0x00, 0x6F, 0x00, 0x6D, 0x00,
                    0x4B, 0x00, 0x65, 0x00, 0x72, 0x00, 0x6E, 0x00, 0x65, 0x00, 0x6C,
                    0x00, 0x53, 0x00, 0x69, 0x00, 0x67, 0x00, 0x6E, 0x00, 0x65, 0x00,
                    0x72, 0x00, 0x73, 0x00, 0x00, 0x00, 0x00, 0x00
                ];

                byte[] newValue =
                [
                    0x43, 0x00, 0x6F, 0x00, 0x64, 0x00, 0x65, 0x00, 0x49, 0x00, 0x6E,
                    0x00, 0x74, 0x00, 0x65, 0x00, 0x67, 0x00, 0x72, 0x00, 0x69, 0x00,
                    0x74, 0x00, 0x79, 0x00, 0x2D, 0x00, 0x41, 0x00, 0x6C, 0x00, 0x6C,
                    0x00, 0x6F, 0x00, 0x77, 0x00, 0x43, 0x00, 0x6F, 0x00, 0x6E, 0x00,
                    0x66, 0x00, 0x69, 0x00, 0x67, 0x00, 0x75, 0x00, 0x72, 0x00, 0x61,
                    0x00, 0x62, 0x00, 0x6C, 0x00, 0x65, 0x00, 0x50, 0x00, 0x6F, 0x00,
                    0x6C, 0x00, 0x69, 0x00, 0x63, 0x00, 0x79, 0x00, 0x2D, 0x00, 0x43,
                    0x00, 0x75, 0x00, 0x73, 0x00, 0x74, 0x00, 0x6F, 0x00, 0x6D, 0x00,
                    0x4B, 0x00, 0x65, 0x00, 0x72, 0x00, 0x6E, 0x00, 0x65, 0x00, 0x6C,
                    0x00, 0x53, 0x00, 0x69, 0x00, 0x67, 0x00, 0x6E, 0x00, 0x65, 0x00,
                    0x72, 0x00, 0x73, 0x00, 0x01, 0x00, 0x00, 0x00
                ];

                int[] positions = Locate(value, oldValue);

                bool patched = false;

                foreach (int position in positions)
                {
                    patched = true;

                    for (int i = 0; i < newValue.Length; i++)
                    {
                        value[i + position] = newValue[i];
                    }
                }

                if (patched)
                {
                    key.SetValue("ProductPolicy", value);
                }

                key = hive.Root.OpenSubKey("ControlSet001\\Control\\CI\\Protected");
                key.SetValue("Licensed", 0x00000001);

                key = hive.Root.OpenSubKey("ControlSet001\\Control\\CI\\Policy");
                key.SetValue("WhqlSettings", 0x00000001);
            }
            catch
            {

            }
        }
    }
}