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
    bool center, labels, quick, bell, date, seconds, saveBlocked, line, autoHide, group, middle, wheel;
    int style;

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
        style = Settings.TaskbarStyle; line = Settings.TaskbarLine; autoHide = Settings.TaskbarAutoHide; group = Settings.TaskbarGroup;
        middle = Settings.TaskbarMiddleClose; wheel = Settings.TaskbarWheel;
    }

    [TestCleanup]
    public void Restore()
    {
        Settings.TaskbarSize = size; Settings.TaskbarCenter = center; Settings.TaskbarLabels = labels;
        Settings.TaskbarQuick = quick; Settings.TaskbarBell = bell; Settings.TaskbarDate = date; Settings.TaskbarSeconds = seconds;
        Settings.TaskbarStyle = style; Settings.TaskbarLine = line; Settings.TaskbarAutoHide = autoHide; Settings.TaskbarGroup = group;
        Settings.TaskbarMiddleClose = middle; Settings.TaskbarWheel = wheel;
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

    // ---- Étape 2 : apparence et comportement ----

    [TestMethod]
    public void AppearanceAndBehaviorSurviveSaveAndLoad()
    {
        Settings.TaskbarStyle = 1; Settings.TaskbarLine = false; Settings.TaskbarAutoHide = true;
        Settings.TaskbarGroup = true; Settings.TaskbarMiddleClose = true; Settings.TaskbarWheel = false;
        Assert.IsTrue(Settings.Save());
        Settings.TaskbarStyle = 0; Settings.TaskbarLine = true; Settings.TaskbarAutoHide = false;
        Settings.TaskbarGroup = false; Settings.TaskbarMiddleClose = false; Settings.TaskbarWheel = true;
        Assert.IsTrue(Settings.Load(Settings.FilePath));
        Assert.AreEqual(1, Settings.TaskbarStyle);
        Assert.IsTrue(Settings.TaskbarAutoHide && Settings.TaskbarGroup && Settings.TaskbarMiddleClose);
        Assert.IsFalse(Settings.TaskbarLine || Settings.TaskbarWheel);
    }

    [TestMethod]
    public void GroupingMakesOneButtonPerApp()
    {
        var a1 = new TaskWindow { Handle = new IntPtr(1), AppKey = @"exe:C:\Apps\Notes.exe", Title = "Notes 1" };
        var a2 = new TaskWindow { Handle = new IntPtr(2), AppKey = @"exe:C:\Apps\Notes.exe", Title = "Notes 2" };
        var b1 = new TaskWindow { Handle = new IntPtr(3), AppKey = @"exe:C:\Apps\Other.exe", Title = "Other" };
        var pins = new[] { @"exe:C:\Apps\Notes.exe>Notes" };
        var windows = new System.Collections.Generic.List<TaskWindow> { a1, b1, a2 };

        var separate = TaskbarModel.Build(pins, windows, false);
        Assert.AreEqual(3, separate.Count, "Sans regroupement : l'épingle avec sa première fenêtre, puis une fenêtre par bouton.");

        var grouped = TaskbarModel.Build(pins, windows, true);
        Assert.AreEqual(2, grouped.Count);
        Assert.IsTrue(grouped[0].Pinned);
        Assert.AreEqual(2, grouped[0].Count, "Les deux fenêtres de Notes dans l'épingle.");
        Assert.AreEqual(1, grouped[1].Count);
        Assert.IsNull(grouped[1].Group, "Une seule fenêtre : pas de groupe.");

        Assert.AreSame(a1, BarForm.NextInGroup(grouped[0], IntPtr.Zero), "Rien au premier plan : la première fenêtre.");
        Assert.AreSame(a2, BarForm.NextInGroup(grouped[0], a1.Handle), "Un clic passe à la suivante.");
        Assert.AreSame(a1, BarForm.NextInGroup(grouped[0], a2.Handle), "Puis revient à la première.");
    }

    [TestMethod]
    public void UngroupedWindowsOfUnpinnedAppsAreGroupedToo()
    {
        var w1 = new TaskWindow { Handle = new IntPtr(1), AppKey = @"exe:C:\Apps\Edge.exe" };
        var w2 = new TaskWindow { Handle = new IntPtr(2), AppKey = @"exe:C:\Apps\Edge.exe" };
        var w3 = new TaskWindow { Handle = new IntPtr(3) };   // application inconnue : jamais regroupée
        var w4 = new TaskWindow { Handle = new IntPtr(4) };
        var grouped = TaskbarModel.Build(new string[0], new System.Collections.Generic.List<TaskWindow> { w1, w3, w2, w4 }, true);
        Assert.AreEqual(3, grouped.Count);
        Assert.AreEqual(2, grouped[0].Count);
    }

    [TestMethod]
    public void WallpaperPlacementFollowsWindowsStyle()
    {
        Rectangle screen = new Rectangle(0, 0, 1920, 1080);
        RectangleF fill = BarForm.WallpaperBounds(new Size(1000, 1000), screen, 0);
        Assert.AreEqual(1920f, fill.Width, 0.5f, "Remplir : l'image couvre toute la largeur.");
        Assert.IsTrue(fill.Top < 0 && fill.Bottom > 1080, "Et déborde en haut et en bas.");
        RectangleF fit = BarForm.WallpaperBounds(new Size(1000, 1000), screen, 1);
        Assert.AreEqual(1080f, fit.Height, 0.5f);
        Assert.IsTrue(fit.Left > 0, "Ajuster : bandes sur les côtés.");
        Assert.AreEqual((RectangleF)screen, BarForm.WallpaperBounds(new Size(10, 10), screen, 2), "Étirer.");
        RectangleF center = BarForm.WallpaperBounds(new Size(100, 50), screen, 3);
        Assert.AreEqual(new RectangleF(910, 515, 100, 50), center);
    }

    [TestMethod]
    public void GlassIsMadeFromTheWallpaperUnderTheBar()
    {
        string file = Path.Combine(dir, "fond.png");
        using (Bitmap wallpaper = new Bitmap(200, 100))
        {
            using (Graphics g = Graphics.FromImage(wallpaper))
            {
                g.Clear(Color.Red);
                g.FillRectangle(Brushes.Blue, 0, 90, 200, 10);   // bas de l'image : bleu
            }
            wallpaper.Save(file);
        }
        Rectangle screen = new Rectangle(0, 0, 200, 100), area = new Rectangle(0, 90, 200, 10);
        using (Bitmap glass = BarForm.MakeGlass(file, 2, screen, area, Color.FromArgb(0, Color.Black)))
        {
            Assert.IsNotNull(glass);
            Assert.AreEqual(area.Size, glass.Size);
            Color middle = glass.GetPixel(100, 5);
            Assert.IsTrue(middle.B > middle.R, "La barre montre la partie du fond d'écran qu'elle cache (le bas bleu).");
        }
        Assert.IsNull(BarForm.MakeGlass(Path.Combine(dir, "absent.png"), 0, screen, area, Color.Black));
        File.WriteAllText(Path.Combine(dir, "abime.png"), "pas une image");
        Assert.IsNull(BarForm.MakeGlass(Path.Combine(dir, "abime.png"), 0, screen, area, Color.Black), "Fichier abîmé : pas d'erreur, fond uni.");
    }

    [TestMethod]
    public void AutoHideReservesOnlyAThinEdge()
    {
        Settings.TaskbarAutoHide = false;
        Assert.AreEqual(BarForm.BarHeight, BarForm.ReservedHeight);
        Settings.TaskbarAutoHide = true;
        Assert.AreEqual(BarForm.Edge, BarForm.ReservedHeight, "Les fenêtres agrandies descendent jusqu'au liseré.");
        Assert.IsTrue(BarForm.Edge >= 2);
    }

    [TestMethod]
    public void TintedBackgroundUsesTheAccent()
    {
        Assert.AreNotEqual(Theme.Bg.ToArgb(), BarForm.TintedBackground.ToArgb());
    }
}
