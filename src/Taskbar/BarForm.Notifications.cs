// BureauCache — ouverture du panneau Notifications et calendrier depuis l'horloge et la cloche (T-042, D-018)
using System;
using System.Drawing;
using System.Windows.Forms;

partial class BarForm
{
    NotificationsForm notifications;
    int notificationsZone = -1;

    // L'horloge ouvre le panneau sur le calendrier, la cloche sur les notifications ; un second clic le ferme
    void ToggleNotifications(int zone)
    {
        if (Sandbox.Active) return;
        if (notifications != null) { CloseNotifications(); return; }
        HidePreview();
        Rectangle anchor = RectangleToScreen(zones[zone]);
        NotificationsForm form = new NotificationsForm(zone == 2);
        notifications = form;
        notificationsZone = zone;
        form.FormClosed += (s, e) =>
        {
            if (notifications == form) { notifications = null; notificationsZone = -1; }
            Invalidate();
        };
        form.PlaceAbove(anchor, Screen.FromRectangle(anchor).WorkingArea);
        form.Show(this);
        form.Activate();
        Native.SetForegroundWindow(form.Handle);
        Invalidate();
    }

    void CloseNotifications()
    {
        if (notifications != null) notifications.Close();
    }

    // Panneaux ouverts depuis la barre : fermés quand elle bouge, change de thème ou disparaît
    void ClosePanels()
    {
        CloseQuickSettings();
        CloseNotifications();
    }

    bool PanelOpen { get { return quickSettings != null || notifications != null; } }
}
