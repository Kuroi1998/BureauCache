using System;
using System.Drawing;
using System.Windows.Forms;

partial class SettingsForm
{
    int buildingTab;

    void Subtab(string title)
    {
        SettingsPage model = pageLayouts[pageLayouts.Count - 1];
        model.Tabs.Add(title);
        buildingTab = model.Tabs.Count;
    }

    void BuildSettingsTabs()
    {
        for (int i = 0; i < pageLayouts.Count; i++)
        {
            SettingsPage model = pageLayouts[i];
            Panel body = pages[i];
            var view = new SettingsTabControl(model.Title) { Visible = false, TabIndex = 2 };
            model.View = view;
            view.TabPages.Add(T("Tous", "All", "Todo"));
            foreach (string title in model.Tabs) view.TabPages.Add(title);
            view.FitTabLabels();
            foreach (TabPage tab in view.TabPages) tab.UseVisualStyleBackColor = false;
            model.Scroll = new Point[view.TabCount];
            body.Dock = DockStyle.Fill;
            body.Visible = true;
            view.TabPages[0].Controls.Add(body);
            view.SelectedIndexChanged += (s, e) => SwitchSubtab(model, body);
            Controls.Add(view);
        }
    }

    void SwitchSubtab(SettingsPage model, Panel body)
    {
        int next = model.View.SelectedIndex;
        if (next < 0 || next == model.SelectedTab) return;
        Point scroll = body.AutoScrollPosition;
        model.Scroll[model.SelectedTab] = new Point(-scroll.X, -scroll.Y);
        model.SelectedTab = next;
        model.View.TabPages[next].Controls.Add(body);
        model.View.RefreshTabName();
        ClearRevealedRow();
        ArrangePages();
        body.AutoScrollPosition = model.Scroll[next];
        QueueDesktopAnalysis();
        RefreshTrayIconStatus();
    }
}

sealed class SettingsTabControl : TabControl
{
    readonly string category;

    public SettingsTabControl(string category)
    {
        this.category = category;
        Appearance = TabAppearance.FlatButtons;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = Ui.Font("Segoe UI Semibold", 9.5f);
        Padding = new Point(Ui.S(10), Ui.S(4));
        Multiline = true;
        SizeMode = TabSizeMode.Fixed;
        SelectedIndexChanged += (s, e) => Invalidate();
        GotFocus += (s, e) => Invalidate();
        LostFocus += (s, e) => Invalidate();
    }

    public void FitTabLabels()
    {
        int minWidth = Ui.S(56);
        foreach (TabPage tab in TabPages)
            minWidth = Math.Max(minWidth, TextRenderer.MeasureText(tab.Text, Font).Width + Ui.S(18));
        ItemSize = new Size(minWidth, Ui.S(33));
        RefreshTabName();
    }

    public void RefreshTabName()
    {
        if (SelectedTab == null) return;
        AccessibleName = L.T("Sous-onglets de ", "Subtabs for ", "Pestañas de ") + category + ". " +
            L.T("Onglet actif : ", "Current tab: ", "Pestaña activa: ") + SelectedTab.Text;
        if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Modifiers == Keys.None && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
        {
            int direction = e.KeyCode == Keys.Right ? 1 : -1;
            SelectedIndex = Math.Max(0, Math.Min(TabCount - 1, SelectedIndex + direction));
            e.SuppressKeyPress = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Bg);
        for (int i = 0; i < TabCount; i++) DrawTab(e.Graphics, i);
        base.OnPaint(e);
    }

    void DrawTab(Graphics graphics, int index)
    {
        Rectangle bounds = GetTabRect(index);
        bool selected = index == SelectedIndex;
        bool contrast = SystemInformation.HighContrast;
        Color fill = contrast ? (selected ? SystemColors.Highlight : SystemColors.Control)
            : selected ? Theme.Mix(Theme.Surface, Theme.Accent, 0.16f) : Theme.Surface;
        Color fg = contrast ? (selected ? SystemColors.HighlightText : SystemColors.ControlText) : Theme.Fg;
        var pill = new Rectangle(bounds.X, bounds.Y + Ui.S(2), bounds.Width - 1, bounds.Height - Ui.S(5));
        Ui.Fill(graphics, fill, pill, Ui.S(Ui.RadiusControl));
        if (selected || contrast) Ui.Edge(graphics, selected && !contrast ? Theme.Accent : fg, pill, Ui.S(Ui.RadiusControl));
        TextRenderer.DrawText(graphics, TabPages[index].Text, Font, pill, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        if (selected && Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(pill, -3, -3), fg, fill);
    }
}
