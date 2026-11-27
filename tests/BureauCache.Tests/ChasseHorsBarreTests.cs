using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-036 : revue de Codex hors de la barre (CDX-20 à 29, QA-68). Chaque test a son dossier de réglages isolé.
[TestClass]
public class ChasseHorsBarreTests
{
    string previousDir, dir, snapshot;

    [TestInitialize]
    public void Isolate()
    {
        previousDir = Settings.DirOverride;
        dir = Path.Combine(Path.GetTempPath(), "BureauCache.Chasse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        snapshot = Path.Combine(dir, "avant-test.ini");
        File.WriteAllLines(snapshot, Settings.Serialize());
        Settings.DirOverride = Path.Combine(dir, "Reglages");
        Directory.CreateDirectory(Settings.DirOverride);
    }

    [TestCleanup]
    public void Restore()
    {
        Settings.CancelPendingSave();
        Settings.ProfileUnreadable = false;
        Settings.Load(snapshot);
        Settings.ArchiveLog = new List<string>();
        Settings.DirOverride = previousDir;
        Weather.Reset();
        try { Directory.Delete(dir, true); } catch { }
    }

    // CDX-21 ---------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CDX21_OnlyTheStandardTempFolderIsCleaned()
    {
        string local = @"C:\Users\x\AppData\Local";
        Assert.AreEqual(@"C:\Users\x\AppData\Local\Temp\", Cleaner.TemporaryTarget(local, @"C:\Users\x\AppData\Local\Temp\"));
        Assert.AreEqual(@"C:\Users\x\AppData\Local\Temp\2\", Cleaner.TemporaryTarget(local, @"C:\Users\x\AppData\Local\Temp\2"), "Sous-dossier de session (bureau à distance).");
        Assert.IsNull(Cleaner.TemporaryTarget(local, @"C:\Users\x\AppData\Local\Google\Chrome\User Data\"), "Profil de navigateur : jamais.");
        Assert.IsNull(Cleaner.TemporaryTarget(local, @"C:\Users\x\AppData\Local\Temporary Internet Files\"), "Nom qui commence comme Temp : refusé.");
        Assert.IsNull(Cleaner.TemporaryTarget(local, @"D:\Temp\"));
        Assert.IsNull(Cleaner.TemporaryTarget(local, ""));
    }

    // CDX-22 ---------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CDX22_RefusedLaunchRestoresAndStartsThePreviousVersionOnce()
    {
        List<string> calls = new List<string>();
        int starts = 0;
        Program.RelaunchUpdate(@"C:\app\BureauCache.exe",
            p => { starts++; calls.Add("start"); if (starts == 1) throw new System.ComponentModel.Win32Exception(193); },
            p => calls.Add("rollback"), m => calls.Add("warn"));
        CollectionAssert.AreEqual(new[] { "start", "rollback", "warn", "start" }, calls);
    }

    [TestMethod]
    public void CDX22_SuccessfulLaunchDoesNothingElse()
    {
        List<string> calls = new List<string>();
        Program.RelaunchUpdate(@"C:\app\BureauCache.exe", p => calls.Add("start"), p => calls.Add("rollback"), m => calls.Add("warn"));
        CollectionAssert.AreEqual(new[] { "start" }, calls);
    }

    [TestMethod]
    public void CDX22_FailedRollbackWarnsWithoutStartingAgain()
    {
        List<string> calls = new List<string>();
        Program.RelaunchUpdate(@"C:\app\BureauCache.exe", p => { calls.Add("start"); throw new System.ComponentModel.Win32Exception(193); },
            p => { calls.Add("rollback"); throw new IOException(); }, m => calls.Add("warn"));
        CollectionAssert.AreEqual(new[] { "start", "rollback", "warn" }, calls);
    }

    // CDX-23 ---------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CDX23_LockedSettingsAreNeverOverwrittenByDefaults()
    {
        File.WriteAllLines(Settings.FilePath, Settings.Serialize().Select(l => l.StartsWith("PanelWidth=") ? "PanelWidth=444" : l));
        byte[] before = File.ReadAllBytes(Settings.FilePath);
        using (new FileStream(Settings.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Settings.LoadWithRetries(Settings.FilePath, 2, 1);
            Assert.IsTrue(Settings.ProfileUnreadable, "Fichier verrouillé : signalé illisible.");
        }
        Settings.PanelWidth = 300;   // l'application continue avec d'autres valeurs…
        Assert.IsFalse(Settings.Save(), "… mais n'écrit rien tant que les réglages n'ont pas été lus.");
        CollectionAssert.AreEqual(before, File.ReadAllBytes(Settings.FilePath), "Les vrais réglages sont intacts.");
    }

    [TestMethod]
    public void CDX23_DamagedSettingsAreNotTreatedAsLocked()
    {
        File.WriteAllText(Settings.FilePath, "pas un fichier de réglages");
        Settings.LoadWithRetries(Settings.FilePath, 2, 1);
        Assert.IsFalse(Settings.ProfileUnreadable, "Un fichier abîmé n'est pas verrouillé : on peut le réécrire (sauvegardes D-007).");
    }

    // CDX-25 ---------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CDX25_ImportKeepsLocalProtectionsWhenTidyUpIsOn()
    {
        string contrat = @"C:\Users\x\Desktop\Contrat.pdf", budget = @"C:\Users\x\Desktop\Budget.xlsx";
        long recent = DateTime.Now.Ticks;
        Settings.ArchiveOn = true;
        Settings.Favorites = new List<string> { contrat };
        Settings.Usage[budget] = new long[] { 3, recent };

        List<string> lines = Settings.Serialize().Where(l => !l.StartsWith("Favorites=") && !l.StartsWith("Usage=")).ToList();
        lines.Add("Favorites=");
        lines.Add("Usage=");
        string foreign = Path.Combine(dir, "recu.ini");
        File.WriteAllLines(foreign, lines, new UTF8Encoding(false));

        List<string> kept = new List<string>();
        Assert.IsTrue(SettingsBackup.ImportFrom(foreign, kept));
        Assert.IsTrue(Settings.FavIndex(contrat) >= 0, "Le favori local protège encore Contrat.pdf.");
        Assert.IsTrue(Settings.Usage.ContainsKey(budget) && Settings.Usage[budget][1] == recent, "La date d'usage locale protège encore Budget.xlsx.");
        Assert.IsTrue(kept.Any(k => k.Contains(L.T("favoris", "favorites", "favoritos"))), "Le bilan de l'import le dit.");
    }

    // CDX-27 ---------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CDX27_PinnedSettingDrivesTheScreenReservation()
    {
        Assert.AreEqual("unpin", PanelForm.PinAction(false, true));
        Assert.AreEqual("pin", PanelForm.PinAction(true, false));
        Assert.AreEqual("", PanelForm.PinAction(true, true));
        Assert.AreEqual("", PanelForm.PinAction(false, false));
    }

    // CDX-28 ---------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CDX28_ImportForAnotherPlaceDropsTheCachedForecast()
    {
        Settings.WeatherOn = true;
        Settings.WeatherLat = 50.85; Settings.WeatherLon = 4.35;    // Bruxelles
        Weather.Current = new WeatherReport();
        List<string> lines = Settings.Serialize().Where(l => !l.StartsWith("WeatherLat=") && !l.StartsWith("WeatherLon=")).ToList();
        lines.Add("WeatherLat=40.42");
        lines.Add("WeatherLon=-3.70");                               // Madrid
        string foreign = Path.Combine(dir, "madrid.ini");
        File.WriteAllLines(foreign, lines, new UTF8Encoding(false));

        Assert.IsTrue(SettingsBackup.RestoreFrom(foreign));
        Assert.IsNull(Weather.Current, "La météo de Bruxelles n'est plus affichée sous le nom de Madrid.");
    }

    [TestMethod]
    public void CDX28_SamePlaceKeepsTheCachedForecast()
    {
        Settings.WeatherOn = true;
        Settings.WeatherLat = 50.85; Settings.WeatherLon = 4.35;
        WeatherReport report = new WeatherReport();
        Weather.Current = report;
        string same = Path.Combine(dir, "meme.ini");
        File.WriteAllLines(same, Settings.Serialize(), new UTF8Encoding(false));
        Assert.IsTrue(SettingsBackup.RestoreFrom(same));
        Assert.AreSame(report, Weather.Current, "Même lieu : pas de nouvelle requête inutile.");
    }

    // CDX-29 ---------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CDX29_OldVersionOfAModifiedFileIconIsReleasedAfterReload()
    {
        string file = Path.Combine(dir, "image.txt");
        File.WriteAllText(file, "v1");
        File.SetLastWriteTimeUtc(file, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Bitmap v1 = IconCache.ForFile(file, 16);
        Assert.IsNotNull(v1);
        int width = v1.Width;
        File.SetLastWriteTimeUtc(file, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        Bitmap v2 = IconCache.ForFile(file, 16);
        Assert.AreNotSame(v1, v2);
        Assert.AreEqual(width, v1.Width, "L'ancienne image reste utilisable tant que l'ancienne liste est affichée.");
        IconCache.ReleaseRetired();
        Assert.ThrowsException<ArgumentException>(() => { int w = v1.Width; }, "Libérée après la publication des nouvelles listes.");
        Assert.AreEqual(width, v2.Width, "La nouvelle reste.");
    }
}
