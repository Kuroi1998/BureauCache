using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// D-011 : historique du presse-papiers (en mémoire, copies privées ignorées) et bureaux virtuels.
// Le vrai presse-papiers n'est jamais écouté pendant les tests (bac à sable).
[TestClass]
public class ClipboardDesktopTests
{
    bool clipboardOn;

    [TestInitialize]
    public void SetUp() { clipboardOn = Settings.ClipboardOn; ClipboardHistory.Clear(); }

    [TestCleanup]
    public void TearDown() { ClipboardHistory.Clear(); Settings.ClipboardOn = clipboardOn; Settings.CancelPendingSave(); }

    [TestMethod]
    public void HistoryKeepsTheLatestTextsWithoutDuplicates()
    {
        for (int i = 1; i <= ClipboardHistory.MaxEntries + 5; i++) Assert.IsTrue(ClipboardHistory.Add("texte " + i));
        List<ClipboardHistory.Entry> items = ClipboardHistory.Items;
        Assert.AreEqual(ClipboardHistory.MaxEntries, items.Count);
        Assert.AreEqual("texte 25", items[0].Text, "Le plus récent en tête.");
        Assert.AreEqual("texte 6", items.Last().Text, "Les plus anciens partent.");

        int id = items[5].Id;
        string again = items[5].Text;
        ClipboardHistory.Add(again);
        Assert.AreEqual(ClipboardHistory.MaxEntries, ClipboardHistory.Items.Count, "Un texte recopié remonte au lieu d'être dupliqué.");
        Assert.AreEqual(id, ClipboardHistory.Items[0].Id);
    }

    [TestMethod]
    public void EmptyBlankAndHugeTextsAreRefused()
    {
        Assert.IsFalse(ClipboardHistory.Add(null));
        Assert.IsFalse(ClipboardHistory.Add(""));
        Assert.IsFalse(ClipboardHistory.Add(" \r\n\t "));
        Assert.IsFalse(ClipboardHistory.Add(new string('x', ClipboardHistory.MaxLength + 1)));
        Assert.IsTrue(ClipboardHistory.Add(new string('x', ClipboardHistory.MaxLength)));
        Assert.AreEqual(1, ClipboardHistory.Items.Count);
    }

    [TestMethod]
    public void RemoveAndClearNotifyThePanel()
    {
        int fired = 0;
        Action h = () => fired++;
        ClipboardHistory.Changed += h;
        try
        {
            ClipboardHistory.Add("un");
            ClipboardHistory.Add("deux");
            ClipboardHistory.Remove(ClipboardHistory.Items.First(e => e.Text == "un").Id);
            CollectionAssert.AreEqual(new[] { "deux" }, ClipboardHistory.Items.Select(e => e.Text).ToArray());
            ClipboardHistory.Clear();
            Assert.AreEqual(0, ClipboardHistory.Items.Count);
            Assert.AreEqual(4, fired);
            ClipboardHistory.Clear();
            Assert.AreEqual(4, fired, "Vider un historique déjà vide ne redessine pas le panneau.");
        }
        finally { ClipboardHistory.Changed -= h; }
    }

    [TestMethod]
    public void PreviewShowsTheFirstMeaningfulLine()
    {
        Assert.AreEqual("Bonjour le monde", ClipboardHistory.Preview("\r\n   \r\n  Bonjour   le\tmonde \r\nsuite", 80));
        Assert.AreEqual("abcd…", ClipboardHistory.Preview("abcdefgh", 5));
        Assert.AreEqual("", ClipboardHistory.Preview(null, 5));
    }

    static DataObject Text(string s) { DataObject d = new DataObject(); d.SetData(DataFormats.UnicodeText, s); return d; }

    [TestMethod]
    public void PreviewKeepsSupplementaryCharactersAndCombiningMarksIntact()
    {
        Assert.AreEqual("x…", ClipboardHistory.Preview("x\U0001F986 suite", 3));
        Assert.AreEqual("x…", ClipboardHistory.Preview("xe\u0301 suite", 3));
        Assert.AreEqual("", ClipboardHistory.Preview("texte", 0));
    }

