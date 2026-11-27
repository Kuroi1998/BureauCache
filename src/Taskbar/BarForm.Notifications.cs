// BureauCache — panneaux de l'horloge (calendrier) et de la cloche (notifications) (T-042, D-018)
using System;
using System.Drawing;
using System.Windows.Forms;

partial class BarForm
{
    BarPopup popup;          // calendrier ou notifications : un seul ouvert à la fois
    int popupZone = -1;

    // Horloge : calendrier ; cloche : notifications. Un second clic sur le même bouton ferme son panneau, un clic sur
    // l'autre bouton passe à l'autre panneau.
    void TogglePopup(int zone)
    {
        if (Sandbox.Active) return;
        bool same = popup != null && popupZone == zone;
        ClosePopup();
        if (same) return;
        HidePreview();
        Rectangle anchor = RectangleToScreen(zones[zone]);
        BarPopup form = zone == 2 ? (BarPopup)new CalendarForm() : new NotificationsForm();
        popup = form;
        popupZone = zone;
        form.FormClosed += (s, e) =>
        {
            if (popup == form) { popup = null; popupZone = -1; }
            Invalidate();
        };
        form.PlaceAbove(anchor, Screen.FromRectangle(anchor).WorkingArea);
        form.Show(this);
        form.Activate();
        Native.SetForegroundWindow(form.Handle);
        Invalidate();
    }

    void ClosePopup()
    {
        if (popup != null) popup.Close();
    }

    // Panneaux ouverts depuis la barre : fermés quand elle bouge, change de thème ou disparaît
    void ClosePanels()
    {
        CloseQuickSettings();
        ClosePopup();
    }

    bool PanelOpen { get { return quickSettings != null || popup != null; } }
}
