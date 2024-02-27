namespace DriverStore
{
    [Flags]
    internal enum DriverStoreConfigureFlags : uint
    {
        None = 0U,
        Force = 1U,
        ActiveOnly = 2U,
        SourceConfigurations = 65536U,
        SourceDeviceIds = 131072U,
        TargetDeviceNodes = 1048576U
    }
}
