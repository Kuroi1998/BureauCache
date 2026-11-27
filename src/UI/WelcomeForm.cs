// BureauCache — WelcomeForm
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Visite guidée au premier lancement (et depuis Paramètres → Général → Aide)
//  Six écrans : le panneau, le raccourci, la recherche, l'organisation, la barre des tâches, les petits plus (T-038)
// =====================================================================
class WelcomeForm : Form
{
    const int Pages = 6;
    int page;
    readonly List<Control>[] pageControls = new List<Control>[Pages];
    readonly FlatButton bNext, bPrev, bSkip;
    readonly Action onFinish;
    readonly Font fTitle = Ui.Font("Segoe UI Semibold", 17f);
    readonly Font fText = Ui.Font("Segoe UI", 10.5f);
    readonly Font fKey = Ui.Font("Segoe UI Semibold", 13f);
    readonly Font fSmall = Ui.Font("Segoe UI", 9.5f);
    Segmented side;
    HotkeyBox hk;
    Toggle taskbar;
    bool settingTaskbar;
    readonly int W, H, IllusH;
    // Mise en page mesurée (retour de l'essai : texte rogné) : décalages depuis le bas de l'illustration, calculés
    // d'après la hauteur réelle de chaque texte, dans la langue et au zoom courants
    readonly int[] controlsOff = new int[Pages], tipsOff = new int[Pages], endOff = new int[Pages];
    int examplesOff;
    const TextFormatFlags Wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
    int TextWidth { get { return W - S(80); } }
    int TipWidth { get { return W - S(104); } }
    static int Measure(string text, Font font, int width) { return TextRenderer.MeasureText(text, font, new Size(width, int.MaxValue), Wrap).Height; }

    // Hauteur des réglages d'un écran (choix du bord et interrupteur, raccourci, interrupteur seul)
    internal static int ControlsHeight(int p) { return p == 0 ? S(86) : p == 1 ? S(48) : p == 4 || p == 5 ? S(38) : 0; }

    void ComputeLayout()
    {
        string[] texts = Texts;
        string[][] tips = Tips;
        for (int p = 0; p < Pages; p++)
        {
            int off = S(68) + Measure(texts[p], fText, TextWidth) + S(14);
            controlsOff[p] = off;
            off += ControlsHeight(p);
            if (p == 2) { examplesOff = off; off += 4 * S(32); }
            tipsOff[p] = off;
            foreach (string t in tips[p]) off += Measure(t, fSmall, TipWidth) + S(8);
            endOff[p] = off;
        }
    }

    static int S(int v) { return Ui.S(v); }
    static string T(string fr, string en, string es = null) { return L.T(fr, en, es); }

    string[] Titles { get { return new[] {
        T("Bienvenue dans BureauCache", "Welcome to BureauCache", "Te damos la bienvenida a BureauCache"),
        T("Un raccourci pour tout", "One shortcut for everything", "Un atajo para todo"),
        T("Cherche tout, même avec des fautes", "Search everything, typos included", "Busca en todo, incluso con erratas"),
        T("Organise à ta façon", "Organize your way", "Organiza a tu manera"),
        T("Ta barre des tâches, en mieux", "A better taskbar", "Una barra de tareas mejor"),
        T("Les petits plus", "Little extras", "Pequeños extras") }; } }

