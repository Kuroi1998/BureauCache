using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// QA-68 : une suppression annulable doit envoyer l'élément à la corbeille depuis le bureau, pour que « Restaurer »
// le remette au bon endroit. Tout se passe dans un dossier temporaire ; la vraie corbeille n'est jamais touchée.
[TestClass]
public class PanelDeleteTests
{
    string root, desk, stash;
    readonly List<string> recycled = new List<string>();

    [TestInitialize]
    public void Create()
    {
        root = Path.Combine(Path.GetTempPath(), "BureauCache.Delete-" + Guid.NewGuid().ToString("N"));
        desk = Path.Combine(root, "Bureau");
        stash = Path.Combine(root, "suppression", "lot");
        Directory.CreateDirectory(desk);
        Directory.CreateDirectory(stash);
        recycled.Clear();
    }

    [TestCleanup]
    public void Remove() { try { Directory.Delete(root, true); } catch { } }

    static void Move(string from, string to, bool folder) { if (folder) Directory.Move(from, to); else File.Move(from, to); }

    void Recycle(string p)
    {
        recycled.Add(p);
        if (Directory.Exists(p)) Directory.Delete(p, true); else File.Delete(p);
    }

    string SetAside(string name, bool writeOrigin)
    {
        string original = Path.Combine(desk, name);
        if (writeOrigin) File.WriteAllText(Path.Combine(stash, PanelForm.OriginFile), original);
        File.WriteAllText(Path.Combine(stash, name), "contenu");
        return original;
    }

    [TestMethod]
    public void ItemIsRecycledFromItsOriginalPlace()
    {
        string original = SetAside("rapport.docx", true);
        PanelForm.FinishDelete(stash, Move, Recycle);
        CollectionAssert.AreEqual(new[] { original }, recycled, "« Restaurer » dans la Corbeille le remettra sur le bureau.");
        Assert.IsFalse(Directory.Exists(stash), "Le dossier de mise de côté disparaît.");
    }

    [TestMethod]
    public void FolderIsRecycledFromItsOriginalPlaceToo()
    {
        string original = Path.Combine(desk, "Projet");
        File.WriteAllText(Path.Combine(stash, PanelForm.OriginFile), original);
        Directory.CreateDirectory(Path.Combine(stash, "Projet"));
        File.WriteAllText(Path.Combine(stash, "Projet", "a.txt"), "a");
        PanelForm.FinishDelete(stash, Move, Recycle);
        CollectionAssert.AreEqual(new[] { original }, recycled);
    }

    [TestMethod]
    public void TakenPlaceFallsBackToTheStash()
    {
        string original = SetAside("note.txt", true);
        File.WriteAllText(original, "nouveau fichier de l'utilisateur");
        PanelForm.FinishDelete(stash, Move, Recycle);
        CollectionAssert.AreEqual(new[] { Path.Combine(stash, "note.txt") }, recycled, "Le nouveau fichier du bureau n'est jamais touché.");
        Assert.AreEqual("nouveau fichier de l'utilisateur", File.ReadAllText(original));
    }

    [TestMethod]
    public void StashFromAnOlderVersionWithoutOriginStillEmpties()
    {
        SetAside("ancien.txt", false);
        PanelForm.FinishDelete(stash, Move, Recycle);
        CollectionAssert.AreEqual(new[] { Path.Combine(stash, "ancien.txt") }, recycled);
        Assert.IsFalse(Directory.Exists(stash));
    }

    [TestMethod]
    public void IfTheRecycleBinFailsTheItemStaysVisibleOnTheDesktop()
    {
        string original = SetAside("photo.png", true);
        Assert.ThrowsException<IOException>(() => PanelForm.FinishDelete(stash, Move, p => { throw new IOException("corbeille pleine"); }));
        Assert.IsTrue(File.Exists(original), "Rien n'est perdu : l'élément est revenu sur le bureau.");
    }

    [TestMethod]
    public void OriginFileIsNeverRecycled()
    {
        SetAside("doc.pdf", true);
        PanelForm.FinishDelete(stash, Move, Recycle);
        Assert.IsFalse(recycled.Exists(p => p.EndsWith(PanelForm.OriginFile, StringComparison.OrdinalIgnoreCase)));
    }
}
