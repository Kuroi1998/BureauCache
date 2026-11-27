using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-022 : découverte des fonctions (pastilles, infobulles) et boîtes de message aux couleurs de l'application
[TestClass]
public class HintsTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    public void DialogsAreBlockedDuringTests()
    {
        Assert.ThrowsException<InvalidOperationException>(() => Dialogs.Ask(null, "?"), "Un test ne doit jamais attendre une réponse.");
        Assert.ThrowsException<InvalidOperationException>(() => Dialogs.Info(null, "i"));
        Assert.ThrowsException<InvalidOperationException>(() => Dialogs.Warn(null, "w"));
    }

    [TestMethod]
    public void QuestionHasYesAndNoAndInfoHasASingleButton()
    {
        RunSta(() =>
        {
            using (BureauCache.Tests.LanguageScope.French())
            {
                using (Form question = NewMessage("Éteindre l'ordinateur ?", 0, true))
                {
                    var buttons = question.Controls.OfType<FlatButton>().ToList();
                    Assert.AreEqual(2, buttons.Count);
                    Assert.AreEqual("Oui", buttons.Single(b => b.Primary).Text);
                    Assert.AreEqual("Non", buttons.Single(b => !b.Primary).Text);
                }
                using (Form info = NewMessage("Terminé.", 1, false))
                {
                    var buttons = info.Controls.OfType<FlatButton>().ToList();
                    Assert.AreEqual(1, buttons.Count);
                    Assert.IsTrue(buttons[0].Right <= info.ClientSize.Width, "Le bouton reste dans la fenêtre.");
                }
                using (Form longText = NewMessage(new string('x', 900), 1, false))
                    Assert.IsTrue(longText.Controls.OfType<FlatButton>().All(b => b.Bottom <= longText.ClientSize.Height), "Un long message agrandit la fenêtre.");
            }
        });
    }

    [TestMethod]
    public void ButtonsAreAtLeastAsWideAsTheirTranslatedText()
    {
        RunSta(() =>
        {
            const string spanish = "Restaurar una copia de seguridad completa";
            using (var b = new FlatButton(spanish, true, Ui.S(100)))
                Assert.IsTrue(b.Width >= TextRenderer.MeasureText(spanish, b.Font).Width, "Le libellé n'est plus tronqué.");
        });
    }

    [TestMethod]
    public void SearchChipsInsertTheirPrefixAndEveryHotspotHasATooltip()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm())
            {
                TextBox search = (TextBox)typeof(PanelForm).GetField("search", Private).GetValue(panel);
                MethodInfo click = typeof(PanelForm).GetMethod("ClickChip", Private);
                click.Invoke(panel, new object[] { 7 });
                Assert.AreEqual("=", search.Text);
                click.Invoke(panel, new object[] { 9 });
                Assert.AreEqual("> ", search.Text);
                MethodInfo tip = typeof(PanelForm).GetMethod("TipText", Private);
                foreach (int code in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 })
                    Assert.IsFalse(string.IsNullOrEmpty((string)tip.Invoke(panel, new object[] { code })), "Infobulle manquante : " + code);
                Assert.IsNull(tip.Invoke(panel, new object[] { -1 }));
            }
        });
    }

    static Form NewMessage(string text, int kind, bool safe)
    {
        Type kindType = typeof(MessageDialog).GetNestedType("Kind");
        ConstructorInfo ctor = typeof(MessageDialog).GetConstructors(Private).Single();
        return (Form)ctor.Invoke(new object[] { text, Enum.ToObject(kindType, kind), safe });
    }

    static void RunSta(Action action)
    {
        Exception error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(20000), "Le scénario WinForms a dépassé 20 secondes.");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
