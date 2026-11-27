// BureauCache — Native : barre des tâches de Windows (D-013)
using System;
using System.Runtime.InteropServices;

static partial class Native
{
    public const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3, ABM_GETSTATE = 4, ABM_SETSTATE = 10;
    public const int ABS_AUTOHIDE = 1, ABS_ALWAYSONTOP = 2;
    public const int ABN_POSCHANGED = 1, ABN_FULLSCREENAPP = 2;
    public const int ABE_BOTTOM = 3;
    public const int SW_SHOWNA = 8;

    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    // Barre principale d'Explorer (écran principal). Les barres des autres écrans sont « Shell_SecondaryTrayWnd ».
    public static IntPtr WindowsTaskbar() { return FindWindow("Shell_TrayWnd", null); }

    public static bool WindowsTaskbarResponds()
    {
        IntPtr bar = WindowsTaskbar(), result;
        return bar != IntPtr.Zero && SendMessageTimeout(bar, 0 /* WM_NULL */, IntPtr.Zero, IntPtr.Zero,
            0x2 /* SMTO_ABORTIFHUNG */, 40, out result) != IntPtr.Zero;
    }

    // État de la barre de Windows : masquage automatique (1) ou non. Le message s'adresse à la barre d'Explorer.
    public static int GetTaskbarState()
    {
        APPBARDATA d = new APPBARDATA();
        d.cbSize = Marshal.SizeOf(d);
        d.hWnd = WindowsTaskbar();
        return (int)SHAppBarMessage(ABM_GETSTATE, ref d).ToUInt32();
    }

    public static void SetTaskbarState(int state)
    {
        APPBARDATA d = new APPBARDATA();
        d.cbSize = Marshal.SizeOf(d);
        d.hWnd = WindowsTaskbar();
        d.lParam = new IntPtr(state);
        SHAppBarMessage(ABM_SETSTATE, ref d);
    }

    // Raccourci Windows + touche (Paramètres rapides, Notifications), envoyé comme si l'utilisateur l'avait tapé
    public static void PressWinKey(byte vk)
    {
        const byte LWin = 0x5B; const uint KeyUp = 2;
        keybd_event(LWin, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, KeyUp, UIntPtr.Zero);
        keybd_event(LWin, 0, KeyUp, UIntPtr.Zero);
    }
}
