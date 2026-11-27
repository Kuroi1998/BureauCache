// BureauCache — StartMenuForm : menu Démarrer de la barre BureauCache (D-013, T-034)
using System;
using System.Drawing;
using System.Windows.Forms;

// Recherche (envoyée au panneau), session, raccourcis, compte (verrouiller, se déconnecter) et alimentation
// (veille, redémarrer, arrêter). Il se ferme dès qu'il perd le focus, ou avec Échap.
class StartMenuForm : Form
{
    public event Action OpenPanel;
    public event Action<string> SearchRequested;
    public static DateTime LastClosed = DateTime.MinValue;

    readonly string userName;
    readonly Bitmap picture;
    TextBox search;
    Rectangle searchRect, headerRect;

    static int Pad { get { return Ui.S(24); } }
    static int Gap { get { return Ui.S(10); } }
    static int MenuWidth { get { return Ui.S(600); } }

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
        int width = MenuWidth, inner = width - 2 * Pad, y = Pad;

        // Recherche : le premier caractère tapé ouvre le panneau avec ce texte, comme dans le menu de Windows
        searchRect = new Rectangle(Pad, y, inner, Ui.S(44));
        search = new TextBox { BorderStyle = BorderStyle.None, Font = Ui.Font("Segoe UI", 11.25f), BackColor = Theme.Surface, ForeColor = Theme.Fg,
                               MaxLength = 256, AccessibleName = L.T("Rechercher", "Search", "Buscar") };
        search.Bounds = new Rectangle(searchRect.Left + Ui.S(42), searchRect.Top + (searchRect.Height - search.PreferredHeight) / 2,
                                      searchRect.Width - Ui.S(54), search.PreferredHeight);
        search.TextChanged += (s, e) =>
        {
            string text = search.Text;
            if (text.Trim().Length == 0) return;
            Close();
            if (SearchRequested != null) SearchRequested(text);
        };
        Controls.Add(search);
        y = searchRect.Bottom + Ui.S(16);

        headerRect = new Rectangle(Pad, y, inner, Ui.S(64));
        y = headerRect.Bottom + Ui.S(16);

        int third = (inner - 2 * Gap) / 3, half = (inner - Gap) / 2, tileH = Ui.S(96);
        y = SectionLabel(L.T("RACCOURCIS", "SHORTCUTS", "ACCESOS"), y);
        AddTile(Glyphs.Search, L.T("Applications et recherche", "Apps and search", "Aplicaciones y búsqueda"), Cell(0, y, third, tileH), true, () =>
        {
            Close();
            if (OpenPanel != null) OpenPanel();
        });
        AddTile(Glyphs.Settings, L.T("Paramètres", "Settings", "Configuración"), Cell(1, y, third, tileH), true, () => Launch("ms-settings:"));
        AddTile(Glyphs.Folder, L.T("Explorateur", "File Explorer", "Explorador"), Cell(2, y, third, tileH), true, () => Launch("explorer.exe"));
        y += tileH + Gap;
        AddTile(Glyphs.Page, L.T("Documents", "Documents", "Documentos"), Cell(0, y, third, tileH), true, () => Launch("shell:Personal"));
        AddTile(Glyphs.Download, L.T("Téléchargements", "Downloads", "Descargas"), Cell(1, y, third, tileH), true, () => Launch("shell:Downloads"));
        AddTile(Glyphs.TaskManager, L.T("Gestionnaire des tâches", "Task Manager", "Administrador de tareas"), Cell(2, y, third, tileH), true, () => Launch("taskmgr.exe"));
        y += tileH + Ui.S(20);

        int rowH = Ui.S(56);
        y = SectionLabel(L.T("COMPTE", "ACCOUNT", "CUENTA"), y);
        AddTile(Glyphs.Lock, L.T("Verrouiller", "Lock", "Bloquear"), new Rectangle(Pad, y, half, rowH), false, () => Run(SessionActions.Lock));
        AddTile(Glyphs.User, L.T("Se déconnecter", "Sign out", "Cerrar sesión"), new Rectangle(Pad + half + Gap, y, half, rowH), false, () => Run(SessionActions.SignOut));
        y += rowH + Ui.S(20);

