using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DriverUpdater
{
    public class DriverStoreProvider : IDriverProvider
    {
        private bool disposedValue;
        private readonly string DevicePart;
        private readonly ProcessorArchitecture processorArchitecture;

        public DriverStoreProvider(string DevicePart, ProcessorArchitecture processorArchitecture)
        {
            this.DevicePart = DevicePart;
            this.processorArchitecture = processorArchitecture;
        }

        public uint AddOfflineDriver(string inf)
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
        }

        public uint GetInstalledOEMDrivers(out string[] existingDrivers)
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

        public uint RemoveOfflineDriver(string driverStoreFileName)
        {
            return NativeMethods.DriverStoreOfflineDeleteDriverPackage(
                driverStoreFileName,
                0,
                IntPtr.Zero,
                $"{DevicePart}\\Windows",
                DevicePart);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
