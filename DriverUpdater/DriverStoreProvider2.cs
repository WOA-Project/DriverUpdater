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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DriverUpdater
{
    public class DriverStoreProvider2 : IDriverProvider
    {
        private bool disposedValue;
        private readonly string DevicePart;
        private readonly IntPtr hDriverStore;
        private readonly ProcessorArchitecture processorArchitecture;

        public DriverStoreProvider2(string DevicePart, ProcessorArchitecture processorArchitecture)
        {
            this.DevicePart = DevicePart;
            this.processorArchitecture = processorArchitecture;
            hDriverStore = NativeMethods.DriverStoreOpen($"{DevicePart}\\Windows", DevicePart, 0, IntPtr.Zero);
            if (hDriverStore == IntPtr.Zero)
            {
                throw new Win32Exception();
            }
        }

        public uint AddOfflineDriver(string inf)
        {
            uint ntStatus;

            StringBuilder driverStoreFileName = new(260);

            ntStatus = NativeMethods.DriverStoreImport(
                hDriverStore,
                inf,
                processorArchitecture,
                null,
                DriverStoreImportFlag.None,
                driverStoreFileName,
                driverStoreFileName.Capacity);
            if ((ntStatus & 0x80000000) != 0)
            {
                goto exit;
            }

            StringBuilder publishedFileName = new(260);
            bool isPublishedFileNameChanged = false;

            ntStatus = NativeMethods.DriverStorePublish(
                hDriverStore,
                driverStoreFileName.ToString(),
                DriverStorePublishFlag.None,
                publishedFileName,
                publishedFileName.Capacity,
                ref isPublishedFileNameChanged);
            if ((ntStatus & 0x80000000) != 0)
            {
                goto exit;
            }

            ntStatus = NativeMethods.DriverStoreReflectCritical(
                hDriverStore,
                driverStoreFileName.ToString(),
                DriverStoreReflectCriticalFlag.None,
                null);
            if ((ntStatus & 0x80000000) != 0)
            {
                goto exit;
            }

        exit:
            return ntStatus;
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
            uint ntStatus = NativeMethods.DriverStoreUnreflectCritical(
                hDriverStore,
                driverStoreFileName,
                0,
                null);
            if ((ntStatus & 0x80000000) != 0)
            {
                goto exit;
            }

            StringBuilder publishedFileName = new(260);
            bool isPublishedFileNameChanged = false;

            ntStatus = NativeMethods.DriverStoreUnpublish(
                hDriverStore,
                driverStoreFileName,
                DriverStoreUnpublishFlag.None,
                publishedFileName,
                publishedFileName.Capacity,
                ref isPublishedFileNameChanged);
            if ((ntStatus & 0x80000000) != 0)
            {
                goto exit;
            }

            ntStatus = NativeMethods.DriverStoreDelete(hDriverStore, driverStoreFileName, 0);
            if ((ntStatus & 0x80000000) != 0)
            {
                goto exit;
            }

        exit:
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
