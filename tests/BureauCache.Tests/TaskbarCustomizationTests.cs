using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BureauCache.Tests;

// T-043 : personnalisation de la barre BureauCache (D-019)
[TestClass]
public class TaskbarCustomizationTests
{
    string previousDir, dir;
    int size;
    bool center, labels, quick, bell, date, seconds, saveBlocked;

    [TestInitialize]
    public void Isolate()
    {
        previousDir = Settings.DirOverride;
        dir = Path.Combine(Path.GetTempPath(), "BureauCache.Barre-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Settings.DirOverride = dir;
        saveBlocked = Settings.SaveBlocked;
        Settings.SaveBlocked = false;
        size = Settings.TaskbarSize; center = Settings.TaskbarCenter; labels = Settings.TaskbarLabels;
        quick = Settings.TaskbarQuick; bell = Settings.TaskbarBell; date = Settings.TaskbarDate; seconds = Settings.TaskbarSeconds;
    }

    [TestCleanup]
    public void Restore()
    {
        Settings.TaskbarSize = size; Settings.TaskbarCenter = center; Settings.TaskbarLabels = labels;
        Settings.TaskbarQuick = quick; Settings.TaskbarBell = bell; Settings.TaskbarDate = date; Settings.TaskbarSeconds = seconds;
        Settings.DirOverride = previousDir;
        Settings.SaveBlocked = saveBlocked;
        try { Directory.Delete(dir, true); } catch { }
    }

    static void RunSta(Action body)
    {
        Exception error = null;
        Thread t = new Thread(() => { try { body(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    static Rectangle[] Zones(BarForm bar)
    {
        typeof(BarForm).GetMethod("Layout2", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bar, null);
        return (Rectangle[])typeof(BarForm).GetField("zones", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bar);
    }

    [TestMethod]
    public void TaskbarChoicesSurviveSaveAndLoad()
    {
        Settings.TaskbarSize = 2; Settings.TaskbarCenter = true; Settings.TaskbarLabels = true;
        Settings.TaskbarQuick = false; Settings.TaskbarBell = false; Settings.TaskbarDate = false; Settings.TaskbarSeconds = true;
        Assert.IsTrue(Settings.Save());
        Settings.TaskbarSize = 1; Settings.TaskbarCenter = false; Settings.TaskbarLabels = false;
        Settings.TaskbarQuick = true; Settings.TaskbarBell = true; Settings.TaskbarDate = true; Settings.TaskbarSeconds = false;
        Assert.IsTrue(Settings.Load(Settings.FilePath));
        Assert.AreEqual(2, Settings.TaskbarSize);
        Assert.IsTrue(Settings.TaskbarCenter && Settings.TaskbarLabels && Settings.TaskbarSeconds);
        Assert.IsFalse(Settings.TaskbarQuick || Settings.TaskbarBell || Settings.TaskbarDate);
    }

    [TestMethod]
    public void SizeChangesTheBarHeight()
    {
        Settings.TaskbarSize = 0; int small = BarForm.BarHeight;
        Settings.TaskbarSize = 1; int normal = BarForm.BarHeight;
        Settings.TaskbarSize = 2; int large = BarForm.BarHeight;
        Assert.AreEqual(Ui.S(56), normal, "Taille normale : celle d'avant la personnalisation.");
        Assert.IsTrue(small < normal && normal < large);
    }

    [TestMethod]
    public void ClockShowsSecondsAndHidesTheDateOnRequest()
    {
        DateTime time = new DateTime(2026, 11, 27, 16, 54, 7);
        string t, d;
        using (LanguageScope.French())
        {
            Settings.TaskbarSeconds = false; Settings.TaskbarDate = true;
            BarForm.ClockText(time, out t, out d);
            Assert.AreEqual("16:54", t);
            Assert.AreEqual("27/11/2026", d);
            Settings.TaskbarSeconds = true; Settings.TaskbarDate = false;
            BarForm.ClockText(time, out t, out d);
            Assert.AreEqual("16:54:07", t);
            Assert.IsNull(d);
        }
    }

    [TestMethod]
    public void HiddenItemsLeaveNoZoneAndCenteringMovesStart()
    {
        RunSta(() =>
        {
            using (BarForm bar = new BarForm())
            {
                bar.ClientSize = new Size(1600, BarForm.BarHeight);
                Settings.TaskbarQuick = true; Settings.TaskbarBell = true; Settings.TaskbarCenter = false;
                Rectangle[] z = Zones(bar);
                Assert.IsFalse(z[1].IsEmpty || z[3].IsEmpty);
                int startLeft = z[0].Left;

                Settings.TaskbarQuick = false; Settings.TaskbarBell = false;
                z = Zones(bar);
                Assert.IsTrue(z[1].IsEmpty && z[3].IsEmpty, "Éléments masqués : aucune zone cliquable.");
                Assert.IsFalse(z[2].IsEmpty, "L'horloge reste toujours.");

                Settings.TaskbarCenter = true;
                z = Zones(bar);
                Assert.IsTrue(z[0].Left > startLeft + 400, "Centrées : le bouton Démarrer part vers le milieu.");
                Assert.IsTrue(z[0].Right < z[2].Left);
            }
        });
    }
}
