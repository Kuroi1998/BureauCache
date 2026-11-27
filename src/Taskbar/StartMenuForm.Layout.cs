// BureauCache — StartMenuForm : construction du menu selon les choix de l'utilisateur (T-039, T-044, D-019)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

partial class StartMenuForm
{
    // Une tuile possible du menu : identifiant stable (pour la masquer), apparence et action
    internal sealed class TileSpec
    {
        public string Id, Glyph, Text;
        public Image Picture;
        public Action Click;
        public StartShortcuts.Entry Shortcut;   // raccourci personnel : se retire au lieu de se masquer
    }

    public event Action OpenStartSettings;

    internal static readonly string[] BureauCacheTiles = { "bc.panel", "bc.settings", "bc.tour", "bc.quit" };
    internal static readonly string[] WindowsTiles = { "win.settings", "win.explorer", "win.taskmgr", "win.documents", "win.downloads", "win.control" };

    static bool Hidden(string id) { return Settings.StartHidden.Contains(id); }

    void Build()
    {
        string[] windows =
        {
            L.T("Paramètres Windows", "Windows Settings", "Configuración de Windows"),
            L.T("Explorateur de fichiers", "File Explorer", "Explorador de archivos"),
            L.T("Gestionnaire des tâches", "Task Manager", "Administrador de tareas"),
            L.T("Documents", "Documents", "Documentos"),
            L.T("Téléchargements", "Downloads", "Descargas"),
            L.T("Panneau de configuration", "Control Panel", "Panel de control")
        };
        int third = ColumnWidth(windows, (MinimumWidth - 2 * Pad - 2 * Gap) / 3);
        int inner = 3 * third + 2 * Gap, width = inner + 2 * Pad, y = Pad;
        menuWidth = width;
        searchRect = headerRect = Rectangle.Empty;
        search = null;

        // Recherche : le premier caractère tapé ouvre le panneau avec ce texte, comme dans le menu de Windows
        if (Settings.StartSearch)
        {
            searchRect = new Rectangle(Pad, y, inner, Ui.S(40));
            search = new TextBox { BorderStyle = BorderStyle.None, Font = Ui.Font("Segoe UI", 10.5f), BackColor = Theme.Surface, ForeColor = Theme.Fg,
                                   MaxLength = 256, AccessibleName = L.T("Rechercher", "Search", "Buscar") };
            search.Bounds = new Rectangle(searchRect.Left + Ui.S(42), searchRect.Top + (searchRect.Height - search.PreferredHeight) / 2,
                                          searchRect.Width - Ui.S(54), search.PreferredHeight);
            TextBox box = search;
            box.TextChanged += (s, e) =>
            {
                string text = box.Text;
                if (text.Trim().Length == 0) return;
                Finish(() => { if (SearchRequested != null) SearchRequested(text); });
            };
            Controls.Add(search);
            y = searchRect.Bottom + Ui.S(14);
        }
        if (Settings.StartAccount)
        {
            headerRect = new Rectangle(Pad, y, inner, Ui.S(56));
            y = headerRect.Bottom + Ui.S(16);
        }

        int half = (inner - Gap) / 2, tileH = Ui.S(88), shortH = Math.Max(Ui.S(52), 2 * TileFont.Height + Ui.S(12)), rowH = Ui.S(48);
        if (Settings.StartBureauCache)
            y = AddSection(L.T("BUREAUCACHE", "BUREAUCACHE", "BUREAUCACHE"), L.T("le panneau et ses réglages", "the panel and its settings", "el panel y su configuración"), new[]
            {
                Spec("bc.panel", Glyphs.Search, L.T("Applications et recherche", "Apps and search", "Aplicaciones y búsqueda"), () => Finish(() => { if (OpenPanel != null) OpenPanel(); })),
                Spec("bc.settings", Glyphs.Settings, L.T("Paramètres de BureauCache", "BureauCache settings", "Configuración de BureauCache"), () => Finish(() => { if (OpenSettings != null) OpenSettings(); })),
                Spec("bc.tour", Glyphs.School, L.T("Visite guidée", "Guided tour", "Visita guiada"), () => Finish(() => { if (OpenTour != null) OpenTour(); })),
                // Comme « Quitter » du menu de l'icône : icônes du bureau et barre Windows reviennent
                Spec("bc.quit", Glyphs.Dismiss, L.T("Quitter BureauCache", "Quit BureauCache", "Salir de BureauCache"), () => Finish(() => { if (QuitRequested != null) QuitRequested(); }))
            }, 2, half, shortH, false, y);

        List<TileSpec> mine = StartShortcuts.All().Select(ShortcutSpec).ToList();
        if (mine.Count > 0)
            y = AddSection(L.T("MES RACCOURCIS", "MY SHORTCUTS", "MIS ACCESOS DIRECTOS"), L.T("applications, dossiers et sites ajoutés", "apps, folders and sites you added", "aplicaciones, carpetas y sitios añadidos"),
                mine.ToArray(), 3, third, shortH, false, y);

        if (Settings.StartWindows)
            y = AddSection("WINDOWS", L.T("ouvre les outils de Windows", "opens Windows tools", "abre las herramientas de Windows"), new[]
            {
                Spec("win.settings", Glyphs.Settings, windows[0], () => Launch("ms-settings:")),
                Spec("win.explorer", Glyphs.Folder, windows[1], () => Launch("explorer.exe")),
                Spec("win.taskmgr", Glyphs.TaskManager, windows[2], () => Launch("taskmgr.exe")),
                Spec("win.documents", Glyphs.Page, windows[3], () => Launch("shell:Personal")),
                Spec("win.downloads", Glyphs.Download, windows[4], () => Launch("shell:Downloads")),
                Spec("win.control", Glyphs.Box, windows[5], () => Launch("control.exe"))
            }, 3, third, shortH, false, y);

        if (Settings.StartSession)
            y = AddSection(L.T("SESSION WINDOWS", "WINDOWS SESSION", "SESIÓN DE WINDOWS"),
                L.T("ton compte reste ouvert ou se ferme", "keeps your account open or signs it out", "mantiene tu cuenta abierta o la cierra"), new[]
            {
                Spec("session.lock", Glyphs.Lock, L.T("Verrouiller", "Lock", "Bloquear"), () => Run(SessionActions.Lock)),
                Spec("session.signout", Glyphs.User, L.T("Se déconnecter", "Sign out", "Cerrar sesión"), () => Run(SessionActions.SignOut))
            }, 2, half, rowH, false, y);

        if (Settings.StartPower)
        {
            bool updates = SessionActions.UpdatesPending;
            y = AddSection(L.T("ALIMENTATION", "POWER", "ENERGÍA"), L.T("met en veille, redémarre ou éteint le PC", "sleeps, restarts or shuts down the PC", "suspende, reinicia o apaga el PC"), new[]
            {
                Spec("power.sleep", Glyphs.Sleep, L.T("Mettre en veille", "Sleep", "Suspender"), () => Run(SessionActions.Sleep)),
                Spec("power.restart", Glyphs.Restart, updates ? L.T("Mettre à jour et redémarrer", "Update and restart", "Actualizar y reiniciar") : L.T("Redémarrer", "Restart", "Reiniciar"),
                    () => Run(() => SessionActions.Restart())),
                Spec("power.off", Glyphs.Power, updates ? L.T("Mettre à jour et arrêter", "Update and shut down", "Actualizar y apagar") : L.T("Arrêter", "Shut down", "Apagar"),
                    () => Run(() => SessionActions.PowerOff()))
            }, 3, third, tileH, true, y);
        }

        // Tout masqué : une seule tuile pour revenir aux réglages, jamais un menu vide
        if (!Controls.OfType<StartTile>().Any())
            y = AddSection(L.T("MENU DÉMARRER", "START MENU", "MENÚ INICIO"), L.T("tout est masqué", "everything is hidden", "todo está oculto"), new[]
            {
                new TileSpec { Id = null, Glyph = Glyphs.Settings, Text = L.T("Personnaliser le menu", "Customize the menu", "Personalizar el menú"),
                               Click = () => Finish(() => { if (OpenStartSettings != null) OpenStartSettings(); }) }
            }, 1, inner, shortH, false, y);

        ClientSize = new Size(width, y - Ui.S(16) + Pad);
    }

