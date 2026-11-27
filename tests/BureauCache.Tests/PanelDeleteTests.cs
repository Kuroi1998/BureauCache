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

    // Même disposition que PanelForm.Delete : marqueur d'origine à la racine, élément dans « contenu »
    string SetAside(string name, bool folder = false)
    {
        string original = Path.Combine(desk, name);
        File.WriteAllText(Path.Combine(stash, PanelForm.OriginFile), original);
        string held = PanelForm.HeldPath(stash, original);
        Directory.CreateDirectory(Path.GetDirectoryName(held));
        if (folder) { Directory.CreateDirectory(held); File.WriteAllText(Path.Combine(held, "a.txt"), "a"); }
        else File.WriteAllText(held, "contenu");
        return original;
    }

    [TestMethod]
    public void ItemIsRecycledFromItsOriginalPlace()
    {
        string original = SetAside("rapport.docx");
        PanelForm.FinishDelete(stash, Move, Recycle);
        CollectionAssert.AreEqual(new[] { original }, recycled, "« Restaurer » dans la Corbeille le remettra sur le bureau.");
        Assert.IsFalse(Directory.Exists(stash), "Le dossier de mise de côté disparaît.");
    }

    [TestMethod]
    public void FolderIsRecycledFromItsOriginalPlaceToo()
    {
        string original = SetAside("Projet", true);
        PanelForm.FinishDelete(stash, Move, Recycle);
        CollectionAssert.AreEqual(new[] { original }, recycled);
    }

    [TestMethod]
    public void TakenPlaceFallsBackToTheStash()
    {
        string original = SetAside("note.txt");
        File.WriteAllText(original, "nouveau fichier de l'utilisateur");
        PanelForm.FinishDelete(stash, Move, Recycle);
        CollectionAssert.AreEqual(new[] { PanelForm.HeldPath(stash, original) }, recycled, "Le nouveau fichier du bureau n'est jamais touché.");
        Assert.AreEqual("nouveau fichier de l'utilisateur", File.ReadAllText(original));
    }

    [TestMethod]
    public void StashFromAnOlderVersionWithoutOriginStillEmpties()
    {
        string old = Path.Combine(stash, "ancien.txt");
        File.WriteAllText(old, "a");
        PanelForm.FinishDelete(stash, Move, Recycle);
        CollectionAssert.AreEqual(new[] { old }, recycled);
        Assert.IsFalse(Directory.Exists(stash));
    }

    [TestMethod]
    public void IfTheRecycleBinFailsTheItemStaysVisibleOnTheDesktop()
    {
        string original = SetAside("photo.png");
        Assert.ThrowsException<IOException>(() => PanelForm.FinishDelete(stash, Move, p => { throw new IOException("corbeille pleine"); }));
        Assert.IsTrue(File.Exists(original), "Rien n'est perdu : l'élément est revenu sur le bureau.");
    }

    [TestMethod]
    public void OriginMarkerIsNeverRecycled()
    {
        SetAside("doc.pdf");
        PanelForm.FinishDelete(stash, Move, Recycle);
        Assert.AreEqual(1, recycled.Count);
        Assert.IsFalse(recycled[0].StartsWith(Path.Combine(stash, PanelForm.OriginFile), StringComparison.OrdinalIgnoreCase));
    }

    // CDX-20 : un élément de l'utilisateur qui porte le nom du marqueur (ancien ou actuel) part à la corbeille comme
    // les autres ; il n'est jamais effacé définitivement
    [TestMethod]
    public void CDX20_UserItemNamedLikeTheMarkerGoesToTheRecycleBin()
    {
        foreach (string name in new[] { PanelForm.OriginFile, "bureaucache-origine.txt" })
            foreach (bool folder in new[] { false, true })
            {
                Create();
                string original = SetAside(name, folder);
                PanelForm.FinishDelete(stash, Move, Recycle);
                CollectionAssert.AreEqual(new[] { original }, recycled, name + (folder ? " (dossier)" : ""));
                Remove();
            }
    }
}
