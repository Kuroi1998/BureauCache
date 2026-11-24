// BureauCache — PanelForm (partie 3)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

partial class PanelForm
{
    // ---------- Chargement en arrière-plan ----------
    public void RequestLoad()
    {
        if (loading) { loadPending = true; return; }
        loading = true;
        Dirty = false;
        int px = IconPx, lpx = ListPx;
        bool sys = loadedSystem = Settings.ShowSystem;
        bool apps = loadedApps = Settings.SearchApps;
        bool rec = loadedRecents = Settings.SearchRecent;
        Background.Run(() =>
        {
            List<DesktopItem> a = null, r = null;
            try
            {
                List<DesktopItem> desk = Loader.Desktop(px, sys);
                Post(() => { int oldPx = loadedPx; All = desk; loadedPx = px; loadedOnce = true; Rebuild(true); ReleaseIcons(oldPx); });
                a = apps ? Loader.Apps(lpx) : new List<DesktopItem>();
                r = rec ? Loader.Recent(lpx) : new List<DesktopItem>();
            }
            finally
            {
                // Toujours libérer l'indicateur, même en cas d'erreur, sinon le panneau resterait sur « Chargement… »
                Post(() =>
                {
                    if (a != null) Apps = a;
                    if (r != null) Recent = r;
                    loading = false;
                    Rebuild(true);
                    if (loadPending) { loadPending = false; RequestLoad(); }
                });
            }
        }, true);
    }

    // Après un passage liste ↔ grille, les icônes de l'ancienne taille ne servent plus : on libère la mémoire
    void ReleaseIcons(int oldPx)
    {
        if (oldPx > 0 && oldPx != loadedPx && oldPx != ListPx) IconCache.RemoveSize(oldPx);
    }

    void Post(Action a)
    {
        try { if (IsHandleCreated) BeginInvoke(a); } catch { }
    }

    public void OnSettingsChanged()
    {
        if (IconPx != loadedPx || loadedSystem != Settings.ShowSystem ||
            loadedApps != Settings.SearchApps || loadedRecents != Settings.SearchRecent) Dirty = true;
        RefreshCatalog();
        ApplyCaptureSettings();
        ApplyPanelGeometry();
        // Effet verre activé panneau ouvert, en flou statique : la photo du fond est prise tout de suite (R-31)
        if (IsOpen && glass == null && !liveBlurActive) RefreshStaticGlass();
        UpdateWidgetTimer(IsOpen);
        ApplyTheme();
        if (Dirty) RequestLoad();
        Rebuild(true);
    }

    public void ReloadNow() { Dirty = true; RequestLoad(); }

    public void Rebuild(bool keepScroll)
    {
        string q = search.Text.Trim();
        View.Help = q == "?" ? HelpLines() : null;
        if (q == "?") q = "";
        View.ForceList = q.Length > 0;
        View.EmptyText = q.Length > 0 ? L.T("Aucun résultat pour « ", "No results for \"", "No hay resultados para «") + q + L.T(" »", "\"", "»")
                                      : loading && !loadedOnce ? L.T("Chargement…", "Loading…", "Cargando…") : L.T("Ton bureau est vide", "Your desktop is empty", "Tu escritorio está vacío");
        List<Group> groups = q.Length > 0 ? SearchGroups(q) : BuildGroups();
        if (q.StartsWith("=") && groups.Count == 0)
            View.EmptyText = q.Length == 1 ? L.T("Saisis un calcul après le signe =.", "Enter a calculation after the = sign.", "Introduce un cálculo después del signo =.")
                                          : L.T("Expression invalide.", "Invalid expression.", "Expresión no válida.");
        View.SetGroups(groups, keepScroll);
        Invalidate();
    }

