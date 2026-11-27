// BureauCache — SettingsForm
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

class SettingsActions
{
    public Action Preview, Quit, Install, Uninstall, InstallUpdate, Welcome, HiddenToTray;
    public Func<bool> HotkeyOk;
}

partial class SettingsForm : Form
{
    readonly SettingsActions act;
    readonly Font fPage = Ui.Font("Segoe UI Semibold", 17f);
    readonly Font fSection = Ui.Font("Segoe UI Semibold", 8.75f);
    readonly Font fRow = Ui.Font("Segoe UI Semibold", 10f);
    readonly Font fDesc = Ui.Font("Segoe UI", 9f);
    readonly HashSet<Label> dim = new HashSet<Label>();
    readonly List<Panel> pages = new List<Panel>();
    readonly List<SettingsPage> pageLayouts = new List<SettingsPage>();
    readonly Dictionary<Card, List<SettingsRow>> cardRows = new Dictionary<Card, List<SettingsRow>>();
    readonly SettingsNavigation nav;
    readonly Label pageTitle, pageSummary;
    readonly Panel header;
    readonly int NavW;

    Toggle tQuiet, tHide, tStartup, tAuto, tFull, tHot, tGlass, tLive, tCapture, tAnim, tWidgets, tWeather, tPreview, tSystem, tRecents, tGroups,
        tSApps, tSRecent, tSSettings, tSCmd, tSContent, tClip, tTaskbar, tNew, tArchive, tAutoUpd;
    Segmented sSide, sZone, sScreen, sTheme, sView, sLang, sEngine, sNewDays, sArchDays;
    Slider slDelay, slWidth;
    Swatches swatch;
    HotkeyBox hk;
    InputBox repo;
    Label lDelay, lWidth, lHotStatus, lInstall, lArchive, lUpdate, lTray, lWeatherPlace, lExport, lImport, lRestore, lArchiveUndo;
    FlatButton bInstall, bArchiveNow, bArchiveUndo, bCheck;
    Card customCard;
    string customSig;

    Panel page;
    Card cur;
    bool loading, arranging, layoutReady;

    static int S(int v) { return Ui.S(v); }
    static string T(string fr, string en, string es = null) { return L.T(fr, en, es); }
    static readonly int[] NewDaysOpts = Settings.NewDaysChoices;
    static readonly int[] ArchiveOpts = Settings.ArchiveDaysChoices;

    public int CurrentPage { get { return nav.Selected; } }

    public SettingsForm(SettingsActions act)
    {
        this.act = act;
        NavW = S(204);
        Text = "BureauCache – " + T("Paramètres", "Settings", "Configuración");
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        Size work = Screen.FromPoint(Cursor.Position).WorkingArea.Size;
        MinimumSize = new Size(Math.Min(S(650), work.Width), Math.Min(S(430), work.Height));
        ClientSize = new Size(Math.Max(1, Math.Min(S(980), work.Width - S(26))),
                              Math.Max(1, Math.Min(S(730), work.Height - S(56))));
        KeyPreview = true;

        nav = new SettingsNavigation(new[] { T("Général", "General", "General"), T("Ouverture", "Opening", "Apertura"), T("Apparence", "Appearance", "Apariencia"),
                                  T("Recherche", "Search", "Búsqueda"), T("Organisation", "Organization", "Organización"), T("Avancé", "Advanced", "Opciones avanzadas") },
                          new[] { Glyphs.Settings, Glyphs.Mouse, Glyphs.Palette, Glyphs.Search, Glyphs.Library, Glyphs.Info });
        nav.Width = NavW;
        nav.TabIndex = 0;
        nav.SelectedChanged += ShowPage;
        Controls.Add(nav);

        // Boutons du bas (D-012) : « Quitter » discret, « Aperçu » secondaire au lieu du bleu plein
        FlatButton bQuit = new FlatButton(T("Quitter BureauCache", "Quit BureauCache", "Salir de BureauCache"), false, NavW - S(32)) { Ghost = true };
        nav.AddAction(bQuit, false);
        bQuit.Click += (s, e) => { if (act.Quit != null) act.Quit(); };
        FlatButton bPrev = new FlatButton(T("Aperçu du panneau", "Preview panel", "Vista previa"), false, NavW - S(32));
        nav.AddAction(bPrev, true);
        bPrev.Click += (s, e) => { if (act.Preview != null) act.Preview(); };
        header = new Panel { TabStop = false };
        pageTitle = new Label { Font = fPage, AutoEllipsis = true };
        pageSummary = new Label { Font = fDesc, AutoEllipsis = true };
        dim.Add(pageSummary);
        header.Controls.Add(pageTitle);
        header.Controls.Add(pageSummary);
        Controls.Add(header);

        BuildGeneral();
        BuildOpening();
        BuildLook();
        BuildSearch();
        BuildOrganize();
        BuildAdvanced();

        LoadValues();
        layoutReady = true;
        ShowPage(0);
        Restyle();
        ClientSizeChanged += (s, e) => ArrangePages();
        Settings.Changed += OnSettingsChanged;
    }

