// BureauCache — Cleaner
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

// =====================================================================
//  Nettoyage rapide (décision D-005). Uniquement des caches et des fichiers
//  temporaires DANS LE PROFIL de l'utilisateur. Règles de sécurité :
//  - chaque chemin doit être sous %LOCALAPPDATA%, %APPDATA% ou %TEMP%, jamais
//    dans Windows, Program Files ou OneDrive ;
//  - jamais de suppression récursive : fichier par fichier, sans suivre les
//    jonctions ni les liens, et sans toucher aux fichiers « en ligne uniquement » ;
//  - fichiers temporaires de moins de 24 h ignorés (installations en cours) ;
//  - cache d'une application ignoré tant qu'elle est ouverte.
// =====================================================================
class CleanTarget
{
    public string Dir, Pattern = "*";
    public bool Recurse = true;
    public TimeSpan MinAge = TimeSpan.Zero;
    public string[] Processes = new string[0];
}

class CleanCategory
{
    public string Id, Title, Detail;
    public bool Checked, RecycleBin;
    public List<CleanTarget> Targets = new List<CleanTarget>();
    public long Size;
    public int Files;
    public List<string> Running = new List<string>();   // applications ouvertes, dont le cache est ignoré
}

class CleanResult
{
    public long Freed;
    public int Deleted, Skipped;
}

static class Cleaner
{
    const FileAttributes RecallOnOpen = (FileAttributes)0x40000, RecallOnDataAccess = (FileAttributes)0x400000;

    static string Local { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
    static string Roaming { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } }

