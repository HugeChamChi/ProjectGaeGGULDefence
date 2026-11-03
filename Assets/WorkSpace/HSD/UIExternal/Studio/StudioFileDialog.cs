using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace GGD.UI.Authoring.Studio
{
    /// <summary>Windows-native file selection in the portable Player; no UnityEditor or Python dependency.</summary>
    /// <remarks>
    /// Dialogs run on their own STA thread. A blocking native dialog inside OnGUI pumps the Unity window's
    /// messages, re-enters the player loop on an already deep stack and failed with a stack overflow.
    /// </remarks>
    public static class StudioFileDialog
    {
        private const int DialogStackBytes = 16 * 1024 * 1024, PathCapacity = 32768;
        private static volatile bool _busy;
        /// <summary>Whether a native dialog is currently open.</summary>
        public static bool Busy => _busy;
        /// <summary>Opens a regular file selection dialog; <paramref name="chosen"/> runs on the Unity thread with null on cancel.</summary>
        public static void Open(string filter, string title, Action<string> chosen, Action<Exception> failed) => Run(owner => Choose(owner, filter, title, false), chosen, failed);
        /// <summary>Selects a destination file with a native overwrite prompt.</summary>
        public static void Save(string filter, string title, Action<string> chosen, Action<Exception> failed) => Run(owner => Choose(owner, filter, title, true), chosen, failed);
        /// <summary>Selects a local art folder without requiring Unity Editor.</summary>
        public static void Folder(Action<string> chosen, Action<Exception> failed) => Run(ChooseFolder, chosen, failed);
        private static void Run(Func<IntPtr, string> dialog, Action<string> chosen, Action<Exception> failed)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_busy) throw new InvalidOperationException("이미 파일 선택 창이 열려 있습니다.");
            var context = SynchronizationContext.Current ?? throw new InvalidOperationException("파일 선택 창은 Unity 메인 스레드에서만 열 수 있습니다.");
            IntPtr owner = GetActiveWindow();
            _busy = true;
            var thread = new Thread(() =>
            {
                string path = null; Exception failure = null;
                try { path = dialog(owner); } catch (Exception error) { failure = error; }
                context.Post(_ =>
                {
                    _busy = false;
                    try { if (failure != null) failed(failure); else chosen(path); }
                    catch (Exception error) { failed(error); }
                }, null);
            }, DialogStackBytes) { IsBackground = true, Name = "Studio file dialog" };
            thread.SetApartmentState(ApartmentState.STA);
            try { thread.Start(); } catch { _busy = false; throw; }
#else
            throw new PlatformNotSupportedException("The authoring Studio currently targets Windows");
#endif
        }
        private static string ChooseFolder(IntPtr owner)
        {
            var info = new BrowseInfo { owner = owner, title = "GGD_ArtWork 폴더를 선택하세요", flags = 1 };
            IntPtr result = SHBrowseForFolder(ref info);
            if (result == IntPtr.Zero) return null;
            IntPtr buffer = Marshal.AllocHGlobal(PathCapacity * 2);
            try { return SHGetPathFromIDList(result, buffer) ? Marshal.PtrToStringUni(buffer) : null; }
            finally { Marshal.FreeHGlobal(buffer); Marshal.FreeCoTaskMem(result); }
        }
        private static string Choose(IntPtr owner, string filter, string title, bool save)
        {
            // An unmanaged buffer avoids marshalling a StringBuilder field inside a by-ref struct.
            IntPtr buffer = Marshal.AllocHGlobal(PathCapacity * 2);
            try
            {
                Marshal.WriteInt16(buffer, 0);
                var data = new OpenFileName { size = Marshal.SizeOf<OpenFileName>(), owner = owner, file = buffer, maxFile = PathCapacity,
                    filter = filter.Replace('|', '\0') + "\0\0", title = title, flags = 0x00080000 | 0x00000008 | (save ? 0x2 : 0x1000) };
                return (save ? GetSaveFileName(ref data) : GetOpenFileName(ref data)) ? Marshal.PtrToStringUni(buffer) : null;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct BrowseInfo
        { public IntPtr owner, root, displayName; public string title; public uint flags; public IntPtr callback, parameter; public int image; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct OpenFileName
        {
            public int size; public IntPtr owner, instance; public string filter, customFilter; public int maxCustomFilter, filterIndex;
            public IntPtr file; public int maxFile; public string fileTitle; public int maxFileTitle; public string initialDir, title;
            public int flags; public short fileOffset, extensionOffset; public string defaultExtension; public IntPtr customData, hook; public string template;
            public IntPtr reserved; public int reserved2, flagsEx;
        }
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SHBrowseForFolder(ref BrowseInfo info);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SHGetPathFromIDList(IntPtr item, IntPtr path);
        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetOpenFileName(ref OpenFileName data);
        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSaveFileName(ref OpenFileName data);
    }
}
