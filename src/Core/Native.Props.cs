// BureauCache — Native : propriétés de fenêtre et mémoire partagée du shell, pour les badges de la barre (D-016)
using System;
using System.Runtime.InteropServices;

static partial class Native
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetProp(IntPtr hWnd, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SetProp(IntPtr hWnd, string name, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr RemoveProp(IntPtr hWnd, string name);
    [DllImport("shell32.dll")] public static extern IntPtr SHLockShared(IntPtr handle, uint processId);
    [DllImport("shell32.dll")] public static extern bool SHUnlockShared(IntPtr pv);

    // Texte d'un bloc de mémoire partagée envoyé par une application (description du badge), ou null
    public static string SharedText(IntPtr handle, uint processId)
    {
        if (handle == IntPtr.Zero || processId == 0) return null;
        IntPtr p = SHLockShared(handle, processId);
        if (p == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUni(p); }
        catch { return null; }
        finally { SHUnlockShared(p); }
    }
}
