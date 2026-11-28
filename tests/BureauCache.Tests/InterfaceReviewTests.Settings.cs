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

public partial class InterfaceReviewTests
{
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
#if !WINDOWS_INTERACTIVE_TESTS
    [Ignore("Q30-08 : ouvre et referme une fenêtre modale WinForms dans une session interactive.")]
#endif
    [TestCategory("WindowsInteractive")]
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
}
