using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// L'application repose sur un état statique (L.CurrentLanguage, Settings…) : les tests ne tournent jamais en parallèle.
[assembly: DoNotParallelize]

namespace BureauCache.Tests
{
    /// <summary>
    /// Bac à sable de la suite (QA-32, QA-34) : réglages et bureau dans un dossier temporaire, et tout effet de bord
    /// réel (registre, menu Démarrer, installation, vrais réglages) lève une exception qui fait échouer le test.
    /// En fin de suite, une fenêtre restée ouverte fait échouer le lancement.
    /// </summary>
    [TestClass]
    public static class TestEnvironment
    {
        static string folder;

        [AssemblyInitialize]
        public static void Isolate(TestContext context)
        {
            folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BureauCache.Tests-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            string desktop = System.IO.Path.Combine(folder, "Bureau");
            System.IO.Directory.CreateDirectory(desktop);
            global::Settings.DirOverride = System.IO.Path.Combine(folder, "Reglages");
            global::Paths.DesktopOverride = desktop;
            global::Sandbox.Active = true;
            System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.ThrowException);
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            string leaked = string.Join(", ", System.Windows.Forms.Application.OpenForms.Cast<System.Windows.Forms.Form>()
                .Select(f => f.GetType().Name + " « " + f.Text + " » (détruite : " + f.IsDisposed + ", handle : " + f.IsHandleCreated + ")").ToArray());
            try { System.IO.Directory.Delete(folder, true); } catch { }
            if (leaked.Length > 0) Assert.Fail("Fenêtre(s) restée(s) ouverte(s) après la suite de tests : " + leaked);
        }
    }

    /// <summary>Le bac à sable est actif et bloque les effets de bord réels.</summary>
    [TestClass]
    public class SandboxTests
    {
        [TestMethod]
        public void RealSideEffectsAreBlocked()
        {
            Assert.IsTrue(global::Sandbox.Active);
            StringAssert.StartsWith(global::Paths.Desktop, System.IO.Path.GetTempPath());
            StringAssert.StartsWith(global::Settings.Dir, System.IO.Path.GetTempPath());
            Assert.ThrowsException<System.InvalidOperationException>(() => global::Startup.Set(true));
            Assert.ThrowsException<System.InvalidOperationException>(() => global::Installer.RegisterVersion());
            Assert.ThrowsException<System.InvalidOperationException>(() => global::Installer.Uninstall());
            string previous = global::Settings.DirOverride;
            try
            {
                global::Settings.DirOverride = null;
                Assert.ThrowsException<System.InvalidOperationException>(() => global::Settings.Save(), "Sauvegarde sans dossier isolé.");
            }
            finally { global::Settings.DirOverride = previous; }
        }
    }

    /// <summary>Fixe la langue de l'application le temps d'un test, puis la restaure.</summary>
    internal sealed class LanguageScope : System.IDisposable
    {
        private readonly int previous;

        private LanguageScope(int language)
        {
            previous = L.CurrentLanguage;
            L.CurrentLanguage = language;
        }

        public static LanguageScope French() { return new LanguageScope(L.French); }
        public static LanguageScope English() { return new LanguageScope(L.English); }

        public static LanguageScope Spanish() { return new LanguageScope(L.Spanish); }

        public void Dispose() { L.CurrentLanguage = previous; }
    }
}
