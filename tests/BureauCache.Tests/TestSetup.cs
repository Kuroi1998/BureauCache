using Microsoft.VisualStudio.TestTools.UnitTesting;

// L'application repose sur un état statique (L.CurrentLanguage, Settings…) : les tests ne tournent jamais en parallèle.
[assembly: DoNotParallelize]

namespace BureauCache.Tests
{
    /// <summary>
    /// Isole les réglages : toute sauvegarde des tests va dans un dossier temporaire, jamais dans
    /// %APPDATA%\BureauCache (un test avait écrasé les vrais réglages, QA-32).
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
            global::Settings.DirOverride = folder;
            System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.ThrowException);
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            try { System.IO.Directory.Delete(folder, true); } catch { }
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