    TileSpec Spec(string id, string glyph, string text, Action click)
    {
        return new TileSpec { Id = id, Glyph = glyph, Text = text, Click = click };
    }

    TileSpec ShortcutSpec(StartShortcuts.Entry e)
    {
        Image picture = null;
        if (e.Kind == StartShortcuts.App) { try { picture = IconCache.ForFile(e.Target, Ui.S(24)); } catch (Exception) { } }   // image du cache : jamais libérée ici
        return new TileSpec
        {
            Id = "sc:" + e.Raw, Shortcut = e, Text = e.Name, Picture = picture,
            Glyph = e.Kind == StartShortcuts.Folder ? Glyphs.Folder : e.Kind == StartShortcuts.Site ? Glyphs.Globe : Glyphs.Apps,
            Click = () => Finish(() =>
            {
                if (!StartShortcuts.Open(e))
                    Dialogs.Warn(null, L.T("« " + e.Name + " » est introuvable. Retire ce raccourci avec un clic droit.",
                        "\"" + e.Name + "\" can't be found. Remove this shortcut with a right-click.",
                        "No se encuentra «" + e.Name + "». Quita este acceso directo con un clic derecho."));
            })
        };
    }

    // Titre de section et ses tuiles visibles, rangées sur des colonnes ; rien quand toutes sont masquées
    int AddSection(string title, string hint, TileSpec[] specs, int columns, int cellWidth, int height, bool stacked, int y)
    {
        TileSpec[] shown = specs.Where(s => s.Shortcut != null || s.Id == null || !Hidden(s.Id)).ToArray();
        if (shown.Length == 0) return y;
        y = SectionLabel(title, hint, y);
        for (int i = 0; i < shown.Length; i++)
        {
            int column = i % columns;
            if (i > 0 && column == 0) y += height + Gap;
            AddTile(shown[i], new Rectangle(Pad + column * (cellWidth + Gap), y, cellWidth, height), stacked);
        }
        return y + height + Ui.S(16);
    }

