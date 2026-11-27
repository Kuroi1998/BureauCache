using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// QA-70 : revue Codex depuis la 3.10.2 (CDX-40, CDX-41)
[TestClass]
public class QA70ReviewTests
{
    // CDX-40 : un outil système est lancé depuis System32, jamais un homonyme du dossier courant
    [TestMethod]
    public void Cdx40_SystemToolsUseTheirFullPath()
    {
        foreach (string name in new[] { "taskmgr.exe", "control.exe", "shutdown.exe", "inexistant-bc.exe" })
            Assert.AreEqual(Path.Combine(Environment.SystemDirectory, name), ShellUtil.SystemPath(name));
        Assert.AreEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), ShellUtil.SystemPath("explorer.exe"));
        Assert.AreEqual(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"), ShellUtil.SystemPath("powershell.exe"));
        Assert.AreEqual("ms-settings:display", ShellUtil.SystemPath("ms-settings:display"));
        Assert.AreEqual(@"C:\Dossier\outil.exe", ShellUtil.SystemPath(@"C:\Dossier\outil.exe"));
    }

    // CDX-41 : une restauration refusée ne fait pas perdre la valeur d'origine de TaskbandHWND
    [TestMethod]
    public void Cdx41_FailedRestoreCannotOverwriteTheOriginalRoute()
    {
        const string doubles = @"
using System;
static class Sandbox {
    public const bool Active = false;
    public static void Guard(string operation) {}
}
public static class Settings { public static string Dir; }
public static class Native {
    public static IntPtr Value = new IntPtr(2);
    public static bool RefuseNextSet;
    public static IntPtr WindowsTaskbar() { return new IntPtr(1); }
    public static bool IsWindow(IntPtr h) { return h != IntPtr.Zero; }
    public static IntPtr GetProp(IntPtr h, string name) { return Value; }
    public static bool SetProp(IntPtr h, string name, IntPtr value) {
        if (RefuseNextSet) { RefuseNextSet = false; return false; }
        Value = value; return true;
    }
    public static IntPtr RemoveProp(IntPtr h, string name) { IntPtr old = Value; Value = IntPtr.Zero; return old; }
}";
        const string source = "src/Taskbar/TaskbandRoute.cs";
        var root = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, source))) root = root.Parent;
        Assert.IsNotNull(root);
        string dir = Path.Combine(Path.GetTempPath(), "bc-qa70-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using (var provider = CodeDomProvider.CreateProvider("CSharp"))
            {
                var options = new CompilerParameters { GenerateInMemory = true, TempFiles = new TempFileCollection(dir, false) };
                var result = provider.CompileAssemblyFromSource(options, File.ReadAllText(Path.Combine(root.FullName, source)), doubles);
                Assert.IsFalse(result.Errors.HasErrors, string.Join("\n", result.Errors.Cast<CompilerError>()));
                var asm = result.CompiledAssembly;
                asm.GetType("Settings").GetField("Dir").SetValue(null, dir);
                var route = asm.GetType("TaskbandRoute");
                var native = asm.GetType("Native");
                var install = route.GetMethod("Install");
                Assert.AreEqual(true, install.Invoke(null, new object[] { new IntPtr(3) }));
                string journal = Path.Combine(dir, "badges.etat");
                string original = File.ReadAllText(journal);

                native.GetField("RefuseNextSet").SetValue(null, true);   // la barre est recréée, Explorer refuse une fois
                Assert.AreEqual(false, install.Invoke(null, new object[] { new IntPtr(4) }));
                Assert.AreEqual(original, File.ReadAllText(journal), "Le journal garde la valeur d'Explorer.");
                Assert.AreEqual(new IntPtr(3), native.GetField("Value").GetValue(null));
                Assert.AreEqual(true, route.GetMethod("Restore").Invoke(null, null));
                Assert.AreEqual(new IntPtr(2), native.GetField("Value").GetValue(null), "La valeur d'origine revient.");
                Assert.IsFalse(File.Exists(journal));
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
