using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TaskbarRecoveryReviewTests
{
    // Recompile the production state machine against an in-memory Explorer.
    // This assembly has no P/Invoke and cannot call the application's Native class.
    const string Doubles = @"
using System;
static class Sandbox { public const bool Active = false; public static void Guard(string operation) {} }
public static class Settings { public static string Dir; }
public static class Native {
    public const int ABS_AUTOHIDE=1, ABS_ALWAYSONTOP=2, SW_HIDE=0, SW_SHOWNA=8;
    public static int State;
    public static bool Visible, IgnoreSet;
    public static IntPtr WindowsTaskbar() { return new IntPtr(1); }
    public static int GetTaskbarState() { return State; }
    public static void SetTaskbarState(int value) { if (!IgnoreSet) State=value; }
    public static bool IsWindowVisible(IntPtr h) { return Visible; }
    public static void ShowWindow(IntPtr h,int cmd) { Visible=cmd!=SW_HIDE; }
}";
    Type bar, native;
    string dir;
    string Journal { get { return Path.Combine(dir, "barre.etat"); } }

    [TestInitialize]
    public void CreateIsolatedSession()
    {
        Assert.IsTrue(Sandbox.Active);
        dir = Path.Combine(Path.GetTempPath(), "bc-bar-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string root = AppDomain.CurrentDomain.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "src", "Taskbar", "WindowsBar.cs")))
            root = Path.GetDirectoryName(root);
        Assert.IsNotNull(root, "Run from the repository test output.");
        using (CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp"))
        {
            CompilerParameters options = new CompilerParameters();
            options.GenerateInMemory = true;
            options.TempFiles = new TempFileCollection(dir, false);
            CompilerResults result = provider.CompileAssemblyFromSource(options,
                File.ReadAllText(Path.Combine(root, "src", "Taskbar", "WindowsBar.cs")), Doubles);
            Assert.IsFalse(result.Errors.HasErrors, string.Join("\n", Array.ConvertAll(
                System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Cast<CompilerError>(result.Errors)), e => e.ToString())));
            Assembly model = result.CompiledAssembly;
            model.GetType("Settings").GetField("Dir").SetValue(null, dir);
            native = model.GetType("Native");
            bar = model.GetType("WindowsBar");
        }
        Set("Visible", true);
    }

    [TestCleanup]
    public void RemoveSession() { if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, true); }
    void Set(string field, object value) { native.GetField(field).SetValue(null, value); }
    int State { get { return (int)native.GetField("State").GetValue(null); } }
    bool Hide() { return (bool)bar.GetMethod("Hide").Invoke(null, null); }
    void Restore() { bar.GetMethod("Restore").Invoke(null, null); }

    [TestMethod]
    public void Bar01_MissingJournalWhileRunningRestoresOriginalMode()
    {
        Assert.IsTrue(Hide());
        File.Delete(Journal);
        Restore();
        Assert.AreEqual(0, State & 1);
    }

    [TestMethod]
    public void Bar02_CorruptJournalPreventsFurtherHiding()
    {
        File.WriteAllText(Journal, "incomplete");
        Assert.IsFalse(Hide());
        Assert.AreEqual(0, State & 1);
    }

    [TestMethod]
    public void Bar03_UnconfirmedRestoreKeepsJournal()
    {
        Assert.IsTrue(Hide());
        Set("IgnoreSet", true);
        Restore();
        Assert.IsTrue(File.Exists(Journal));
    }

    // BAR-04 écarté (QA-55) : garder une barre cachée par un état antérieur inconnu (notre propre arrêt brutal sans
    // fichier, par exemple) contredirait l'exigence n° 1. La restauration réaffiche toujours la barre.
    [TestMethod]
    public void Bar04_RestoreAlwaysShowsTheWindowsBar()
    {
        Set("Visible", false);
        Assert.IsTrue(Hide());
        Restore();
        Assert.IsTrue((bool)native.GetField("Visible").GetValue(null));
    }

    // Complément de BAR-03 : une fois la restauration constatée, le fichier disparaît et l'état d'origine est rendu
    [TestMethod]
    public void Bar03b_ConfirmedRestoreRemovesJournalAfterRetry()
    {
        Assert.IsTrue(Hide());
        Set("IgnoreSet", true);
        Assert.IsFalse((bool)bar.GetMethod("Restore").Invoke(null, null));
        Set("IgnoreSet", false);
        Assert.IsTrue((bool)bar.GetMethod("Restore").Invoke(null, null));
        Assert.IsFalse(File.Exists(Journal));
        Assert.AreEqual(0, State & 1);
    }

    // Complément de BAR-02 : la restauration solde un fichier abîmé (barre visible), et le masquage redevient possible
    [TestMethod]
    public void Bar02b_RestoreClearsCorruptJournalThenHideWorks()
    {
        File.WriteAllText(Journal, "incomplete");
        Set("State", 1);
        Assert.IsTrue((bool)bar.GetMethod("Restore").Invoke(null, null));
        Assert.AreEqual(0, State & 1, "Fichier abîmé : barre visible, sans masquage automatique.");
        Assert.IsFalse(File.Exists(Journal));
        Assert.IsTrue(Hide());
    }

    [TestMethod]
    public void Bar05_SecondHideCannotReplaceOriginalAfterJournalLoss()
    {
        Assert.IsTrue(Hide());
        File.Delete(Journal);
        Hide();
        Restore();
        Assert.AreEqual(0, State & 1);
    }

    [TestMethod]
    public void Bar06_UnwritableNewJournalLeavesShellAlone()
    {
        Directory.CreateDirectory(Journal);
        Assert.IsFalse(Hide());
        Assert.AreEqual(0, State);
        Assert.IsTrue((bool)native.GetField("Visible").GetValue(null));
    }
}
