using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-052 : l'avertissement de retour arrière sait si la barre Windows a été rétablie.
[TestClass]
public class RollbackMessageTests
{
    [TestMethod]
    public void RestaurationReussieEstMemorisee()
    {
        // Sous bac à sable, Restore n'a rien à rétablir : le résultat doit être mémorisé comme un succès
        Assert.IsTrue(WindowsBar.Restore());
        Assert.IsTrue(WindowsBar.RestoreOk);
    }
}
