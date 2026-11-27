using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-029 : durcissement issu de l'audit T-028 (AUD-01, AUD-02, SEC-04, SEC-12). Chaque test a son dossier de réglages.
[TestClass]
public class HardeningTests
{
    string previousDir, dir, snapshot;

    [TestInitialize]
    public void Isolate()
    {
        previousDir = Settings.DirOverride;
        dir = Path.Combine(Path.GetTempPath(), "BureauCache.Hardening-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        snapshot = Path.Combine(dir, "avant-test.ini");
        File.WriteAllLines(snapshot, Settings.Serialize());
        Settings.DirOverride = Path.Combine(dir, "Reglages");
    }

    [TestCleanup]
    public void Restore()
    {
        Settings.CancelPendingSave();
        Settings.Load(snapshot);
        Settings.ArchiveLog = new List<string>();
        Settings.DirOverride = previousDir;
        try { Directory.Delete(dir, true); } catch { }
    }

    // Fichier de réglages « reçu » : il veut changer le dépôt, couper les mises à jour, activer le presse-papiers et
    // le rangement, et apporte un journal d'annulation. Il change aussi une préférence ordinaire (largeur du panneau).
    string ForeignFile(int width)
    {
        List<string> lines = Settings.Serialize().Where(l =>
            !l.StartsWith("UpdateRepo=") && !l.StartsWith("AutoUpdate=") && !l.StartsWith("ClipboardOn=") &&
            !l.StartsWith("ArchiveOn=") && !l.StartsWith("ArchiveLog=") && !l.StartsWith("PanelWidth=")).ToList();
        lines.Add("UpdateRepo=quelquun/faux-bureaucache");
        lines.Add("AutoUpdate=0");
        lines.Add("ClipboardOn=1");
        lines.Add("ArchiveOn=1");
        lines.Add(@"ArchiveLog=C:\Users\x\Desktop\a.docx>C:\Users\x\Documents\a.docx");
        lines.Add("PanelWidth=" + width);
        string p = Path.Combine(dir, "recu.ini");
        File.WriteAllLines(p, lines, new UTF8Encoding(false));
        return p;
    }

    void SetLocal()
    {
        Settings.UpdateRepo = "";
        Settings.AutoUpdate = true;
        Settings.ClipboardOn = false;
        Settings.ArchiveOn = false;
        Settings.ArchiveLog = new List<string>();
        Settings.PanelWidth = 300;
    }

    [TestMethod]
    public void ImportKeepsSensitiveLocalChoicesAndTellsWhich()
    {
        SetLocal();
        List<string> kept = new List<string>();
        Assert.IsTrue(SettingsBackup.ImportFrom(ForeignFile(420), kept));

        Assert.AreEqual(420, Settings.PanelWidth, "Les préférences ordinaires sont importées.");
        Assert.AreEqual("", Settings.UpdateRepo, "AUD-01 : le dépôt des mises à jour reste celui de ce PC.");
        Assert.IsTrue(Updater.IsOfficial(Updater.RepoName));
        Assert.IsTrue(Settings.AutoUpdate, "SEC-04 : un import ne coupe pas les mises à jour automatiques.");
        Assert.IsFalse(Settings.ClipboardOn, "SEC-04 : un import n'active pas l'historique du presse-papiers.");
        Assert.IsFalse(Settings.ArchiveOn, "SEC-04 : un import n'active pas le rangement automatique.");
        Assert.AreEqual(0, Settings.ArchiveLog.Count, "AUD-02 : le journal d'annulation importé est ignoré.");
        Assert.AreEqual(5, kept.Count, "Chaque choix gardé est nommé à l'utilisateur.");

        // Ce qui est enregistré sur le disque est bien la version protégée
        string saved = File.ReadAllText(Settings.FilePath);
        StringAssert.Contains(saved, "UpdateRepo=\r\n");
        Assert.IsFalse(saved.Contains("faux-bureaucache"));
    }

    [TestMethod]
    public void ImportOfAHarmlessFileReportsNothingKept()
    {
        SetLocal();
        File.WriteAllLines(Path.Combine(dir, "propre.ini"), Settings.Serialize().Select(l => l.StartsWith("PanelWidth=") ? "PanelWidth=380" : l), new UTF8Encoding(false));
        List<string> kept = new List<string>();
        Assert.IsTrue(SettingsBackup.ImportFrom(Path.Combine(dir, "propre.ini"), kept));
        Assert.AreEqual(380, Settings.PanelWidth);
        Assert.AreEqual(0, kept.Count);
    }

    [TestMethod]
    public void RestoringOwnBackupStillRestoresEverything()
    {
        SetLocal();
        // Une sauvegarde de ce PC (Paramètres → Restaurer) reste une restauration complète
        Assert.IsTrue(SettingsBackup.RestoreFrom(ForeignFile(400)));
        Assert.IsTrue(Settings.ClipboardOn);
        Assert.AreEqual(1, Settings.ArchiveLog.Count);
    }

    sealed class SlowStream : Stream
    {
        public override int Read(byte[] buffer, int offset, int count) { Thread.Sleep(30); buffer[offset] = 1; return 1; }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override long Seek(long o, SeekOrigin s) { throw new NotSupportedException(); }
        public override void SetLength(long v) { throw new NotSupportedException(); }
        public override void Write(byte[] b, int o, int c) { throw new NotSupportedException(); }
    }

    [TestMethod]
    public void TrickleDownloadStopsAtTheTotalDeadline()
    {
        Stopwatch clock = Stopwatch.StartNew();
        try
        {
            Updater.CopyLimited(new SlowStream(), new MemoryStream(), 1024 * 1024, clock, 200);
            Assert.Fail("SEC-12 : un serveur au compte-gouttes doit être abandonné.");
        }
        catch (IOException) { }
        Assert.IsTrue(clock.ElapsedMilliseconds < 2000, "Arrêt peu après l'échéance.");
    }

    [TestMethod]
    public void UninstallUsesTheSystemCommandProcessorWithoutAutoRun()
    {
        // Le lancement réel est bloqué en test : on vérifie la commande construite dans la source compilée
        string src = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Core", "Installation.cs"));
        StringAssert.Contains(src, "Path.Combine(Environment.SystemDirectory, \"cmd.exe\")");
        StringAssert.Contains(src, "\"/d /c for");
    }

    static string RepoRoot()
    {
        string d = AppDomain.CurrentDomain.BaseDirectory;
        while (d != null && !File.Exists(Path.Combine(d, "build.cmd"))) d = Path.GetDirectoryName(d);
        Assert.IsNotNull(d);
        return d;
    }
}
