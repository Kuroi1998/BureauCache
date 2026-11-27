// BureauCache — Native : nom affiché d'un élément du shell, et messages système (barre des tâches, D-014)
using System;
using System.Runtime.InteropServices;

static partial class Native
{
    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint sigdn, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false, EntryPoint = "SHCreateItemFromParsingName")]
    static extern void SHCreateShellItem(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem item);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")] public static extern IntPtr SendMessagePtr(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // Nom que l'Explorateur affiche pour un élément (« Calculatrice » pour shell:AppsFolder\Microsoft.WindowsCalculator…),
    // ou null s'il est introuvable
    public static string ShellDisplayName(string parsingName)
    {
        IShellItem item = null;
        IntPtr p = IntPtr.Zero;
        try
        {
            SHCreateShellItem(parsingName, IntPtr.Zero, typeof(IShellItem).GUID, out item);
            item.GetDisplayName(0 /*SIGDN_NORMALDISPLAY*/, out p);
            string name = p != IntPtr.Zero ? Marshal.PtrToStringUni(p) : null;
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }
        catch { return null; }
        finally
        {
            if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p);
            if (item != null) Marshal.ReleaseComObject(item);
        }
    }
}
