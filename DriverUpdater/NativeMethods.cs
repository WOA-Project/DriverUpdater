/*

Copyright (c) 2017-2021, The LumiaWOA Authors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DriverUpdater
{
    internal static class NativeMethods
    {
        internal enum ProcessorArchitecture : ushort
        {
            PROCESSOR_ARCHITECTURE_INTEL = 0,
            PROCESSOR_ARCHITECTURE_MIPS = 1,
            PROCESSOR_ARCHITECTURE_ALPHA = 2,
            PROCESSOR_ARCHITECTURE_PPC = 3,
            PROCESSOR_ARCHITECTURE_SHX = 4,
            PROCESSOR_ARCHITECTURE_ARM = 5,
            PROCESSOR_ARCHITECTURE_IA64 = 6,
            PROCESSOR_ARCHITECTURE_ALPHA64 = 7,
            PROCESSOR_ARCHITECTURE_MSIL = 8,
            PROCESSOR_ARCHITECTURE_AMD64 = 9,
            PROCESSOR_ARCHITECTURE_IA32_ON_WIN64 = 10,
            PROCESSOR_ARCHITECTURE_NEUTRAL = 11,
            PROCESSOR_ARCHITECTURE_ARM64 = 12,
            PROCESSOR_ARCHITECTURE_ARM32_ON_WIN64 = 13,
            PROCESSOR_ARCHITECTURE_IA32_ON_ARM64 = 14,
        }

        [DllImport("drvstore.dll", EntryPoint = "DriverStoreOpenW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr DriverStoreOpenW(
            string TargetSystemRoot,
            string TargetSystemDrive,
            uint Flags,
            IntPtr Reserved);

        [DllImport("drvstore.dll", EntryPoint = "DriverStoreOpenW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool DriverStoreClose(IntPtr hDriverStore);

        [DllImport("drvstore.dll", EntryPoint = "DriverStoreUnreflectCriticalW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int DriverStoreUnreflectCriticalW(
            IntPtr hDriverStore,
            string DriverStoreFileName,
            uint Flags,
            string FilterDeviceIds);

        [DllImport("drvstore.dll", EntryPoint = "DriverStoreDeleteW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int DriverStoreDeleteW(
            IntPtr hDriverStore,
            string DriverStoreFileName,
            uint Flags);

        [DllImport("drvstore.dll", EntryPoint = "DriverStoreImportW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int DriverStoreImportW(
            IntPtr hDriverStore,
            string DriverPackageFileName,
            ProcessorArchitecture ProcessorArchitecture,
            string LocaleName,
            uint Flags,
            StringBuilder DestInfPath,
            ref int cchDestInfPath);

        [DllImport("drvstore.dll", EntryPoint = "DriverStoreReflectCriticalW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int DriverStoreReflectCriticalW(
            IntPtr hDriverStore,
            string DriverStoreFileName,
            uint Flags,
            string FilterDeviceIds);

        [DllImport("drvstore.dll", EntryPoint = "DriverStoreOfflineAddDriverPackageW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int DriverStoreOfflineAddDriverPackageW(
            string DriverPackageInfPath,
            uint Flags,
            IntPtr Reserved,
            ProcessorArchitecture ProcessorArchitecture,
            string LocaleName,
            StringBuilder DestInfPath,
            ref int cchDestInfPath,
            string TargetSystemRoot,
            string TargetSystemDrive);

        [DllImport("drvstore.dll", EntryPoint = "DriverStoreOfflineDeleteDriverPackageW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int DriverStoreOfflineDeleteDriverPackageW(
            string DriverPackageInfPath,
            uint Flags,
            IntPtr Reserved,
            string TargetSystemRoot,
            string TargetSystemDrive);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Size = 0x2B8, Pack = 0x4)]
        public struct DriverStoreOfflineEnumDriverPackageInfoW
        {
            public int InboxInf;

            public ProcessorArchitecture ProcessorArchitecture;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 85)]
            public string LocaleName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string PublishedInfName;
        };

        public delegate int CallbackRoutine(
            [MarshalAs(UnmanagedType.LPWStr)]
            string DriverPackageInfPath,
            IntPtr DriverStoreOfflineEnumDriverPackageInfoW,
            IntPtr Unknown);

        [DllImport("drvstore.dll", EntryPoint = "DriverStoreOfflineEnumDriverPackageW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int DriverStoreOfflineEnumDriverPackageW(CallbackRoutine CallbackRoutine, IntPtr lParam, string TargetSystemRoot);
    }
}
