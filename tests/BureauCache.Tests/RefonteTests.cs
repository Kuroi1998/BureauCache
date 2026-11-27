using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-031, refonte (D-012) : étape 1, défauts visibles sur les captures du chef de projet.
[TestClass]
public class RefonteTests
{
    // Largeur mesurée simplement : 10 px par caractère
    static int M(string s) { return s.Length * 10; }

    [TestMethod]
    public void TileNamesNeverBreakInsideAWord()
    {
        CollectionAssert.AreEqual(new[] { "Steam" }, ItemsView.SplitTileName("Steam", 80, M));
        // « Win-Debloat-Tools-main » : coupure après un tiret, jamais « Win-Debloat-To / ols-main »
        string[] lines = ItemsView.SplitTileName("Win-Debloat-Tools-main", 120, M);
        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual("Win-Debloat-", lines[0]);
        Assert.AreEqual("Tools-main", lines[1]);
        // Un seul long mot : une ligne, que le dessin termine par « … » (pas « ParalivesPhotoM / ode »)
        CollectionAssert.AreEqual(new[] { "ParalivesPhotoMode" }, ItemsView.SplitTileName("ParalivesPhotoMode", 100, M));
        // Plusieurs mots : la coupure tombe à l'espace la plus lointaine qui tient
        CollectionAssert.AreEqual(new[] { "Assassin's Creed", "Shadows" }, ItemsView.SplitTileName("Assassin's Creed Shadows", 170, M));
        CollectionAssert.AreEqual(new[] { "RatioMaster-1.9.", "1" }, ItemsView.SplitTileName("RatioMaster-1.9.1", 160, M));
        CollectionAssert.AreEqual(new[] { "" }, ItemsView.SplitTileName(null, 80, M));
    }

    [TestMethod]
    public void PanelShowsCityAndCountryOnly()
    {
        Assert.AreEqual("Saint-Ghislain, Belgique", Weather.ShortPlace("Saint-Ghislain, wallonne, Région, Belgique"), "Lieu enregistré par la 3.10.");
        Assert.AreEqual("Mons, Belgique", Weather.ShortPlace("Mons, Région wallonne, Belgique"));
        Assert.AreEqual("Paris, France", Weather.ShortPlace("Paris, France"));
        Assert.AreEqual("Lima", Weather.ShortPlace("Lima"));
        Assert.AreEqual("", Weather.ShortPlace(null));
    }

    static void RunSta(Action action)
    {
        Exception error = null;
        Thread t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.IsBackground = true;
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        Assert.IsTrue(t.Join(10000));
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    [TestMethod]
    public void ClipboardGroupIsAListAndRecentsSkipSystemItems()
    {
        RunSta(() =>
        {
            bool clip = Settings.ClipboardOn, recents = Settings.ShowRecents;
            Dictionary<string, long[]> usage = Settings.Usage;
            long since = Settings.RecentsSince;
            ClipboardHistory.Clear();
            try
            {
                Settings.ClipboardOn = true;
                ClipboardHistory.Add("un texte");
                using (PanelForm panel = new PanelForm())
                {
                    BindingFlags priv = BindingFlags.Instance | BindingFlags.NonPublic;
                    Group g = (Group)typeof(PanelForm).GetMethod("ClipGroup", priv).Invoke(panel, new object[] { 16 });
                    Assert.IsTrue(g.AsList, "Le presse-papiers est une liste compacte, même en vue grille.");

                    // La Corbeille ouverte récemment n'apparaît pas dans les Récents (elle a déjà son widget)
                    DesktopItem bin = new DesktopItem { Key = "::corbeille", Name = "Corbeille", IsSystem = true };
                    DesktopItem doc = new DesktopItem { Key = @"C:\Bureau\rapport.docx", Name = "rapport.docx" };
                    panel.All = new List<DesktopItem> { bin, doc };
                    Settings.ShowRecents = true;
                    Settings.RecentsSince = 0;
                    Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
                    Settings.Usage[bin.Key] = new long[] { 3, DateTime.Now.Ticks };
                    Settings.Usage[doc.Key] = new long[] { 1, DateTime.Now.Ticks };
                    List<Group> groups = (List<Group>)typeof(PanelForm).GetMethod("BuildGroups", priv).Invoke(panel, null);
                    Group rec = groups.First(x => x.Key == "recent");
                    CollectionAssert.AreEqual(new[] { "rapport.docx" }, rec.Items.Select(i => i.Name).ToArray());
                }
            }
            finally
            {
                ClipboardHistory.Clear();
                Settings.ClipboardOn = clip; Settings.ShowRecents = recents; Settings.Usage = usage; Settings.RecentsSince = since;
            }
        });
    }

    [TestMethod]
    public void SharedEdgeIsAFaintOnePixelLine()
    {
        int mode = Settings.ThemeMode;
        try
        {
            Settings.ThemeMode = 1;   // sombre
            double a = Theme.Edge.A / 255.0;
            Assert.IsTrue(a >= 0.06 && a <= 0.10, "Étape 2 : bordure blanche entre 6 et 10 % (" + a.ToString("0.000") + ").");
            Assert.IsTrue(Theme.EdgeHot.A > Theme.Edge.A, "Plus marquée au survol.");
            Theme.HighContrastOverride = true;
            Assert.AreEqual(System.Drawing.SystemColors.WindowText, Theme.Edge, "Contraste élevé : bordure aux couleurs système.");
        }
        finally { Theme.HighContrastOverride = null; Settings.ThemeMode = mode; }
        Assert.AreEqual(10, Ui.RadiusCard);
        Assert.AreEqual(8, Ui.RadiusControl);
    }

    [TestMethod]
    public void CaptionColorIsSentAsColorRef()
    {
        // COLORREF 0x00BBGGRR attendu par DWMWA_CAPTION_COLOR (35) et DWMWA_TEXT_COLOR (36)
        Assert.AreEqual(0x00332211, Native.ColorRef(System.Drawing.Color.FromArgb(0x11, 0x22, 0x33)));
    }

    static double Lum(System.Drawing.Color c)
    {
        Func<int, double> lin = v => { double x = v / 255.0; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); };
        return 0.2126 * lin(c.R) + 0.7152 * lin(c.G) + 0.0722 * lin(c.B);
    }

