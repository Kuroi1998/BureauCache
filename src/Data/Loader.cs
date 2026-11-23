// BureauCache — Loader
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// Chargement en arrière-plan des éléments (bureau, applications, fichiers récents)
static class Loader
{
    static readonly Dictionary<string, KeyValuePair<long, string>> classCache =
        new Dictionary<string, KeyValuePair<long, string>>(StringComparer.OrdinalIgnoreCase);

    static string Classify(string path, DateTime modified)
    {
        KeyValuePair<long, string> cached;
        lock (classCache)
            if (classCache.TryGetValue(path, out cached) && cached.Key == modified.Ticks) return cached.Value;
        string r = Catalog.Classify(path, false);
        lock (classCache) classCache[path] = new KeyValuePair<long, string>(modified.Ticks, r);
        return r;
    }

    static bool IsHidden(string p)
    {
        try { return (File.GetAttributes(p) & (FileAttributes.Hidden | FileAttributes.System)) != 0; }
        catch { return true; }
    }

    public static List<DesktopItem> Desktop(int px, bool showSystem)
    {
        List<DesktopItem> items = new List<DesktopItem>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] dirs = Paths.Desktops;
        if (showSystem)
        {
            items.Add(new DesktopItem { Key = "::ce-pc", Name = L.T("Ce PC", "This PC", "Este equipo"), Target = "explorer.exe", Args = "shell:MyComputerFolder", IsSystem = true, AutoCategory = "Dossiers", Icon = IconCache.Get("stock|15|" + px, () => Icons.Stock(15, px)) });
            items.Add(new DesktopItem { Key = "::corbeille", Name = L.T("Corbeille", "Recycle Bin", "Papelera de reciclaje"), Target = "explorer.exe", Args = "shell:RecycleBinFolder", IsSystem = true, AutoCategory = "Dossiers", Icon = IconCache.Get("stock|31|" + px, () => Icons.Stock(31, px)) });
        }
        foreach (string dir in dirs)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
            try
            {
                foreach (string d in Directory.GetDirectories(dir))
                {
                    if (IsHidden(d)) continue;
                    string n = Path.GetFileName(d);
                    if (!seen.Add("d:" + n)) continue;
                    DateTime cr = Directory.GetCreationTime(d), mo = Directory.GetLastWriteTime(d);
                    items.Add(new DesktopItem { Key = d, Name = n, Target = d, IsFolder = true, AutoCategory = "Dossiers", Created = cr, Modified = mo, Icon = IconCache.ForFile(d, px) });
                }
                foreach (string f in Directory.GetFiles(dir))
                {
                    if (IsHidden(f)) continue;
                    string fn = Path.GetFileName(f);
                    if (fn.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    string n = (ext == ".lnk" || ext == ".url") ? Path.GetFileNameWithoutExtension(f) : fn;
                    string nameKey = "f:" + n;
                    if (seen.Contains(nameKey)) continue;
                    try
                    {
                        FileInfo fi = new FileInfo(f);
                        long size = fi.Length;
                        DateTime modified = fi.LastWriteTime;
                        items.Add(new DesktopItem
                        {
                            Key = f, Name = n, Target = f, AutoCategory = Classify(f, modified),
                            Created = fi.CreationTime, Modified = modified, Size = size, Icon = IconCache.ForFile(f, px)
                        });
                        seen.Add(nameKey);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            catch { }
        }
        return items;
    }

    static void Walk(string dir, List<string> acc, int depth)
    {
        if (depth > 6) return;
        try { acc.AddRange(Directory.GetFiles(dir, "*.lnk")); } catch { }
        try { foreach (string d in Directory.GetDirectories(dir)) Walk(d, acc, depth + 1); } catch { }
    }

    public static List<DesktopItem> Apps(int px)
    {
        List<DesktopItem> res = new List<DesktopItem>();
        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> lnks = new List<string>();
        Walk(Environment.GetFolderPath(Environment.SpecialFolder.Programs), lnks, 0);
        Walk(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), lnks, 0);
        foreach (string l in lnks)
        {
            string n = Path.GetFileNameWithoutExtension(l);
            string nn = Search.Norm(n);
            if (nn.Contains("uninstall") || nn.Contains("desinstall")) continue;
            if (!names.Add(n)) continue;
            res.Add(new DesktopItem { Kind = ItemKind.App, Key = "app:" + l, Name = n, Target = l, Detail = L.T("Application", "App", "Aplicación"), Icon = IconCache.ForFile(l, px) });
        }
        try
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
            dynamic folder = shell.NameSpace("shell:AppsFolder");
            dynamic items = folder.Items();
            int count = items.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    dynamic it = items.Item(i);
                    string n = it.Name;
                    string p = it.Path;
                    if (string.IsNullOrEmpty(n) || string.IsNullOrEmpty(p) || !p.Contains("!") || names.Contains(n)) continue;
                    names.Add(n);
                    string parsing = "shell:AppsFolder\\" + p;
                    res.Add(new DesktopItem
                    {
                        Kind = ItemKind.App, Key = "app:" + p, Name = n, Target = "explorer.exe", Args = parsing,
                        Detail = L.T("Application du Store", "Store app", "Aplicación de la Store"),
                        Icon = IconCache.Get("uwp|" + p + "|" + px, () => Icons.ShellImage(parsing, px, false))
                    });
                }
                catch { }
            }
        }
        catch { }
        return res;
    }

    public static List<DesktopItem> Recent(int px)
    {
        List<DesktopItem> res = new List<DesktopItem>();
        try
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FileInfo f in new DirectoryInfo(dir).GetFiles("*.lnk").OrderByDescending(f => f.LastWriteTime).Take(120))
            {
                string t = ShellUtil.ResolveLnk(f.FullName);
                if (string.IsNullOrEmpty(t) || !seen.Add(t)) continue;
                bool isDir = Directory.Exists(t);
                if (!isDir && !File.Exists(t)) continue;
                string name = Path.GetFileName(t.TrimEnd('\\'));
                if (string.IsNullOrEmpty(name)) continue;
                res.Add(new DesktopItem
                {
                    Kind = ItemKind.Recent, Key = t, Name = name, Target = t, IsFolder = isDir,
                    Detail = Path.GetDirectoryName(t), Modified = f.LastWriteTime, Icon = IconCache.ForFile(t, px)
                });
                if (res.Count >= 40) break;
            }
        }
        catch { }
        return res;
    }
}
