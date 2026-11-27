using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-045 : thèmes prêts, teinte des fonds, accent de Windows, taille de l'interface (D-019)
[TestClass]
public class ThemeCustomizationTests
{
    string previousDir, dir;
    bool saveBlocked, accentFromWindows;
    int palette, zoom, mode, barStyle;
    Color accent;

    [TestInitialize]
    public void Isolate()
    {
        previousDir = Settings.DirOverride;
        dir = Path.Combine(Path.GetTempPath(), "BureauCache.Themes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Settings.DirOverride = dir;
        saveBlocked = Settings.SaveBlocked;
        Settings.SaveBlocked = false;
        palette = Settings.Palette; zoom = Settings.InterfaceZoom; mode = Settings.ThemeMode; barStyle = Settings.TaskbarStyle;
        accent = Settings.Accent; accentFromWindows = Settings.AccentFromWindows;
    }

    [TestCleanup]
    public void Restore()
    {
        Settings.Palette = palette; Settings.InterfaceZoom = zoom; Settings.ThemeMode = mode; Settings.TaskbarStyle = barStyle;
        Settings.Accent = accent; Settings.AccentFromWindows = accentFromWindows;
        Settings.SetWindowsAccentForTests(Color.Empty);
        Settings.DirOverride = previousDir;
        Settings.SaveBlocked = saveBlocked;
        try { Directory.Delete(dir, true); } catch { }
    }

    static double Luminance(Color c)
    {
        Func<double, double> ch = v => { v /= 255.0; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); };
        return 0.2126 * ch(c.R) + 0.7152 * ch(c.G) + 0.0722 * ch(c.B);
    }

    static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    [TestMethod]
    public void ClassicPaletteKeepsTheOriginalColors()
    {
        Settings.Palette = 0; Settings.ThemeMode = 1;
        Assert.AreEqual(Color.FromArgb(24, 24, 28).ToArgb(), Theme.Bg.ToArgb());
        Settings.Palette = 2;
        Assert.AreNotEqual(Color.FromArgb(24, 24, 28).ToArgb(), Theme.Bg.ToArgb(), "Océan : fond teinté.");
        Assert.AreEqual(Color.FromArgb(236, 236, 241).ToArgb(), Theme.Fg.ToArgb(), "Le texte n'est jamais teinté.");
    }

    [TestMethod]
    public void EveryPaletteKeepsReadableText()
    {
        foreach (int m in new[] { 1, 2 })
            for (int p = 0; p < Palettes.All.Length; p++)
            {
                Settings.ThemeMode = m; Settings.Palette = p;
                string where = Palettes.All[p].Fr + (m == 1 ? " sombre" : " clair");
                Assert.IsTrue(Contrast(Theme.Fg, Theme.Bg) >= 7, where + " : texte sur le fond.");
                Assert.IsTrue(Contrast(Theme.FgDim, Theme.Surface) >= 4.5, where + " : texte secondaire sur une surface (AA).");
                Assert.IsTrue(Contrast(Theme.FgDim, Theme.Card) >= 4.5, where + " : texte secondaire sur une carte (AA).");
            }
    }

    [TestMethod]
    public void PresetSetsEverythingAndIsRecognized()
    {
        foreach (Palettes.Preset p in Palettes.Presets)
            Assert.IsTrue(Swatches.Presets.Any(c => c.ToArgb() == p.Accent.ToArgb()), p.Fr + " : accent présent dans la palette des Paramètres.");
        Palettes.ApplyPreset(1);
        Assert.AreEqual(2, Settings.Palette);
        Assert.AreEqual(1, Settings.ThemeMode);
        Assert.AreEqual(1, Settings.TaskbarStyle, "Minuit : barre en verre.");
        Assert.AreEqual(1, Palettes.CurrentPreset());
        Settings.Accent = Color.FromArgb(239, 68, 68);
        Assert.AreEqual(-1, Palettes.CurrentPreset(), "Accent changé à la main : thème personnalisé.");
    }

    [TestMethod]
    public void AccentCanFollowWindows()
    {
        Settings.Accent = Color.FromArgb(59, 130, 246);
        Settings.SetWindowsAccentForTests(Color.FromArgb(200, 30, 90));
        Settings.AccentFromWindows = false;
        Assert.AreEqual(Settings.Accent.ToArgb(), Settings.EffectiveAccent.ToArgb());
        Settings.AccentFromWindows = true;
        Assert.AreEqual(Color.FromArgb(200, 30, 90).ToArgb(), Settings.EffectiveAccent.ToArgb());
        Settings.SetWindowsAccentForTests(Color.Empty);
        Assert.AreEqual(Settings.Accent.ToArgb(), Settings.EffectiveAccent.ToArgb(), "Accent de Windows illisible : celui choisi.");
    }

    [TestMethod]
    public void ThemeChoicesSurviveSaveAndLoad()
    {
        Settings.Palette = 4; Settings.InterfaceZoom = 115; Settings.AccentFromWindows = true;
        Assert.IsTrue(Settings.Save());
        Settings.Palette = 0; Settings.InterfaceZoom = 100; Settings.AccentFromWindows = false;
        Assert.IsTrue(Settings.Load(Settings.FilePath));
        Assert.AreEqual(4, Settings.Palette);
        Assert.AreEqual(115, Settings.InterfaceZoom);
        Assert.IsTrue(Settings.AccentFromWindows);

        File.WriteAllText(Settings.FilePath, File.ReadAllText(Settings.FilePath).Replace("InterfaceZoom=115", "InterfaceZoom=400").Replace("Palette=4", "Palette=99"));
        Settings.InterfaceZoom = 100;
        Assert.IsTrue(Settings.Load(Settings.FilePath));
        Assert.AreEqual(100, Settings.InterfaceZoom, "Taille inconnue : ignorée.");
        Assert.AreEqual(Palettes.All.Length - 1, Settings.Palette, "Palette bornée.");
    }

    [TestMethod]
    public void InterfaceZoomEnlargesFonts()
    {
        float before = Ui.FontZoom;
        try
        {
            Ui.FontZoom = 1f;
            float normal = Ui.Font("Segoe UI", 11.25f).Size;
            Ui.FontZoom = 1.3f;
            Assert.AreEqual(normal * 1.3f, Ui.Font("Segoe UI", 11.25f).Size, 0.01f);
        }
        finally { Ui.FontZoom = before; }
    }

    [TestMethod]
    public void ThemeCardsFitTheirNames()
    {
        foreach (Func<IDisposable> scope in new Func<IDisposable>[] { () => BureauCache.Tests.LanguageScope.French(), () => BureauCache.Tests.LanguageScope.English(), () => BureauCache.Tests.LanguageScope.Spanish() })
            using (scope())
            using (ThemePicker picker = new ThemePicker())
                for (int i = 0; i < Palettes.Presets.Length; i++)
                {
                    Rectangle r = picker.CardBounds(i);
                    Assert.IsTrue(new Rectangle(Point.Empty, picker.Size).Contains(r));
                    Assert.IsTrue(TextRenderer.MeasureText(Palettes.Presets[i].Name, Ui.Font("Segoe UI", 9f)).Width <= r.Width - Ui.S(12), Palettes.Presets[i].Name);
                }
    }
}
