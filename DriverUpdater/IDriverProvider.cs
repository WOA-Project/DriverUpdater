using System;

namespace DriverUpdater
{
    public interface IDriverProvider : IDisposable
    {
        public uint GetInstalledOEMDrivers(out string[] existingDrivers);
        public uint RemoveOfflineDriver(string driverStoreFileName);
        public uint AddOfflineDriver(string driverStoreFileName);
    }
}
