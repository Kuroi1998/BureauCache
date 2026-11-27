// BureauCache — StartMenuForm : menu Démarrer de la barre BureauCache (D-013, T-034)
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Recherche (envoyée au panneau), session, puis quatre sections : BureauCache (panneau, réglages, visite, quitter), Windows
// (outils du système), session Windows (verrouiller, se déconnecter) et alimentation (veille, redémarrer, arrêter). Il se ferme dès qu'il perd le focus, ou avec Échap.
partial class StartMenuForm : Form
{
    public event Action OpenPanel, OpenSettings, OpenTour, QuitRequested;
    public event Action<string> SearchRequested;
    static readonly System.Diagnostics.Stopwatch sinceClosed = new System.Diagnostics.Stopwatch();
    public static bool JustClosed { get { return sinceClosed.IsRunning && sinceClosed.ElapsedMilliseconds < 250; } }

    readonly string userName;
    readonly Bitmap picture;
    TextBox search;
    Rectangle searchRect, headerRect;
    int menuWidth;
    Rectangle? placedAnchor;
    Rectangle placedArea;

    static int Pad { get { return Ui.S(20); } }
    static int Gap { get { return Ui.S(8); } }
    static int MinimumWidth { get { return Ui.S(540); } }
    static Font TileFont { get { return Ui.Font("Segoe UI", 10f); } }

    // Largeur d'une colonne pour que chaque texte tienne en deux lignes au plus sans couper un mot. Essai du
    // 2026-11-27 : « Télécharge… », « Panneau de configurati… » et « Paramètres de » sans la suite à l'échelle de
    // l'écran du chef de projet, avec une largeur fixe.
    internal static int ColumnWidth(string[] texts, int minimum)
    {
        int w = minimum;
        while (w < Ui.S(320) && !texts.All(t => StartTile.FitsBeside(t, TileFont, w))) w += Ui.S(4);
        return w;
    }

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

    static Rectangle Cell(int column, int y, int w, int h) { return new Rectangle(Pad + column * (w + Gap), y, w, h); }

    public void PlaceAbove(Rectangle anchor, Rectangle area)
    {
        placedAnchor = anchor;
        placedArea = area;
        Size content = ClientSize;
        int bottom = Math.Max(area.Top + 1, Math.Min(area.Bottom, anchor.Top - Ui.S(8)));
        int height = Math.Min(content.Height, bottom - area.Top);
        int width = Math.Min(area.Width, content.Width + (height < content.Height ? SystemInformation.VerticalScrollBarWidth : 0));
        if (width < content.Width || height < content.Height)
        {
            AutoScroll = true;
            AutoScrollMinSize = content;
            ClientSize = new Size(width, height);
        }
        Location = new Point(Math.Max(area.Left, Math.Min(anchor.Left, area.Right - Width)), bottom - Height);
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        Invalidate();
    }

    // Titre de section en capitales, suivi d'une légende plus discrète qui dit ce que la section ouvre
    int SectionLabel(string title, string hint, int y)
    {
        Font bold = Ui.Font("Segoe UI Semibold", 9f), light = Ui.Font("Segoe UI", 9f);
        int titleW = TextRenderer.MeasureText(title, bold).Width;
        Label l = new Label { Text = title, AutoSize = false, ForeColor = Theme.Fg, BackColor = Theme.Bg, Font = bold,
                              Bounds = new Rectangle(Pad, y, titleW + Ui.S(4), Ui.S(20)) };
        Label h = new Label { Text = "· " + hint, AutoSize = false, ForeColor = Theme.FgDim, BackColor = Theme.Bg, Font = light, AutoEllipsis = true,
                              Bounds = new Rectangle(l.Right + Ui.S(4), y, menuWidth - 2 * Pad - titleW - Ui.S(8), Ui.S(20)) };
        Controls.Add(l);
        Controls.Add(h);
        return y + Ui.S(24);
    }

    void AddTile(string glyph, string text, Rectangle bounds, bool stacked, Action click)
    {
        StartTile t = new StartTile(glyph, text, stacked) { Bounds = bounds };
        t.Click += (s, e) => click();
        Controls.Add(t);
    }

