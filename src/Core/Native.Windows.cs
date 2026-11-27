// BureauCache — Native : fenêtres ouvertes pour la barre des tâches (D-013, T-033)
using System;
using System.Runtime.InteropServices;
using System.Text;

static partial class Native
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong32(IntPtr hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetClassLongW")] static extern int GetClassLong32(IntPtr hWnd, int index);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr hWnd, bool altTab);
    // Version postée de ShowWindow : une application figée ne bloque jamais la barre (CDX-03)
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern IntPtr CopyIcon(IntPtr hIcon);
    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)] public static extern bool PostMessageChecked(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] public static extern bool RegisterShellHookWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool DeregisterShellHookWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);

    public const uint GW_OWNER = 4;
    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const long WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000, WS_EX_NOACTIVATE = 0x08000000, WS_CHILD = 0x40000000;
    public const uint WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 2;
    public const uint EVENT_SYSTEM_FOREGROUND = 3, EVENT_SYSTEM_MINIMIZESTART = 0x16, EVENT_SYSTEM_MINIMIZEEND = 0x17;
    public const uint EVENT_OBJECT_CREATE = 0x8000, EVENT_OBJECT_DESTROY = 0x8001, EVENT_OBJECT_SHOW = 0x8002, EVENT_OBJECT_HIDE = 0x8003;
    public const uint EVENT_OBJECT_NAMECHANGE = 0x800C, EVENT_OBJECT_CLOAKED = 0x8017, EVENT_OBJECT_UNCLOAKED = 0x8018;
    public const int HSHELL_WINDOWACTIVATED = 4, HSHELL_RUDEAPPACTIVATED = 0x8004, HSHELL_FLASH = 0x8006;
    public const int WM_CLOSE = 0x10, WM_GETICON = 0x7F, ICON_BIG = 1, ICON_SMALL2 = 2;
    public const int SW_MINIMIZE = 6, SW_RESTORE = 9;

    public static long WindowStyle(IntPtr h, int index)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(h, index).ToInt64() : GetWindowLong32(h, index);
    }

    public static IntPtr ClassIcon(IntPtr h, int index)
    {
        return IntPtr.Size == 8 ? GetClassLongPtr64(h, index) : new IntPtr(GetClassLong32(h, index));
    }

    public static string WindowTitle(IntPtr h)
    {
        int n = GetWindowTextLength(h);
        if (n <= 0) return "";
        StringBuilder sb = new StringBuilder(Math.Min(n, 1024) + 1);
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string WindowClass(IntPtr h)
    {
        StringBuilder sb = new StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    // Fenêtre masquée par DWM : autre bureau virtuel, application du Store suspendue ou pas encore affichée
    public static bool IsCloaked(IntPtr h)
    {
        int v;
        try { return DwmGetWindowAttribute(h, 14 /*DWMWA_CLOAKED*/, out v, 4) == 0 && v != 0; }
        catch { return false; }
    }

    // Chemin de l'exe d'une fenêtre, avec le droit le plus réduit (pas d'accès à la mémoire du processus)
    public static string ProcessPath(IntPtr h)
    {
        uint pid;
        GetWindowThreadProcessId(h, out pid);
        if (pid == 0) return null;
        IntPtr p = OpenProcess(0x1000 /*PROCESS_QUERY_LIMITED_INFORMATION*/, false, pid);
        if (p == IntPtr.Zero) return null;
        try
        {
            StringBuilder sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(p, 0, sb, ref size) ? sb.ToString(0, size) : null;
        }
        finally { CloseHandle(p); }
    }

    // Icône que la fenêtre déclare, sans jamais attendre plus de 100 ms une application qui ne répond pas
    public static IntPtr WindowIcon(IntPtr h, bool big)
    {
        IntPtr r;
        if (SendMessageTimeout(h, WM_GETICON, new IntPtr(big ? ICON_BIG : ICON_SMALL2), IntPtr.Zero, 0x2 /*SMTO_ABORTIFHUNG*/, 100, out r) != IntPtr.Zero && r != IntPtr.Zero)
            return r;
        r = ClassIcon(h, big ? -14 /*GCLP_HICON*/ : -34 /*GCLP_HICONSM*/);
        if (r == IntPtr.Zero) r = ClassIcon(h, big ? -34 : -14);
        return r;
    }
}
