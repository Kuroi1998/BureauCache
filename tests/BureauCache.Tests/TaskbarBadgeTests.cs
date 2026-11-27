using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-041 : badges et progressions des applications (D-016). La vraie barre d'Explorer n'est jamais touchée en test.
[TestClass]
public class TaskbarBadgeTests
{
    [DllImport("shlwapi.dll", EntryPoint = "#7", ExactSpelling = true)]
    static extern IntPtr SHAllocShared(byte[] data, uint bytes, uint processId);

    [DllImport("shlwapi.dll", EntryPoint = "#10", ExactSpelling = true)]
    static extern bool SHFreeShared(IntPtr handle, uint processId);

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
    public void ReadsUnicodeBadgeDescriptionFromSharedMemory()
    {
        const string description = "1 notification reçue";
        byte[] data = Encoding.Unicode.GetBytes(description + "\0");
        using (var process = System.Diagnostics.Process.GetCurrentProcess())
        {
            uint pid = (uint)process.Id;
            IntPtr handle = SHAllocShared(data, (uint)data.Length, pid);
            Assert.AreNotEqual(IntPtr.Zero, handle);
            try { Assert.AreEqual(description, Native.SharedText(handle, pid)); }
            finally { SHFreeShared(handle, pid); }
        }
    }

    [TestMethod]
    public void HiddenWindowKeepsItsBadgeUntilItCloses()
    {
        Exception failure = null;
        Thread thread = new Thread(() =>
        {
            try
            {
                using (var bar = new BarForm())
                using (var window = new Form())
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var badges = (Dictionary<IntPtr, BarForm.Badge>)typeof(BarForm).GetField("badges", flags).GetValue(bar);
                    var prune = typeof(BarForm).GetMethod("ForgetClosedWindowBadges", flags);
                    IntPtr hwnd = window.Handle;
                    Assert.IsFalse(window.Visible);
                    using (var icon = new Bitmap(16, 16))
                    {
                        badges.Add(hwnd, new BarForm.Badge { Icon = icon });
                        prune.Invoke(bar, null);
                        Assert.AreSame(icon, badges[hwnd].Icon);
                        window.Dispose();
                        prune.Invoke(bar, null);
                        Assert.IsFalse(badges.ContainsKey(hwnd));
                    }
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void DiscordAttentionCountsAlertsUntilClearedAndLetsTheNativeBadgeWin()
    {
        var window = new TaskWindow { AppKey = "aumid:com.squirrel.Discord.Discord" };
        Assert.IsNull(BarForm.DiscordAlertText(window, false));
        Assert.IsTrue(window.RegisterDiscordAlert());
        Assert.AreEqual("1", BarForm.DiscordAlertText(window, false));
        Assert.IsTrue(window.RegisterDiscordAlert());
        Assert.AreEqual("2", BarForm.DiscordAlertText(window, false));
        Assert.IsNull(BarForm.DiscordAlertText(window, true));
        for (int i = 0; i < 200; i++) window.RegisterDiscordAlert();
        Assert.AreEqual("99+", BarForm.DiscordAlertText(window, false));
        window.ClearDiscordAlerts();
        Assert.IsNull(BarForm.DiscordAlertText(window, false));
        Assert.IsNull(BarForm.DiscordAlertText(null, false));
        Assert.IsTrue(new TaskWindow { AppKey = @"exe:C:\Discord\app-1.0.9269\DISCORD.EXE" }.RegisterDiscordAlert());
        Assert.IsFalse(new TaskWindow { AppKey = @"exe:C:\Example\NotDiscord.exe" }.RegisterDiscordAlert());
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