    // Racines autorisées et zones interdites, calculées sur ce PC
    public static string[] Roots { get { return Full(new[] { Local, Roaming, Path.GetTempPath() }); } }
    public static string[] Forbidden
    {
        get
        {
            return Full(new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetEnvironmentVariable("OneDrive"),
                Environment.GetEnvironmentVariable("OneDriveConsumer"),
                Environment.GetEnvironmentVariable("OneDriveCommercial") });
        }
    }

    static string[] Full(string[] paths)
    {
        return paths.Where(p => !string.IsNullOrEmpty(p)).Select(p => Path.GetFullPath(p).TrimEnd('\\') + "\\").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // Vrai si path est dans une racine autorisée et hors des zones interdites
    internal static bool IsAllowed(string path, string[] roots, string[] forbidden)
    {
        string full;
        try { full = Path.GetFullPath(path).TrimEnd('\\') + "\\"; }
        catch { return false; }
        if (forbidden.Any(f => full.StartsWith(f, StringComparison.OrdinalIgnoreCase))) return false;
        return roots.Any(r => full.StartsWith(r, StringComparison.OrdinalIgnoreCase));
    }

    // ---------- Catégories ----------
    public static List<CleanCategory> Categories()
    {
        List<CleanCategory> list = new List<CleanCategory>();

        CleanCategory temp = new CleanCategory { Id = "temp", Checked = true,
            Title = L.T("Fichiers temporaires", "Temporary files"),
            Detail = L.T("Dossier temporaire de ton compte (fichiers de plus de 24 h).", "Your account's temporary folder (files older than 24 h).") };
        temp.Targets.Add(new CleanTarget { Dir = Path.GetTempPath(), MinAge = TimeSpan.FromHours(24) });
        list.Add(temp);

        CleanCategory web = new CleanCategory { Id = "browsers", Checked = true,
            Title = L.T("Caches des navigateurs", "Browser caches"),
            Detail = L.T("Edge, Chrome, Brave, Vivaldi, Opera, Firefox. Mots de passe, historique et sessions restent intacts.",
                         "Edge, Chrome, Brave, Vivaldi, Opera, Firefox. Passwords, history and sessions stay intact.") };
        Chromium(web, Path.Combine(Local, @"Microsoft\Edge\User Data"), "msedge");
        Chromium(web, Path.Combine(Local, @"Google\Chrome\User Data"), "chrome");
        Chromium(web, Path.Combine(Local, @"BraveSoftware\Brave-Browser\User Data"), "brave");
        Chromium(web, Path.Combine(Local, @"Vivaldi\User Data"), "vivaldi");
        foreach (string opera in new[] { "Opera Stable", "Opera GX Stable" })
            Dirs(web, Path.Combine(Path.Combine(Local, "Opera Software"), opera), new[] { @"Cache\Cache_Data", "GPUCache" }, "opera");
        foreach (string profile in SubDirs(Path.Combine(Local, @"Mozilla\Firefox\Profiles")))
            Dirs(web, profile, new[] { "cache2", "startupCache" }, "firefox");
        list.Add(web);

        CleanCategory apps = new CleanCategory { Id = "apps", Checked = true,
            Title = L.T("Caches des applications", "App caches"),
            Detail = L.T("Discord, Teams, Steam, Visual Studio Code. Comptes et réglages restent intacts.",
                         "Discord, Teams, Steam, Visual Studio Code. Accounts and settings stay intact.") };
        Dirs(apps, Path.Combine(Roaming, "discord"), new[] { @"Cache\Cache_Data", "Code Cache", "GPUCache" }, "Discord");
        Dirs(apps, Path.Combine(Local, @"Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\EBWebView\Default"),
            new[] { @"Cache\Cache_Data", "GPUCache" }, "ms-teams");
        Dirs(apps, Path.Combine(Local, @"Steam\htmlcache"), new[] { "Cache", "Cache_Data", "Code Cache", "GPUCache" }, "steam", "steamwebhelper");
        Dirs(apps, Path.Combine(Roaming, "Code"), new[] { "Cache", "CachedData", "Code Cache", "GPUCache" }, "Code");
        list.Add(apps);

        CleanCategory crash = new CleanCategory { Id = "crash",
            Title = L.T("Rapports d'erreurs", "Error reports"),
            Detail = L.T("Rapports de plantage et vidages mémoire de Windows. Utiles pour diagnostiquer un problème récent.",
                         "Windows crash reports and memory dumps. Useful to diagnose a recent problem.") };
        Dirs(crash, Path.Combine(Local, @"Microsoft\Windows\WER"), new[] { "ReportArchive", "ReportQueue" });
        crash.Targets.Add(new CleanTarget { Dir = Path.Combine(Local, "CrashDumps") });
        list.Add(crash);

        CleanCategory thumbs = new CleanCategory { Id = "thumbs",
            Title = L.T("Miniatures de Windows", "Windows thumbnails"),
            Detail = L.T("Recréées automatiquement, mais l'Explorateur sera plus lent la première fois. Souvent verrouillées.",
                         "Rebuilt automatically, but Explorer will be slower the first time. Often locked.") };
        thumbs.Targets.Add(new CleanTarget { Dir = Path.Combine(Local, @"Microsoft\Windows\Explorer"), Pattern = "thumbcache_*.db", Recurse = false });
        list.Add(thumbs);

        list.Add(new CleanCategory { Id = "bin", RecycleBin = true,
            Title = L.T("Corbeille", "Recycle Bin"),
            Detail = L.T("Vide définitivement la corbeille de tous les disques.", "Permanently empties the Recycle Bin on all drives.") });
        return list;
    }

    static IEnumerable<string> SubDirs(string dir)
    {
        try { return Directory.Exists(dir) ? Directory.GetDirectories(dir) : new string[0]; }
        catch { return new string[0]; }
    }

    // Navigateurs Chromium : profil « Default » et profils « Profile N »
    static void Chromium(CleanCategory c, string userData, string process)
    {
        foreach (string profile in SubDirs(userData))
        {
            string name = Path.GetFileName(profile);
            if (name == "Default" || name.StartsWith("Profile ", StringComparison.Ordinal))
                Dirs(c, profile, new[] { @"Cache\Cache_Data", "Code Cache", "GPUCache" }, process);
        }
    }

    static void Dirs(CleanCategory c, string root, string[] subs, params string[] processes)
    {
        foreach (string s in subs)
        {
            string d = Path.Combine(root, s);
            if (Directory.Exists(d)) c.Targets.Add(new CleanTarget { Dir = d, Processes = processes });
        }
    }

    // ---------- Parcours sûr ----------
    static bool Linked(FileAttributes a) { return (a & FileAttributes.ReparsePoint) != 0; }
    static bool CloudOnly(FileAttributes a) { return (a & (FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess)) != 0; }

    // Fichiers supprimables d'une cible, sans jamais entrer dans une jonction ni un lien
    internal static IEnumerable<FileInfo> Files(CleanTarget t, string[] roots, string[] forbidden, DateTime nowUtc)
    {
        if (!IsAllowed(t.Dir, roots, forbidden)) yield break;
        DirectoryInfo root = new DirectoryInfo(t.Dir);
        if (!root.Exists || Linked(root.Attributes)) yield break;
        Stack<DirectoryInfo> pending = new Stack<DirectoryInfo>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            DirectoryInfo dir = pending.Pop();
            FileSystemInfo[] entries;
            try { entries = dir.GetFileSystemInfos(); }
            catch { continue; }
            foreach (FileSystemInfo e in entries)
            {
                FileAttributes a;
                try { a = e.Attributes; } catch { continue; }
                if (Linked(a) || CloudOnly(a)) continue;
                DirectoryInfo sub = e as DirectoryInfo;
                if (sub != null) { if (t.Recurse) pending.Push(sub); continue; }
                FileInfo f = (FileInfo)e;
                if (t.Pattern != "*" && !Matches(f.Name, t.Pattern)) continue;
                if (t.MinAge > TimeSpan.Zero && (nowUtc - f.LastWriteTimeUtc < t.MinAge || nowUtc - f.CreationTimeUtc < t.MinAge)) continue;
                yield return f;
            }
        }
    }

    // Motif simple « préfixe*suffixe »
    internal static bool Matches(string name, string pattern)
    {
        int star = pattern.IndexOf('*');
        if (star < 0) return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
        string pre = pattern.Substring(0, star), post = pattern.Substring(star + 1);
        return name.Length >= pre.Length + post.Length && name.StartsWith(pre, StringComparison.OrdinalIgnoreCase) && name.EndsWith(post, StringComparison.OrdinalIgnoreCase);
    }

    // Nom affiché à l'utilisateur pour un processus (« Ferme Edge pour… »)
    internal static string FriendlyName(string process)
    {
        switch (process.ToLowerInvariant())
        {
            case "msedge": return "Edge";
            case "chrome": return "Chrome";
            case "brave": return "Brave";
            case "vivaldi": return "Vivaldi";
            case "opera": return "Opera";
            case "firefox": return "Firefox";
            case "discord": return "Discord";
            case "ms-teams": return "Teams";
            case "steam": case "steamwebhelper": return "Steam";
            case "code": return "Visual Studio Code";
        }
        return process;
    }

    static bool IsRunning(string[] processes, List<string> running)
    {
        bool any = false;
        foreach (string p in processes)
        {
            Process[] found;
            try { found = Process.GetProcessesByName(p); } catch { continue; }
            string name = FriendlyName(p);
            if (found.Length > 0) { any = true; if (!running.Contains(name)) running.Add(name); }
            foreach (Process x in found) x.Dispose();
        }
        return any;
    }

    // ---------- Analyse et nettoyage (bloquants : à lancer en arrière-plan) ----------
    public static void Measure(CleanCategory c)
    {
        c.Size = 0; c.Files = 0; c.Running.Clear();
        if (c.RecycleBin)
        {
            long count, size;
            Native.RecycleBinInfo(out count, out size);
            c.Size = size; c.Files = (int)Math.Min(int.MaxValue, count);
            return;
        }
        string[] roots = Roots, forbidden = Forbidden;
        DateTime now = DateTime.UtcNow;
        foreach (CleanTarget t in c.Targets)
        {
            if (IsRunning(t.Processes, c.Running)) continue;
            foreach (FileInfo f in Files(t, roots, forbidden, now))
            {
                try { c.Size += f.Length; c.Files++; } catch { }
            }
        }
    }

    public static CleanResult Clean(IEnumerable<CleanCategory> categories)
    {
        return Clean(categories, Roots, Forbidden);
    }

    internal static CleanResult Clean(IEnumerable<CleanCategory> categories, string[] roots, string[] forbidden)
    {
        CleanResult r = new CleanResult();
        DateTime now = DateTime.UtcNow;
        foreach (CleanCategory c in categories)
        {
            if (c.RecycleBin)
            {
                long count, size;
                Native.RecycleBinInfo(out count, out size);
                if (Native.EmptyRecycleBinQuietly()) { r.Freed += size; r.Deleted += (int)Math.Min(int.MaxValue, count); }
                continue;
            }
            List<string> running = new List<string>();
            foreach (CleanTarget t in c.Targets)
            {
                if (IsRunning(t.Processes, running)) continue;
                foreach (FileInfo f in Files(t, roots, forbidden, now).ToList())
                {
                    long len = 0;
                    try
                    {
                        len = f.Length;
                        if ((f.Attributes & FileAttributes.ReadOnly) != 0) f.Attributes &= ~FileAttributes.ReadOnly;
                        f.Delete();
                        r.Freed += len; r.Deleted++;
                    }
                    catch { r.Skipped++; }   // fichier utilisé : on le laisse
                }
                RemoveEmptyDirs(t, roots, forbidden);
            }
        }
        return r;
    }

    // Sous-dossiers devenus vides (jamais la cible elle-même, jamais à travers un lien)
    static void RemoveEmptyDirs(CleanTarget t, string[] roots, string[] forbidden)
    {
        if (!t.Recurse || !IsAllowed(t.Dir, roots, forbidden)) return;
        List<DirectoryInfo> all = new List<DirectoryInfo>();
        Stack<DirectoryInfo> pending = new Stack<DirectoryInfo>();
        try { pending.Push(new DirectoryInfo(t.Dir)); } catch { return; }
        while (pending.Count > 0)
        {
            DirectoryInfo d = pending.Pop();
            DirectoryInfo[] subs;
            try { subs = d.GetDirectories(); } catch { continue; }
            foreach (DirectoryInfo s in subs)
            {
                try { if (Linked(s.Attributes)) continue; } catch { continue; }
                all.Add(s);
                pending.Push(s);
            }
        }
        for (int i = all.Count - 1; i >= 0; i--)
        {
            try { if (!all[i].EnumerateFileSystemInfos().Any()) all[i].Delete(false); } catch { }
        }
    }
}