    List<Group> BuildGroups()
    {
        List<Group> res = new List<Group>();
        Dictionary<string, DesktopItem> byKey = new Dictionary<string, DesktopItem>(StringComparer.OrdinalIgnoreCase);
        foreach (DesktopItem it in All) byKey[it.Key] = it;

        Group fav = new Group { Key = "fav", Title = L.T("Favoris", "Favorites", "Favoritos"), Glyph = Glyphs.StarFilled, GlyphColor = Theme.Accent };
        HashSet<string> favKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string k in Settings.Favorites)
        {
            DesktopItem it;
            if (byKey.TryGetValue(k, out it) && favKeys.Add(k)) fav.Items.Add(it);
        }
        if (fav.Items.Count > 0) res.Add(fav);

        if (Settings.ShowRecents)
        {
            Group rec = new Group { Key = "recent", Title = L.T("Récents", "Recent", "Recientes"), Glyph = Glyphs.Recent };
            rec.Items = All.Where(i => Settings.Usage.ContainsKey(i.Key) && Settings.Usage[i.Key][0] > 0 && Settings.Usage[i.Key][1] > Settings.RecentsSince && !favKeys.Contains(i.Key))
                           .OrderByDescending(i => Settings.Usage[i.Key][1]).Take(5).ToList();
            if (rec.Items.Count > 0) res.Add(rec);
        }

