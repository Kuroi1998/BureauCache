using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// D-014 : applications épinglées et ordre des boutons de la barre. Aucun lancement réel (bac à sable).
[TestClass]
public class TaskbarPinsTests
{
    const string Notepad = @"exe:C:\Windows\notepad.exe";
    const string Calc = "aumid:Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";

    sealed class ClickBar : BarForm
    {
        public void PressAt(Point p) { OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0)); }
        public void ReleaseAt(Point p) { OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0)); }
    }

    [TestMethod]
    public void PreviewGapConnectsOnlyTheHoveredButtonAndItsThumbnail()
    {
        Rectangle button = new Rectangle(100, 700, 50, 40);
        Rectangle preview = new Rectangle(120, 500, 220, 192);
        Rectangle gap = BarForm.PreviewGap(button, preview);
        Assert.IsTrue(gap.Contains(125, 695));
        Assert.IsFalse(gap.Contains(110, 695));
        Assert.IsFalse(gap.Contains(155, 695));
        Assert.IsTrue(BarForm.PreviewGap(button, new Rectangle(200, 500, 220, 192)).IsEmpty);
    }

    [TestMethod]
    public void ReleasingAPinOverStartDoesNotOpenTheMenu()
    {
        List<string> previous = Settings.TaskbarPins;
        try
        {
            Settings.TaskbarPins = new List<string> { Calc + ">Calc" };
            using (ClickBar bar = new ClickBar())
            using (Bitmap frame = new Bitmap(bar.Width, bar.Height))
            {
                bar.RebuildButtons();
                bar.DrawToBitmap(frame, bar.ClientRectangle);
                Rectangle start = bar.RectangleToClient(bar.StartButtonBounds);
                Point center = new Point(start.Left + start.Width / 2, start.Top + start.Height / 2);
                int opens = 0;
                bar.StartClicked += () => opens++;
                bar.PressAt(new Point(start.Right + Ui.S(14), center.Y));
                bar.ReleaseAt(center);
                Assert.AreEqual(0, opens);
                bar.PressAt(center);
                bar.ReleaseAt(center);
                Assert.AreEqual(1, opens);
            }
        }
        finally { Settings.TaskbarPins = previous; }
    }

    static TaskWindow W(int h, string key, string title = "Fenêtre")
    {
        return new TaskWindow { Handle = new IntPtr(h), AppKey = key, Title = title };
    }

    [TestMethod]
    public void PinsComeFirstWithTheirWindowsThenOtherWindows()
    {
        List<string> pins = new List<string> { Calc + ">Calculatrice", Notepad + ">Bloc-notes" };
        List<TaskWindow> windows = new List<TaskWindow> { W(1, "exe:C:\\Autre\\a.exe"), W(2, Notepad), W(3, Notepad), W(4, null) };
        List<BarButton> b = TaskbarModel.Build(pins, windows);

        Assert.AreEqual(5, b.Count);
        Assert.IsTrue(b[0].Pinned && b[0].Key == Calc && b[0].Window == null, "Épingle fermée : pas de fenêtre.");
        Assert.IsTrue(b[1].Pinned && b[1].Window.Handle == new IntPtr(2), "Épingle ouverte : sa première fenêtre.");
        Assert.IsTrue(!b[2].Pinned && b[2].Window.Handle == new IntPtr(3), "Autre fenêtre de l'application, juste après.");
        Assert.AreEqual(new IntPtr(1), b[3].Window.Handle);
        Assert.AreEqual(new IntPtr(4), b[4].Window.Handle, "Fenêtre sans clé : affichée quand même.");
    }

    [TestMethod]
    public void DuplicateAndDamagedPinsAreIgnored()
    {
        List<string> pins = new List<string> { Notepad + ">A", Notepad.ToUpperInvariant() + ">B", "", "n'importe quoi", "exe:relatif\\a.exe",
            @"exe:\\serveur\partage\a.exe", @"exe:C:\a.txt", "aumid:", "aumid:a b", "exe:C:\\a.exe>x\u0007" };
        List<BarButton> b = TaskbarModel.Build(pins, new List<TaskWindow>());
        Assert.AreEqual(1, b.Count, "Seule la première épingle valide reste : " + string.Join(", ", b.Select(x => x.Key)));
        Assert.AreEqual("A", b[0].Name);
    }

    [TestMethod]
    public void OnlyFullyQualifiedLocalExecutablePinsAreAccepted()
    {
        string key, name;
        foreach (string path in new[] { @"C:outil.exe", @"\outil.exe", "/outil.exe", "//serveur/partage/a.exe", @"C:\a*.exe" })
            Assert.IsFalse(TaskbarModel.ParsePin("exe:" + path + ">Outil", out key, out name), path);
        Assert.IsTrue(TaskbarModel.ParsePin(@"EXE:C:\Mes outils\outil.exe>Outil", out key, out name));
        Assert.AreEqual(@"exe:C:\Mes outils\outil.exe", key);
    }

    [TestMethod]
    public void PinningRejectsKeysThatWouldSplitIntoTwoSettingsEntries()
    {
        string[] original = { Notepad + ">Bloc-notes" };
        CollectionAssert.AreEqual(original, TaskbarModel.WithPin(original, "aumid:test|aumid:other", "Test"));
        CollectionAssert.AreEqual(original, TaskbarModel.WithPin(original, "aumid:test>other", "Test"));
    }

    [TestMethod]
    public void PinFormatRoundTripsAndStripsSeparators()
    {
        string entry = TaskbarModel.FormatPin(Calc, "Calc>ul|atrice\n");
        string key, name;
        Assert.IsTrue(TaskbarModel.ParsePin(entry, out key, out name));
        Assert.AreEqual(Calc, key);
        Assert.AreEqual("Calculatrice", name);
    }

    [TestMethod]
    public void PinWithoutNameGetsADefaultName()
    {
        string key, name;
        Assert.IsTrue(TaskbarModel.ParsePin(@"exe:C:\Programmes\Outil\outil.exe", out key, out name));
        Assert.AreEqual("outil", name, "Exe absent : nom du fichier.");
        Assert.AreEqual("Google Chrome", TaskbarModel.DefaultName("aumid:Chrome", "Une page - Google Chrome"));
    }

    [TestMethod]
    public void PinAndUnpinKeepOthersInOrder()
    {
        List<string> pins = new List<string> { Calc + ">Calculatrice" };
        pins = TaskbarModel.WithPin(pins, Notepad, "Bloc-notes");
        CollectionAssert.AreEqual(new[] { Calc + ">Calculatrice", Notepad + ">Bloc-notes" }, pins);
        Assert.IsTrue(TaskbarModel.IsPinned(pins, Notepad.ToUpperInvariant()), "Clés comparées sans la casse.");
        pins = TaskbarModel.WithPin(pins, Notepad, "Notepad");
        Assert.AreEqual(2, pins.Count, "Épingler deux fois ne double pas l'épingle.");
        pins = TaskbarModel.Without(pins, Calc);
        CollectionAssert.AreEqual(new[] { Notepad + ">Notepad" }, pins);
    }

    [TestMethod]
    public void DraggingProducesTheNewPinAndWindowOrders()
    {
        List<string> pins = new List<string> { Calc + ">Calculatrice", Notepad + ">Bloc-notes" };
        List<TaskWindow> windows = new List<TaskWindow> { W(1, "exe:C:\\x.exe"), W(2, Notepad) };
        List<BarButton> b = TaskbarModel.Build(pins, windows);
        // Bloc-notes glissé avant la Calculatrice, la fenêtre x avant le Bloc-notes
        BarButton note = b[1], calc = b[0], x = b[2];
        List<BarButton> dragged = new List<BarButton> { x, note, calc };
        CollectionAssert.AreEqual(new[] { Notepad + ">Bloc-notes", Calc + ">Calculatrice" }, TaskbarModel.PinsInOrder(dragged));
        CollectionAssert.AreEqual(new[] { new IntPtr(1), new IntPtr(2) }, TaskbarModel.WindowsInOrder(dragged));
        // Reconstruite, la fenêtre non épinglée revient après les épingles (D-014)
        List<BarButton> rebuilt = TaskbarModel.Build(TaskbarModel.PinsInOrder(dragged), windows);
        Assert.AreEqual(Notepad, rebuilt[0].Key);
        Assert.AreEqual(Calc, rebuilt[1].Key);
        Assert.AreEqual(new IntPtr(1), rebuilt[2].Window.Handle);
    }

    // QA-66 : les programmes hôtes ne désignent pas l'application (toutes les applis du Store auraient la même épingle)
    [TestMethod]
    public void HostProcessesGiveNoAppKey()
    {
        Assert.IsNull(WindowList.KeyForWindow(null, @"C:\Windows\System32\ApplicationFrameHost.exe"));
        Assert.IsNull(WindowList.KeyForWindow(null, @"C:\Windows\System32\DLLHOST.EXE"));
        Assert.IsNull(WindowList.KeyForWindow("", @"C:\Windows\System32\rundll32.exe"));
        Assert.AreEqual("aumid:Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
            WindowList.KeyForWindow("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", @"C:\Windows\System32\ApplicationFrameHost.exe"),
            "Avec un identifiant, l'hôte n'importe pas.");
        Assert.AreEqual(Notepad, WindowList.KeyForWindow(null, Notepad.Substring(4)));
        Assert.AreEqual(@"exe:C:\Outils\myapplicationframehost.exe", WindowList.KeyForWindow(null, @"C:\Outils\myapplicationframehost.exe"),
            "Seul le nom exact du fichier compte.");
    }

    [TestMethod]
    public void WindowKeyPrefersTheAppUserModelId()
    {
        Assert.AreEqual("aumid:App.Id", TaskbarModel.KeyFor("App.Id", @"C:\a.exe"));
        Assert.AreEqual(@"exe:C:\a.exe", TaskbarModel.KeyFor(null, @"C:\a.exe"));
        Assert.IsNull(TaskbarModel.KeyFor("", null));
    }

    [TestMethod]
    public void LaunchingAPinIsBlockedInTests()
    {
        Assert.ThrowsException<InvalidOperationException>(() => TaskbarModel.Launch(Notepad));
        Assert.IsFalse(TaskbarModel.CanLaunch(@"exe:C:\nexiste\pas.exe"));
        Assert.IsTrue(TaskbarModel.CanLaunch(Calc, id => true), "Application connue de Windows : lançable.");
    }

    [TestMethod]
    public void PinsAreSavedAndKeptLocalOnImport()
    {
        string previous = Settings.DirOverride, dir = Path.Combine(Path.GetTempPath(), "BureauCache.Pins-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string snapshot = Path.Combine(dir, "avant.ini");
        File.WriteAllLines(snapshot, Settings.Serialize());
        Settings.DirOverride = Path.Combine(dir, "Reglages");
        try
        {
            Settings.TaskbarPins = new List<string> { Calc + ">Calculatrice" };
            Assert.IsTrue(Settings.Serialize().Contains("TaskbarPins=" + Calc + ">Calculatrice"));

            List<string> lines = Settings.Serialize().Where(l => !l.StartsWith("TaskbarPins=")).ToList();
            lines.Add(@"TaskbarPins=exe:C:\Users\x\Downloads\inconnu.exe>Outil");
            string foreign = Path.Combine(dir, "recu.ini");
            File.WriteAllLines(foreign, lines, new UTF8Encoding(false));
            List<string> kept = new List<string>();
            Assert.IsTrue(SettingsBackup.ImportFrom(foreign, kept));
            CollectionAssert.AreEqual(new[] { Calc + ">Calculatrice" }, Settings.TaskbarPins, "Une épingle importée n'est pas reprise.");
            Assert.IsTrue(kept.Any(k => k.Contains(L.T("épinglées", "pinned", "ancladas"))));
        }
        finally
        {
            Settings.CancelPendingSave();
            Settings.Load(snapshot);
            Settings.TaskbarPins = new List<string>();
            Settings.DirOverride = previous;
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    // Revue Codex (QA-67) ------------------------------------------------------------------------------------------

    [TestMethod]
    public void CDX01_RollbackAssuresRecoveryBeforeShowingWarning()
    {
        foreach (bool restored in new[] { true, false })
        {
            List<string> calls = new List<string>();
            Program.PrepareRollback(() => { calls.Add("restore"); return restored; }, () => calls.Add("retry"), () => calls.Add("warn"));
            CollectionAssert.AreEqual(restored ? new[] { "restore", "warn" } : new[] { "restore", "retry", "warn" }, calls);
        }
        List<string> afterError = new List<string>();
        Program.PrepareRollback(() => { throw new InvalidOperationException(); }, () => afterError.Add("retry"), () => afterError.Add("warn"));
        CollectionAssert.AreEqual(new[] { "retry", "warn" }, afterError, "Une restauration qui échoue n'empêche pas l'avertissement.");
    }

    [TestMethod]
    public void CDX06_ShutdownReportsRefusalWithoutRetrying()
    {
        foreach (uint error in new uint[] { 1191, 1314, 5 })
        {
            List<uint> calls = new List<uint>(), errors = new List<uint>();
            uint flags = Native.SHUTDOWN_POWEROFF | Native.SHUTDOWN_HYBRID | Native.SHUTDOWN_INSTALL_UPDATES;
            Assert.IsFalse(SessionActions.RequestShutdown(flags, f => { calls.Add(f); return error; }, errors.Add));
            CollectionAssert.AreEqual(new[] { flags }, calls, "Erreur " + error + " : pas de nouvel essai.");
            CollectionAssert.AreEqual(new[] { error }, errors, "Erreur " + error + " : signalée.");
        }
    }

    [TestMethod]
    public void CDX06_HybridFailureRetriesNormalShutdownKeepingUpdates()
    {
        List<uint> calls = new List<uint>(), errors = new List<uint>();
        uint flags = Native.SHUTDOWN_POWEROFF | Native.SHUTDOWN_HYBRID | Native.SHUTDOWN_INSTALL_UPDATES;
        Assert.IsTrue(SessionActions.RequestShutdown(flags, f => { calls.Add(f); return (f & Native.SHUTDOWN_HYBRID) != 0 ? 50u : 0u; }, errors.Add));
        CollectionAssert.AreEqual(new[] { flags, Native.SHUTDOWN_POWEROFF | Native.SHUTDOWN_INSTALL_UPDATES }, calls);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void CDX09_ValidAppIdAloneDoesNotMakeAnAppLaunchable()
    {
        List<string> lookups = new List<string>();
        Func<string, bool> exists = id => { lookups.Add(id); return id == "Exemple_abc!App"; };
        Assert.IsTrue(TaskbarModel.ValidKey("aumid:Exemple.Win32"));
        Assert.IsFalse(TaskbarModel.CanLaunch("aumid:Exemple.Win32", exists));
        Assert.IsTrue(TaskbarModel.CanLaunch("aumid:Exemple_abc!App", exists));
        CollectionAssert.AreEqual(new[] { "Exemple.Win32", "Exemple_abc!App" }, lookups);
    }

    [TestMethod]
    public void CDX10_CloseReportsAnAccessDeniedByTheTarget()
    {
        IntPtr target = new IntPtr(42);
        List<int> errors = new List<int>();
        List<IntPtr> attempts = new List<IntPtr>();
        Assert.IsFalse(WindowList.TryClose(target, h => h == target, h => { attempts.Add(h); return 5; }, errors.Add));
        CollectionAssert.AreEqual(new[] { target }, attempts);
        CollectionAssert.AreEqual(new[] { 5 }, errors);
        Assert.IsTrue(WindowList.TryClose(target, h => true, h => 0, errors.Add), "Demande partie : vrai.");
        Assert.IsFalse(WindowList.TryClose(target, h => false, h => 0, errors.Add), "Fenêtre disparue : rien n'est envoyé.");
        Assert.AreEqual(1, errors.Count);
    }

    [TestMethod]
    public void CDX11_FailedSleepDoesNotBlankDisplayOnNonS0Machine()
    {
        int displayOff = 0, failures = 0;
        SessionActions.RequestSleep(() => false, () => false, () => displayOff++, () => failures++);
        Assert.AreEqual(0, displayOff);
        Assert.AreEqual(1, failures);
        SessionActions.RequestSleep(() => false, () => true, () => displayOff++, () => failures++);
        Assert.AreEqual(1, displayOff, "Veille moderne : l'écran s'éteint, le PC entre en veille.");
        SessionActions.RequestSleep(() => true, () => true, () => displayOff++, () => failures++);
        Assert.AreEqual(1, displayOff, "Veille classique réussie : rien d'autre.");
        Assert.AreEqual(1, failures);
    }

    [TestMethod]
    public void CDX13_ReleasingOverStartAfterPressInEmptyBarDoesNotOpenIt()
    {
        Assert.IsTrue(Sandbox.Active);
        List<string> previous = Settings.TaskbarPins;
        try
        {
            Settings.TaskbarPins = new List<string>();
            using (ClickBar bar = new ClickBar())
            using (Bitmap frame = new Bitmap(bar.Width, bar.Height))
            {
                bar.RebuildButtons();
                bar.DrawToBitmap(frame, bar.ClientRectangle);
                Rectangle start = bar.RectangleToClient(bar.StartButtonBounds);
                int y = start.Top + start.Height / 2;
                Point center = new Point(start.Left + start.Width / 2, y);
                int opens = 0;
                bar.StartClicked += () => opens++;

                bar.PressAt(new Point(start.Right + 1, y));
                bar.ReleaseAt(center);
                Assert.AreEqual(0, opens, "Appui dans le vide, relâché sur Démarrer : rien.");

                bar.PressAt(center);
                bar.ReleaseAt(center);
                Assert.AreEqual(1, opens, "Clic entier sur Démarrer : le menu s'ouvre.");
            }
        }
        finally { Settings.TaskbarPins = previous; }
    }
}
