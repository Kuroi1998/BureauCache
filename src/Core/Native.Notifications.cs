// BureauCache — notifications du centre de Windows, lues par UserNotificationListener (T-042, D-018)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;

// Essai du 2026-11-27 sur le PC du chef de projet (Windows 11, build 26300) : un processus de bureau non empaqueté
// obtient l'accès « Allowed » et lit les notifications (application, heure, textes). Le contenu des notifications des
// autres applications est seulement affiché : jamais écrit sur disque, dans un journal ni envoyé ailleurs (D-018).
static partial class Native
{
    internal enum NotificationAccess { Unavailable, Unspecified, Denied, Allowed }

    internal sealed class SystemNotification
    {
        public uint Id;
        public string AppName, AppId, Title, Body;
        public DateTime Time;
        public Bitmap Logo;   // seulement pour une application dont le logo n'était pas encore connu
    }

    internal const int MaxNotifications = 100, MaxNotificationTitle = 200, MaxNotificationBody = 600;
    const string NotificationsRuntime = ", Windows.UI.Notifications, ContentType=WindowsRuntime";

    static class NotificationTypes
    {
        internal static readonly Type Listener = Type.GetType("Windows.UI.Notifications.Management.UserNotificationListener" + NotificationsRuntime, false);
        internal static readonly Type Kinds = Type.GetType("Windows.UI.Notifications.NotificationKinds" + NotificationsRuntime, false);
        internal static readonly Type KnownBindings = Type.GetType("Windows.UI.Notifications.KnownNotificationBindings" + NotificationsRuntime, false);
    }

    static object NotificationListener()
    {
        Type t = NotificationTypes.Listener;
        return t == null ? null : t.GetProperty("Current").GetValue(null, null);
    }

    internal static NotificationAccess NotificationAccessStatus()
    {
        if (Sandbox.Active) return NotificationAccess.Unavailable;
        try
        {
            object listener = NotificationListener();
            if (listener == null) return NotificationAccess.Unavailable;
            string status = NotificationTypes.Listener.GetMethod("GetAccessStatus").Invoke(listener, null).ToString();
            return status == "Allowed" ? NotificationAccess.Allowed : status == "Denied" ? NotificationAccess.Denied : NotificationAccess.Unspecified;
        }
        catch (Exception) { return NotificationAccess.Unavailable; }
    }