    string[] Texts { get { return new[] {
        T("Tes icônes du bureau sont rangées dans un panneau qui apparaît quand ta souris touche le bord de l'écran. Choisis le bord qui te convient :",
          "Your desktop icons live in a panel that slides in when your mouse touches the edge of the screen. Pick the edge you prefer:",
          "Los iconos del escritorio están en un panel que se abre al tocar el borde con el ratón. Elige el borde que prefieras:"),
        T("Ouvre le panneau depuis n'importe où, même sans la souris. Clique dans le champ pour choisir une autre combinaison.",
          "Open the panel from anywhere, even without the mouse. Click the box to pick another combination.",
          "Abre el panel desde cualquier lugar, incluso sin ratón. Haz clic en el campo para elegir otra combinación."),
        T("Tape ce que tu cherches : bureau, applications, fichiers récents, paramètres Windows, et même le texte des documents du bureau. F1 affiche l'aide et tous les raccourcis.",
          "Type what you're looking for: desktop, apps, recent files, Windows settings, even the text inside desktop documents. F1 shows help and every shortcut.",
          "Escribe lo que buscas: escritorio, aplicaciones, archivos recientes, configuración de Windows e incluso el texto de los documentos del escritorio. F1 muestra la ayuda y todos los atajos."),
        T("Glisse un élément pour le classer ou le mettre en favori. Clic droit pour toutes les options.",
          "Drag an item to sort it or make it a favorite. Right-click for every option.",
          "Arrastra un elemento para ordenarlo o añadirlo a favoritos. Haz clic derecho para ver las opciones."),
        T("En option, BureauCache remplace la barre de Windows : menu Démarrer, fenêtres ouvertes avec aperçu, applications épinglées. Tu peux revenir en arrière à tout moment.",
          "Optionally, BureauCache replaces the Windows taskbar: Start menu, open windows with previews, pinned apps. You can switch back at any time.",
          "Si quieres, BureauCache reemplaza la barra de Windows: menú Inicio, ventanas abiertas con vista previa, aplicaciones ancladas. Puedes volver atrás en cualquier momento."),
        T("Le panneau a aussi des tuiles et des options pratiques. Tout se règle dans les Paramètres (double-clic sur l'icône près de l'horloge).",
          "The panel also has handy tiles and options. Everything is in Settings (double-click the icon by the clock).",
          "El panel también tiene mosaicos y opciones prácticas. Todo se ajusta en Configuración (doble clic en el icono junto al reloj).") }; } }

    // Conseils affichés sous le texte, par écran (aucun pour les écrans 0 à 2)
    string[][] Tips { get { return new[] {
        new string[0], new string[0], new string[0],
        new[] {
            T("Ctrl+1 … 9 ouvre tes favoris, F1 affiche tous les raccourcis.", "Ctrl+1 … 9 opens your favorites, F1 shows every shortcut.", "Ctrl+1 a 9 abre tus favoritos. F1 muestra todos los atajos."),
            T("Suppr supprime, et Ctrl+Z annule si tu t'es trompé.", "Delete removes an item, Ctrl+Z undoes it if you made a mistake.", "Supr elimina un elemento y Ctrl+Z deshace la acción."),
            T("Tire le bord du panneau pour l'élargir, Ctrl+P pour l'épingler.", "Drag the panel's edge to widen it, Ctrl+P to pin it.", "Arrastra el borde del panel para ensancharlo. Pulsa Ctrl+P para fijarlo."),
            T("Rangement automatique des fichiers inutilisés, avec tes propres règles : Paramètres → Organisation.", "Automatic tidy-up of unused files, with your own rules: Settings → Organization.", "Organización automática de archivos sin usar, con tus propias reglas: Configuración → Organización.") },
        new[] {
            T("Démarrer ouvre ton menu : recherche, compte, veille, arrêt.", "Start opens your menu: search, account, sleep, shut down.", "Inicio abre tu menú: búsqueda, cuenta, suspender, apagar."),
            T("Survole une fenêtre pour l'aperçu, clic droit pour l'épingler, glisse les icônes pour les ranger.", "Hover a window for a preview, right-click to pin it, drag icons to reorder them.", "Pasa el ratón por una ventana para verla, clic derecho para anclarla y arrastra los iconos para ordenarlos."),
            T("Pour revenir à la barre Windows : clic droit sur la barre, « Rétablir la barre des tâches de Windows ».", "To go back to the Windows taskbar: right-click the bar, \"Bring back the Windows taskbar\".", "Para volver a la barra de Windows: clic derecho en la barra, «Restablecer la barra de tareas de Windows».") },
        new[] {
            T("Météo et nettoyage du disque : clic droit sur leurs tuiles dans le panneau.", "Weather and disk cleanup: right-click their tiles in the panel.", "Tiempo y limpieza del disco: clic derecho en sus mosaicos del panel."),
            T("Historique du presse-papiers : tes derniers textes copiés (Paramètres → Recherche).", "Clipboard history: your latest copied texts (Settings → Search).", "Historial del portapapeles: tus últimos textos copiados (Configuración → Búsqueda)."),
            T("Clic droit sur l'icône près de l'horloge : Gestionnaire des tâches, mises à jour, visite guidée.", "Right-click the icon by the clock: Task Manager, updates, guided tour.", "Clic derecho en el icono junto al reloj: Administrador de tareas, actualizaciones, visita guiada.") } }; } }

