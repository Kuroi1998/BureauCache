using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// D-014 : applications épinglées et ordre des boutons de la barre. Aucun lancement réel (bac à sable).
[TestClass]
public class TaskbarPinsTests
{
    const string Notepad = @"exe:C:\Windows\notepad.exe";
    const string Calc = "aumid:Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";

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
        Assert.IsTrue(TaskbarModel.CanLaunch(Calc));
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
}
