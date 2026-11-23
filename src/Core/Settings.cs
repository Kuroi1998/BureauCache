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

static class Settings
{
    public static bool HideIcons = true, RightSide = false, AutoClose = true, ShowSystem = true, ShowRecents = true,
        GroupCats = true, GridView = false, Glass = true, LiveBlur = true, Animations = true, NoFullscreen = true, HotkeyOn = true,
        SearchApps = true, SearchRecent = true, SearchSettings = true, SearchCommands = true,
        NewBadge = true, ArchiveOn = false, HoverPreview = true, Widgets = true, AutoUpdate = true, Pinned = false, QuietStart = false, TrayHintShown = false;
    public static bool HidePanelInCapture;
    public static string LastVersion = "";
    public static List<string> ArchiveLog = new List<string>();
    public static int ThemeMode = 0, Language = 0, Delay = 120, PanelWidth = 340, EdgeZone = 0, ScreenIdx = -1,
        HotMods = 3, HotKey = (int)Keys.B, WebEngine = 0, NewDays = 3, ArchiveDays = 60;
    public static long LastUpdateCheck, LastArchive;
    public static string ArchiveName = "Archive du bureau", UpdateRepo = "";
    // Écran choisi, reconnu par son nom de périphérique : un indice seul change de sens quand on débranche
    // ou réordonne les écrans (R-10)
    public static string ScreenName = "";
    // Météo (D-005) : désactivée tant qu'aucun lieu n'est choisi
    public static bool WeatherOn = false;
    public static string WeatherPlace = "";
    public static double WeatherLat = double.NaN, WeatherLon = double.NaN;
    public static Color Accent = Color.FromArgb(59, 130, 246);
    public static List<string> Favorites = new List<string>(), Order = new List<string>();
    public static HashSet<string> Collapsed = new HashSet<string>();
    public static Dictionary<string, string> CatOverride = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, long[]> Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
    public static List<CustomCat> CustomCats = new List<CustomCat>();
    public static bool FirstRun;
    public static event Action Changed;

    static bool sysDark = true;
    public static bool Dark { get { return ThemeMode == 1 || (ThemeMode == 0 && sysDark); } }

