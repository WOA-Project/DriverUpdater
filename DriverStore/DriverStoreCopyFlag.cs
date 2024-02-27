namespace DriverStore
{
    [Flags]
    internal enum DriverStoreCopyFlag : uint
    {
        None = 0U,
        External = 1U,
        CopyInfs = 2U,
        SkipExistingCopyInfs = 4U,
        SystemDefaultLocale = 8U,
        Hardlink = 16U
    }
}
