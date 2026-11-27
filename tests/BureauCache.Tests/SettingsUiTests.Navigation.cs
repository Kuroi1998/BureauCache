using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    public partial class SettingsUiTests
    {
        [TestMethod]
        public void SettingsSearch_FindsWeatherWithOrWithoutAccentsAndOpensTheSelectedRow()
        {
            InWinForms(() =>
            {
                using (var form = new TestSettingsForm(new SettingsActions()))
                {
                    form.Size = form.MinimumSize;
                    form.Show();
                    var box = SettingsControl<InputBox>(form, "settingsSearch").Box;
                    var results = SettingsControl<ListBox>(form, "searchResults");
                    Assert.IsTrue(form.CommandKey(Keys.Control | Keys.F));
                    Assert.IsTrue(box.Focused);
                    box.Text = "MÉTÉO";
                    string[] accented = results.Items.Cast<object>().Select(item => item.ToString()).ToArray();
                    box.Text = "meteo";
                    CollectionAssert.AreEqual(accented, results.Items.Cast<object>().Select(item => item.ToString()).ToArray());
                    CollectionAssert.AreEqual(new[] { "Météo — Apparence › Panneau", "Lieu de la météo — Apparence › Panneau" }, accented);
                    Assert.IsTrue(form.DialogKey(Keys.Down));
                    Assert.AreEqual(1, results.SelectedIndex);
                    Assert.IsTrue(form.DialogKey(Keys.Enter));
                    Assert.AreEqual(2, form.CurrentPage);
                    Assert.AreEqual("Panneau", VisibleTabs(form).SelectedTab.Text);
                    Assert.IsTrue(SettingsControl<InputBox>(form, "placeBox").Box.Focused);
                    Assert.AreEqual("", box.Text);
                    Assert.IsFalse(results.Visible);
                    Assert.IsFalse(SettingsControl<Segmented>(form, "sTheme").Visible);
                    Assert.IsTrue(form.CommandKey(Keys.Control | Keys.F));
                    Assert.AreEqual("meteo", box.Text);
                    Assert.AreEqual(1, results.SelectedIndex);
                    Assert.IsTrue(form.DialogKey(Keys.Tab));
                    Assert.IsTrue(results.Focused);
                    Assert.IsTrue(form.DialogKey(Keys.Shift | Keys.Tab));
                    Assert.IsTrue(box.Focused);
                    Assert.IsTrue(form.DialogKey(Keys.Escape));
                    Assert.IsTrue(form.CommandKey(Keys.Control | Keys.F));
                    Assert.AreEqual("", box.Text);

                    box.Text = "ville meteo";
                    Assert.AreEqual(1, results.Items.Count);
                    Rectangle hit = results.GetItemRectangle(0);
                    MouseClick(results, hit);
                    Assert.AreEqual("", box.Text, "A mouse click should open the result.");
                    Assert.IsTrue(SettingsControl<InputBox>(form, "placeBox").Box.Focused);
                    box.Text = "qxznoresult";
                    Assert.IsTrue(SettingsControl<Label>(form, "noSearchResults").Visible);
                    Assert.IsTrue(form.DialogKey(Keys.Escape));
                    Assert.IsTrue(form.Visible);
                    Assert.AreEqual("Panneau", VisibleTabs(form).SelectedTab.Text);

                    using (LanguageScope.Spanish())
                    using (var replacement = new SettingsForm(new SettingsActions()))
                    {
                        replacement.RestoreWindowState(form);
                        replacement.Show();
                        Assert.AreEqual("Panel", VisibleTabs(replacement).SelectedTab.Text);
                    }
                }
            });
        }

        [TestMethod]
        public void SettingsSearch_UsesLocalizedKeywordsAndPaths()
        {
            InWinForms(() =>
            {
                using (LanguageScope.English()) VerifySearch("dark mode", "Theme — Appearance › Theme");
                using (LanguageScope.Spanish()) VerifySearch("modo oscuro", "Tema — Apariencia › Tema");
            });
        }

        [TestMethod]
        public void SettingsSearch_RanksCommonTasksFirst()
        {
            InWinForms(() =>
            {
                using (var form = new SettingsForm(new SettingsActions()))
                {
                    form.Show();
                    var box = SettingsControl<InputBox>(form, "settingsSearch").Box;
                    var results = SettingsControl<ListBox>(form, "searchResults");
                    box.Text = "capture ecran";
                    StringAssert.StartsWith(results.Items[0].ToString(), "Masquer le panneau dans les captures");
                    box.Text = "demarrage automatique";
                    StringAssert.StartsWith(results.Items[0].ToString(), "Lancer au démarrage de Windows");
                    box.Text = "icone horloge";
                    StringAssert.StartsWith(results.Items[0].ToString(), "Toujours visible");
                }
            });
        }

        [TestMethod]
        public void SettingsAccessibility_ExposesActiveCategorySelectedTabAndFocusedSetting()
        {
            InWinForms(() =>
            {
                using (var form = new TestSettingsForm(new SettingsActions()))
                {
                    form.Show();
                    Assert.IsTrue(form.CommandKey(Keys.Control | Keys.F));
                    SettingsControl<InputBox>(form, "settingsSearch").Box.Text = "ville meteo";
                    Assert.IsTrue(form.DialogKey(Keys.Enter));
                    var focused = SettingsControl<InputBox>(form, "placeBox").Box;
                    Assert.IsTrue(focused.Focused);
                    Assert.AreEqual("Lieu de la météo", focused.AccessibilityObject.Name);
                    Assert.IsTrue(RectangleToPage(focused, VisibleTabs(form)).Contains(focused.RectangleToScreen(focused.ClientRectangle)));
                    var nav = SettingsControl<SettingsNavigation>(form, "nav");
                    var categories = nav.Controls.OfType<Panel>().Single().Controls.Cast<Control>();
                    Assert.AreEqual("Apparence, rubrique active", categories.Single(item => item.AccessibilityObject.Name.EndsWith(", rubrique active")).AccessibilityObject.Name);
                    var tabs = VisibleTabs(form);
                    Assert.AreEqual("Panneau", tabs.SelectedTab.Text);
                    StringAssert.Contains(tabs.AccessibilityObject.Name, "Onglet actif : Panneau");
                    Assert.IsTrue(form.DialogKey(Keys.Shift | Keys.Tab));
                    Assert.IsTrue(form.ActiveControl != null && !focused.Focused);
                    Assert.IsTrue(form.DialogKey(Keys.Tab));
                    Assert.IsTrue(focused.Focused);
                }
            });
        }

        [DataTestMethod]
        [DataRow(1.25f)]
        [DataRow(1.50f)]
        public void SettingsCompactLayout_FitsSpanishTabsAndChoicesAtCombinedScale(float dpiScale)
        {
            InWinForms(() =>
            {
                float previousScale = Ui.Scale, previousZoom = Ui.FontZoom;
                try
                {
                    using (var screen = Graphics.FromHwnd(IntPtr.Zero)) Ui.FontZoom = 1.30f * dpiScale / (screen.DpiX / 96f);
                    Ui.Scale = dpiScale * 1.30f;
                    using (LanguageScope.Spanish()) VerifyPages(true);
                }
                finally { Ui.Scale = previousScale; Ui.FontZoom = previousZoom; }
            });
        }

        [TestMethod]
        public void SettingsTabs_SelectByClickAndArrowAndSaveThroughExistingSettingsHandler()
        {
            InWinForms(() =>
            {
                string previousDir = Settings.DirOverride;
                bool previousAnimations = Settings.Animations;
                int previousTheme = Settings.ThemeMode;
                try
                {
                    Settings.DirOverride = Path.Combine(previousDir, Guid.NewGuid().ToString("N"));
                    Settings.Animations = true;
                    Settings.ThemeMode = 1;
                    using (var form = new TestSettingsForm(new SettingsActions()))
                    {
                        form.Size = form.MinimumSize;
                        form.Show();
                        form.SelectPage(2);
                        TabControl tabs = VisibleTabs(form);
                        Rectangle tab = tabs.GetTabRect(1);
                        int point = ((tab.Top + tab.Height / 2) << 16) | (tab.Left + tab.Width / 2);
                        Native.SendMessagePtr(tabs.Handle, 0x201, (IntPtr)1, (IntPtr)point);
                        Native.SendMessagePtr(tabs.Handle, 0x202, IntPtr.Zero, (IntPtr)point);
                        Assert.AreEqual("Thème", tabs.SelectedTab.Text);
                        Assert.IsTrue(tabs.Focus());
                        Native.SendMessagePtr(tabs.Handle, 0x100, (IntPtr)Keys.Right, IntPtr.Zero);
                        Assert.AreEqual("Panneau", tabs.SelectedTab.Text);
                        Native.SendMessagePtr(tabs.Handle, 0x100, (IntPtr)Keys.Left, IntPtr.Zero);
                        Assert.AreEqual("Thème", tabs.SelectedTab.Text);
                        Assert.AreEqual(Theme.Bg, form.BackColor);

                        var toggle = SettingsControl<Toggle>(form, "tAnim");
                        toggle.AccessibilityObject.DoDefaultAction();
                        Settings.Flush();
                        Assert.IsFalse(Settings.Animations);
                        Assert.IsTrue(File.Exists(Settings.FilePath));
                        Settings.Animations = true;
                        Assert.IsTrue(Settings.Load(Settings.FilePath));
                        Assert.IsFalse(Settings.Animations);
                        Settings.ThemeMode = 2;
                        Settings.Fire();
                        Assert.AreEqual(Theme.Bg, form.BackColor);
                        Assert.IsFalse(Settings.Dark);
                    }
                }
                finally
                {
                    Settings.CancelPendingSave();
                    Settings.DirOverride = previousDir;
                    Settings.Animations = previousAnimations;
                    Settings.ThemeMode = previousTheme;
                }
            });
        }

        [TestMethod]
        public void SettingsSlider_CoalescesDragAndPublishesFinalValueOnRelease()
        {
            InWinForms(() =>
            {
                int previousDelay = Settings.Delay;
                bool previousSaveBlocked = Settings.SaveBlocked;
                int notifications = 0;
                Action changed = () => notifications++;
                try
                {
                    Settings.SaveBlocked = true;
                    using (var form = new SettingsForm(new SettingsActions()))
                    {
                        form.Show();
                        form.SelectPage(1);
                        Slider slider = SettingsControl<Slider>(form, "slDelay");
                        Settings.Changed += changed;
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        typeof(Slider).GetMethod("OnMouseDown", flags).Invoke(slider, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 0, 5, 0) });
                        for (int x = 10; x < slider.Width; x += 5)
                            typeof(Slider).GetMethod("OnMouseMove", flags).Invoke(slider, new object[] { new MouseEventArgs(MouseButtons.Left, 0, x, 5, 0) });
                        Assert.AreEqual(0, notifications);
                        Assert.AreEqual(1000, Settings.Delay);
                        typeof(Slider).GetMethod("OnMouseUp", flags).Invoke(slider, new object[] { new MouseEventArgs(MouseButtons.Left, 1, slider.Width - 1, 5, 0) });
                        Assert.AreEqual(1, notifications);
                        Assert.AreEqual("1000 ms", SettingsControl<Label>(form, "lDelay").Text);
                    }
                }
                finally
                {
                    Settings.Changed -= changed;
                    Settings.SaveBlocked = previousSaveBlocked;
                    Settings.Delay = previousDelay;
                }
            });
        }

        [TestMethod]
        public void SettingsSearch_PreservesResultsUntilTheCatalogChanges()
        {
            InWinForms(() =>
            {
                var category = new CustomCat { Name = "Borealis-QA", Glyph = Glyphs.Library, Color = Color.Blue };
                try
                {
                    using (var form = new SettingsForm(new SettingsActions()))
                    {
                        form.Show();
                        var box = SettingsControl<InputBox>(form, "settingsSearch").Box;
                        var results = SettingsControl<ListBox>(form, "searchResults");
                        box.Text = "meteo";
                        object first = results.Items[0];
                        Settings.Fire();
                        Assert.AreSame(first, results.Items[0]);
                        box.Text = "borealis";
                        Assert.AreEqual(0, results.Items.Count);
                        Settings.CustomCats.Add(category);
                        Settings.Fire();
                        StringAssert.StartsWith(results.Items[0].ToString(), category.Name);
                        Settings.CustomCats.Remove(category);
                        Settings.Fire();
                        Assert.AreEqual(0, results.Items.Count);
                    }
                }
                finally { Settings.CustomCats.Remove(category); }
            });
        }

        [TestMethod]
        public void SettingsDesktopAnalysis_IsScheduledOnlyForVisibleArchiveControls()
        {
            InWinForms(() =>
            {
                using (var form = new SettingsForm(new SettingsActions()))
                {
                    form.Show();
                    form.SelectPage(4);
                    var timer = (Timer)typeof(SettingsForm).GetField("desktopAnalysisTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    Assert.IsTrue(timer.Enabled);
                    var tabs = VisibleTabs(form);
                    tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().Single(tab => tab.Text == "Catégories");
                    Assert.IsFalse(timer.Enabled);
                    Settings.Fire();
                    Assert.IsFalse(timer.Enabled);
                    tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().Single(tab => tab.Text == "Rangement");
                    Assert.IsTrue(timer.Enabled);
                    SettingsControl<InputBox>(form, "settingsSearch").Box.Text = "meteo";
                    Assert.IsFalse(timer.Enabled);
                }
            });
        }

        [TestMethod]
        public void SettingsLanguageRecreation_MaximizedLanguageTabKeepsVisibleControls()
        {
            InWinForms(() =>
            {
                using (var previous = new SettingsForm(new SettingsActions()))
                {
                    previous.WindowState = FormWindowState.Maximized;
                    previous.Show();
                    VisibleTabs(previous).SelectedIndex = 3;
                    Assert.IsTrue(SettingsControl<Segmented>(previous, "sLang").Focus());
                    using (LanguageScope.English()) VerifyLanguageReplacement(previous, "Language & help", "Language");
                    using (LanguageScope.Spanish()) VerifyLanguageReplacement(previous, "Idioma y ayuda", "Idioma");
                    VerifyLanguageReplacement(previous, "Langue et aide", "Langue");
                }
            });
        }

        private static void VerifyLanguageReplacement(SettingsForm previous, string tabTitle, string fieldName)
        {
            bool languageFocused = SettingsControl<Segmented>(previous, "sLang").Focused;
            using (var replacement = new SettingsForm(new SettingsActions()))
            {
                replacement.RestoreWindowState(previous);
                bool preparedBeforeReveal = false;
                bool paintedBeforeReveal = false;
                var toPaint = replacement.Controls.OfType<TabControl>().Single(t => t.SelectedIndex == 3);
                toPaint.Paint += (s, e) => { if (replacement.Opacity == 0 && previous.Visible) paintedBeforeReveal = true; };
                replacement.VisibleChanged += (s, e) =>
                {
                    if (!replacement.Visible) return;
                    Assert.IsTrue(previous.Visible);
                    Assert.AreEqual(0d, replacement.Opacity);
                    preparedBeforeReveal = true;
                };
                replacement.ShowAfterPainting();
                Assert.IsTrue(preparedBeforeReveal);
                Assert.IsTrue(paintedBeforeReveal, "The tab must paint before the replacement is revealed.");
                Assert.AreEqual(1d, replacement.Opacity);
                var tab = VisibleTabs(replacement).SelectedTab;
                var language = SettingsControl<Segmented>(replacement, "sLang");
                Assert.AreEqual(FormWindowState.Maximized, replacement.WindowState);
                Assert.AreEqual(tabTitle, tab.Text);
                StringAssert.EndsWith(VisibleTabs(replacement).AccessibilityObject.Name, tabTitle);
                Assert.IsTrue(tab.Contains(language), "The language panel belongs to the selected tab.");
                Assert.IsTrue(language.Visible);
                Assert.IsTrue(RectangleToPage(language, VisibleTabs(replacement)).Contains(language.RectangleToScreen(language.ClientRectangle)));
                Assert.AreEqual(fieldName, language.AccessibleName);
                if (languageFocused) Assert.IsTrue(language.Focused);
            }
        }

        [TestMethod]
        public void TaskbarStartup_AllowsSlowExplorerButDoesNotRetryIndefinitely()
        {
            long deadline = -1;
            Assert.IsTrue(App.WaitForTaskbarExplorer(150, ref deadline, false));
            Assert.AreEqual(10150L, deadline);
            Assert.IsTrue(App.WaitForTaskbarExplorer(10149, ref deadline, false));
            Assert.IsFalse(App.WaitForTaskbarExplorer(10150, ref deadline, false));
            Assert.IsFalse(App.WaitForTaskbarExplorer(11000, ref deadline, true));
            Assert.AreEqual(-1L, deadline);
            Assert.IsTrue(App.WaitForTaskbarExplorer(11500, ref deadline, false));
            Assert.IsFalse(App.WaitForTaskbarExplorer(13000, ref deadline, true));
        }

        private static void VerifySearch(string query, string result)
        {
            using (var form = new SettingsForm(new SettingsActions()))
            {
                form.Show();
                SettingsControl<InputBox>(form, "settingsSearch").Box.Text = query;
                Assert.AreEqual(result, SettingsControl<ListBox>(form, "searchResults").Items[0].ToString());
            }
        }

        private static T SettingsControl<T>(SettingsForm form, string field) where T : Control
        {
            return (T)typeof(SettingsForm).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
        }

        private static TabControl VisibleTabs(SettingsForm form) { return form.Controls.OfType<TabControl>().Single(tab => tab.Visible); }

        private static Rectangle RectangleToPage(Control target, TabControl tabs)
        {
            Panel body = tabs.SelectedTab.Controls.OfType<Panel>().Single(panel => panel.Contains(target));
            return body.RectangleToScreen(body.ClientRectangle);
        }

        private static void MouseClick(ListBox list, Rectangle item)
        {
            int point = ((item.Top + item.Height / 2) << 16) | (item.Left + 5);
            Native.SendMessagePtr(list.Handle, 0x201, (IntPtr)1, (IntPtr)point);
            Native.SendMessagePtr(list.Handle, 0x202, IntPtr.Zero, (IntPtr)point);
        }
    }
}
