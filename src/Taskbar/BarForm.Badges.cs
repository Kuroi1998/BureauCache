// BureauCache — BarForm : badges (« 1 » de Discord) et barres de progression des applications (D-016, T-041)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

partial class BarForm
{
    // Messages envoyés à la fenêtre « TaskbandHWND » par ITaskbarList3 (valeurs relevées dans ManagedShell, non
    // documentées par Microsoft) : wParam = fenêtre de l'application
    internal const int MsgProgressValue = 0x400 + 64, MsgProgressState = 0x400 + 65, MsgOverlayIcon = 0x400 + 79, MsgOverlayText = 0x400 + 85;
    internal const int ProgressNone = 0, ProgressIndeterminate = 1, ProgressNormal = 2, ProgressError = 4, ProgressPaused = 8;
    internal const int ProgressScale = 65534;   // échelle de la valeur, comme RetroBar

    internal sealed class Badge
    {
        public Bitmap Icon;
        public string Text;
        public int State, Value;
        public bool Empty { get { return Icon == null && State == ProgressNone; } }
    }

    readonly Dictionary<IntPtr, Badge> badges = new Dictionary<IntPtr, Badge>();

    void StartBadges()
    {
        if (Sandbox.Active || !TaskbandRoute.Install(Handle)) return;

        // Les fenêtres déjà ouvertes ont pu envoyer leur badge à Explorer avant le remplacement de sa barre.
        uint created = Native.RegisterWindowMessage("TaskbarButtonCreated");
        if (created == 0) return;
        foreach (TaskWindow window in windows.Items)
            Native.PostMessage(window.Handle, (int)created, IntPtr.Zero, IntPtr.Zero);
    }

    void StopBadges()
    {
        TaskbandRoute.Restore();
        foreach (Badge b in badges.Values) if (b.Icon != null) b.Icon.Dispose();
        badges.Clear();
    }

    // Vrai si le message venait d'ITaskbarList3 et a été traité
    // PROTOTYPE (T-041) : journal des messages WM_USER à WM_USER + 255 reçus par la barre, pour savoir ce que Windows
    // envoie vraiment. Numéro du message et identifiants de fenêtres seulement. À retirer avant toute publication.
    static int loggedMessages;

