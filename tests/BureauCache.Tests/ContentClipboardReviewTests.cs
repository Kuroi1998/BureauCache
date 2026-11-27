using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ContentClipboardReviewTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    [TestCategory("Review")]
    public void QA42_01_QueryRestrictsTermsToDocumentContents()
    {
        AssertSandbox();
        string sql = ContentSearch.BuildQuery("facture", new[] { Paths.Desktop }, 20);
        StringAssert.Contains(sql, "CONTAINS(System.Search.Contents,",
            "QA42-01 : une correspondance dans les métadonnées seules ne devrait pas remonter dans le contenu.");
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA42_02_TermAbandonedForACommandCancelsTheDebounce()
    {
        RunSta(() =>
        {
            bool previous = Settings.SearchContent;
            try
            {
                Settings.SearchContent = true;
                using (var panel = new PanelForm())
                {
                    TextBox search = Field<TextBox>(panel, "search");
                    var timer = Field<System.Windows.Forms.Timer>(panel, "contentTimer");
                    search.Text = "facture";
                    Assert.IsTrue(timer.Enabled);
                    search.Text = ">";
                    Assert.IsFalse(timer.Enabled, "QA42-02 : le minuteur peut encore lancer la recherche abandonnée.");
                }
            }
            finally { Settings.SearchContent = previous; }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void ReturningToAnOldQueryDoesNotShowItsStaleFileResults()
    {
        RunSta(() =>
        {
            bool previous = Settings.SearchContent;
            try
            {
                Settings.SearchContent = true;
                using (var panel = new PanelForm())
                {
                    TextBox search = Field<TextBox>(panel, "search");
                    search.Text = "facture";
                    string file = Path.Combine(Paths.Desktop, "ancien.txt");
                    Field<List<DesktopItem>>(panel, "contentItems").Add(new DesktopItem { Key = file, Target = file, Name = "ancien.txt" });
                    typeof(PanelForm).GetField("contentFor", Private).SetValue(panel, "facture");
                    panel.Rebuild(true);
                    Assert.IsTrue(Field<List<Group>>(panel.View, "groups").Any(g => g.Key == "s:content"));
                    search.Text = "autre";
                    search.Text = "facture";
                    Assert.IsFalse(Field<List<Group>>(panel.View, "groups").Any(g => g.Key == "s:content"));
                }
            }
            finally { Settings.SearchContent = previous; }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA42_03_ScopeUsesTheCanonicalPath()
    {
        AssertSandbox();
        string path = ContentSearch.PathFromUrl("file:C:/QA/Bureau/%2E%2E/hors-bureau.txt");
        Assert.IsFalse(path != null && ContentSearch.NearDesktop(path, new[] { @"C:\QA\Bureau" }),
            "QA42-03 : le résultat résolu hors du bureau ne doit pas être retenu.");
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA42_04_UnreadableDesktopIsReportedAsPartial()
    {
        AssertSandbox();
        string missing = Path.Combine(Paths.Desktop, Guid.NewGuid().ToString("N"));
        Clutter.Report report = Clutter.Measure(missing, new HashSet<string>(),
            new Dictionary<string, long[]>(), Path.Combine(missing, "Archive"), DateTime.UtcNow);
        Assert.IsTrue(report.Partial, "QA42-04 : une énumération impossible est présentée comme un bureau vide.");
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA42_05_HistorySearchCanFindTextBeyondThePreview()
    {
        RunSta(() =>
        {
            bool previous = Settings.ClipboardOn, content = Settings.SearchContent;
            ClipboardHistory.Clear();
            try
            {
                Settings.ClipboardOn = true;
                Settings.SearchContent = false;
                ClipboardHistory.Add("Première ligne\r\nquasarzeta");
                for (int i = 0; i < 5; i++) ClipboardHistory.Add("Texte fictif " + i);
                using (var panel = new PanelForm())
                {
                    Field<TextBox>(panel, "search").Text = "quasarzeta";
                    var groups = Field<List<Group>>(panel.View, "groups");
                    Assert.IsTrue(groups.SelectMany(group => group.Items).Any(item => item.Kind == ItemKind.Clip),
                        "QA42-05 : le texte est en mémoire, mais le panneau ne le retrouve pas.");
                }
            }
            finally
            {
                ClipboardHistory.Clear();
                Settings.ClipboardOn = previous;
                Settings.SearchContent = content;
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA42_06_UndoDoesNotMoveADifferentFileWithMatchingSizeAndTimestamp()
    {
        using (var scope = new ArchiveScope())
        {
            string source = scope.OldFile("capture.png", "aaaa");
            Assert.AreEqual(1, Archiver.Run(new List<string> { source }));
            string archived = Path.Combine(scope.Destination, "capture.png");
            string original = Path.Combine(scope.Destination, "original.png");
            DateTime stamp = File.GetLastWriteTimeUtc(archived);
            FileOps.Move(archived, original, false);
            File.WriteAllText(archived, "bbbb");
            File.SetLastWriteTimeUtc(archived, stamp);

            Assert.AreEqual(0, Archiver.Undo(), "QA42-06 : un autre fichier a été pris pour l'original.");
            Assert.AreEqual("bbbb", File.ReadAllText(archived));
            Assert.AreEqual("aaaa", File.ReadAllText(original));
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA42_07_UndoKeepsTheJournalWhenTheDestinationIsTemporarilyMissing()
    {
        using (var scope = new ArchiveScope())
        {
            string source = scope.OldFile("capture.png", "archive fictive");
            Assert.AreEqual(1, Archiver.Run(new List<string> { source }));
            string offline = Path.Combine(scope.Destination, "..", "indisponible");
            offline = Path.GetFullPath(offline);
            FileOps.Move(scope.Destination, offline, true);
            Assert.AreEqual(0, Archiver.Undo());
            Assert.IsTrue(Archiver.CanUndo, "QA42-07 : le chemin indisponible a fait disparaître le journal.");
            FileOps.Move(offline, scope.Destination, true);
            Assert.AreEqual(1, Archiver.Undo());
            Assert.IsTrue(File.Exists(source));
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA42_08_RunCannotReportAMoveWithoutADurableJournal()
    {
        using (var scope = new ArchiveScope())
        {
            string source = scope.OldFile("capture.png", "archive fictive");
            Settings.LastVersion = "";
            int padding = (int)(Settings.MaxFileSize - Settings.Utf8Size(Settings.Serialize()) - 32);
            Assert.IsTrue(padding > 0);
            Settings.LastVersion = new string('x', padding);
            Assert.IsTrue(Settings.Save(), "Le profil sans le nouveau journal doit pouvoir être enregistré.");

            int moved = Archiver.Run(new List<string> { source });
            if (moved == 0)
                Assert.IsTrue(File.Exists(source), "QA42-08 : un déplacement refusé doit laisser la source en place.");
            else
                Assert.IsTrue(File.ReadAllText(Settings.FilePath).Contains(source + ">"),
                    "QA42-08 : le profil enregistré ne contient pas le déplacement annoncé.");
        }
    }

    static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, Private).GetValue(target);
    }

    static void AssertSandbox()
    {
        Assert.IsTrue(Sandbox.Active);
        Assert.IsNotNull(Paths.DesktopOverride);
        Assert.IsNotNull(Settings.DirOverride);
        Sandbox.GuardPath(Paths.DesktopOverride, "bureau de test");
        Sandbox.GuardPath(Settings.DirOverride, "réglages de test");
    }

    static void RunSta(Action work)
    {
        AssertSandbox();
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
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    sealed class ArchiveScope : IDisposable
    {
        readonly string root;
        readonly string desktop = Paths.DesktopOverride, settings = Settings.DirOverride;
        readonly string archive = Settings.ArchiveName, version = Settings.LastVersion;
        readonly long lastArchive = Settings.LastArchive;
        readonly List<ArchiveRule> rules = Settings.ArchiveRules;
        readonly List<string> log = Settings.ArchiveLog, favorites = Settings.Favorites, order = Settings.Order;
        readonly Dictionary<string, long[]> usage = Settings.Usage;
        readonly Dictionary<string, string> categories = Settings.CatOverride;
        readonly bool saveBlocked = Settings.SaveBlocked;

        public ArchiveScope()
        {
            AssertSandbox();
            root = Path.Combine(Path.GetTempPath(), "BureauCache-QA42-" + Guid.NewGuid().ToString("N"));
            string desk = Path.Combine(root, "Bureau");
            Directory.CreateDirectory(desk);
            Paths.DesktopOverride = desk;
            Settings.DirOverride = Path.Combine(root, "Reglages");
            Settings.ArchiveName = "Archive-QA";
            Settings.SaveBlocked = false;
            Settings.ArchiveLog = new List<string>();
            Settings.Favorites = new List<string>();
            Settings.Order = new List<string>();
            Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
            Settings.CatOverride = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Settings.ArchiveRules = new List<ArchiveRule>
            {
                new ArchiveRule { Name = "Captures", Filter = RuleFilter.Extensions, Pattern = "png", Target = RuleTarget.Pictures }
            };
        }

        public string Destination { get { return ArchiveRules.TargetFolder(RuleTarget.Pictures); } }

        public string OldFile(string name, string text)
        {
            string path = Path.Combine(Paths.Desktop, name);
            File.WriteAllText(path, text);
            DateTime old = DateTime.UtcNow.AddHours(-3);
            File.SetCreationTimeUtc(path, old);
            File.SetLastWriteTimeUtc(path, old);
            return path;
        }

        public void Dispose()
        {
            Settings.CancelPendingSave();
            Settings.ArchiveLog = log;
            Settings.ArchiveRules = rules;
            Settings.Favorites = favorites;
            Settings.Order = order;
            Settings.Usage = usage;
            Settings.CatOverride = categories;
            Settings.ArchiveName = archive;
            Settings.LastVersion = version;
            Settings.LastArchive = lastArchive;
            Settings.SaveBlocked = saveBlocked;
            Paths.DesktopOverride = desktop;
            Settings.DirOverride = settings;
            Directory.Delete(root, true);
        }
    }
}
