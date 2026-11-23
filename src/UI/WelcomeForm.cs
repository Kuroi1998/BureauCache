// BureauCache — WelcomeForm
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Visite guidée au premier lancement
// =====================================================================
class WelcomeForm : Form
{
    int page;
    readonly List<Control>[] pageControls = new List<Control>[4];
    readonly FlatButton bNext, bPrev, bSkip;
    readonly Action onFinish;
    readonly Font fTitle = Ui.Font("Segoe UI Semibold", 17f);
    readonly Font fText = Ui.Font("Segoe UI", 10.5f);
    readonly Font fKey = Ui.Font("Segoe UI Semibold", 13f);
    readonly Font fSmall = Ui.Font("Segoe UI", 9.5f);
    Segmented side;
    HotkeyBox hk;
    readonly int W, H, IllusH;

    static int S(int v) { return Ui.S(v); }
    static string T(string fr, string en, string es = null) { return L.T(fr, en, es); }

    string[] Titles { get { return new[] {
        T("Bienvenue dans BureauCache", "Welcome to BureauCache", "Te damos la bienvenida a BureauCache"),
        T("Un raccourci pour tout", "One shortcut for everything", "Un atajo para todo"),
        T("Cherche tout, même avec des fautes", "Search everything, typos included", "Busca en todo, incluso con erratas"),
        T("Organise à ta façon", "Organize your way", "Organiza a tu manera") }; } }

    string[] Texts { get { return new[] {
        T("Tes icônes du bureau sont rangées dans un panneau qui apparaît quand ta souris touche le bord de l'écran. Choisis le bord qui te convient :",
          "Your desktop icons live in a panel that slides in when your mouse touches the edge of the screen. Pick the edge you prefer:", "Los iconos del escritorio están en un panel que se abre al tocar el borde con el ratón. Elige el borde que prefieras:"),
        T("Ouvre le panneau depuis n'importe où, même sans la souris. Clique dans le champ pour choisir une autre combinaison.",
          "Open the panel from anywhere, even without the mouse. Click the box to pick another combination.", "Abre el panel desde cualquier lugar, incluso sin ratón. Haz clic en el campo para elegir otra combinación."),
        T("Tape simplement ce que tu cherches : bureau, applications, fichiers récents, paramètres Windows…",
          "Just type what you're looking for: desktop, apps, recent files, Windows settings…", "Escribe lo que buscas: escritorio, aplicaciones, archivos recientes, configuración de Windows…"),
        T("Glisse un élément pour le classer ou le mettre en favori. Clic droit pour toutes les options.",
          "Drag an item to sort it or make it a favorite. Right-click for every option.", "Arrastra un elemento para ordenarlo o añadirlo a favoritos. Haz clic derecho para ver las opciones.") }; } }

    public WelcomeForm(Action onFinish)
    {
        this.onFinish = onFinish;
        W = S(620); H = S(580); IllusH = S(220);
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
        for (int i = 0; i < 4; i++) pageControls[i] = new List<Control>();

        int cy = IllusH + S(130);
        side = new Segmented(T("Gauche", "Left", "Izquierda"), T("Droite", "Right", "Derecha")) { Location = new Point(S(40), cy) };
        side.Selected = Settings.RightSide ? 1 : 0;
        side.Changed += (s, e) => { Settings.RightSide = side.Selected == 1; Settings.Notify(); Invalidate(); };
        Toggle hide = new Toggle { Location = new Point(S(40), cy + S(52)), Checked = Settings.HideIcons };
        hide.Changed += (s, e) => { Settings.HideIcons = hide.Checked; Settings.Notify(); };
        Label hideL = new Label { Text = T("Cacher les icônes du bureau", "Hide desktop icons", "Ocultar los iconos del escritorio"), AutoSize = true, Font = fSmall, Location = new Point(S(94), cy + S(54)) };
        AddTo(0, side, hide, hideL);

        hk = new HotkeyBox { Location = new Point(S(40), cy), Mods = Settings.HotMods, Key = Settings.HotKey };
        hk.Changed += (s, e) => { Settings.HotMods = hk.Mods; Settings.HotKey = hk.Key; Settings.HotkeyOn = true; Settings.Notify(); Invalidate(); };
        AddTo(1, hk);

        Toggle start = new Toggle { Location = new Point(S(40), cy + S(66)), Checked = Startup.Enabled };
        start.Changed += (s, e) => { try { Startup.Set(start.Checked, Installer.IsInstalled ? Installer.Exe : null); } catch { } };
        Label startL = new Label { Text = T("Lancer BureauCache au démarrage de Windows", "Start BureauCache with Windows", "Iniciar BureauCache con Windows"), AutoSize = true, Font = fSmall, Location = new Point(S(94), cy + S(68)) };
        AddTo(3, start, startL);

        bNext = new FlatButton(T("Suivant", "Next", "Siguiente"), true, S(150)) { Location = new Point(W - S(40) - S(150), H - S(62)) };
        bPrev = new FlatButton(T("Précédent", "Back", "Atrás"), false, S(120)) { Location = new Point(bNext.Left - S(10) - S(120), H - S(62)) };
        bSkip = new FlatButton(T("Passer", "Skip", "Omitir"), false, S(100)) { Location = new Point(S(40), H - S(62)) };
        bNext.Click += (s, e) => { if (page == 3) Finish(); else SetPage(page + 1); };
        bPrev.Click += (s, e) => SetPage(page - 1);
        bSkip.Click += (s, e) => Close();
        Controls.AddRange(new Control[] { bNext, bPrev, bSkip });
        foreach (Control c in Controls)
        {
            c.BackColor = Theme.Bg;
            if (c is Label) c.ForeColor = Theme.Fg;
        }
        SetPage(0);
    }

