// BureauCache — SettingsBackup
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// =====================================================================
//  Sauvegardes automatiques des réglages (D-007)
// =====================================================================
// Une copie par jour de settings.ini, prise avant sa première réécriture de la journée, dans
// <dossier des réglages>\sauvegardes. On garde les 7 plus récentes. Restaurer ou importer copie d'abord l'état
// actuel dans un emplacement « avant restauration », lui-même restaurable. Rien ici ne doit empêcher un
// enregistrement : chaque opération sur les copies est protégée.
static class SettingsBackup
{
    public const int Kept = 7;
    const string DailyPrefix = "settings-";
    const string BeforeRestoreName = "settings-avant-restauration.ini";

    public static string Folder { get { return Path.Combine(Settings.Dir, "sauvegardes"); } }
    static string BeforeRestorePath { get { return Path.Combine(Folder, BeforeRestoreName); } }

    public sealed class Entry
    {
        public string Path;
        public DateTime Date;          // jour de la copie, ou date du fichier pour « avant restauration »
        public bool BeforeRestore;
        public int Favorites, Categories;
        public long Size;
    }

    // Même critère que Settings.Load : un fichier que Load refuserait ne vaut pas la peine d'être gardé
    public static bool IsValid(string path)
    {
        try
        {
            FileInfo f = new FileInfo(path);
            if (!f.Exists || f.Length > Settings.MaxFileSize) return false;
            return File.ReadLines(path).Any(line => line.Trim() == "HideIcons=0" || line.Trim() == "HideIcons=1");
        }
        catch { return false; }
    }

    // Appelé par Settings.Save juste avant de remplacer settings.ini
    public static void BeforeSave()
    {
        try
        {
            string current = Settings.FilePath;
            string today = DailyPath(DateTime.Now);
            if (File.Exists(today) || !IsValid(current)) return;
            Directory.CreateDirectory(Folder);
            FileOps.CopyFile(current, today);
            Prune();
        }
        catch { }
    }

    // Avant une restauration ou un import : l'état actuel reste récupérable
    public static void KeepCurrentBeforeRestore()
    {
        try
        {
            Settings.Flush();
            if (!IsValid(Settings.FilePath)) return;
            Directory.CreateDirectory(Folder);
            FileOps.CopyFile(Settings.FilePath, BeforeRestorePath);
        }
        catch { }
    }

    static string DailyPath(DateTime day)
    {
        return Path.Combine(Folder, DailyPrefix + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".ini");
    }

    static bool TryDailyDate(string path, out DateTime date)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        date = DateTime.MinValue;
        return name.StartsWith(DailyPrefix, StringComparison.Ordinal) &&
               DateTime.TryParseExact(name.Substring(DailyPrefix.Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    // Garde les Kept copies quotidiennes les plus récentes
    internal static void Prune()
    {
        foreach (Entry old in Daily().Skip(Kept))
            try { File.Delete(old.Path); } catch { }
    }

    static List<Entry> Daily()
    {
        List<Entry> list = new List<Entry>();
        if (!Directory.Exists(Folder)) return list;
        foreach (string p in Directory.GetFiles(Folder, DailyPrefix + "*.ini"))
        {
            DateTime d;
            if (TryDailyDate(p, out d)) list.Add(new Entry { Path = p, Date = d });
        }
        return list.OrderByDescending(e => e.Date).ToList();
    }

    // Sauvegardes valides, la plus récente d'abord ; « avant restauration » en tête quand elle existe
    public static List<Entry> List()
    {
        List<Entry> list = new List<Entry>();
        try
        {
            if (File.Exists(BeforeRestorePath))
                list.Add(new Entry { Path = BeforeRestorePath, Date = File.GetLastWriteTime(BeforeRestorePath), BeforeRestore = true });
            list.AddRange(Daily());
        }
        catch { }
        List<Entry> valid = new List<Entry>();
        foreach (Entry e in list)
        {
            if (!IsValid(e.Path)) continue;
            Describe(e);
            valid.Add(e);
        }
        return valid;
    }

    // Quelques repères pour reconnaître une sauvegarde : nombre de favoris et de catégories personnalisées
    static void Describe(Entry e)
    {
        try
        {
            e.Size = new FileInfo(e.Path).Length;
            foreach (string raw in File.ReadLines(e.Path))
            {
                string line = raw.Trim();
                if (line.StartsWith("Favorites=", StringComparison.Ordinal)) e.Favorites = Count(line.Substring(10));
                else if (line.StartsWith("CustomCats=", StringComparison.Ordinal)) e.Categories = Count(line.Substring(11));
            }
        }
        catch { }
    }

    static int Count(string list)
    {
        return list.Length == 0 ? 0 : list.Split('|').Count(s => s.Length > 0);
    }

    // Remplace les réglages en mémoire par ceux de la sauvegarde, puis les enregistre. Faux si elle est illisible.
    public static bool Restore(Entry e)
    {
        if (e == null || !IsValid(e.Path)) return false;
        // Copie de la sauvegarde choisie d'abord : « avant restauration » peut être elle-même la source
        string source = Path.Combine(Folder, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            FileOps.CopyFile(e.Path, source);
            KeepCurrentBeforeRestore();
            if (!Settings.Load(source)) return false;
            Settings.Save();
            return true;
        }
        catch { return false; }
        finally { try { File.Delete(source); } catch { } }
    }

    // Désinstallation : le dossier part avec les réglages
    public static void DeleteAll()
    {
        try { if (Directory.Exists(Folder)) Directory.Delete(Folder, true); } catch { }
    }
}
