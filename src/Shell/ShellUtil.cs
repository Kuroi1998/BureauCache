// BureauCache — ShellUtil
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

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
        Sandbox.GuardPath(lnk, "raccourci");
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

    // Windows 11 note dans le registre si l'icône d'un programme est affichée à côté de l'horloge (IsPromoted = 1)
    // ou rangée dans la flèche ^. Lecture seule : c'est à l'utilisateur de changer ce réglage de Windows.
    // Renvoie null si l'information n'existe pas (Windows 10, ou icône encore jamais affichée).
    public static bool? TrayIconPromoted(string exePath)
    {
        try
        {
            string tail = Path.Combine(Path.GetFileName(Path.GetDirectoryName(exePath)), Path.GetFileName(exePath));
            using (RegistryKey root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings"))
            {
                if (root == null) return null;
                bool? found = null;
                foreach (string name in root.GetSubKeyNames())
                    using (RegistryKey k = root.OpenSubKey(name))
                    {
                        string p = k == null ? null : k.GetValue("ExecutablePath") as string;
                        if (p == null || !p.EndsWith(tail, StringComparison.OrdinalIgnoreCase)) continue;
                        object v = k.GetValue("IsPromoted");
                        bool promoted = v is int && (int)v == 1;
                        if (string.Equals(p, exePath, StringComparison.OrdinalIgnoreCase)) return promoted;
                        found = promoted;
                    }
                return found;
            }
        }
        catch { return null; }
    }

    // Un nom nu (« taskmgr.exe », « devmgmt.msc ») est cherché par le Shell dans le dossier courant avant System32 :
    // un fichier homonyme posé à côté de BureauCache serait lancé à sa place (CDX-40). On donne le chemin complet
    // quand l'outil existe dans le dossier système, sinon le nom tel quel (adresses ms-settings:, protocoles).
    internal static string SystemPath(string name)
    {
        if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '\\', '/', ':' }) >= 0) return name;
        try
        {
            string full = Path.Combine(Environment.SystemDirectory, name);
            return File.Exists(full) ? full : name;
        }
        catch (Exception) { return name; }
    }

    public static void Open(string target, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(target);
        if (args != null) psi.Arguments = args;
        psi.UseShellExecute = true;
        Process.Start(psi);
    }
}
