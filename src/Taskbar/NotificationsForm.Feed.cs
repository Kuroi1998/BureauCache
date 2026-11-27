// BureauCache — lecture des notifications de Windows pour le panneau de la barre (T-042, D-018)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

partial class NotificationsForm
{
    // Relue toutes les 3 s tant que le panneau est ouvert : une application de bureau n'est pas prévenue des nouvelles
    // notifications sans tâche d'arrière-plan empaquetée
    readonly Timer feed = new Timer { Interval = 3000 };
    readonly Dictionary<string, Bitmap> logos = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
    bool reading;
    static Task mutation;
    string notesActionUri, shownState;

    async void ReadFeed()
    {
        if (Sandbox.Active || reading || IsDisposed || (mutation != null && !mutation.IsCompleted)) return;
        string[] known = logos.Keys.ToArray();
        Task observedMutation = mutation;
        reading = true;
        Tuple<List<Native.SystemNotification>, Native.NotificationAccess> result;
        try
        {
            result = await Task.Run(() =>
            {
                Native.NotificationAccess access;
                List<Native.SystemNotification> list = Native.ReadNotifications(known, out access);
                return Tuple.Create(list, access);
            });
        }
        catch (Exception)
        {
            result = Tuple.Create((List<Native.SystemNotification>)null, Native.NotificationAccess.Unavailable);
        }
        finally { reading = false; }
        ApplyFeedResult(observedMutation, result.Item2, result.Item1);
    }

    void ApplyFeedResult(Task observedMutation, Native.NotificationAccess access, List<Native.SystemNotification> list)
    {
        if (IsDisposed || mutation != observedMutation) DisposeLogos(list);
        else ShowFeed(access, list);
    }

    static void QueueMutation(Action change)
    {
        if (Sandbox.Active) return;
        mutation = mutation == null ? Task.Run(change) : mutation.ContinueWith(t => change(), TaskScheduler.Default);
    }

    static void DisposeLogos(List<Native.SystemNotification> list)
    {
        if (list == null) return;
        foreach (Native.SystemNotification n in list) if (n.Logo != null) { n.Logo.Dispose(); n.Logo = null; }
    }

    // null : première lecture en cours. Ne refait la mise en page que si quelque chose a changé.
    internal void ShowFeed(Native.NotificationAccess? access, List<Native.SystemNotification> list)
    {
        if (list != null)
            foreach (Native.SystemNotification n in list)
            {
                if (n.Logo == null) continue;
                if (n.AppId == null || logos.ContainsKey(n.AppId)) n.Logo.Dispose();
                else logos[n.AppId] = n.Logo;
                n.Logo = null;
            }
        bool allowed = access == Native.NotificationAccess.Allowed && list != null;
        string state = (access.HasValue ? access.Value.ToString() : "…") + "|" + (allowed ? Signature(list) : string.Empty);
        if (state == shownState) return;
        shownState = state;

        notesActionUri = null;
        string text = null, action = null;
        if (!access.HasValue) text = L.T("Lecture des notifications…", "Reading notifications…", "Leyendo notificaciones…");
        else if (allowed)
        {
            if (list.Count == 0) text = L.T("Aucune nouvelle notification.", "No new notifications.", "No hay notificaciones nuevas.");
        }
        else if (access == Native.NotificationAccess.Denied)
        {
            text = L.T("BureauCache n'a pas accès à tes notifications. Autorise « Accès aux notifications » dans les paramètres de confidentialité de Windows.",
                "BureauCache can't access your notifications. Turn on \"Notification access\" in Windows privacy settings.",
                "BureauCache no tiene acceso a tus notificaciones. Activa «Acceso a notificaciones» en la configuración de privacidad de Windows.");
            action = L.T("Paramètres de confidentialité", "Privacy settings", "Configuración de privacidad");
            notesActionUri = "ms-settings:privacy-notifications";
        }
        else
        {
            text = L.T("Windows garde les notifications des applications. Ouvre-les dans son panneau.",
                "Windows keeps your app notifications. Open them in its panel.",
                "Windows guarda las notificaciones de las aplicaciones. Ábrelas en su panel.");
            action = L.T("Ouvrir les notifications de Windows", "Open Windows notifications", "Abrir las notificaciones de Windows");
        }
        List.SetItems(allowed ? list : null);
        showList = allowed && list.Count > 0;
        showText = text != null;
        notesText.Text = text ?? string.Empty;
        showAction = action != null;
        notesAction.Text = action ?? string.Empty;
        notesAction.GlyphOnly = false;
        notesAction.SetGlyph(notesActionUri != null ? Glyphs.Shield : Glyphs.Bell);
        Arrange();
    }

    static string Signature(List<Native.SystemNotification> list)
    {
        StringBuilder b = new StringBuilder();
        foreach (Native.SystemNotification n in list)
        {
            b.Append(n.Id).Append('/').Append(n.Time.ToBinary()).Append('/');
            AppendField(b, n.AppId);
            AppendField(b, n.AppName);
            AppendField(b, n.Title);
            AppendField(b, n.Body);
        }
        return b.ToString();
    }

    static void AppendField(StringBuilder signature, string value)
    {
        signature.Append(value == null ? -1 : value.Length).Append(':').Append(value);
    }

    Image LogoOf(string appId)
    {
        Bitmap logo;
        return appId != null && logos.TryGetValue(appId, out logo) ? logo : null;
    }

    void Dismiss(Native.SystemNotification n)
    {
        uint id = n.Id;
        List.Remove(id);
        AfterLocalChange();
        QueueMutation(() => Native.RemoveNotification(id));
    }

    void ClearAll()
    {
        List.SetItems(new List<Native.SystemNotification>());
        AfterLocalChange();
        QueueMutation(() => Native.ClearNotifications());
        List.Focus();
    }

    // La liste a changé ici avant que Windows confirme : affichage tout de suite, la prochaine lecture recale
    void AfterLocalChange()
    {
        bool empty = List.Count == 0;
        showList = !empty;
        showText = empty;
        notesText.Text = empty ? L.T("Aucune nouvelle notification.", "No new notifications.", "No hay notificaciones nuevas.") : string.Empty;
        shownState = null;
        Arrange();
        if (empty) notifySettings.Focus();
    }

    // Clic sur une notification : comme chez Windows, elle disparaît et son application s'ouvre
    void OpenApp(Native.SystemNotification n)
    {
        if (!IsLaunchableAppId(n.AppId)) return;
        uint id = n.Id;
        QueueMutation(() => Native.RemoveNotification(id));
        Launch(@"shell:AppsFolder\" + n.AppId);
    }

    // Identifiant venu de Windows pour une autre application : on n'ouvre que la liste des applications installées
    internal static bool IsLaunchableAppId(string appId)
    {
        if (string.IsNullOrEmpty(appId) || appId.Length > 300 || appId.Contains("..") || appId.StartsWith("\\") || appId.Contains(":")) return false;
        foreach (char c in appId) if (char.IsControl(c) || c == '"' || c == '<' || c == '>' || c == '|' || c == '*' || c == '?') return false;
        return true;
    }

    void DisposeFeed()
    {
        feed.Dispose();
        foreach (Bitmap logo in logos.Values) logo.Dispose();
        logos.Clear();
    }
}
