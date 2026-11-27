using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class InterfaceReviewTests
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    public void IconCacheRetriesAfterATemporaryExtractionFailure()
    {
        const int size = 12347;
        string key = "qa65|" + Guid.NewGuid().ToString("N") + "|" + size;
        try
        {
            Assert.IsNull(IconCache.Get(key, () => null));
            Bitmap recovered = IconCache.Get(key, () => new Bitmap(1, 1));
            Assert.IsNotNull(recovered);
            Assert.AreSame(recovered, IconCache.Get(key, () => null));
        }
        finally { IconCache.RemoveSize(size); }
    }

    [TestMethod]
    public void SettingsWeatherIgnoresResultsAfterTheQueryIsEdited()
    {
        RunSta(() =>
        {
            using (var form = new SettingsForm(new SettingsActions()))
            {
                TextBox query = Field<InputBox>(form, "placeBox").Box;
                query.Text = "Mons";
                int previousTicket = Field<int>(form, "placeSearch");
                query.Text = "Namur";
                Call(form, "ShowPlaces", previousTicket, new List<WeatherPlace>
                {
                    new WeatherPlace { Name = "Mons", Country = "Belgique" }
                }, null);
                Assert.IsNull(Field<ContextMenuStrip>(form, "placeMenu"));
            }
        });
    }

    [TestMethod]
    public void APanelLoadRequestedBeforeTheHandleExistsStaysPending()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm())
            {
                Assert.IsFalse(panel.IsHandleCreated);
                panel.RequestLoad();
                Assert.IsTrue(panel.Dirty);
                Assert.IsFalse(Field<bool>(panel, "loading"));
            }
        });
    }

    [TestMethod]
    public void DisposingThePanelStopsItsOwnTimersAndReleasesThePreview()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm())
            {
                var timers = new[] { "anim", "widgetTimer", "blurTimer", "contentTimer" }
                    .Select(name => Field<System.Windows.Forms.Timer>(panel, name)).ToArray();
                var preview = Field<PreviewForm>(panel, "preview");
                try
                {
                    foreach (var timer in timers) timer.Start();
                    panel.Dispose();
                    Assert.IsFalse(timers.Any(timer => timer.Enabled));
                    Assert.IsTrue(preview.IsDisposed);
                }
                finally
                {
                    foreach (var timer in timers) timer.Dispose();
                    preview.Dispose();
                }
            }
        });
    }

    [TestMethod]
    public void DisposingTheListStopsItsActiveTimers()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm())
            using (var view = new ItemsView(panel))
            {
                var scroll = Field<System.Windows.Forms.Timer>(view, "scrollAnim");
                var hover = Field<System.Windows.Forms.Timer>(view, "hoverTimer");
                try
                {
                    scroll.Start();
                    hover.Start();
                    view.Dispose();
                    Assert.IsFalse(scroll.Enabled);
                    Assert.IsFalse(hover.Enabled);
                }
                finally { scroll.Stop(); hover.Stop(); }
            }
        });
    }

    [TestMethod]
    public void RebuildingTheListCancelsAnObsoleteDrop()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm())
            using (var view = new ItemsView(panel) { Size = new Size(350, 300), ForceList = true })
            {
                Group group = new Group { Key = "all" };
                group.Items.Add(new DesktopItem { Kind = ItemKind.Desktop, Key = "test", Name = "Test" });
                view.SetGroups(new List<Group> { group }, false);
                var rows = (System.Collections.IList)typeof(ItemsView).GetField("rows", PrivateInstance).GetValue(view);
                typeof(ItemsView).GetField("dragRow", PrivateInstance).SetValue(view, rows[0]);
                typeof(ItemsView).GetField("dragging", PrivateInstance).SetValue(view, true);
                typeof(ItemsView).GetField("dropGroup", PrivateInstance).SetValue(view, group);
                int drops = 0;
                view.ItemDropped += (item, source, target, index) => drops++;
                panel.Busy = true;
                view.SetGroups(new List<Group>(), true);
                typeof(ItemsView).GetMethod("OnMouseUp", PrivateInstance)
                    .Invoke(view, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 20, 12, 0) });
                Assert.AreEqual(0, drops);
                Assert.IsFalse(panel.Busy);
                Assert.IsFalse(view.IsScrolling);
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q30_01_NonAnimatedOpeningAppliesExclusionBeforeVisibleContent()
    {
        if (!Native.CaptureExclusionSupported) Assert.Inconclusive("Q30-01 : Windows 10 2004 ou plus récent est requis.");
        RunSta(() =>
        {
            bool animation = Settings.Animations, hideCapture = Settings.HidePanelInCapture;
            bool glass = Settings.Glass, widgets = Settings.Widgets, weather = Settings.WeatherOn;
            try
            {
                Settings.Animations = Settings.Glass = Settings.Widgets = Settings.WeatherOn = false;
                Settings.HidePanelInCapture = true;
                using (var panel = new PanelForm())
                {
                    panel.Dirty = false;
                    uint? firstExposedAffinity = null;
                    panel.VisibleChanged += (sender, args) =>
                    {
                        if (panel.Visible && panel.Opacity > 0)
                            firstExposedAffinity = Field<uint>(panel, "appliedAffinity");
                    };
                    panel.ShowPanel(Screen.PrimaryScreen);
                    if (Field<uint>(panel, "appliedAffinity") != Native.WDA_EXCLUDEFROMCAPTURE)
                        Assert.Inconclusive("Q30-01 : le bureau de test a refusé l'affinité Win32.");
                    Assert.IsTrue(!firstExposedAffinity.HasValue ||
                        firstExposedAffinity.Value == Native.WDA_EXCLUDEFROMCAPTURE,
                        "Q30-01 : la fenêtre est devenue visible et opaque avant l'application de l'affinité.");
                    panel.HidePanel();
                }
            }
            finally
            {
                Settings.Animations = animation;
                Settings.HidePanelInCapture = hideCapture;
                Settings.Glass = glass;
                Settings.Widgets = widgets;
                Settings.WeatherOn = weather;
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q30_01_RecreatingTheVisibleHandleReappliesExclusion()
    {
        if (!Native.CaptureExclusionSupported) Assert.Inconclusive("Q30-01 : Windows 10 2004 ou plus récent est requis.");
        RunSta(() =>
        {
            bool animation = Settings.Animations, hideCapture = Settings.HidePanelInCapture;
            bool glass = Settings.Glass, widgets = Settings.Widgets, weather = Settings.WeatherOn;
            try
            {
                Settings.Animations = Settings.Glass = Settings.Widgets = Settings.WeatherOn = false;
                Settings.HidePanelInCapture = true;
                using (var panel = new PanelForm())
                {
                    panel.Dirty = false;
                    panel.ShowPanel(Screen.PrimaryScreen);
                    if (Field<uint>(panel, "appliedAffinity") != Native.WDA_EXCLUDEFROMCAPTURE)
                        Assert.Inconclusive("Q30-01 : le bureau de test a refusé l'affinité Win32.");
                    int created = 0;
                    panel.HandleCreated += (sender, args) => created++;
                    Call(panel, "RecreateHandle");
                    Assert.AreEqual(1, created);
                    Assert.IsTrue(panel.Visible);
                    Assert.AreEqual(Native.WDA_EXCLUDEFROMCAPTURE, Field<uint>(panel, "appliedAffinity"),
                        "Q30-01 : le nouveau handle visible a perdu l'affinité.");
                    panel.HidePanel();
                }
            }
            finally
            {
                Settings.Animations = animation;
                Settings.HidePanelInCapture = hideCapture;
                Settings.Glass = glass;
                Settings.Widgets = widgets;
                Settings.WeatherOn = weather;
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q30_02_HorizontalArrowsSetAToggle()
    {
        RunSta(() =>
        {
            using (var dialog = new ThemedDialog("QA", 300, 160))
            using (var toggle = new Toggle())
            {
                dialog.Controls.Add(toggle);
                IntPtr handle = dialog.Handle;   // cycle de vie normal : sans handle, WinForms ne retire jamais la fenêtre de OpenForms
                Call(dialog, "OnLoad", EventArgs.Empty);
                int changed = 0;
                toggle.Changed += (sender, args) => changed++;
                Call(toggle, "OnKeyDown", new KeyEventArgs(Keys.Right));
                Assert.IsTrue(toggle.Checked, "Q30-02 : Droite est capturée mais n'active pas l'interrupteur.");
                Call(toggle, "OnKeyDown", new KeyEventArgs(Keys.Left));
                Assert.IsFalse(toggle.Checked);
                Assert.AreEqual(2, changed);
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q30_03_CleanupDialogFitsA768PixelWorkAreaAt150Percent()
    {
        RunSta(() =>
        {
            float originalScale = Ui.Scale;
            try
            {
                Ui.Scale = 1.5f;
                using (BureauCache.Tests.LanguageScope.Spanish())
                using (var dialog = NewDialog<CleanupDialog>())
                {
                    int footer = dialog.Controls.OfType<FlatButton>().Max(button => button.Bottom);
                    Assert.IsTrue(footer <= dialog.ClientSize.Height);
                    Assert.IsTrue(dialog.Height <= 768,
                        "Q30-03 : le dialogue fait " + dialog.Height + " px cadre compris pour une zone de 768 px.");
                }
            }
            finally { Ui.Scale = originalScale; }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q30_03_CleanupRejectsUserCloseOnlyWhileDeleting()
    {
        RunSta(() =>
        {
            using (var dialog = NewDialog<CleanupDialog>())
            {
                var field = typeof(CleanupDialog).GetField("cleaning", PrivateInstance);
                field.SetValue(dialog, true);
                var during = new FormClosingEventArgs(CloseReason.UserClosing, false);
                Call(dialog, "OnFormClosing", during);
                Assert.IsTrue(during.Cancel, "Q30-03 : la suppression doit bloquer la fermeture utilisateur.");
                field.SetValue(dialog, false);
                var after = new FormClosingEventArgs(CloseReason.UserClosing, false);
                Call(dialog, "OnFormClosing", after);
                Assert.IsFalse(after.Cancel);
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q30_04_WeatherErrorDoesNotOverlapPrivacyNotice()
    {
        RunSta(() =>
        {
            using (BureauCache.Tests.LanguageScope.Spanish())
            using (var dialog = NewDialog<WeatherPlaceDialog>())
            {
                Label status = Field<Label>(dialog, "status");
                Label privacy = dialog.Controls.OfType<Label>().OrderByDescending(label => label.Text.Length).First();
                var reason = new System.Net.WebException("The remote server could not be reached after several attempts.");
                status.Text = L.T("Recherche impossible : ", "Search failed: ", "Error de búsqueda: ") + reason.Message;
                Assert.IsTrue(status.Bottom <= privacy.Top,
                    "Q30-04 : le message d'erreur recouvre le texte de confidentialité.");
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q30_05_LegacyImportDoesNotInheritTheCurrentRecentsCutoff()
    {
        string file = Path.GetTempFileName();
        long previousCutoff = Settings.RecentsSince;
        bool hideIcons = Settings.HideIcons, hideCapture = Settings.HidePanelInCapture;
        try
        {
            Settings.RecentsSince = DateTime.Now.AddMinutes(-1).Ticks;
            File.WriteAllText(file, "HideIcons=" + (hideIcons ? "1" : "0") + Environment.NewLine);
            Assert.IsTrue(Settings.Load(file));
            Assert.AreEqual(0L, Settings.RecentsSince,
                "Q30-05 : le filtre absent d'un ancien INI a été repris du profil précédent.");
        }
        finally
        {
            Settings.RecentsSince = previousCutoff;
            Settings.HideIcons = hideIcons;
            Settings.HidePanelInCapture = hideCapture;
            File.Delete(file);
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q30_06_ClearedRecentsDoNotShowAnImportedFutureUsageDate()
    {
        RunSta(() =>
        {
            var previous = Settings.Usage;
            long cutoff = Settings.RecentsSince;
            bool show = Settings.ShowRecents;
            const string key = @"C:\FakeDesktop\future.txt";
            try
            {
                Settings.ShowRecents = true;
                long future = DateTime.Now.AddHours(1).Ticks;
                Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { key, new long[] { 2, future } }
                };
                Settings.ClearRecents();
                Assert.AreEqual(future, Settings.Usage[key][1], "La date d'usage reste disponible au rangement.");
                using (var panel = new PanelForm())
                {
                    panel.All.Add(new DesktopItem { Name = "future.txt", Key = key, Target = key, AutoCategory = "Fichiers" });
                    var groups = (List<Group>)Call(panel, "BuildGroups");
                    Assert.IsFalse(groups.Any(group => group.Key == "recent" && group.Items.Any(item => item.Key == key)),
                        "Q30-06 : une date en avance laisse un élément visible après l'effacement.");
                }
            }
            finally
            {
                Settings.Usage = previous;
                Settings.RecentsSince = cutoff;
                Settings.ShowRecents = show;
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q30_07_UndoingAnOrganisationChangeKeepsANewerUsageDate()
    {
        var usage = Settings.Usage;
        var favorites = Settings.Favorites;
        var order = Settings.Order;
        var categories = Settings.CatOverride;
        long cutoff = Settings.RecentsSince;
        bool saveBlocked = Settings.SaveBlocked;
        const string key = @"C:\FakeDesktop\report.txt";
        try
        {
            Settings.SaveBlocked = true;
            Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase)
            {
                { key, new long[] { 2, DateTime.Now.AddDays(-90).Ticks } }
            };
            Settings.Snap snap = Settings.Snapshot();
            Settings.ClearRecents();
            Settings.RecordUse(key);
            long reopened = Settings.Usage[key][1];
            Settings.Restore(snap);
            Assert.AreEqual(reopened, Settings.Usage[key][1],
                "Q30-07 : l'annulation a supprimé l'usage récent qui protège du rangement.");
        }
        finally
        {
            Settings.CancelPendingSave();
            Settings.Usage = usage;
            Settings.Favorites = favorites;
            Settings.Order = order;
            Settings.CatOverride = categories;
            Settings.RecentsSince = cutoff;
            Settings.SaveBlocked = saveBlocked;
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    [Ignore("Q30-08 : ouvre et referme une fenêtre modale WinForms dans une session interactive.")]
    public void Q30_08_DisablingWeatherBeforeTheQueuedCallbackCancelsLocationPrompt()
    {
        RunSta(() =>
        {
            bool on = Settings.WeatherOn, saveBlocked = Settings.SaveBlocked;
            string place = Settings.WeatherPlace;
            int dialogs = 0;
            try
            {
                Settings.SaveBlocked = true;
                Settings.WeatherOn = false;
                Settings.WeatherPlace = "";
                using (var form = new SettingsForm(new SettingsActions()))
                using (var timer = new System.Windows.Forms.Timer { Interval = 30 })
                {
                    IntPtr handle = form.Handle;
                    timer.Tick += (sender, args) =>
                    {
                        WeatherPlaceDialog dialog = Application.OpenForms.OfType<WeatherPlaceDialog>().FirstOrDefault();
                        if (dialog == null) return;
                        dialogs++;
                        dialog.Close();
                    };
                    timer.Start();
                    Toggle toggle = Field<Toggle>(form, "tWeather");
                    // Correctif retenu (QA-31) : sans lieu, la météo n'est activée que par le choix d'un lieu
                    Call(toggle, "OnClick", EventArgs.Empty);
                    Assert.IsFalse(Settings.WeatherOn);
                    Call(toggle, "OnClick", EventArgs.Empty);
                    Assert.IsFalse(Settings.WeatherOn);
                    Application.DoEvents();
                    Assert.AreEqual(0, dialogs, "Q30-08 : une activation déjà annulée ouvre encore le choix du lieu.");
                }
            }
            finally
            {
                Settings.CancelPendingSave();
                Settings.SaveBlocked = saveBlocked;
                Settings.WeatherOn = on;
                Settings.WeatherPlace = place;
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q30_08_DestroyedSettingsWindowDoesNotLeaveWeatherOnWithoutALocation()
    {
        RunSta(() =>
        {
            bool on = Settings.WeatherOn, saveBlocked = Settings.SaveBlocked;
            string place = Settings.WeatherPlace;
            try
            {
                Settings.SaveBlocked = true;
                Settings.WeatherOn = false;
                Settings.WeatherPlace = "";
                using (var form = new SettingsForm(new SettingsActions()))
                {
                    IntPtr handle = form.Handle;
                    Call(Field<Toggle>(form, "tWeather"), "OnClick", EventArgs.Empty);
                    form.Dispose();
                    Application.DoEvents();
                    Assert.IsFalse(Settings.WeatherOn,
                        "Q30-08 : l'activation reste enregistrée après la perte du rappel de choix du lieu.");
                }
            }
            finally
            {
                Settings.CancelPendingSave();
                Settings.SaveBlocked = saveBlocked;
                Settings.WeatherOn = on;
                Settings.WeatherPlace = place;
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q30_04_EnterInWeatherResultsChoosesTheSelectedPlace()
    {
        RunSta(() =>
        {
            bool on = Settings.WeatherOn, saveBlocked = Settings.SaveBlocked;
            string place = Settings.WeatherPlace;
            double lat = Settings.WeatherLat, lon = Settings.WeatherLon;
            try
            {
                Settings.SaveBlocked = true;
                using (var dialog = NewDialog<WeatherPlaceDialog>())
                {
                    var choice = new WeatherPlace { Name = "Ville QA", Lat = 50.0, Lon = 4.0 };
                    Call(dialog, "ShowResults", new List<WeatherPlace> { choice });
                    ListBox results = Field<ListBox>(dialog, "results");
                    Message message = Message.Create(results.Handle, 0x100, (IntPtr)Keys.Enter, (IntPtr)1);
                    if (results.PreProcessControlMessage(ref message) != PreProcessControlState.MessageProcessed)
                    {
                        typeof(Control).GetMethod("WndProc", PrivateInstance)
                            .Invoke(results, new object[] { message });
                    }
                    Assert.AreEqual(choice.Label, Settings.WeatherPlace, "Q30-04 : Entrée doit valider le lieu sélectionné.");
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult);
                }
            }
            finally
            {
                Settings.CancelPendingSave();
                Settings.SaveBlocked = saveBlocked;
                Settings.WeatherOn = on;
                Settings.WeatherPlace = place;
                Settings.WeatherLat = lat;
                Settings.WeatherLon = lon;
            }
        });
    }

    static T NewDialog<T>() where T : Form
    {
        return (T)Activator.CreateInstance(typeof(T), true);
    }

    static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    }

    static object Call(object target, string name, params object[] args)
    {
        return target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);
    }

    static void RunSta(Action work)
    {
        Exception error = null;
        var thread = new Thread(() =>
        {
            try { work(); }
            catch (Exception ex) { error = ex; }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(12000), "Le scénario WinForms a dépassé douze secondes.");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
