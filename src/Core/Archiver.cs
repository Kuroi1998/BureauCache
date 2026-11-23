// BureauCache — Archiver
using System;
using System.Collections.Generic;
using System.IO;

// =====================================================================
//  Rangement automatique du bureau
// =====================================================================
static class Archiver
{
    public static string Desk { get { return Paths.Desktop; } }
    public static string Folder { get { return Path.Combine(Desk, Settings.ArchiveName); } }

    // Nombre maximal d'entrées examinées dans un dossier : au-delà, son ancienneté n'est pas confirmée
    internal const int MaxScan = 5000;

    internal enum Activity { Recent, Old, Unknown }

    static DateTime Limit { get { return DateTime.Now.AddDays(-Settings.ArchiveDays); } }

    public static List<string> Candidates()
    {
        List<string> res = new List<string>();
        DateTime limit = Limit;
        string[] entries;
        try { entries = Directory.GetFileSystemEntries(Desk); } catch { return res; }
        foreach (string p in entries) if (IsCandidate(p, limit)) res.Add(p);
        return res;
    }

    // Vrai si l'élément du bureau peut être rangé : ni exclu, ni utilisé depuis la limite, et ancienneté confirmée
    static bool IsCandidate(string p, DateTime limit)
    {
        try
        {
            string name = Path.GetFileName(p);
            if (string.Equals(p, Folder, StringComparison.OrdinalIgnoreCase)) return false;
            FileAttributes a = File.GetAttributes(p);
            if ((a & (FileAttributes.Hidden | FileAttributes.System)) != 0) return false;
            string ext = Path.GetExtension(p).ToLowerInvariant();
            if (ext == ".lnk" || ext == ".url" || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) return false;
            if (Settings.FavIndex(p) >= 0) return false;
            long[] u;
            if (Settings.Usage.TryGetValue(p, out u) && new DateTime(u[1]) >= limit) return false;
            return ActivityOf(p, limit, MaxScan) == Activity.Old;
        }
        catch { return false; }
    }

    // Ancienneté d'un fichier ou d'un dossier (R-01). Un dossier n'est « ancien » que si lui-même et tout son
    // contenu, sous-dossiers compris, le sont. Le parcours s'arrête au premier élément récent. Si le dossier ne peut
    // pas être lu entièrement (droits, erreur) ou dépasse maxEntries, l'ancienneté n'est pas confirmée : Unknown.
    internal static Activity ActivityOf(string path, DateTime limit, int maxEntries)
    {
        try
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            FileSystemInfo root = dir.Exists ? (FileSystemInfo)dir : new FileInfo(path);
            if (!root.Exists) return Activity.Unknown;
            if (IsRecent(root, limit)) return Activity.Recent;
            if (!dir.Exists) return Activity.Old;
            int seen = 0;
            Stack<DirectoryInfo> pending = new Stack<DirectoryInfo>();
            pending.Push(dir);
            while (pending.Count > 0)
            {
                foreach (FileSystemInfo child in pending.Pop().EnumerateFileSystemInfos())
                {
                    if (++seen > maxEntries) return Activity.Unknown;
                    if (IsRecent(child, limit)) return Activity.Recent;
                    DirectoryInfo sub = child as DirectoryInfo;
                    if (sub != null) pending.Push(sub);
                }
            }
            return Activity.Old;
        }
        catch { return Activity.Unknown; }
    }

    // La date de création compte aussi : un élément ancien copié récemment a été apporté récemment
    static bool IsRecent(FileSystemInfo info, DateTime limit)
    {
        return info.LastWriteTime >= limit || info.CreationTime >= limit;
    }

    // Rangement automatique : les candidats viennent d'être évalués, pas besoin de les revérifier
    public static int RunNow() { return Run(Candidates(), false); }

    // Rangement d'une liste établie plus tôt (ex. : après confirmation) : chaque élément est revérifié (R-03)
    public static int Run(List<string> items) { return Run(items, true); }

    static int Run(List<string> items, bool revalidate)
    {
        if (items.Count == 0) return 0;
        string folder = Folder;
        DateTime limit = Limit;
        try
        {
            Directory.CreateDirectory(folder);
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) return 0;
        }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }
        int n = 0;
        List<string> log = new List<string>();
        foreach (string p in items)
        {
            // L'élément a pu être utilisé, renommé ou déplacé depuis l'établissement de la liste
            if (revalidate && !IsCandidate(p, limit)) continue;
            try
            {
                string dest = Unique(Path.Combine(folder, Path.GetFileName(p)));
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
        int restored = RestoreArchived(Desk, Settings.ArchiveLog);
        Settings.SaveQuiet();
        return restored;
    }

    internal static int RestoreArchived(string desktop, List<string> journal)
    {
        int n = 0;
        List<string> pending = new List<string>();
        foreach (string e in journal)
        {
            int i = e.IndexOf('>');
            if (i <= 0) continue;
            string src = e.Substring(0, i), dest = e.Substring(i + 1);
            try
            {
                src = Path.GetFullPath(src);
                dest = Path.GetFullPath(dest);
                string parent = Path.GetDirectoryName(dest);
                if (parent == null || !Paths.SameFolder(Path.GetDirectoryName(src), desktop) ||
                    !Paths.SameFolder(Path.GetDirectoryName(parent), desktop)) continue;
                if (!Directory.Exists(parent) || (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) continue;
                if (File.Exists(src) || Directory.Exists(src)) { pending.Add(e); continue; }
                if (Directory.Exists(dest)) Directory.Move(dest, src);
                else if (File.Exists(dest)) File.Move(dest, src);
                else continue;
                Settings.RenameKey(dest, src);
                n++;
            }
            catch { pending.Add(e); }
        }
        journal.Clear();
        journal.AddRange(pending);
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
