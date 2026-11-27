// BureauCache — Native : identifiant d'application d'une fenêtre (AppUserModelID), pour l'icône des applis du Store
using System;
using System.Runtime.InteropServices;

static partial class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct PROPERTYKEY { public Guid fmtid; public uint pid; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPVARIANT { public ushort vt; ushort r1, r2, r3; public IntPtr p; IntPtr p2; }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
        [PreserveSig] int Commit();
    }

    [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT v);

    // System.AppUserModel.ID de la fenêtre, ou null
    public static string AppUserModelId(IntPtr hwnd)
    {
        IPropertyStore store = null;
        try
        {
            Guid iid = typeof(IPropertyStore).GUID;
            if (SHGetPropertyStoreForWindow(hwnd, ref iid, out store) != 0 || store == null) return null;
            PROPERTYKEY key = new PROPERTYKEY { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
            PROPVARIANT v;
            if (store.GetValue(ref key, out v) != 0) return null;
            try { return v.vt == 31 /*VT_LPWSTR*/ && v.p != IntPtr.Zero ? Marshal.PtrToStringUni(v.p) : null; }
            finally { PropVariantClear(ref v); }
        }
        catch { return null; }
        finally { if (store != null) Marshal.ReleaseComObject(store); }
    }
}
