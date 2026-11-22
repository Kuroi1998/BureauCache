// BureauCache — ItemsView (partie 2)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

partial class ItemsView
{
    // ---------- Souris ----------
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        EndHover();
        if (e.Button == MouseButtons.Left && contentH > Height && e.X >= Width - S(14))
        {
            Rectangle t = Thumb();
            if (e.Y >= t.Top && e.Y <= t.Bottom) { scrollDragging = true; scrollDragY = e.Y; scrollDragStart = scroll; }
            else ScrollTo(scrollTarget + (e.Y < t.Top ? -Height : Height) * 0.8f, true);
            Invalidate();
            return;
        }
        downPt = e.Location;
        downRow = RowAt(e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (scrollDragging)
        {
            Rectangle t = Thumb();
            float ratio = MaxScroll / Math.Max(1f, Height - t.Height);
            scroll = scrollTarget = Math.Max(0, Math.Min(MaxScroll, scrollDragStart + (e.Y - scrollDragY) * ratio));
            Invalidate();
            return;
        }
        if (e.Button == MouseButtons.Left && downRow >= 0 && downRow < rows.Count && !dragging && !rows[downRow].IsHeader &&
            (Math.Abs(e.X - downPt.X) > SystemInformation.DragSize.Width || Math.Abs(e.Y - downPt.Y) > SystemInformation.DragSize.Height))
        {
            dragging = true;
            dragRow = rows[downRow];
            owner.Busy = true;
            EndHover();
        }
        if (dragging)
        {
            bool outside = e.X < -S(4) || e.X > Width + S(4) || e.Y < -S(160) || e.Y > Height + S(4);
            if (outside && dragRow.Item.HasPath) { StartExternalDrag(); return; }
            if (e.Y < S(28)) ScrollTo(scrollTarget - S(14), false);
            else if (e.Y > Height - S(28)) ScrollTo(scrollTarget + S(14), false);
            ComputeDrop(e.Location);
            Invalidate();
            return;
        }
        int h = RowAt(e.Location);
        if (h != hover)
        {
            hover = h;
            EndHover();
            if (h >= 0 && !rows[h].IsHeader) hoverTimer.Start();
            Invalidate();
        }
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        EndHover();
        if (hover != -1 && !dragging) { hover = -1; Invalidate(); }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (scrollDragging) { scrollDragging = false; Invalidate(); return; }
        if (dragging)
        {
            dragging = false;
            owner.Busy = false;
            Group g = dropGroup;
            dropGroup = null;
            Invalidate();
            if (g != null && ItemDropped != null) ItemDropped(dragRow.Item, dragRow.G, g, dropIndex);
            downRow = -1;
            return;
        }
        int r = RowAt(e.Location);
        if (r < 0 || r != downRow) { downRow = -1; return; }
        downRow = -1;
        VRow row = rows[r];
        if (row.IsHeader)
        {
            if (e.Button != MouseButtons.Left || row.G.IsSearch) return;
            if (!Settings.Collapsed.Remove(row.G.Key)) Settings.Collapsed.Add(row.G.Key);
            Settings.SaveQuiet();
            LayoutRows();
            Clamp();
            hover = -1;
            Invalidate();
            return;
        }
        sel = r;
        Invalidate();
        if (e.Button == MouseButtons.Left && ItemActivated != null) ItemActivated(row.Item);
        else if (e.Button == MouseButtons.Right && ItemContext != null) ItemContext(row.Item, e.Location);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollTo(scrollTarget - e.Delta / 120f * S(44) * 2.5f, true);
    }

    public void WheelFromParent(int delta) { ScrollTo(scrollTarget - delta / 120f * S(44) * 2.5f, true); }

