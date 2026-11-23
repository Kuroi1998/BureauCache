// BureauCache — Native
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// =====================================================================
//  Win32
// =====================================================================
static class Native
{
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string cls, string name);
    [DllImport("user32.dll")] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string name);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr hWnd);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindFirstFile(string path, out WIN32_FIND_DATA data);
    [DllImport("kernel32.dll")] static extern bool FindClose(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern short GetKeyState(int vk);
    [DllImport("user32.dll")] public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO info);
    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);
    [DllImport("user32.dll")] public static extern bool LockWorkStation();
    [DllImport("user32.dll")] public static extern bool ExitWindowsEx(uint flags, uint reason);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowTheme(IntPtr hWnd, string app, string idList);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
    [DllImport("shell32.dll")] public static extern int SHGetImageList(int list, ref Guid riid, out IntPtr ppv);
    [DllImport("comctl32.dll")] public static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, int flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SHEmptyRecycleBin(IntPtr hwnd, string root, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SHQueryRecycleBin(string root, ref SHQUERYRBINFO info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItemImageFactory ppv);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WIN32_FIND_DATA
    {
        public uint Attributes;
        // FILETIME = deux DWORD alignés sur 4 octets (un long décalerait les champs suivants)
        public uint CreatedLow, CreatedHigh, AccessedLow, AccessedHigh, WrittenLow, WrittenHigh;
        public uint SizeHigh, SizeLow, ReparseTag, Reserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string ShortName;
    }

    const uint TagMountPoint = 0xA0000003, TagSymlink = 0xA000000C;

    // Vrai pour une jonction ou un lien symbolique. Les fichiers OneDrive « à la demande » sont aussi des points
    // d'analyse, mais avec une autre étiquette : ce ne sont pas des liens. Illisible : vrai, par prudence.
    public static bool IsLink(string path)
    {
        WIN32_FIND_DATA d;
        IntPtr h = FindFirstFile(path.TrimEnd('\\'), out d);
        if (h == new IntPtr(-1)) return true;
        FindClose(h);
        if ((d.Attributes & 0x400) == 0) return false;
        return d.ReparseTag == TagMountPoint || d.ReparseTag == TagSymlink;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }
    [DllImport("shell32.dll")] public static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);
    [StructLayout(LayoutKind.Sequential)] public struct ICONINFO { public bool fIcon; public int x, y; public IntPtr hbmMask, hbmColor; }
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential)] public struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItemImageFactory { [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm); }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO sfi, uint size, uint flags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHSTOCKICONINFO
    {
        public uint cbSize; public IntPtr hIcon; public int iSysIconIndex; public int iIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szPath;
    }
    [DllImport("shell32.dll")] public static extern int SHGetStockIconInfo(uint siid, uint flags, ref SHSTOCKICONINFO info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHELLEXECUTEINFO
    {
        public int cbSize; public uint fMask; public IntPtr hwnd;
        public string lpVerb, lpFile, lpParameters, lpDirectory;
        public int nShow; public IntPtr hInstApp, lpIDList; public string lpClass;
        public IntPtr hkeyClass; public uint dwHotKey; public IntPtr hIcon, hProcess;
    }

    [DllImport("kernel32.dll")] public static extern bool SetDefaultDllDirectories(uint flags);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    public const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x800;
    public const int SW_HIDE = 0, SW_SHOW = 5;

    public static IntPtr FindDesktopListView()
    {
        IntPtr progman = FindWindow("Progman", null);
        IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView == IntPtr.Zero)
        {
            IntPtr w = IntPtr.Zero;
            while ((w = FindWindowEx(IntPtr.Zero, w, "WorkerW", null)) != IntPtr.Zero)
            {
                defView = FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero) break;
            }
        }
        if (defView == IntPtr.Zero) return IntPtr.Zero;
        return FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
    }

    public static void RestoreDesktopIcons()
    {
        IntPtr lv = FindDesktopListView();
        if (lv != IntPtr.Zero) ShowWindow(lv, SW_SHOW);
    }

    public static bool ForegroundIsFullscreen()
    {
        IntPtr h = GetForegroundWindow();
        StringBuilder sb = new StringBuilder(64);
        if (h != IntPtr.Zero) GetClassName(h, sb, 64);
        int st;
        if (SHQueryUserNotificationState(out st) != 0) st = 0;
        RECT r;
        Rectangle window = h != IntPtr.Zero && GetWindowRect(h, out r) ? Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom) : Rectangle.Empty;
        Rectangle screen = h != IntPtr.Zero ? Screen.FromHandle(h).Bounds : Rectangle.Empty;
        return IsFullscreen(h != IntPtr.Zero, sb.ToString(), h != IntPtr.Zero && h == GetShellWindow(), st, window, screen);
    }

    // Décision séparée des appels Windows, pour être testée. Le bureau et la barre des tâches ne sont jamais du plein
    // écran : ils sont écartés AVANT l'état de notification, car Windows répond « occupé » (2) quand le bureau est au
    // premier plan, ce qui empêchait le panneau de s'ouvrir depuis le bureau (option « Pas en plein écran »).
    internal static bool IsFullscreen(bool hasWindow, string cls, bool isShellWindow, int notificationState, Rectangle window, Rectangle screen)
    {
        if (!hasWindow || isShellWindow) return false;
        if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return false;
        if (notificationState == 2 || notificationState == 3 || notificationState == 4) return true;   // jeu, Direct3D, présentation
        if (window.IsEmpty || screen.IsEmpty) return false;
        return window.Left <= screen.Left && window.Top <= screen.Top && window.Right >= screen.Right && window.Bottom >= screen.Bottom;
    }

    public static void SetDarkTitleBar(IntPtr hwnd, bool dark)
    {
        int v = dark ? 1 : 0;
        try { DwmSetWindowAttribute(hwnd, 20, ref v, 4); } catch { }
    }

    public static void SetRoundCorners(IntPtr hwnd)
    {
        int v = 2;
        try { DwmSetWindowAttribute(hwnd, 33, ref v, 4); } catch { }
    }

    public static void SetDarkScrollbars(IntPtr hwnd, bool dark)
    {
        try { SetWindowTheme(hwnd, dark ? "DarkMode_Explorer" : "Explorer", null); } catch { }
    }

    // Vide la corbeille de tous les disques sans fenêtre ni son. Une corbeille déjà vide renvoie E_UNEXPECTED : c'est un succès.
    public static bool EmptyRecycleBinQuietly()
    {
        try
        {
            int hr = SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4);
            return hr == 0 || hr == unchecked((int)0x8000FFFF);
        }
        catch { return false; }
    }

    public static void RecycleBinInfo(out long count, out long size)
    {
        count = size = 0;
        try
        {
            SHQUERYRBINFO i = new SHQUERYRBINFO();
            i.cbSize = Marshal.SizeOf(i);
            if (SHQueryRecycleBin(null, ref i) == 0) { count = i.i64NumItems; size = i.i64Size; }
        }
        catch { }
    }
}
