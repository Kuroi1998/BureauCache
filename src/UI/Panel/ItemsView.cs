// BureauCache — ItemsView
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Vue des éléments (liste ou grille)
// =====================================================================
partial class ItemsView : Control
{
    readonly PanelForm owner;
    List<Group> groups = new List<Group>();
    readonly List<VRow> rows = new List<VRow>();
    int contentH;
    float scroll, scrollTarget;
    readonly System.Windows.Forms.Timer scrollAnim = new System.Windows.Forms.Timer();
    readonly Stopwatch scrollClock = new Stopwatch();
    readonly System.Windows.Forms.Timer hoverTimer = new System.Windows.Forms.Timer();
    int hover = -1, sel = -1, downRow = -1;
    Point downPt;
    bool dragging, scrollDragging, dropActive;
    VRow dragRow;
    int scrollDragY;
    float scrollDragStart;
    Group dropGroup;
    int dropIndex;
    Rectangle dropMark;
    bool selfDragOut;
    public string EmptyText = "";
    public bool ForceList, ShowNumbers;
    public List<KeyValuePair<string, string>> Help;

    readonly Font fItem = Ui.Font("Segoe UI", 10.5f);
    readonly Font fDetail = Ui.Font("Segoe UI", 8.5f);
    readonly Font fGrid = Ui.Font("Segoe UI", 9f);
    readonly Font fHeader = Ui.Font("Segoe UI Semibold", 8.75f);
    readonly Font fBadge = Ui.Font("Segoe UI Semibold", 7.5f);
    readonly Font fGlyph = Ui.Font(Ui.GlyphFont, 9f);

    public event Action<DesktopItem> ItemActivated;
    public event Action<DesktopItem, Point> ItemContext;
    public event Action<DesktopItem, Group, Group, int> ItemDropped;
    public event Action<string[], DragDropEffects> FilesDropped;
    public event Action<DesktopItem, Rectangle> HoverItem;
    public event Action HoverEnd;

    public ItemsView(PanelForm owner)
    {
        this.owner = owner;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        AllowDrop = true;
        scrollAnim.Interval = 16;   // la résolution du minuteur de Windows est de 15,6 ms : 10 ms ne servait à rien (Gemini C6)
        scrollAnim.Tick += (s, e) => AdvanceScroll();
        hoverTimer.Interval = 450;
        hoverTimer.Tick += (s, e) =>
        {
            hoverTimer.Stop();
            if (dragging || hover < 0 || hover >= rows.Count || rows[hover].IsHeader || HoverItem == null) return;
            Point pointer = PointToClient(Cursor.Position);
            if (!ClientRectangle.Contains(pointer) || RowAt(pointer) != hover) return;
            HoverItem(rows[hover].Item, RectangleToScreen(Screenify(rows[hover].R)));
        };
    }

    static int S(int v) { return Ui.S(v); }
    bool Grid { get { return Settings.GridView && !ForceList; } }
    int ItemH { get { return S(44); } }
    int HeaderH { get { return S(32); } }
    int CellH { get { return S(100); } }
    int MinCellW { get { return S(88); } }
    float MaxScroll { get { return Math.Max(0, contentH - Height); } }
    public bool IsScrolling { get { return scrollAnim.Enabled || scrollDragging || dragging; } }

    public DesktopItem Selected { get { return sel >= 0 && sel < rows.Count ? rows[sel].Item : null; } }

    void EndHover()
    {
        hoverTimer.Stop();
        if (HoverEnd != null) HoverEnd();
    }

    public void CancelHover()
    {
        EndHover();
        if (hover < 0) return;
        hover = -1;
        Invalidate();
    }

