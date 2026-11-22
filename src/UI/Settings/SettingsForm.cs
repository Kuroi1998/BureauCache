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
    readonly Font fRow = Ui.Font("Segoe UI", 10.5f);
    readonly Font fDesc = Ui.Font("Segoe UI", 8.75f);
    readonly HashSet<Label> dim = new HashSet<Label>();
    readonly List<Panel> pages = new List<Panel>();
    readonly SideNav nav;
    readonly int W, H, NavW;

    Toggle tQuiet, tHide, tStartup, tAuto, tFull, tHot, tGlass, tLive, tAnim, tWidgets, tPreview, tSystem, tRecents, tGroups,
        tSApps, tSRecent, tSSettings, tSCmd, tNew, tArchive, tAutoUpd;
    Segmented sSide, sZone, sScreen, sTheme, sView, sLang, sEngine, sNewDays, sArchDays;
    Slider slDelay, slWidth;
    Swatches swatch;
    HotkeyBox hk;
    InputBox repo;
    Label lDelay, lWidth, lHotStatus, lInstall, lArchive, lUpdate, lTray;
    FlatButton bInstall, bArchiveNow, bArchiveUndo, bCheck;
    Card customCard;
    string customSig;

    Panel page;
    Card cur;
    int y, rowY;
    bool loading;

    static int S(int v) { return Ui.S(v); }
    static string T(string fr, string en) { return L.T(fr, en); }
    static readonly int[] NewDaysOpts = { 1, 3, 7 };
    static readonly int[] ArchiveOpts = { 30, 60, 90, 180 };

    public int CurrentPage { get { return nav.Selected; } }

    public SettingsForm(SettingsActions act)
    {
        this.act = act;
        W = S(820); H = S(700); NavW = S(220);
        Text = "BureauCache – " + T("Paramètres", "Settings");
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(W, H);
        Icon = Ui.MakeIcon(Settings.Accent);
        KeyPreview = true;

        nav = new SideNav(new[] { T("Général", "General"), T("Ouverture", "Opening"), T("Apparence", "Appearance"),
                                  T("Recherche", "Search"), T("Organisation", "Organization"), T("Avancé", "Advanced") },
                          new[] { Glyphs.Settings, Glyphs.Mouse, Glyphs.Palette, Glyphs.Search, Glyphs.Library, Glyphs.Info });
        nav.Bounds = new Rectangle(0, 0, NavW, H);
        nav.Cursor = Cursors.Default;
        nav.SelectedChanged += ShowPage;
        Controls.Add(nav);

        FlatButton bQuit = new FlatButton(T("Quitter BureauCache", "Quit BureauCache"), false, NavW - S(32));
        bQuit.Location = new Point(S(16), H - S(16) - bQuit.Height);
        bQuit.Click += (s, e) => { if (act.Quit != null) act.Quit(); };
        FlatButton bPrev = new FlatButton(T("Aperçu du panneau", "Preview panel"), true, NavW - S(32));
        bPrev.Location = new Point(S(16), bQuit.Top - S(8) - bPrev.Height);
        bPrev.Click += (s, e) => { if (act.Preview != null) act.Preview(); };
        nav.Controls.Add(bQuit);
        nav.Controls.Add(bPrev);

        BuildGeneral();
        BuildOpening();
        BuildLook();
        BuildSearch();
        BuildOrganize();
        BuildAdvanced();

        LoadValues();
        ShowPage(0);
        Restyle();
        Settings.Changed += OnSettingsChanged;
    }

    protected override void Dispose(bool disposing)
    {
        Settings.Changed -= OnSettingsChanged;
        base.Dispose(disposing);
    }

    // ---------- Construction ----------
    void NewPage(string title)
    {
        page = new BufferedPanel { Bounds = new Rectangle(NavW, 0, W - NavW, H), AutoScroll = true, Visible = false };
        page.AutoScrollMargin = new Size(0, S(24));
        page.Controls.Add(new Label { Text = title, Font = fPage, AutoSize = true, Location = new Point(S(28), S(24)) });
        pages.Add(page);
        Controls.Add(page);
        y = S(80);
    }

    void Section(string title)
    {
        Label l = new Label { Text = title, Font = fSection, AutoSize = true, Location = new Point(S(32), y) };
        dim.Add(l);
        page.Controls.Add(l);
        y += S(24);
        cur = new Card { Location = new Point(S(24), y), Width = page.Width - S(48) - S(14) };
        page.Controls.Add(cur);
        rowY = 0;
    }

    Label Row(string title, string desc, Control ctl, Label value, Control lead = null)
    {
        if (rowY > 0) cur.Separators.Add(rowY);
        int h = desc != null ? S(62) : S(52);
        int th = TextRenderer.MeasureText("Ag", fRow).Height;
        int tx = S(16);
        if (lead != null)
        {
            lead.Location = new Point(S(16), rowY + (h - lead.Height) / 2);
            cur.Controls.Add(lead);
            tx = lead.Right + S(12);
        }
        Label t = new Label { Text = title, Font = fRow, AutoSize = true };
        t.Location = new Point(tx, desc != null ? rowY + S(11) : rowY + (h - th) / 2);
        cur.Controls.Add(t);
        Label d = null;
        if (desc != null)
        {
            d = new Label { Text = desc, Font = fDesc, AutoSize = true, Location = new Point(tx, rowY + S(12) + th) };
            dim.Add(d);
            cur.Controls.Add(d);
        }
        int right = cur.Width - S(16);
        if (ctl != null)
        {
            ctl.Location = new Point(right - ctl.Width, rowY + (h - ctl.Height) / 2);
            cur.Controls.Add(ctl);
            right = ctl.Left - S(8);
        }
        if (value != null)
        {
            value.Location = new Point(right - value.Width, rowY + (h - value.Height) / 2);
            cur.Controls.Add(value);
        }
        rowY += h;
        cur.Height = rowY;
        return d;
    }

    void Note(string text)
    {
        if (rowY > 0) cur.Separators.Add(rowY);
        Label l = new Label { Text = text, Font = fDesc, AutoSize = true, MaximumSize = new Size(cur.Width - S(32), 0) };
        l.Location = new Point(S(16), rowY + S(14));
        dim.Add(l);
        cur.Controls.Add(l);
        rowY += l.Height + S(28);
        cur.Height = rowY;
    }

    void EndSection() { y += cur.Height + S(22); }

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
        FlatButton b = new FlatButton(text, primary, width);
        b.Height = S(32);
        b.Click += (s, e) => click();
        return b;
    }

    bool Ask(string msg)
    {
        return Dialogs.Ask(this, msg);
    }
}