    public WelcomeForm(Action onFinish)
    {
        this.onFinish = onFinish;
        W = S(620); IllusH = S(220);
        ComputeLayout();
        // La fenêtre a la hauteur de l'écran le plus long, plus la pagination et les boutons
        H = IllusH + endOff.Max() + S(104);
        // Petite zone de travail (zoom de 150 % sur un écran de 768 px) : l'illustration rétrécit pour que les
        // boutons du bas restent visibles (R-17)
        int over = H + S(40) - Screen.FromPoint(Cursor.Position).WorkingArea.Height;
        if (over > 0) { int cut = Math.Min(over, IllusH - S(80)); IllusH -= cut; H -= cut; }
        Text = "BureauCache";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(W, H);
        Icon = Ui.MakeIcon(Settings.Accent);
        BackColor = Theme.Bg;
        DoubleBuffered = true;
        KeyPreview = true;
        for (int i = 0; i < Pages; i++) pageControls[i] = new List<Control>();

        int cy = IllusH + controlsOff[0];
        side = new Segmented(T("Gauche", "Left", "Izquierda"), T("Droite", "Right", "Derecha")) { Location = new Point(S(40), cy) };
        side.Selected = Settings.RightSide ? 1 : 0;
        side.Changed += (s, e) => { Settings.RightSide = side.Selected == 1; Settings.Notify(); Invalidate(); };
        Toggle hide = new Toggle { Location = new Point(S(40), cy + S(52)), Checked = Settings.HideIcons };
        hide.Changed += (s, e) => { Settings.HideIcons = hide.Checked; Settings.Notify(); };
        Label hideL = new Label { Text = T("Cacher les icônes du bureau", "Hide desktop icons", "Ocultar los iconos del escritorio"), AutoSize = true, Font = fSmall, Location = new Point(S(94), cy + S(54)) };
        side.AccessibleName = T("Bord de l'écran", "Screen edge", "Borde de la pantalla");
        hide.AccessibleName = hideL.Text;
        AddTo(0, side, hide, hideL);

        hk = new HotkeyBox { Location = new Point(S(40), IllusH + controlsOff[1]), Mods = Settings.HotMods, Key = Settings.HotKey };
        hk.Changed += (s, e) => { Settings.HotMods = hk.Mods; Settings.HotKey = hk.Key; Settings.HotkeyOn = true; Settings.Notify(); Invalidate(); };
        hk.AccessibleName = T("Raccourci clavier", "Keyboard shortcut", "Atajo de teclado");
        AddTo(1, hk);

        // Écran 5 : la même option et la même confirmation que dans les Paramètres (la barre Windows va disparaître)
        cy = IllusH + controlsOff[4];
        taskbar = new Toggle { Location = new Point(S(40), cy), Checked = Settings.TaskbarOn };
        taskbar.Changed += (s, e) => OnTaskbarToggled();
        Settings.Changed += SyncTaskbarToggle;
        Label taskbarL = new Label { Text = TaskbarConsent.Label, AutoSize = true, Font = fSmall, Location = new Point(S(94), cy + S(2)) };
        taskbar.AccessibleName = taskbarL.Text;
        AddTo(4, taskbar, taskbarL);

        cy = IllusH + controlsOff[5];
        Toggle start = new Toggle { Location = new Point(S(40), cy), Checked = Startup.Enabled };
        start.Changed += (s, e) => { try { Startup.Set(start.Checked, Installer.IsInstalled ? Installer.Exe : null); } catch { } };
        Label startL = new Label { Text = T("Lancer BureauCache au démarrage de Windows", "Start BureauCache with Windows", "Iniciar BureauCache con Windows"), AutoSize = true, Font = fSmall, Location = new Point(S(94), cy + S(2)) };
        start.AccessibleName = startL.Text;
        AddTo(5, start, startL);

        bNext = new FlatButton(T("Suivant", "Next", "Siguiente"), true, S(150)) { Location = new Point(W - S(40) - S(150), H - S(62)) };
        bPrev = new FlatButton(T("Précédent", "Back", "Atrás"), false, S(120)) { Location = new Point(bNext.Left - S(10) - S(120), H - S(62)) };
        bSkip = new FlatButton(T("Passer", "Skip", "Omitir"), false, S(100)) { Location = new Point(S(40), H - S(62)) };
        bNext.Click += (s, e) => Next();
        bPrev.Click += (s, e) => SetPage(page - 1);
        bSkip.Click += (s, e) => Close();
        Controls.AddRange(new Control[] { bNext, bPrev, bSkip });
        foreach (Control c in Controls)
        {
            c.BackColor = Theme.Bg;
            if (c is Label) c.ForeColor = Theme.Fg;
            // Tabulation, cadre de focus, Espace et Entrée sur chaque contrôle, comme dans les Paramètres (GEM-08)
            FlatControl flat = c as FlatControl;
            if (flat != null) flat.EnableSettingsKeyboard();
        }
        bNext.TabIndex = 0; bPrev.TabIndex = 1; bSkip.TabIndex = 2;
        SetPage(0);
    }