    void Launch(string target) { Finish(() => ShellUtil.Open(ShellUtil.SystemPath(target), null)); }

    // L'action part après la fermeture du menu (verrouiller ou se mettre en veille avec le menu ouvert le laisserait au retour)
    void Run(Action action) { Finish(action); }

    bool finishing;

    // Ferme le menu puis lance l'action, une seule fois. La fermeture est différée : fermer le menu (donc détruire la
    // tuile ou la recherche) pendant qu'elle traite encore son clic ou sa frappe pouvait lever une exception, et une
    // deuxième frappe rapide relançait la recherche (QA-66).
    void Finish(Action after)
    {
        if (finishing) return;
        finishing = true;
        Action run = () =>
        {
            Close();
            if (after != null) try { after(); } catch { }
        };
        if (!IsHandleCreated) { run(); return; }
        Hide();   // disparaît tout de suite ; OnDeactivate ne ferme plus rien (Visible est faux)
        BeginInvoke(run);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Native.SetRoundCorners(Handle);
        // Texte d'invite dans la recherche (EM_SETCUEBANNER), visible même quand elle a le focus
        if (search == null) return;   // recherche masquée (T-044)
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
        sinceClosed.Restart();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && picture != null && !IsDisposed) picture.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Ui.Edge(g, Theme.EdgeHot, new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Ui.S(Ui.RadiusCard));

        Rectangle searchBounds = searchRect, headerBounds = headerRect;
        searchBounds.Offset(AutoScrollPosition);
        headerBounds.Offset(AutoScrollPosition);
        if (!searchRect.IsEmpty)
        {
            Ui.Fill(g, Theme.Surface, searchBounds, searchBounds.Height / 2f);
            Ui.Glyph(g, Glyphs.Search, 12f, Theme.FgDim, new Rectangle(searchBounds.Left + Ui.S(12), searchBounds.Top, Ui.S(24), searchBounds.Height));
        }
        if (headerRect.IsEmpty) return;   // compte masqué (T-044)

        // Session : photo (ou initiales), nom, et le PC sur lequel elle est ouverte
        int size = Ui.S(48);
        Rectangle pic = new Rectangle(headerBounds.Left, headerBounds.Top + (headerBounds.Height - size) / 2, size, size);
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
        int textLeft = pic.Right + Ui.S(16), textW = headerBounds.Right - textLeft, mid = headerBounds.Top + headerBounds.Height / 2;
        TextFormatFlags f = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Bottom;
        TextRenderer.DrawText(g, userName, Ui.Font("Segoe UI Semibold", 13f), new Rectangle(textLeft, headerBounds.Top, textW, mid - headerBounds.Top), Theme.Fg, f);
        TextRenderer.DrawText(g, L.T("Session sur ", "Signed in on ", "Sesión en ") + Environment.MachineName, Ui.Font("Segoe UI", 9f),
            new Rectangle(textLeft, mid + Ui.S(2), textW, Ui.S(22)), Theme.FgDim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using (Pen line = new Pen(Theme.Line)) g.DrawLine(line, headerBounds.Left, headerBounds.Bottom + Ui.S(8), headerBounds.Right, headerBounds.Bottom + Ui.S(8));
    }
}

// Tuile du menu : icône et texte, au clavier (Tab, Entrée, Espace) et lue par les lecteurs d'écran comme un bouton
class StartTile : FlatControl
{
    string glyph;
    readonly bool stacked;   // icône au-dessus du texte, sinon à gauche
    public bool GlyphOnly;   // bouton icône seule (flèches du calendrier), nommé par AccessibleName
    public Image Picture;    // icône d'application à la place du glyphe (raccourcis personnels) ; appartient à l'appelant
    bool hover;

    protected override AccessibleRole FlatRole { get { return AccessibleRole.PushButton; } }
    protected override string FlatDefaultAction { get { return L.T("Appuyer", "Press", "Pulsar"); } }

    public StartTile(string glyph, string text, bool stacked)
    {
        this.glyph = glyph;
        this.stacked = stacked;
        Text = text;
        Font = Ui.Font("Segoe UI", 10f);
        BackColor = Theme.Bg;
        EnableSettingsKeyboard();
    }

