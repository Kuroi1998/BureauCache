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
static class Log { internal static void Warn(string contexte, Exception ex) {} }
public static class Settings { public static string Dir; }
public static class Native {
    public const int ABS_AUTOHIDE=1, ABS_ALWAYSONTOP=2, SW_HIDE=0, SW_SHOWNA=8;
    public static int State;
    public static bool Visible, IgnoreSet, IgnoreShow, Absent;
    public static IntPtr WindowsTaskbar() { return Absent ? IntPtr.Zero : new IntPtr(1); }
    public static int GetTaskbarState() { return State; }
    public static void SetTaskbarState(int value) { if (!IgnoreSet) State=value; }
    public static bool IsWindowVisible(IntPtr h) { return Visible; }
    public static void ShowWindow(IntPtr h,int cmd) { if (!IgnoreShow) Visible=cmd!=SW_HIDE; }
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

    // T-052 : RestoreOk dit à l'avertissement de retour arrière si la barre est revenue (Explorer absent = échec)
    [TestMethod]
    public void Bar_RestoreOkReportsAMissingTaskbar()
    {
        Assert.IsTrue(Hide());
        Set("Absent", true);
        Restore();
        Assert.IsFalse((bool)bar.GetProperty("RestoreOk").GetValue(null));
        Set("Absent", false);
        Restore();
        Assert.IsTrue((bool)bar.GetProperty("RestoreOk").GetValue(null));
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
    public void FailedHideRestoresOriginalModeBeforeReturning()
    {
        Set("IgnoreShow", true);
        Assert.IsFalse(Hide());
        Assert.AreEqual(0, State);
        Assert.IsFalse(File.Exists(Journal));
    }

    [TestMethod]
    public void RestoreRetriesIfExplorerRemainsHidden()
    {
        Assert.IsTrue(Hide());
        Set("IgnoreShow", true);
        Assert.IsFalse((bool)bar.GetMethod("Restore").Invoke(null, null));
        Assert.IsTrue(File.Exists(Journal));
        Set("IgnoreShow", false);
        Restore();
        Assert.IsFalse(File.Exists(Journal));
    }

    [TestMethod]
    public void ReapplyDoesNotHideExplorerWhenRecoveryJournalCannotBeRecreated()
    {
        Assert.IsTrue(Hide());
        File.Delete(Journal);
        Directory.CreateDirectory(Journal);
        Set("Visible", true);
        Set("State", 0);
        bar.GetMethod("Reapply").Invoke(null, null);
        Assert.AreEqual(0, State);
        Assert.IsTrue((bool)native.GetField("Visible").GetValue(null));
    }

    [TestMethod]
    public void ReapplyRefusesToHideWithACorruptJournal()
    {
        Assert.IsTrue(Hide());
        File.WriteAllText(Journal, "incomplete");
        Set("Visible", true);
        Set("State", 0);
        object result = bar.GetMethod("Reapply").Invoke(null, null);
        Assert.AreEqual(0, State);
        Assert.IsTrue((bool)native.GetField("Visible").GetValue(null));
        Assert.AreEqual(false, result);
    }

    [TestMethod]
    public void SecondHideDoesNotChangeExplorerWhenRecoveryJournalCannotBeRecreated()
    {
        Assert.IsTrue(Hide());
        File.Delete(Journal);
        Directory.CreateDirectory(Journal);
        Set("Visible", true);
        Set("State", 0);
        Assert.IsFalse(Hide());
        Assert.AreEqual(0, State);
        Assert.IsTrue((bool)native.GetField("Visible").GetValue(null));
    }

    [TestMethod]
    public void Bar06_UnwritableNewJournalLeavesShellAlone()
    {
        Directory.CreateDirectory(Journal);
        Assert.IsFalse(Hide());
        Assert.AreEqual(0, State);
        Assert.IsTrue((bool)native.GetField("Visible").GetValue(null));
    }

    // Revue Codex (QA-67) : un ancien fichier d'état remis en place pendant le masquage est refusé tout de suite
    [TestMethod]
    public void CDX02_ReapplyRejectsWrongJournalWhileExplorerIsStillHidden()
    {
        Assert.IsTrue(Hide());
        Assert.AreEqual(1, State & 1);
        Assert.IsFalse((bool)native.GetField("Visible").GetValue(null));

        File.WriteAllText(Journal, "BureauCache barre 1\r\netat=1\r\n");

        Assert.IsFalse((bool)bar.GetMethod("Reapply").Invoke(null, null));
        Restore();
        Assert.AreEqual(0, State & 1, "La copie en mémoire de l'état d'origine l'emporte.");
        Assert.IsTrue((bool)native.GetField("Visible").GetValue(null));
    }
}
