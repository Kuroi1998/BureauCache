// BureauCache — liste des notifications de Windows dans le panneau de la barre (T-042, D-018)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Regroupées par application (la plus récente d'abord), comme chez Windows. Survol : bouton ✕ pour effacer ; clic :
// ouvre l'application. Clavier : flèches, Entrée (ouvrir), Suppr (effacer). Défilement à la molette au-delà de MaxHeight.
class NotificationList : Control
{
    sealed class Row
    {
        public Native.SystemNotification Item;   // null : en-tête de l'application
        public string App, AppId;
        public Rectangle Bounds;
        public int TitleHeight, BodyHeight;
    }

    readonly List<Row> rows = new List<Row>();
    List<Native.SystemNotification> items = new List<Native.SystemNotification>();
    int contentHeight, scroll, hover = -1, selected = -1;
    bool hoverClose;

    public Func<string, Image> Logo;   // logo d'une application, par son identifiant
    public event Action<Native.SystemNotification> Dismissed, Opened;
    public int MaxHeight = 400;

    public NotificationList()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Font = Ui.Font("Segoe UI", 9.5f);
        BackColor = Theme.Card;
        TabStop = true;
        AccessibleRole = AccessibleRole.List;
        AccessibleName = L.T("Notifications", "Notifications", "Notificaciones");
    }

    Font TitleFont { get { return Ui.Font("Segoe UI Semibold", 10f); } }
    Font AppFont { get { return Ui.Font("Segoe UI", 9.5f); } }
    int Pad { get { return Ui.S(10); } }
    int CloseSize { get { return Math.Max(Ui.S(28), Font.Height + Ui.S(10)); } }
    int HeaderHeight { get { return Math.Max(Ui.S(30), AppFont.Height + Ui.S(12)); } }

    public int Count { get { return items.Count; } }
    public int PreferredHeight { get { return Math.Min(contentHeight, MaxHeight); } }
    internal int ContentHeight { get { return contentHeight; } }
    internal Native.SystemNotification SelectedItem { get { return selected >= 0 && selected < rows.Count ? rows[selected].Item : null; } }

    // Nouvelle lecture : la sélection et le défilement restent sur la même notification quand elle existe encore
    public void SetItems(List<Native.SystemNotification> list)
    {
        uint keep = SelectedItem != null ? SelectedItem.Id : 0;
        items = list ?? new List<Native.SystemNotification>();
        Relayout();
        selected = keep == 0 ? -1 : rows.FindIndex(r => r.Item != null && r.Item.Id == keep);
        hover = -1;
        Invalidate();
    }

    internal void Remove(uint id)
    {
        int before = SelectedIndexAmongItems();
        items = items.Where(n => n.Id != id).ToList();
        Relayout();
        SelectItemAt(Math.Min(before, items.Count - 1));
    }

    int SelectedIndexAmongItems()
    {
        Native.SystemNotification s = SelectedItem;
        return s == null ? -1 : rows.Where(r => r.Item != null).ToList().FindIndex(r => r.Item.Id == s.Id);
    }

    void SelectItemAt(int index)
    {
        List<int> itemRows = Enumerable.Range(0, rows.Count).Where(i => rows[i].Item != null).ToList();
        selected = index >= 0 && index < itemRows.Count ? itemRows[index] : -1;
        EnsureVisible();
        Invalidate();
    }

    void Relayout()
    {
        rows.Clear();
        int width = Math.Max(Ui.S(120), Width), y = 0;
        int textWidth = Math.Max(Ui.S(60), width - 2 * Pad - CloseSize);
        TextFormatFlags wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;
        foreach (var group in items.GroupBy(n => n.AppId ?? n.AppName))
        {
            Native.SystemNotification first = group.First();
            rows.Add(new Row { App = first.AppName, AppId = first.AppId, Bounds = new Rectangle(0, y, width, HeaderHeight) });
            y += HeaderHeight;
            foreach (Native.SystemNotification n in group)
            {
                Row r = new Row { Item = n, App = n.AppName, AppId = n.AppId };
                r.TitleHeight = TitleFont.Height;
                int body = string.IsNullOrEmpty(n.Body) ? 0 :
                    TextRenderer.MeasureText(n.Body, Font, new Size(textWidth, int.MaxValue), wrap).Height;
                r.BodyHeight = Math.Min(body, 3 * Font.Height);
                int height = Pad + Font.Height + Ui.S(2) + r.TitleHeight + (r.BodyHeight > 0 ? Ui.S(2) + r.BodyHeight : 0) + Pad;
                r.Bounds = new Rectangle(0, y, width, height);
                rows.Add(r);
                y += height + Ui.S(6);
            }
        }
        contentHeight = y;
        scroll = Math.Max(0, Math.Min(scroll, contentHeight - Height));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    Rectangle Shown(Rectangle r) { r.Offset(0, -scroll); return r; }

    Rectangle CloseBounds(Row r)
    {
        Rectangle b = Shown(r.Bounds);
        return new Rectangle(b.Right - CloseSize - Ui.S(4), b.Top + Ui.S(4), CloseSize, CloseSize);
    }

    int RowAt(Point p)
    {
        for (int i = 0; i < rows.Count; i++) if (rows[i].Item != null && Shown(rows[i].Bounds).Contains(p)) return i;
        return -1;
    }

    void EnsureVisible()
    {
        if (selected < 0) return;
        Rectangle r = rows[selected].Bounds;
        if (selected > 0 && rows[selected - 1].Item == null) r = Rectangle.Union(r, rows[selected - 1].Bounds);
        if (r.Top < scroll) scroll = r.Top;
        else if (r.Bottom > scroll + Height) scroll = Math.Min(r.Top, r.Bottom - Height);
        scroll = Math.Max(0, Math.Min(scroll, Math.Max(0, contentHeight - Height)));
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int next = Math.Max(0, Math.Min(Math.Max(0, contentHeight - Height), scroll - e.Delta * Ui.S(48) / 120));
        if (next != scroll) { scroll = next; UpdateHover(PointToClient(MousePosition)); Invalidate(); }
    }

    void UpdateHover(Point p)
    {
        int h = RowAt(p);
        bool close = h >= 0 && CloseBounds(rows[h]).Contains(p);
        if (h != hover || close != hoverClose) { hover = h; hoverClose = close; Invalidate(); }
    }

    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); UpdateHover(e.Location); }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hover != -1) { hover = -1; hoverClose = false; Invalidate(); }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        Focus();
        int i = RowAt(e.Location);
        if (i < 0 || e.Button != MouseButtons.Left) return;
        selected = i;
        Native.SystemNotification n = rows[i].Item;
        if (CloseBounds(rows[i]).Contains(e.Location)) { if (Dismissed != null) Dismissed(n); }
        else if (Opened != null) Opened(n);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Enter || keyData == Keys.Delete || base.IsInputKey(keyData);
    }

    // Vrai si la touche a été traitée
    internal bool HandleKey(Keys key)
    {
        int index = SelectedIndexAmongItems(), count = items.Count;
        if (count == 0) return false;
        switch (key)
        {
            case Keys.Down: SelectItemAt(Math.Min(count - 1, index + 1)); return true;
            case Keys.Up: SelectItemAt(Math.Max(0, index - 1)); return true;
            case Keys.Enter: if (SelectedItem != null && Opened != null) Opened(SelectedItem); return SelectedItem != null;
            case Keys.Delete: if (SelectedItem != null && Dismissed != null) Dismissed(SelectedItem); return SelectedItem != null;
        }
        return false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (HandleKey(e.KeyData)) { e.Handled = true; e.SuppressKeyPress = true; return; }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (selected < 0) SelectItemAt(0);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    string TimeText(DateTime time)
    {
        return time.Date == DateTime.Today ? time.ToString("t", L.Culture) : time.ToString(L.T("d MMM, HH:mm", "MMM d, h:mm tt", "d MMM, HH:mm"), L.Culture);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        TextFormatFlags line = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
        TextFormatFlags wrap = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.TextBoxControl;
        for (int i = 0; i < rows.Count; i++)
        {
            Row r = rows[i];
            Rectangle b = Shown(r.Bounds);
            if (b.Bottom < 0 || b.Top > Height) continue;
            if (r.Item == null)
            {
                int icon = Ui.S(18);
                Image logo = Logo == null || r.AppId == null ? null : Logo(r.AppId);
                Rectangle iconBox = new Rectangle(b.Left + Ui.S(2), b.Top + (b.Height - icon) / 2, icon, icon);
                if (logo != null) g.DrawImage(logo, iconBox);
                else Ui.Glyph(g, Glyphs.Bell, 10f, Theme.FgDim, iconBox);
                TextRenderer.DrawText(g, r.App, AppFont, new Rectangle(iconBox.Right + Ui.S(8), b.Top, b.Width - icon - Ui.S(10), b.Height), Theme.Fg, line);
                continue;
            }
            bool hot = i == hover, chosen = i == selected && Focused;
            Ui.Fill(g, hot || chosen ? Theme.Hover : Theme.Surface, b, Ui.S(Ui.RadiusControl));
            if (chosen) Ui.Edge(g, Theme.Accent, b, Ui.S(Ui.RadiusControl));
            int x = b.Left + Pad, w = b.Width - 2 * Pad - CloseSize, y = b.Top + Pad;
            Color text = Theme.SelText(chosen, Theme.Fg), dim = Theme.SelText(chosen, Theme.FgDim);
            TextRenderer.DrawText(g, TimeText(r.Item.Time), Font, new Rectangle(x, y, w, Font.Height), dim, line);
            y += Font.Height + Ui.S(2);
            TextRenderer.DrawText(g, r.Item.Title ?? r.App, TitleFont, new Rectangle(x, y, w, r.TitleHeight), text, line);
            y += r.TitleHeight + Ui.S(2);
            if (r.BodyHeight > 0) TextRenderer.DrawText(g, r.Item.Body, Font, new Rectangle(x, y, w, r.BodyHeight), dim, wrap);
            if (hot || chosen)
            {
                Rectangle close = CloseBounds(r);
                if (hot && hoverClose) Ui.Fill(g, Theme.HoverOverlay, close, Ui.S(4));
                Ui.Glyph(g, Glyphs.Dismiss, 9f, text, close);
            }
        }
        if (contentHeight > Height)
        {
            int track = Height, thumb = Math.Max(Ui.S(24), track * Height / contentHeight);
            int top = (track - thumb) * scroll / Math.Max(1, contentHeight - Height);
            Ui.Fill(g, Theme.Track, new Rectangle(Width - Ui.S(3), top, Ui.S(3), thumb), Ui.S(2));
        }
    }
}
