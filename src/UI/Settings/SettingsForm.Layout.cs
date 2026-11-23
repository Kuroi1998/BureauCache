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
    }

    sealed class SettingsSection
    {
        public Label Title;
        public Card Card;
        public readonly List<SettingsRow> Rows = new List<SettingsRow>();
    }

    sealed class SettingsRow
    {
        public Label Title, Description, Value;
        public Control Lead, Action, Secondary;
        public int ActionWidth;
    }

    void ArrangePages()
    {
        if (arranging || !layoutReady || IsDisposed || Disposing) return;
        arranging = true;
        Panel body = pages[nav.Selected];
        try
        {
            int width = Math.Max(1, ClientSize.Width - NavW);
            int pad = width < S(570) ? S(18) : S(34);
            int content = Math.Min(S(860), Math.Max(1, width - pad * 2 - SystemInformation.VerticalScrollBarWidth));
            int left = (width - SystemInformation.VerticalScrollBarWidth - content) / 2;
            int headerHeight = S(104);
            nav.Bounds = new Rectangle(0, 0, NavW, ClientSize.Height);
            header.Bounds = new Rectangle(NavW, 0, width, headerHeight);
            pageTitle.Bounds = new Rectangle(left, S(20), content, S(38));
            pageSummary.Bounds = new Rectangle(left, S(62), content, S(24));
            Point scroll = body.AutoScrollPosition;
            body.SuspendLayout();
            try
            {
                body.AutoScrollPosition = Point.Empty;
                body.Bounds = new Rectangle(NavW, headerHeight, width, Math.Max(1, ClientSize.Height - headerHeight));
                int y = S(14);
                foreach (SettingsSection section in pageLayouts[nav.Selected].Sections)
                {
                    section.Title.Location = new Point(left + S(3), y);
                    y += section.Title.Height + S(9);
                    section.Card.Location = new Point(left, y);
                    section.Card.Width = content;
                    LayoutRows(section);
                    y += section.Card.Height + S(24);
                }
                body.AutoScrollMinSize = new Size(0, y);
            }
            finally { body.ResumeLayout(true); }
            body.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
        }
        finally { arranging = false; }
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
            if (row.Action != null) row.Action.Width = Math.Max(1, Math.Min(row.ActionWidth, right - pad - extra));
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
