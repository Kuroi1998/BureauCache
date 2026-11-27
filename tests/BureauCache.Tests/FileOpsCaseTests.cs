using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// QA-68 : renommer un élément du bureau en changeant seulement la casse (« projet » → « Projet »)
[TestClass]
public class FileOpsCaseTests
{
    string root;

    [TestInitialize]
    public void Create()
    {
        root = Path.Combine(Path.GetTempPath(), "BureauCache.Case-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void Remove() { try { Directory.Delete(root, true); } catch { } }

    string RealName(string path)
    {
        return Directory.GetFileSystemEntries(Path.GetDirectoryName(path)).Select(Path.GetFileName)
            .Single(n => string.Equals(n, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void FolderCanChangeOnlyItsCase()
    {
        string from = Path.Combine(root, "projet"), to = Path.Combine(root, "Projet");
        Directory.CreateDirectory(from);
        File.WriteAllText(Path.Combine(from, "a.txt"), "a");
        FileOps.Move(from, to, true);
        Assert.AreEqual("Projet", RealName(to));
        Assert.AreEqual("a", File.ReadAllText(Path.Combine(to, "a.txt")), "Le contenu suit.");
    }

    [TestMethod]
    public void FileCanChangeOnlyItsCase()
    {
        string from = Path.Combine(root, "note.txt"), to = Path.Combine(root, "Note.txt");
        File.WriteAllText(from, "n");
        FileOps.Move(from, to, false);
        Assert.AreEqual("Note.txt", RealName(to));
    }

    [TestMethod]
    public void RenamingOntoAnotherExistingFolderStillFails()
    {
        string a = Path.Combine(root, "a"), b = Path.Combine(root, "b");
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);
        Assert.ThrowsException<IOException>(() => FileOps.Move(a, b, true));
        Assert.IsTrue(Directory.Exists(a) && Directory.Exists(b));
    }
}
