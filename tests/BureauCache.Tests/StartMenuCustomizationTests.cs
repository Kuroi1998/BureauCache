using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-044 : menu Démarrer à la carte (D-019)
[TestClass]
public class StartMenuCustomizationTests
{
    string previousDir, dir;
    bool saveBlocked, search, account, bureauCache, windows, session, power;
    List<string> hidden, shortcuts;

    [TestInitialize]
    public void Isolate()
    {
        previousDir = Settings.DirOverride;
        dir = Path.Combine(Path.GetTempPath(), "BureauCache.Demarrer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Settings.DirOverride = dir;
        saveBlocked = Settings.SaveBlocked;
        Settings.SaveBlocked = false;
        search = Settings.StartSearch; account = Settings.StartAccount; bureauCache = Settings.StartBureauCache;
        windows = Settings.StartWindows; session = Settings.StartSession; power = Settings.StartPower;
        hidden = Settings.StartHidden; shortcuts = Settings.StartShortcuts;
        Settings.StartHidden = new List<string>();
        Settings.StartShortcuts = new List<string>();
    }

    [TestCleanup]
    public void Restore()
    {
        Settings.StartSearch = search; Settings.StartAccount = account; Settings.StartBureauCache = bureauCache;
        Settings.StartWindows = windows; Settings.StartSession = session; Settings.StartPower = power;
        Settings.StartHidden = hidden; Settings.StartShortcuts = shortcuts;
        Settings.DirOverride = previousDir;
        Settings.SaveBlocked = saveBlocked;
        try { Directory.Delete(dir, true); } catch { }
    }

    static string[] Tiles(StartMenuForm menu) { return menu.Controls.OfType<StartTile>().Select(t => t.Text).ToArray(); }

    [TestMethod]
    public void ShortcutTargetsAreChecked()
    {
        Assert.IsTrue(StartShortcuts.Valid(StartShortcuts.App, @"C:\Program Files\App\app.exe"));
        Assert.IsTrue(StartShortcuts.Valid(StartShortcuts.App, @"C:\Users\Moi\Bureau\Jeu.lnk"));
        Assert.IsFalse(StartShortcuts.Valid(StartShortcuts.App, @"app.exe"), "Chemin relatif refusé.");
        Assert.IsFalse(StartShortcuts.Valid(StartShortcuts.App, @"C:\Docs\note.txt"), "Pas une application.");
        Assert.IsFalse(StartShortcuts.Valid(StartShortcuts.App, @"C:\Apps\..\Windows\cmd.exe"), "Chemin détourné refusé.");
        Assert.IsTrue(StartShortcuts.Valid(StartShortcuts.Folder, @"D:\Photos"));
        Assert.IsTrue(StartShortcuts.Valid(StartShortcuts.Site, "https://www.exemple.fr/page?x=1"));
        Assert.IsFalse(StartShortcuts.Valid(StartShortcuts.Site, "javascript:alert(1)"));
        Assert.IsFalse(StartShortcuts.Valid(StartShortcuts.Site, "file:///C:/Windows/System32/cmd.exe"));
        Assert.IsFalse(StartShortcuts.Valid(StartShortcuts.Site, "https://exemple.fr/a|b"), "Séparateur du fichier de réglages refusé.");
        Assert.IsFalse(StartShortcuts.Valid('x', @"C:\Apps\app.exe"));
    }

    [TestMethod]
    public void ShortcutsAreStoredCleanedAndLimited()
    {
        Assert.IsTrue(StartShortcuts.Add(StartShortcuts.Site, "https://exemple.fr", "Mon > site | préféré"));
        StartShortcuts.Entry e;
        Assert.IsTrue(StartShortcuts.Parse(Settings.StartShortcuts[0], out e));
        Assert.AreEqual("Mon  site  préféré", e.Name, "Séparateurs retirés du nom.");
        Assert.IsFalse(StartShortcuts.Add(StartShortcuts.Site, "HTTPS://EXEMPLE.FR".ToLowerInvariant(), "Doublon"), "Même cible : pas de doublon.");
        for (int i = 1; i < StartShortcuts.Max; i++) Assert.IsTrue(StartShortcuts.Add(StartShortcuts.Site, "https://site" + i + ".fr", null));
        Assert.IsFalse(StartShortcuts.Add(StartShortcuts.Site, "https://encore.fr", null), "12 au plus.");
        Assert.AreEqual(StartShortcuts.Max, StartShortcuts.All().Count);
        Assert.AreEqual("site1.fr", StartShortcuts.All()[1].Name, "Nom proposé : l'adresse du site.");
        Settings.StartShortcuts.Add("u>Piège>javascript:alert(1)");
        Assert.AreEqual(StartShortcuts.Max, StartShortcuts.All().Count, "Entrée douteuse du fichier : ignorée.");
    }

    [TestMethod]
    public void SiteAddressIsCompleted()
    {
        Assert.AreEqual("https://exemple.fr", SiteShortcutDialog.Normalize(" exemple.fr "));
        Assert.AreEqual("http://intranet.local", SiteShortcutDialog.Normalize("http://intranet.local"));
        Assert.IsNull(SiteShortcutDialog.Normalize("ftp://exemple.fr"));
        Assert.IsNull(SiteShortcutDialog.Normalize(""));
    }

    [TestMethod]
    public void StartChoicesSurviveSaveAndLoad()
    {
        Settings.StartSearch = false; Settings.StartPower = false;
        Settings.StartHidden.Add("win.control");
        StartShortcuts.Add(StartShortcuts.Folder, dir, "Projet");
        Assert.IsTrue(Settings.Save());
        Settings.StartSearch = true; Settings.StartPower = true;
        Settings.StartHidden = new List<string>(); Settings.StartShortcuts = new List<string>();
        Assert.IsTrue(Settings.Load(Settings.FilePath));
        Assert.IsFalse(Settings.StartSearch || Settings.StartPower);
        CollectionAssert.AreEqual(new[] { "win.control" }, Settings.StartHidden);
        Assert.AreEqual("Projet", StartShortcuts.All()[0].Name);
    }

    [TestMethod]
    public void MenuFollowsHiddenSectionsTilesAndShortcuts()
    {
        using (BureauCache.Tests.LanguageScope.French())
        {
            using (StartMenuForm menu = new StartMenuForm())
                Assert.AreEqual(15, Tiles(menu).Length, "Par défaut : le menu complet.");

            Settings.StartHidden.Add("win.taskmgr");
            Settings.StartPower = false;
            StartShortcuts.Add(StartShortcuts.Folder, dir, "Projet");
            using (StartMenuForm menu = new StartMenuForm())
            {
                string[] tiles = Tiles(menu);
                Assert.IsFalse(tiles.Contains("Gestionnaire des tâches"), "Tuile masquée.");
                Assert.IsFalse(tiles.Contains("Arrêter"), "Section Alimentation masquée.");
                Assert.IsTrue(tiles.Contains("Projet"), "Raccourci personnel affiché.");
                Assert.AreEqual(15 - 1 - 3 + 1, tiles.Length);
                foreach (StartTile t in menu.Controls.OfType<StartTile>())
                    Assert.IsTrue(menu.ClientRectangle.Contains(t.Bounds), t.Text);
            }

            Settings.StartBureauCache = Settings.StartWindows = Settings.StartSession = false;
            Settings.StartShortcuts.Clear();
            using (StartMenuForm menu = new StartMenuForm())
                CollectionAssert.AreEqual(new[] { "Personnaliser le menu" }, Tiles(menu), "Tout masqué : jamais un menu vide.");
        }
    }

    [TestMethod]
    public void MenuWithoutSearchAndAccountIsShorter()
    {
        int full, bare;
        using (StartMenuForm menu = new StartMenuForm()) full = menu.ClientSize.Height;
        Settings.StartSearch = Settings.StartAccount = false;
        using (StartMenuForm menu = new StartMenuForm())
        {
            bare = menu.ClientSize.Height;
            Assert.IsFalse(menu.Controls.OfType<System.Windows.Forms.TextBox>().Any(), "Pas de champ de recherche.");
        }
        Assert.IsTrue(bare < full - 80);
    }
}
