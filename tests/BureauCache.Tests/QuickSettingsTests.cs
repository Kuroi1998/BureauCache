using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    [TestClass]
    public class QuickSettingsTests
    {
        [TestMethod]
        public void Popup_StaysAboveTheAnchorOnAMonitorWithNegativeCoordinates()
        {
            var area = new Rectangle(-1440, -150, 1440, 850);
            var anchor = new Rectangle(-172, area.Bottom + Ui.S(8) + 8, 64, 50);
            var content = new Size(460, 520);
            var actual = QuickSettingsForm.PopupBounds(anchor, area, content);

            Assert.IsTrue(area.Contains(actual));
            Assert.AreEqual(anchor.Right, actual.Right);
            Assert.AreEqual(area.Bottom, actual.Bottom);
            Assert.AreEqual(content, actual.Size);
        }

        [TestMethod]
        public void Popup_FitsInASmallWorkAreaAndLeavesRoomForTheScrollbar()
        {
            var area = new Rectangle(20, 40, 600, 290);
            var anchor = new Rectangle(24, 328, 64, 50);
            var content = new Size(460, 520);
            var actual = QuickSettingsForm.PopupBounds(anchor, area, content);

            Assert.IsTrue(area.Contains(actual));
            Assert.AreEqual(area.Left, actual.Left);
            Assert.AreEqual(area.Top, actual.Top);
            Assert.AreEqual(anchor.Top - Ui.S(8), actual.Bottom);
            Assert.AreEqual(content.Width + SystemInformation.VerticalScrollBarWidth, actual.Width);
        }

        [TestMethod]
        public void Sandbox_QuickSettingsBackendsSkipLiveAudioDisplayAndMedia()
        {
            Assert.IsTrue(Sandbox.Active);
            bool muted, internalDisplay, accessible, pluggedIn, saver;
            Assert.IsNull(Native.QuickVolume(82, true, out muted));
            Assert.IsNull(Native.QuickBrightness(IntPtr.Zero, 10, true, out internalDisplay));
            Assert.IsNull(Native.QuickBattery(out pluggedIn, out saver));
            Assert.IsNull(Native.ReadQuickMedia(null, out accessible));
            Assert.IsFalse(Native.QuickMediaCommand(null, null, 0xB3));
            Assert.AreEqual(Native.QuickWifiStatus.Unavailable, Native.ReadQuickWifi(true, new List<Native.QuickWifiNetwork>(), CancellationToken.None));
            Assert.IsFalse(Native.ChangeQuickWifi(new Native.QuickWifiNetwork(), null, CancellationToken.None));
            Assert.IsFalse(Native.ReadQuickBluetooth(new List<Native.QuickBluetoothDevice>(), CancellationToken.None));
            Assert.IsFalse(Native.PairQuickBluetooth(new Native.QuickBluetoothDevice(), IntPtr.Zero, CancellationToken.None));
        }

        [TestMethod]
        public void LocalizedTiles_FitInThePanelInEachLanguage()
        {
            InWinForms(() =>
            {
                float previousScale = Ui.Scale;
                try
                {
                    foreach (float scale in new[] { 1f, 1.25f, 1.5f })
                    {
                        Ui.Scale = scale;
                        using (LanguageScope.French()) VerifyTiles();
                        using (LanguageScope.English()) VerifyTiles();
                        using (LanguageScope.Spanish()) VerifyTiles();
                    }
                }
                finally { Ui.Scale = previousScale; }
            });
        }

        [TestMethod]
        public void Sliders_ShowTheNewPercentageBeforeWindowsResponds()
        {
            InWinForms(() =>
            {
                using (LanguageScope.French())
                using (var form = new QuickSettingsForm(IntPtr.Zero))
                {
                    var keyDown = typeof(Slider).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
                    var sliders = form.Controls.OfType<Slider>().ToArray();
                    Assert.AreEqual(2, sliders.Length);
                    foreach (var slider in sliders)
                    {
                        var readout = form.Controls.OfType<Label>().Single(label => label.Text == slider.AccessibleName);
                        slider.Value = 41;
                        keyDown.Invoke(slider, new object[] { new KeyEventArgs(Keys.Right) });
                        Assert.AreEqual(slider.AccessibleName + " · 42 %", readout.Text);
                    }
                }
            });
        }

        [TestMethod]
        public void InternalBrightness_UsesTheClosestSupportedStep()
        {
            var levels = new byte[] { 100, 40, 20, 60, 10 };
            Assert.AreEqual(40, Native.NearestBrightnessLevel(49, levels));
            Assert.AreEqual(40, Native.NearestBrightnessLevel(50, levels));
            Assert.AreEqual(60, Native.NearestBrightnessLevel(51, levels));
            Assert.AreEqual(10, Native.NearestBrightnessLevel(0, levels));
            Assert.AreEqual(47, Native.NearestBrightnessLevel(47, null));
        }

        [TestMethod]
        public void ConnectionTiles_KeepResultsAfterLosingFocusAndEscapeReturnsHome()
        {
            InWinForms(() =>
            {
                using (LanguageScope.French())
                using (var form = new QuickSettingsForm(IntPtr.Zero))
                {
                    form.Location = new Point(SystemInformation.VirtualScreen.Left - 2000, SystemInformation.VirtualScreen.Top);
                    form.Show();
                    var click = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
                    var key = typeof(QuickSettingsForm).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
                    var deactivate = typeof(QuickSettingsForm).GetMethod("OnDeactivate", BindingFlags.Instance | BindingFlags.NonPublic);
                    var showResults = typeof(QuickSettingsForm).GetMethod("ShowConnectionResults", BindingFlags.Instance | BindingFlags.NonPublic);
                    var snapshotType = typeof(QuickSettingsForm).GetNestedType("ConnectionSnapshot", BindingFlags.NonPublic);
                    var rowType = typeof(QuickSettingsForm).GetNestedType("ConnectionRow", BindingFlags.NonPublic);
                    foreach (var caption in new[] { "Wi-Fi", "Bluetooth" })
                    {
                        var tile = form.Controls.OfType<StartTile>().Single(control => control.Text == caption);
                        click.Invoke(tile, new object[] { EventArgs.Empty });
                        var page = form.Controls.OfType<Panel>().Single();
                        var layout = (TableLayoutPanel)page.Controls[0];
                        var list = layout.Controls.OfType<ListBox>().Single();
                        Assert.IsTrue(list.AccessibleName.Contains(caption), caption);
                        Assert.IsTrue(list.Height > list.ItemHeight, caption);
                        Assert.IsFalse(tile.Visible);
                        list.Enabled = false;
                        deactivate.Invoke(form, new object[] { EventArgs.Empty });
                        Assert.IsTrue(form.Visible, caption);

                        var snapshot = Activator.CreateInstance(snapshotType, true);
                        var row = Activator.CreateInstance(rowType, true);
                        rowType.GetField("Name").SetValue(row, caption + " Test");
                        var rows = (System.Collections.IList)snapshotType.GetField("Rows").GetValue(snapshot);
                        rows.Add(row);
                        showResults.Invoke(form, new[] { snapshot, null, null });
                        deactivate.Invoke(form, new object[] { EventArgs.Empty });
                        Assert.IsTrue(form.Visible, caption);
                        Assert.IsTrue(list.Enabled, caption);
                        Assert.AreEqual(1, list.Items.Count, caption);
                        list.SelectedIndex = 0;
                        Assert.AreSame(row, list.SelectedItem, caption);

                        Assert.AreEqual(true, key.Invoke(form, new object[] { new Message(), Keys.Escape }));
                        Assert.IsTrue(tile.Visible);
                        Assert.IsTrue(page.IsDisposed);
                    }
                    deactivate.Invoke(form, new object[] { EventArgs.Empty });
                    Assert.IsTrue(form.IsDisposed);
                }
            });
        }

        [TestMethod]
        public void BarPositionNotification_ClosesThePanelOnlyWhenTheBarMoves()
        {
            InWinForms(() =>
            {
                using (var bar = new BarForm())
                using (var panel = new QuickSettingsForm(IntPtr.Zero))
                {
                    var quickSettings = typeof(BarForm).GetField("quickSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                    var applyBounds = typeof(BarForm).GetMethod("ApplyBarBounds", BindingFlags.Instance | BindingFlags.NonPublic);
                    quickSettings.SetValue(bar, panel);
                    var bounds = bar.Bounds;
                    applyBounds.Invoke(bar, new object[] { bounds });
                    Assert.IsFalse(panel.IsDisposed);
                    bounds.Offset(-1, 0);
                    applyBounds.Invoke(bar, new object[] { bounds });
                    Assert.IsTrue(panel.IsDisposed);
                }
            });
        }

        [TestMethod]
        public void WifiTemporaryProfile_UsesExactSsidAndEscapesThePassword()
        {
            const string password = "a<&>1234";
            var network = new Native.QuickWifiNetwork { Ssid = new byte[] { 0x43, 0x26, 0xC3, 0xA9 }, Auth = 7, Cipher = 4 };
            var doc = new XmlDocument();
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("w", "http://www.microsoft.com/networking/WLAN/profile/v1");
            doc.LoadXml(Native.QuickWifiProfile(network, password));
            Assert.AreEqual("4326C3A9", doc.SelectSingleNode("//w:SSID/w:hex", ns).InnerText);
            Assert.AreEqual(password, doc.SelectSingleNode("//w:keyMaterial", ns).InnerText);
            Assert.AreEqual("WPA2PSK", doc.SelectSingleNode("//w:authentication", ns).InnerText);
            network.Auth = 9;
            doc.LoadXml(Native.QuickWifiProfile(network, password));
            Assert.AreEqual("WPA3SAE", doc.SelectSingleNode("//w:authentication", ns).InnerText);
            Assert.IsTrue(Native.ValidQuickWifiPassword(password));
            Assert.IsFalse(Native.ValidQuickWifiPassword("1234567"));
        }

        static void InWinForms(Action action)
        {
            Exception error = null;
            var ui = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { error = ex; }
            });
            ui.SetApartmentState(ApartmentState.STA);
            ui.Start();
            ui.Join();
            if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
        }

        static void VerifyTiles()
        {
            using (var form = new QuickSettingsForm(IntPtr.Zero))
            {
                foreach (var tile in form.Controls.OfType<StartTile>())
                {
                    Assert.IsTrue(form.ClientRectangle.Contains(tile.Bounds), tile.Text);
                    Assert.IsTrue(tile.TextFits, tile.Text);
                }
            }
        }
    }
}
