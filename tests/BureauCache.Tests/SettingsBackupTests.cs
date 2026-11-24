using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// D-007 : sauvegardes automatiques des réglages. Chaque test a son propre dossier de réglages.
[TestClass]
public class SettingsBackupTests
{
    string previousDir, dir;
    List<string> favorites;
    bool saveBlocked;

    [TestInitialize]
    public void Isolate()
    {
        previousDir = Settings.DirOverride;
        dir = Path.Combine(Path.GetTempPath(), "BureauCache.Backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Settings.DirOverride = dir;
        favorites = Settings.Favorites;
        Settings.Favorites = new List<string>();
        saveBlocked = Settings.SaveBlocked;
        Settings.SaveBlocked = false;
    }

    [TestCleanup]
    public void Restore()
    {
        Settings.DirOverride = previousDir;
        Settings.Favorites = favorites;
        Settings.SaveBlocked = saveBlocked;
        try { Directory.Delete(dir, true); } catch { }
    }

    static string[] Backups() { return Directory.Exists(SettingsBackup.Folder) ? Directory.GetFiles(SettingsBackup.Folder) : new string[0]; }

    [TestMethod]
    public void FirstSaveOfTheDayCopiesThePreviousFileOnce()
    {
        Settings.Favorites.Add(@"C:\Bureau\avant.txt");
        Assert.IsTrue(Settings.Save());
        Assert.AreEqual(0, Backups().Length, "Aucun fichier précédent : rien à copier.");

        Settings.Favorites.Clear();
        Settings.Favorites.Add(@"C:\Bureau\apres.txt");
        Assert.IsTrue(Settings.Save());
        string[] copies = Backups();
        Assert.AreEqual(1, copies.Length);
        StringAssert.Contains(File.ReadAllText(copies[0]), @"avant.txt", "La copie garde l'état d'avant la réécriture.");

        Settings.Favorites.Add(@"C:\Bureau\encore.txt");
        Assert.IsTrue(Settings.Save());
        Assert.AreEqual(1, Backups().Length, "Une seule copie par jour.");
        StringAssert.Contains(File.ReadAllText(Backups()[0]), @"avant.txt", "La copie du jour n'est pas remplacée par les écritures suivantes.");
    }

    [TestMethod]
    public void InvalidPreviousFileIsNotCopied()
    {
        File.WriteAllText(Settings.FilePath, "pas un fichier de réglages");
        Assert.IsTrue(Settings.Save());
        Assert.AreEqual(0, Backups().Length);
    }

    [TestMethod]
    public void OnlyTheSevenMostRecentDailyCopiesAreKept()
    {
        Directory.CreateDirectory(SettingsBackup.Folder);
        for (int i = 1; i <= 10; i++)
            File.WriteAllText(Path.Combine(SettingsBackup.Folder, "settings-" + DateTime.Today.AddDays(-i).ToString("yyyy-MM-dd") + ".ini"), "HideIcons=1");
        SettingsBackup.Prune();
        List<SettingsBackup.Entry> kept = SettingsBackup.List();
        Assert.AreEqual(SettingsBackup.Kept, kept.Count);
        Assert.AreEqual(DateTime.Today.AddDays(-1), kept[0].Date, "La plus récente d'abord.");
        Assert.AreEqual(DateTime.Today.AddDays(-7), kept[kept.Count - 1].Date);
    }

    [TestMethod]
    public void RestoreBringsBackTheBackupAndKeepsTheCurrentStateRestorable()
    {
        Settings.Favorites.Add(@"C:\Bureau\sauvegarde.txt");
        Assert.IsTrue(Settings.Save());
        Directory.CreateDirectory(SettingsBackup.Folder);
        string yesterday = Path.Combine(SettingsBackup.Folder, "settings-" + DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd") + ".ini");
        File.Copy(Settings.FilePath, yesterday);

        Settings.Favorites.Clear();
        Settings.Favorites.Add(@"C:\Bureau\actuel.txt");
        Assert.IsTrue(Settings.Save());

        SettingsBackup.Entry entry = SettingsBackup.List().First(e => e.Path == yesterday);
        Assert.AreEqual(1, entry.Favorites);
        Assert.IsTrue(SettingsBackup.Restore(entry));
        CollectionAssert.AreEqual(new[] { @"C:\Bureau\sauvegarde.txt" }, Settings.Favorites);
        StringAssert.Contains(File.ReadAllText(Settings.FilePath), "sauvegarde.txt", "La restauration est enregistrée.");

        SettingsBackup.Entry before = SettingsBackup.List().First();
        Assert.IsTrue(before.BeforeRestore, "L'état d'avant la restauration est proposé en premier.");
        Assert.IsTrue(SettingsBackup.Restore(before), "La restauration elle-même est réversible.");
        CollectionAssert.AreEqual(new[] { @"C:\Bureau\actuel.txt" }, Settings.Favorites);
    }

    [TestMethod]
    public void InvalidBackupsAreNotListedOrRestored()
    {
        Directory.CreateDirectory(SettingsBackup.Folder);
        string broken = Path.Combine(SettingsBackup.Folder, "settings-" + DateTime.Today.AddDays(-2).ToString("yyyy-MM-dd") + ".ini");
        File.WriteAllText(broken, "abîmé");
        Assert.AreEqual(0, SettingsBackup.List().Count);
        Assert.IsFalse(SettingsBackup.Restore(new SettingsBackup.Entry { Path = broken }));
    }

    [TestMethod]
    public void DeleteAllRemovesTheBackupFolder()
    {
        Directory.CreateDirectory(SettingsBackup.Folder);
        File.WriteAllText(Path.Combine(SettingsBackup.Folder, "settings-2026-01-01.ini"), "HideIcons=1");
        SettingsBackup.DeleteAll();
        Assert.IsFalse(Directory.Exists(SettingsBackup.Folder));
    }
}
