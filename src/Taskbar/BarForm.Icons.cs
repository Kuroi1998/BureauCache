using System;
using System.Drawing;
using System.Windows.Forms;

partial class BarForm
{
    readonly Timer buttonIconTimer = new Timer { Interval = 20 };
    ContextMenuStrip iconOverflow;

    void InitializeButtonIcons() { buttonIconTimer.Tick += (s, e) => LoadNextButtonIcon(); }

    void DisposeButtonIcons()
    {
        buttonIconTimer.Dispose();
        if (iconOverflow != null) iconOverflow.Close();
    }

    void ScheduleButtonIcons()
    {
        if (!Sandbox.Active && !Disposing && !IsDisposed && !buttonIconTimer.Enabled) buttonIconTimer.Start();
    }

    void TrackOverflowIcons(ContextMenuStrip menu)
    {
        iconOverflow = menu;
        menu.Closed += (s, e) => { if (iconOverflow == menu) iconOverflow = null; };
    }

    static Bitmap CachedButtonIcon(BarButton button, int px, out bool pending)
    {
        pending = false;
        Bitmap image = button.Window == null ? null : TaskIcons.CachedWindow(button.Window.Handle, px, out pending);
        if (image != null) return image;
        bool keyPending;
        image = TaskIcons.CachedKey(button.Key, px, out keyPending);
        pending |= keyPending;
        return image;
    }

    void LoadNextButtonIcon()
    {
        if (!Visible || !IsHandleCreated) { buttonIconTimer.Stop(); return; }
        int px = (int)(Ui.S(32) * SizeScale);
        if (!collapsedBar)
        {
            for (int i = 0; i < shown && i < buttons.Count; i++)
            {
                bool pending;
                CachedButtonIcon(buttons[i], px, out pending);
                if (!pending) continue;
                IconOf(buttons[i], px);
                if (i < buttonRects.Count) Invalidate(buttonRects[i]);
                return; // Un seul accès à la fois, hors du dessin de la barre.
            }
        }
        if (!initialTrackingPending) StartupTimeline.Finish("icones_visibles_initiales");
        if (LoadNextOverflowIcon()) return;
        buttonIconTimer.Stop();
    }

    bool LoadNextOverflowIcon()
    {
        if (iconOverflow == null || !iconOverflow.Visible) return false;
        int px = iconOverflow.ImageScalingSize.Width;
        foreach (ToolStripItem item in iconOverflow.Items)
        {
            var button = item.Tag as BarButton;
            if (button == null) continue;
            item.Tag = null;
            Bitmap source = IconOf(button, px);
            if (source != null)
            {
                Bitmap owned = new Bitmap(source);
                item.Image = owned;
                item.Disposed += (s, e) => owned.Dispose();
            }
            return true;
        }
        return false;
    }
}
