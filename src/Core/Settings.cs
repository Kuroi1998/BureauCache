// BureauCache — CustomCat, Settings
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// =====================================================================
//  Réglages (%APPDATA%\BureauCache\settings.ini)
// =====================================================================
class CustomCat
{
    public string Name, Glyph;
    public Color Color;
}

static partial class Settings
{
    public static bool HideIcons = true, RightSide = false, AutoClose = true, ShowSystem = true, ShowRecents = true,
        GroupCats = true, GridView = false, Glass = true, LiveBlur = true, Animations = true, NoFullscreen = true, HotkeyOn = true,
        SearchApps = true, SearchRecent = true, SearchSettings = true, SearchCommands = true, SearchContent = true,
        NewBadge = true, ArchiveOn = false, HoverPreview = true, Widgets = true, AutoUpdate = true, Pinned = false, QuietStart = false, TrayHintShown = false;
    public static bool HidePanelInCapture;
    public static string LastVersion = "";
    public static List<string> ArchiveLog = new List<string>();
    public static int ThemeMode = 0, Language = 0, Delay = 120, PanelWidth = 340, EdgeZone = 0, ScreenIdx = -1,
        HotMods = 3, HotKey = (int)Keys.B, WebEngine = 0, NewDays = 3, ArchiveDays = 60;
    public static long LastUpdateCheck, LastArchive;
    // « Effacer l'historique des récents » : les dates d'usage restent (elles protègent du rangement automatique),
    // seuls les éléments utilisés depuis ce moment s'affichent dans « Récents » (QA-30)
    public static long RecentsSince;
    public static string ArchiveName = "Archive du bureau", UpdateRepo = "";
    // Écran choisi, reconnu par son nom de périphérique : un indice seul change de sens quand on débranche
    // ou réordonne les écrans (R-10)
    public static string ScreenName = "";
    // Météo (D-005) : désactivée tant qu'aucun lieu n'est choisi
    public static bool WeatherOn = false;
    public static bool ClipboardOn = false;   // historique du presse-papiers, en mémoire seulement (D-011)
    public static bool TaskbarOn = false;     // barre BureauCache à la place de celle de Windows (D-013)
    public static List<string> TaskbarPins = new List<string>();   // applications épinglées, « clé>nom » (D-014)
    // Personnalisation de la barre (T-043, D-019) : taille 0 petite, 1 normale, 2 grande ; icônes centrées ; titres des
    // fenêtres à côté des icônes ; éléments affichés à droite ; date et secondes de l'horloge
    public static int TaskbarSize = 1;
    public static bool TaskbarCenter = false, TaskbarLabels = false, TaskbarQuick = true, TaskbarBell = true,
        TaskbarDate = true, TaskbarSeconds = false;
    // Étape 2 : fond 0 uni, 1 verre (fond d'écran flouté), 2 teinté de la couleur d'accent ; ligne du haut ; masquage
    // automatique ; fenêtres d'une même application regroupées ; clic du milieu qui ferme ; molette sur le volume
    public static int TaskbarStyle = 0;
    public static bool TaskbarLine = true, TaskbarAutoHide = false, TaskbarGroup = false, TaskbarMiddleClose = false, TaskbarWheel = true;
    // Menu Démarrer à la carte (T-044) : parties affichées, tuiles masquées (par identifiant), raccourcis personnels
    public static bool StartSearch = true, StartAccount = true, StartBureauCache = true, StartWindows = true, StartSession = true, StartPower = true;
    public static List<string> StartHidden = new List<string>(), StartShortcuts = new List<string>();
    // Thèmes (T-045) : palette des fonds, accent repris de Windows, taille de l'interface en % (au prochain démarrage)
    public static int Palette = 0, InterfaceZoom = 100;
    public static bool AccentFromWindows = false;
    static Color windowsAccent = Color.Empty;
    public static readonly int[] Zooms = { 90, 100, 115, 130 };
    // Accent réellement utilisé : celui de Windows s'il est suivi et lisible, sinon celui choisi
    public static Color EffectiveAccent { get { return AccentFromWindows && !windowsAccent.IsEmpty ? windowsAccent : Accent; } }
    internal static void SetWindowsAccentForTests(Color c) { windowsAccent = c; }
    public static string WeatherPlace = "", WeatherShort = "";   // libellé complet et « Ville, Pays » (QA52-05)
    public static double WeatherLat = double.NaN, WeatherLon = double.NaN;
    public static Color Accent = Color.FromArgb(59, 130, 246);
    public static List<string> Favorites = new List<string>(), Order = new List<string>();
    public static HashSet<string> Collapsed = new HashSet<string>();
    public static Dictionary<string, string> CatOverride = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, long[]> Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
    public static List<CustomCat> CustomCats = new List<CustomCat>();
    public static List<ArchiveRule> ArchiveRules = new List<ArchiveRule>();   // règles de rangement (D-009)
    public static bool FirstRun;
    public static event Action Changed;