    static void LogTaskbarMessage(Message m)
    {
        if (Sandbox.Active || loggedMessages >= 500 || m.Msg < 0x400 || m.Msg > 0x4FF) return;
        loggedMessages++;
        try
        {
            System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BureauCache-badges.log"),
                DateTime.Now.ToString("HH:mm:ss.fff") + "  WM_USER+" + (m.Msg - 0x400) + "  wParam=0x" + m.WParam.ToInt64().ToString("X") +
                "  lParam=0x" + m.LParam.ToInt64().ToString("X") + "  route=" + TaskbandRoute.Installed + "\r\n");
        }
        catch { }
    }

    bool HandleTaskbarList(ref Message m)
    {
        LogTaskbarMessage(m);
        if (m.Msg != MsgProgressValue && m.Msg != MsgProgressState && m.Msg != MsgOverlayIcon && m.Msg != MsgOverlayText) return false;
        if (!TaskbandRoute.Installed) return false;
        IntPtr hwnd = m.WParam;
        m.Result = IntPtr.Zero;
        if (!Native.IsWindow(hwnd)) return true;
        Badge b;
        if (!badges.TryGetValue(hwnd, out b)) badges[hwnd] = b = new Badge();
        switch (m.Msg)
        {
            case MsgOverlayIcon:
                if (b.Icon != null && m.LParam == IntPtr.Zero && windows != null) windows.ClearDiscordAlerts(hwnd);
                if (b.Icon != null) { b.Icon.Dispose(); b.Icon = null; }
                // L'icône appartient à l'application : on en convertit une copie (comme CDX-07)
                if (m.LParam != IntPtr.Zero)
                {
                    IntPtr copy = Native.CopyIcon(m.LParam);
                    b.Icon = Icons.FromHIcon(copy);
                }
                else b.Text = null;
                break;
            case MsgOverlayText:
                uint pid;
                Native.GetWindowThreadProcessId(hwnd, out pid);
                b.Text = Native.SharedText(m.LParam, pid);
                break;
            case MsgProgressState:
                b.State = (int)(m.LParam.ToInt64() & 0xFF);
                if (b.State == ProgressNone) b.Value = 0;
                break;
            case MsgProgressValue:
                b.Value = (int)Math.Max(0, Math.Min(ProgressScale, m.LParam.ToInt64()));
                if (b.State == ProgressNone) b.State = ProgressNormal;   // une valeur sans état préalable s'affiche quand même
                break;
        }
        if (b.Empty) { badges.Remove(hwnd); }
        Invalidate();
        return true;
    }

    // Fenêtres fermées : leurs badges disparaissent
    void ForgetClosedWindowBadges()
    {
        foreach (IntPtr h in badges.Keys.Where(k => !Native.IsWindow(k)).ToList())
        {
            if (badges[h].Icon != null) badges[h].Icon.Dispose();
            badges.Remove(h);
        }
    }

    internal static Color ProgressColor(int state)
    {
        if ((state & ProgressError) != 0) return Color.FromArgb(232, 17, 35);
        if ((state & ProgressPaused) != 0) return Color.FromArgb(252, 202, 0);
        return Color.FromArgb(6, 176, 37);
    }

    // Largeur remplie de la barre de progression (indéterminée : un tiers, qui ne bouge pas dans ce prototype)
    internal static int ProgressWidth(int state, int value, int full)
    {
        if (state == ProgressNone) return 0;
        if ((state & ProgressIndeterminate) != 0) return full / 3;
        return (int)Math.Round(full * Math.Max(0, Math.Min(ProgressScale, value)) / (double)ProgressScale);
    }

    internal static string DiscordAlertText(TaskWindow window, bool hasOverlay)
    {
        if (hasOverlay || window == null || window.DiscordAlerts == 0) return null;
        return window.DiscordAlerts >= TaskWindow.MaxDiscordAlerts
            ? "99+" : window.DiscordAlerts.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    // Badge en haut à droite de l'icône, progression sous l'icône (au-dessus du trait de fenêtre active)
    void PaintBadge(Graphics g, Rectangle button, Rectangle icon, TaskWindow window)
    {
        if (window == null) return;
        Badge b;
        badges.TryGetValue(window.Handle, out b);
        bool hasOverlay = b != null && b.Icon != null;
        string alertText = DiscordAlertText(window, hasOverlay);
        if (hasOverlay)
        {
            int s = Ui.S(16);
            Rectangle r = new Rectangle(icon.Right - s + Ui.S(4), icon.Top - Ui.S(3), s, s);
            try { g.DrawImage(b.Icon, r); } catch { }
        }
        else if (alertText != null)
        {
            int height = Ui.S(17), width = Ui.S(alertText.Length == 1 ? 17 : alertText.Length == 2 ? 21 : 26);
            var bounds = new Rectangle(icon.Right - width + Ui.S(4), icon.Top - Ui.S(3), width, height);
            Ui.Fill(g, Theme.Bg, bounds, height / 2f);
            bounds.Inflate(-Ui.S(1), -Ui.S(1));
            Ui.Fill(g, Color.FromArgb(196, 34, 49), bounds, bounds.Height / 2f);
            TextRenderer.DrawText(g, alertText, Ui.Font("Segoe UI Semibold", 8f), bounds, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        if (b != null && b.State != ProgressNone)
        {
            int full = button.Width - Ui.S(16), h = Ui.S(3), y = button.Bottom - Ui.S(9);
            Ui.Fill(g, Color.FromArgb(70, 128, 128, 128), new RectangleF(button.Left + Ui.S(8), y, full, h), Ui.S(1));
            int w = ProgressWidth(b.State, b.Value, full);
            if (w > 0) Ui.Fill(g, ProgressColor(b.State), new RectangleF(button.Left + Ui.S(8), y, w, h), Ui.S(1));
        }
    }

    // Description du badge pour l'infobulle (« 1 message non lu »…), ou null
    string BadgeText(IntPtr hwnd)
    {
        Badge b;
        return hwnd != IntPtr.Zero && badges.TryGetValue(hwnd, out b) && b.Icon != null && !string.IsNullOrEmpty(b.Text) ? b.Text : null;
    }
}
