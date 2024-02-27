namespace DriverStore
{
    [Flags]
    public enum DriverPackageOpenFlag : uint
    {
        VersionOnly = 1U,
        FilesOnly = 2U,
        DefaultLanguage = 4U,
        LocalizableStrings = 8U,
        TargetOSVersion = 16U,
        StrictValidation = 32U,
        ClassSchemaOnly = 64U,
        LogTelemetry = 128U,
        PrimaryOnly = 256U
    }
}
