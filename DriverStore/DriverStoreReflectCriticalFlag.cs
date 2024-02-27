namespace DriverStore
{
    [Flags]
    internal enum DriverStoreReflectCriticalFlag : uint
    {
        None = 0U,
        Force = 1U,
        Configurations = 2U
    }
}
