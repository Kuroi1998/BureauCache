// BureauCache — Archiver
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// =====================================================================
//  Rangement automatique du bureau
// =====================================================================
static class Archiver
{
    public static string Desk { get { return Paths.Desktop; } }
    public static string Folder { get { return Path.Combine(Desk, Settings.ArchiveName); } }

    static DateTime Max(DateTime a, DateTime b) { return a > b ? a : b; }

    public static List<string> Candidates()
    {
        List<string> res = new List<string>();
        DateTime limit = DateTime.Now.AddDays(-Settings.ArchiveDays);
        string[] entries;
        try { entries = Directory.GetFileSystemEntries(Desk); } catch { return res; }
        foreach (string p in entries)
        {
            try
            {
                string name = Path.GetFileName(p);
                if (string.Equals(p, Folder, StringComparison.OrdinalIgnoreCase)) continue;
                FileAttributes a = File.GetAttributes(p);
                if ((a & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                string ext = Path.GetExtension(p).ToLowerInvariant();
                if (ext == ".lnk" || ext == ".url" || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                if (Settings.FavIndex(p) >= 0) continue;
                DateTime last = Max(File.GetLastWriteTime(p), File.GetCreationTime(p));
                long[] u;
                if (Settings.Usage.TryGetValue(p, out u)) last = Max(last, new DateTime(u[1]));
                if ((a & FileAttributes.Directory) != 0)
                    try { foreach (string c in Directory.EnumerateFileSystemEntries(p).Take(300)) last = Max(last, File.GetLastWriteTime(c)); } catch { }
                if (last < limit) res.Add(p);
            }
            catch { }
        }
        return res;
    }

    public static int Run(List<string> items)
    {
        if (items.Count == 0) return 0;
        Directory.CreateDirectory(Folder);
        int n = 0;
        List<string> log = new List<string>();
        foreach (string p in items)
        {
            try
            {
                string dest = Unique(Path.Combine(Folder, Path.GetFileName(p)));
                if (Directory.Exists(p)) Directory.Move(p, dest); else File.Move(p, dest);
                Settings.RenameKey(p, dest);
                log.Add(p + ">" + dest);
                n++;
            }
            catch { }
        }
        if (log.Count > 0) Settings.ArchiveLog = log;
        Settings.LastArchive = DateTime.Now.Ticks;
        Settings.SaveQuiet();
        return n;
    }

    public static bool CanUndo { get { return Settings.ArchiveLog.Count > 0; } }

    // Remet sur le bureau les éléments du dernier rangement
    public static int Undo()
    {
        int n = 0;
        foreach (string e in Settings.ArchiveLog)
        {
            int i = e.IndexOf('>');
            if (i <= 0) continue;
            string src = e.Substring(0, i), dest = e.Substring(i + 1);
            try
            {
                if (File.Exists(src) || Directory.Exists(src)) continue;
                if (Directory.Exists(dest)) Directory.Move(dest, src);
                else if (File.Exists(dest)) File.Move(dest, src);
                else continue;
                Settings.RenameKey(dest, src);
                n++;
            }
            catch { }
        }
        Settings.ArchiveLog = new List<string>();
        Settings.SaveQuiet();
        return n;
    }

    static string Unique(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path), name = Path.GetFileNameWithoutExtension(path), ext = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            string p = Path.Combine(dir, name + " (" + i + ")" + ext);
            if (!File.Exists(p) && !Directory.Exists(p)) return p;
        }
    }
}
