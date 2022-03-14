/*
 * Copyright (c) The LumiaWOA and DuoWOA authors
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
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
            DismApi.Initialize(DismLogLevel.LogErrors);
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
