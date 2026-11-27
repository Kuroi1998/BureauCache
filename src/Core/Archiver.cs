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

    // Nombre maximal d'entrées examinées dans un dossier : au-delà, son ancienneté n'est pas confirmée
    internal const int MaxScan = 5000;

    internal enum Activity { Recent, Old, Unknown }

    // Limite en temps universel : l'heure locale se répète lors du changement d'heure d'octobre
    internal static DateTime Limit { get { return DateTime.UtcNow.AddDays(-Settings.ArchiveDays); } }

    public static List<string> Candidates()
    {
        List<string> res = new List<string>();
        DateTime limit = Limit;
        string folder = Folder;
        string[] entries;
        try { entries = Directory.GetFileSystemEntries(Desk); } catch { return res; }
        List<ArchiveRule> rules = Settings.ArchiveRules.ToList();
        foreach (string p in entries)
        {
            // Une règle dont le filtre correspond décide seule, avec son propre âge (D-009)
            ArchiveRule rule = ArchiveRules.For(p, rules);
            if (CanArchiveTo(rule != null ? rule.Destination(folder) : folder, Desk) &&
                IsCandidate(p, rule != null ? rule.Limit : limit)) res.Add(p);
        }
        return res;
    }

    // Décompte pour la page Organisation, sur un fil de fond (R-23) : favoris, historique, date limite et dossier
    // d'archive sont relevés ensemble sur le fil d'interface ; le parcours ne lit plus rien des réglages (Q26-08)
    public static int CountCandidates(HashSet<string> favorites, Dictionary<string, long[]> usage, DateTime limit, string folder)
    {
        return CountCandidates(favorites, usage, limit, folder, new List<ArchiveRule>());
    }

    public static int CountCandidates(HashSet<string> favorites, Dictionary<string, long[]> usage, DateTime limit, string folder, List<ArchiveRule> rules)
    {
        string[] entries;
        try { entries = Directory.GetFileSystemEntries(Desk); } catch { return 0; }
        int n = 0;
        foreach (string p in entries)
        {
            try
            {
                if (favorites.Contains(p) || !PassesFilters(p, folder)) continue;
                ArchiveRule rule = ArchiveRules.For(p, rules);
                if (!CanArchiveTo(rule != null ? rule.Destination(folder) : folder, Desk)) continue;
                DateTime itemLimit = rule != null ? rule.Limit : limit;
                long[] u;
                if (usage.TryGetValue(p, out u) && new DateTime(u[1], DateTimeKind.Local).ToUniversalTime() >= itemLimit) continue;
                if (ActivityOf(p, itemLimit, MaxScan) == Activity.Old) n++;
            }
            catch { }
        }
        return n;
    }

    // Filtres qui ne dépendent pas des réglages : dossier d'archive, éléments cachés ou système, raccourcis
    internal static bool PassesFilters(string p, string folder)
    {
        string name = Path.GetFileName(p);
        if (string.Equals(p, folder, StringComparison.OrdinalIgnoreCase)) return false;
        FileAttributes a = File.GetAttributes(p);
        if ((a & (FileAttributes.Hidden | FileAttributes.System)) != 0) return false;
        string ext = Path.GetExtension(p).ToLowerInvariant();
        return !(ext == ".lnk" || ext == ".url" || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase));
    }

    // Vrai si l'élément du bureau peut être rangé : ni exclu, ni utilisé depuis la limite, et ancienneté confirmée
    static bool IsCandidate(string p, DateTime limit)
    {
        try
        {
            if (!PassesFilters(p, Folder)) return false;
            if (Settings.FavIndex(p) >= 0) return false;
            long[] u;
            if (Settings.Usage.TryGetValue(p, out u) && new DateTime(u[1], DateTimeKind.Local).ToUniversalTime() >= limit) return false;
            return ActivityOf(p, limit, MaxScan) == Activity.Old;
        }
        catch { return false; }
    }

    // Ancienneté d'un fichier ou d'un dossier (R-01). Un dossier n'est « ancien » que si lui-même et tout son
    // contenu, sous-dossiers compris, le sont. Le parcours s'arrête au premier élément récent. Si le dossier ne peut
    // pas être lu entièrement (droits, erreur) ou dépasse maxEntries, l'ancienneté n'est pas confirmée : Unknown.
    // Une jonction ou un lien symbolique, à la racine ou dans le contenu, rend aussi l'ancienneté non confirmée : son
    // contenu est ailleurs (parfois actif), il peut boucler, et un lien relatif se casse si on le déplace.
    // limit : en temps universel de préférence (une heure locale est convertie).
    internal static Activity ActivityOf(string path, DateTime limit, int maxEntries)
    {
        if (limit.Kind != DateTimeKind.Utc) limit = limit.ToUniversalTime();
        try
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            FileSystemInfo root = dir.Exists ? (FileSystemInfo)dir : new FileInfo(path);
            if (!root.Exists || IsLink(root)) return Activity.Unknown;
            if (IsRecent(root, limit)) return Activity.Recent;
            if (!dir.Exists) return Activity.Old;
            int seen = 0;
            Stack<DirectoryInfo> pending = new Stack<DirectoryInfo>();
            pending.Push(dir);
            while (pending.Count > 0)
            {
                foreach (FileSystemInfo child in pending.Pop().EnumerateFileSystemInfos())
                {
                    if (++seen > maxEntries || IsLink(child)) return Activity.Unknown;
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
    static bool IsRecent(FileSystemInfo info, DateTime limitUtc)
    {
        return info.LastWriteTimeUtc >= limitUtc || info.CreationTimeUtc >= limitUtc;
    }

    static bool IsLink(FileSystemInfo info)
    {
        return (info.Attributes & FileAttributes.ReparsePoint) != 0 && Native.IsLink(info.FullName);
    }

    // Rangement automatique : chaque élément du bureau est évalué juste avant d'être déplacé
    public static int RunNow()
    {
        string[] entries;
        try { entries = Directory.GetFileSystemEntries(Desk); } catch { return 0; }
        return Run(new List<string>(entries));
    }

    // Range les éléments de la liste qui sont encore candidats. L'évaluation est refaite juste avant chaque
    // déplacement : l'élément a pu être utilisé, modifié ou remplacé depuis l'établissement de la liste (R-03).
    public static int Run(List<string> items)
    {
        string folder = Folder;
        DateTime limit = Limit;
        List<ArchiveRule> rules = Settings.ArchiveRules.ToList();
        Dictionary<string, bool> ready = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        int n = 0;
        List<string> log = new List<string>();
        bool journalChecked = false;
        foreach (string item in items)
        {
            // Seuls les éléments posés directement sur le bureau sont rangés, quelle que soit la liste reçue (QA34-02)
            string p;
            try { p = Path.GetFullPath(item); }
            catch (ArgumentException) { continue; }
            catch (NotSupportedException) { continue; }
            catch (PathTooLongException) { continue; }
            if (!Paths.SameFolder(Path.GetDirectoryName(p), Desk)) continue;
            // Une règle dont le filtre correspond décide seule : son âge et sa destination (D-009). Sinon,
            // le rangement par ancienneté vers le dossier d'archive.
            ArchiveRule rule = ArchiveRules.For(p, rules);
            string target = rule != null ? rule.Destination(folder) : folder;
            if (!CanArchiveTo(target, Desk) || !IsCandidate(p, rule != null ? rule.Limit : limit)) continue;
            bool ok;
            if (!ready.TryGetValue(target, out ok)) ready[target] = ok = PrepareFolder(target);
            if (!ok) continue;
            // Un fichier encore ouvert par un programme (enregistrement, téléchargement en cours) reste en place (QA39-01)
            if (File.Exists(p) && Native.FileInUse(p)) continue;
            // Avant le premier déplacement, on vérifie que les réglages s'enregistrent : sans journal durable,
            // un rangement ne pourrait plus être annulé après un redémarrage (QA39-05)
            if (!journalChecked)
            {
                if (!Settings.Save()) break;
                journalChecked = true;
            }
            try
            {
                string dest = Unique(Path.Combine(target, Path.GetFileName(p)));
                if (Directory.Exists(p)) Directory.Move(p, dest); else File.Move(p, dest);
                Settings.RenameKey(p, dest);
                log.Add(p + ">" + dest + ">" + Fingerprint(dest));
                n++;
            }
            catch { }
        }
        List<string> previous = Settings.ArchiveLog;
        if (log.Count > 0) Settings.ArchiveLog = log;
        Settings.LastArchive = DateTime.Now.Ticks;
        if (n == 0) { Settings.SaveQuiet(); return 0; }
        // Enregistrement immédiat : un arrêt brutal ne doit pas perdre le journal d'annulation. S'il échoue
        // (disque plein, fichier devenu trop grand), les éléments reviennent sur le bureau : aucun déplacement
        // annoncé sans annulation durable (QA42-08).
        if (Settings.Save()) return n;
        List<string> stuck = new List<string>();
        for (int i = log.Count - 1; i >= 0; i--)
        {
            string[] parts = log[i].Split('>');
            try
            {
                if (Directory.Exists(parts[1])) Directory.Move(parts[1], parts[0]); else File.Move(parts[1], parts[0]);
                Settings.RenameKey(parts[1], parts[0]);
                n--;
            }
            catch { stuck.Add(log[i]); }
        }
        // Ce qui n'a pas pu revenir garde son entrée, en mémoire au moins
        Settings.ArchiveLog = stuck.Count > 0 ? stuck : previous;
        Settings.SaveQuiet();
        return n;
    }

    static bool CanArchiveTo(string target, string desktop)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        try { return !Paths.SameFolder(Path.GetFullPath(target), desktop); }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (PathTooLongException) { return false; }
    }

    // Crée le dossier d'archive. Refusé si c'est une jonction ou un lien (les éléments partiraient ailleurs).
    // Un dossier OneDrive « à la demande » est aussi un point d'analyse, mais ce n'est pas un lien : il est accepté.
    static bool PrepareFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            return !IsLink(new DirectoryInfo(folder));
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
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
            // « source>destination>empreinte » ; les journaux d'avant QA39-02 n'ont pas d'empreinte
            string[] parts = e.Split('>');
            if (parts.Length < 2 || parts[0].Length == 0) continue;
            string src = parts[0], dest = parts[1], print = parts.Length > 2 ? parts[2] : null;
            try
            {
                src = Path.GetFullPath(src);
                dest = Path.GetFullPath(dest);
                string parent = Path.GetDirectoryName(dest);
                // Retour seulement vers le bureau, et seulement depuis un dossier où le rangement dépose (D-009)
                if (parent == null || !Paths.SameFolder(Path.GetDirectoryName(src), desktop) ||
                    !ArchiveRules.IsAllowedDestination(parent, desktop)) continue;
                // Dossier de destination absent (disque débranché, OneDrive pas encore synchronisé) : l'entrée est
                // gardée pour un nouvel essai (QA42-07)
                if (!Directory.Exists(parent)) { pending.Add(e); continue; }
                // Même règle qu'à l'aller (PrepareFolder) : seuls les liens et les jonctions sont refusés. Un dossier
                // OneDrive « à la demande » est un point d'analyse sans être un lien : il était refusé ici, et l'entrée
                // quittait le journal, ce qui rendait l'annulation impossible (CDX-26). Un lien ou un dossier illisible
                // garde l'entrée, pour un nouvel essai, sans rien déplacer.
                if (IsLink(new DirectoryInfo(parent))) { pending.Add(e); continue; }
                // Autre élément à la place de celui qui a été rangé (renommé puis remplacé) : on n'y touche pas.
                // L'empreinte est l'identité NTFS du fichier ; une identité illisible compte comme un conflit (QA39-02, QA42-06).
                if (print != null)
                {
                    if (print.Length == 0) continue;
                    string current = Fingerprint(dest);
                    if (current.Length == 0) { pending.Add(e); continue; }
                    if (print != current) continue;
                }
                if (File.Exists(src) || Directory.Exists(src)) { pending.Add(e); continue; }
                if (Directory.Exists(dest)) Directory.Move(dest, src);
                else if (File.Exists(dest)) File.Move(dest, src);
                else { pending.Add(e); continue; }
                Settings.RenameKey(dest, src);
                n++;
            }
            catch { pending.Add(e); }
        }
        journal.Clear();
        journal.AddRange(pending);
        return n;
    }

    // Identité du fichier ou du dossier sur son volume (numéro de série du volume et index NTFS), relevée après le
    // déplacement : deux éléments différents ne la partagent jamais, même avec la même taille et la même date (QA42-06).
    // Vide si l'élément n'existe pas ou ne peut pas être ouvert.
    internal static string Fingerprint(string path)
    {
        string id = Native.FileId(path);
        return id == null ? "" : "i" + id;
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
