using Microsoft.VisualStudio.TestTools.UnitTesting;

// L'application repose sur un état statique (L.CurrentLanguage, Settings…) : les tests ne tournent jamais en parallèle.
[assembly: DoNotParallelize]

namespace BureauCache.Tests
{
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
