using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-032 : barre BureauCache, étape 1 (D-013). Le vrai masquage de la barre Windows n'est jamais exécuté en test :
// on vérifie le fichier d'état qui garantit la restauration, le bac à sable et le réglage.
[TestClass]
public class TaskbarTests
{
    string previousDir, dir, snapshot;

    [TestInitialize]
    public void Isolate()
    {
        previousDir = Settings.DirOverride;
        dir = Path.Combine(Path.GetTempPath(), "BureauCache.Taskbar-" + Guid.NewGuid().ToString("N"));
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
        Settings.Load(snapshot);
        Settings.DirOverride = previousDir;
        try { Directory.Delete(dir, true); } catch { }
    }

    [TestMethod]
    public void StateFileRoundTripsEveryKnownState()
    {
        for (int s = 0; s <= 3; s++)
            Assert.AreEqual(s, WindowsBar.ParseState(WindowsBar.FormatState(s)));
    }

    [TestMethod]
    public void DamagedOrForeignStateFileIsRejected()
    {
        Assert.AreEqual(-1, WindowsBar.ParseState(null));
        Assert.AreEqual(-1, WindowsBar.ParseState(""));
        Assert.AreEqual(-1, WindowsBar.ParseState("etat=1"), "Sans en-tête, ce n'est pas notre fichier.");
        Assert.AreEqual(-1, WindowsBar.ParseState("Autre chose\r\netat=1\r\n"));
        Assert.AreEqual(-1, WindowsBar.ParseState("BureauCache barre 1\r\netat=9\r\n"), "Bits inconnus refusés.");
        Assert.AreEqual(-1, WindowsBar.ParseState("BureauCache barre 1\r\netat=-1\r\n"));
        Assert.AreEqual(-1, WindowsBar.ParseState("BureauCache barre 1\r\netat=abc\r\n"));
        Assert.AreEqual(-1, WindowsBar.ParseState("BureauCache barre 1\r\netat=1" + new string(' ', 300)), "Fichier trop long.");
        Assert.AreEqual(2, WindowsBar.ParseState("BureauCache barre 1\netat=2"), "Fins de ligne Unix acceptées.");
    }

    [TestMethod]
    public void HidingTheRealTaskbarIsBlockedInTests()
    {
        Assert.IsTrue(Sandbox.Active);
        Assert.ThrowsException<InvalidOperationException>(() => WindowsBar.Hide());
        Assert.IsFalse(File.Exists(WindowsBar.StatePath), "Rien n'est écrit quand le masquage est bloqué.");
        Assert.IsFalse(WindowsBar.Hidden);
    }

    [TestMethod]
    public void RestoreDoesNothingInTestsAndKeepsTheStateFile()
    {
        File.WriteAllText(WindowsBar.StatePath, WindowsBar.FormatState(2), new UTF8Encoding(false));
        WindowsBar.Restore();
        Assert.IsTrue(File.Exists(WindowsBar.StatePath), "En test, la vraie barre n'est pas touchée et le filet reste.");
    }

    [TestMethod]
    public void StateFileLivesInTheSettingsFolder()
    {
        Assert.AreEqual(Path.Combine(Settings.Dir, "barre.etat"), WindowsBar.StatePath);
    }

    [TestMethod]
    public void TaskbarSettingIsOffByDefaultAndRoundTrips()
    {
        // Load ne remet pas les valeurs absentes à zéro : la valeur par défaut se lit dans la source, comme pour ClipboardOn
        string root = AppDomain.CurrentDomain.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "build.cmd"))) root = Path.GetDirectoryName(root);
        Assert.IsNotNull(root, "Racine du dépôt introuvable.");
        StringAssert.Contains(File.ReadAllText(Path.Combine(root, "src", "Core", "Settings.cs")), "public static bool TaskbarOn = false;",
            "Option désactivée par défaut (D-013).");

        Settings.TaskbarOn = true;
        string saved = Path.Combine(dir, "aller-retour.ini");
        File.WriteAllLines(saved, Settings.Serialize());
        Assert.IsTrue(File.ReadAllLines(saved).Contains("TaskbarOn=1"));
        Settings.TaskbarOn = false;
        Settings.Load(saved);
        Assert.IsTrue(Settings.TaskbarOn);
    }

    [TestMethod]
    public void ImportDoesNotTurnOnTheTaskbarReplacement()
    {
        Settings.TaskbarOn = false;
        List<string> lines = Settings.Serialize().Where(l => !l.StartsWith("TaskbarOn=")).ToList();
        lines.Add("TaskbarOn=1");
        string foreign = Path.Combine(dir, "recu.ini");
        File.WriteAllLines(foreign, lines, new UTF8Encoding(false));

        List<string> kept = new List<string>();
        Assert.IsTrue(SettingsBackup.ImportFrom(foreign, kept));
        Assert.IsFalse(Settings.TaskbarOn, "Un fichier importé ne cache pas la barre Windows de ce PC.");
        Assert.IsTrue(kept.Any(k => k.Contains(L.T("barre des tâches", "taskbar", "barra de tareas"))), "Le choix gardé est nommé.");
    }
}