    // Barre remplacée ou rétablie depuis son menu pendant que la visite est ouverte : l'interrupteur suit, sans
    // reposer la question ni réécrire le réglage (CDX-48)
    void SyncTaskbarToggle()
    {
        if (IsDisposed || taskbar.Checked == Settings.TaskbarOn) return;
        settingTaskbar = true;
        try { taskbar.Checked = Settings.TaskbarOn; }
        finally { settingTaskbar = false; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Settings.Changed -= SyncTaskbarToggle;
        base.Dispose(disposing);
    }

    void OnTaskbarToggled()
    {
        if (settingTaskbar) return;
        if (taskbar.Checked && !Dialogs.Ask(this, TaskbarConsent.Question))
        {
            settingTaskbar = true; taskbar.Checked = false; settingTaskbar = false;
            return;
        }
        Settings.TaskbarOn = taskbar.Checked;
        Settings.Notify();
    }

    void AddTo(int p, params Control[] cs)
    {
        foreach (Control c in cs) { pageControls[p].Add(c); Controls.Add(c); }
    }

    void Next() { if (page == Pages - 1) Finish(); else SetPage(page + 1); }

    void SetPage(int p)
    {
        page = Math.Max(0, Math.Min(Pages - 1, p));
        for (int i = 0; i < Pages; i++) foreach (Control c in pageControls[i]) c.Visible = i == page;
        bPrev.Visible = page > 0;
        bNext.Text = page == Pages - 1 ? T("C'est parti !", "Let's go!", "¡Empezar!") : T("Suivant", "Next", "Siguiente");
        if (ActiveControl == null || !ActiveControl.Visible) bNext.Focus();
        Invalidate();
    }

    void Finish()
    {
        Close();
        if (onFinish != null) onFinish();
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Ui.ThemeTitleBar(Handle); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (HotkeyBox.Capturing) { base.OnKeyDown(e); return; }
        // Le contrôle qui a le focus traite d'abord ses touches : Entrée et Espace sur un bouton ou un interrupteur,
        // les flèches sur un choix segmenté. Sinon Entrée et → avancent, ← recule (GEM-08).
        Control focus = ActiveControl;
        bool ownsEnter = focus is FlatControl && !(focus is Segmented);
        bool ownsArrows = focus is Segmented;
        if ((e.KeyCode == Keys.Right && !ownsArrows) || (e.KeyCode == Keys.Enter && !ownsEnter)) { Next(); e.Handled = true; }
        else if (e.KeyCode == Keys.Left && !ownsArrows) { SetPage(page - 1); e.Handled = true; }
        else if (e.KeyCode == Keys.Escape) Close();
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle ill = new Rectangle(0, 0, W, IllusH);
        using (LinearGradientBrush b = new LinearGradientBrush(ill, Theme.Accent, Theme.Mix(Theme.Accent, Color.Black, 0.55f), 35f)) g.FillRectangle(b, ill);
        // Bandeau réduit sur une petite zone de travail (R-17) : les dessins, prévus pour S(220), sont mis à
        // l'échelle et centrés pour ne pas déborder sur le titre (Q26-09)
        float k = Math.Min(1f, IllusH / (float)S(220));
        g.SetClip(ill);
        g.TranslateTransform(W * (1 - k) / 2, 0);
        g.ScaleTransform(k, k);
        switch (page)
        {
            case 0: DrawScreen(g); break;
            case 1: DrawKeys(g); break;
            case 2: DrawSearch(g); break;
            case 3: DrawTiles(g); break;
            case 4: DrawTaskbar(g); break;
            case 5: DrawExtras(g); break;
        }
        g.ResetTransform();
        g.ResetClip();
        TextRenderer.DrawText(g, Titles[page], fTitle, new Point(S(38), IllusH + S(26)), Theme.Fg, TextFormatFlags.NoPrefix);
        string text = Texts[page];
        TextRenderer.DrawText(g, text, fText, new Rectangle(S(40), IllusH + S(68), TextWidth, Measure(text, fText, TextWidth) + S(4)), Theme.FgDim, Wrap);
        if (page == 2) DrawExamples(g, IllusH + examplesOff);
        string[] tips = Tips[page];
        if (tips.Length > 0) DrawTips(g, IllusH + tipsOff[page], tips);
        int step = S(22), x0 = W / 2 - (Pages * step + S(14)) / 2;
        for (int i = 0; i < Pages; i++)
        {
            int d = i == page ? S(22) : S(8);
            int x = x0 + i * step + (i > page ? S(14) : 0);
            Ui.Fill(g, i == page ? Theme.Accent : Theme.Track, new Rectangle(x, H - S(90), d, S(8)), S(4));
        }
    }

    void DrawScreen(Graphics g)
    {
        Rectangle mon = new Rectangle(W / 2 - S(150), S(30), S(300), S(150));
        Ui.Fill(g, Color.FromArgb(60, 0, 0, 0), new Rectangle(mon.X + S(4), mon.Y + S(6), mon.Width, mon.Height), S(10));
        Ui.Fill(g, Color.FromArgb(235, 250, 250, 252), mon, S(10));
        Ui.Fill(g, Color.FromArgb(90, 255, 255, 255), new Rectangle(W / 2 - S(30), mon.Bottom, S(60), S(14)), S(3));
        bool right = Settings.RightSide;
        Rectangle panel = new Rectangle(right ? mon.Right - S(80) : mon.X, mon.Y, S(80), mon.Height);
        using (GraphicsPath p = Ui.Round(mon, S(10)))
        {
            g.SetClip(p);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 34, 40))) g.FillRectangle(b, panel);
            g.ResetClip();
        }
        for (int i = 0; i < 5; i++)
        {
            Ui.Fill(g, Swatches.Presets[(i * 2) % Swatches.Presets.Length], new Rectangle(panel.X + S(10), panel.Y + S(14) + i * S(26), S(16), S(16)), S(4));
            Ui.Fill(g, Color.FromArgb(120, 255, 255, 255), new Rectangle(panel.X + S(32), panel.Y + S(19) + i * S(26), S(38), S(6)), S(3));
        }
        int cx = right ? mon.Right - S(2) : mon.X + S(2), cyy = mon.Y + S(80);
        Point[] arrow = right
            ? new[] { new Point(cx, cyy), new Point(cx - S(14), cyy - S(10)), new Point(cx - S(14), cyy + S(10)) }
            : new[] { new Point(cx, cyy), new Point(cx + S(14), cyy - S(10)), new Point(cx + S(14), cyy + S(10)) };
        using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillPolygon(b, arrow);
    }

    void DrawKeys(Graphics g)
    {
        string combo = HotkeyBox.Format(Settings.HotMods, Settings.HotKey);
        string[] keys = combo.Split(new[] { " + " }, StringSplitOptions.None);
        int[] widths = keys.Select(k => Math.Max(S(64), TextRenderer.MeasureText(k, fKey).Width + S(30))).ToArray();
        int total = widths.Sum() + (keys.Length - 1) * S(30);
        int x = (W - total) / 2, y = S(70);
        for (int i = 0; i < keys.Length; i++)
        {
            Rectangle k = new Rectangle(x, y, widths[i], S(70));
            Ui.Fill(g, Color.FromArgb(70, 0, 0, 0), new Rectangle(k.X, k.Y + S(6), k.Width, k.Height), S(12));
            Ui.Fill(g, Color.FromArgb(250, 250, 252), k, S(12));
            TextRenderer.DrawText(g, keys[i], fKey, k, Color.FromArgb(30, 30, 36), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            x += widths[i];
            if (i < keys.Length - 1)
            {
                TextRenderer.DrawText(g, "+", fKey, new Rectangle(x, y, S(30), S(70)), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                x += S(30);
            }
        }
    }

    void DrawSearch(Graphics g)
    {
        Rectangle bar = new Rectangle(W / 2 - S(170), S(46), S(340), S(44));
        Ui.Fill(g, Color.FromArgb(60, 0, 0, 0), new Rectangle(bar.X, bar.Y + S(5), bar.Width, bar.Height), S(10));
        Ui.Fill(g, Color.FromArgb(34, 34, 40), bar, S(10));
        Ui.Glyph(g, Glyphs.Search, 11f, Color.FromArgb(170, 170, 180), new Rectangle(bar.X + S(8), bar.Y, S(30), bar.Height));
        TextRenderer.DrawText(g, "asasin", fText, new Rectangle(bar.X + S(42), bar.Y, S(200), bar.Height), Color.White, TextFormatFlags.VerticalCenter);
        Rectangle res = new Rectangle(bar.X, bar.Bottom + S(10), bar.Width, S(52));
        Ui.Fill(g, Color.FromArgb(44, 44, 52), res, S(10));
        Ui.Fill(g, Color.FromArgb(200, 30, 30), new Rectangle(res.X + S(12), res.Y + S(10), S(32), S(32)), S(6));
        TextRenderer.DrawText(g, "Assassin's Creed Shadows", fText, new Rectangle(res.X + S(56), res.Y, S(260), res.Height), Color.White, TextFormatFlags.VerticalCenter);
    }

    void DrawTiles(Graphics g)
    {
        string[] glyphs = { Glyphs.StarFilled, Glyphs.Game, Glyphs.Apps, Glyphs.Folder, Glyphs.Book };
        int size = S(70), gap = S(18), x = (W - (size * 5 + gap * 4)) / 2;
        for (int i = 0; i < 5; i++)
        {
            Rectangle r = new Rectangle(x + i * (size + gap), S(60) + (i % 2 == 0 ? 0 : S(20)), size, size);
            Ui.Fill(g, Color.FromArgb(60, 0, 0, 0), new Rectangle(r.X, r.Y + S(6), r.Width, r.Height), S(14));
            Ui.Fill(g, i == 0 ? Color.FromArgb(250, 204, 21) : Swatches.Presets[i % Swatches.Presets.Length], r, S(14));
            Ui.Glyph(g, glyphs[i], 18f, Color.White, r);
        }
    }

    // Écran 5 : un écran avec la barre BureauCache en bas et son menu Démarrer ouvert
    void DrawTaskbar(Graphics g)
    {
        Rectangle mon = new Rectangle(W / 2 - S(170), S(24), S(340), S(170));
        Ui.Fill(g, Color.FromArgb(60, 0, 0, 0), new Rectangle(mon.X + S(4), mon.Y + S(6), mon.Width, mon.Height), S(10));
        Ui.Fill(g, Color.FromArgb(235, 250, 250, 252), mon, S(10));
        Rectangle bar = new Rectangle(mon.X, mon.Bottom - S(26), mon.Width, S(26));
        using (GraphicsPath p = Ui.Round(mon, S(10)))
        {
            g.SetClip(p, CombineMode.Intersect);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(28, 28, 34))) g.FillRectangle(b, bar);
            g.ResetClip();
            g.SetClip(new Rectangle(0, 0, W, IllusH));
        }
        // Menu Démarrer ouvert au-dessus du bouton
        Rectangle menu = new Rectangle(mon.X + S(8), mon.Y + S(14), S(130), bar.Top - mon.Y - S(20));
        Ui.Fill(g, Color.FromArgb(40, 40, 48), menu, S(8));
        Ui.Fill(g, Color.FromArgb(70, 255, 255, 255), new Rectangle(menu.X + S(8), menu.Y + S(8), menu.Width - S(16), S(12)), S(6));
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 3; c++)
                Ui.Fill(g, Color.FromArgb(60, 60, 70), new Rectangle(menu.X + S(8) + c * S(40), menu.Y + S(28) + r * S(32), S(34), S(26)), S(4));
        Ui.Fill(g, Color.FromArgb(90, 90, 100), new Rectangle(menu.X + S(8), menu.Bottom - S(18), menu.Width - S(16), S(10)), S(4));
        // Barre : logo Démarrer, fenêtres (la première active), heure
        Ui.DrawLogo(g, new Rectangle(bar.X + S(8), bar.Y + S(4), S(18), S(18)), Theme.Accent);
        for (int i = 0; i < 4; i++)
        {
            Rectangle ic = new Rectangle(bar.X + S(36) + i * S(26), bar.Y + S(4), S(16), S(16));
            Ui.Fill(g, Swatches.Presets[(i * 3 + 1) % Swatches.Presets.Length], ic, S(4));
            int lw = i == 0 ? S(10) : S(4);
            Ui.Fill(g, i == 0 ? Theme.Accent : Color.FromArgb(150, 150, 160), new Rectangle(ic.X + (ic.Width - lw) / 2, bar.Bottom - S(4), lw, S(2)), S(1));
        }
        TextRenderer.DrawText(g, DateTime.Now.ToString("t", L.Culture), fSmall, new Rectangle(bar.Right - S(60), bar.Y, S(54), bar.Height), Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        // Aperçu au survol de la deuxième fenêtre
        Rectangle prev = new Rectangle(bar.X + S(36) + S(26) - S(30), bar.Top - S(58), S(76), S(50));
        Ui.Fill(g, Color.FromArgb(40, 40, 48), prev, S(6));
        Ui.Fill(g, Color.FromArgb(225, 230, 240), new Rectangle(prev.X + S(6), prev.Y + S(14), prev.Width - S(12), prev.Height - S(20)), S(3));
    }

    // Écran 6 : tuiles météo, disque et presse-papiers
    void DrawExtras(Graphics g)
    {
        string[] glyphs = { Glyphs.World, Glyphs.Drive, Glyphs.Copy, Glyphs.TaskManager };
        string[] labels = { "21°", "130 Go", "Ctrl+C", "" };
        int size = S(84), gap = S(20), x = (W - (size * 4 + gap * 3)) / 2;
        for (int i = 0; i < 4; i++)
        {
            Rectangle r = new Rectangle(x + i * (size + gap), S(56) + (i % 2 == 0 ? 0 : S(16)), size, size);
            Ui.Fill(g, Color.FromArgb(60, 0, 0, 0), new Rectangle(r.X, r.Y + S(6), r.Width, r.Height), S(14));
            Ui.Fill(g, Color.FromArgb(40, 40, 48), r, S(14));
            Ui.Glyph(g, glyphs[i], 20f, Color.White, new Rectangle(r.X, r.Y + S(8), r.Width, r.Height / 2));
            if (labels[i].Length > 0)
                TextRenderer.DrawText(g, labels[i], fSmall, new Rectangle(r.X, r.Y + r.Height / 2 + S(4), r.Width, S(24)), Color.FromArgb(210, 210, 220),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
        }
    }

    void DrawExamples(Graphics g, int y)
    {
        string[][] ex = {
            new[] { "steam", T("tes jeux et applications", "your games and apps", "tus juegos y aplicaciones") },
            new[] { "= 12*7", T("calcule (Entrée copie le résultat)", "calculates (Enter copies the result)", "calcula (Intro copia el resultado)") },
            new[] { T("? météo", "? weather", "? tiempo"), T("cherche sur le web", "searches the web", "busca en la web") },
            new[] { ">", T("commandes : verrouiller, veille, redémarrer…", "commands: lock, sleep, restart…", "comandos: bloquear, suspender, reiniciar…") } };
        foreach (string[] e in ex)
        {
            Rectangle pill = new Rectangle(S(40), y, S(96), S(26));
            Ui.Fill(g, Theme.Surface, pill, S(6));
            TextRenderer.DrawText(g, e[0], fSmall, pill, Theme.Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, e[1], fSmall, new Rectangle(pill.Right + S(12), y, W - pill.Right - S(52), S(26)), Theme.FgDim, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            y += S(32);
        }
    }

    // Conseils : chacun a la hauteur de son texte, le suivant commence dessous
    void DrawTips(Graphics g, int y, string[] tips)
    {
        foreach (string t in tips)
        {
            int h = Measure(t, fSmall, TipWidth);
            Ui.Glyph(g, Glyphs.Check, 9f, Theme.Accent, new Rectangle(S(36), y, S(24), fSmall.Height + S(4)));
            TextRenderer.DrawText(g, t, fSmall, new Rectangle(S(64), y + S(2), TipWidth, h + S(2)), Theme.Fg, Wrap);
            y += h + S(8);
        }
    }

    // Bas du contenu d'un écran (texte, réglages, exemples, conseils), pour les tests
    internal int ContentBottom(int p) { return IllusH + endOff[p]; }
    internal int TextBottom(int p) { return IllusH + S(68) + Measure(Texts[p], fText, TextWidth); }
    internal int ControlsTop(int p) { return IllusH + controlsOff[p]; }
    internal int TipsBottom(int p) { return ContentBottom(p); }

    internal int DotsTop { get { return H - S(90); } }
    internal int PageCount { get { return Pages; } }
}
