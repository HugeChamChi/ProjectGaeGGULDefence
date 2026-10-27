using System;
using System.Runtime.InteropServices;
using System.Text;

namespace GGD.UI.Authoring.Studio
{
    /// <summary>Windows-native file selection in the portable Player; no UnityEditor or Python dependency.</summary>
    public static class StudioFileDialog
    {
        /// <summary>Opens a regular file selection dialog.</summary>
        public static string Open(string filter, string title) => Choose(filter, title, false);
        /// <summary>Selects a destination file with a native overwrite prompt.</summary>
        public static string Save(string filter, string title) => Choose(filter, title, true);
        private static string Choose(string filter, string title, bool save)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var data = new OpenFileName { size = Marshal.SizeOf<OpenFileName>(), file = new StringBuilder(32768), maxFile = 32768,
                filter = filter.Replace('|', '\0') + "\0\0", title = title, flags = 0x00080000 | 0x00000008 | (save ? 0x2 : 0x1000) };
            return (save ? GetSaveFileName(ref data) : GetOpenFileName(ref data)) ? data.file.ToString() : null;
#else
            throw new PlatformNotSupportedException("The authoring Studio currently targets Windows");
#endif
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct OpenFileName
        {
            public int size; public IntPtr owner, instance; public string filter, customFilter; public int maxCustomFilter, filterIndex;
            public StringBuilder file; public int maxFile; public string fileTitle; public int maxFileTitle; public string initialDir, title;
            public int flags; public short fileOffset, extensionOffset; public string defaultExtension; public IntPtr customData, hook; public string template;
            public IntPtr reserved; public int reserved2, flagsEx;
        }
        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetOpenFileName(ref OpenFileName data);
        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSaveFileName(ref OpenFileName data);
    }
}