    [TestMethod]
    public void PrivateCopiesAreRecognized()
    {
        Assert.IsFalse(ClipboardHistory.IsPrivate(Text("public")));
        Assert.IsTrue(ClipboardHistory.IsPrivate(null));

        DataObject excluded = Text("secret");
        excluded.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[] { 0 }));
        Assert.IsTrue(ClipboardHistory.IsPrivate(excluded));

        DataObject ignored = Text("secret");
        ignored.SetData("Clipboard Viewer Ignore", new MemoryStream(new byte[] { 0 }));
        Assert.IsTrue(ClipboardHistory.IsPrivate(ignored));

        DataObject refused = Text("secret");
        refused.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
        Assert.IsTrue(ClipboardHistory.IsPrivate(refused));

        DataObject allowed = Text("ok");
        allowed.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(1)));
        Assert.IsFalse(ClipboardHistory.IsPrivate(allowed));
    }

    [TestMethod]
    public void HistoryIsNeverWrittenToTheSettings()
    {
        Settings.ClipboardOn = true;
        ClipboardHistory.Add("mot-de-passe-123");
        string[] lines = Settings.Serialize();
        Assert.IsFalse(lines.Any(l => l.Contains("mot-de-passe-123")), "Le texte copié ne doit jamais aller sur le disque.");
        Assert.IsTrue(lines.Contains("ClipboardOn=1"));
    }

    [TestMethod]
    public void ClipboardOptionIsOffByDefaultAndRoundTrips()
    {
        StringAssert.Contains(File.ReadAllText(Path.Combine(RepoRoot(), "src", "Core", "Settings.cs")), "public static bool ClipboardOn = false;", "L'historique est désactivé par défaut.");
        string ini = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ini");
        try
        {
            Settings.ClipboardOn = true;
            File.WriteAllLines(ini, Settings.Serialize());
            Settings.ClipboardOn = false;
            Assert.IsTrue(Settings.Load(ini));
            Assert.IsTrue(Settings.ClipboardOn);
        }
        finally { File.Delete(ini); }
    }

    static string RepoRoot()
    {
        string d = AppDomain.CurrentDomain.BaseDirectory;
        while (d != null && !File.Exists(Path.Combine(d, "build.cmd"))) d = Path.GetDirectoryName(d);
        Assert.IsNotNull(d, "Racine du dépôt introuvable.");
        return d;
    }

    [TestMethod]
    public void ListenerAndDesktopMovesAreBlockedDuringTests()
    {
        ClipboardWatcher w = new ClipboardWatcher();
        try { w.Enable(true); Assert.Fail("Le bac à sable doit bloquer l'écoute du vrai presse-papiers."); }
        catch (InvalidOperationException e) { StringAssert.Contains(e.Message, "Effet de bord bloqué"); }
        finally { w.Close(); }
        Assert.IsFalse(Native.MoveToActiveDesktop(new IntPtr(1234)));
    }

    [TestMethod]
    public void PanelShowsTheClipboardSectionOnlyWhenEnabled()
    {
        RunSta(() =>
        {
            using (PanelForm panel = new PanelForm())
            {
                MethodInfo clipGroup = typeof(PanelForm).GetMethod("ClipGroup", BindingFlags.Instance | BindingFlags.NonPublic);
                ClipboardHistory.Add("premier");
                ClipboardHistory.Add("second\r\nligne");
                Settings.ClipboardOn = false;
                Assert.IsNull(clipGroup.Invoke(panel, new object[] { 16 }));
                Settings.ClipboardOn = true;
                Group g = (Group)clipGroup.Invoke(panel, new object[] { 16 });
                Assert.IsNotNull(g);
                Assert.AreEqual("clip", g.Key);
                CollectionAssert.AreEqual(new[] { "second", "premier" }, g.Items.Select(i => i.Name).ToArray());
                Assert.IsTrue(g.Items.All(i => i.Kind == ItemKind.Clip && i.Run != null && !i.HasPath));

                // Le menu contextuel retire l'entrée de l'historique
                typeof(PanelForm).GetMethod("RemoveClip", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, new object[] { g.Items[0] });
                CollectionAssert.AreEqual(new[] { "premier" }, ClipboardHistory.Items.Select(e => e.Text).ToArray());
            }
        });
    }

    static void RunSta(Action action)
    {
        Exception error = null;
        Thread t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.IsBackground = true;
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        Assert.IsTrue(t.Join(10000), "Le scénario WinForms doit se terminer en dix secondes.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
