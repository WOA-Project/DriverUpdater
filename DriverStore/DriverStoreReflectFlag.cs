namespace DriverStore
{
    [Flags]
    internal enum DriverStoreReflectFlag : uint
    {
        None = 0U,
        FilesOnly = 1U,
        ActiveDrivers = 2U,
        ExternalOnly = 4U,
        Configurations = 8U
    }
}
