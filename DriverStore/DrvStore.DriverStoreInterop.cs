using System.Runtime.InteropServices;
using System.Text;

namespace DriverStore
{
    public partial class DrvStore
    {
        internal static class DriverStoreInterop
        {
            [DllImport("kernel32", SetLastError = true)]
            internal static extern nint LoadLibrary(string lpFileName);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool FreeLibrary(nint hModule);

            [DllImport("kernel32.dll")]
            internal static extern uint GetSystemWindowsDirectoryW([MarshalAs(UnmanagedType.LPWStr)][Out] StringBuilder lpBuffer, uint uSize);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, EntryPoint = "DriverStoreOpenW", SetLastError = true)]
            internal static extern nint DriverStoreOpen(string targetSystemPath, string targetBootDrive, DriverStoreOpenFlag Flags, nint transactionHandle);

            [DllImport("drvstore.dll", SetLastError = true)]
            internal static extern bool DriverStoreClose(nint driverStoreHandle);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, EntryPoint = "DriverStoreImportW", SetLastError = true)]
            internal static extern uint DriverStoreImport(nint driverStoreHandle, string driverPackageFileName, ProcessorArchitecture ProcessorArchitecture, string? localeName, DriverStoreImportFlag flags, StringBuilder driverStoreFileName, int driverStoreFileNameSize);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, EntryPoint = "DriverStoreOfflineAddDriverPackageW", SetLastError = true)]
            internal static extern uint DriverStoreOfflineAddDriverPackage(string DriverPackageInfPath, DriverStoreOfflineAddDriverPackageFlags Flags, nint Reserved, ushort ProcessorArchitecture, string LocaleName, StringBuilder DestInfPath, ref int cchDestInfPath, string TargetSystemRoot, string TargetSystemDrive);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, EntryPoint = "DriverStoreConfigureW", SetLastError = true)]
            internal static extern uint DriverStoreConfigure(nint hDriverStore, string DriverStoreFilename, DriverStoreConfigureFlags Flags, string SourceFilter, string TargetFilter);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, EntryPoint = "DriverStoreReflectCriticalW", SetLastError = true)]
            internal static extern uint DriverStoreReflectCritical(nint driverStoreHandle, string driverStoreFileName, DriverStoreReflectCriticalFlag flag, string? filterDeviceId);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, EntryPoint = "DriverStoreReflectW", SetLastError = true)]
            internal static extern uint DriverStoreReflect(nint driverStoreHandle, string driverStoreFileName, DriverStoreReflectFlag flag, string filterSectionNames);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, EntryPoint = "DriverStorePublishW", SetLastError = true)]
            internal static extern uint DriverStorePublish(nint driverStoreHandle, string driverStoreFileName, DriverStorePublishFlag flag, StringBuilder publishedFileName, int publishedFileNameSize, ref bool isPublishedFileNameChanged);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, EntryPoint = "DriverStoreSetObjectPropertyW", SetLastError = true)]
            internal static extern bool DriverStoreSetObjectProperty(nint driverStoreHandle, DriverStoreObjectType objectType, string objectName, ref DevPropKey propertyKey, DevPropType propertyType, ref uint propertyBuffer, int propertySize, DriverStoreSetObjectPropertyFlag flag);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern bool DriverPackageEnumFilesW(nint driverPackageHandle, nint enumContext, DriverPackageEnumFilesFlag flags, EnumFilesDelegate callbackRoutine, nint lParam);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, EntryPoint = "DriverPackageOpenW", SetLastError = true)]
            internal static extern nint DriverPackageOpen(string driverPackageFilename, ProcessorArchitecture processorArchitecture, string? localeName, DriverPackageOpenFlag flags, nint resolveContext);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, EntryPoint = "DriverPackageGetVersionInfoW", SetLastError = true)]
            internal static extern bool DriverPackageGetVersionInfo(nint driverPackageHandle, nint pVersionInfo);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, EntryPoint = "DriverPackageGetPropertyW", SetLastError = true)]
            internal static extern bool DriverPackageGetProperty(nint driverPackageHandle, nint enumContext, string sectionName, nint propertyKey, nint propertyType, nint propertyBuffer, uint bufferSize, nint propertySize, DriverPackageGetPropertyFlag flags);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern void DriverPackageClose(nint driverPackageHandle);

            [DllImport("drvstore.dll", CharSet = CharSet.Unicode, EntryPoint = "DriverStoreCopyW", SetLastError = true)]
            internal static extern uint DriverStoreCopy(nint driverPackageHandle, string driverPackageFilename, ProcessorArchitecture processorArchitecture, nint localeName, DriverStoreCopyFlag flags, string destinationPath);

            public delegate bool EnumFilesDelegate(nint driverPackageHandle, nint pDriverFile, nint lParam);

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct DevPropTypeInternal
            {
                internal DevPropType PropType;
            }
        }
    }
}
