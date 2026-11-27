using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

partial class SettingsForm
{
    SettingsSearchBox settingsSearch;
    ListBox searchResults;
    Label noSearchResults;
    SettingsRow revealedRow;
    SettingsSection revealedSection;
    bool clearingSearch;
    bool searchCatalogDirty, searchViewActive;
    string lastSearchQuery;
    SettingMatch lastSearchMatch;

    bool Searching { get { return !string.IsNullOrWhiteSpace(settingsSearch.Box.Text); } }

    sealed class SettingMatch
    {
        public SettingsRow Row;
        public SettingsSection Section;
        public int Page, Rank;
        public string Path;
        public override string ToString() { return Row.Title.Text + " — " + Path; }
    }

    void BuildSettingsSearch()
    {
        settingsSearch = new SettingsSearchBox(T("Rechercher un réglage", "Find a setting", "Buscar un ajuste")) { TabIndex = 0 };
        header.TabIndex = 1;
        header.Controls.Add(settingsSearch);
        searchResults = new ListBox { Visible = false, TabIndex = 2, DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = S(60), IntegralHeight = false, BorderStyle = BorderStyle.None,
            AccessibleName = T("Résultats des réglages", "Settings results", "Resultados de configuración") };
        noSearchResults = Description(T("Aucun réglage trouvé. Essaie un autre mot-clé.",
            "No settings found. Try another keyword.", "No se encontraron ajustes. Prueba otra palabra clave."));
        noSearchResults.AutoSize = false;
        noSearchResults.Visible = false;
        Controls.Add(searchResults);
        Controls.Add(noSearchResults);
        settingsSearch.Box.TextChanged += (s, e) =>
        {
            if (clearingSearch) return;
            if (!Searching) { lastSearchQuery = null; lastSearchMatch = null; }
            RefreshSettingsSearch();
        };
        settingsSearch.Box.KeyDown += (s, e) =>
        {
            if (searchResults.Items.Count == 0 || !Searching || e.Modifiers != Keys.None) return;
            if (e.KeyCode == Keys.Enter) OpenSettingMatch();
            else if (e.KeyCode == Keys.Down) MoveSearchSelection(1);
            else if (e.KeyCode == Keys.Up) MoveSearchSelection(-1);
            else return;
            e.SuppressKeyPress = true;
        };
        object pressedResult = null;
        searchResults.MouseDown += (s, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            int index = searchResults.IndexFromPoint(e.Location);
            pressedResult = index < 0 ? null : searchResults.Items[index];
        };
        searchResults.MouseUp += (s, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            int index = searchResults.IndexFromPoint(e.Location);
            bool sameResult = index >= 0 && ReferenceEquals(searchResults.Items[index], pressedResult);
            pressedResult = null;
            if (sameResult)
            {
                searchResults.SelectedIndex = index;
                OpenSettingMatch();
            }
        };
        searchResults.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            OpenSettingMatch();
        };
        searchResults.DrawItem += DrawSettingMatch;
        searchResults.HandleCreated += (s, e) => Native.SetDarkScrollbars(searchResults.Handle, Settings.Dark && !SystemInformation.HighContrast);
    }

    void MoveSearchSelection(int offset)
    {
        searchResults.SelectedIndex = Math.Max(0, Math.Min(searchResults.Items.Count - 1, searchResults.SelectedIndex + offset));
    }

    IEnumerable<SettingMatch> FindSettings(string query)
    {
        string normalized = Search.Norm(query);
        if (normalized.Length == 0) yield break;
        string[] words = normalized.Split(' ');
        string[] prefixes = words.Select(word => " " + word).ToArray();
        for (int i = 0; i < pageLayouts.Count; i++)
        {
            SettingsPage model = pageLayouts[i];
            foreach (SettingsSection section in model.Sections)
            {
                foreach (SettingsRow row in section.Rows)
                {
                    if (row.Title == null) continue;
                    string title = row.SearchTitle;
                    int rank = title == normalized ? 400 : title.StartsWith(normalized, StringComparison.Ordinal) ? 300 : 0;
                    bool matched = true;
                    for (int k = 0; k < words.Length; k++)
                    {
                        if (HasKeyword(title, words[k], prefixes[k])) rank += 12;
                        else if (HasKeyword(row.Keywords, words[k], prefixes[k])) rank += 6;
                        else if (HasKeyword(section.SearchArea, words[k], prefixes[k])) rank += 2;
                        else { matched = false; break; }
                    }
                    if (matched) yield return new SettingMatch { Row = row, Section = section, Page = i, Path = section.Path, Rank = rank };
                }
            }
        }
    }

    static bool HasKeyword(string text, string word, string prefix)
    {
        return text.StartsWith(word, StringComparison.Ordinal) || text.IndexOf(prefix, StringComparison.Ordinal) >= 0;
    }

    void RefreshSettingsSearch(bool keepSelection = false)
    {
        if (!layoutReady) return;
        searchCatalogDirty = false;
        ClearRevealedRow();
        var previous = keepSelection ? searchResults.SelectedItem as SettingMatch : null;
        searchResults.BeginUpdate();
        try
        {
            searchResults.Items.Clear();
            if (Searching)
            {
                int selected = 0;
                foreach (SettingMatch match in FindSettings(settingsSearch.Box.Text).OrderByDescending(item => item.Rank))
                {
                    int index = searchResults.Items.Add(match);
                    if (SameSetting(previous, match)) selected = index;
                }
                if (searchResults.Items.Count > 0) searchResults.SelectedIndex = selected;
            }
        }
        finally { searchResults.EndUpdate(); }
        UpdateSettingsView();
    }

    void UpdateSettingsView()
    {
        bool searching = Searching;
        searchResults.Visible = searching && searchResults.Items.Count > 0;
        noSearchResults.Visible = searching && searchResults.Items.Count == 0;
        if (searching && searchViewActive) return;
        searchViewActive = searching;
        for (int k = 0; k < pageLayouts.Count; k++) pageLayouts[k].View.Visible = !searching && k == nav.Selected;
        pageTitle.Text = searching ? searchResults.AccessibleName : pageLayouts[nav.Selected].Title;
        pageSummary.Text = pageLayouts[nav.Selected].Summary;
        pageSummary.Visible = !searching;
        ArrangePages();
        QueueDesktopAnalysis();
    }

    void ClearSettingsSearch()
    {
        clearingSearch = true;
        try { settingsSearch.Box.Clear(); }
        finally { clearingSearch = false; }
        searchResults.Items.Clear();
    }

    static bool SameSetting(SettingMatch previous, SettingMatch match)
    {
        return previous != null && (previous.Row == match.Row ||
            (previous.Section == match.Section && previous.Row.Title.Text == match.Row.Title.Text));
    }

    void RememberSettingsSearch()
    {
        if (!Searching) return;
        lastSearchQuery = settingsSearch.Box.Text;
        lastSearchMatch = searchResults.SelectedItem as SettingMatch;
    }

    void ResumeSettingsSearch()
    {
        if (!Searching && lastSearchQuery != null)
        {
            settingsSearch.Box.Text = lastSearchQuery;
            for (int i = 0; i < searchResults.Items.Count; i++)
            {
                if (!SameSetting(lastSearchMatch, (SettingMatch)searchResults.Items[i])) continue;
                searchResults.SelectedIndex = i;
                break;
            }
        }
        settingsSearch.Box.Focus();
        settingsSearch.Box.SelectAll();
    }

    void OpenSettingMatch()
    {
        var match = searchResults.SelectedItem as SettingMatch;
        if (match == null || match.Row.Title.IsDisposed) return;
        ShowPage(match.Page);
        SettingsPage model = pageLayouts[match.Page];
        model.View.SelectedIndex = match.Section.Tab;
        revealedRow = match.Row;
        revealedSection = match.Section;
        revealedSection.Card.Invalidate();

        Control target = match.Row.Action is InputBox ? ((InputBox)match.Row.Action).Box :
            match.Row.Secondary is InputBox ? ((InputBox)match.Row.Secondary).Box : match.Row.Action;
        if (target == null || !target.Focus())
        {
            if (match.Row.Secondary == null || !match.Row.Secondary.Focus()) model.View.Focus();
        }
        Panel body = pages[match.Page];
        int top = revealedSection.Card.Top + revealedRow.Title.Top - body.AutoScrollPosition.Y;
        int scrollY = Math.Max(0, top - S(24));
        if (target != null && target.Focused)
        {
            int bottom = body.PointToClient(target.PointToScreen(new Point(0, target.Height))).Y - body.AutoScrollPosition.Y;
            scrollY = Math.Max(scrollY, bottom - body.ClientSize.Height);
        }
        body.AutoScrollPosition = new Point(0, scrollY);
    }

    void ClearRevealedRow()
    {
        if (revealedSection != null) revealedSection.Card.Invalidate();
        revealedSection = null;
        revealedRow = null;
    }

    void PaintRevealedRow(Graphics graphics, SettingsSection section)
    {
        if (revealedSection != section || revealedRow.Title.IsDisposed) return;
        int height = revealedRow.Title.Height + (revealedRow.Description == null ? 0 : S(4) + revealedRow.Description.Height);
        Ui.Fill(graphics, Theme.Accent, new RectangleF(S(5), revealedRow.Title.Top, S(3), height), S(2));
    }

    void DrawSettingMatch(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= searchResults.Items.Count) return;
        var match = (SettingMatch)searchResults.Items[e.Index];
        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool contrast = SystemInformation.HighContrast;
        Color fill = selected ? (contrast ? SystemColors.Highlight : Theme.Mix(Theme.Card, Theme.Accent, 0.14f)) : Theme.Card;
        Color fg = selected && contrast ? SystemColors.HighlightText : Theme.Fg;
        using (var brush = new SolidBrush(searchResults.BackColor)) e.Graphics.FillRectangle(brush, e.Bounds);
        var tile = new Rectangle(e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Width - 2, e.Bounds.Height - S(7));
        Ui.Fill(e.Graphics, fill, tile, S(Ui.RadiusCard));
        Ui.Edge(e.Graphics, contrast ? fg : Theme.Edge, tile, S(Ui.RadiusCard));
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        var text = new Rectangle(tile.X + S(13), tile.Y + S(5), Math.Max(1, tile.Width - S(50)), S(23));
        TextRenderer.DrawText(e.Graphics, match.Row.Title.Text, fRow, text, fg, flags);
        text.Y += S(23);
        text.Height = S(19);
        TextRenderer.DrawText(e.Graphics, match.Path, fDesc, text, selected && contrast ? fg : Theme.FgDim, flags);
        TextRenderer.DrawText(e.Graphics, "›", fRow, new Rectangle(tile.Right - S(25), tile.Top, S(18), tile.Height), fg, flags);
        if ((e.State & DrawItemState.Focus) != 0 && searchResults.Focused)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(tile, -S(3), -S(3)), fg, fill);
    }
}

