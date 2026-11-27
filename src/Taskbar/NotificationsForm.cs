// BureauCache — panneau Notifications et calendrier de la barre des tâches (T-042, D-018)
using System;
using System.Drawing;
using System.Windows.Forms;

// Ouvert par l'horloge et la cloche, à la place du panneau de Windows (Windows + N). Deux cartes : les notifications
// de Windows (NotificationsForm.Feed.cs), puis le calendrier.
partial class NotificationsForm : Form
{
    static bool collapsed;   // calendrier replié, gardé pour la session comme chez Windows

    readonly Label notesTitle, notesText, dayTitle, monthTitle;
    readonly StartTile notesAction, clearAll, notifySettings, collapse, previous, next;
    internal readonly NotificationList List;
    internal readonly CalendarView Calendar;
    readonly Timer midnight = new Timer { Interval = 30000 };
    Rectangle notesCard, calendarCard, anchor, area;
    DateTime shownToday;
    bool placed, launching;
    // Ce que la carte des notifications montre. Control.Visible reste faux tant que le panneau n'est pas affiché :
    // la mise en page ne peut pas s'y fier.
    bool showList, showText, showAction;

    static int Margin2 { get { return Ui.S(10); } }
    static int Inset { get { return Ui.S(14); } }
    static int Gap { get { return Ui.S(8); } }
    static int PanelWidth { get { return Math.Max(Ui.S(400), 23 * Ui.Font("Segoe UI", 10f).Height); } }
    static int CardWidth { get { return PanelWidth - 2 * Margin2; } }
    static int Inner { get { return CardWidth - 2 * Inset; } }
    static int ButtonHeight { get { return Math.Max(Ui.S(42), 2 * Ui.Font("Segoe UI", 10f).Height + Ui.S(6)); } }
    static int IconButton { get { return Math.Max(Ui.S(36), Ui.Font("Segoe UI", 10f).Height + Ui.S(16)); } }

    public NotificationsForm(bool calendarFirst)
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Theme.Bg;
        Text = L.T("Notifications et calendrier", "Notifications and calendar", "Notificaciones y calendario");
        AccessibleName = Text;

        notesTitle = AddLabel(L.T("Notifications", "Notifications", "Notificaciones"), true);
        clearAll = AddTile(Glyphs.Delete, L.T("Effacer tout", "Clear all", "Borrar todo"), ClearAll);
        notifySettings = AddTile(Glyphs.Settings, string.Empty, () => Launch("ms-settings:notifications"));
        notifySettings.AccessibleName = L.T("Paramètres de notification", "Notification settings", "Configuración de notificaciones");
        notesText = AddLabel(string.Empty, false);
        notesAction = AddTile(Glyphs.Bell, string.Empty, () => Launch(notesActionUri));
        List = new NotificationList { Logo = LogoOf };
        List.Dismissed += Dismiss;
        List.Opened += OpenApp;
        Controls.Add(List);

        dayTitle = AddLabel(string.Empty, true);
        collapse = AddTile(Glyphs.ChevronDown, string.Empty, ToggleCollapse);
        monthTitle = AddLabel(string.Empty, true);
        previous = AddTile(Glyphs.ChevronUp, string.Empty, () => Calendar.ShowMonth(-1));
        next = AddTile(Glyphs.ChevronDown, string.Empty, () => Calendar.ShowMonth(1));
        previous.AccessibleName = L.T("Mois précédent", "Previous month", "Mes anterior");
        next.AccessibleName = L.T("Mois suivant", "Next month", "Mes siguiente");

        Calendar = new CalendarView();
        Calendar.MonthChanged += (s, e) => monthTitle.Text = CalendarMonth.MonthTitle(Calendar.Month, L.Culture);
        Controls.Add(Calendar);
        monthTitle.Text = CalendarMonth.MonthTitle(Calendar.Month, L.Culture);

