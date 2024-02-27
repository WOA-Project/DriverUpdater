using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using static DriverStore.DrvStore;

namespace DriverStore
{
    public class DrvPackage : IDisposable
    {
        private static readonly object syncRoot = new();
        private bool disposed;
        private nint _hDrvPackage = nint.Zero;
        private static List<string>? _fileNameSourceList;
        private static List<string>? _fileNameDestinationList;

        internal DrvPackage(nint driverPackage)
        {
            _hDrvPackage = driverPackage;
        }

        public DrvPackage(string infFile, ProcessorArchitecture processArchitecture, DriverPackageOpenFlag flags)
        {
            nint intPtr = DriverStoreInterop.DriverPackageOpen(infFile, processArchitecture, null, flags, nint.Zero);
            if (intPtr == nint.Zero)
            {
                int lastWin32Error = Marshal.GetLastWin32Error();
                throw new Exception($"{Path.GetFileName(infFile)} is not a valid input INF, Last error = {lastWin32Error:X}");
            }

            _hDrvPackage = intPtr;
        }

        ~DrvPackage()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Close()
        {
            if (!(_hDrvPackage != nint.Zero))
            {
                return;
            }
            Console.WriteLine($"Diagnostic: DriverPackageClose {_hDrvPackage}");
            DriverStoreInterop.DriverPackageClose(_hDrvPackage);
            _hDrvPackage = nint.Zero;
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

        public void DriverPackageGetVersionInfo(out DriverPackageVersionInfo versionInfo)
        {
            DriverPackageVersionInfo driverPackageVersionInfo = default;
            driverPackageVersionInfo.Size = (uint)Marshal.SizeOf(driverPackageVersionInfo);
            nint intPtr = Marshal.AllocHGlobal(Marshal.SizeOf(driverPackageVersionInfo));
            Marshal.StructureToPtr(driverPackageVersionInfo, intPtr, false);

            if (!DriverStoreInterop.DriverPackageGetVersionInfo(_hDrvPackage, intPtr))
            {
                int lastWin32Error = Marshal.GetLastWin32Error();
                Marshal.FreeHGlobal(intPtr);
                Console.WriteLine($"Error: DriverPackageGetVersionInfo failed error 0x{lastWin32Error:X8}");
                throw new Win32Exception(lastWin32Error);
            }

            versionInfo = (DriverPackageVersionInfo)Marshal.PtrToStructure(intPtr, typeof(DriverPackageVersionInfo));
            Marshal.FreeHGlobal(intPtr);
        }

        public void DriverPackageGetProperty(nint enumContext, string sectionName, ref DevPropKey propertyKey, out string propertyBuffer, DriverPackageGetPropertyFlag flags)
        {
            nint propertyKeyHandle = nint.Zero;
            nint propertyTypeHandle = nint.Zero;
            nint propertySizeHandle = nint.Zero;
            nint propertyBufferHandle = nint.Zero;
            try
            {
                string? cleanedSectionName = sectionName == string.Empty ? null : sectionName;
                propertyKeyHandle = Marshal.AllocHGlobal(Marshal.SizeOf(propertyKey));
                Marshal.StructureToPtr(propertyKey, propertyKeyHandle, false);
                uint bufferSize = 0U;
                propertySizeHandle = Marshal.AllocHGlobal(Marshal.SizeOf(bufferSize));
                if (!DriverStoreInterop.DriverPackageGetProperty(_hDrvPackage, enumContext, cleanedSectionName, propertyKeyHandle, nint.Zero, nint.Zero, 0U, propertySizeHandle, flags))
                {
                    int lastWin32Error = Marshal.GetLastWin32Error();
                    if (lastWin32Error == 1168)
                    {
                        propertyBuffer = "";
                        return;
                    }
                    if (lastWin32Error != 122)
                    {
                        Console.WriteLine("Error: DriverPackageGetProperty failed");
                        throw new Win32Exception(lastWin32Error);
                    }
                }
                bufferSize = (uint)Marshal.ReadInt32(propertySizeHandle);
                propertyBufferHandle = Marshal.AllocHGlobal((int)bufferSize);
                propertyTypeHandle = Marshal.AllocHGlobal(Marshal.SizeOf(new DriverStoreInterop.DevPropTypeInternal
                {
                    PropType = DevPropType.DevPropTypeString
                }));
                if (!DriverStoreInterop.DriverPackageGetProperty(_hDrvPackage, enumContext, cleanedSectionName, propertyKeyHandle, propertyTypeHandle, propertyBufferHandle, bufferSize, propertySizeHandle, flags))
                {
                    int lastWin32Error2 = Marshal.GetLastWin32Error();
                    Console.WriteLine("Error: DriverPackageGetProperty failed");
                    throw new Win32Exception(lastWin32Error2);
                }
                char[] array = new char[(bufferSize / 2U) - 1U];
                Marshal.Copy(propertyBufferHandle, array, 0, (int)((bufferSize / 2U) - 1U));
                propertyBuffer = new string(array);
            }
            finally
            {
                Marshal.FreeHGlobal(propertyKeyHandle);
                Marshal.FreeHGlobal(propertyTypeHandle);
                Marshal.FreeHGlobal(propertySizeHandle);
                Marshal.FreeHGlobal(propertyBufferHandle);
            }
        }

        public void DriverPackageGetProperty(nint enumContext, string sectionName, ref DevPropKey propertyKey, out ulong propertyBuffer, DriverPackageGetPropertyFlag flags)
        {
            nint propertyKeyHandle = nint.Zero;
            nint propertyTypeHandle = nint.Zero;
            nint propertySizeHandle = nint.Zero;
            nint propertyBufferHandle = nint.Zero;
            try
            {
                string? cleanedSectionName = sectionName == string.Empty ? null : sectionName;
                propertyKeyHandle = Marshal.AllocHGlobal(Marshal.SizeOf(propertyKey));
                Marshal.StructureToPtr(propertyKey, propertyKeyHandle, false);
                uint bufferSize = 0U;
                propertySizeHandle = Marshal.AllocHGlobal(Marshal.SizeOf(bufferSize));
                if (!DriverStoreInterop.DriverPackageGetProperty(_hDrvPackage, enumContext, cleanedSectionName, propertyKeyHandle, nint.Zero, nint.Zero, 0U, propertySizeHandle, flags))
                {
                    int lastWin32Error = Marshal.GetLastWin32Error();
                    if (lastWin32Error == 1168)
                    {
                        propertyBuffer = 0UL;
                        return;
                    }
                    if (lastWin32Error != 122)
                    {
                        Console.WriteLine("Error: DriverPackageGetProperty failed");
                        throw new Win32Exception(lastWin32Error);
                    }
                }
                bufferSize = (uint)Marshal.ReadInt32(propertySizeHandle);
                propertyBufferHandle = Marshal.AllocHGlobal((int)bufferSize);
                propertyTypeHandle = Marshal.AllocHGlobal(Marshal.SizeOf(new DriverStoreInterop.DevPropTypeInternal
                {
                    PropType = DevPropType.DevPropTypeUint64
                }));
                if (!DriverStoreInterop.DriverPackageGetProperty(_hDrvPackage, enumContext, cleanedSectionName, propertyKeyHandle, propertyTypeHandle, propertyBufferHandle, bufferSize, propertySizeHandle, flags))
                {
                    int lastWin32Error2 = Marshal.GetLastWin32Error();
                    Console.WriteLine("Error: DriverPackageGetProperty failed");
                    throw new Win32Exception(lastWin32Error2);
                }
                byte[] array = new byte[bufferSize];
                Marshal.Copy(propertyBufferHandle, array, 0, (int)bufferSize);
                propertyBuffer = BitConverter.ToUInt64(array, 0);
            }
            finally
            {
                Marshal.FreeHGlobal(propertyKeyHandle);
                Marshal.FreeHGlobal(propertyTypeHandle);
                Marshal.FreeHGlobal(propertySizeHandle);
                Marshal.FreeHGlobal(propertyBufferHandle);
            }
        }

        public static bool DriverIncludesInfs(string infFile, ProcessorArchitecture processArchitecture)
        {
            int result = 0;
            nint IParam = Marshal.AllocHGlobal(4);
            using DrvPackage driverPackageHandle = new(infFile, processArchitecture, DriverPackageOpenFlag.FilesOnly);
            Marshal.WriteInt32(IParam, result);

            _ = DriverStoreInterop.DriverPackageEnumFilesW(driverPackageHandle._hDrvPackage, nint.Zero, DriverPackageEnumFilesFlag.IncludeInfs, new DriverStoreInterop.EnumFilesDelegate(IncludeFileCallback), IParam);

            result = Marshal.ReadInt32(IParam);

            Marshal.FreeHGlobal(IParam);

            Console.WriteLine($"Diagnostic: INF includes other INFs: {result}");

            return result != 0;
        }

        public static void DriverGetFiles(string infFile, ProcessorArchitecture processArchitecture, DriverPackageEnumFilesFlag flags, out List<string> fileNameSourceList, out List<string> fileNameDestinationList)
        {
            _fileNameSourceList = [];
            _fileNameDestinationList = [];
            using DrvPackage driverPackageHandle = new(infFile, processArchitecture, DriverPackageOpenFlag.PrimaryOnly);
            _ = DriverStoreInterop.DriverPackageEnumFilesW(driverPackageHandle._hDrvPackage, nint.Zero, flags, new DriverStoreInterop.EnumFilesDelegate(DriverFileCallback), nint.Zero);
            fileNameSourceList = _fileNameSourceList;
            fileNameDestinationList = _fileNameDestinationList;
        }

        private static bool IncludeFileCallback(nint driverPackageHandle, nint pDriverFile, nint lParam)
        {
            Marshal.WriteInt32(lParam, 1);
            return false;
        }

        private static bool DriverFileCallback(nint driverPackageHandle, nint pDriverFile, nint lParam)
        {
            DriverFile driverFile = (DriverFile)Marshal.PtrToStructure(pDriverFile, typeof(DriverFile));
            string driverFileSourcePath = driverFile.SourcePath;
            string fileNameSource;
            if (!string.IsNullOrEmpty(driverFileSourcePath))
            {
                if (driverFileSourcePath[0] == '.' && driverFileSourcePath[1] == '\\')
                {
                    driverFileSourcePath = driverFileSourcePath[2..];
                }
                else if (driverFileSourcePath[0] == '\\')
                {
                    driverFileSourcePath = driverFileSourcePath[1..];
                }
                fileNameSource = driverFileSourcePath;
                if (!string.IsNullOrEmpty(driverFile.SourceFile))
                {
                    fileNameSource = Path.Combine(fileNameSource, driverFile.SourceFile);
                }
            }
            else
            {
                fileNameSource = !string.IsNullOrEmpty(driverFile.SourceFile) ? driverFile.SourceFile : "";
            }
            string systemNeutralFileName = driverFile.DestinationPath;
            if (systemNeutralFileName.Equals("") && driverFile.Type == DriverFileType.Inf)
            {
                systemNeutralFileName = ".\\";
            }
            string fileNameDestination;
            if (!string.IsNullOrEmpty(systemNeutralFileName))
            {
                systemNeutralFileName = GetSystemNeutralFilename(systemNeutralFileName);
                fileNameDestination = systemNeutralFileName;
                if (!string.IsNullOrEmpty(driverFile.DestinationFile))
                {
                    fileNameDestination = Path.Combine(fileNameDestination, driverFile.DestinationFile);
                }
            }
            else
            {
                fileNameDestination = !string.IsNullOrEmpty(driverFile.DestinationFile) ? driverFile.DestinationFile : "";
            }
            _fileNameSourceList.Add(fileNameSource);
            _fileNameDestinationList.Add(fileNameDestination);
            return true;
        }

        private static string GetSystemNeutralFilename(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            StringBuilder stringBuilder = new();
            if (DriverStoreInterop.GetSystemWindowsDirectoryW(stringBuilder, 260U) == 0U)
            {
                throw new Exception("Failed in GetSystemWindowsDirectory");
            }

            string text = stringBuilder.ToString();
            text = text.TrimEnd(['\\']);
            string text2 = Path.GetPathRoot(text).TrimEnd(['\\']);

            if (path.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            {
                path = $"%SystemRoot%{path[text.Length..]}";
            }
            else if (path.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
            {
                path = $"%SystemDrive%{path[text2.Length..]}";
            }

            return path;
        }

        public static void ValidateInf(string infFile, ProcessorArchitecture processArchitecture)
        {
            using DrvPackage intPtr = new(infFile, processArchitecture, DriverPackageOpenFlag.StrictValidation | DriverPackageOpenFlag.PrimaryOnly);
        }
    }
}