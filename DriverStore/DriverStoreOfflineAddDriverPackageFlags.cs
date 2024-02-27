namespace DriverStore
{
    [Flags]
    internal enum DriverStoreOfflineAddDriverPackageFlags : uint
    {
        None = 0U,
        SkipInstall = 1U,
        Inbox = 2U,
        F6 = 4U,
        SkipExternalFilePresenceCheck = 8U,
        NoTempCopy = 16U,
        UseHardLinks = 32U,
        InstallOnly = 64U,
        ReplacePackage = 128U,
        Force = 256U,
        BaseVersion = 512U
    }
}