    void AddTo(int p, params Control[] cs)
    {
        foreach (Control c in cs) { pageControls[p].Add(c); Controls.Add(c); }
    }

    void SetPage(int p)
    {
        page = Math.Max(0, Math.Min(3, p));
        for (int i = 0; i < 4; i++) foreach (Control c in pageControls[i]) c.Visible = i == page;
        bPrev.Visible = page > 0;
        bNext.Text = page == 3 ? T("C'est parti !", "Let's go!", "¡Empezar!") : T("Suivant", "Next", "Siguiente");
        Invalidate();
    }

    void Finish()
    {
        Close();
        if (onFinish != null) onFinish();
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.SetDarkTitleBar(Handle, Settings.Dark); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (HotkeyBox.Capturing) { base.OnKeyDown(e); return; }
        if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Enter) { if (page == 3) Finish(); else SetPage(page + 1); e.Handled = true; }
        else if (e.KeyCode == Keys.Left) { SetPage(page - 1); e.Handled = true; }
        else if (e.KeyCode == Keys.Escape) Close();
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle ill = new Rectangle(0, 0, W, IllusH);
        using (LinearGradientBrush b = new LinearGradientBrush(ill, Theme.Accent, Theme.Mix(Theme.Accent, Color.Black, 0.55f), 35f)) g.FillRectangle(b, ill);
        switch (page)
        {
            case 0: DrawScreen(g); break;
            case 1: DrawKeys(g); break;
            case 2: DrawSearch(g); break;
            case 3: DrawTiles(g); break;
        }
        TextRenderer.DrawText(g, Titles[page], fTitle, new Point(S(38), IllusH + S(26)), Theme.Fg, TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, Texts[page], fText, new Rectangle(S(40), IllusH + S(68), W - S(80), S(60)), Theme.FgDim,
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        if (page == 2) DrawExamples(g, IllusH + S(126));
        if (page == 3) DrawTips(g, IllusH + S(118));
        for (int i = 0; i < 4; i++)
        {
            int d = i == page ? S(22) : S(8);
            int x = W / 2 - S(40) + i * S(22) + (i > page ? S(14) : 0);
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
        int total = 0;
        int[] widths = keys.Select(k => Math.Max(S(64), TextRenderer.MeasureText(k, fKey).Width + S(30))).ToArray();
        total = widths.Sum() + (keys.Length - 1) * S(30);
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

    void DrawTips(Graphics g, int y)
    {
        string[] tips = {
            T("Ctrl+1 … 9 ouvre tes favoris, F1 affiche tous les raccourcis.", "Ctrl+1 … 9 opens your favorites, F1 shows every shortcut.", "Ctrl+1 a 9 abre tus favoritos. F1 muestra todos los atajos."),
            T("Suppr supprime, et Ctrl+Z annule si tu t'es trompé.", "Delete removes an item, Ctrl+Z undoes it if you made a mistake.", "Supr elimina un elemento y Ctrl+Z deshace la acción."),
            T("Tire le bord du panneau pour l'élargir, Ctrl+P pour l'épingler.", "Drag the panel's edge to widen it, Ctrl+P to pin it.", "Arrastra el borde del panel para ensancharlo. Pulsa Ctrl+P para fijarlo.") };
        foreach (string t in tips)
        {
            Ui.Glyph(g, Glyphs.Check, 9f, Theme.Accent, new Rectangle(S(36), y, S(24), S(22)));
            TextRenderer.DrawText(g, t, fSmall, new Rectangle(S(64), y, W - S(104), S(22)), Theme.Fg, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            y += S(24);
        }
    }
}