        shownToday = DateTime.Today;
        dayTitle.Text = CalendarMonth.DayTitle(shownToday, L.Culture);
        midnight.Tick += (s, e) => CheckDate();
        feed.Tick += (s, e) => ReadFeed();
        ShowFeed(Sandbox.Active ? Native.NotificationAccess.Unavailable : (Native.NotificationAccess?)null, null);
        ActiveControl = calendarFirst && !collapsed ? (Control)Calendar : notifySettings;
    }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 /* WS_EX_TOOLWINDOW */; return cp; }
    }

    internal bool Collapsed { get { return collapsed; } }

    Label AddLabel(string text, bool emphasis)
    {
        Label label = new Label
        {
            Text = text, AutoEllipsis = true, UseMnemonic = false, TextAlign = ContentAlignment.MiddleLeft,
            Font = Ui.Font(emphasis ? "Segoe UI Semibold" : "Segoe UI", emphasis ? 11.5f : 9.5f),
            ForeColor = emphasis ? Theme.Fg : Theme.FgDim, BackColor = Theme.Card
        };
        Controls.Add(label);
        return label;
    }

    StartTile AddTile(string glyph, string text, Action click)
    {
        StartTile tile = new StartTile(glyph, text, false) { BackColor = Theme.Card };
        if (text.Length == 0) tile.GlyphOnly = true;
        tile.Click += (s, e) => click();
        Controls.Add(tile);
        return tile;
    }

    // Place les cartes et leur contenu ; le calendrier replié ne garde que la date du jour
    void Arrange()
    {
        SuspendLayout();
        int x = Margin2 + Inset, y = Margin2;
        notesCard = new Rectangle(Margin2, y, CardWidth, 0);
        y += Ui.S(8);
        notifySettings.Bounds = new Rectangle(x + Inner - IconButton, y, IconButton, IconButton);
        int clearWidth = TextRenderer.MeasureText(clearAll.Text, clearAll.Font).Width + Ui.S(62);
        clearAll.Bounds = new Rectangle(notifySettings.Left - Ui.S(4) - clearWidth, y, clearWidth, IconButton);
        clearAll.Visible = showList && List.Count > 0;
        notesTitle.Bounds = new Rectangle(x, y, (showList && List.Count > 0 ? clearAll.Left : notifySettings.Left) - Gap - x, IconButton);
        y = notesTitle.Bottom + Ui.S(8);
        List.Visible = showList;
        notesText.Visible = showText;
        notesAction.Visible = showAction;
        if (showList)
        {
            List.MaxHeight = collapsed ? Ui.S(560) : Ui.S(380);
            List.Bounds = new Rectangle(x, y, Inner, Math.Max(1, List.Height));
            List.Height = Math.Max(1, List.PreferredHeight);
            y = List.Bottom + Inset;
        }
        if (showText)
        {
            int textHeight = TextRenderer.MeasureText(notesText.Text, notesText.Font, new Size(Inner, int.MaxValue), TextFormatFlags.WordBreak).Height;
            notesText.AutoEllipsis = false;
            notesText.TextAlign = ContentAlignment.TopLeft;
            notesText.Bounds = new Rectangle(x, y, Inner, textHeight + Ui.S(2));
            y = notesText.Bottom + Gap;
        }
        if (showAction)
        {
            notesAction.Bounds = new Rectangle(x, y, Inner, ButtonHeight);
            y = notesAction.Bottom + Inset;
        }
        else if (showText) y += Inset - Gap;
        notesCard.Height = y - notesCard.Top;

        y += Gap;
        calendarCard = new Rectangle(Margin2, y, CardWidth, 0);
        y += Ui.S(6);
        collapse.Bounds = new Rectangle(x + Inner - IconButton, y, IconButton, IconButton);
        dayTitle.Bounds = new Rectangle(x, y, Inner - IconButton - Gap, IconButton);
        collapse.SetGlyph(collapsed ? Glyphs.ChevronUp : Glyphs.ChevronDown);
        collapse.AccessibleName = collapsed ? L.T("Afficher le calendrier", "Show calendar", "Mostrar calendario") :
            L.T("Masquer le calendrier", "Hide calendar", "Ocultar calendario");
        y = collapse.Bottom + Ui.S(6);
        monthTitle.Visible = previous.Visible = next.Visible = Calendar.Visible = !collapsed;
        if (!collapsed)
        {
            y += Ui.S(4);
            next.Bounds = new Rectangle(x + Inner - IconButton, y, IconButton, IconButton);
            previous.Bounds = new Rectangle(next.Left - Ui.S(4) - IconButton, y, IconButton, IconButton);
            monthTitle.Bounds = new Rectangle(x, y, previous.Left - Gap - x, IconButton);
            y = next.Bottom + Ui.S(4);
            Calendar.Bounds = new Rectangle(x - Ui.S(4), y, Inner + Ui.S(8), Calendar.PreferredHeight);
            y = Calendar.Bottom + Ui.S(8);
        }
        calendarCard.Height = y - calendarCard.Top;
        ClientSize = new Size(PanelWidth, calendarCard.Bottom + Margin2);
        ResumeLayout(false);
        if (placed) PlaceAbove(anchor, area);
        Invalidate();
    }

    internal void ToggleCollapse()
    {
        bool focused = Calendar.Focused;
        collapsed = !collapsed;
        Arrange();
        if (focused || collapse.Focused) collapse.Focus();
    }

    public void PlaceAbove(Rectangle anchor, Rectangle area)
    {
        this.anchor = anchor;
        this.area = area;
        placed = true;
        Size content = ClientSize;
        Rectangle bounds = QuickSettingsForm.PopupBounds(anchor, area, content);
        AutoScroll = bounds.Size != content;
        AutoScrollMinSize = AutoScroll ? content : Size.Empty;
        Bounds = bounds;
    }

    // Panneau resté ouvert à minuit : la date du jour et le cercle d'aujourd'hui suivent
    void CheckDate()
    {
        if (DateTime.Today == shownToday) return;
        bool showingToday = Calendar.Selected == shownToday;
        shownToday = DateTime.Today;
        dayTitle.Text = CalendarMonth.DayTitle(shownToday, L.Culture);
        if (showingToday) Calendar.SetSelected(shownToday);
        Calendar.Invalidate();
        List.Invalidate();   // « hier » s'affiche avec sa date
    }

    // Lien vers Windows : le panneau se ferme d'abord (null : panneau des notifications de Windows, Windows + N)
    void Launch(string uri)
    {
        if (Sandbox.Active || launching) return;
        launching = true;
        BeginInvoke((Action)delegate
        {
            Close();
            try
            {
                if (uri == null) Native.PressWinKey(0x4E);
                else ShellUtil.Open(uri, null);
            }
            catch (Exception) { }
        });
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (Sandbox.Active) return;
        Native.SetRoundCorners(Handle);
        midnight.Start();
        ReadFeed();
        feed.Start();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (Visible) Close();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys key)
    {
        if (key == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref message, key);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Point scroll = AutoScrollPosition;
        foreach (Rectangle card in new[] { notesCard, calendarCard })
        {
            Rectangle r = card;
            r.Offset(scroll);
            Ui.Card(e.Graphics, Theme.Card, r, Ui.S(Ui.RadiusCard), false);
        }
        Ui.Edge(e.Graphics, Theme.EdgeHot, new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Ui.S(Ui.RadiusCard));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            midnight.Dispose();
            DisposeFeed();
        }
        base.Dispose(disposing);
    }
}
