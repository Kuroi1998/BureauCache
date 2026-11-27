using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class RulesComfortReviewTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    string root, previousDesktop, previousSettings, previousArchiveName;
    List<ArchiveRule> previousRules;
    List<string> previousLog, previousFavorites, previousOrder;
    Dictionary<string, long[]> previousUsage;
    Dictionary<string, string> previousCategories;
    long previousLastArchive;

    [TestInitialize]
    public void Isolate()
    {
        Assert.IsTrue(Sandbox.Active);
        Assert.IsNotNull(Paths.DesktopOverride);
        Assert.IsNotNull(Settings.DirOverride);
        previousDesktop = Paths.DesktopOverride;
        previousSettings = Settings.DirOverride;
        previousArchiveName = Settings.ArchiveName;
        previousRules = Settings.ArchiveRules;
        previousLog = Settings.ArchiveLog;
        previousFavorites = Settings.Favorites;
        previousOrder = Settings.Order;
        previousUsage = Settings.Usage;
        previousCategories = Settings.CatOverride;
        previousLastArchive = Settings.LastArchive;

        root = Path.Combine(Path.GetTempPath(), "BureauCache-RulesReview-" + Guid.NewGuid().ToString("N"));
        Paths.DesktopOverride = Path.Combine(root, "Bureau");
        Settings.DirOverride = Path.Combine(root, "Reglages");
        Directory.CreateDirectory(Paths.Desktop);
        Settings.ArchiveName = "Archive-QA";
        Settings.ArchiveRules = new List<ArchiveRule>();
        Settings.ArchiveLog = new List<string>();
        Settings.Favorites = new List<string>();
        Settings.Order = new List<string>();
        Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
        Settings.CatOverride = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    [TestCleanup]
    public void RestoreSettings()
    {
        if (root == null) return;
        Settings.CancelPendingSave();
        Settings.ArchiveRules = previousRules;
        Settings.ArchiveLog = previousLog;
        Settings.Favorites = previousFavorites;
        Settings.Order = previousOrder;
        Settings.Usage = previousUsage;
        Settings.CatOverride = previousCategories;
        Settings.LastArchive = previousLastArchive;
        Settings.ArchiveName = previousArchiveName;
        Paths.DesktopOverride = previousDesktop;
        Settings.DirOverride = previousSettings;
        Directory.Delete(root, true);
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA39_01_OpenWriterCannotHaveItsFileMovedByAnOnArrivalRule()
    {
        ArchiveRule rule = AddRule(RuleTarget.Pictures, "");
        string source = OldFile("progress.png");
        using (var output = new FileStream(source, FileMode.Open, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.AreEqual(0, Archiver.Run(new List<string> { source }),
                "QA39-01 : la règle a déplacé un fichier encore ouvert en écriture.");
            Assert.IsTrue(File.Exists(source));
            Assert.IsFalse(File.Exists(Path.Combine(rule.Destination(), "progress.png")));
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA39_02_UndoLeavesAReplacementAtTheArchivedPathAlone()
    {
        ArchiveRule rule = AddRule(RuleTarget.Pictures, "");
        string source = OldFile("original.png");
        Assert.AreEqual(1, Archiver.Run(new List<string> { source }));
        string archived = Path.Combine(rule.Destination(), "original.png");
        string renamed = Path.Combine(rule.Destination(), "renamed.png");
        FileOps.Move(archived, renamed, false);
        File.WriteAllText(archived, "autre fichier");

        Assert.AreEqual(0, Archiver.Undo(), "QA39-02 : Annuler a pris le fichier de remplacement.");
        Assert.IsFalse(File.Exists(source));
        Assert.AreEqual("autre fichier", File.ReadAllText(archived));
        Assert.AreEqual("fichier de test", File.ReadAllText(renamed));
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA39_03_UndoUsesTheArchiveLocationRecordedAtMoveTime()
    {
        ArchiveRule rule = AddRule(RuleTarget.Archive, "Captures");
        string source = OldFile("capture.png");
        string archived = Path.Combine(rule.Destination(), "capture.png");
        Assert.AreEqual(1, Archiver.Run(new List<string> { source }));
        Assert.IsTrue(File.Exists(archived));

        Settings.ArchiveRules.Clear();
        Settings.ArchiveName = "Nouvelle-archive-QA";
        Assert.AreEqual(1, Archiver.Undo(), "QA39-03 : changer le dossier d'archive invalide l'ancien journal.");
        Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA39_03_UndoSurvivesRuleRemovalAndKeepsAnExistingNameCollisionIntact()
    {
        ArchiveRule rule = AddRule(RuleTarget.Pictures, "Captures");
        string source = OldFile("collision.png");
        Directory.CreateDirectory(rule.Destination());
        string occupied = Path.Combine(rule.Destination(), "collision.png");
        File.WriteAllText(occupied, "à conserver");
        Assert.AreEqual(1, Archiver.Run(new List<string> { source }));
        Assert.AreEqual("à conserver", File.ReadAllText(occupied));
        Assert.IsTrue(File.Exists(Path.Combine(rule.Destination(), "collision (2).png")));

        Settings.ArchiveRules.Clear();
        Assert.AreEqual(1, Archiver.Undo(), "QA39-03 : supprimer la règle ne doit pas invalider son journal.");
        Assert.AreEqual("fichier de test", File.ReadAllText(source));
        Assert.AreEqual("à conserver", File.ReadAllText(occupied));
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA39_04_UnavailableDestinationDoesNotStopTheNextRule()
    {
        ArchiveRule blocked = AddRule(RuleTarget.Pictures, "Captures");
        ArchiveRule usable = AddRule(RuleTarget.Documents, "");
        usable.Pattern = "txt";
        Directory.CreateDirectory(ArchiveRules.TargetFolder(RuleTarget.Pictures));
        File.WriteAllText(blocked.Destination(), "un fichier bloque la création du dossier");
        string first = OldFile("capture.png"), second = OldFile("note.txt");

        Assert.AreEqual(1, Archiver.Run(new List<string> { first, second }), "QA39-04 : seul le fichier de la destination disponible doit partir.");
        Assert.IsTrue(File.Exists(first));
        Assert.IsTrue(File.Exists(Path.Combine(usable.Destination(), "note.txt")));
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA39_01_RulesStillSkipProtectedRecentAndExclusivelyLockedFiles()
    {
        ArchiveRule rule = AddRule(RuleTarget.Pictures, "");
        rule.Pattern = "png lnk";
        string favorite = OldFile("favorite.png"), shortcut = OldFile("shortcut.lnk");
        string used = OldFile("used.png"), edited = OldFile("edited.png"), locked = OldFile("locked.png");
        Settings.Favorites.Add(favorite);
        Settings.Usage.Add(used, new[] { 1L, DateTime.Now.AddMinutes(-15).Ticks });
        File.SetLastWriteTimeUtc(edited, DateTime.UtcNow.AddMinutes(-15));
        using (var output = new FileStream(locked, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            Assert.AreEqual(0, Archiver.Run(new List<string> { favorite, shortcut, used, edited, locked }));
            Assert.IsTrue(File.Exists(favorite) && File.Exists(shortcut) && File.Exists(used) &&
                          File.Exists(edited) && File.Exists(locked), "QA39-01 : les éléments protégés restent sur le bureau.");
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA39_05_FailedJournalSaveDoesNotLeaveAMoveWithoutDurableUndo()
    {
        ArchiveRule rule = AddRule(RuleTarget.Pictures, "");
        string source = OldFile("journal.png");
        Directory.CreateDirectory(Settings.FilePath);

        int moved = Archiver.Run(new List<string> { source });
        Assert.AreEqual(0, moved, "QA39-05 : le rangement annonce un succès sans pouvoir enregistrer l'annulation.");
        Assert.IsTrue(File.Exists(source));
        Assert.IsFalse(File.Exists(Path.Combine(rule.Destination(), "journal.png")));
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA39_06_EditingASavedAgeDoesNotChangeItWithoutUserInput()
    {
        ArchiveRule rule = ArchiveRule.Parse("Capture>0>png>2>1>");
        Assert.IsNotNull(rule);
        RunSta(() =>
        {
            ConstructorInfo constructor = typeof(RuleDialog).GetConstructor(Private, null, new[] { typeof(ArchiveRule) }, null);
            using (var dialog = (Form)constructor.Invoke(new object[] { rule }))
            {
                ArchiveRule current = (ArchiveRule)Call(dialog, "Current");
                Assert.AreEqual(2, current.Days, "QA39-06 : l'ouverture de l'éditeur remplace 2 jours par 7.");
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA37_01_SearchCompletionDoesNotRequestAMenuInHiddenSettings()
    {
        RunSta(() =>
        {
            using (var form = new SettingsForm(new SettingsActions()))
            using (var menu = new ContextMenuStrip())
            {
                bool requested = false;
                menu.Opening += (sender, args) => { requested = true; args.Cancel = true; };
                SetField(form, "placeMenu", menu);
                SetField(form, "placeSearch", 1);
                Assert.IsFalse(form.Visible);
                var results = new List<WeatherPlace> { new WeatherPlace { Name = "Mons", Lat = 50.45, Lon = 3.95 } };
                Call(form, "ShowPlaces", 1, results, null);
                Assert.IsFalse(requested, "QA37-01 : le résultat demande l'ouverture d'un menu alors que la fenêtre est cachée.");
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA37_03_FinishingThePanelCloseClearsTheChromeFocus()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm())
            {
                SetField(panel, "chromeFocus", 7);
                Call(panel, "Finish");
                Assert.AreEqual(-1, (int)typeof(PanelForm).GetField("chromeFocus", Private).GetValue(panel),
                    "QA37-03 : une nouvelle ouverture hérite du focus F6 de la session précédente.");
            }
        });
    }

    static ArchiveRule AddRule(RuleTarget target, string sub)
    {
        var rule = new ArchiveRule { Name = "Capture", Filter = RuleFilter.Extensions, Pattern = "png", Target = target, Sub = sub };
        Settings.ArchiveRules.Add(rule);
        return rule;
    }

    static string OldFile(string name)
    {
        string path = Path.Combine(Paths.Desktop, name);
        File.WriteAllText(path, "fichier de test");
        DateTime old = DateTime.UtcNow.AddHours(-3);
        File.SetCreationTimeUtc(path, old);
        File.SetLastWriteTimeUtc(path, old);
        return path;
    }

    static object Call(object target, string method, params object[] args)
    {
        return target.GetType().GetMethod(method, Private).Invoke(target, args);
    }

    static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, Private).SetValue(target, value);
    }

    static void RunSta(Action action)
    {
        Exception error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(10000), "Le scénario WinForms doit se terminer en dix secondes.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