    static System.Drawing.Color Over(System.Drawing.Color top, System.Drawing.Color under)
    {
        double a = top.A / 255.0;
        return System.Drawing.Color.FromArgb((int)(top.R * a + under.R * (1 - a)), (int)(top.G * a + under.G * (1 - a)), (int)(top.B * a + under.B * (1 - a)));
    }

    [TestMethod]
    public void ButtonStylesFollowTheRedesign()
    {
        int mode = Settings.ThemeMode;
        try
        {
            foreach (int m in new[] { 1, 2 })
            {
                Settings.ThemeMode = m;
                System.Drawing.Color bg, text, edge;
                FlatButton danger = new FlatButton("Désinstaller", false) { Danger = true };
                danger.Style(out bg, out text, out edge);
                Assert.IsTrue(bg.A < 128, "Danger sobre : fond rouge translucide, pas plein.");
                Assert.IsTrue(edge.A > 0, "Danger sobre : bordure rouge fine.");
                System.Drawing.Color under = Over(bg, Theme.Card);
                double ratio = (Math.Max(Lum(text), Lum(under)) + 0.05) / (Math.Min(Lum(text), Lum(under)) + 0.05);
                Assert.IsTrue(ratio >= 4.5, "Texte « danger » lisible (" + ratio.ToString("0.00") + ":1, thème " + m + ").");

                FlatButton ghost = new FlatButton("Quitter", false) { Ghost = true };
                ghost.Style(out bg, out text, out edge);
                Assert.AreEqual(0, bg.A, "Discret : transparent hors survol.");

                FlatButton secondary = new FlatButton("Aperçu", false);
                secondary.Style(out bg, out text, out edge);
                Assert.AreEqual(Theme.Edge, edge, "Secondaire : bordure fine lumineuse.");
            }
        }
        finally { Settings.ThemeMode = mode; }
    }

    [TestMethod]
    public void IconFontPrefersFluentAndFallsBackToMdl2()
    {
        Assert.AreEqual("Segoe Fluent Icons", Ui.PickGlyphFont(new[] { "Arial", "Segoe MDL2 Assets", "Segoe Fluent Icons" }));
        Assert.AreEqual("Segoe MDL2 Assets", Ui.PickGlyphFont(new[] { "Arial", "Segoe MDL2 Assets" }), "Windows 10 : repli sur MDL2.");
        Assert.AreEqual("Segoe MDL2 Assets", Ui.PickGlyphFont(new string[0]));
    }

    [TestMethod]
    public void SearchChipsAreIconsWithTheirPrefixInTheTooltip()
    {
        BindingFlags priv = BindingFlags.Static | BindingFlags.NonPublic;
        string[] glyphs = (string[])typeof(PanelForm).GetField("ChipGlyphs", priv).GetValue(null);
        CollectionAssert.AreEqual(new[] { Glyphs.Calculator, Glyphs.Help, Glyphs.Terminal }, glyphs);
        string[] prefixes = (string[])typeof(PanelForm).GetField("ChipPrefixes", priv).GetValue(null);
        CollectionAssert.AreEqual(new[] { "=", "?", ">" }, prefixes, "Le clic écrit toujours le préfixe.");
    }
}