        bool updates = SessionActions.UpdatesPending;
        y = SectionLabel(L.T("ALIMENTATION", "POWER", "ENERGÍA"), y);
        AddTile(Glyphs.Sleep, L.T("Mettre en veille", "Sleep", "Suspender"), Cell(0, y, third, tileH), true, () => Run(SessionActions.Sleep));
        AddTile(Glyphs.Restart, updates ? L.T("Mettre à jour et redémarrer", "Update and restart", "Actualizar y reiniciar") : L.T("Redémarrer", "Restart", "Reiniciar"),
            Cell(1, y, third, tileH), true, () => Run(() => SessionActions.Restart()));
        AddTile(Glyphs.Power, updates ? L.T("Mettre à jour et arrêter", "Update and shut down", "Actualizar y apagar") : L.T("Arrêter", "Shut down", "Apagar"),
            Cell(2, y, third, tileH), true, () => Run(() => SessionActions.PowerOff()));
        y += tileH + Pad;

        ClientSize = new Size(width, y);
    }

    static Rectangle Cell(int column, int y, int w, int h) { return new Rectangle(Pad + column * (w + Gap), y, w, h); }

    int SectionLabel(string text, int y)
    {
        Label l = new Label { Text = text, AutoSize = false, ForeColor = Theme.FgDim, BackColor = Theme.Bg, Font = Ui.Font("Segoe UI Semibold", 9f),
                              Bounds = new Rectangle(Pad, y, MenuWidth - 2 * Pad, Ui.S(22)) };
        Controls.Add(l);
        return y + Ui.S(28);
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
        // Texte d'invite dans la recherche (EM_SETCUEBANNER), visible même quand elle a le focus
        Native.SendMessage(search.Handle, 0x1501, new IntPtr(1), L.T("Rechercher des applications, des paramètres et des fichiers",
            "Search for apps, settings and files", "Buscar aplicaciones, configuración y archivos"));
        ActiveControl = search;
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

        Ui.Fill(g, Theme.Surface, searchRect, searchRect.Height / 2f);
        Ui.Glyph(g, Glyphs.Search, 12f, Theme.FgDim, new Rectangle(searchRect.Left + Ui.S(12), searchRect.Top, Ui.S(24), searchRect.Height));

        // Session : photo (ou initiales), nom, et le PC sur lequel elle est ouverte
        int size = Ui.S(56);
        Rectangle pic = new Rectangle(headerRect.Left, headerRect.Top + (headerRect.Height - size) / 2, size, size);
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
            TextRenderer.DrawText(g, SessionActions.Initials(userName), Ui.Font("Segoe UI Semibold", 15f), pic, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        int textLeft = pic.Right + Ui.S(16), textW = headerRect.Right - textLeft, mid = headerRect.Top + headerRect.Height / 2;
        TextFormatFlags f = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Bottom;
        TextRenderer.DrawText(g, userName, Ui.Font("Segoe UI Semibold", 14f), new Rectangle(textLeft, headerRect.Top, textW, mid - headerRect.Top), Theme.Fg, f);
        TextRenderer.DrawText(g, L.T("Session sur ", "Signed in on ", "Sesión en ") + Environment.MachineName, Ui.Font("Segoe UI", 9.75f),
            new Rectangle(textLeft, mid + Ui.S(2), textW, Ui.S(22)), Theme.FgDim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using (Pen line = new Pen(Theme.Line)) g.DrawLine(line, Pad, headerRect.Bottom + Ui.S(8), ClientSize.Width - Pad, headerRect.Bottom + Ui.S(8));
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
        Font = Ui.Font("Segoe UI", 10.5f);
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
            int glyphH = Height * 11 / 20;
            Ui.Glyph(g, glyph, 20f, Theme.Fg, new Rectangle(0, Ui.S(6), Width, glyphH - Ui.S(6)));
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Ui.S(6), glyphH, Width - Ui.S(12), Height - glyphH - Ui.S(4)), Theme.Fg,
                f | TextFormatFlags.HorizontalCenter);
        }
        else
        {
            Ui.Glyph(g, glyph, 15f, Theme.Fg, new Rectangle(Ui.S(12), 0, Ui.S(32), Height));
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Ui.S(54), 0, Width - Ui.S(60), Height), Theme.Fg,
                f | TextFormatFlags.VerticalCenter);
        }
        DrawSettingsFocus(g);
    }
}
