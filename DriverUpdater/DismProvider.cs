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
using System.IO;
using System.Linq;

namespace DriverUpdater
{
    internal class DismProvider : IDisposable
    {
        private bool disposedValue;
        private readonly DismSession session;

        public DismProvider(string DrivePath)
        {
            // Workaround for issue in DISM api
            if (!DrivePath.EndsWith('\\'))
            {
                DrivePath += "\\";
            }
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
            List<string> lexistingDrivers = [];

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

            existingDrivers = [.. lexistingDrivers];

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

        public bool InstallApplications(IEnumerable<(string, string)> applicationPaths)
        {
            Logging.Log("Installing App Packages...");

            IEnumerable<(string, string)> applicationPackages = applicationPaths.Where(x => !Path.GetDirectoryName(x.Item1).EndsWith(Path.DirectorySeparatorChar + "Frameworks"));

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            foreach ((string AppXFilePath, string LicenseFilePath) in applicationPackages)
            {
                Console.Title = $"Driver Updater - Installing App Package - {AppXFilePath}";
                Logging.ShowProgress(Progress++, applicationPackages.Count(), startTime, false, "Installing App Packages...", Path.GetFileName(AppXFilePath));

                const int maxAttempts = 3;
                int currentFails = 0;
                ulong ntStatus = 0;

                while (currentFails < maxAttempts)
                {
                    try
                    {
                        DismApi.AddProvisionedAppxPackage(session, AppXFilePath, null, !string.IsNullOrEmpty(LicenseFilePath) ? LicenseFilePath : null, null);
                    }
                    catch (Exception e)
                    {
                        ntStatus = (uint)e.HResult;
                    }

                    // Invalid ARG can be thrown when an issue happens with a specific driver inf
                    // No investigation done yet, but for now, this will do just fine
                    if ((ntStatus & 0x80000000) != 0 && ntStatus != 0xC1570118)
                    {
                        currentFails++;
                    }
                    else
                    {
                        break;
                    }
                }

                if ((ntStatus & 0x80000000) != 0 && ntStatus != 0xC1570118)
                {
                    Logging.Log("");
                    Logging.Log($"DismApi->AddProvisionedAppxPackage: ntStatus=0x{ntStatus:X8}, app={AppXFilePath}", Logging.LoggingLevel.Error);

                    //return false;
                }
            }

            Logging.ShowProgress(applicationPackages.Count(), applicationPackages.Count(), startTime, false, "Installing App Packages...", "");
            Logging.Log("");

            return true;
        }

        private bool UninstallDrivers(string[] existingOEMDrivers)
        {
            long Progress;
            DateTime startTime;

            Logging.Log("Uninstalling drivers...");

            Progress = 0;
            startTime = DateTime.Now;

            foreach (string driver in existingOEMDrivers)
            {
                Console.Title = $"Driver Updater - RemoveOfflineDriver - {driver}";
                Logging.ShowProgress(Progress++, existingOEMDrivers.Length, startTime, false, "Uninstalling drivers...", driver);

                uint ntStatus = RemoveOfflineDriver(driver);
                if ((ntStatus & 0x80000000) != 0)
                {
                    Logging.Log("");
                    Logging.Log($"RemoveOfflineDriver: ntStatus=0x{ntStatus:X8}, driver={driver}", Logging.LoggingLevel.Error);

                    //return false;
                }
            }
            Logging.ShowProgress(existingOEMDrivers.Length, existingOEMDrivers.Length, startTime, false, "Uninstalling drivers...", "");
            Logging.Log("");

            return true;
        }

        public bool UninstallExistingDrivers()
        {
            Logging.Log("Enumerating existing drivers...");

            uint ntStatus = GetInstalledOEMDrivers(out string[] existingOEMDrivers);

            if ((ntStatus & 0x80000000) != 0)
            {
                Logging.Log($"DriverStoreOfflineEnumDriverPackage: ntStatus=0x{ntStatus:X8}", Logging.LoggingLevel.Error);
                //return false;
            }

            return UninstallDrivers(existingOEMDrivers);
        }

        public bool InstallDrivers(IEnumerable<string> infFiles)
        {
            Logging.Log("Installing new drivers...");

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            // Install every inf present in the component folder
            foreach (string inf in infFiles)
            {
                // First add the driver package to the image
                Console.Title = $"Driver Updater - DismApi->AddDriver - {inf}";
                Logging.ShowProgress(Progress++, infFiles.Count(), startTime, false, "Installing new drivers...", Path.GetFileName(inf));

                const int maxAttempts = 3;
                int currentFails = 0;
                uint ntStatus = 0;

                while (currentFails < maxAttempts)
                {
                    ntStatus = AddOfflineDriver(inf);

                    // Invalid ARG can be thrown when an issue happens with a specific driver inf
                    // No investigation done yet, but for now, this will do just fine
                    if ((ntStatus & 0x80000000) != 0)
                    {
                        currentFails++;
                    }
                    else
                    {
                        break;
                    }
                }

                if ((ntStatus & 0x80000000) != 0)
                {
                    Logging.Log("");
                    Logging.Log($"DismApi->AddDriver: ntStatus=0x{ntStatus:X8}, driverInf={inf}", Logging.LoggingLevel.Error);

                    //return false;
                }
            }
            Logging.ShowProgress(infFiles.Count(), infFiles.Count(), startTime, false, "Installing new drivers...", "");
            Logging.Log("");

            return true;
        }

        public bool InstallFrameworkDependencies(IEnumerable<(string AppXFilePath, string LicenseFilePath)> deps)
        {
            IEnumerable<(string AppXFilePath, string LicenseFilePath)> appDependencyPackages = deps.Where(x => x.AppXFilePath.Replace(Path.DirectorySeparatorChar + Path.GetFileName(x.AppXFilePath), "").EndsWith(Path.DirectorySeparatorChar + "Frameworks"));

            Logging.Log("Installing App Framework Packages...");

            long Progress = 0;
            DateTime startTime = DateTime.Now;

            foreach ((string AppXFilePath, string LicenseFilePath) in appDependencyPackages)
            {
                Console.Title = $"Driver Updater - Installing App Framework Package - {AppXFilePath}";
                Logging.ShowProgress(Progress++, appDependencyPackages.Count(), startTime, false, "Installing App Framework Packages...", Path.GetFileName(AppXFilePath));

                const int maxAttempts = 3;
                int currentFails = 0;
                ulong ntStatus = 0;

                while (currentFails < maxAttempts)
                {
                    try
                    {
                        DismApi.AddProvisionedAppxPackage(session, AppXFilePath, null, !string.IsNullOrEmpty(LicenseFilePath) ? LicenseFilePath : null, null);
                    }
                    catch (Exception e)
                    {
                        ntStatus = (uint)e.HResult;
                    }

                    // Invalid ARG can be thrown when an issue happens with a specific driver inf
                    // No investigation done yet, but for now, this will do just fine
                    if ((ntStatus & 0x80000000) != 0 && ntStatus != 0xC1570118)
                    {
                        currentFails++;
                    }
                    else
                    {
                        break;
                    }
                }

                if ((ntStatus & 0x80000000) != 0 && ntStatus != 0xC1570118)
                {
                    Logging.Log("");
                    Logging.Log($"DismApi->AddProvisionedAppxPackage: ntStatus=0x{ntStatus:X8}, app={AppXFilePath}", Logging.LoggingLevel.Error);

                    //return false;
                }
            }

            Logging.ShowProgress(appDependencyPackages.Count(), appDependencyPackages.Count(), startTime, false, "Installing App Framework Packages...", "");
            Logging.Log("");

            return true;
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
