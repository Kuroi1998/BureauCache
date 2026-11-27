// BureauCache — Native : propriétés de fenêtre et mémoire partagée du shell, pour les badges de la barre (D-016)
using System;
using System.Runtime.InteropServices;

static partial class Native
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetProp(IntPtr hWnd, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SetProp(IntPtr hWnd, string name, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr RemoveProp(IntPtr hWnd, string name);
    // shlwapi exporte ces deux fonctions par ordinal.
    [DllImport("shlwapi.dll", EntryPoint = "#8", ExactSpelling = true)] static extern IntPtr SHLockShared(IntPtr handle, uint processId);
    [DllImport("shlwapi.dll", EntryPoint = "#9", ExactSpelling = true)] static extern bool SHUnlockShared(IntPtr pv);

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORY_BASIC_INFORMATION { public IntPtr BaseAddress, AllocationBase; public uint AllocationProtect; public IntPtr RegionSize; public uint State, Protect, Type; }
    [DllImport("kernel32.dll")] static extern UIntPtr VirtualQuery(IntPtr address, out MEMORY_BASIC_INFORMATION info, UIntPtr length);

    internal const int MaxSharedChars = 512;   // une description de badge fait quelques mots

    // Texte d'un bloc de mémoire partagée envoyé par une application (description du badge), ou null. La lecture
    // s'arrête au premier caractère nul, mais jamais au-delà de la région projetée ni de 512 caractères : un bloc mal
    // formé (sans caractère nul) faisait lire hors de la mémoire, une erreur que .NET ne rattrape pas (QA-69).
    public static string SharedText(IntPtr handle, uint processId)
    {
        if (handle == IntPtr.Zero || processId == 0) return null;
        try
        {
            IntPtr p = SHLockShared(handle, processId);
            if (p == IntPtr.Zero) return null;
            try
            {
                MEMORY_BASIC_INFORMATION info;
                if (VirtualQuery(p, out info, (UIntPtr)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) == UIntPtr.Zero) return null;
                long available = info.BaseAddress.ToInt64() + info.RegionSize.ToInt64() - p.ToInt64();
                return ReadBoundedUnicode(p, (int)Math.Min(MaxSharedChars, Math.Max(0, available / 2)));
            }
            finally { SHUnlockShared(p); }
        }
        catch { return null; }
    }

    // Lit au plus maxChars caractères UTF-16, jusqu'au premier caractère nul
    internal static string ReadBoundedUnicode(IntPtr p, int maxChars)
    {
        if (p == IntPtr.Zero || maxChars <= 0) return null;
        int n = 0;
        while (n < maxChars && Marshal.ReadInt16(p, n * 2) != 0) n++;
        return Marshal.PtrToStringUni(p, n);
    }
}
