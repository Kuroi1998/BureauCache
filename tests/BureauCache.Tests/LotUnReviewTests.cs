using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class LotUnReviewTests
{
    const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    string fixtures;

    [TestInitialize]
    public void SetUp()
    {
        Assert.IsTrue(Sandbox.Active);
        Assert.IsNotNull(Paths.DesktopOverride);
        Assert.IsNotNull(Settings.DirOverride);
        string temp = Path.GetFullPath(Path.GetTempPath());
        Assert.IsTrue(Path.GetFullPath(Paths.DesktopOverride).StartsWith(temp, StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(Path.GetFullPath(Settings.DirOverride).StartsWith(temp, StringComparison.OrdinalIgnoreCase));
        fixtures = Path.Combine(temp, "BureauCache-LotUn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtures);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (fixtures != null) Directory.Delete(fixtures, true);
    }

    // Règle retenue (QA-35) : en mode test, aucune écriture hors de %TEMP%. La cible est hors de %TEMP%, dans un
    // dossier qui n'existe pas : même si le garde-fou régressait, rien ne pourrait y être écrit.
    static string Outside(string name)
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "BureauCache-QA-absent-" + Guid.NewGuid().ToString("N"), name);
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA34_01_FileMoveCannotLeaveTheIsolatedDesktop()
    {
        string source = Path.Combine(Paths.Desktop, Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(source, "test fixture");
        try
        {
            Assert.ThrowsException<InvalidOperationException>(() => FileOps.Move(source, Outside("outside.txt"), false),
                "QA34-01 : FileOps.Move doit refuser une destination hors de %TEMP%.");
            Assert.IsTrue(File.Exists(source));
        }
        finally { if (File.Exists(source)) File.Delete(source); }
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA34_01_CopyCannotOverwriteAFileOutsideTheIsolatedFolders()
    {
        string source = Path.Combine(Paths.Desktop, Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(source, "modified");
        try
        {
            Assert.ThrowsException<InvalidOperationException>(() => FileOps.CopyFile(source, Outside("outside.txt")),
                "QA34-01 : FileOps.CopyFile doit refuser une destination hors de %TEMP%.");
        }
        finally { File.Delete(source); }
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA34_02_ArchiveRejectsAFileOutsideTheIsolatedDesktop()
    {
        string outside = Path.Combine(fixtures, "outside.txt");
        File.WriteAllText(outside, "test fixture");
        File.SetCreationTimeUtc(outside, DateTime.UtcNow.AddYears(-2));
        File.SetLastWriteTimeUtc(outside, DateTime.UtcNow.AddYears(-2));

        string oldDesktop = Paths.DesktopOverride, oldSettings = Settings.DirOverride, archiveName = Settings.ArchiveName;
        long lastArchive = Settings.LastArchive;
        List<string> log = Settings.ArchiveLog, favorites = Settings.Favorites, order = Settings.Order;
        var usage = Settings.Usage;
        var categories = Settings.CatOverride;
        string desk = Path.Combine(fixtures, "Bureau");
        Directory.CreateDirectory(desk);
        try
        {
            Paths.DesktopOverride = desk;
            Settings.DirOverride = Path.Combine(fixtures, "Reglages");
            Settings.ArchiveName = "QA-Archive";
            Settings.ArchiveLog = new List<string>();
            Settings.Favorites = new List<string>();
            Settings.Order = new List<string>();
            Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
            Settings.CatOverride = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int moved = Archiver.Run(new List<string> { outside });
            Assert.IsTrue(moved == 0 && File.Exists(outside),
                "QA34-02 : Archiver.Run a déplacé un fichier qui ne se trouvait pas sur le bureau isolé.");
        }
        finally
        {
            Settings.CancelPendingSave();
            Paths.DesktopOverride = oldDesktop;
            Settings.DirOverride = oldSettings;
            Settings.ArchiveName = archiveName;
            Settings.ArchiveLog = log;
            Settings.LastArchive = lastArchive;
            Settings.Favorites = favorites;
            Settings.Order = order;
            Settings.Usage = usage;
            Settings.CatOverride = categories;
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA34_03_UpdaterCannotReplaceAnExecutableOutsideTheIsolatedFolders()
    {
        string staged = Path.Combine(fixtures, "staged.exe");
        File.WriteAllText(staged, "replacement fixture");
        string installed = Outside("outside.exe");
        Assert.ThrowsException<InvalidOperationException>(() => Updater.ReplaceExecutable(staged, installed),
            "QA34-03 : le remplacement doit refuser un exe hors de %TEMP%.");
        Assert.ThrowsException<InvalidOperationException>(() => Updater.BeginStart(installed));
        Assert.ThrowsException<InvalidOperationException>(() => Updater.RollBack(installed));
        Assert.ThrowsException<InvalidOperationException>(() => FileOps.Recycle(staged, false), "La vraie corbeille est bloquée.");
        Assert.ThrowsException<InvalidOperationException>(() => Cleaner.Clean(new List<CleanCategory>()), "Le nettoyage réel est bloqué.");
        Assert.AreEqual(1, Paths.Desktops.Length, "Le bureau commun réel n'est jamais parcouru.");
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA34_04_OpenFormsOnlyDetectsFormsRegisteredWithWinForms()
    {
        RunSta(() =>
        {
            using (var form = new Form())
            {
                Assert.IsFalse(form.IsHandleCreated);
                Assert.IsFalse(Application.OpenForms.Cast<Form>().Any(candidate => ReferenceEquals(candidate, form)),
                    "QA34-04 : témoin du périmètre de détection, aucune instance sans handle n'a été enregistrée.");
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA34_09_ClickingAnUndoErrorOutsideTheButtonDoesNotCommit()
    {
        RunSta(() =>
        {
            using (var toast = new ToastControl(null))
            {
                int committed = 0;
                toast.Show("Suppression QA", () => { throw new IOException("Test obstruction"); }, () => committed++);
                toast.Undo();
                Assert.IsTrue(toast.CanUndo, "Le bandeau doit permettre une nouvelle tentative.");
                typeof(ToastControl).GetMethod("OnMouseClick", InstancePrivate)
                    .Invoke(toast, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0) });
                Assert.AreEqual(0, committed,
                    "QA34-09 : un clic sur le texte d'erreur a validé la suppression en attente.");
                Assert.IsTrue(toast.CanUndo);
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA34_10_StationaryPointerIsReevaluatedWhenTheScreenSelectionChanges()
    {
        RunSta(() =>
        {
            bool right = Settings.RightSide;
            int zone = Settings.EdgeZone, index = Settings.ScreenIdx;
            string name = Settings.ScreenName;
            try
            {
                Screen[] all = Screen.AllScreens;
                Screen screen = all.OrderByDescending(value => value.Bounds.Right).First();
                var point = new Point(screen.Bounds.Right - 1, screen.Bounds.Top);
                var app = (App)FormatterServices.GetUninitializedObject(typeof(App));
                var screens = typeof(App).GetField("screens", InstancePrivate);
                screens.SetValue(app, new[] { screen });
                Settings.RightSide = true;
                Settings.EdgeZone = 0;
                Settings.ScreenIdx = -1;
                Settings.ScreenName = "";
                Assert.IsTrue(AtEdge(app, point));
                Settings.RightSide = false;
                Assert.IsFalse(AtEdge(app, point));
                Settings.RightSide = true;
                Settings.EdgeZone = 3;
                Assert.IsFalse(AtEdge(app, point));
                Settings.EdgeZone = 0;
                Assert.IsTrue(AtEdge(app, point));

                Rectangle bounds = screen.Bounds;
                Screen other = FakeScreen("QA-autre", new Rectangle(bounds.Left - 200, bounds.Top, 100, bounds.Height));
                screens.SetValue(app, new[] { screen, other });
                Settings.ScreenIdx = 0;
                Settings.ScreenName = screen.DeviceName;
                Assert.IsTrue(AtEdge(app, point));
                Settings.ScreenName = other.DeviceName;
                Assert.IsFalse(AtEdge(app, point));
                Settings.ScreenName = "";
                Settings.ScreenIdx = 0;
                Assert.IsTrue(AtEdge(app, point));
                Settings.ScreenIdx = 1;
                Assert.IsFalse(AtEdge(app, point));

                Settings.ScreenIdx = -1;
                Assert.IsTrue(AtEdge(app, point));
                Screen adjacent = FakeScreen("QA-adjacent", new Rectangle(bounds.Right, bounds.Top, 100, bounds.Height));
                screens.SetValue(app, new[] { screen, adjacent });
                Assert.IsFalse(AtEdge(app, point), "QA34-10 : le nouvel écran adjacent invalide le bord précédent.");
            }
            finally
            {
                Settings.RightSide = right;
                Settings.EdgeZone = zone;
                Settings.ScreenIdx = index;
                Settings.ScreenName = name;
            }
        });
    }

    static Screen FakeScreen(string name, Rectangle bounds)
    {
        var screen = (Screen)FormatterServices.GetUninitializedObject(typeof(Screen));
        typeof(Screen).GetField("deviceName", InstancePrivate).SetValue(screen, name);
        typeof(Screen).GetField("bounds", InstancePrivate).SetValue(screen, bounds);
        return screen;
    }

    static bool AtEdge(App app, Point point)
    {
        var method = typeof(App).GetMethod("AtEdge", InstancePrivate);
        return (bool)method.Invoke(app, new object[] { point, null });
    }

    static void RunSta(Action work)
    {
        Exception error = null;
        var thread = new Thread(() =>
        {
            try { work(); }
            catch (Exception ex) { error = ex; }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(10000), "Le scénario WinForms a dépassé dix secondes.");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
