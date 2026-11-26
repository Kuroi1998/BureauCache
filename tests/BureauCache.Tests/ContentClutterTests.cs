using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// D-010 : recherche dans le contenu (construction de la requête, sans toucher au vrai index) et encombrement
// du bureau (sur le bureau du bac à sable).
[TestClass]
public class ContentClutterTests
{
    string desk;

    [TestInitialize]
    public void SetUp()
    {
        desk = Paths.Desktop;
        StringAssert.StartsWith(desk, Path.GetTempPath(), "Le bureau doit être celui du bac à sable.");
        Clean();
    }

    [TestCleanup]
    public void TearDown() { Clean(); Settings.CancelPendingSave(); }

    void Clean()
    {
        foreach (string f in Directory.GetFiles(desk)) { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); }
        foreach (string d in Directory.GetDirectories(desk)) Directory.Delete(d, true);
    }

    string NewFile(string name, int bytes, int ageDays)
    {
        string p = Path.Combine(desk, name);
        Directory.CreateDirectory(Path.GetDirectoryName(p));
        File.WriteAllBytes(p, new byte[bytes]);
        DateTime t = DateTime.UtcNow.AddDays(-ageDays);
        File.SetCreationTimeUtc(p, t);
        File.SetLastWriteTimeUtc(p, t);
        return p;
    }

    // ---------- Recherche dans le contenu ----------

    [TestMethod]
    public void SanitizeKeepsWordsAndDropsQuerySyntax()
    {
        Assert.AreEqual("facture 2026", ContentSearch.Sanitize("  facture   2026 "));
        Assert.AreEqual("a OR b c d", ContentSearch.Sanitize("a' OR \"b\" (c) * d"), "Guillemets, apostrophes, parenthèses et astérisques disparaissent.");
        // Chaque mot est cité : « OR » est cherché comme texte, pas comme opérateur
        StringAssert.Contains(ContentSearch.BuildQuery("abc' OR \"x", new[] { @"C:\x" }, 5), "'\"abc*\" AND \"OR*\" AND \"x*\"'");
        Assert.AreEqual("réunion-équipe", ContentSearch.Sanitize("réunion-équipe"));
        Assert.AreEqual(ContentSearch.MaxTermLength, ContentSearch.Sanitize(new string('x', 500)).Length);
        Assert.AreEqual("", ContentSearch.Sanitize(null));
    }

    [TestMethod]
    public void ContentSearchOnlyAppliesToRealWords()
    {
        Assert.IsTrue(ContentSearch.Applies("facture"));
        Assert.IsFalse(ContentSearch.Applies("fa"), "Trop court.");
        Assert.IsFalse(ContentSearch.Applies("'\"*"), "Rien d'utilisable après nettoyage.");
        Assert.IsFalse(ContentSearch.Applies("=12*7"));
        Assert.IsFalse(ContentSearch.Applies("?météo"));
        Assert.IsFalse(ContentSearch.Applies(">veille"));
    }

    [TestMethod]
    public void QueryIsScopedToTheDesktopsAndEscapesPaths()
    {
        string sql = ContentSearch.BuildQuery("fact rapport", new[] { @"C:\Users\O'Brien\Desktop\", @"C:\Users\Public\Desktop" }, 20);
        StringAssert.Contains(sql, "SCOPE='file:C:/Users/O''Brien/Desktop'");
        StringAssert.Contains(sql, "SCOPE='file:C:/Users/Public/Desktop'");
        StringAssert.Contains(sql, "CONTAINS(*, '\"fact*\" AND \"rapport*\"')");
        StringAssert.StartsWith(sql, "SELECT TOP 20 System.ItemUrl FROM SystemIndex");
        Assert.IsNull(ContentSearch.BuildQuery("ab", new[] { @"C:\x" }, 20));
        Assert.IsNull(ContentSearch.BuildQuery("facture", new string[0], 20), "Sans bureau, pas de requête sur tout le disque.");
        StringAssert.StartsWith(ContentSearch.BuildQuery("facture", new[] { @"C:\x" }, 100000), "SELECT TOP 200 ");
    }

    [TestMethod]
    public void IndexUrlsBecomeRealPathsNearTheDesktop()
    {
        Assert.AreEqual(@"C:\Users\a\Desktop\mon rapport.txt", ContentSearch.PathFromUrl("file:C:/Users/a/Desktop/mon%20rapport.txt"));
        Assert.IsNull(ContentSearch.PathFromUrl("mapi://{S-1-5}/message"));
        Assert.IsNull(ContentSearch.PathFromUrl(null));
        string[] desks = { @"C:\Users\a\Desktop" };
        Assert.IsTrue(ContentSearch.NearDesktop(@"C:\Users\a\Desktop\x.txt", desks));
        Assert.IsTrue(ContentSearch.NearDesktop(@"C:\Users\a\Desktop\Projet\x.txt", desks));
        Assert.IsFalse(ContentSearch.NearDesktop(@"C:\Users\a\Desktop\Tor\Docs\Licences\x.txt", desks), "Trop profond : bruit des logiciels portables.");
        Assert.IsFalse(ContentSearch.NearDesktop(@"C:\Users\a\DesktopBis\x.txt", desks));
    }

    [TestMethod]
    public void RealIndexIsNeverQueriedDuringTests()
    {
        try { ContentSearch.Find("facture", 5); Assert.Fail("Le bac à sable doit bloquer l'index Windows."); }
        catch (InvalidOperationException e) { StringAssert.Contains(e.Message, "Effet de bord bloqué"); }
    }

    [TestMethod]
    public void SearchContentSettingIsSavedAndLoaded()
    {
        bool saved = Settings.SearchContent;
        string ini = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ini");
        try
        {
            Settings.SearchContent = false;
            File.WriteAllLines(ini, Settings.Serialize());
            Settings.SearchContent = true;
            Assert.IsTrue(Settings.Load(ini));
            Assert.IsFalse(Settings.SearchContent);
        }
        finally { Settings.SearchContent = saved; File.Delete(ini); }
    }

    // ---------- Encombrement ----------

    static HashSet<string> NoFavorites() { return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
    static Dictionary<string, long[]> NoUsage() { return new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase); }

    [TestMethod]
    public void ClutterCountsSizesAndUnusedItems()
    {
        NewFile("recent.txt", 100, 2);
        NewFile("mois.txt", 2000, 45);
        NewFile("vieux.bin", 50000, 200);
        NewFile(@"Projet\a.txt", 300, 120);
        NewFile(@"Projet\b.txt", 700, 120);
        Directory.SetLastWriteTimeUtc(Path.Combine(desk, "Projet"), DateTime.UtcNow.AddDays(-120));
        Directory.SetCreationTimeUtc(Path.Combine(desk, "Projet"), DateTime.UtcNow.AddDays(-120));

        Clutter.Report r = Clutter.Measure(desk, NoFavorites(), NoUsage(), Path.Combine(desk, "Archives"), DateTime.UtcNow);
        Assert.AreEqual(4, r.Count);
        Assert.AreEqual(100 + 2000 + 50000 + 300 + 700, r.Bytes);
        Assert.AreEqual(3, r.Unused30, "mois.txt, vieux.bin et Projet.");
        Assert.AreEqual(2, r.Unused90, "vieux.bin et Projet.");
        Assert.IsFalse(r.Partial);
        CollectionAssert.AreEqual(new[] { "vieux.bin", "mois.txt", "Projet", "recent.txt" }, r.Biggest.Select(k => Path.GetFileName(k.Key)).ToArray());
    }

    [TestMethod]
    public void ClutterIgnoresShortcutsHiddenFilesArchiveAndUsedOrFavoriteItems()
    {
        NewFile("raccourci.lnk", 10, 300);
        string hidden = NewFile("cache.dat", 10, 300);
        File.SetAttributes(hidden, FileAttributes.Hidden);
        NewFile(@"Archives\rangé.txt", 10, 300);
        string fav = NewFile("favori.txt", 10, 300);
        string used = NewFile("ouvert.txt", 10, 300);
        HashSet<string> favorites = NoFavorites(); favorites.Add(fav);
        Dictionary<string, long[]> usage = NoUsage(); usage[used] = new long[] { 3, DateTime.Now.AddDays(-5).Ticks };

        Clutter.Report r = Clutter.Measure(desk, favorites, usage, Path.Combine(desk, "Archives"), DateTime.UtcNow);
        Assert.AreEqual(2, r.Count, "Seuls favori.txt et ouvert.txt comptent.");
        Assert.AreEqual(0, r.Unused30, "Un favori ou un fichier ouvert récemment n'est pas inutilisé.");
    }

    [TestMethod]
    public void ClutterMarksHugeFoldersAsPartial()
    {
        for (int i = 0; i < 5; i++) NewFile(@"Gros\f" + i + ".txt", 10, 1);
        bool partial;
        long size = Clutter.SizeOf(Path.Combine(desk, "Gros"), 3, out partial);
        Assert.IsTrue(partial);
        Assert.IsTrue(size <= 30);
        Assert.AreEqual(50, Clutter.SizeOf(Path.Combine(desk, "Gros"), 100, out partial));
        Assert.IsFalse(partial);
    }

    [TestMethod]
    public void MissingDesktopGivesAnEmptyReport()
    {
        Clutter.Report r = Clutter.Measure(Path.Combine(desk, "absent"), NoFavorites(), NoUsage(), "", DateTime.UtcNow);
        Assert.AreEqual(0, r.Count);
        Assert.AreEqual(0, r.Biggest.Count);
    }
}
