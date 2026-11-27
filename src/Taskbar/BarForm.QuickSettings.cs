// BureauCache — ouverture du panneau réseau/son depuis la barre (T-040)
using System;
using System.Drawing;
using System.Windows.Forms;

partial class BarForm
{
    QuickSettingsForm quickSettings;

    void ToggleQuickSettings()
    {
        if (Sandbox.Active) return;
        if (quickSettings != null) { CloseQuickSettings(); return; }
        HidePreview();
        Rectangle anchor = RectangleToScreen(zones[1]);
        QuickSettingsForm form = new QuickSettingsForm(Native.QuickMonitor(Handle));
        quickSettings = form;
        form.FormClosed += (s, e) =>
        {
            if (quickSettings == form) quickSettings = null;
            Invalidate(zones[1]);
        };
        form.PlaceAbove(anchor, Screen.FromRectangle(anchor).WorkingArea);
        form.Show(this);
        form.Activate();
        Native.SetForegroundWindow(form.Handle);
        Invalidate(zones[1]);
    }

    void CloseQuickSettings()
    {
        if (quickSettings != null) quickSettings.Close();
    }
}
