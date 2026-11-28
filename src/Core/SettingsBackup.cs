// BureauCache — SettingsBackup
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

// =====================================================================
//  Sauvegardes automatiques des réglages (D-007)
// =====================================================================
// Une copie par jour de settings.ini, prise avant sa première réécriture de la journée, dans
// <dossier des réglages>\sauvegardes. On garde les 7 copies lisibles les plus récentes. Restaurer ou importer est
// transactionnel : l'état en mémoire est mis de côté, la source est chargée puis enregistrée, et le point « avant
// restauration » n'est remplacé qu'à la fin ; au moindre échec, mémoire et disque reviennent à l'état initial.
// La copie quotidienne, elle, ne doit jamais empêcher un enregistrement : ses erreurs sont ignorées.
static class SettingsBackup
{
    public const int Kept = 7;
    const string DailyPrefix = "settings-";
    const string BeforeRestoreName = "settings-avant-restauration.ini";
    static readonly object gate = new object();   // copies et restaurations de ce processus, une à la fois

    public static string Folder { get { return Path.Combine(Settings.Dir, "sauvegardes"); } }
    static string BeforeRestorePath { get { return Path.Combine(Folder, BeforeRestoreName); } }
    static string TempPath { get { return Path.Combine(Folder, Guid.NewGuid().ToString("N") + ".tmp"); } }

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
        bool unavailable;
        return IsValid(path, out unavailable);
    }

    static bool IsValid(string path, out bool unavailable)
    {
        unavailable = false;
        try
        {
            FileInfo f = new FileInfo(path);
            if (f.Length > Settings.MaxFileSize) return false;
            return File.ReadLines(path).Any(line => line.Trim() == "HideIcons=0" || line.Trim() == "HideIcons=1");
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch { unavailable = true; return false; }
    }

    // ---------- Copie quotidienne ----------

    // Appelé par Settings.Save juste avant de remplacer settings.ini. Une copie du jour absente ou illisible est
    // (re)faite à partir d'un fichier temporaire vérifié (D007-01) ; la rotation est relancée à chaque fois, pour
    // rattraper une suppression empêchée par un verrou (D007-04).
    public static void BeforeSave()
    {
        lock (gate)
        {
            try
            {
                string today = DailyPath(DateTime.Now);
                bool unavailable;
                if (!IsValid(today, out unavailable) && !unavailable && IsValid(Settings.FilePath))
                {
                    Directory.CreateDirectory(Folder);
                    string tmp = TempPath;
                    try
                    {
                        FileOps.CopyFile(Settings.FilePath, tmp);
                        if (IsValid(tmp))
                        {
                            if (File.Exists(today)) File.Copy(tmp, today, true);   // copie du jour illisible
                            else File.Move(tmp, today);
                        }
                    }
                    finally { try { File.Delete(tmp); } catch { } }
                }
                Prune(today);
            }
            catch { }
        }
    }

    static string DailyPath(DateTime day)
    {
        return Path.Combine(Folder, DailyPrefix + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".ini");
    }

    static bool TryDailyDate(string path, out DateTime date)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        date = DateTime.MinValue;
        return string.Equals(Path.GetExtension(path), ".ini", StringComparison.OrdinalIgnoreCase) &&   // pas « .inix » (D007-09)
               name.StartsWith(DailyPrefix, StringComparison.Ordinal) &&
               DateTime.TryParseExact(name.Substring(DailyPrefix.Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    // Garde les Kept copies quotidiennes lisibles les plus récentes ; une copie illisible ne prend pas de place
    // (D007-03). « protect », la copie que l'on vient de faire, n'est jamais supprimée, même si l'horloge a reculé
    // et que des copies portent une date future (D007-02). Une copie temporairement inaccessible reste en place.
    internal static void Prune(string protect = null)
    {
        List<Entry> all = Daily();
        List<Entry> valid = new List<Entry>();
        HashSet<Entry> unavailable = new HashSet<Entry>();
        foreach (Entry e in all)
        {
            bool inaccessible;
            if (IsValid(e.Path, out inaccessible)) valid.Add(e);
            else if (inaccessible) unavailable.Add(e);
        }
        List<Entry> keep = valid.Take(Kept).ToList();
        Entry mine = protect == null ? null : valid.FirstOrDefault(e => string.Equals(e.Path, protect, StringComparison.OrdinalIgnoreCase));
        if (mine != null && !keep.Contains(mine)) { keep.RemoveAt(keep.Count - 1); keep.Add(mine); }
        DateTime oldestKept = keep.Count == 0 ? DateTime.MaxValue : keep.Min(e => e.Date);
        foreach (Entry e in all)
        {
            if (keep.Contains(e) || unavailable.Contains(e)) continue;
            bool readable = valid.Contains(e);
            if (readable || e.Date < oldestKept)
                try { File.Delete(e.Path); } catch { }
        }
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

    // ---------- Liste ----------

    // Sauvegardes valides, la plus récente d'abord ; « avant restauration » en tête quand elle existe
    public static List<Entry> List()
    {
        List<Entry> list = new List<Entry>();
        try
        {
            for (int i = 1; i <= UndoKept; i++)
                if (File.Exists(UndoPath(i)))
                    list.Add(new Entry { Path = UndoPath(i), Date = File.GetLastWriteTime(UndoPath(i)), BeforeRestore = true });
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

    // ---------- Restauration et import ----------

    public static bool Restore(Entry e)
    {
        return e != null && RestoreFrom(e.Path);
    }

    // Remplace les réglages par ceux du fichier, de façon transactionnelle (D007-05, D007-07, D007-10). Faux, sans
    // rien changer, si le fichier est illisible ou si une étape échoue.
    public static bool RestoreFrom(string path)
    {
        return RestoreFrom(path, null);
    }

    // Import d'un fichier venu d'ailleurs : comme une restauration, mais les choix qui touchent à la sécurité ou aux
    // fichiers restent ceux de ce PC (AUD-01, AUD-02, SEC-04). kept reçoit le nom de ceux que le fichier voulait changer.
    public static bool ImportFrom(string path, List<string> kept)
    {
        return RestoreFrom(path, kept ?? new List<string>());
    }

    // Réglages sensibles d'un import : dépôt et mises à jour automatiques (un dépôt tiers n'exige pas de signature),
    // presse-papiers, barre des tâches (D-013), rangement automatique et journal d'annulation (il désigne des fichiers à déplacer)
    sealed class LocalChoices
    {
        string repo, archiveName; bool autoUpdate, clipboard, archive, taskbar; List<string> pins; int archiveDays; List<string> log; List<ArchiveRule> rules;
        List<string> favorites, shortcuts; Dictionary<string, long[]> usage;

        public static LocalChoices Capture()
        {
            return new LocalChoices { repo = Settings.UpdateRepo, autoUpdate = Settings.AutoUpdate, clipboard = Settings.ClipboardOn, taskbar = Settings.TaskbarOn, pins = Settings.TaskbarPins.ToList(),
                                      archive = Settings.ArchiveOn, log = Settings.ArchiveLog.ToList(), rules = Settings.ArchiveRules.ToList(),
                                      archiveName = Settings.ArchiveName, archiveDays = Settings.ArchiveDays,
                                      favorites = Settings.Favorites.ToList(),
                                      shortcuts = Settings.StartShortcuts.ToList(),
                                      usage = Settings.Usage.ToDictionary(e => e.Key, e => (long[])e.Value.Clone(), StringComparer.OrdinalIgnoreCase) };
        }

        static string Sig(List<ArchiveRule> r) { return string.Join("|", r.Select(x => x.Serialize()).ToArray()); }

        public void Reapply(List<string> kept)
        {
            if (!string.Equals((Settings.UpdateRepo ?? "").Trim(), (repo ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                kept.Add(L.T("le dépôt des mises à jour", "the update repository", "el repositorio de actualizaciones"));
            if (Settings.AutoUpdate != autoUpdate) kept.Add(L.T("la vérification automatique des mises à jour", "automatic update checks", "la comprobación automática de actualizaciones"));
            if (Settings.ClipboardOn != clipboard) kept.Add(L.T("l'historique du presse-papiers", "the clipboard history", "el historial del portapapeles"));
            if (!Settings.TaskbarPins.SequenceEqual(pins)) kept.Add(L.T("les applications épinglées à la barre", "the apps pinned to the taskbar", "las aplicaciones ancladas a la barra"));
            if (!Settings.StartShortcuts.SequenceEqual(shortcuts)) kept.Add(L.T("les raccourcis personnels du menu Démarrer", "the personal Start menu shortcuts", "los accesos directos personales del menú Inicio"));
            if (Settings.TaskbarOn != taskbar) kept.Add(L.T("la barre des tâches BureauCache", "the BureauCache taskbar", "la barra de tareas de BureauCache"));
            if (Settings.ArchiveOn != archive) kept.Add(L.T("le rangement automatique", "automatic tidy-up", "la organización automática"));
            if (Settings.ArchiveLog.Count > 0) kept.Add(L.T("le journal d'annulation du rangement", "the tidy-up undo log", "el registro para deshacer la organización"));
            // Rangement automatique actif sur ce PC : ses règles, son dossier et son ancienneté restent les siens, sinon
            // l'import changerait quels fichiers partent et où (QA52-01). Rangement inactif : les règles importées
            // ne s'exécutent qu'avec « Ranger maintenant », qui liste les fichiers et demande confirmation.
            bool keepPolicy = archive;
            if (keepPolicy && (Sig(Settings.ArchiveRules) != Sig(rules) || Settings.ArchiveName != archiveName || Settings.ArchiveDays != archiveDays))
                kept.Add(L.T("les règles et le dossier du rangement automatique", "the automatic tidy-up rules and folder", "las reglas y la carpeta de la organización automática"));
            Settings.UpdateRepo = repo; Settings.AutoUpdate = autoUpdate; Settings.ClipboardOn = clipboard; Settings.TaskbarOn = taskbar; Settings.TaskbarPins = pins;
            Settings.ArchiveOn = archive; Settings.ArchiveLog = log;
            Settings.StartShortcuts = shortcuts;
            if (keepPolicy) { Settings.ArchiveRules = rules; Settings.ArchiveName = archiveName; Settings.ArchiveDays = archiveDays; }
            // Rangement actif : les favoris et les dates d'usage de ce PC protègent des fichiers du rangement. Un import
            // les remplaçait, et des fichiers protégés pouvaient partir au passage suivant (CDX-25) : on fusionne.
            if (keepPolicy && MergeProtections(favorites, usage))
                kept.Add(L.T("les favoris et les dates d'utilisation qui protègent tes fichiers du rangement",
                             "the favorites and usage dates that protect your files from tidy-up",
                             "los favoritos y las fechas de uso que protegen tus archivos de la organización"));
        }

        // Vrai si une protection locale manquait dans l'import. Favoris : ceux de l'import, puis les locaux absents.
        // Usage : pour chaque élément, la date la plus récente et le plus grand nombre d'ouvertures.
        internal static bool MergeProtections(List<string> localFavorites, Dictionary<string, long[]> localUsage)
        {
            bool added = false;
            foreach (string f in localFavorites)
                if (Settings.FavIndex(f) < 0) { Settings.Favorites.Add(f); added = true; }
            foreach (KeyValuePair<string, long[]> e in localUsage)
            {
                long[] imported;
                if (!Settings.Usage.TryGetValue(e.Key, out imported)) { Settings.Usage[e.Key] = (long[])e.Value.Clone(); added = true; continue; }
                if (e.Value[1] > imported[1]) { imported[1] = e.Value[1]; added = true; }
                if (e.Value[0] > imported[0]) imported[0] = e.Value[0];
            }
            return added;
        }
    }

    static bool SameCoordinate(double a, double b) { return (double.IsNaN(a) && double.IsNaN(b)) || a == b; }

    static bool RestoreFrom(string path, List<string> kept)
    {
        lock (gate)
        {
            if (!IsValid(path)) return false;
            bool profileWasUnreadable = Settings.ProfileUnreadable, restored = false;
            string source = null, undo = null;
            try
            {
                Directory.CreateDirectory(Folder);
                // Copie de la source d'abord : « avant restauration » peut être elle-même la source
                source = TempPath;
                FileOps.CopyFile(path, source);
                if (!IsValid(source)) return false;
                // L'état en mémoire (pas seulement le disque) : c'est lui qui servira à revenir en arrière
                undo = TempPath;
                File.WriteAllLines(undo, Settings.Serialize(), new UTF8Encoding(false));
                if (!IsValid(undo)) return false;

                LocalChoices local = kept != null ? LocalChoices.Capture() : null;
                bool weatherOn = Settings.WeatherOn;
                double weatherLat = Settings.WeatherLat, weatherLon = Settings.WeatherLon;
                bool loaded = LoadReplacing(source);
                if (loaded && local != null) local.Reapply(kept);
                // Autorisation d'écrire pour cet essai uniquement. Un échec rétablit le verrou du profil.
                if (loaded) Settings.ProfileUnreadable = false;
                if (!loaded || !Settings.Save())
                {
                    LoadReplacing(undo);               // le disque n'a pas changé : la mémoire seulement
                    return false;
                }
                try { KeepUndo(undo); }
                catch
                {
                    // Point de retour impossible à écrire : on ne garde pas une restauration sans retour possible.
                    // Si le retour arrière ne s'enregistre pas non plus, l'état d'avant est gardé à part (QA34-05).
                    if (!LoadReplacing(undo) || !Settings.Save())
                        try { File.Copy(undo, Path.Combine(Folder, "settings-avant-restauration-secours.ini"), true); }
                        catch (Exception ex) { Log.Warn("sauvegarde.reglages", ex); }
                    return false;
                }
                // Une autre prévision est nécessaire si la restauration a changé le lieu ou désactivé la météo.
                if (Settings.WeatherOn != weatherOn || !SameCoordinate(Settings.WeatherLat, weatherLat) || !SameCoordinate(Settings.WeatherLon, weatherLon))
                    Weather.Reset();
                restored = true;
                return true;
            }
            catch (Exception ex) { Log.Warn("sauvegarde.reglages", ex); return false; }
            finally
            {
                if (!restored) Settings.ProfileUnreadable = profileWasUnreadable;
                if (source != null) try { File.Delete(source); } catch { }
                if (undo != null) try { File.Delete(undo); } catch { }
            }
        }
    }

    // Trois points de retour : importer puis restaurer ne fait plus disparaître l'état d'origine (QA34-05)
    const int UndoKept = 3;

    static string UndoPath(int i)
    {
        return i <= 1 ? BeforeRestorePath : Path.Combine(Folder, "settings-avant-restauration-" + i + ".ini");
    }

    static void KeepUndo(string undo)
    {
        for (int i = UndoKept - 1; i >= 1; i--)
            if (File.Exists(UndoPath(i))) File.Copy(UndoPath(i), UndoPath(i + 1), true);
        File.Copy(undo, BeforeRestorePath, true);
    }

    // Charge un fichier de réglages complet : les listes absentes d'une ancienne sauvegarde repartent vides au
    // lieu de garder celles du profil courant (D007-06). Les réglages simples absents gardent leur valeur.
    static bool LoadReplacing(string path)
    {
        if (!IsValid(path)) return false;
        Settings.Favorites = new List<string>();
        Settings.Order = new List<string>();
        Settings.Collapsed = new HashSet<string>();
        Settings.CatOverride = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
        Settings.CustomCats = new List<CustomCat>();
        Settings.ArchiveLog = new List<string>();
        Settings.ArchiveRules = new List<ArchiveRule>();
        Settings.TaskbarPins = new List<string>();
        Settings.StartHidden = new List<string>();
        Settings.StartShortcuts = new List<string>();
        return Settings.Load(path);
    }

    // ---------- Désinstallation ----------

    public static void DeleteAll()
    {
        try { if (Directory.Exists(Folder)) Directory.Delete(Folder, true); } catch { }
    }
}