    public void SetGroups(List<Group> g, bool keepScroll)
    {
        string selKey = Selected != null ? Selected.Key : null;
        DesktopItem hoverItem = hover >= 0 && hover < rows.Count && !rows[hover].IsHeader ? rows[hover].Item : null;
        groups = g;
        LayoutRows();
        if (!keepScroll) { scroll = scrollTarget = 0; }
        Clamp();
        sel = -1;
        // Une reconstruction qui laisse le même élément sous le pointeur garde son survol et son aperçu (R-18).
        // Même objet seulement : un rechargement crée de nouvelles instances, dont le contenu ou l'icône ont pu
        // changer, et l'aperçu montrerait alors des données ou une icône périmées (Q26-07)
        int kept = -1;
        if (keepScroll && hoverItem != null)
        {
            int r = RowAt(PointToClient(Cursor.Position));
            if (r >= 0 && r < rows.Count && !rows[r].IsHeader && ReferenceEquals(rows[r].Item, hoverItem)) kept = r;
        }
        hover = kept;
        if (kept < 0) EndHover();
        if (keepScroll && selKey != null)
            for (int i = 0; i < rows.Count; i++) if (!rows[i].IsHeader && rows[i].Item.Key == selKey) { sel = i; break; }
        Invalidate();
    }

    void LayoutRows()
    {
        downRow = -1;
        rows.Clear();
        int y = S(2);
        int w = Width - S(4);
        foreach (Group g in groups)
        {
            if (g.Title != null)
            {
                rows.Add(new VRow { G = g, R = new Rectangle(S(6), y, w - S(8), HeaderH) });
                y += HeaderH;
                if (!g.IsSearch && Settings.Collapsed.Contains(g.Key)) { y += S(2); continue; }
            }
            if (Grid)
            {
                int avail = w - S(8);
                int cols = Math.Max(1, avail / MinCellW);
                int cw = avail / cols;
                for (int i = 0; i < g.Items.Count; i++)
                    rows.Add(new VRow { G = g, Item = g.Items[i], IndexInGroup = i, R = new Rectangle(S(6) + (i % cols) * cw, y + (i / cols) * CellH, cw, CellH) });
                y += ((g.Items.Count + cols - 1) / cols) * CellH;
            }
            else
            {
                for (int i = 0; i < g.Items.Count; i++)
                {
                    rows.Add(new VRow { G = g, Item = g.Items[i], IndexInGroup = i, R = new Rectangle(S(6), y, w - S(8), ItemH) });
                    y += ItemH;
                }
            }
            y += S(8);
        }
        contentH = y;
    }

    void Clamp()
    {
        scrollTarget = Math.Max(0, Math.Min(MaxScroll, scrollTarget));
        scroll = Math.Max(0, Math.Min(MaxScroll, scroll));
        if (scroll == scrollTarget) scrollAnim.Stop();
    }

    void ScrollTo(float target, bool animate)
    {
        target = Math.Max(0, Math.Min(MaxScroll, target));
        if (animate && Settings.Animations)
        {
            if (target == scrollTarget) return;
            CancelHover();
            scrollTarget = target;
            if (!scrollAnim.Enabled) { scrollClock.Restart(); scrollAnim.Start(); }
            return;
        }
        if (!dragging) CancelHover();
        scroll = scrollTarget = target;
        scrollAnim.Stop();
        Invalidate();
    }

    void AdvanceScroll()
    {
        int previous = (int)scroll;
        double elapsed = scrollClock.Elapsed.TotalMilliseconds;
        scrollClock.Restart();
        // Le rythme reste stable même si le fil d'interface retarde un tick.
        scroll += (float)((scrollTarget - scroll) * (1 - Math.Exp(-elapsed / 42)));
        if (!Settings.Animations || Math.Abs(scrollTarget - scroll) < 0.75f)
        {
            scroll = scrollTarget;
            scrollAnim.Stop();
        }
        if ((int)scroll != previous) Invalidate();
    }

    public void StopScroll()
    {
        scrollTarget = scroll;
        scrollAnim.Stop();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutRows();
        Clamp();
        Invalidate();
    }

    int RowAt(Point p)
    {
        Point c = new Point(p.X, p.Y + (int)scroll);
        for (int i = FirstRowBelow(c.Y); i < rows.Count && rows[i].R.Top <= c.Y; i++)
            if (rows[i].R.Contains(c)) return i;
        return -1;
    }