    public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BureauCache"); } }
    public static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }
    public const long MaxFileSize = 2 * 1024 * 1024;
    // Historique d'usage borné : au-delà, les entrées les plus anciennes sont oubliées, pour que le fichier
    // écrit reste toujours relisible sous MaxFileSize (R-14). 2 000 chemins de 260 caractères font ~550 Ko.
    public const int MaxUsage = 2000;

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

    public static void Load() { Load(FilePath); }

    public static bool Load(string path)
    {
        RefreshSystemTheme();
        if (!File.Exists(path)) { FirstRun = true; return false; }
        bool hasThemeMode = false;
        string oldDark = null;
        bool hidePanelInCapture = false;
        try
        {
            string[] lines;
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length > MaxFileSize) return false;
                using (var reader = new StreamReader(stream))
                    lines = reader.ReadToEnd().Split('\n');
            }
            if (!lines.Any(line => line.Trim() == "HideIcons=0" || line.Trim() == "HideIcons=1")) return false;
            ScreenName = "";   // un INI d'avant 3.8 n'a pas ce nom : celui de la configuration précédente ne doit pas rester (Q26-05)
            foreach (string line in lines)
            {
                int i = line.IndexOf('=');
                if (i <= 0) continue;
                string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                int n;
                long ln;
                bool b = v == "1";
                switch (k)
                {
                    case "HideIcons": HideIcons = b; break;
                    case "RightSide": RightSide = b; break;
                    case "AutoClose": AutoClose = b; break;
                    case "ShowSystem": ShowSystem = b; break;
                    case "ShowRecents": ShowRecents = b; break;
                    case "GroupCats": GroupCats = b; break;
                    case "GridView": GridView = b; break;
                    case "Glass": Glass = b; break;
                    case "LiveBlur": LiveBlur = b; break;
                    case "HidePanelInCapture": hidePanelInCapture = b; break;
                    case "Animations": Animations = b; break;
                    case "NoFullscreen": NoFullscreen = b; break;
                    case "HotkeyOn": HotkeyOn = b; break;
                    case "SearchApps": SearchApps = b; break;
                    case "SearchRecent": SearchRecent = b; break;
                    case "SearchSettings": SearchSettings = b; break;
                    case "SearchCommands": SearchCommands = b; break;
                    case "NewBadge": NewBadge = b; break;
                    case "ArchiveOn": ArchiveOn = b; break;
                    case "HoverPreview": HoverPreview = b; break;
                    case "Widgets": Widgets = b; break;
                    case "AutoUpdate": AutoUpdate = b; break;
                    case "Pinned": Pinned = b; break;
                    case "QuietStart": QuietStart = b; break;
                    case "TrayHintShown": TrayHintShown = b; break;
                    case "LastVersion": LastVersion = v; break;
                    case "ArchiveLog": ArchiveLog = SplitList(v); break;
                    case "Dark": oldDark = v; break;
                    case "ThemeMode": if (int.TryParse(v, out n)) { ThemeMode = n; hasThemeMode = true; } break;
                    case "Language": if (int.TryParse(v, out n)) Language = n; break;
                    case "Delay": if (int.TryParse(v, out n)) Delay = n; break;
                    case "PanelWidth": if (int.TryParse(v, out n)) PanelWidth = n; break;
                    case "EdgeZone": if (int.TryParse(v, out n)) EdgeZone = n; break;
                    case "ScreenIdx": if (int.TryParse(v, out n)) ScreenIdx = n; break;
                    case "ScreenName": ScreenName = v.Length > 100 ? "" : v; break;
                    case "HotMods": if (int.TryParse(v, out n)) HotMods = n; break;
                    case "HotKey": if (int.TryParse(v, out n)) HotKey = n; break;
                    case "WebEngine": if (int.TryParse(v, out n)) WebEngine = n; break;
                    case "NewDays": if (int.TryParse(v, out n)) NewDays = n; break;
                    case "ArchiveDays": if (int.TryParse(v, out n)) ArchiveDays = n; break;
                    case "LastUpdateCheck": if (long.TryParse(v, out ln) && ln >= 0 && ln <= DateTime.Now.Ticks) LastUpdateCheck = ln; break;
                    case "LastArchive": if (long.TryParse(v, out ln)) LastArchive = ln; break;
                    case "ArchiveName": if (FileOps.IsValidName(v)) ArchiveName = v; break;
                    case "UpdateRepo": UpdateRepo = v; break;
                    case "WeatherOn": WeatherOn = b; break;
                    case "WeatherPlace": WeatherPlace = v.Length > 200 ? v.Substring(0, 200) : v; break;
                    case "WeatherLat": WeatherLat = Coord(v, 90); break;
                    case "WeatherLon": WeatherLon = Coord(v, 180); break;
                    case "Accent": Accent = ParseColor(v, Accent); break;
                    case "Favorites": Favorites = SplitList(v); break;
                    case "Order": Order = SplitList(v); break;
                    case "Collapsed": Collapsed = new HashSet<string>(SplitList(v)); break;
                    case "CatOverride":
                        CatOverride.Clear();
                        foreach (string e in SplitList(v))
                        {
                            int j = e.IndexOf('>');
                            if (j > 0) CatOverride[e.Substring(0, j)] = e.Substring(j + 1);
                        }
                        break;
                    case "Usage":
                        Usage.Clear();
                        foreach (string e in SplitList(v))
                        {
                            int j = e.LastIndexOf('>');
                            if (j <= 0) continue;
                            string[] p = e.Substring(j + 1).Split(',');
                            long c, t;
                            if (p.Length == 2 && long.TryParse(p[0], out c) && long.TryParse(p[1], out t))
                                Usage[e.Substring(0, j)] = new long[] { c, t };
                        }
                        break;
                    case "CustomCats":
                        CustomCats.Clear();
                        foreach (string e in SplitList(v))
                        {
                            string[] p = e.Split('>');
                            int g;
                            if (p.Length == 3 && p[0].Length > 0 && int.TryParse(p[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g))
                                CustomCats.Add(new CustomCat { Name = p[0], Glyph = ((char)g).ToString(), Color = ParseColor(p[2], Color.Gray) });
                        }
                        break;
                }
            }
        }
        catch { return false; }
        if (!hasThemeMode && oldDark != null) ThemeMode = oldDark == "1" ? 1 : 2;
        Delay = Math.Max(0, Math.Min(1000, Delay));
        PanelWidth = Math.Max(260, Math.Min(560, PanelWidth));
        EdgeZone = Math.Max(0, Math.Min(3, EdgeZone));
        ThemeMode = Math.Max(0, Math.Min(2, ThemeMode));
        if (Language < L.SystemLanguage || Language > L.Spanish) Language = L.SystemLanguage;
        HidePanelInCapture = hidePanelInCapture;
        WebEngine = Math.Max(0, Math.Min(3, WebEngine));
        if (NewDays < 1) NewDays = 3;
        if (ArchiveDays < 7 || ArchiveDays > 36500) ArchiveDays = 60;
        // Une durée importée hors des choix proposés s'afficherait comme le premier choix tout en restant
        // utilisée : elle est ramenée au premier choix au moins aussi long, pour ne jamais ranger plus tôt
        // que demandé (R-33). Au-delà du plus long choix, c'est ce dernier.
        NewDays = RoundUp(NewDaysChoices, NewDays);
        ArchiveDays = RoundUp(ArchiveDaysChoices, ArchiveDays);
        // Ancien réglage par indice : converti en nom d'écran avec la disposition actuelle (Q26-05)
        if (ScreenIdx >= 0 && ScreenName.Length == 0)
            try { Screen[] all = Screen.AllScreens; if (ScreenIdx < all.Length) ScreenName = all[ScreenIdx].DeviceName; } catch { }
        return true;
    }

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

    // ---------- Enregistrement ----------
    // Les modifications sont regroupées (400 ms) puis écrites de façon atomique :
    // fichier temporaire puis remplacement, pour ne jamais laisser un fichier à moitié écrit.
    static System.Windows.Forms.Timer saveTimer;
    static SynchronizationContext ui;
    static int uiThreadId;

    public static void AttachToUi()
    {
        uiThreadId = Thread.CurrentThread.ManagedThreadId;
        ui = SynchronizationContext.Current;
    }

    public static void SaveSoon()
    {
        if (ui == null) { Save(); return; }
        if (Thread.CurrentThread.ManagedThreadId != uiThreadId) { ui.Post(_ => SaveSoon(), null); return; }
        if (saveTimer == null)
        {
            saveTimer = new System.Windows.Forms.Timer { Interval = 400 };
            saveTimer.Tick += (s, e) => { saveTimer.Stop(); Save(); };
        }
        saveTimer.Stop();
        saveTimer.Start();
    }

    public static void Flush()
    {
        if (saveTimer != null && saveTimer.Enabled) { saveTimer.Stop(); Save(); }
    }

    public static void CancelPendingSave()
    {
        if (saveTimer != null) saveTimer.Stop();
    }

    public static bool SaveBlocked;   // posé par la désinstallation (Q26-03)

    public static bool Save()
    {
        if (SaveBlocked) return false;
        string tmp = null;
        try
        {
            Directory.CreateDirectory(Dir);
            tmp = Path.Combine(Dir, Guid.NewGuid().ToString("N") + ".tmp");
            string[] lines = Serialize();
            // Un fichier que Load refuserait ne remplace jamais le précédent (Q24-01)
            if (Utf8Size(lines) > MaxFileSize)
            {
                System.Diagnostics.Trace.TraceError("BureauCache settings: file would exceed " + MaxFileSize + " bytes, not saved.");
                return false;
            }
            File.WriteAllLines(tmp, lines, new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("BureauCache settings: " + ex.Message);
            return false;
        }
        finally { if (tmp != null) try { File.Delete(tmp); } catch { } }
    }

    // Le fichier écrit doit rester relisible : au-delà de MaxFileSize, la moitié la plus ancienne de
    // l'historique d'usage est oubliée, autant de fois que nécessaire (R-14, Q24-01)
    static string[] Serialize()
    {
        PruneUsage();
        string[] lines = SerializeLines();
        while (Usage.Count > 0 && Utf8Size(lines) > MaxFileSize)
        {
            foreach (string k in Usage.OrderBy(kv => kv.Value[1]).Take((Usage.Count + 1) / 2).Select(kv => kv.Key).ToList())
                Usage.Remove(k);
            lines = SerializeLines();
        }
        return lines;
    }

    internal static long Utf8Size(string[] lines)
    {
        long size = 0;
        foreach (string line in lines) size += Encoding.UTF8.GetByteCount(line) + 2;
        return size;
    }

    static string[] SerializeLines()
    {
        {
            return new string[] {
                "HideIcons=" + B(HideIcons), "RightSide=" + B(RightSide), "AutoClose=" + B(AutoClose),
                "ShowSystem=" + B(ShowSystem), "ShowRecents=" + B(ShowRecents), "GroupCats=" + B(GroupCats),
                "GridView=" + B(GridView), "Glass=" + B(Glass), "LiveBlur=" + B(LiveBlur), "HidePanelInCapture=" + B(HidePanelInCapture), "Animations=" + B(Animations),
                "NoFullscreen=" + B(NoFullscreen), "HotkeyOn=" + B(HotkeyOn),
                "SearchApps=" + B(SearchApps), "SearchRecent=" + B(SearchRecent), "SearchSettings=" + B(SearchSettings),
                "SearchCommands=" + B(SearchCommands), "NewBadge=" + B(NewBadge), "ArchiveOn=" + B(ArchiveOn),
                "HoverPreview=" + B(HoverPreview), "Widgets=" + B(Widgets), "AutoUpdate=" + B(AutoUpdate),
                "Pinned=" + B(Pinned), "QuietStart=" + B(QuietStart), "TrayHintShown=" + B(TrayHintShown), "LastVersion=" + LastVersion, "ArchiveLog=" + string.Join("|", ArchiveLog),
                "ThemeMode=" + ThemeMode, "Language=" + Language, "Delay=" + Delay, "PanelWidth=" + PanelWidth,
                "EdgeZone=" + EdgeZone, "ScreenIdx=" + ScreenIdx, "ScreenName=" + ScreenName, "HotMods=" + HotMods, "HotKey=" + HotKey,
                "WebEngine=" + WebEngine, "NewDays=" + NewDays, "ArchiveDays=" + ArchiveDays,
                "LastUpdateCheck=" + LastUpdateCheck, "LastArchive=" + LastArchive,
                "ArchiveName=" + ArchiveName, "UpdateRepo=" + UpdateRepo,
                "WeatherOn=" + B(WeatherOn), "WeatherPlace=" + WeatherPlace,
                "WeatherLat=" + (double.IsNaN(WeatherLat) ? "" : WeatherLat.ToString("R", CultureInfo.InvariantCulture)),
                "WeatherLon=" + (double.IsNaN(WeatherLon) ? "" : WeatherLon.ToString("R", CultureInfo.InvariantCulture)),
                "Accent=" + Hex(Accent),
                "Favorites=" + string.Join("|", Favorites),
                "Order=" + string.Join("|", Order),
                "Collapsed=" + string.Join("|", Collapsed),
                "CatOverride=" + string.Join("|", CatOverride.Select(kv => kv.Key + ">" + kv.Value)),
                "Usage=" + string.Join("|", Usage.Select(kv => kv.Key + ">" + kv.Value[0] + "," + kv.Value[1])),
                "CustomCats=" + string.Join("|", CustomCats.Select(c => c.Name + ">" + ((int)c.Glyph[0]).ToString("X4") + ">" + Hex(c.Color)))
            };
        }
    }

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
        Favorites = s.Fav; Order = s.Order; CatOverride = s.Cat; Usage = s.Usage;
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

    public static void RecordUse(string key)
    {
        long[] u;
        if (!Usage.TryGetValue(key, out u)) u = new long[] { 0, 0 };
        Usage[key] = new long[] { u[0] + 1, DateTime.Now.Ticks };
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