    static bool sysDark = true;
    public static bool Dark { get { return ThemeMode == 1 || (ThemeMode == 0 && sysDark); } }
    public static bool SystemDark { get { return sysDark; } }

    // Dossier de remplacement, posé par les tests : ils ne doivent jamais écrire dans les vrais réglages (QA-32)
    internal static string DirOverride;
    public static string Dir { get { return DirOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BureauCache"); } }
    public static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }
    public const long MaxFileSize = 2 * 1024 * 1024;
    // Historique d'usage borné : au-delà, les entrées les plus anciennes sont oubliées, pour que le fichier
    // écrit reste toujours relisible sous MaxFileSize (R-14). 2 000 chemins de 260 caractères font ~550 Ko.
    public const int MaxUsage = 2000;

    // Relit l'accent de Windows ; vrai s'il a changé
    public static bool RefreshWindowsAccent()
    {
        Color? read = Palettes.ReadWindowsAccent();
        Color now = read.HasValue ? read.Value : Color.Empty;
        bool changed = now.ToArgb() != windowsAccent.ToArgb();
        windowsAccent = now;
        return changed;
    }

    public static bool RefreshSystemTheme()
    {
        bool old = sysDark;
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                sysDark = !(v is int && (int)v == 1);
            }
        }
        catch { }
        return old != sysDark;
    }

    static List<string> SplitList(string v) { return v.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList(); }
    static double Coord(string v, double limit)
    {
        double d;
        return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && Math.Abs(d) <= limit ? d : double.NaN;
    }
    static Color ParseColor(string v, Color def)
    {
        int n;
        if (int.TryParse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n))
            return Color.FromArgb(255, (n >> 16) & 255, (n >> 8) & 255, n & 255);
        return def;
    }
    static string Hex(Color c) { return (c.ToArgb() & 0xFFFFFF).ToString("X6"); }


    public static readonly int[] NewDaysChoices = { 1, 3, 7 };
    public static readonly int[] ArchiveDaysChoices = { 30, 60, 90, 180 };

    // Indice dans « all » de l'écran choisi, ou -1 pour tous les écrans. Un écran choisi mais débranché
    // rend le bord actif partout plutôt que sur l'écran qui a pris son indice (R-10).
    public static int ChosenScreen(Screen[] all)
    {
        if (ScreenIdx < 0) return -1;
        if (!string.IsNullOrEmpty(ScreenName))
        {
            for (int i = 0; i < all.Length; i++) if (all[i].DeviceName == ScreenName) return i;
            return -1;
        }
        return ScreenIdx < all.Length ? ScreenIdx : -1;
    }

    internal static int RoundUp(int[] choices, int value)
    {
        foreach (int c in choices) if (c >= value) return c;
        return choices[choices.Length - 1];
    }

    static string B(bool b) { return b ? "1" : "0"; }


    // Copie de l'organisation, pour pouvoir annuler une action
    public class Snap
    {
        public List<string> Fav, Order;
        public Dictionary<string, string> Cat;
        public Dictionary<string, long[]> Usage;
    }

    public static Snap Snapshot()
    {
        return new Snap
        {
            Fav = new List<string>(Favorites), Order = new List<string>(Order),
            Cat = new Dictionary<string, string>(CatOverride, StringComparer.OrdinalIgnoreCase),
            Usage = new Dictionary<string, long[]>(Usage, StringComparer.OrdinalIgnoreCase)
        };
    }

    public static void Restore(Snap s)
    {
        // Un usage enregistré depuis l'instantané (fichier rouvert entre-temps) est gardé : il protège le fichier du
        // rangement automatique, et annuler un changement d'organisation ne doit pas le retirer (Q30-07)
        Dictionary<string, long[]> restored = new Dictionary<string, long[]>(s.Usage, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, long[]> kv in Usage)
        {
            long[] old;
            if (restored.TryGetValue(kv.Key, out old) && kv.Value[1] > old[1]) restored[kv.Key] = kv.Value;
        }
        Favorites = s.Fav; Order = s.Order; CatOverride = s.Cat; Usage = restored;
        SaveQuiet();
    }

    public static void Notify() { SaveSoon(); Fire(); }
    public static void SaveQuiet() { SaveSoon(); }
    public static void Fire() { if (Changed != null) Changed(); }

    public static int FavIndex(string key)
    {
        return Favorites.FindIndex(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
    }

    public static CustomCat FindCat(string name)
    {
        return CustomCats.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    // Efface l'historique visible sans retirer la protection des fichiers utilisés récemment
    public static void ClearRecents()
    {
        // Nouveaux tableaux : les instantanés d'annulation (Snapshot) partagent les anciens
        foreach (string k in Usage.Keys.ToList()) Usage[k] = new long[] { 0, Usage[k][1] };
        RecentsSince = DateTime.Now.Ticks;
    }

    public static void RecordUse(string key)
    {
        long[] u;
        if (!Usage.TryGetValue(key, out u)) u = new long[] { 0, 0 };
        long count = Math.Max(0, u[0]);
        Usage[key] = new long[] { count == long.MaxValue ? count : count + 1, DateTime.Now.Ticks };
        PruneUsage();
        SaveQuiet();
    }

    // Ramène l'historique vers MaxUsage entrées en oubliant les plus anciennes. Une entrée de moins de
    // 180 jours (la plus longue durée de rangement proposée) est gardée : elle protège encore un élément
    // du rangement automatique (Q24-02). La taille du fichier reste bornée par Serialize.
    internal static void PruneUsage()
    {
        if (Usage.Count <= MaxUsage) return;
        long protect = DateTime.Now.AddDays(-ArchiveDaysChoices[ArchiveDaysChoices.Length - 1]).Ticks;
        foreach (string k in Usage.Where(kv => kv.Value[1] < protect).OrderBy(kv => kv.Value[1])
                                  .Take(Usage.Count - MaxUsage).Select(kv => kv.Key).ToList())
            Usage.Remove(k);
    }

    public static void RenameKey(string o, string n)
    {
        for (int i = 0; i < Favorites.Count; i++) if (string.Equals(Favorites[i], o, StringComparison.OrdinalIgnoreCase)) Favorites[i] = n;
        for (int i = 0; i < Order.Count; i++) if (string.Equals(Order[i], o, StringComparison.OrdinalIgnoreCase)) Order[i] = n;
        string c;
        if (CatOverride.TryGetValue(o, out c)) { CatOverride.Remove(o); CatOverride[n] = c; }
        long[] u;
        if (Usage.TryGetValue(o, out u)) { Usage.Remove(o); Usage[n] = u; }
        SaveQuiet();
    }

    public static void RemoveKey(string k)
    {
        Favorites.RemoveAll(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase));
        Order.RemoveAll(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase));
        CatOverride.Remove(k);
        Usage.Remove(k);
        SaveQuiet();
    }

    public static void RenameCategory(CustomCat c, string newName)
    {
        foreach (string k in CatOverride.Keys.ToList())
            if (string.Equals(CatOverride[k], c.Name, StringComparison.OrdinalIgnoreCase)) CatOverride[k] = newName;
        if (Collapsed.Remove("cat:" + c.Name)) Collapsed.Add("cat:" + newName);
        c.Name = newName;
    }

    public static void DeleteCategory(CustomCat c)
    {
        foreach (string k in CatOverride.Keys.ToList())
            if (string.Equals(CatOverride[k], c.Name, StringComparison.OrdinalIgnoreCase)) CatOverride.Remove(k);
        Collapsed.Remove("cat:" + c.Name);
        CustomCats.Remove(c);
    }
}
