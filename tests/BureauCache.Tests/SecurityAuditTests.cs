using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class SecurityAuditTests
{
    [TestInitialize]
    public void RequireTestEnvironment()
    {
        Assert.IsTrue(Sandbox.Active);
        Assert.IsNotNull(Paths.DesktopOverride);
        Assert.IsNotNull(Settings.DirOverride);
        Sandbox.GuardPath(Paths.DesktopOverride, "test desktop");
        Sandbox.GuardPath(Settings.DirOverride, "test settings");
    }

    [TestMethod]
    public void SEC_01_RejectsReservedWindowsDeviceNames()
    {
        Assert.IsTrue(FileOps.IsValidName("rapport.txt"));
        string[] reservedNames = { "COM\u00B9", "LPT\u00B2.txt", "COM\u00B3.log" };
        foreach (string name in reservedNames)
            Assert.IsFalse(FileOps.IsValidName(name), "Reserved Windows name accepted: " + name);
    }

    [TestMethod]
    public void SEC_02_IndexFileUrlRequiresDriveRoot()
    {
        string absolute = Path.Combine(Paths.DesktopOverride, "rapport.txt");
        Assert.AreEqual(absolute, ContentSearch.PathFromUrl("file:" + absolute.Replace('\\', '/')));

        string drive = Path.GetPathRoot(absolute).Substring(0, 2);
        Assert.IsNull(ContentSearch.PathFromUrl("file:" + drive + "rapport.txt"),
            "An index URL must identify a file independently of the working directory.");
    }

    [TestMethod]
    public void IndexAcceptsOnlyOrdinaryDriveFilePaths()
    {
        Assert.AreEqual(@"C:\Bureau\100%.txt", ContentSearch.PathFromUrl("file:///C:/Bureau/100%25.txt"));
        Assert.IsNull(ContentSearch.PathFromUrl("file:C:/Bureau/rapport.txt%3Aannexe"));
        Assert.IsNull(ContentSearch.PathFromUrl("file:C:/Bureau/rapport%3F.txt"));
        Assert.IsNull(ContentSearch.PathFromUrl("file:%C3%89:/Bureau/rapport.txt"));
    }

    [TestMethod]
    public void WindowsSettingsEntriesUseSystemPaths()
    {
        var entries = SystemCatalog.WindowsSettings(16);
        var controlPanel = entries.Single(entry => entry.Key == "set:control.exe");
        var variables = entries.Single(entry => entry.Key.StartsWith("set:rundll32.exe", StringComparison.Ordinal));
        Assert.AreEqual(Path.Combine(Environment.SystemDirectory, "control.exe"), controlPanel.Target);
        Assert.AreEqual(Path.Combine(Environment.SystemDirectory, "rundll32.exe"), variables.Target);
        Assert.AreEqual("\"" + Path.Combine(Environment.SystemDirectory, "sysdm.cpl") + "\",EditEnvironmentVariables", variables.Args);
    }

    [TestMethod]
    public void DeferredUninstallKeepsProfilePathOutOfTheCommandText()
    {
        string directory = Path.Combine(Paths.DesktopOverride, "Bureau 100% & projet!");
        var command = Installer.FolderRemovalCommand(directory);
        var other = Installer.FolderRemovalCommand(Paths.DesktopOverride);
        Assert.AreEqual(other.Arguments, command.Arguments);
        Assert.AreEqual(directory, command.EnvironmentVariables["BUREAUCACHE_REMOVE_DIR"]);
        Assert.AreEqual(Path.Combine(Environment.SystemDirectory, "ping.exe"), command.EnvironmentVariables["BUREAUCACHE_REMOVE_PING"]);
        StringAssert.Contains(command.Arguments, "/d /e:on /v:on");
        Assert.AreEqual(Path.Combine(Environment.SystemDirectory, "cmd.exe"), command.FileName);
        Assert.IsFalse(command.UseShellExecute);
    }

    [TestMethod]
    public void SEC_03_CalculatorDetectionRespectsEvaluationLimit()
    {
        string validExpression = new string('1', 198) + "+1";
        string oversizedExpression = new string('1', 199) + "+1";
        double value;

        Assert.IsTrue(Calc.LooksLikeMath(validExpression));
        Assert.IsFalse(Calc.TryEval(oversizedExpression, out value));
        Assert.IsFalse(Calc.LooksLikeMath(oversizedExpression),
            "The detector must apply the same length limit as the evaluator.");
    }
}