    // Notifications du centre de Windows, les plus récentes d'abord, ou null si la lecture n'est pas permise.
    // knownLogos : applications dont le logo est déjà affiché (il n'est pas relu). Hors du fil d'interface.
    internal static List<SystemNotification> ReadNotifications(ICollection<string> knownLogos, out NotificationAccess access)
    {
        access = NotificationAccessStatus();
        if (access != NotificationAccess.Allowed || WinRtTypes.AsyncInfo == null || NotificationTypes.Kinds == null) return null;
        try
        {
            object listener = NotificationListener();
            MethodInfo get = NotificationTypes.Listener.GetMethod("GetNotificationsAsync");
            IEnumerable found = WinRtResult(get, listener, new[] { Enum.Parse(NotificationTypes.Kinds, "Toast") }, 3000) as IEnumerable;
            if (found == null) { access = NotificationAccess.Unavailable; return null; }
            Type itemType = get.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0];
            string generic = NotificationTypes.KnownBindings == null ? "ToastGeneric" :
                (string)NotificationTypes.KnownBindings.GetProperty("ToastGeneric").GetValue(null, null);
            HashSet<string> logos = new HashSet<string>(knownLogos ?? new string[0], StringComparer.OrdinalIgnoreCase);
            List<SystemNotification> result = new List<SystemNotification>();
            foreach (object item in found)
            {
                SystemNotification n = ReadNotification(itemType, item, generic, logos);
                if (n != null) result.Add(n);
            }
            result.Sort((a, b) => b.Time.CompareTo(a.Time));
            if (result.Count > MaxNotifications)
            {
                foreach (SystemNotification extra in result.Skip(MaxNotifications)) if (extra.Logo != null) extra.Logo.Dispose();
                result.RemoveRange(MaxNotifications, result.Count - MaxNotifications);
            }
            return result;
        }
        catch (Exception) { access = NotificationAccess.Unavailable; return null; }
    }

    static SystemNotification ReadNotification(Type itemType, object item, string generic, HashSet<string> logos)
    {
        try
        {
            SystemNotification n = new SystemNotification();
            n.Id = (uint)itemType.GetProperty("Id").GetValue(item, null);
            n.Time = ((DateTimeOffset)itemType.GetProperty("CreationTime").GetValue(item, null)).LocalDateTime;
            PropertyInfo appInfo = itemType.GetProperty("AppInfo");
            object app = appInfo.GetValue(item, null);
            if (app != null)
            {
                Type at = appInfo.PropertyType;
                n.AppId = CleanNotificationText((string)at.GetProperty("AppUserModelId").GetValue(app, null), 300, false);
                PropertyInfo display = at.GetProperty("DisplayInfo");
                object info = display.GetValue(app, null);
                if (info != null)
                {
                    n.AppName = CleanNotificationText((string)display.PropertyType.GetProperty("DisplayName").GetValue(info, null), 100, false);
                    if (n.AppId != null && logos.Add(n.AppId)) n.Logo = NotificationLogo(display.PropertyType, info);
                }
            }
            ReadNotificationTexts(itemType, item, generic, n);
            if (string.IsNullOrEmpty(n.AppName)) n.AppName = L.T("Application", "App", "Aplicación");
            return n;
        }
        catch (Exception) { return null; }
    }

    static void ReadNotificationTexts(Type itemType, object item, string generic, SystemNotification n)
    {
        PropertyInfo notification = itemType.GetProperty("Notification");
        object toast = notification.GetValue(item, null);
        if (toast == null) return;
        PropertyInfo visual = notification.PropertyType.GetProperty("Visual");
        object v = visual.GetValue(toast, null);
        if (v == null) return;
        MethodInfo getBinding = visual.PropertyType.GetMethod("GetBinding");
        object binding = getBinding.Invoke(v, new object[] { generic });
        if (binding == null) return;
        MethodInfo getTexts = getBinding.ReturnType.GetMethod("GetTextElements");
        IEnumerable texts = getTexts.Invoke(binding, null) as IEnumerable;
        PropertyInfo text = getTexts.ReturnType.GetGenericArguments()[0].GetProperty("Text");
        if (texts == null || text == null) return;
        List<string> lines = new List<string>();
        foreach (object t in texts)
        {
            string s = text.GetValue(t, null) as string;
            if (!string.IsNullOrWhiteSpace(s)) lines.Add(s.Trim());
            if (lines.Count >= 6) break;
        }
        if (lines.Count == 0) return;
        n.Title = CleanNotificationText(lines[0], MaxNotificationTitle, false);
        n.Body = CleanNotificationText(string.Join("\n", lines.Skip(1)), MaxNotificationBody, true);
    }

    static Bitmap NotificationLogo(Type displayType, object info)
    {
        try
        {
            MethodInfo getLogo = displayType.GetMethod("GetLogo");
            object size = Activator.CreateInstance(getLogo.GetParameters()[0].ParameterType, 32.0, 32.0);
            object reference = getLogo.Invoke(info, new[] { size });
            return reference == null ? null : ReadMediaArtwork(getLogo.ReturnType, reference);
        }
        catch (Exception) { return null; }
    }

    // Texte venu d'une autre application : borné, sans caractères de contrôle (retours à la ligne gardés si permis)
    internal static string CleanNotificationText(string text, int max, bool keepLines)
    {
        if (string.IsNullOrEmpty(text)) return null;
        StringBuilder b = new StringBuilder(Math.Min(text.Length, max + 1));
        foreach (char c in text)
        {
            if (b.Length >= max) break;
            if (c == '\n' && keepLines) b.Append('\n');
            else if (char.IsControl(c)) b.Append(' ');
            else b.Append(c);
        }
        string result = b.ToString().Trim();
        if (text.Length > max && result.Length > 0) result = result.Substring(0, Math.Max(0, result.Length - 1)) + "…";
        return result.Length == 0 ? null : result;
    }

    internal static bool RemoveNotification(uint id)
    {
        if (Sandbox.Active) return false;
        try
        {
            object listener = NotificationListener();
            if (listener == null) return false;
            NotificationTypes.Listener.GetMethod("RemoveNotification").Invoke(listener, new object[] { id });
            return true;
        }
        catch (Exception) { return false; }
    }

    internal static bool ClearNotifications()
    {
        if (Sandbox.Active) return false;
        try
        {
            object listener = NotificationListener();
            if (listener == null) return false;
            NotificationTypes.Listener.GetMethod("ClearNotifications").Invoke(listener, null);
            return true;
        }
        catch (Exception) { return false; }
    }
}
