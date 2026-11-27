// BureauCache — Settings (lecture et écriture de settings.ini)
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

static partial class Settings
{
    // Chargement au démarrage. Un settings.ini présent mais momentanément illisible (antivirus, synchronisation
    // OneDrive) est relu quelques fois ; s'il le reste, plus rien ne l'écrit pendant cette session : sinon les valeurs
    // par défaut remplaceraient les vrais réglages dès que le verrou disparaît (CDX-23).
    public static void Load() { LoadWithRetries(FilePath, 10, 300); }

    internal static void LoadWithRetries(string path, int attempts, int pauseMs)
    {
        ProfileUnreadable = false;
        for (int i = 0; i < attempts; i++)
        {
            if (Load(path) || !LastLoadUnreadable) return;
            if (i + 1 < attempts) Thread.Sleep(pauseMs);
        }
        ProfileUnreadable = true;
    }

    // Vrai si le dernier Load a échoué faute de pouvoir LIRE le fichier (verrou, droits), et non parce qu'il est abîmé
    internal static bool LastLoadUnreadable;
    // Réglages illisibles au démarrage : aucune écriture jusqu'à une restauration ou un import explicite (CDX-23)
    public static bool ProfileUnreadable;

    public static bool Load(string path)
    {
        LastLoadUnreadable = false;
        RefreshSystemTheme();
        if (!File.Exists(path)) { FirstRun = true; return false; }
        bool hasThemeMode = false;
        bool hasWeatherPlace = false, hasWeatherShort = false;
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
            RecentsSince = 0;  // absent d'un ancien INI : tous les récents de ce fichier s'affichent (Q30-05)
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
                    case "SearchContent": SearchContent = b; break;
                    case "ClipboardOn": ClipboardOn = b; break;
                    case "TaskbarOn": TaskbarOn = b; break;
                    case "TaskbarPins": TaskbarPins = SplitList(v); break;
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
                    case "RecentsSince": if (long.TryParse(v, out ln) && ln >= 0 && ln <= DateTime.Now.Ticks) RecentsSince = ln; break;
                    case "ArchiveName": if (FileOps.IsValidName(v)) ArchiveName = v; break;
                    case "UpdateRepo": UpdateRepo = v; break;
                    case "WeatherOn": WeatherOn = b; break;
                    case "WeatherPlace": WeatherPlace = v.Length > 200 ? v.Substring(0, 200) : v; hasWeatherPlace = true; break;
                    case "WeatherShort": WeatherShort = v.Length > 200 ? v.Substring(0, 200) : v; hasWeatherShort = true; break;
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
                            if (p.Length == 2 && long.TryParse(p[0], out c) && long.TryParse(p[1], out t) &&
                                t >= 0 && t <= DateTime.MaxValue.Ticks)
                                Usage[e.Substring(0, j)] = new long[] { Math.Max(0, c), t };
                        }
                        break;
                    case "ArchiveRules":
                        ArchiveRules = new List<ArchiveRule>();
                        foreach (string e in SplitList(v))
                        {
                            ArchiveRule r = ArchiveRule.Parse(e);
                            if (r != null && ArchiveRules.Count < global::ArchiveRules.MaxRules) ArchiveRules.Add(r);
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
        catch (IOException) { LastLoadUnreadable = true; return false; }
        catch (UnauthorizedAccessException) { LastLoadUnreadable = true; return false; }
        catch { return false; }
        if (hasWeatherPlace && !hasWeatherShort) WeatherShort = "";
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
        if (SaveBlocked || ProfileUnreadable) return false;
        if (DirOverride == null) Sandbox.Guard("écriture des vrais réglages");   // défense en profondeur (QA-32)
        Sandbox.GuardPath(Dir, "écriture des réglages");                       // dossier isolé sous %TEMP% (QA34-01)
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
            SettingsBackup.BeforeSave();   // copie du jour, avant la première réécriture (D-007)
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
    internal static string[] Serialize()
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
                "SearchCommands=" + B(SearchCommands), "SearchContent=" + B(SearchContent), "ClipboardOn=" + B(ClipboardOn), "TaskbarOn=" + B(TaskbarOn), "TaskbarPins=" + string.Join("|", TaskbarPins), "NewBadge=" + B(NewBadge), "ArchiveOn=" + B(ArchiveOn),
                "HoverPreview=" + B(HoverPreview), "Widgets=" + B(Widgets), "AutoUpdate=" + B(AutoUpdate),
                "Pinned=" + B(Pinned), "QuietStart=" + B(QuietStart), "TrayHintShown=" + B(TrayHintShown), "LastVersion=" + LastVersion, "ArchiveLog=" + string.Join("|", ArchiveLog),
                "ThemeMode=" + ThemeMode, "Language=" + Language, "Delay=" + Delay, "PanelWidth=" + PanelWidth,
                "EdgeZone=" + EdgeZone, "ScreenIdx=" + ScreenIdx, "ScreenName=" + ScreenName, "HotMods=" + HotMods, "HotKey=" + HotKey,
                "WebEngine=" + WebEngine, "NewDays=" + NewDays, "ArchiveDays=" + ArchiveDays,
                "LastUpdateCheck=" + LastUpdateCheck, "LastArchive=" + LastArchive, "RecentsSince=" + RecentsSince,
                "ArchiveName=" + ArchiveName, "UpdateRepo=" + UpdateRepo,
                "WeatherOn=" + B(WeatherOn), "WeatherPlace=" + WeatherPlace, "WeatherShort=" + WeatherShort,
                "WeatherLat=" + (double.IsNaN(WeatherLat) ? "" : WeatherLat.ToString("R", CultureInfo.InvariantCulture)),
                "WeatherLon=" + (double.IsNaN(WeatherLon) ? "" : WeatherLon.ToString("R", CultureInfo.InvariantCulture)),
                "Accent=" + Hex(Accent),
                "Favorites=" + string.Join("|", Favorites),
                "Order=" + string.Join("|", Order),
                "Collapsed=" + string.Join("|", Collapsed),
                "CatOverride=" + string.Join("|", CatOverride.Select(kv => kv.Key + ">" + kv.Value)),
                "Usage=" + string.Join("|", Usage.Select(kv => kv.Key + ">" + kv.Value[0] + "," + kv.Value[1])),
                "CustomCats=" + string.Join("|", CustomCats.Select(c => c.Name + ">" + ((int)c.Glyph[0]).ToString("X4") + ">" + Hex(c.Color))),
                "ArchiveRules=" + string.Join("|", ArchiveRules.Select(r => r.Serialize()))
            };
        }
    }
}
