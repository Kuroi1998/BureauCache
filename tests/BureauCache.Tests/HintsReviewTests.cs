using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class HintsReviewTests
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    [TestCategory("Review")]
    public void QA36_01_SearchUsesTheSpaceFreedByTheChips()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm { Width = Ui.S(260) })
            {
                TextBox search = GetField<TextBox>(panel, "search");
                int emptyWidth = search.Width;
                search.Text = "= 123+456";
                Assert.IsTrue(search.Width > emptyWidth, "QA36-01 : le champ reste aussi étroit après la disparition des pastilles.");
                search.Clear();
                Assert.AreEqual(emptyWidth, search.Width);
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA36_01_ChipClicksAvoidTheTextBoxAndGripOnEitherSide()
    {
        RunSta(() =>
        {
            bool originalSide = Settings.RightSide;
            float originalScale = Ui.Scale;
            try
            {
                foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
                {
                    Ui.Scale = scale;
                    foreach (bool right in new[] { false, true })
                    {
                        Settings.RightSide = right;
                        using (var panel = new PanelForm { Width = Ui.S(260) })
                        {
                            Rectangle search = GetField<TextBox>(panel, "search").Bounds;
                            Rectangle[] chips = GetField<Rectangle[]>(panel, "rcChips");
                            for (int i = 0; i < chips.Length; i++)
                            {
                                Rectangle chip = chips[i];
                                var center = new Point(chip.Left + chip.Width / 2, chip.Top + chip.Height / 2);
                                Assert.IsFalse(search.IntersectsWith(chip), "QA36-01 : la zone d'une pastille empiète sur la saisie.");
                                Assert.IsFalse((bool)Call(panel, "InGrip", new Point(chip.Left, center.Y)));
                                Assert.IsFalse((bool)Call(panel, "InGrip", new Point(chip.Right - 1, center.Y)));
                                Assert.AreEqual(7 + i, (int)Call(panel, "HitButton", center));
                            }
                        }
                    }
                }
            }
            finally
            {
                Settings.RightSide = originalSide;
                Ui.Scale = originalScale;
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA36_02_QuestionChipAndTypedQuestionBothShowHelpBeforeTheQuery()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm())
            {
                TextBox search = GetField<TextBox>(panel, "search");
                Rectangle chip = GetField<Rectangle[]>(panel, "rcChips")[1];
                var clic = new MouseEventArgs(MouseButtons.Left, 1, chip.Left + 1, chip.Top + 1, 0);
                Call(panel, "OnMouseClick", clic);
                Assert.AreEqual("?", search.Text.Trim(), "QA36-02 : le clic doit conserver le préfixe web.");
                Assert.IsNotNull(panel.View.Help, "QA36-02 : le clic affiche la même aide que le préfixe saisi seul.");
                search.Text += " bureau";
                Assert.IsNull(panel.View.Help);
                DesktopItem first = panel.View.Selected;
                Assert.IsNotNull(first);
                Assert.AreEqual(ItemKind.Web, first.Kind);
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA36_03_PinTooltipRefreshesWhileThePointerStaysOnTheButton()
    {
        RunSta(() =>
        {
            bool original = Settings.Pinned;
            try
            {
                using (var panel = new PanelForm())
                {
                    Settings.Pinned = false;
                    Call(panel, "UpdateTip", 5);
                    ToolTip tip = GetField<ToolTip>(panel, "tips");
                    string before = tip.GetToolTip(panel);
                    Settings.Pinned = true;
                    Call(panel, "UpdateTip", 5);
                    Assert.AreNotEqual(before, tip.GetToolTip(panel), "QA36-03 : l'infobulle invite toujours à épingler.");
                }
            }
            finally { Settings.Pinned = original; }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA36_04_LongSpanishMessageFitsInTheWorkingArea()
    {
        RunSta(() =>
        {
            float scale = Ui.Scale;
            try
            {
                Ui.Scale = 2f;
                using (BureauCache.Tests.LanguageScope.Spanish())
                {
                    string notes = string.Join("\n", Enumerable.Repeat("— Sí", 110));
                    using (Form dialog = NewMessage(notes))
                    {
                        int available = Screen.FromPoint(Cursor.Position).WorkingArea.Height;
                        Assert.IsTrue(dialog.Height <= available,
                            string.Format("QA36-04 : hauteur {0}, zone de travail {1}.", dialog.Height, available));
                    }
                }
            }
            finally { Ui.Scale = scale; }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void QA36_07_SecondFavoriteMatchesTheSecondDisplayedItemWithDuplicateKeys()
    {
        RunSta(() =>
        {
            List<string> favorites = Settings.Favorites;
            try
            {
                using (var panel = new PanelForm())
                {
                    var activated = new List<string>();
                    panel.All.Add(new DesktopItem { Key = "qa-a", Name = "A", Run = () => activated.Add("A") });
                    panel.All.Add(new DesktopItem { Key = "qa-b", Name = "B", Run = () => activated.Add("B") });
                    Settings.Favorites = new List<string> { "qa-a", "QA-A", "qa-b" };
                    panel.Rebuild(false);
                    List<Group> groups = GetField<List<Group>>(panel.View, "groups");
                    Assert.AreEqual("B", groups.Single(g => g.Key == "fav").Items[1].Name);
                    object[] args = { new Message(), Keys.Control | Keys.D2 };
                    Assert.IsTrue((bool)typeof(PanelForm).GetMethod("ProcessCmdKey", PrivateInstance).Invoke(panel, args));
                    CollectionAssert.AreEqual(new[] { "B" }, activated, "QA36-07 : Ctrl+2 ouvre A alors que le deuxième favori affiché est B.");
                }
            }
            finally { Settings.Favorites = favorites; }
        });
    }

    static Form NewMessage(string message)
    {
        ConstructorInfo constructor = typeof(MessageDialog).GetConstructors(PrivateInstance).Single();
        return (Form)constructor.Invoke(new object[] { message, MessageDialog.Kind.Question, true });
    }

    static T GetField<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    }

    static object Call(object target, string name, params object[] args)
    {
        return target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);
    }

    static void RunSta(Action action)
    {
        Assert.IsTrue(Sandbox.Active);
        Exception error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(10000), "Le scénario WinForms doit se terminer en dix secondes.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
