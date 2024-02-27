using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DriverStore
{
    public partial class DrvStore : IDisposable
    {
        public const uint DriverDatabaseConfigOptionsOneCore = 51U;

        internal const int ERROR_INSUFFICIENT_BUFFER = 122;

        internal const int ERROR_NOT_FOUND = 1168;

        private static readonly object syncRoot = new();

        private bool disposed;

        private nint _hDrvStore = nint.Zero;

        private readonly string _stagingRootDirectory;

        private readonly string _stagingSystemDirectory;

        private readonly string _targetBootDrive;

        public DrvStore(string stagingPath, string targetBootDrive)
        {
            _stagingRootDirectory = Environment.ExpandEnvironmentVariables(stagingPath);
            _stagingSystemDirectory = Path.Combine(_stagingRootDirectory, "windows");
            _targetBootDrive = targetBootDrive;
        }

        ~DrvStore()
        {
            Dispose(false);
        }

        public void Create()
        {
            Console.WriteLine($"Diagnostic: Creating driver store at {_stagingSystemDirectory}");
            _ = Directory.CreateDirectory(_stagingSystemDirectory);

            if (_hDrvStore != nint.Zero)
            {
                Console.WriteLine($"Diagnostic: Attempting to open a driver store that was not closed {_hDrvStore}");
                Close();
            }

            try
            {
                _hDrvStore = DriverStoreInterop.DriverStoreOpen(_stagingSystemDirectory, _targetBootDrive, DriverStoreOpenFlag.Create, nint.Zero);
            }
            catch
            {
                throw new Exception("DriverStoreOpen failed");
            }

            if (_hDrvStore == nint.Zero)
            {
                int lastWin32Error = Marshal.GetLastWin32Error();
                Console.WriteLine($"Error: DriverStoreOpen failed error 0x{lastWin32Error:X8}");
                throw new Win32Exception(lastWin32Error);
            }

            Console.WriteLine($"Diagnostic: DriverStoreOpen {_hDrvStore}");
        }

        public void SetupConfigOptions(uint configOptions)
        {
            DevPropKey devPropKey = new()
            {
                fmtid = new Guid("8163eb00-142c-4f7a-94e1-a274cc47dbba"),
                pid = 16U
            };

            Console.WriteLine($"Diagnostic: Setting DriverStore ConfigOptions to 0x{configOptions:X}");

            bool flag;
            try
            {
                flag = DriverStoreInterop.DriverStoreSetObjectProperty(_hDrvStore, DriverStoreObjectType.DriverDatabase, "SYSTEM", ref devPropKey, DevPropType.DevPropTypeUint32, ref configOptions, 4, DriverStoreSetObjectPropertyFlag.None);
            }
            catch
            {
                throw new Exception("DriverStoreSetObjectProperty failed");
            }

            if (!flag)
            {
                int lastWin32Error = Marshal.GetLastWin32Error();
                Console.WriteLine($"Error: DriverStoreSetObjectProperty failed error 0x{lastWin32Error:X8}");
                throw new Win32Exception(lastWin32Error);
            }
        }

        public void ImportDriver(string infPath, string[] referencePaths, string[] stagingSubdirs, ProcessorArchitecture processArchitecture)
        {
            Console.WriteLine($"Diagnostic: Importing driver {infPath} into store {_stagingRootDirectory}");

            if (_hDrvStore == nint.Zero)
            {
                throw new InvalidOperationException("The driver store has not been created");
            }

            if (string.IsNullOrEmpty(infPath))
            {
                throw new ArgumentNullException(nameof(infPath));
            }

            string importPath = Path.Combine(_stagingRootDirectory, "import");
            _ = Directory.CreateDirectory(importPath);
            string driverPackageFileName = CopyToDirectory(infPath, importPath);

            if (referencePaths != null)
            {
                for (int i = 0; i < referencePaths.Length; i++)
                {
                    string stagingDirectory = string.IsNullOrEmpty(stagingSubdirs[i]) ? importPath : Path.Combine(importPath, stagingSubdirs[i]);
                    _ = Directory.CreateDirectory(stagingDirectory);
                    _ = CopyToDirectory(referencePaths[i], stagingDirectory);
                }
            }

            StringBuilder driverStoreFileName = new(260);
            DriverStoreImportFlag driverStoreImportFlag = DriverStoreImportFlag.SkipTempCopy | DriverStoreImportFlag.SkipExternalFileCheck | DriverStoreImportFlag.Inbox | DriverStoreImportFlag.SystemDefaultLocale;

            uint result = DriverStoreInterop.DriverStoreImport(_hDrvStore, driverPackageFileName, processArchitecture, null, driverStoreImportFlag, driverStoreFileName, driverStoreFileName.Capacity);
            if (result != 0U)
            {
                Console.WriteLine($"Error: DriverStoreImport failed error 0x{result:X8}");
                throw new Win32Exception((int)result);
            }

            Console.WriteLine($"Diagnostic: Driverstore INF path: {driverStoreFileName}");
            Console.WriteLine("Diagnostic: Publishing driver");
            StringBuilder publishedFileName = new(260);
            bool isPublishedFileNameChanged = false;

            result = DriverStoreInterop.DriverStorePublish(_hDrvStore, driverStoreFileName.ToString(), DriverStorePublishFlag.None, publishedFileName, publishedFileName.Capacity, ref isPublishedFileNameChanged);
            if (result != 0U)
            {
                Console.WriteLine($"Error: DriverStorePublish failed error 0x{result:X8}");
                throw new Win32Exception((int)result);
            }

            Console.WriteLine($"Diagnostic: Published INF path: {publishedFileName}");
            DriverStoreReflectCriticalFlag driverStoreReflectCriticalFlag = DriverStoreReflectCriticalFlag.Force | DriverStoreReflectCriticalFlag.Configurations;

            result = DriverStoreInterop.DriverStoreReflectCritical(_hDrvStore, driverStoreFileName.ToString(), driverStoreReflectCriticalFlag, null);
            if (result != 0U)
            {
                Console.WriteLine($"Error: DriverStoreReflectCritical failed error 0x{result:X8}");
                throw new Win32Exception((int)result);
            }
        }

        public void CopyDriver(string infPath, ProcessorArchitecture processArchitecture, string destination)
        {
            Console.WriteLine($"Diagnostic: Copying driver {infPath} to {destination}");

            uint result = DriverStoreInterop.DriverStoreCopy(_hDrvStore, infPath, processArchitecture, nint.Zero, DriverStoreCopyFlag.None, destination);
            if (result != 0U)
            {
                Console.WriteLine($"Error: DriverStoreCopy failed error 0x{result:X8}");
                throw new Win32Exception((int)result);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Close()
        {
            if (!(_hDrvStore != nint.Zero))
            {
                return;
            }
            Console.WriteLine($"Diagnostic: DriverStoreClose {_hDrvStore}");
            if (DriverStoreInterop.DriverStoreClose(_hDrvStore))
            {
                _hDrvStore = nint.Zero;
                return;
            }
            throw new Exception("Unable to close driver store");
        }

        private static string CopyToDirectory(string filePath, string destinationDirectory)
        {
            string expandedFilePath = Environment.ExpandEnvironmentVariables(filePath);
            Console.WriteLine($"Diagnostic: Copying {expandedFilePath} to {destinationDirectory}");
            if (!File.Exists(expandedFilePath))
            {
                throw new Exception($"Can't find required file: {expandedFilePath}");
            }
            string destinationFilePath = Path.Combine(destinationDirectory, Path.GetFileName(expandedFilePath));
            File.Copy(expandedFilePath, destinationFilePath, true);
            File.SetAttributes(destinationFilePath, FileAttributes.Normal);
            return destinationFilePath;
        }

        private void Dispose(bool disposing)
        {
            object obj = syncRoot;
            lock (obj)
            {
                if (!disposed)
                {
                    Close();
                    disposed = true;
                }
            }
        }
    }
}
