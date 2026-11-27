using System;
using System.Drawing;
using System.Windows.Forms;

sealed class SettingsNavigation : Panel
{
    readonly Panel links = new Panel { AutoScroll = true, TabIndex = 0 };
    readonly NavigationButton[] items;
    FlatButton preview, quit;
    int selected;
    public event Action<int> SelectedChanged;

    public SettingsNavigation(string[] names, string[] glyphs)
    {
        DoubleBuffered = true;
        Font = Ui.Font("Segoe UI Semibold", 10f);
        items = new NavigationButton[names.Length];
        Controls.Add(links);
        for (int i = 0; i < items.Length; i++)
        {
            int index = i;
            var item = new NavigationButton(names[i], glyphs[i]) { TabIndex = i };
            item.Click += (s, e) => { if (SelectedChanged != null) SelectedChanged(index); };
            items[i] = item;
            links.Controls.Add(item);
        }
    }

    public int Selected
    {
        get { return selected; }
        set
        {
            items[selected].Active = false;
            items[selected].Invalidate();
            selected = value;
            items[selected].Active = true;
            items[selected].Invalidate();
            RevealSelection();
        }
    }

    public void AddAction(FlatButton button, bool isPreview)
    {
        if (isPreview) preview = button;
        else quit = button;
        button.EnableSettingsKeyboard();
        button.TabIndex = isPreview ? 1 : 2;
        Controls.Add(button);
    }

    public void RefreshPalette()
    {
        BackColor = links.BackColor = SystemInformation.HighContrast ? SystemColors.Control : Theme.Side;
        Invalidate(true);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (items == null || quit == null || preview == null) return;
        int pad = Ui.S(14), top = Ui.S(80);
        quit.Location = new Point(pad, Height - pad - quit.Height);
        preview.Location = new Point(pad, quit.Top - Ui.S(8) - preview.Height);
        Point scroll = links.AutoScrollPosition;
        links.SuspendLayout();
        try
        {
            links.AutoScrollPosition = Point.Empty;
            links.Bounds = new Rectangle(pad, top, Width - pad * 2, Math.Max(1, preview.Top - top - pad));
            int row = Math.Max(Ui.S(32), Math.Min(Ui.S(45), links.Height / items.Length));
            int extent = items.Length * row;
            int width = links.Width - (extent > links.Height ? SystemInformation.VerticalScrollBarWidth : 0);
            for (int i = 0; i < items.Length; i++)
                items[i].Bounds = new Rectangle(0, i * row, Math.Max(1, width), row - Ui.S(5));
            links.AutoScrollMinSize = new Size(0, extent);
        }
        finally { links.ResumeLayout(true); }
        links.AutoScrollPosition = new Point(0, -scroll.Y);
        RevealSelection();
    }

    void RevealSelection()
    {
        Rectangle item = items[selected].Bounds;
        int delta = item.Top < 0 ? item.Top : Math.Max(0, item.Bottom - links.ClientSize.Height);
        if (delta != 0) links.AutoScrollPosition = new Point(0, -links.AutoScrollPosition.Y + delta);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int x = Ui.S(16);
        Ui.DrawLogo(e.Graphics, new Rectangle(x, Ui.S(21), Ui.S(34), Ui.S(34)), Settings.Accent);
        Color text = SystemInformation.HighContrast ? SystemColors.ControlText : Theme.Fg;
        TextRenderer.DrawText(e.Graphics, "BureauCache", Font, new Point(Ui.S(60), Ui.S(28)), text, TextFormatFlags.NoPrefix);
        using (Pen pen = new Pen(SystemInformation.HighContrast ? SystemColors.ControlDark : Theme.Line))
        {
            e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
            if (preview != null) e.Graphics.DrawLine(pen, x, preview.Top - Ui.S(9), Width - x, preview.Top - Ui.S(9));
        }
    }

    sealed class NavigationButton : Button
    {
        readonly string glyph;
        public bool Active;

        public NavigationButton(string name, string glyph)
        {
            Text = name;
            this.glyph = glyph;
            FlatStyle = FlatStyle.Flat;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData != Keys.Enter) return base.ProcessDialogKey(keyData);
            PerformClick();
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            bool contrast = SystemInformation.HighContrast;
            Color surface = contrast ? SystemColors.Control : Theme.Side;
            Color fg = contrast ? SystemColors.ControlText : Theme.Fg;
            if (contrast && Active) { surface = SystemColors.Highlight; fg = SystemColors.HighlightText; }
            e.Graphics.Clear(surface);
            if (!contrast && (Active || ClientRectangle.Contains(PointToClient(Cursor.Position))))
                Ui.Card(e.Graphics, Active ? Theme.SelOverlay : Theme.HoverOverlay, ClientRectangle, Ui.S(Ui.RadiusControl), Active);   // D-012
            if (Active && !contrast)
                Ui.Fill(e.Graphics, Theme.Accent, new RectangleF(0, Ui.S(10), Ui.S(3), Height - Ui.S(20)), Ui.S(2));
            Ui.Glyph(e.Graphics, glyph, 10f, fg, new Rectangle(Ui.S(7), 0, Ui.S(27), Height));
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(Ui.S(38), 0, Width - Ui.S(42), Height), fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            // Cadre de focus seulement quand on navigue au clavier, pas après un clic (D-012)
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -Ui.S(3), -Ui.S(3)), fg, surface);
        }
    }
}
