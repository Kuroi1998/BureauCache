using Microsoft.VisualStudio.TestTools.UnitTesting;

// L'application repose sur un état statique (L.En, Settings…) : les tests ne tournent jamais en parallèle.
[assembly: DoNotParallelize]

namespace BureauCache.Tests
{
    /// <summary>Fixe la langue de l'application le temps d'un test, puis la restaure.</summary>
    internal sealed class LanguageScope : System.IDisposable
    {
        private readonly bool previous;

        private LanguageScope(bool english)
        {
            previous = L.En;
            L.En = english;
        }

        public static LanguageScope French() { return new LanguageScope(false); }
        public static LanguageScope English() { return new LanguageScope(true); }

        public void Dispose() { L.En = previous; }
    }
}
