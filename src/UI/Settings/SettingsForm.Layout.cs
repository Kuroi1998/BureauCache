using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

partial class SettingsForm
{
    sealed class SettingsPage
    {
        public string Title, Summary;
        public readonly List<SettingsSection> Sections = new List<SettingsSection>();
        public readonly List<string> Tabs = new List<string>();
        public SettingsTabControl View;
        public int SelectedTab;
        public Point[] Scroll;
        public int LaidOutTab = -1, LaidOutWidth;
    }

    sealed class SettingsSection
    {
        public Label Title;
        public Card Card;
        public int Tab;
        public string Path, SearchArea;
        public bool LayoutDirty = true;
        public readonly List<SettingsRow> Rows = new List<SettingsRow>();
    }

    sealed class SettingsRow
    {
        public Label Title, Description, Value;
        public Control Lead, Action, Secondary;
        public int ActionWidth;
        public string Keywords, SearchTitle;
    }

    void ArrangePages()
    {
        if (arranging || !layoutReady || IsDisposed || Disposing) return;
        arranging = true;
        SettingsPage current = pageLayouts[nav.Selected];
        Panel body = pages[nav.Selected];
        try
        {
            int width = Math.Max(1, ClientSize.Width - NavW);
            int pad = width < S(570) ? S(18) : S(34);
            int content = Math.Min(S(860), Math.Max(1, width - pad * 2 - SystemInformation.VerticalScrollBarWidth));
            int left = (width - SystemInformation.VerticalScrollBarWidth - content) / 2;
            int titleHeight = Math.Max(S(43), MeasureHeader(pageTitle, content));
            int summaryHeight = Searching ? 0 : Math.Max(S(25), MeasureHeader(pageSummary, content));
            int headerHeight = S(77) + titleHeight + summaryHeight;
            nav.Bounds = new Rectangle(0, 0, NavW, ClientSize.Height);
            header.Bounds = new Rectangle(NavW, 0, width, headerHeight);
            settingsSearch.Bounds = new Rectangle(left, S(16), content, S(40));
            pageTitle.Bounds = new Rectangle(left, S(66), content, titleHeight);
            pageSummary.Bounds = new Rectangle(left, S(68) + titleHeight, content, summaryHeight);
            if (Searching)
            {
                searchResults.Bounds = new Rectangle(NavW + left, headerHeight, content, Math.Max(1, ClientSize.Height - headerHeight - S(12)));
                noSearchResults.Bounds = searchResults.Bounds;
                return;
            }
            current.View.Bounds = new Rectangle(NavW + left - S(4), headerHeight, width - left + S(4), Math.Max(1, ClientSize.Height - headerHeight));
            bool changed = current.LaidOutTab != current.SelectedTab || current.LaidOutWidth != content;
            if (!changed)
                foreach (SettingsSection section in current.Sections)
                    if (section.LayoutDirty && (current.SelectedTab == 0 || section.Tab == current.SelectedTab)) { changed = true; break; }
            if (!changed) return;
            Point scroll = body.AutoScrollPosition;
            body.SuspendLayout();
            try
            {
                body.AutoScrollPosition = Point.Empty;
                int y = S(8);
                int inset = S(2);
                foreach (SettingsSection section in current.Sections)
                {
                    bool shown = current.SelectedTab == 0 || section.Tab == current.SelectedTab;
                    section.Title.Visible = section.Card.Visible = shown;
                    if (!shown) continue;
                    section.Title.Location = new Point(inset + S(3), y);
                    y += section.Title.Height + S(7);
                    section.Card.Location = new Point(inset, y);
                    if (section.LayoutDirty || section.Card.Width != content)
                    {
                        section.Card.Width = content;
                        LayoutRows(section);
                        section.LayoutDirty = false;
                    }
                    y += section.Card.Height + S(19);
                }
                body.AutoScrollMinSize = new Size(0, y);
                current.LaidOutTab = current.SelectedTab;
                current.LaidOutWidth = content;
            }
            finally { body.ResumeLayout(true); }
            body.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
        }
        finally { arranging = false; }
    }

    static int MeasureHeader(Label label, int width)
    {
        return TextRenderer.MeasureText(label.Text, label.Font, new Size(width, 0),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
    }

    void LayoutRows(SettingsSection section)
    {
        Card card = section.Card;
        int pad = S(16), gap = S(10), y = 0;
        int right = card.Width - pad;
        card.Separators.Clear();
        foreach (SettingsRow row in section.Rows)
        {
            if (y > 0) card.Separators.Add(y);
            int textX = row.Lead == null ? pad : pad + row.Lead.Width + gap;
            int extra = (row.Value == null ? 0 : row.Value.Width + gap) +
                        (row.Secondary == null ? 0 : row.Secondary.Width + gap);
            if (row.Action != null)
            {
                row.Action.Width = Math.Max(1, Math.Min(row.ActionWidth, right - pad - extra));
                Segmented segmented = row.Action as Segmented;
                if (segmented != null) segmented.FitSettingsWidth();
            }
            int actionWidth = row.Action == null ? 0 : row.Action.Width + extra;
            bool stacked = actionWidth > 0 && right - textX - actionWidth - gap < S(220);
            int textWidth = Math.Max(1, right - textX - (actionWidth == 0 || stacked ? 0 : actionWidth + gap));
            FitLabel(row.Title, textWidth);
            FitLabel(row.Description, textWidth);
            int titleHeight = row.Title == null ? 0 : row.Title.Height;
            int textGap = row.Title == null || row.Description == null ? 0 : S(4);
            int textHeight = titleHeight + textGap + (row.Description == null ? 0 : row.Description.Height);
            int leadHeight = row.Lead == null ? 0 : row.Lead.Height;
            int actionHeight = row.Action == null ? 0 : row.Action.Height;
            if (row.Secondary != null) actionHeight = Math.Max(actionHeight, row.Secondary.Height);
            if (row.Value != null) actionHeight = Math.Max(actionHeight, row.Value.Height);
            int topHeight = Math.Max(textHeight, Math.Max(leadHeight, stacked ? 0 : actionHeight));
            int height = Math.Max(S(56), topHeight + S(26) + (stacked ? actionHeight + gap : 0));
            int top = y + (stacked ? S(13) : (height - topHeight) / 2);
            int textY = top + (topHeight - textHeight) / 2;
            if (row.Title != null) row.Title.Location = new Point(textX, textY);
            if (row.Description != null) row.Description.Location = new Point(textX, textY + titleHeight + textGap);
            if (row.Lead != null) row.Lead.Location = new Point(pad, top + (topHeight - leadHeight) / 2);
            int actionY = stacked ? top + topHeight + gap : y + (height - actionHeight) / 2;
            int actionRight = right;
            PlaceAction(row.Action, ref actionRight, actionY, actionHeight, gap);
            PlaceAction(row.Value, ref actionRight, actionY, actionHeight, gap);
            PlaceAction(row.Secondary, ref actionRight, actionY, actionHeight, gap);
            y += height;
        }
        card.Height = y;
        card.Invalidate();
    }

    static void FitLabel(Label label, int width)
    {
        if (label != null) label.MaximumSize = new Size(width, 0);
    }

    static void PlaceAction(Control control, ref int right, int y, int height, int gap)
    {
        if (control == null) return;
        control.Location = new Point(right - control.Width, y + (height - control.Height) / 2);
        right = control.Left - gap;
    }
}
