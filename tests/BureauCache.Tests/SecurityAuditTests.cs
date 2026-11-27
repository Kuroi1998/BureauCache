using System;
using System.IO;
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
