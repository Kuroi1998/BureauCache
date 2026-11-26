// BureauCache — Native (presse-papiers et bureaux virtuels)
using System;
using System.Runtime.InteropServices;

// D-011 : écoute du presse-papiers et API documentée des bureaux virtuels (IVirtualDesktopManager).
// L'interface interne IVirtualDesktopManagerInternal n'est pas utilisée : elle change d'une version de Windows
// à l'autre.
static partial class Native
{
    [DllImport("user32.dll", SetLastError = true)] public static extern bool AddClipboardFormatListener(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RemoveClipboardFormatListener(IntPtr hWnd);

    [ComImport, Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IVirtualDesktopManager
    {
        [PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out int onCurrent);
        [PreserveSig] int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);
        [PreserveSig] int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }

    [ComImport, Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
    class VirtualDesktopManagerClass { }

    static IVirtualDesktopManager desktops;
    static bool desktopsUnavailable;

    // Créé une fois, sur le fil d'interface (STA). Null avant Windows 10 ou si le service est absent.
    static IVirtualDesktopManager Desktops
    {
        get
        {
            if (desktops == null && !desktopsUnavailable)
            {
                try { desktops = (IVirtualDesktopManager)new VirtualDesktopManagerClass(); }
                catch { desktopsUnavailable = true; }
            }
            return desktops;
        }
    }

    // Place la fenêtre sur le bureau virtuel actif, repéré par la fenêtre au premier plan. Ne fait rien si elle y est
    // déjà, si le premier plan n'appartient à aucun bureau (bureau Windows, barre des tâches) ou pendant les tests.
    // Vrai si la fenêtre a été déplacée.
    public static bool MoveToActiveDesktop(IntPtr window)
    {
        if (Sandbox.Active || window == IntPtr.Zero) return false;
        IVirtualDesktopManager m = Desktops;
        if (m == null) return false;
        try
        {
            int onCurrent;
            if (m.IsWindowOnCurrentVirtualDesktop(window, out onCurrent) != 0 || onCurrent != 0) return false;
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == window) return false;
            Guid id;
            if (m.GetWindowDesktopId(fg, out id) != 0 || id == Guid.Empty) return false;
            return m.MoveWindowToDesktop(window, ref id) == 0;
        }
        catch { return false; }
    }
}
