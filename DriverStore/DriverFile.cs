using System.Runtime.InteropServices;

namespace DriverStore
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DriverFile
    {
        internal DriverFileOperation Operation;

        internal string ExternalFile;

        internal DriverFileType Type;

        internal uint Flags;

        internal string SourceFile;

        internal string SourcePath;

        internal string DestinationFile;

        internal string DestinationPath;

        internal string ArchiveFile;

        internal string SecurityDescriptor;

        internal string SectionName;
    }
}
