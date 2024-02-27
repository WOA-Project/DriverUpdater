namespace DriverStore
{
    [Flags]
    public enum DriverPackageEnumFilesFlag : uint
    {
        Copy = 1U,
        Delete = 2U,
        Rename = 4U,
        Inf = 16U,
        Catalog = 32U,
        Binaries = 64U,
        CopyInfs = 128U,
        IncludeInfs = 256U,
        External = 4096U,
        UniqueSource = 8192U,
        UniqueDestination = 16384U
    }
}