sealed class SettingsSearchBox : InputBox
{
    readonly Label placeholder;

    public SettingsSearchBox(string name) : base(1)
    {
        Padding = new Padding(Ui.S(38), Ui.S(11), Ui.S(75), 0);
        Box.AccessibleName = name;
        Box.MaxLength = 150;
        placeholder = new Label { Text = name, Font = Box.Font, AutoEllipsis = true, UseMnemonic = false, AccessibleRole = AccessibleRole.None };
        placeholder.Click += (s, e) => Box.Focus();
        Controls.Add(placeholder);
        placeholder.BringToFront();
        Box.LocationChanged += (s, e) => placeholder.Bounds = Box.Bounds;
        Box.SizeChanged += (s, e) => placeholder.Bounds = Box.Bounds;
        placeholder.Bounds = Box.Bounds;
        Box.GotFocus += (s, e) => RefreshPlaceholder();
        Box.LostFocus += (s, e) => RefreshPlaceholder();
        Box.TextChanged += (s, e) => RefreshPlaceholder();
    }

    public void ApplySearchTheme()
    {
        placeholder.BackColor = Box.BackColor;
        placeholder.ForeColor = SystemInformation.HighContrast ? SystemColors.ControlText : Theme.FgDim;
    }

    void RefreshPlaceholder()
    {
        placeholder.Visible = !Box.Focused && Box.TextLength == 0;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Color fg = SystemInformation.HighContrast ? SystemColors.WindowText : Theme.FgDim;
        Ui.Edge(e.Graphics, Box.Focused ? Theme.Accent : Theme.Track,
            new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(Ui.RadiusControl));
        Ui.Glyph(e.Graphics, Glyphs.Search, 11f, fg, new Rectangle(Ui.S(8), 0, Ui.S(23), Height));
        var hint = new Rectangle(Width - Ui.S(64), Ui.S(8), Ui.S(55), Height - Ui.S(16));
        Ui.Fill(e.Graphics, Theme.Card, hint, Ui.S(5));
        TextRenderer.DrawText(e.Graphics, "Ctrl+F", Box.Font, hint, Theme.Fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
