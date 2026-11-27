using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// D-009 : règles de rangement. Bureau et dossiers de destination isolés dans le bac à sable des tests.
[TestClass]
public class ArchiveRulesTests
{
    List<ArchiveRule> rules;
    List<string> log, favorites;
    Dictionary<string, long[]> usage;
    int archiveDays;
    string desk;

    [TestInitialize]
    public void SetUp()
    {
        rules = Settings.ArchiveRules; log = Settings.ArchiveLog; favorites = Settings.Favorites; usage = Settings.Usage; archiveDays = Settings.ArchiveDays;
        Settings.ArchiveLog = new List<string>();
        Settings.Favorites = new List<string>();
        Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
        Settings.ArchiveDays = 60;
        desk = Paths.Desktop;
        StringAssert.StartsWith(desk, Path.GetTempPath(), "Le bureau doit être celui du bac à sable.");
        Clean();
    }

    [TestCleanup]
    public void TearDown()
    {
        Clean();
        Settings.CancelPendingSave();
        Settings.ArchiveRules = rules; Settings.ArchiveLog = log; Settings.Favorites = favorites; Settings.Usage = usage; Settings.ArchiveDays = archiveDays;
    }

    void Clean()
    {
        foreach (string f in Directory.GetFiles(desk)) System.IO.File.Delete(f);
        foreach (string d in Directory.GetDirectories(desk)) Directory.Delete(d, true);
        string folders = Path.Combine(Path.GetDirectoryName(desk.TrimEnd('\\')), "Dossiers");
        if (Directory.Exists(folders)) Directory.Delete(folders, true);
    }

    string NewFile(string name, int ageDays)
    {
        string p = Path.Combine(desk, name);
        System.IO.File.WriteAllText(p, "test");
        DateTime t = DateTime.UtcNow.AddDays(-ageDays);
        System.IO.File.SetCreationTimeUtc(p, t);
        System.IO.File.SetLastWriteTimeUtc(p, t);
        return p;
    }

    static ArchiveRule Screenshots(int days)
    {
        return new ArchiveRule { Name = "Captures", Filter = RuleFilter.NameContains, Pattern = "Capture", Days = days, Target = RuleTarget.Pictures, Sub = "Captures" };
    }

    [TestMethod]
    public void RuleRoundTripsAndInvalidRulesAreRejected()
    {
        ArchiveRule r = new ArchiveRule { Name = "Images", Filter = RuleFilter.Extensions, Pattern = "png .JPG", Days = 7, Target = RuleTarget.Pictures, Sub = "" };
        ArchiveRule back = ArchiveRule.Parse(r.Serialize());
        Assert.IsNotNull(back);
        Assert.AreEqual(r.Serialize(), back.Serialize());
        Assert.IsNull(ArchiveRule.Parse("Nom>0>png>7>9>"), "Destination inconnue.");
        Assert.IsNull(ArchiveRule.Parse("Nom>1>>7>1>"), "Filtre vide.");
        Assert.IsNotNull(new ArchiveRule { Name = "x", Filter = RuleFilter.Extensions, Pattern = "png", Sub = "..\\Windows" }.Validate(), "Pas de chemin dans le sous-dossier.");
    }

    [TestMethod]
    public void FiltersMatchExtensionsAndNameWords()
    {
        var ext = new ArchiveRule { Filter = RuleFilter.Extensions, Pattern = "png, .jpg" };
        Assert.IsTrue(ext.Matches(@"C:\x\photo.PNG"));
        Assert.IsTrue(ext.Matches(@"C:\x\photo.jpg"));
        Assert.IsFalse(ext.Matches(@"C:\x\photo.jpeg"));
        Assert.IsFalse(ext.Matches(@"C:\x\png"));
        Assert.IsTrue(Screenshots(7).Matches(@"C:\x\Capture d'écran 2026.png"));
        Assert.IsFalse(Screenshots(7).Matches(@"C:\x\rapport.docx"));
    }

    [TestMethod]
    public void RuleMovesMatchingFilesToItsDestinationAndUndoBringsThemBack()
    {
        Settings.ArchiveRules = new List<ArchiveRule> { Screenshots(7) };
        string shot = NewFile("Capture 1.png", 10), young = NewFile("Capture 2.png", 2), old = NewFile("rapport.docx", 90), recent = NewFile("notes.txt", 5);
        int n = Archiver.RunNow();
        string dest = Path.Combine(ArchiveRules.TargetFolder(RuleTarget.Pictures), "Captures");
        Assert.AreEqual(2, n);
        Assert.IsTrue(System.IO.File.Exists(Path.Combine(dest, "Capture 1.png")), "La règle range la capture ancienne.");
        Assert.IsTrue(System.IO.File.Exists(young), "Trop récente pour la règle : elle reste.");
        Assert.IsTrue(System.IO.File.Exists(Path.Combine(Archiver.Folder, "rapport.docx")), "Les autres fichiers suivent l'ancienneté.");
        Assert.IsTrue(System.IO.File.Exists(recent));

        Assert.AreEqual(2, Archiver.Undo(), "L'annulation remet aussi en place ce que la règle a déplacé.");
        Assert.IsTrue(System.IO.File.Exists(shot) && System.IO.File.Exists(old));
    }

    [TestMethod]
    public void MatchingRuleDecidesAloneAndFavoritesNeverMove()
    {
        // Règle à 90 jours : une capture de 70 jours ne part pas, même si l'ancienneté générale (60 j) le permettrait
        Settings.ArchiveRules = new List<ArchiveRule> { Screenshots(90) };
        string shot = NewFile("Capture ancienne.png", 70);
        string fav = NewFile("Capture favorite.png", 200);
        Settings.Favorites.Add(fav);
        Assert.AreEqual(0, Archiver.RunNow());
        Assert.IsTrue(System.IO.File.Exists(shot) && System.IO.File.Exists(fav));
    }

    [TestMethod]
    public void RulesAreSavedAndLoadedWithTheSettings()
    {
        Settings.ArchiveRules = new List<ArchiveRule> { Screenshots(7) };
        string ini = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ini");
        try
        {
            System.IO.File.WriteAllLines(ini, Settings.Serialize());
            Settings.ArchiveRules = new List<ArchiveRule>();
            Assert.IsTrue(Settings.Load(ini));
            Assert.AreEqual(1, Settings.ArchiveRules.Count);
            Assert.AreEqual(Screenshots(7).Serialize(), Settings.ArchiveRules[0].Serialize());
        }
        finally { System.IO.File.Delete(ini); }
    }
}