    void ComputeDrop(Point p)
    {
        Point c = new Point(p.X, p.Y + (int)scroll);
        VRow row = null;
        int best = int.MaxValue;
        foreach (VRow r in rows)
        {
            if (r.R.Contains(c)) { row = r; break; }
            int d = c.Y < r.R.Top ? r.R.Top - c.Y : c.Y > r.R.Bottom ? c.Y - r.R.Bottom : 0;
            if (d == 0) d = c.X < r.R.Left ? r.R.Left - c.X : c.X - r.R.Right;
            if (d < best) { best = d; row = r; }
        }
        dropGroup = null;
        dropMark = Rectangle.Empty;
        if (row == null || row.G.Key == "recent" || row.G.IsSearch || !dragRow.Item.IsDesktopFile && !dragRow.Item.IsSystem) return;
        dropGroup = row.G;
        if (row.IsHeader)
        {
            dropIndex = 0;
            dropMark = new Rectangle(row.R.X + S(8), row.R.Bottom - S(1), row.R.Width - S(16), S(2));
            return;
        }
        bool before = Grid ? c.X < row.R.X + row.R.Width / 2 : c.Y < row.R.Y + row.R.Height / 2;
        dropIndex = row.IndexInGroup + (before ? 0 : 1);
        if (Grid)
            dropMark = new Rectangle((before ? row.R.Left : row.R.Right) - S(1), row.R.Y + S(8), S(3), row.R.Height - S(16));
        else
            dropMark = new Rectangle(row.R.X + S(8), (before ? row.R.Top : row.R.Bottom) - S(1), row.R.Width - S(16), S(3));
    }

    void StartExternalDrag()
    {
        DesktopItem it = dragRow.Item;
        dragging = false;
        dropGroup = null;
        Invalidate();
        selfDragOut = true;
        owner.Busy = true;
        try { DoDragDrop(new DataObject(DataFormats.FileDrop, new string[] { it.Target }), DragDropEffects.Copy | DragDropEffects.Link); }
        catch { }
        selfDragOut = false;
        owner.Busy = false;
        owner.HidePanel();
    }

    static DragDropEffects EffectFor(DragEventArgs e)
    {
        if ((e.KeyState & 32) != 0 && (e.AllowedEffect & DragDropEffects.Link) != 0) return DragDropEffects.Link;
        if ((e.KeyState & 4) != 0 && (e.AllowedEffect & DragDropEffects.Move) != 0) return DragDropEffects.Move;
        return (e.AllowedEffect & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy : DragDropEffects.Link;
    }

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        if (!selfDragOut && e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = EffectFor(e); dropActive = true; Invalidate(); }
        else e.Effect = DragDropEffects.None;
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        e.Effect = !selfDragOut && e.Data.GetDataPresent(DataFormats.FileDrop) ? EffectFor(e) : DragDropEffects.None;
    }

    protected override void OnDragLeave(EventArgs e) { base.OnDragLeave(e); dropActive = false; Invalidate(); }

    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        dropActive = false;
        Invalidate();
        string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
        DragDropEffects eff = EffectFor(e);
        if (files != null && files.Length > 0 && FilesDropped != null)
            BeginInvoke(new Action(() => FilesDropped(files, eff)));
    }

    // ---------- Clavier ----------
    public void MoveSelection(Keys k)
    {
        List<int> items = new List<int>();
        for (int i = 0; i < rows.Count; i++) if (!rows[i].IsHeader) items.Add(i);
        if (items.Count == 0) return;
        if (sel < 0 || sel >= rows.Count || rows[sel].IsHeader) { sel = items[0]; EnsureVisible(); return; }
        Rectangle cur = rows[sel].R;
        int next = -1;
        if (k == Keys.Left || k == Keys.Right)
        {
            int pos = items.IndexOf(sel) + (k == Keys.Right ? 1 : -1);
            if (pos >= 0 && pos < items.Count) next = items[pos];
        }
        else
        {
            bool down = k == Keys.Down;
            int bestY = down ? int.MaxValue : int.MinValue, bestDx = int.MaxValue;
            foreach (int i in items)
            {
                Rectangle r = rows[i].R;
                if (down ? r.Top <= cur.Top : r.Top >= cur.Top) continue;
                int dx = Math.Abs(r.X - cur.X);
                if ((down ? r.Top < bestY : r.Top > bestY) || (r.Top == bestY && dx < bestDx))
                { bestY = r.Top; bestDx = dx; next = i; }
            }
        }
        if (next >= 0) { sel = next; EnsureVisible(); }
    }

    public void SelectFirst()
    {
        sel = -1;
        for (int i = 0; i < rows.Count; i++) if (!rows[i].IsHeader) { sel = i; break; }
        Invalidate();
    }

    void EnsureVisible()
    {
        Rectangle r = rows[sel].R;
        if (r.Top < scrollTarget) ScrollTo(r.Top - S(36), true);
        else if (r.Bottom > scrollTarget + Height) ScrollTo(r.Bottom - Height + S(8), true);
        Invalidate();
    }
}