    public void SetGlyph(string value)
    {
        if (glyph == value) return;
        glyph = value;
        Invalidate();
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
        bool highlight = Enabled && (hover || Focused);
        Color text = Enabled ? Theme.SelText(highlight, Theme.Fg) : Theme.FgDim;
        Ui.Fill(g, highlight ? Theme.Hover : Theme.Surface, r, Ui.S(Ui.RadiusControl));
        if (GlyphOnly) Ui.Glyph(g, glyph, 11f, text, ClientRectangle);
        else if (stacked)
        {
            Ui.Glyph(g, glyph, 17f, text, GlyphArea);
            TextRenderer.DrawText(g, Text, Font, TextArea, text, TextFlags | TextFormatFlags.HorizontalCenter);
        }
        else
        {
            if (Picture != null)
            {
                int px = Ui.S(24);
                g.DrawImage(Picture, new Rectangle(Ui.S(13), (Height - px) / 2, px, px));
            }
            else Ui.Glyph(g, glyph, 14f, text, new Rectangle(Ui.S(10), 0, Ui.S(30), Height));
            TextRenderer.DrawText(g, Text, Font, TextArea, text, TextFlags | TextFormatFlags.VerticalCenter);
        }
        DrawSettingsFocus(g);
    }

    const TextFormatFlags TextFlags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak;
    static int GlyphHeight { get { return Ui.S(28); } }
    static int GlyphGap { get { return Ui.S(4); } }

    // Hauteur réelle du texte, sur deux lignes au plus, mesurée à la largeur de la tuile
    int TextHeight
    {
        get
        {
            int w = stacked ? Width - Ui.S(12) : Width - Ui.S(52);
            int measured = TextRenderer.MeasureText(Text, Font, new Size(Math.Max(1, w), int.MaxValue), TextFlags).Height;
            return Math.Min(measured, 2 * Font.Height + Ui.S(2));
        }
    }

    // Icône puis texte, centrés ensemble verticalement : le texte n'est jamais coupé en hauteur (essai du 2026-11-27)
    int BlockTop { get { return Math.Max(Ui.S(4), (Height - (GlyphHeight + GlyphGap + TextHeight)) / 2); } }

    Rectangle GlyphArea { get { return new Rectangle(0, BlockTop, Width, GlyphHeight); } }

    internal Rectangle TextArea
    {
        get
        {
            if (!stacked) return new Rectangle(Ui.S(48), 0, Width - Ui.S(52), Height);
            int top = BlockTop + GlyphHeight + GlyphGap;
            return new Rectangle(Ui.S(6), top, Width - Ui.S(12), Math.Max(TextHeight + Ui.S(2), Height - top - Ui.S(4)));
        }
    }

    // Chaque mot tient sur une ligne : sinon Windows le coupe par « … » ou le fait disparaître, alors que la hauteur
    // mesurée semble bonne (essai du 2026-11-27 : « Télécharge… » passait l'ancien test)
    internal static bool WordsFit(string text, Font font, int width)
    {
        foreach (string word in (text ?? string.Empty).Split(' '))
            if (word.Length > 0 && TextRenderer.MeasureText(word, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width > width)
                return false;
        return true;
    }

    // Une tuile à icône à gauche de largeur tileWidth affiche ce texte en deux lignes au plus, sans couper de mot
    internal static bool FitsBeside(string text, Font font, int tileWidth)
    {
        int width = tileWidth - Ui.S(52);
        return WordsFit(text, font, width) &&
               TextRenderer.MeasureText(text, font, new Size(width, int.MaxValue), TextFlags).Height <= 2 * font.Height + Ui.S(2);
    }

    // Faux si le texte, mis en page, dépasse sa zone (test de non-régression)
    internal bool TextFits
    {
        get
        {
            Rectangle a = TextArea;
            Size need = TextRenderer.MeasureText(Text, Font, new Size(a.Width, int.MaxValue), TextFlags);
            return need.Height <= a.Height && a.Bottom <= Height && WordsFit(Text, Font, a.Width);
        }
    }
}
