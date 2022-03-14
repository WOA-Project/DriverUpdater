using Microsoft.Dism;
using System;
using System.Collections.Generic;

namespace DriverUpdater
{
    public class DismDriverProvider : IDriverProvider
    {
        private bool disposedValue;
        private readonly DismSession session;

        public DismDriverProvider(string DrivePath)
        {
            session = DismApi.OpenOfflineSession(DrivePath);
        }

        public uint AddOfflineDriver(string driverStoreFileName)
        {
            uint ntStatus = 0;

            try
            {
                DismApi.AddDriver(session, driverStoreFileName, false);
            }
            catch (Exception e)
            {
                ntStatus = (uint)e.HResult;
            }

            return ntStatus;
        }

        public uint GetInstalledOEMDrivers(out string[] existingDrivers)
        {
            List<string> lexistingDrivers = new();

            uint ntStatus = 0;

            try
            {
                foreach (DismDriverPackage driver in DismApi.GetDrivers(session, false))
                {
                    lexistingDrivers.Add(driver.PublishedName);
                }
            }
            catch (Exception e)
            {
                ntStatus = (uint)e.HResult;
            }

            existingDrivers = lexistingDrivers.ToArray();

            return ntStatus;
        }

        public uint RemoveOfflineDriver(string driverStoreFileName)
        {
            uint ntStatus = 0;

            try
            {
                DismApi.RemoveDriver(session, driverStoreFileName);
            }
            catch (Exception e)
            {
                ntStatus = (uint)e.HResult;
            }

            return ntStatus;
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

                session.Dispose();
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
