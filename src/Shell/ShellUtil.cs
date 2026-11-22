// BureauCache — ShellUtil
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// =====================================================================
//  Raccourcis, liens, verbes du shell
// =====================================================================
static class ShellUtil
{
    [ThreadStatic] static object wsh;
    static dynamic Wsh
    {
        get
        {
            if (wsh == null) wsh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            return wsh;
        }
    }

    public static string ResolveLnk(string p)
    {
        try { dynamic s = Wsh.CreateShortcut(p); return (string)s.TargetPath; }
        catch { return null; }
    }

    public static void CreateShortcut(string lnk, string target, string desc)
    {
        dynamic s = Wsh.CreateShortcut(lnk);
        s.TargetPath = target;
        string wd = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(wd)) s.WorkingDirectory = wd;
        if (desc != null) s.Description = desc;
        s.Save();
    }

    public static string ReadUrl(string p)
    {
        try
        {
            if (new FileInfo(p).Length > 64 * 1024) return null; // un raccourci Internet fait quelques octets
            foreach (string l in File.ReadAllLines(p))
                if (l.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)) return l.Substring(4);
        }
        catch { }
        return null;
    }

    public static List<KeyValuePair<string, object>> Verbs(string path)
    {
        List<KeyValuePair<string, object>> res = new List<KeyValuePair<string, object>>();
        try
        {
            dynamic app = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
            dynamic folder = app.NameSpace(Path.GetDirectoryName(path));
            if (folder == null) return res;
            dynamic item = folder.ParseName(Path.GetFileName(path));
            if (item == null) return res;
            dynamic verbs = item.Verbs();
            int n = verbs.Count;
            for (int i = 0; i < n; i++)
            {
                dynamic v = verbs.Item(i);
                string name = v.Name;
                if (!string.IsNullOrEmpty(name) && name.Replace("&", "").Trim().Length > 0)
                    res.Add(new KeyValuePair<string, object>(name, (object)v));
            }
        }
        catch { }
        return res;
    }

    public static void InvokeVerb(object verb)
    {
        try { ((dynamic)verb).DoIt(); } catch { }
    }

    // Ouvre l'Explorateur avec l'élément sélectionné
    public static void Reveal(string path)
    {
        try { Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch { }
    }

    public static void Properties(string path)
    {
        Native.SHELLEXECUTEINFO i = new Native.SHELLEXECUTEINFO();
        i.cbSize = Marshal.SizeOf(i);
        i.lpVerb = "properties";
        i.lpFile = path;
        i.nShow = 1;
        i.fMask = 0x0000000C;
        Native.ShellExecuteEx(ref i);
    }

    public static void Open(string target, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(target);
        if (args != null) psi.Arguments = args;
        psi.UseShellExecute = true;
        Process.Start(psi);
    }
}