    protected override void Dispose(bool disposing)
    {
        Icon ownedIcon = null;
        if (disposing)
        {
            Settings.Changed -= OnSettingsChanged;
            DisposePlaceMenu();
            ownedIcon = settingsIcon;
            settingsIcon = null;
        }
        base.Dispose(disposing);
        if (ownedIcon != null) ownedIcon.Dispose();
    }

    // ---------- Construction ----------
    void NewPage(string title, string summary)
    {
        page = new BufferedPanel { AutoScroll = true, Visible = false, TabIndex = 1 };
        pageLayouts.Add(new SettingsPage { Title = title, Summary = summary });
        pages.Add(page);
        Controls.Add(page);
    }

    void Section(string title)
    {
        Label label = new Label { Text = title, Font = fSection, AutoSize = true };
        dim.Add(label);
        page.Controls.Add(label);
        cur = new Card { TabIndex = page.Controls.Count };
        page.Controls.Add(cur);
        var section = new SettingsSection { Title = label, Card = cur };
        pageLayouts[pageLayouts.Count - 1].Sections.Add(section);
        cardRows.Add(cur, section.Rows);
    }

    Label Row(string title, string desc, Control ctl, Label value, Control lead = null, Control secondary = null)
    {
        var row = new SettingsRow { Title = new Label { Text = title, Font = fRow, AutoSize = true, UseMnemonic = false },
            Action = ctl, ActionWidth = ctl == null ? 0 : ctl.Width, Secondary = secondary, Value = value, Lead = lead };
        if (desc != null)
        {
            row.Description = Description(desc);
            Panel ownerPage = (Panel)cur.Parent;
            row.Description.TextChanged += (s, e) =>
            {
                if (!loading && layoutReady && pages[nav.Selected] == ownerPage) ArrangePages();
            };
        }
        if (ctl != null)
        {
            ctl.AccessibleName = title;
            InputBox input = ctl as InputBox;
            if (input != null) input.Box.AccessibleName = title;
        }
        if (secondary != null) secondary.AccessibleName = secondary.Text + " · " + title;
        if (ctl is FlatButton && lead != null) ctl.AccessibleName = ctl.Text + " · " + title;
        AddRow(row, row.Title, row.Description, lead, secondary, ctl, value);
        return row.Description;
    }

    void Note(string text)
    {
        Label label = Description(text);
        AddRow(new SettingsRow { Description = label }, label);
    }

    Label Description(string text)
    {
        Label label = new Label { Text = text, Font = fDesc, AutoSize = true, UseMnemonic = false };
        dim.Add(label);
        return label;
    }

    void AddRow(SettingsRow row, params Control[] controls)
    {
        foreach (Control control in controls)
        {
            if (control == null) continue;
            control.TabIndex = cur.Controls.Count;
            FlatControl flat = control as FlatControl;
            if (flat != null && control != row.Lead) flat.EnableSettingsKeyboard();
            cur.Controls.Add(control);
        }
        cardRows[cur].Add(row);
    }

    Label ValueLabel()
    {
        Label l = new Label { AutoSize = false, Size = new Size(S(64), S(24)), TextAlign = ContentAlignment.MiddleRight, Font = fDesc };
        dim.Add(l);
        return l;
    }

    Toggle NewToggle(Action<bool> set)
    {
        Toggle t = new Toggle();
        t.Changed += (s, e) => { if (!loading) { set(t.Checked); Settings.Notify(); } };
        return t;
    }

    Segmented NewSeg(Action<int> set, params string[] opts)
    {
        Segmented sg = new Segmented(opts);
        sg.Changed += (s, e) => { if (!loading) { set(sg.Selected); Settings.Notify(); } };
        return sg;
    }

    FlatButton Button(string text, bool primary, int width, Action click)
    {
        FlatButton b = new FlatButton(text, primary);
        b.Width = Math.Max(width, b.Width);
        b.Height = S(32);
        b.Click += (s, e) => click();
        return b;
    }

    bool Ask(string msg)
    {
        return Dialogs.Ask(this, msg);
    }
}
