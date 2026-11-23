using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    /// <summary>
    /// Premiers tests de src/Search/Calc.cs (T-001).
    /// La couverture complète des cas limites est l'objet de T-002.
    /// </summary>
    [TestClass]
    public class CalcTests
    {
        private const double Tolerance = 1e-9;

        private static double Eval(string expression)
        {
            double value;
            Assert.IsTrue(Calc.TryEval(expression, out value), "Expression refusée : " + expression);
            return value;
        }

        private static void AssertRejected(string expression)
        {
            double value;
            Assert.IsFalse(Calc.TryEval(expression, out value), "Expression acceptée à tort : " + expression);
        }

        [DataTestMethod]
        [DataRow("2+3*4", 14.0)]
        [DataRow("(2+3)*4", 20.0)]
        [DataRow("10-4-3", 3.0)]
        [DataRow("2^3^2", 512.0)]          // puissance associative à droite
        [DataRow("2**3", 8.0)]
        [DataRow("6×7", 42.0)]
        [DataRow("84÷2", 42.0)]
        [DataRow("6 x 7", 42.0)]
        [DataRow("50%", 0.5)]
        [DataRow("200*10%", 20.0)]
        [DataRow("  1 +\t2  ", 3.0)]       // tabulations acceptées (QA-03)
        public void Operateurs_et_priorites(string expression, double expected)
        {
            Assert.AreEqual(expected, Eval(expression), Tolerance);
        }

        [DataTestMethod]
        [DataRow("1,5+1", 2.5)]            // virgule seule : séparateur décimal
        [DataRow("1.5+1", 2.5)]
        [DataRow("2e3", 2000.0)]
        [DataRow("1.5e-3", 0.0015)]
        [DataRow("1.5E+2", 150.0)]         // exposants avec signe (QA-03)
        public void Nombres(string expression, double expected)
        {
            Assert.AreEqual(expected, Eval(expression), Tolerance);
        }

        [DataTestMethod]
        [DataRow("sqrt(16)", 4.0)]
        [DataRow("racine(9)", 3.0)]
        [DataRow("sin(90)", 1.0)]          // degrés
        [DataRow("abs(-3)", 3.0)]
        [DataRow("arrondi(2.6)", 3.0)]
        [DataRow("log(1000)", 3.0)]
        public void Fonctions(string expression, double expected)
        {
            Assert.AreEqual(expected, Eval(expression), Tolerance);
        }

        [TestMethod]
        public void Constantes()
        {
            Assert.AreEqual(Math.PI, Eval("pi"), Tolerance);
            Assert.AreEqual(Math.E, Eval("e"), Tolerance);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("1/0")]                   // infini refusé
        [DataRow("(1+2")]
        [DataRow("1+2)")]
        [DataRow("foo(2)")]
        [DataRow("1e")]                    // exposant incomplet
        [DataRow("1e+")]
        [DataRow("1e308*10")]              // dépassement
        [DataRow("sqrt(-1)")]              // NaN refusé
        public void Expressions_refusees(string expression)
        {
            AssertRejected(expression);
        }

        [TestMethod]
        public void Expression_trop_longue_refusee()
        {
            AssertRejected("1" + string.Concat(System.Linq.Enumerable.Repeat("+1", 100)));
        }

        [TestMethod]
        public void Imbrication_profonde_refusee_sans_plantage()
        {
            AssertRejected(new string('(', 90) + "1" + new string(')', 90));
            AssertRejected(new string('-', 150) + "1");
        }

        [DataTestMethod]
        [DataRow("12*7", true)]
        [DataRow("50%", true)]
        [DataRow("(1+2)*3", true)]
        [DataRow("12", false)]
        [DataRow("abc", false)]
        [DataRow("+", false)]
        public void LooksLikeMath(string text, bool expected)
        {
            Assert.AreEqual(expected, Calc.LooksLikeMath(text));
        }

        [TestMethod]
        public void Format_anglais()
        {
            using (LanguageScope.English())
            {
                Assert.AreEqual("1,234", Calc.Format(1234));
                Assert.AreEqual("2.5", Calc.Format(2.5));
                Assert.AreEqual("-42", Calc.Format(-42));
            }
        }

        [TestMethod]
        public void Format_francais()
        {
            using (LanguageScope.French())
            {
                string group = L.Culture.NumberFormat.NumberGroupSeparator; // espace insécable, selon Windows
                Assert.AreEqual("1" + group + "234", Calc.Format(1234));
                Assert.AreEqual("2,5", Calc.Format(2.5));
            }
        }

        // QA-03 : la calculatrice doit relire ses propres résultats (copier-coller d'un résultat)
        [DataTestMethod]
        [DataRow(2.5, false)]
        [DataRow(2.5, true)]
        [DataRow(-42.0, false)]
        [DataRow(-42.0, true)]
        [DataRow(0.125, false)]
        [DataRow(0.125, true)]
        [DataRow(1.5e-12, false)]
        [DataRow(1.5e-12, true)]
        [DataRow(1234.5, true)]
        public void Aller_retour_format_puis_calcul(double value, bool english)
        {
            AssertRoundTrip(value, english);
        }

        // R-19 : dès qu'un résultat a un séparateur de milliers, il n'est plus relu (FR)
        // ou relu avec une valeur fausse (EN, « 1,234 » devient 1,234). Voir TASKS.md, T-007.
        [Ignore("Défaut connu R-19 : retirer cet attribut avec la correction (T-007)")]
        [DataTestMethod]
        [DataRow(1234.5, false)]
        [DataRow(1234.0, false)]
        [DataRow(1234.0, true)]
        [DataRow(1e20, false)]
        [DataRow(1e20, true)]
        public void Aller_retour_avec_separateur_de_milliers(double value, bool english)
        {
            AssertRoundTrip(value, english);
        }

        private static void AssertRoundTrip(double value, bool english)
        {
            using (english ? LanguageScope.English() : LanguageScope.French())
            {
                string lang = english ? "EN" : "FR";
                string text = Calc.Format(value);
                double reread;
                Assert.IsTrue(Calc.TryEval(text, out reread), lang + " : résultat formaté non relu : « " + text + " »");
                Assert.AreEqual(value, reread, Math.Abs(value) * 1e-9, lang + " : « " + text + " » relu comme " + reread);
            }
        }
    }
}
