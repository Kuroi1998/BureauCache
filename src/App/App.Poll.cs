// BureauCache — App (détection du bord et boucle Poll)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

partial class App
{
    Screen TargetScreen(Point p)
    {
        Screen[] all = screens;
        int i = Settings.ChosenScreen(all);
        return i >= 0 ? all[i] : Screen.FromPoint(p);
    }

    void ApplyIconVisibility()
    {
        if (listView == IntPtr.Zero || !Native.IsWindow(listView)) listView = Native.FindDesktopListView();
        if (listView == IntPtr.Zero) return;
        bool vis = Native.IsWindowVisible(listView);
        if (Settings.HideIcons && vis) Native.ShowWindow(listView, Native.SW_HIDE);
        else if (!Settings.HideIcons && !vis) Native.ShowWindow(listView, Native.SW_SHOW);
    }

    static bool ScreenExistsAt(Screen[] all, Point p)
    {
        foreach (Screen s in all) if (s.Bounds.Contains(p)) return true;
        return false;
    }

    // Le test du bord tourne 50 fois par seconde : tant que la souris, les écrans et les réglages du bord ne changent
    // pas, le résultat précédent est réutilisé (Gemini C1)
    Point edgeAt = new Point(int.MinValue, int.MinValue);
    Screen[] edgeScreens;
    bool edgeRight, edgeResult;
    int edgeZone, edgeIdx;
    string edgeName;
    Screen edgeScreen;

    bool AtEdge(Point p, out Screen scr)
    {
        // Une seule lecture de la liste des écrans : elle peut être remplacée par DisplaySettingsChanged sur un autre
        // fil, et le résultat doit être mémorisé avec la liste qui a servi à le calculer (QA34-10)
        Screen[] all = screens;
        if (p == edgeAt && all == edgeScreens && Settings.RightSide == edgeRight && Settings.EdgeZone == edgeZone &&
            Settings.ScreenIdx == edgeIdx && string.Equals(Settings.ScreenName, edgeName)) { scr = edgeScreen; return edgeResult; }
        edgeResult = ComputeAtEdge(p, all, out scr);
        edgeAt = p; edgeScreens = all; edgeScreen = scr;
        edgeRight = Settings.RightSide; edgeZone = Settings.EdgeZone; edgeIdx = Settings.ScreenIdx; edgeName = Settings.ScreenName;
        return edgeResult;
    }

    static bool ComputeAtEdge(Point p, Screen[] all, out Screen scr)
    {
        scr = Screen.FromPoint(p);
        int chosen = Settings.ChosenScreen(all);
        if (chosen >= 0 && !all[chosen].Bounds.Contains(p)) return false;
        Rectangle b = scr.Bounds;
        bool edge = Settings.RightSide
            ? p.X >= b.Right - 1 && !ScreenExistsAt(all, new Point(p.X + 1, p.Y))
            : p.X <= b.Left && !ScreenExistsAt(all, new Point(p.X - 1, p.Y));
        if (!edge) return false;
        if (Settings.EdgeZone == 0) return true;
        int third = b.Height / 3, rel = p.Y - b.Top;
        if (Settings.EdgeZone == 1) return rel < third;
        if (Settings.EdgeZone == 2) return rel >= third && rel < 2 * third;
        return rel >= 2 * third;
    }

    void Poll(object sender, EventArgs e)
    {
        pollCount++;
        // Les échéances se mesurent en temps écoulé, pas en ticks : le minuteur de Windows Forms tourne
        // plus lentement que demandé (granularité de 15,6 ms, interface chargée) (R-11)
        long now = clock.ElapsedMilliseconds;
        // Après une veille, le chronomètre a avancé sans que l'application tourne : un délai de souris entamé
        // avant la veille ne compte plus (Q26-01)
        if (now - lastPoll > 2000) { edgeSince = -1; outsideSince = -1; }
        lastPoll = now;
        // L'essai demande 10 s de fonctionnement réel, mesurées en ticks traités : la veille ne compte pas (Q26-01)
        if (pollCount >= 500 && now >= 10000) ConfirmTrial();
        if (pollCount % 50 == 0) { ApplyIconVisibility(); KeepWindowsBarHidden(); }
        // Épinglé, le panneau suit le changement de bureau virtuel (D-011)
        if (pollCount % 10 == 0 && Settings.Pinned && panel.Visible) Native.MoveToActiveDesktop(panel.Handle);
        if (pollCount % 250 == 0) RefreshTray(); // point « nouveaux fichiers » sur l'icône
        if (pollCount % 25 == 0 && panel.Dirty && !panel.Busy) panel.RequestLoad();
        if (now >= nextArchive)
        {
            RunArchive();                                   // 1 min après le démarrage, puis toutes les 6 h
            nextArchive = now + (archiveDue ? 60000L : 6 * 3600000L);   // reporté : nouvel essai dans 1 min
        }
        if (now >= nextUpdateCheck)
        {
            nextUpdateCheck = now + 3600000L;               // 30 s après le démarrage, puis toutes les heures
            CheckUpdateInBackground();
        }

        Point p = Cursor.Position;
        bool open = panel.IsOpen;
        Screen scr;
        bool atEdge = AtEdge(p, out scr);
        if (wasOpen && !open && atEdge) requireLeave = true;
        wasOpen = open;
        if (requireLeave && !atEdge) requireLeave = false;

        // Chaque délai repart de zéro dès que sa condition cesse d'être vraie (Q26-02)
        if (open) edgeSince = -1;
        if (!open || !Settings.AutoClose || Settings.Pinned) outsideSince = -1;
        if (!open)
        {
            if (!atEdge || requireLeave) { edgeSince = -1; return; }
            if (edgeSince < 0) edgeSince = now;
            int needed = Settings.Delay + (Control.MouseButtons != MouseButtons.None ? 350 : 0);
            if (now - edgeSince >= needed)
            {
                edgeSince = -1;
                if (Settings.NoFullscreen && Native.ForegroundIsFullscreen()) { requireLeave = true; return; }
                outsideSince = -1;
                autoCloseArmed = true;
                panel.ShowPanel(scr);
            }
        }
        else if (Settings.AutoClose && !Settings.Pinned)
        {
            Rectangle r = panel.Bounds;
            r.Inflate(20, 0);
            if (r.Contains(p)) autoCloseArmed = true;
            if (autoCloseArmed && !r.Contains(p) && !panel.MenuOpen && !panel.Busy) { if (outsideSince < 0) outsideSince = now; }
            else outsideSince = -1;
            if (outsideSince >= 0 && now - outsideSince >= 500) { outsideSince = -1; panel.HidePanel(); }
        }
    }
}
