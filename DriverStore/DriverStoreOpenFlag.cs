namespace DriverStore
{
    [Flags]
    internal enum DriverStoreOpenFlag : uint
    {
        None = 0U,
        Create = 1U,
        Exclusive = 2U
    }
}
