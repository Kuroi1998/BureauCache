using System;
using System.Drawing;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-041 : badges et progressions des applications (D-016). La vraie barre d'Explorer n'est jamais touchée en test.
[TestClass]
public class TaskbarBadgeTests
{
    string previousDir, dir;

    [TestInitialize]
    public void Isolate()
    {
        previousDir = Settings.DirOverride;
        dir = Path.Combine(Path.GetTempPath(), "BureauCache.Badges-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Settings.DirOverride = dir;
    }

    [TestCleanup]
    public void Restore()
    {
        Settings.DirOverride = previousDir;
        try { Directory.Delete(dir, true); } catch { }
    }

    [TestMethod]
    public void StateFileRoundTrips()
    {
        long tray, original, ours;
        Assert.IsTrue(TaskbandRoute.Parse(TaskbandRoute.Format(0x10A2C, 0x20B3D, 0x30C4E), out tray, out original, out ours));
        Assert.AreEqual(0x10A2C, tray);
        Assert.AreEqual(0x20B3D, original);
        Assert.AreEqual(0x30C4E, ours);
        Assert.IsTrue(TaskbandRoute.Parse(TaskbandRoute.Format(1, 0, 2), out tray, out original, out ours), "Pas de valeur d'origine : accepté.");
    }

    [TestMethod]
    public void DamagedStateFileIsRejected()
    {
        long a, b, c;
        Assert.IsFalse(TaskbandRoute.Parse(null, out a, out b, out c));
        Assert.IsFalse(TaskbandRoute.Parse("BureauCache badges 1\r\nZZ\r\n0\r\n1", out a, out b, out c));
        Assert.IsFalse(TaskbandRoute.Parse("Autre\r\n1\r\n0\r\n2", out a, out b, out c));
        Assert.IsFalse(TaskbandRoute.Parse("BureauCache badges 1\r\n0\r\n0\r\n2", out a, out b, out c), "Fenêtre d'Explorer nulle.");
        Assert.IsFalse(TaskbandRoute.Parse("BureauCache badges 1\r\n1\r\n0\r\n0", out a, out b, out c), "Notre fenêtre nulle.");
    }

    [TestMethod]
    public void RoutingIsBlockedInTestsAndRestoreTouchesNothing()
    {
        Assert.IsTrue(Sandbox.Active);
        Assert.ThrowsException<InvalidOperationException>(() => TaskbandRoute.Install(new IntPtr(1)));
        File.WriteAllText(TaskbandRoute.StatePath, TaskbandRoute.Format(1, 2, 3), new UTF8Encoding(false));
        Assert.IsTrue(TaskbandRoute.Restore());
        Assert.IsTrue(File.Exists(TaskbandRoute.StatePath), "En test, ni la vraie barre ni le fichier ne sont touchés.");
        Assert.IsFalse(TaskbandRoute.Installed);
    }

    [TestMethod]
    public void ProgressBarFollowsTheApplicationState()
    {
        int full = 100;
        Assert.AreEqual(0, BarForm.ProgressWidth(BarForm.ProgressNone, 30000, full), "Pas de progression : rien.");
        Assert.AreEqual(50, BarForm.ProgressWidth(BarForm.ProgressNormal, BarForm.ProgressScale / 2, full));
        Assert.AreEqual(100, BarForm.ProgressWidth(BarForm.ProgressNormal, BarForm.ProgressScale, full));
        Assert.AreEqual(100, BarForm.ProgressWidth(BarForm.ProgressNormal, 999999, full), "Valeur hors échelle : bornée.");
        Assert.AreEqual(0, BarForm.ProgressWidth(BarForm.ProgressNormal, -5, full));
        Assert.AreEqual(33, BarForm.ProgressWidth(BarForm.ProgressIndeterminate, 0, full), "Indéterminée : un tiers.");
        Assert.AreEqual(25, BarForm.ProgressWidth(BarForm.ProgressPaused, BarForm.ProgressScale / 4, full));
    }

    [TestMethod]
    public void ProgressColorsMatchWindows()
    {
        Assert.AreEqual(Color.FromArgb(6, 176, 37), BarForm.ProgressColor(BarForm.ProgressNormal), "Vert.");
        Assert.AreEqual(Color.FromArgb(252, 202, 0), BarForm.ProgressColor(BarForm.ProgressPaused), "Jaune.");
        Assert.AreEqual(Color.FromArgb(232, 17, 35), BarForm.ProgressColor(BarForm.ProgressError), "Rouge.");
    }
}
