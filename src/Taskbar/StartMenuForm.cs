// BureauCache — StartMenuForm : menu Démarrer de la barre BureauCache (D-013, T-034)
using System;
using System.Drawing;
using System.Windows.Forms;

// Raccourcis (panneau BureauCache, Paramètres, Explorateur), compte (verrouiller, se déconnecter) et alimentation
// (veille, redémarrer, arrêter). Il se ferme dès qu'il perd le focus, ou avec Échap.
class StartMenuForm : Form
{
    public event Action OpenPanel;
    public static DateTime LastClosed = DateTime.MinValue;

    readonly string userName;
    readonly Bitmap picture;
    int headerHeight;

    public StartMenuForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = Theme.Bg;
        Text = L.T("Menu Démarrer", "Start menu", "Menú Inicio");
        AccessibleName = Text;
        userName = Native.UserDisplayName();
        picture = SessionActions.Picture();
        Build();
    }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 /*WS_EX_TOOLWINDOW*/; return cp; }
    }

    void Build()
    {
        int pad = Ui.S(16), width = Ui.S(372), y = pad;
        headerHeight = Ui.S(56);
        y += headerHeight + Ui.S(8);

        y = SectionLabel(L.T("RACCOURCIS", "SHORTCUTS", "ACCESOS"), y);
        int tileW = (width - 2 * pad - 2 * Ui.S(8)) / 3, tileH = Ui.S(76);
        AddTile(Glyphs.Search, L.T("Applications et recherche", "Apps and search", "Aplicaciones y búsqueda"), new Rectangle(pad, y, tileW, tileH), true, () =>
        {
            Close();
            if (OpenPanel != null) OpenPanel();
        });
        AddTile(Glyphs.Settings, L.T("Paramètres", "Settings", "Configuración"), new Rectangle(pad + tileW + Ui.S(8), y, tileW, tileH), true,
            () => Launch("ms-settings:"));
        AddTile(Glyphs.Folder, L.T("Explorateur", "File Explorer", "Explorador"), new Rectangle(pad + 2 * (tileW + Ui.S(8)), y, tileW, tileH), true,
            () => Launch("explorer.exe"));
        y += tileH + Ui.S(14);

        int half = (width - 2 * pad - Ui.S(8)) / 2, rowH = Ui.S(40);
        y = SectionLabel(L.T("COMPTE", "ACCOUNT", "CUENTA"), y);
        AddTile(Glyphs.Lock, L.T("Verrouiller", "Lock", "Bloquear"), new Rectangle(pad, y, half, rowH), false, () => Run(SessionActions.Lock));
        AddTile(Glyphs.User, L.T("Se déconnecter", "Sign out", "Cerrar sesión"), new Rectangle(pad + half + Ui.S(8), y, half, rowH), false, () => Run(SessionActions.SignOut));
        y += rowH + Ui.S(14);

        bool updates = SessionActions.UpdatesPending;
        int third = (width - 2 * pad - 2 * Ui.S(8)) / 3;
        y = SectionLabel(L.T("ALIMENTATION", "POWER", "ENERGÍA"), y);
        AddTile(Glyphs.Sleep, L.T("Mettre en veille", "Sleep", "Suspender"), new Rectangle(pad, y, third, Ui.S(64)), true, () => Run(SessionActions.Sleep));
        AddTile(Glyphs.Restart, updates ? L.T("Mettre à jour et redémarrer", "Update and restart", "Actualizar y reiniciar") : L.T("Redémarrer", "Restart", "Reiniciar"),
            new Rectangle(pad + third + Ui.S(8), y, third, Ui.S(64)), true, () => Run(() => SessionActions.Restart()));
        AddTile(Glyphs.Power, updates ? L.T("Mettre à jour et arrêter", "Update and shut down", "Actualizar y apagar") : L.T("Arrêter", "Shut down", "Apagar"),
            new Rectangle(pad + 2 * (third + Ui.S(8)), y, third, Ui.S(64)), true, () => Run(() => SessionActions.PowerOff()));
        y += Ui.S(64) + pad;

        ClientSize = new Size(width, y);
    }

    int SectionLabel(string text, int y)
    {
        Label l = new Label { Text = text, AutoSize = false, ForeColor = Theme.FgDim, BackColor = Theme.Bg, Font = Ui.Font("Segoe UI Semibold", 8.25f),
                              Bounds = new Rectangle(Ui.S(16), y, Ui.S(340), Ui.S(18)) };
        Controls.Add(l);
        return y + Ui.S(22);
    }

    void AddTile(string glyph, string text, Rectangle bounds, bool stacked, Action click)
    {
        StartTile t = new StartTile(glyph, text, stacked) { Bounds = bounds };
        t.Click += (s, e) => click();
        Controls.Add(t);
    }

    void Launch(string target)
    {
        Close();
        try { ShellUtil.Open(target, null); } catch { }
    }

    // L'action part après la fermeture du menu (verrouiller ou se mettre en veille avec le menu ouvert le laisserait au retour)
    void Run(Action action)
    {
        Close();
        try { action(); } catch { }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Native.SetRoundCorners(Handle);
        Native.SetDarkTitleBar(Handle, Settings.Dark);
    }

    protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); if (Visible) Close(); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); return; }
        base.OnKeyDown(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        LastClosed = DateTime.Now;
        if (picture != null) picture.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Ui.Edge(g, Theme.EdgeHot, new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Ui.S(Ui.RadiusCard));

        // En-tête : photo (ou initiales) et nom de la session
        int pad = Ui.S(16), size = Ui.S(44);
        Rectangle pic = new Rectangle(pad, pad + (headerHeight - size) / 2, size, size);
        if (picture != null)
        {
            using (System.Drawing.Drawing2D.GraphicsPath p = new System.Drawing.Drawing2D.GraphicsPath())
            {
                p.AddEllipse(pic);
                g.SetClip(p);
                g.DrawImage(picture, pic);
                g.ResetClip();
            }
        }
        else
        {
            using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, pic);
            TextRenderer.DrawText(g, SessionActions.Initials(userName), Ui.Font("Segoe UI Semibold", 12f), pic, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        Rectangle name = new Rectangle(pic.Right + Ui.S(12), pic.Top, ClientSize.Width - pic.Right - Ui.S(28), size);
        TextRenderer.DrawText(g, userName, Ui.Font("Segoe UI Semibold", 11.25f), name, Theme.Fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using (Pen line = new Pen(Theme.Line)) g.DrawLine(line, pad, pad + headerHeight, ClientSize.Width - pad, pad + headerHeight);
    }
}

// Tuile du menu : icône et texte, au clavier (Tab, Entrée, Espace) et lue par les lecteurs d'écran comme un bouton
class StartTile : FlatControl
{
    readonly string glyph;
    readonly bool stacked;   // icône au-dessus du texte, sinon à gauche
    bool hover;

    protected override AccessibleRole FlatRole { get { return AccessibleRole.PushButton; } }
    protected override string FlatDefaultAction { get { return L.T("Appuyer", "Press", "Pulsar"); } }

    public StartTile(string glyph, string text, bool stacked)
    {
        this.glyph = glyph;
        this.stacked = stacked;
        Text = text;
        Font = Ui.Font("Segoe UI", 9f);
        BackColor = Theme.Bg;
        EnableSettingsKeyboard();
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Enter || base.IsInputKey(keyData); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; OnClick(EventArgs.Empty); }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
        Ui.Fill(g, hover || Focused ? Theme.Hover : Theme.Surface, r, Ui.S(Ui.RadiusControl));
        TextFormatFlags f = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak;
        if (stacked)
        {
            Ui.Glyph(g, glyph, 15f, Theme.Fg, new Rectangle(0, Ui.S(8), Width, Height / 2 - Ui.S(4)));
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Ui.S(4), Height / 2, Width - Ui.S(8), Height / 2 - Ui.S(4)), Theme.Fg,
                f | TextFormatFlags.HorizontalCenter);
        }
        else
        {
            Ui.Glyph(g, glyph, 12f, Theme.Fg, new Rectangle(Ui.S(8), 0, Ui.S(28), Height));
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Ui.S(40), 0, Width - Ui.S(44), Height), Theme.Fg,
                f | TextFormatFlags.VerticalCenter);
        }
        DrawSettingsFocus(g);
    }
}