    int FirstRowBelow(int y)
    {
        int lo = 0, hi = rows.Count;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (rows[mid].R.Bottom <= y) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    Rectangle Screenify(Rectangle r) { r.Offset(0, -(int)scroll); return r; }

    // ---------- Dessin ----------
    protected override void OnPaintBackground(PaintEventArgs e) { owner.PaintBg(e.Graphics, e.ClipRectangle, Location); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Help != null) { DrawHelp(g); return; }
        int top = (int)scroll + e.ClipRectangle.Top, bottom = (int)scroll + e.ClipRectangle.Bottom;
        for (int i = FirstRowBelow(top); i < rows.Count && rows[i].R.Top < bottom; i++)
        {
            VRow row = rows[i];
            Rectangle r = Screenify(row.R);
            if (row.IsHeader) DrawHeader(g, row, r, i == hover);
            else if (Grid) DrawCell(g, row.Item, r, i == sel, i == hover, dragging && row == dragRow);
            else DrawListItem(g, row.Item, r, i == sel, i == hover, dragging && row == dragRow);
            if (ShowNumbers && !row.IsHeader && row.G.Key == "fav" && row.IndexInGroup < 9)
            {
                Point ip = Grid ? new Point(r.X + (r.Width - S(48)) / 2, r.Y + S(10)) : new Point(r.X + S(12), r.Y + (r.Height - S(32)) / 2);
                Rectangle badge = new Rectangle(ip.X - S(6), ip.Y - S(6), S(20), S(20));
                Ui.Fill(g, Theme.Accent, badge, S(10));
                using (Pen p = new Pen(Theme.Bg, S(2))) g.DrawEllipse(p, badge);
                TextRenderer.DrawText(g, (row.IndexInGroup + 1).ToString(), fBadge, badge, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
        if (dragging && dropGroup != null && !dropMark.IsEmpty)
            Ui.Fill(g, Theme.Accent, Screenify(dropMark), S(1));

        if (rows.Count == 0)
            TextRenderer.DrawText(g, EmptyText, fItem, new Rectangle(S(16), S(30), Width - S(32), S(140)), Theme.FgDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        if (contentH > Height)
        {
            Rectangle t = Thumb();
            Ui.Fill(g, Color.FromArgb(scrollDragging ? 150 : 90, Theme.FgDim), t, t.Width / 2f);
        }

        if (dropActive)
        {
            Rectangle r = new Rectangle(S(6), S(4), Width - S(12), Height - S(8));
            using (GraphicsPath p = Ui.Round(r, S(10)))
            using (Pen pen = new Pen(Theme.Accent, S(2)) { DashStyle = DashStyle.Dash })
            using (SolidBrush b = new SolidBrush(Color.FromArgb(30, Theme.Accent)))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
            string msg = L.T("Dépose ici pour ajouter au bureau", "Drop here to add to the desktop", "Suelta aquí para añadir al escritorio");
            Size ts = TextRenderer.MeasureText(msg, fItem);
            Rectangle pill = new Rectangle((Width - ts.Width) / 2 - S(16), Height / 2 - S(20), ts.Width + S(32), S(40));
            Ui.Fill(g, Theme.Accent, pill, S(20));
            TextRenderer.DrawText(g, msg, fItem, pill, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    void DrawHelp(Graphics g)
    {
        int y = S(6);
        TextRenderer.DrawText(g, L.T("RACCOURCIS CLAVIER", "KEYBOARD SHORTCUTS", "ATAJOS DE TECLADO"), fHeader, new Point(S(16), y + S(6)), Theme.FgDim, TextFormatFlags.NoPrefix);
        y += S(34);
        int keyW = 0;
        foreach (KeyValuePair<string, string> h in Help) keyW = Math.Max(keyW, TextRenderer.MeasureText(h.Key, fBadge).Width);
        keyW += S(14);
        foreach (KeyValuePair<string, string> h in Help)
        {
            int tw = Width - (S(14) + keyW + S(10)) - S(14);
            int th = TextRenderer.MeasureText(h.Value, fDetail, new Size(tw, 400), TextFormatFlags.WordBreak).Height;
            int rh = Math.Max(S(30), th + S(10));
            Rectangle pill = new Rectangle(S(14), y + (rh - S(22)) / 2, keyW, S(22));
            Ui.Fill(g, Theme.SelOverlay, pill, S(6));
            TextRenderer.DrawText(g, h.Key, fBadge, pill, Theme.Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, h.Value, fDetail, new Rectangle(pill.Right + S(10), y + (rh - th) / 2, tw, th), Theme.Fg,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            y += rh;
        }
        TextRenderer.DrawText(g, L.T("Échap pour revenir", "Esc to go back", "Esc para volver"), fDetail, new Rectangle(0, y + S(10), Width, S(20)), Theme.FgDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
    }

    // Tab / Maj+Tab : passe à la section suivante / précédente
    public void JumpGroup(int dir)
    {
        if (Help != null) return;
        List<Group> withItems = groups.Where(g => g.Items.Count > 0 && !(g.Title != null && !g.IsSearch && Settings.Collapsed.Contains(g.Key))).ToList();
        if (withItems.Count == 0) return;
        Group cur = sel >= 0 && sel < rows.Count ? rows[sel].G : null;
        int i = cur == null ? (dir > 0 ? 0 : withItems.Count - 1) : withItems.IndexOf(cur) + dir;
        if (i < 0) i = withItems.Count - 1;
        if (i >= withItems.Count) i = 0;
        for (int k = 0; k < rows.Count; k++)
            if (!rows[k].IsHeader && rows[k].G == withItems[i]) { sel = k; EnsureVisible(); return; }
    }

    public Point SelectedPoint()
    {
        if (sel < 0 || sel >= rows.Count) return new Point(S(40), S(40));
        Rectangle r = Screenify(rows[sel].R);
        return new Point(r.X + S(40), r.Y + r.Height / 2);
    }

    Rectangle Thumb()
    {
        int h = Math.Max(S(36), (int)(Height * (float)Height / contentH));
        int y = MaxScroll <= 0 ? 0 : (int)((Height - h) * scroll / MaxScroll);
        int w = scrollDragging ? S(6) : S(4);
        return new Rectangle(Width - w - S(3), y, w, h);
    }

    void DrawHeader(Graphics g, VRow row, Rectangle r, bool hot)
    {
        if (hot && !dragging && !row.G.IsSearch) Ui.Fill(g, Theme.HoverOverlay, r, S(6));
        bool collapsed = !row.G.IsSearch && Settings.Collapsed.Contains(row.G.Key);
        Ui.Glyph(g, row.G.Glyph ?? Glyphs.AllItems, 8.5f, row.G.GlyphColor ?? Theme.FgDim, new Rectangle(r.X + S(6), r.Y, S(20), r.Height));
        string t = row.G.Title.ToUpper(L.Culture);
        TextRenderer.DrawText(g, t, fHeader, new Rectangle(r.X + S(30), r.Y, r.Width, r.Height), Theme.FgDim,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        int tw = TextRenderer.MeasureText(t, fHeader).Width;
        TextRenderer.DrawText(g, row.G.Items.Count.ToString(), fHeader, new Rectangle(r.X + S(30) + tw + S(2), r.Y, S(40), r.Height),
            Color.FromArgb(150, Theme.FgDim), TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (!row.G.IsSearch)
            TextRenderer.DrawText(g, collapsed ? Glyphs.ChevronRight : Glyphs.ChevronDown, fGlyph, new Rectangle(r.Right - S(30), r.Y, S(24), r.Height), Theme.FgDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        // Rappel des raccourcis des favoris, sinon visibles seulement en maintenant Ctrl (Gemini B6)
        if (row.G.Key == "fav" && row.G.Items.Count > 0 && !collapsed)
            TextRenderer.DrawText(g, "Ctrl+1…" + Math.Min(9, row.G.Items.Count), fHeader, new Rectangle(r.X, r.Y, r.Width - S(36), r.Height),
                Color.FromArgb(150, Theme.FgDim), TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    void DrawIconOrPlaceholder(Graphics g, Bitmap b, Rectangle r, bool ghost)
    {
        if (b == null) { Ui.Fill(g, Theme.HoverOverlay, r, S(6)); return; }
        if (!ghost) { g.DrawImage(b, r); return; }
        using (ImageAttributes ia = new ImageAttributes())
        {
            ia.SetColorMatrix(new ColorMatrix { Matrix33 = 0.35f });
            g.DrawImage(b, r, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia);
        }
    }

    void DrawListItem(Graphics g, DesktopItem it, Rectangle r, bool selected, bool hot, bool ghost)
    {
        Rectangle bg = new Rectangle(r.X, r.Y + S(1), r.Width, r.Height - S(2));
        if (selected) Ui.Fill(g, Theme.SelOverlay, bg, S(8));
        else if (hot && !dragging) Ui.Fill(g, Theme.HoverOverlay, bg, S(8));
        if (selected) Ui.Fill(g, Theme.Accent, new RectangleF(bg.X, bg.Y + S(11), S(3), bg.Height - S(22)), S(2));
        int isz = S(32);
        int ix = r.X + S(12), iy = r.Y + (r.Height - isz) / 2;
        DrawIconOrPlaceholder(g, it.Icon, new Rectangle(ix, iy, isz, isz), ghost);
        int tx = ix + isz + S(12);
        int right = r.Right - S(8);
        if (it.IsNew)
        {
            string nb = L.T("Nouveau", "New", "Nuevo");
            Size bs = TextRenderer.MeasureText(nb, fBadge);
            Rectangle pill = new Rectangle(right - bs.Width - S(8), r.Y + (r.Height - S(18)) / 2, bs.Width + S(8), S(18));
            Ui.Fill(g, Theme.Accent, pill, S(9));
            TextRenderer.DrawText(g, nb, fBadge, pill, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            right = pill.Left - S(6);
        }
        Color fg = ghost ? Theme.FgDim : Theme.Fg;
        bool detail = ForceList && !string.IsNullOrEmpty(it.Detail);
        if (detail)
        {
            TextRenderer.DrawText(g, it.Name, fItem, new Rectangle(tx, r.Y + S(3), right - tx, r.Height / 2), fg,
                TextFormatFlags.Bottom | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, it.Detail, fDetail, new Rectangle(tx, r.Y + r.Height / 2 + S(1), right - tx, r.Height / 2 - S(4)), Theme.FgDim,
                TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
        }
        else
            TextRenderer.DrawText(g, it.Name, fItem, new Rectangle(tx, r.Y, right - tx, r.Height), fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    void DrawCell(Graphics g, DesktopItem it, Rectangle r, bool selected, bool hot, bool ghost)
    {
        Rectangle bg = new Rectangle(r.X + S(3), r.Y + S(3), r.Width - S(6), r.Height - S(6));
        if (selected) Ui.Fill(g, Theme.SelOverlay, bg, S(10));
        else if (hot && !dragging) Ui.Fill(g, Theme.HoverOverlay, bg, S(10));
        int isz = S(48);
        Rectangle ir = new Rectangle(r.X + (r.Width - isz) / 2, r.Y + S(10), isz, isz);
        DrawIconOrPlaceholder(g, it.Icon, ir, ghost);
        if (it.IsNew)
        {
            int d = S(12);
            Rectangle dot = new Rectangle(ir.Right - d + S(3), ir.Top - S(3), d, d);
            using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, dot);
            using (Pen p = new Pen(Theme.Bg, S(2))) g.DrawEllipse(p, dot);
        }
        Rectangle tr = new Rectangle(r.X + S(6), ir.Bottom + S(4), r.Width - S(12), r.Bottom - ir.Bottom - S(8));
        TextRenderer.DrawText(g, it.Name, fGrid, tr, ghost ? Theme.FgDim : Theme.Fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis |
            TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
    }
}
