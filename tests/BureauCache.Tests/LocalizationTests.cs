using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    [TestClass]
    public class LocalizationTests
    {
        [DataTestMethod]
        [DataRow(L.SystemLanguage, "fr-BE", L.French)]
        [DataRow(L.SystemLanguage, "es-MX", L.Spanish)]
        [DataRow(L.SystemLanguage, "nl-BE", L.English)]
        [DataRow(L.French, "es-ES", L.French)]
        [DataRow(L.English, "fr-BE", L.English)]
        [DataRow(L.Spanish, "en-US", L.Spanish)]
        public void ResolvesExplicitAndSystemLanguage(int setting, string system, int expected)
        {
            Assert.AreEqual(expected, L.ResolveLanguage(setting, CultureInfo.GetCultureInfo(system)));
        }

        [TestMethod]
        public void SpanishUIUsesSpanishTextAndEnglishFallback()
        {
            using (LanguageScope.Spanish())
            {
                Assert.AreEqual("es-ES", L.Culture.Name);
                Assert.AreEqual("¿Configuración?", L.T("Réglages", "Settings", "¿Configuración?"));
                Assert.AreEqual("Settings", L.T("Réglages", "Settings"));
                Assert.AreEqual("Juegos", L.Cat("Jeux"));
                Assert.AreEqual("1,5 KB", L.Size(1536));
                Assert.IsTrue(SystemCatalog.WindowsSettings(16).Exists(i => i.Name == "Energía y batería"));
            }
        }

        [TestMethod]
        public void SpanishCalculationCanReadBackItsGroupedResult()
        {
            using (LanguageScope.Spanish())
            {
                string result = Calc.Format(1234.56);
                Assert.AreEqual("1\u00A0234,56", result);
                double value;
                Assert.IsTrue(Calc.TryEval(result, out value));
                Assert.AreEqual(1234.56, value, 0.00001);
            }
        }

        [DataTestMethod]
        [DataRow("1.234,56 + 0,44", 1235.0)]
        [DataRow("1.234.567 + 1", 1234568.0)]
        [DataRow("1.234 + 1", 2.234)]
        [DataRow("1,234.56 + 0.44", 1235.0)]
        public void SpanishCalculationAcceptsPastedNumbers(string calculation, double expected)
        {
            using (LanguageScope.Spanish())
            {
                double value;
                Assert.IsTrue(Calc.TryEval(calculation, out value));
                Assert.AreEqual(expected, value, 0.00001);
                Assert.IsFalse(Calc.TryEval("12.34,56", out value));
            }
        }

        [TestMethod]
        public void SettingsRoundTripKeepsSpanishAndCaptureChoice()
        {
            string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ini");
            bool originalCapture = Settings.HidePanelInCapture;
            int originalLanguage = Settings.Language;
            var serialize = typeof(Settings).GetMethod("Serialize", BindingFlags.NonPublic | BindingFlags.Static);
            var originalSettings = (string[])serialize.Invoke(null, null);
            try
            {
                Settings.Language = L.Spanish;
                Settings.HidePanelInCapture = true;
                File.WriteAllLines(file, (string[])serialize.Invoke(null, null));
                Settings.Language = L.French;
                Settings.HidePanelInCapture = false;

                Assert.IsTrue(Settings.Load(file));
                Assert.AreEqual(L.Spanish, Settings.Language);
                Assert.IsTrue(Settings.HidePanelInCapture);

                File.WriteAllLines(file, new[] { "HideIcons=0", "Language=2", "LiveBlur=1" });
                Assert.IsTrue(Settings.Load(file));
                Assert.AreEqual(L.English, Settings.Language);
                Assert.IsFalse(Settings.HidePanelInCapture);
            }
            finally
            {
                File.WriteAllLines(file, originalSettings);
                Settings.Load(file);
                Settings.Language = originalLanguage;
                Settings.HidePanelInCapture = originalCapture;
                File.Delete(file);
            }
        }
    }
}