        Dictionary<string, int> order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Settings.Order.Count; i++) order[Settings.Order[i]] = i;
        Comparison<DesktopItem> cmp = (a, b) =>
        {
            int ia, ib;
            bool ha = order.TryGetValue(a.Key, out ia), hb = order.TryGetValue(b.Key, out ib);
            if (ha && hb) return ia.CompareTo(ib);
            if (ha) return -1;
            if (hb) return 1;
            if (a.IsSystem != b.IsSystem) return a.IsSystem ? -1 : 1;
            if (a.IsFolder != b.IsFolder) return a.IsFolder ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        };
        List<DesktopItem> rest = All.Where(i => !favKeys.Contains(i.Key)).ToList();
        if (Settings.GroupCats)
        {
            foreach (string c in Catalog.AllCats())
            {
                Group g = new Group { Key = "cat:" + c, Title = Catalog.Display(c), Glyph = Catalog.GlyphOf(c), GlyphColor = Catalog.ColorOf(c) };
                g.Items = rest.Where(i => string.Equals(i.Category, c, StringComparison.OrdinalIgnoreCase)).ToList();
                g.Items.Sort(cmp);
                if (g.Items.Count > 0) res.Add(g);
            }
        }
        else
        {
            Group g = new Group { Key = "all", Title = res.Count > 0 ? L.T("Tous les éléments", "All items", "Todos los elementos") : null, Glyph = Glyphs.AllItems };
            g.Items = rest;
            g.Items.Sort(cmp);
            res.Add(g);
        }
        return res;
    }

    // ---------- Recherche universelle ----------
    static List<KeyValuePair<DesktopItem, int>> Rank(IEnumerable<DesktopItem> list, string nq, int max)
    {
        List<KeyValuePair<DesktopItem, int>> res = new List<KeyValuePair<DesktopItem, int>>();
        foreach (DesktopItem it in list)
        {
            int s = Search.ScoreNormalized(nq, it.NormName);
            foreach (string kw in it.NormKeywords) s = Math.Max(s, Search.ScoreNormalized(nq, kw) - 150);
            if (s < 0) continue;
            long[] u;
            if (Settings.Usage.TryGetValue(it.Key, out u)) s += (int)Math.Min(u[0] * 15, 120);
            res.Add(new KeyValuePair<DesktopItem, int>(it, s));
        }
        return res.OrderByDescending(k => k.Value).ThenBy(k => k.Key.Name, StringComparer.CurrentCultureIgnoreCase).Take(max).ToList();
    }

    List<Group> SearchGroups(string q)
    {
        List<Group> res = new List<Group>();
        int px = ListPx;
        if (q.StartsWith("?"))
        {
            string w = q.Substring(1).Trim();
            if (w.Length > 0) res.Add(WebGroup(w, px));
            return res;
        }
        if (q.StartsWith(">"))
        {
            string c = Search.Norm(q.Substring(1));
            Group g = new Group { Key = "s:cmd", Title = L.T("Commandes", "Commands", "Comandos"), Glyph = Glyphs.Terminal };
            g.Items = c.Length == 0 ? commands.ToList() : Rank(commands, c, 20).Select(k => k.Key).ToList();
            if (g.Items.Count > 0) res.Add(g);
            return res;
        }
        bool explicitCalc = q.StartsWith("=");
        string expr = explicitCalc ? q.Substring(1) : q;
        double v;
        if ((explicitCalc || Calc.LooksLikeMath(q)) && Calc.TryEval(expr, out v))
        {
            string r = Calc.Format(v);
            res.Add(new Group
            {
                Key = "s:calc", Title = L.T("Calcul", "Calculator", "Calculadora"), Glyph = Glyphs.Calculator,
                Items = { new DesktopItem { Kind = ItemKind.Calc, Key = "calc", Name = "= " + r, Args = r,
                    Detail = L.T("Entrée pour copier le résultat", "Press Enter to copy the result", "Pulsa Intro para copiar el resultado"),
                    Icon = IconCache.Tile("=", "Segoe UI", Color.FromArgb(22, 163, 74), px) } }
            });
        }
        if (explicitCalc) return res;
        string nq = Search.Norm(q);
        if (nq.Length > 0)
        {
            List<KeyValuePair<Group, int>> found = new List<KeyValuePair<Group, int>>();
            Action<string, string, string, IEnumerable<DesktopItem>, int> addGroup = (key, title, glyph, src, max) =>
            {
                List<KeyValuePair<DesktopItem, int>> r = Rank(src, nq, max);
                if (r.Count == 0) return;
                Group g = new Group { Key = key, Title = title, Glyph = glyph, Items = r.Select(k => k.Key).ToList() };
                found.Add(new KeyValuePair<Group, int>(g, r[0].Value));
            };
            addGroup("s:desk", L.T("Bureau", "Desktop", "Escritorio"), Glyphs.AllItems, All, 12);
            HashSet<string> deskNames = new HashSet<string>(All.Select(i => i.NormName));
            if (Settings.SearchApps) addGroup("s:apps", L.T("Applications", "Apps", "Aplicaciones"), Glyphs.Apps, Apps.Where(a => !deskNames.Contains(a.NormName)), 8);
            if (Settings.SearchRecent) addGroup("s:recent", L.T("Fichiers récents", "Recent files", "Archivos recientes"), Glyphs.Recent, Recent, 6);
            if (Settings.SearchSettings) addGroup("s:set", L.T("Paramètres Windows", "Windows settings", "Configuración de Windows"), Glyphs.Settings, winSettings, 5);
            if (Settings.SearchCommands) addGroup("s:cmd", L.T("Commandes", "Commands", "Comandos"), Glyphs.Terminal, commands, 4);
            res.AddRange(found.OrderByDescending(k => k.Value).Select(k => k.Key));
        }
        if (Settings.WebEngine != 3 && !res.Any(g => g.Key == "s:calc")) res.Add(WebGroup(q, px));
        return res;
    }

    static string EngineName { get { return new[] { "Google", "Bing", "DuckDuckGo", "" }[Settings.WebEngine]; } }

    static string EngineUrl(string q)
    {
        string e = Uri.EscapeDataString(Search.ReplaceInvalidUnicode(q));
        switch (Settings.WebEngine)
        {
            case 1: return "https://www.bing.com/search?q=" + e;
            case 2: return "https://duckduckgo.com/?q=" + e;
            default: return "https://www.google.com/search?q=" + e;
        }
    }

    Group WebGroup(string q, int px)
    {
        string engine = Settings.WebEngine == 3 ? "Google" : EngineName;
        string url = EngineUrl(q);
        return new Group
        {
            Key = "s:web", Title = "Web", Glyph = Glyphs.Globe,
            Items = { new DesktopItem {
                Kind = ItemKind.Web, Key = "web", Name = L.T("Rechercher « " + q + " » sur " + engine, "Search " + engine + " for \"" + q + "\"", "Buscar «" + q + "» en " + engine),
                Detail = url, Icon = IconCache.Tile(Glyphs.Globe, Ui.GlyphFont, Settings.Accent, px), Run = () => ShellUtil.Open(url, null) } }
        };
    }

    // ---------- Organisation par glisser-déposer ----------
    void HandleDrop(DesktopItem it, Group from, Group to, int index)
    {
        if (to.Key == "recent" || to.IsSearch) return;
        Settings.Snap snap = Settings.Snapshot();
        if (from != to)
        {
            string msg = to.Key == "fav" ? L.T("« " + it.Name + " » ajouté aux favoris", "\"" + it.Name + "\" added to favorites", "«" + it.Name + "» se añadió a favoritos")
                                         : L.T("« " + it.Name + " » déplacé vers " + to.Title, "\"" + it.Name + "\" moved to " + to.Title, "«" + it.Name + "» se movió a " + to.Title);
            BeginInvoke(new Action(() => Toast(msg, () => { Settings.Restore(snap); Rebuild(true); }, null)));
        }
        if (to.Key == "fav")
        {
            int cur = Settings.FavIndex(it.Key);
            if (cur >= 0) { Settings.Favorites.RemoveAt(cur); if (cur < index) index--; }
            index = Math.Max(0, Math.Min(Settings.Favorites.Count, index));
            Settings.Favorites.Insert(index, it.Key);
        }
        else
        {
            int fi = Settings.FavIndex(it.Key);
            if (fi >= 0) Settings.Favorites.RemoveAt(fi);
            if (to.Key.StartsWith("cat:")) SetCategory(it, to.Key.Substring(4), false);
            List<string> keys = to.Items.Select(i => i.Key).ToList();
            int cur = keys.FindIndex(k => string.Equals(k, it.Key, StringComparison.OrdinalIgnoreCase));
            if (cur >= 0) { keys.RemoveAt(cur); if (cur < index) index--; }
            index = Math.Max(0, Math.Min(keys.Count, index));
            keys.Insert(index, it.Key);
            HashSet<string> ks = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
            Settings.Order.RemoveAll(k => ks.Contains(k));
            Settings.Order.AddRange(keys);
        }
        Settings.SaveQuiet();
        Rebuild(true);
    }

    void ToggleFavorite(DesktopItem it)
    {
        Settings.Snap snap = Settings.Snapshot();
        int i = Settings.FavIndex(it.Key);
        if (i >= 0) Settings.Favorites.RemoveAt(i); else Settings.Favorites.Add(it.Key);
        Settings.SaveQuiet();
        Rebuild(true);
        Toast(i >= 0 ? L.T("« " + it.Name + " » retiré des favoris", "\"" + it.Name + "\" removed from favorites", "«" + it.Name + "» se quitó de favoritos")
                     : L.T("« " + it.Name + " » ajouté aux favoris", "\"" + it.Name + "\" added to favorites", "«" + it.Name + "» se añadió a favoritos"),
              () => { Settings.Restore(snap); Rebuild(true); }, null);
    }

    void MoveToCategory(DesktopItem it, string cat)
    {
        Settings.Snap snap = Settings.Snapshot();
        SetCategory(it, cat, true);
        Rebuild(true);
        Toast(L.T("« " + it.Name + " » déplacé vers " + Catalog.Display(cat), "\"" + it.Name + "\" moved to " + Catalog.Display(cat), "«" + it.Name + "» se movió a " + Catalog.Display(cat)),
              () => { Settings.Restore(snap); Rebuild(true); }, null);
    }

    static void SetCategory(DesktopItem it, string cat, bool save)
    {
        if (string.Equals(cat, it.AutoCategory, StringComparison.OrdinalIgnoreCase)) Settings.CatOverride.Remove(it.Key);
        else Settings.CatOverride[it.Key] = cat;
        if (save) Settings.SaveQuiet();
    }
}