    void AddTile(TileSpec spec, Rectangle bounds, bool stacked)
    {
        StartTile t = new StartTile(spec.Glyph, spec.Text, stacked) { Bounds = bounds, Picture = spec.Picture, Tag = spec };
        t.Click += (s, e) => spec.Click();
        t.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) ShowTileMenu(t, spec, e.Location); };
        Controls.Add(t);
    }

    // Clic droit : masquer la tuile (ou retirer le raccourci), ou ouvrir la page de personnalisation
    void ShowTileMenu(Control tile, TileSpec spec, Point at)
    {
        ContextMenuStrip menu = Menus.Create();
        if (spec.Shortcut != null)
            menu.Items.Add(Menus.Item(L.T("Retirer ce raccourci", "Remove this shortcut", "Quitar este acceso directo"), Glyphs.Delete, () => Change(() => StartShortcuts.Remove(spec.Shortcut.Raw))));
        else if (spec.Id != null)
            menu.Items.Add(Menus.Item(L.T("Masquer cette tuile", "Hide this tile", "Ocultar este mosaico"), Glyphs.Eye, () => Change(() => { if (!Settings.StartHidden.Contains(spec.Id)) Settings.StartHidden.Add(spec.Id); })));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Menus.Item(L.T("Personnaliser le menu Démarrer…", "Customize the Start menu…", "Personalizar el menú Inicio…"), Glyphs.Settings,
            () => Finish(() => { if (OpenStartSettings != null) OpenStartSettings(); })));
        menu.Closed += (s, e) => BeginInvoke((Action)menu.Dispose);
        menu.Show(tile, at);
    }

    // Réglage changé depuis le menu : enregistré, puis le menu se reconstruit à la même place
    void Change(Action change)
    {
        change();
        Settings.SaveQuiet();
        Settings.Notify();
        Rebuild();
    }

    void Rebuild()
    {
        if (IsDisposed || finishing) return;
        SuspendLayout();
        foreach (Control c in Controls.Cast<Control>().ToList()) { Controls.Remove(c); c.Dispose(); }
        AutoScroll = false;
        AutoScrollMinSize = Size.Empty;
        Build();
        ResumeLayout();
        if (placedAnchor.HasValue) PlaceAbove(placedAnchor.Value, placedArea);
        if (search != null) ActiveControl = search;
        Invalidate();
    }
}
