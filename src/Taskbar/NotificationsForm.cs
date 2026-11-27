// BureauCache — panneau des notifications ouvert par la cloche de la barre (T-042, D-018)
using System;
using System.Drawing;
using System.Windows.Forms;

// Les notifications de Windows (lecture dans NotificationsForm.Feed.cs). Le calendrier a son propre panneau,
// ouvert par l'horloge (CalendarForm).
partial class NotificationsForm : BarPopup
{
    readonly Label notesTitle, notesText;
    readonly StartTile notesAction, clearAll, notifySettings;
    internal readonly NotificationList List;
    // Ce que la carte montre. Control.Visible reste faux tant que le panneau n'est pas affiché : la mise en page ne
    // peut pas s'y fier.
    bool showList, showText, showAction;

    public NotificationsForm() : base(L.T("Notifications", "Notifications", "Notificaciones"))
    {
        notesTitle = AddLabel(L.T("Notifications", "Notifications", "Notificaciones"), true);
        clearAll = AddTile(Glyphs.Delete, L.T("Effacer tout", "Clear all", "Borrar todo"), ClearAll);
        notifySettings = AddTile(Glyphs.Settings, string.Empty, () => Launch("ms-settings:notifications"));
        notifySettings.AccessibleName = L.T("Paramètres de notification", "Notification settings", "Configuración de notificaciones");
        notesText = AddLabel(string.Empty, false);
        notesAction = AddTile(Glyphs.Bell, string.Empty, () => Launch(notesActionUri));
        List = new NotificationList { Logo = LogoOf, MaxHeight = Ui.S(560) };
        List.Dismissed += Dismiss;
        List.Opened += OpenApp;
        Controls.Add(List);

        feed.Tick += (s, e) => ReadFeed();
        ShowFeed(Sandbox.Active ? Native.NotificationAccess.Unavailable : (Native.NotificationAccess?)null, null);
        ActiveControl = notifySettings;
    }

    void Arrange()
    {
        Point scroll = AutoScrollPosition;
        AutoScrollPosition = Point.Empty;
        SuspendLayout();
        int x = ContentLeft, y = Margin2 + Ui.S(8);
        bool items = showList && List.Count > 0;
        notifySettings.Bounds = new Rectangle(x + Inner - IconButton, y, IconButton, IconButton);
        int clearWidth = TextRenderer.MeasureText(clearAll.Text, clearAll.Font).Width + Ui.S(62);
        clearAll.Bounds = new Rectangle(notifySettings.Left - Ui.S(4) - clearWidth, y, clearWidth, IconButton);
        clearAll.Visible = items;
        notesTitle.Bounds = new Rectangle(x, y, (items ? clearAll.Left : notifySettings.Left) - Gap - x, IconButton);
        y = notesTitle.Bottom + Ui.S(8);
        List.Visible = showList;
        notesText.Visible = showText;
        notesAction.Visible = showAction;
        if (showList)
        {
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
        ResumeLayout(false);
        FinishLayout(Margin2, y);
        AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (Sandbox.Active) return;
        ReadFeed();
        feed.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) DisposeFeed();
        base.Dispose(disposing);
    }
}
