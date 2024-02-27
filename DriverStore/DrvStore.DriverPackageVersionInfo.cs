using System.Runtime.InteropServices;

namespace DriverStore
{
    public partial class DrvStore
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DriverPackageVersionInfo
        {
            public uint Size;

            public ProcessorArchitecture Architecture;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 85)]
            public string LocaleName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ProviderName;

            public System.Runtime.InteropServices.ComTypes.FILETIME DriverDate;

            public ulong DriverVersion;

            public Guid ClassGuid;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ClassName;

            public uint ClassVersion;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string CatalogFile;

            private readonly uint Flags;
        }
    }
}
