using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SettingsBackupReviewTests
{
    const string Original = @"C:\FakeDesktop\original.txt";
    const string Restored = @"C:\FakeDesktop\restored.txt";
    const string OldUndo = @"C:\FakeDesktop\old-undo.txt";
    string previousDirectory, directory;
    Dictionary<FieldInfo, object> settings;

    [TestInitialize]
    public void SetUp()
    {
        settings = typeof(Settings).GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(field => !field.IsLiteral && !field.IsInitOnly)
            .ToDictionary(field => field, field => field.GetValue(null));
        previousDirectory = Settings.DirOverride;
        directory = Path.Combine(Path.GetTempPath(), "BureauCache-BackupReview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Settings.DirOverride = directory;
        Settings.SaveBlocked = false;
        Settings.Favorites = new List<string>();
        Settings.Order = new List<string>();
        Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
        Settings.CatOverride = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Settings.CustomCats = new List<CustomCat>();
    }

    [TestCleanup]
    public void TearDown()
    {
        Settings.CancelPendingSave();
        foreach (var field in settings) field.Key.SetValue(null, field.Value);
        Settings.DirOverride = previousDirectory;
        Directory.Delete(directory, true);
    }

    [TestMethod]
    [TestCategory("Review")]
    public void D007_01_InvalidDailyCopyDoesNotSuppressTheFirstValidPrewriteSnapshot()
    {
        SaveOriginal();
        string today = Daily(DateTime.Today);
        File.WriteAllText(today, "copie incomplète");
        Settings.Favorites = new List<string> { Restored };
        Assert.IsTrue(Settings.Save(), "D007-01 : une sauvegarde automatique ne doit pas bloquer l'enregistrement.");
        Assert.IsTrue(SettingsBackup.IsValid(today), "D007-01 : un fichier quotidien invalide a empêché toute copie saine.");
        StringAssert.Contains(File.ReadAllText(today), Original);
    }

    [TestMethod]
    [TestCategory("Review")]
    public void D007_02_ClockRollbackDoesNotImmediatelyPruneTheNewSnapshot()
    {
        SaveOriginal();
        DateTime now = DateTime.Today;
        for (int i = 1; i <= SettingsBackup.Kept; i++) WriteIni(Daily(now.AddDays(i)), Restored);
        Settings.Favorites = new List<string> { Restored };
        Assert.IsTrue(Settings.Save());
        string today = Daily(now);
        Assert.IsTrue(File.Exists(today), "D007-02 : les dates futures ont évincé la copie créée aujourd'hui.");
        StringAssert.Contains(File.ReadAllText(today), Original);
    }

    [TestMethod]
    [TestCategory("Review")]
    public void D007_03_InvalidCopiesDoNotDisplaceValidCopiesDuringPruning()
    {
        Directory.CreateDirectory(SettingsBackup.Folder);
        DateTime now = DateTime.Today;
        for (int i = 1; i <= SettingsBackup.Kept; i++) WriteIni(Daily(now.AddDays(-i)), Original);
        File.WriteAllText(Daily(now), "copie incomplète");
        string foreign = Path.Combine(SettingsBackup.Folder, "settings-2026-02-31.ini");
        WriteIni(foreign, Restored);
        SettingsBackup.Prune();
        Assert.IsTrue(File.Exists(foreign), "Les dates mal formées sont étrangères à la rotation.");
        Assert.IsFalse(SettingsBackup.List().Any(entry => entry.Path == foreign));
        Assert.AreEqual(SettingsBackup.Kept, SettingsBackup.List().Count,
            "D007-03 : un fichier illisible a consommé une place et provoqué la suppression d'une copie lisible.");
    }

    [TestMethod]
    [TestCategory("Review")]
    public void D007_04_SameDaySaveRetriesPruningAfterAFileIsUnlocked()
    {
        SaveOriginal();
        DateTime now = DateTime.Today;
        for (int i = 1; i <= SettingsBackup.Kept; i++) WriteIni(Daily(now.AddDays(-i)), Original);
        string oldest = Daily(now.AddDays(-SettingsBackup.Kept));
        using (File.Open(oldest, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.IsTrue(Settings.Save());
            Assert.IsTrue(File.Exists(oldest), "Contrôle : le fichier verrouillé est encore présent.");
        }
        Assert.IsTrue(Settings.Save());
        Assert.AreEqual(SettingsBackup.Kept, SettingsBackup.List().Count,
            "D007-04 : l'enregistrement suivant n'a pas relancé la rotation après la levée du verrou.");
    }

    [TestMethod]
    [TestCategory("Review")]
    public void D007_05_SaveFailureMakesRestoreFailWithoutChangingMemory()
    {
        SaveOriginal();
        string source = Daily(DateTime.Today.AddDays(-1));
        WriteIni(source, Restored);
        bool success;
        using (File.Open(Settings.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            success = SettingsBackup.Restore(Find(source));
        StringAssert.Contains(File.ReadAllText(Settings.FilePath), Original);
        bool memoryIntact = Settings.Favorites.SequenceEqual(new[] { Original });
        Assert.IsTrue(!success && memoryIntact,
            "D007-05 : succès annoncé = " + success + ", réglages en mémoire intacts = " + memoryIntact + ".");
    }

    [TestMethod]
    [TestCategory("Review")]
    public void D007_05_UncommittedRestoreDoesNotReplaceTheExistingUndoCopy()
    {
        SaveOriginal();
        string source = Daily(DateTime.Today.AddDays(-1));
        WriteIni(source, Restored);
        WriteIni(UndoPath(), OldUndo);
        using (File.Open(Settings.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            SettingsBackup.Restore(Find(source));
        StringAssert.Contains(File.ReadAllText(UndoPath()), OldUndo,
            "D007-05 : l'échec a effacé le point de retour antérieur.");
    }

    [TestMethod]
    [TestCategory("Review")]
    public void D007_06_RestoringALegacySnapshotDoesNotKeepAbsentCollections()
    {
        SaveOriginal();
        string source = Daily(DateTime.Today.AddDays(-1));
        File.WriteAllText(source, "HideIcons=0\r\nScreenIdx=-1\r\n");
        Assert.IsTrue(SettingsBackup.Restore(Find(source)));
        Assert.AreEqual(0, Settings.Favorites.Count,
            "D007-06 : les favoris absents de l'ancienne sauvegarde ont été repris du profil courant.");
    }

    [TestMethod]
    [TestCategory("Review")]
    public void D007_07_RestoreCannotClaimSuccessWithoutKeepingThePreviousState()
    {
        SaveOriginal();
        string source = Daily(DateTime.Today.AddDays(-1));
        WriteIni(source, Restored);
        WriteIni(Daily(DateTime.Today), OldUndo);
        WriteIni(UndoPath(), OldUndo);
        bool success;
        using (File.Open(UndoPath(), FileMode.Open, FileAccess.Read, FileShare.Read))
            success = SettingsBackup.Restore(Find(source));
        if (success)
            StringAssert.Contains(File.ReadAllText(UndoPath()), Original,
                "D007-07 : une restauration réussie a laissé un point de retour qui désigne un autre état.");
        else
            CollectionAssert.AreEqual(new[] { Original }, Settings.Favorites);
    }

    [TestMethod]
    [TestCategory("Review")]
    public void D007_07_RestoringTheUndoSourceSwapsItWithTheCurrentState()
    {
        SaveOriginal();
        string undo = UndoPath();
        WriteIni(undo, Restored);
        Assert.IsTrue(SettingsBackup.Restore(Find(undo)), "La source « avant restauration » doit survivre au remplacement de son emplacement.");
        CollectionAssert.AreEqual(new[] { Restored }, Settings.Favorites);
        StringAssert.Contains(File.ReadAllText(undo), Original);
    }

    [TestMethod]
    [TestCategory("Review")]
    public void D007_09_NonIniExtensionsAreNeverListedOrPruned()
    {
        Directory.CreateDirectory(SettingsBackup.Folder);
        DateTime now = DateTime.Today;
        for (int i = 1; i <= SettingsBackup.Kept; i++) WriteIni(Daily(now.AddDays(-i)), Original);
        string foreign = Daily(now.AddDays(-SettingsBackup.Kept - 1)) + "x";
        WriteIni(foreign, Restored);
        bool listed = SettingsBackup.List().Any(entry => entry.Path == foreign);
        SettingsBackup.Prune();
        Assert.IsTrue(File.Exists(foreign), "D007-09 : la rotation a supprimé un fichier d'une autre extension.");
        Assert.IsFalse(listed, "D007-09 : un fichier d'une autre extension est proposé à la restauration.");
    }

    [TestMethod]
    [TestCategory("Review")]
    public void D007_08_EmptyBackupListDisablesRestore()
    {
        RunSta(() =>
        {
            using (var dialog = NewDialog())
            {
                var button = dialog.Controls.OfType<FlatButton>().Single(control => control.Primary);
                Assert.IsFalse(button.Enabled, "D007-08 : le bouton principal reste actif malgré la liste vide.");
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void D007_08_LongSpanishUndoEntryCanBeReadInTheList()
    {
        Directory.CreateDirectory(SettingsBackup.Folder);
        WriteIni(UndoPath(), Restored);
        RunSta(() =>
        {
            float scale = Ui.Scale;
            try
            {
                Ui.Scale = 1f;
                using (BureauCache.Tests.LanguageScope.Spanish())
                using (var dialog = NewDialog())
                {
                    var list = dialog.Controls.OfType<ListBox>().Single();
                    string entry = (string)list.Items[0];
                    int width = TextRenderer.MeasureText(entry, list.Font).Width;
                    Assert.IsTrue(list.HorizontalScrollbar || width <= list.ClientSize.Width,
                        "D007-08 : l'entrée mesure " + width + " px pour une liste de " + list.ClientSize.Width + " px.");
                }
            }
            finally { Ui.Scale = scale; }
        });
    }

    void SaveOriginal()
    {
        Settings.Favorites = new List<string> { Original };
        Assert.IsTrue(Settings.Save());
        Directory.CreateDirectory(SettingsBackup.Folder);
    }

    static string Daily(DateTime date)
    {
        return Path.Combine(SettingsBackup.Folder,
            "settings-" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".ini");
    }

    static string UndoPath()
    {
        return Path.Combine(SettingsBackup.Folder, "settings-avant-restauration.ini");
    }

    static void WriteIni(string path, string favorite)
    {
        File.WriteAllText(path, "HideIcons=0\r\nScreenIdx=-1\r\nFavorites=" + favorite + "\r\n");
    }

    static SettingsBackup.Entry Find(string path)
    {
        return SettingsBackup.List().Single(entry => entry.Path == path);
    }

    static BackupDialog NewDialog()
    {
        return (BackupDialog)Activator.CreateInstance(typeof(BackupDialog), true);
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
        Assert.IsTrue(thread.Join(10000));
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
