using System.Drawing;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    [TestClass]
    public class SettingsUiTests
    {
        private sealed class TestSlider : Slider
        {
            public TestSlider() : base(0, 100, 10) { EnableSettingsKeyboard(); }
            public MouseEventArgs Wheel()
            {
                var args = new HandledMouseEventArgs(MouseButtons.None, 0, 0, 0, 120);
                OnMouseWheel(args);
                return args;
            }
        }

        private sealed class TestSwatches : Swatches
        {
            public void ClickAt(int x, int y) { OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)); }
        }

        private sealed class TestSettingsForm : SettingsForm
        {
            public TestSettingsForm(SettingsActions actions) : base(actions) { }
            public bool DialogKey(Keys key) { return ProcessDialogKey(key); }
        }

        [TestMethod]
        public void SettingsSlider_WheelLeavesSettingAlone()
        {
            using (var slider = new TestSlider())
            {
                slider.Value = 50;
                int changes = 0;
                slider.Changed += (s, e) => changes++;
                var wheel = (HandledMouseEventArgs)slider.Wheel();
                Assert.AreEqual(50, slider.Value);
                Assert.AreEqual(0, changes);
                Assert.IsFalse(wheel.Handled);
            }
        }

        [TestMethod]
        public void Swatches_ClickInGapDoesNotChooseAColor()
        {
            using (var swatches = new TestSwatches())
            {
                swatches.Selected = Swatches.Presets[1];
                int changes = 0;
                swatches.Changed += (s, e) => changes++;
                swatches.ClickAt(Ui.S(28), Ui.S(14));
                Assert.AreEqual(Swatches.Presets[1], swatches.Selected);
                Assert.AreEqual(0, changes);
            }
        }

        [TestMethod]
        public void Swatches_ClickSelectedColorDoesNotSignalAChange()
        {
            using (var swatches = new TestSwatches())
            {
                var first = Swatches.Presets[0];
                swatches.Selected = Color.FromArgb(first.ToArgb());
                int changes = 0;
                swatches.Changed += (s, e) => changes++;
                swatches.ClickAt(Ui.S(14), Ui.S(14));
                Assert.AreEqual(0, changes);
            }
        }

        [TestMethod]
        public void AccentText_AdaptsToLightAndDarkFills()
        {
            Assert.AreEqual(Color.Black, SettingsPalette.OnFill(Color.FromArgb(249, 115, 22)));
            Assert.AreEqual(Color.White, SettingsPalette.OnFill(Color.FromArgb(20, 30, 60)));
        }

        [TestMethod]
        public void SettingsPages_StayWithinViewportAfterResizeAndNavigation()
        {
            InWinForms(VerifyPages);
            InWinForms(() => { using (LanguageScope.Spanish()) VerifyPages(); });
        }

        [TestMethod]
        public void SettingsEscape_UsesSameHidePathAsCloseButton()
        {
            InWinForms(() =>
            {
                int hidden = 0;
                using (var form = new TestSettingsForm(new SettingsActions { HiddenToTray = () => hidden++ }))
                {
                    form.Show();
                    Assert.IsTrue(form.DialogKey(Keys.Escape));
                    Assert.IsFalse(form.Visible);
                    Assert.AreEqual(1, hidden);
                }
            });
        }

        [TestMethod]
        public void SettingsCategory_ShowsLiteralAmpersandsInNames()
        {
            InWinForms(() =>
            {
                var category = new CustomCat { Name = "R&D & Tests", Glyph = Glyphs.Library, Color = Color.Blue };
                Settings.CustomCats.Add(category);
                try
                {
                    using (var form = new SettingsForm(new SettingsActions()))
                    {
                        form.Show();
                        form.SelectPage(4);
                        var card = (Card)typeof(SettingsForm).GetField("customCard", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                        foreach (Control item in card.Controls)
                        {
                            var label = item as Label;
                            if (label == null || label.Text != category.Name) continue;
                            Assert.IsFalse(label.UseMnemonic);
                            return;
                        }
                        Assert.Fail("Category label missing.");
                    }
                }
                finally { Settings.CustomCats.Remove(category); }
            });
        }

        [TestMethod]
        public void SettingsScroll_ReturnsToSamePositionAfterSwitchingPages()
        {
            InWinForms(() =>
            {
                using (var form = new SettingsForm(new SettingsActions()))
                {
                    form.Show();
                    form.Height = form.MinimumSize.Height;
                    var pages = (List<Panel>)typeof(SettingsForm).GetField("pages", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    pages[0].AutoScrollPosition = new Point(0, Ui.S(160));
                    int position = pages[0].AutoScrollPosition.Y;
                    Assert.IsTrue(position < 0);
                    form.SelectPage(1);
                    form.SelectPage(0);
                    Assert.AreEqual(position, pages[0].AutoScrollPosition.Y);
                }
            });
        }

        [TestMethod]
        public void SettingsNavigation_SelectedItemStaysReachableWhenSidebarShrinks()
        {
            InWinForms(() =>
            {
                using (var form = new SettingsForm(new SettingsActions()))
                {
                    form.Show();
                    form.SelectPage(5);
                    form.Height = form.MinimumSize.Height;
                    var nav = (SettingsNavigation)typeof(SettingsForm).GetField("nav", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    foreach (Control child in nav.Controls)
                    {
                        var links = child as Panel;
                        if (links == null) continue;
                        Assert.IsFalse(links.HorizontalScroll.Visible);
                        Assert.IsTrue(links.ClientRectangle.Contains(links.Controls[5].Bounds),
                            "Viewport " + links.ClientRectangle + ", last button " + links.Controls[5].Bounds + ", scroll " + links.AutoScrollPosition);
                        return;
                    }
                    Assert.Fail("Navigation panel missing.");
                }
            });
        }

        private static void InWinForms(System.Action test)
        {
            System.Exception failure = null;
            var ui = new Thread(() =>
            {
                try { using (LanguageScope.French()) test(); }
                catch (System.Exception ex) { failure = ex; }
            });
            ui.SetApartmentState(ApartmentState.STA);
            ui.Start();
            ui.Join();
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        [TestMethod]
        public void SettingsLanguageRecreation_KeepsWindowSizeAndActivePage()
        {
            InWinForms(() =>
            {
                using (var previous = new SettingsForm(new SettingsActions()))
                {
                    previous.Show();
                    previous.Size = previous.MinimumSize;
                    previous.SelectPage(2);
                    using (LanguageScope.Spanish())
                    using (var replacement = new SettingsForm(new SettingsActions()))
                    {
                        replacement.RestoreWindowState(previous);
                        Assert.AreEqual(previous.Bounds, replacement.Bounds);
                        Assert.AreEqual(2, replacement.CurrentPage);
                    }
                }
            });
        }

        [TestMethod]
        public void SettingsLanguageRecreation_KeepsScrollOnTheActivePage()
        {
            InWinForms(() =>
            {
                using (var previous = new SettingsForm(new SettingsActions()))
                {
                    previous.Size = previous.MinimumSize;
                    previous.Show();
                    previous.SelectPage(2);
                    var fields = BindingFlags.Instance | BindingFlags.NonPublic;
                    var pages = typeof(SettingsForm).GetField("pages", fields);
                    var source = ((List<Panel>)pages.GetValue(previous))[2];
                    source.AutoScrollPosition = new Point(0, Ui.S(100));
                    Assert.IsTrue(source.AutoScrollPosition.Y < 0);

                    using (LanguageScope.Spanish())
                    using (var replacement = new SettingsForm(new SettingsActions()))
                    {
                        replacement.RestoreWindowState(previous);
                        replacement.Show();
                        var target = ((List<Panel>)pages.GetValue(replacement))[2];
                        Assert.AreEqual(source.AutoScrollPosition.Y, target.AutoScrollPosition.Y);
                    }
                }
            });
        }

        [TestMethod]
        public void PanelVisibleInCapture_KeepsGlassWhenResized()
        {
            InWinForms(() =>
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var glassField = typeof(PanelForm).GetField("glass", flags);
                var refresh = typeof(PanelForm).GetMethod("RefreshStaticGlass", flags);
                bool originalCapture = Settings.HidePanelInCapture, originalGlass = Settings.Glass;
                try
                {
                    Settings.HidePanelInCapture = false;
                    Settings.Glass = true;
                    using (var panel = new PanelForm())
                    using (var bitmap = new Bitmap(100, 100))
                    using (var rendered = new Bitmap(200, 150))
                    {
                        using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Lime);
                        glassField.SetValue(panel, bitmap);
                        panel.Show();
                        panel.ClientSize = rendered.Size;
                        refresh.Invoke(panel, null);
                        using (var graphics = Graphics.FromImage(rendered))
                            panel.PaintBg(graphics, panel.ClientRectangle, Point.Empty);

                        Assert.AreEqual(Color.Lime.ToArgb(), rendered.GetPixel(180, 130).ToArgb());
                        glassField.SetValue(panel, null);
                    }
                }
                finally
                {
                    Settings.Glass = originalGlass;
                    Settings.HidePanelInCapture = originalCapture;
                }
            });
        }

        [TestMethod]
        public void OpenPanelAppliesGeometryAndWidgetChangesImmediately()
        {
            InWinForms(() =>
            {
                bool oldGlass = Settings.Glass, oldRight = Settings.RightSide;
                bool oldWidgets = Settings.Widgets, oldWeather = Settings.WeatherOn;
                bool oldAnimations = Settings.Animations;
                int oldWidth = Settings.PanelWidth;
                try
                {
                    Settings.Glass = Settings.RightSide = Settings.Widgets = Settings.WeatherOn = Settings.Animations = false;
                    Settings.PanelWidth = 340;
                    using (var panel = new PanelForm { Dirty = false })
                    {
                        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        typeof(PanelForm).GetField("loadedPx", flags).SetValue(panel, panel.IconPx);
                        typeof(PanelForm).GetField("loadedSystem", flags).SetValue(panel, Settings.ShowSystem);
                        typeof(PanelForm).GetField("loadedApps", flags).SetValue(panel, Settings.SearchApps);
                        typeof(PanelForm).GetField("loadedRecents", flags).SetValue(panel, Settings.SearchRecent);

                        Screen screen = Screen.PrimaryScreen;
                        panel.ShowPanel(screen);
                        Assert.AreEqual(screen.WorkingArea.Left, panel.Left);
                        Settings.PanelWidth = 420;
                        Settings.RightSide = true;
                        panel.OnSettingsChanged();
                        Assert.AreEqual(Ui.S(420), panel.Width);
                        Assert.AreEqual(screen.WorkingArea.Right, panel.Right);

                        var timer = (System.Windows.Forms.Timer)typeof(PanelForm).GetField("widgetTimer", flags).GetValue(panel);
                        Assert.IsFalse(timer.Enabled);
                        Settings.Widgets = true;
                        panel.OnSettingsChanged();
                        Assert.IsTrue(timer.Enabled);
                        Settings.Widgets = false;
                        panel.OnSettingsChanged();
                        Assert.IsFalse(timer.Enabled);
                    }
                }
                finally
                {
                    Settings.Glass = oldGlass;
                    Settings.RightSide = oldRight;
                    Settings.Widgets = oldWidgets;
                    Settings.WeatherOn = oldWeather;
                    Settings.Animations = oldAnimations;
                    Settings.PanelWidth = oldWidth;
                }
            });
        }

        [TestMethod]
        public void PanelRefreshKeepsCatalogUntilRelevantSettingsChange()
        {
            InWinForms(() =>
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var windows = typeof(PanelForm).GetField("winSettings", flags);
                var commands = typeof(PanelForm).GetField("commands", flags);
                Color accent = Settings.Accent;
                try
                {
                    using (var panel = new PanelForm())
                    {
                        var originalWindows = windows.GetValue(panel);
                        var originalCommands = commands.GetValue(panel);
                        panel.RefreshCatalog();
                        Assert.AreSame(originalWindows, windows.GetValue(panel));
                        Assert.AreSame(originalCommands, commands.GetValue(panel));

                        Settings.Accent = accent.ToArgb() == Color.Lime.ToArgb() ? Color.Blue : Color.Lime;
                        panel.RefreshCatalog();
                        Assert.AreSame(originalWindows, windows.GetValue(panel));
                        Assert.AreNotSame(originalCommands, commands.GetValue(panel));

                        using (LanguageScope.Spanish())
                        {
                            panel.RefreshCatalog();
                            Assert.AreNotSame(originalWindows, windows.GetValue(panel));
                            var items = (List<DesktopItem>)commands.GetValue(panel);
                            Assert.IsTrue(items.Exists(item => item.Key == "cmd:Lock" && item.Name == "Bloquear"));
                        }
                    }
                }
                finally { Settings.Accent = accent; }
            });
        }

        private static void VerifyPages()
        {
            using (var form = new SettingsForm(new SettingsActions()))
            {
                form.Show();
                var pages = (List<Panel>)typeof(SettingsForm).GetField("pages", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                foreach (int width in new[] { Ui.S(980), form.MinimumSize.Width, Ui.S(820) })
                {
                    form.Width = width;
                    form.Height = form.MinimumSize.Height;
                    for (int index = 0; index < pages.Count; index++)
                    {
                        form.SelectPage(index);
                        Panel page = pages[index];
                        Assert.AreEqual(index, form.CurrentPage);
                        Assert.IsTrue(page.Visible);
                        Assert.IsFalse(page.HorizontalScroll.Visible, "Page " + index);
                        foreach (Control control in page.Controls)
                        {
                            var card = control as Card;
                            if (card == null) continue;
                            foreach (Control child in card.Controls)
                            {
                                Assert.IsTrue(card.ClientRectangle.Contains(child.Bounds), child.Text);
                                foreach (Control other in card.Controls)
                                    Assert.IsFalse(child != other && child.Bounds.IntersectsWith(other.Bounds), child.Text + " / " + other.Text);
                            }
                        }
                    }
                }
            }
        }
    }
}
