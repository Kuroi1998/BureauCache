using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BureauCache.Tests;

// T-042 : liste des notifications de Windows dans le panneau de la barre (D-018)
[TestClass]
public class NotificationListTests
{
    static void RunSta(Action body)
    {
        Exception error = null;
        Thread t = new Thread(() => { try { body(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    static List<Native.SystemNotification> Sample()
    {
        DateTime now = DateTime.Now;
        return new List<Native.SystemNotification>
        {
            new Native.SystemNotification { Id = 3, AppId = "Discord", AppName = "Discord", Title = "Nouveau message", Body = "Salut, tu viens ce soir ?", Time = now },
            new Native.SystemNotification { Id = 2, AppId = "Claude", AppName = "Claude", Title = "Réponse prête", Body = new string('x', 400), Time = now.AddMinutes(-5) },
            new Native.SystemNotification { Id = 1, AppId = "Discord", AppName = "Discord", Title = "Appel manqué", Time = now.AddDays(-1) },
        };
    }

    [TestMethod]
    public void TextFromOtherAppsIsBoundedAndCleaned()
    {
        Assert.IsNull(Native.CleanNotificationText(null, 10, false));
        Assert.IsNull(Native.CleanNotificationText(" \t ", 10, false));
        Assert.AreEqual("a b", Native.CleanNotificationText("a\u0007b", 10, false));
        Assert.AreEqual("ligne 1\nligne 2", Native.CleanNotificationText("ligne 1\nligne 2", 50, true));
        Assert.AreEqual("ligne 1 ligne 2", Native.CleanNotificationText("ligne 1\nligne 2", 50, false));
        string longText = Native.CleanNotificationText(new string('y', 5000), Native.MaxNotificationBody, true);
        Assert.AreEqual(Native.MaxNotificationBody, longText.Length);
        Assert.IsTrue(longText.EndsWith("…"));
    }

    [TestMethod]
    public void OnlyInstalledAppIdsAreOpened()
    {
        Assert.IsTrue(NotificationsForm.IsLaunchableAppId("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"));
        Assert.IsTrue(NotificationsForm.IsLaunchableAppId(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Discord\Discord.exe"));
        Assert.IsTrue(NotificationsForm.IsLaunchableAppId("com.squirrel.Discord.Discord"));
        Assert.IsFalse(NotificationsForm.IsLaunchableAppId(null));
        Assert.IsFalse(NotificationsForm.IsLaunchableAppId(@"C:\Windows\System32\cmd.exe"), "Chemin absolu : pas une application installée.");
        Assert.IsFalse(NotificationsForm.IsLaunchableAppId(@"..\..\Windows\System32\cmd.exe"));
        Assert.IsFalse(NotificationsForm.IsLaunchableAppId("app\" /c calc"));
        Assert.IsFalse(NotificationsForm.IsLaunchableAppId(@"\\serveur\partage\app.exe"));
    }

    [TestMethod]
    public void ListGroupsByAppAndHandlesKeyboard()
    {
        RunSta(() =>
        {
            using (NotificationList list = new NotificationList { Width = 360, Height = 300 })
            {
                list.SetItems(Sample());
                Assert.AreEqual(3, list.Count);
                Assert.IsTrue(list.ContentHeight > 0);
                List<uint> opened = new List<uint>(), dismissed = new List<uint>();
                list.Opened += n => opened.Add(n.Id);
                list.Dismissed += n => dismissed.Add(n.Id);
                Assert.IsTrue(list.HandleKey(Keys.Down));
                Assert.AreEqual(3u, list.SelectedItem.Id, "Discord d'abord : le plus récent.");
                Assert.IsTrue(list.HandleKey(Keys.Down));
                Assert.AreEqual(1u, list.SelectedItem.Id, "Regroupées par application : l'appel manqué de Discord suit.");
                Assert.IsTrue(list.HandleKey(Keys.Down));
                Assert.AreEqual(2u, list.SelectedItem.Id);
                Assert.IsTrue(list.HandleKey(Keys.Enter));
                Assert.IsTrue(list.HandleKey(Keys.Delete));
                CollectionAssert.AreEqual(new[] { 2u }, opened);
                CollectionAssert.AreEqual(new[] { 2u }, dismissed);
                list.Remove(2);
                Assert.AreEqual(2, list.Count);
                Assert.AreEqual(1u, list.SelectedItem.Id, "La sélection passe à la notification d'à côté.");
                list.SetItems(Sample());
                Assert.AreEqual(1u, list.SelectedItem.Id, "Une relecture garde la sélection.");
            }
        });
    }

    [TestMethod]
    public void PanelShowsTheListAndEveryFallback()
    {
        RunSta(() =>
        {
            foreach (Func<LanguageScope> scope in new Func<LanguageScope>[] { LanguageScope.French, LanguageScope.English, LanguageScope.Spanish })
                using (scope())
                using (NotificationsForm form = new NotificationsForm())
                {
                    form.Location = new Point(-4000, -4000);   // affiché hors écran : Visible a alors un sens
                    form.Show();
                    form.ShowFeed(Native.NotificationAccess.Allowed, Sample());
                    Assert.IsTrue(form.List.Height > 1, "La liste a sa place.");
                    Assert.AreEqual(3, form.List.Count);
                    CheckFits(form);
                    int full = form.List.ContentHeight;

                    typeof(NotificationsForm).GetMethod("Dismiss", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { form.List.SelectedItem ?? Sample()[0] });
                    Assert.AreEqual(2, form.List.Count);
                    Assert.IsTrue(form.List.ContentHeight < full, "Une notification effacée libère sa place.");

                    typeof(NotificationsForm).GetMethod("ClearAll", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
                    Assert.AreEqual(0, form.List.Count);
                    CheckFits(form);

                    form.ShowFeed(Native.NotificationAccess.Denied, null);
                    CheckFits(form);
                    form.ShowFeed(Native.NotificationAccess.Unavailable, null);
                    CheckFits(form);
                    form.ShowFeed(null, null);
                    CheckFits(form);
                }
        });
    }

    static void CheckFits(NotificationsForm form)
    {
        foreach (StartTile tile in form.Controls.OfType<StartTile>().Where(t => t.Visible))
        {
            Assert.IsTrue(form.ClientRectangle.Contains(tile.Bounds), L.CurrentLanguage + " : " + tile.Text);
            if (!tile.GlyphOnly) Assert.IsTrue(tile.TextFits, L.CurrentLanguage + " : " + tile.Text);
            else Assert.IsFalse(string.IsNullOrEmpty(tile.AccessibleName), "Bouton icône seule sans nom.");
        }
        foreach (Label label in form.Controls.OfType<Label>().Where(l => l.Visible))
        {
            Assert.IsTrue(form.ClientRectangle.Contains(label.Bounds), label.Text);
            Size need = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, int.MaxValue), TextFormatFlags.WordBreak);
            Assert.IsTrue(need.Height <= label.Height, L.CurrentLanguage + " : texte rogné « " + label.Text + " »");
        }
    }
}
