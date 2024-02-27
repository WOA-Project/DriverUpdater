namespace DriverStore
{
    [Flags]
    internal enum DriverStoreImportFlag : uint
    {
        None = 0U,
        SkipTempCopy = 1U,
        SkipExternalFileCheck = 2U,
        NoRestorePoint = 4U,
        NonInteractive = 8U,
        Replace = 32U,
        Hardlink = 64U,
        PublishSameName = 256U,
        Inbox = 512U,
        F6 = 1024U,
        BaseVersion = 2048U,
        SystemDefaultLocale = 4096U,
        SystemCritical = 8192U
    }
}
