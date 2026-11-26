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

    // Racines autorisées : AppData local et itinérant, s'ils sont sur un disque local (revue QA-23 : un AppData redirigé
    // vers un partage réseau est exclu). %TEMP% n'est PAS une racine : il n'est nettoyé que s'il est dans AppData\Local
    // (cas normal), jamais s'il a été redirigé vers un partage, un dossier de données ou la racine d'un disque.
    public static string[] Roots { get { return Full(new[] { Local, Roaming }).Where(LocalFolder).ToArray(); } }

    static bool LocalFolder(string path)
    {
        try
        {
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return false;
            string root = Path.GetPathRoot(path);
            if (string.Equals(root.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return false;
            return new DriveInfo(root).DriveType == DriveType.Fixed;
        }
        catch { return false; }
    }
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

    // Vrai si path est dans une racine autorisée et hors des zones interdites. Racines et zones sont comparées comme
    // des dossiers complets (séparateur final ajouté) : « allowed-voisin » n'est pas dans « allowed » (revue QA-23).
    internal static bool IsAllowed(string path, string[] roots, string[] forbidden)
    {
        string full;
        try { full = Dir(path); }
        catch { return false; }
        if (forbidden.Any(f => !string.IsNullOrEmpty(f) && full.StartsWith(Dir(f), StringComparison.OrdinalIgnoreCase))) return false;
        return roots.Any(r => !string.IsNullOrEmpty(r) && full.StartsWith(Dir(r), StringComparison.OrdinalIgnoreCase));
    }

    static string Dir(string path) { return Path.GetFullPath(path).TrimEnd('\\') + "\\"; }

    // Vrai si un dossier entre la racine autorisée et path (path compris) est une jonction, un lien ou un point de
    // montage : la cible serait en réalité ailleurs (revue QA-23)
    internal static bool CrossesLink(string path, string[] roots)
    {
        try
        {
            string full = Dir(path);
            string root = roots.Select(r => Dir(r)).Where(r => full.StartsWith(r, StringComparison.OrdinalIgnoreCase))
                               .OrderByDescending(r => r.Length).FirstOrDefault();
            if (root == null) return true;
            for (DirectoryInfo d = new DirectoryInfo(full); d != null && d.FullName.TrimEnd('\\').Length >= root.TrimEnd('\\').Length; d = d.Parent)
                if (d.Exists && Linked(d.Attributes)) return true;
            return false;
        }
        catch { return true; }
    }

    // ---------- Catégories ----------
    public static List<CleanCategory> Categories()
    {
        List<CleanCategory> list = new List<CleanCategory>();

        CleanCategory temp = new CleanCategory { Id = "temp", Checked = true,
            Title = L.T("Fichiers temporaires", "Temporary files", "Archivos temporales"),
            Detail = L.T("Dossier temporaire de ton compte (fichiers de plus de 24 h).", "Your account's temporary folder (files older than 24 h).", "Carpeta temporal de tu cuenta (archivos de más de 24 h).") };
        temp.Targets.Add(new CleanTarget { Dir = Path.GetTempPath(), MinAge = TimeSpan.FromHours(24) });
        list.Add(temp);

        CleanCategory web = new CleanCategory { Id = "browsers", Checked = true,
            Title = L.T("Caches des navigateurs", "Browser caches", "Cachés de navegadores"),
            Detail = L.T("Edge, Chrome, Brave, Vivaldi, Opera, Firefox. Mots de passe, historique et sessions restent intacts.",
                         "Edge, Chrome, Brave, Vivaldi, Opera, Firefox. Passwords, history and sessions stay intact.", "Edge, Chrome, Brave, Vivaldi, Opera y Firefox. Se conservan las contraseñas, el historial y las sesiones.") };
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
            Title = L.T("Caches des applications", "App caches", "Cachés de aplicaciones"),
            Detail = L.T("Discord, Teams, Steam, Visual Studio Code. Comptes et réglages restent intacts.",
                         "Discord, Teams, Steam, Visual Studio Code. Accounts and settings stay intact.", "Discord, Teams, Steam y Visual Studio Code. Se conservan las cuentas y la configuración.") };
        Dirs(apps, Path.Combine(Roaming, "discord"), new[] { @"Cache\Cache_Data", "Code Cache", "GPUCache" }, "Discord");
        Dirs(apps, Path.Combine(Local, @"Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\EBWebView\Default"),
            new[] { @"Cache\Cache_Data", "GPUCache" }, "ms-teams");
        Dirs(apps, Path.Combine(Local, @"Steam\htmlcache"), new[] { "Cache", "Cache_Data", "Code Cache", "GPUCache" }, "steam", "steamwebhelper");
        Dirs(apps, Path.Combine(Roaming, "Code"), new[] { "Cache", "CachedData", "Code Cache", "GPUCache" }, "Code");
        list.Add(apps);

        CleanCategory crash = new CleanCategory { Id = "crash",
            Title = L.T("Rapports d'erreurs", "Error reports", "Informes de errores"),
            Detail = L.T("Rapports de plantage et vidages mémoire de Windows. Utiles pour diagnostiquer un problème récent.",
                         "Windows crash reports and memory dumps. Useful to diagnose a recent problem.", "Informes de fallos y volcados de memoria de Windows. Ayudan a diagnosticar problemas recientes.") };
        Dirs(crash, Path.Combine(Local, @"Microsoft\Windows\WER"), new[] { "ReportArchive", "ReportQueue" });
        crash.Targets.Add(new CleanTarget { Dir = Path.Combine(Local, "CrashDumps") });
        list.Add(crash);

        CleanCategory thumbs = new CleanCategory { Id = "thumbs",
            Title = L.T("Miniatures de Windows", "Windows thumbnails", "Miniaturas de Windows"),
            Detail = L.T("Recréées automatiquement, mais l'Explorateur sera plus lent la première fois. Souvent verrouillées.",
                         "Rebuilt automatically, but Explorer will be slower the first time. Often locked.", "Se regeneran automáticamente, aunque el Explorador irá más lento la primera vez. Suelen estar bloqueadas.") };
        thumbs.Targets.Add(new CleanTarget { Dir = Path.Combine(Local, @"Microsoft\Windows\Explorer"), Pattern = "thumbcache_*.db", Recurse = false });
        list.Add(thumbs);

        list.Add(new CleanCategory { Id = "bin", RecycleBin = true,
            Title = L.T("Corbeille", "Recycle Bin", "Papelera de reciclaje"),
            Detail = L.T("Vide définitivement la corbeille de tous les disques.", "Permanently empties the Recycle Bin on all drives.", "Vacía de forma permanente la papelera de reciclaje de todas las unidades.") });
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

    // Fichiers supprimables d'une cible : jamais à travers une jonction, un lien ou un point de montage (ni dans la
    // cible, ni au-dessus d'elle), jamais dans une zone interdite, même sous une cible autorisée (revue QA-23)
    internal static IEnumerable<FileInfo> Files(CleanTarget t, string[] roots, string[] forbidden, DateTime nowUtc)
    {
        if (!IsAllowed(t.Dir, roots, forbidden) || CrossesLink(t.Dir, roots)) yield break;
        DirectoryInfo root = new DirectoryInfo(t.Dir);
        if (!root.Exists) yield break;
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
                FileInfo f = e as FileInfo;
                if (f == null)
                {
                    DirectoryInfo sub = (DirectoryInfo)e;
                    if (t.Recurse && Usable(sub, roots, forbidden)) pending.Push(sub);
                    continue;
                }
                if (Eligible(f, t, nowUtc)) yield return f;
            }
        }
    }

    static bool Usable(DirectoryInfo d, string[] roots, string[] forbidden)
    {
        try { return !Linked(d.Attributes) && IsAllowed(d.FullName, roots, forbidden); }
        catch { return false; }
    }

    // Un fichier en lecture seule est laissé en place : retirer l'attribut modifierait aussi les autres noms d'un lien
    // physique, et c'est souvent un fichier protégé volontairement
    static bool Eligible(FileInfo f, CleanTarget t, DateTime nowUtc)
    {
        try
        {
            FileAttributes a = f.Attributes;
            if (Linked(a) || CloudOnly(a) || (a & FileAttributes.ReadOnly) != 0) return false;
            if (t.Pattern != "*" && !Matches(f.Name, t.Pattern)) return false;
            if (t.MinAge > TimeSpan.Zero && (nowUtc - f.LastWriteTimeUtc < t.MinAge || nowUtc - f.CreationTimeUtc < t.MinAge)) return false;
            return true;
        }
        catch { return false; }
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
        Sandbox.Guard("nettoyage des vrais caches");
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
                Sandbox.Guard("vidage de la corbeille");
                Native.RecycleBinInfo(out count, out size);
                if (count > 0 && Native.EmptyRecycleBinQuietly()) { r.Freed += size; r.Deleted += (int)Math.Min(int.MaxValue, count); }
                continue;
            }
            foreach (CleanTarget t in c.Targets)
            {
                if (IsRunning(t.Processes, new List<string>())) continue;   // revérifié juste avant chaque cible
                Dictionary<string, bool> dirOk = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (FileInfo f in Files(t, roots, forbidden, now).ToList())
                {
                    long freed = Delete(f, t, roots, forbidden, dirOk);
                    if (freed < 0) { r.Skipped++; continue; }
                    r.Freed += freed;
                    r.Deleted++;
                }
                RemoveEmptyDirs(t, roots, forbidden);
            }
        }
        return r;
    }

    // Suppression d'un fichier après une dernière vérification : il a pu changer depuis l'inventaire. Une ouverture
    // EXCLUSIVE préalable échoue si un programme l'a encore ouvert, même en partageant la suppression (revue QA-23).
    // Renvoie la taille libérée, ou -1 si le fichier est laissé en place.
    static long Delete(FileInfo f, CleanTarget t, string[] roots, string[] forbidden, Dictionary<string, bool> dirOk)
    {
        Sandbox.GuardPath(f.FullName, "suppression de cache");
        try
        {
            f.Refresh();
            if (!f.Exists || !Eligible(f, t, DateTime.UtcNow)) return -1;
            bool ok;
            if (!dirOk.TryGetValue(f.DirectoryName, out ok))
                dirOk[f.DirectoryName] = ok = IsAllowed(f.DirectoryName, roots, forbidden) && !CrossesLink(f.DirectoryName, roots);
            if (!ok) return -1;
            long length = f.Length;
            using (new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.None)) { }
            f.Delete();
            return length;
        }
        catch { return -1; }   // fichier utilisé ou inaccessible : on le laisse
    }

    // Sous-dossiers devenus vides (jamais la cible elle-même, jamais à travers un lien, jamais dans une zone interdite)
    static void RemoveEmptyDirs(CleanTarget t, string[] roots, string[] forbidden)
    {
        if (!t.Recurse || !IsAllowed(t.Dir, roots, forbidden) || CrossesLink(t.Dir, roots)) return;
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
                if (!Usable(s, roots, forbidden)) continue;
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
