// BureauCache — App : barre des tâches BureauCache à la place de celle de Windows (D-013)
using System;
using System.Drawing;
using System.Windows.Forms;

partial class App
{
    BarForm bar;
    bool taskbarRefused;            // un échec n'est signalé qu'une fois, jusqu'à ce que l'option soit coupée
    bool taskbarRestorePending;
    readonly Timer taskbarRefresh = new Timer { Interval = 150 };
    long explorerDeadline = -1;
    volatile bool sessionEnding;    // fin de session : plus aucun masquage (BAR-11)

    void InitializeTaskbar()
    {
        taskbarRefresh.Tick += (s, e) => { taskbarRefresh.Stop(); ApplyTaskbar(); };
        if (Settings.TaskbarOn) taskbarRefresh.Start();
        else ApplyTaskbar();
    }

    internal static bool WaitForTaskbarExplorer(long now, ref long deadline, bool available)
    {
        if (available) { deadline = -1; return false; }
        if (deadline < 0) deadline = now + 10000;
        return now < deadline;
    }

    // Ordre imposé par D-013 : la barre Windows est cachée AVANT que la nôtre ne s'installe, et rendue APRÈS son départ.
    // Tout échec en chemin rend immédiatement la barre Windows (BAR-09).
    void ApplyTaskbar()
    {
        bool want = Settings.TaskbarOn && !Sandbox.Active && !sessionEnding;
        if (!want)
        {
            StartupTimeline.Finish("barre_desactivee");
            taskbarRefused = false;
            explorerDeadline = -1;
            taskbarRefresh.Stop();
        }
        else if (taskbarRefresh.Enabled) return;
        if (want && bar == null && !taskbarRefused)
        {
            StartupTimeline.Mark("attente_explorer");
            // Sans le processus de surveillance, un plantage laisserait la barre Windows cachée (BAR-07)
            if (!Program.WatchdogRunning) { RefuseTaskbar(); return; }
            bool explorerReady = Native.WindowsTaskbarResponds();
            if (WaitForTaskbarExplorer(clock.ElapsedMilliseconds, ref explorerDeadline, explorerReady))
            {
                taskbarRefresh.Start();
                return;
            }
            if (!explorerReady)
            {
                taskbarRestorePending = true;
                WarnTaskbarRefused();
                return;
            }
            StartupTimeline.Mark("explorer_pret");
            if (!WindowsBar.Hide()) { RefuseTaskbar(); return; }
            StartupTimeline.Mark("barre_windows_masquee");
            BarForm created = null;
            bool ready = false;
            try
            {
                created = new BarForm();
                created.StartClicked += StartFromBar;
                created.MenuRequested += ShowTrayMenu;
                created.ExplorerRestarted += OnExplorerRestarted;
                created.InitializationFailed += () => { if (bar == created) RefuseTaskbar(); };
                created.Show();
                ready = created.Registered && !sessionEnding;
            }
            catch (Exception ex) { Log.Warn("barre.creation", ex); }   // le refus est annoncé juste après (T-061)
            if (!ready)
            {
                try { if (created != null) { created.Close(); created.Dispose(); } } catch { }
                RefuseTaskbar();
                return;
            }
            bar = created;
            return;
        }
        else if (!want && bar != null) CloseBar();
        else if (!want) RestoreTaskbar();   // reste d'un plantage ou d'un arrêt brutal : la barre Windows revient
        if (bar != null) bar.ApplyChangedSettings();
    }

    void RefuseTaskbar()
    {
        CloseBar();
        WarnTaskbarRefused();
    }

    void WarnTaskbarRefused()
    {
        StartupTimeline.Finish("barre_refusee");
        if (taskbarRefused || sessionEnding) return;
        taskbarRefused = true;
        Balloon(L.T("Échec du remplacement de la barre des tâches. BureauCache rétablit la barre Windows.",
                    "Taskbar replacement failed. BureauCache is restoring the Windows taskbar.",
                    "Falló el reemplazo de la barra de tareas. BureauCache está restaurando la barra de Windows."), null);
    }

    void CloseBar()
    {
        if (startMenu != null && !startMenu.IsDisposed) try { startMenu.Close(); } catch { }
        if (bar != null)
        {
            BarForm old = bar;
            bar = null;
            try { old.Close(); old.Dispose(); } catch { }
        }
        RestoreTaskbar();
    }

    void RestoreTaskbar() { taskbarRestorePending = !WindowsBar.Restore(); }

    // Explorer a redémarré : on ne recache sa barre que si notre réservation a été acceptée de nouveau (BAR-09)
    void OnExplorerRestarted()
    {
        if (bar == null || sessionEnding) return;
        if (!Program.WatchdogRunning || !bar.Registered || !WindowsBar.Reapply()) RefuseTaskbar();
    }

    StartMenuForm startMenu;

    // Bouton Démarrer : ouvre ou ferme le menu. Un menu qui vient de se fermer sur ce même clic (perte du focus) ne
    // se rouvre pas, comme pour l'icône près de l'horloge.
    void StartFromBar()
    {
        if (startMenu != null && !startMenu.IsDisposed) { startMenu.Close(); return; }
        if (StartMenuForm.JustClosed) return;
        if (bar == null) return;
        startMenu = new StartMenuForm();
        startMenu.OpenPanel += () => { OpenManually(Screen.PrimaryScreen); Native.SetForegroundWindow(panel.Handle); };
        startMenu.OpenSettings += ShowSettings;
        startMenu.OpenTour += ShowWelcome;
        startMenu.QuitRequested += QuitByUser;
        startMenu.OpenStartSettings += () => { ShowSettings(); settingsForm.SelectPage(SettingsForm.TaskbarPage); };
        startMenu.SearchRequested += text =>
        {
            OpenManually(Screen.PrimaryScreen);
            Native.SetForegroundWindow(panel.Handle);
            panel.SearchFor(text);
        };
        bar.HidePreview();
        startMenu.FormClosed += (s, e) => { startMenu = null; };
        Rectangle start = bar.StartButtonBounds;
        startMenu.PlaceAbove(start, Screen.FromRectangle(start).WorkingArea);
        startMenu.Show();
        startMenu.Activate();
        Native.SetForegroundWindow(startMenu.Handle);
    }

    void RestoreWindowsBarByUser()
    {
        Settings.TaskbarOn = false;
        Settings.Notify();
    }

    // Appelé par Poll environ une fois par seconde
    void KeepWindowsBarHidden()
    {
        if (sessionEnding) return;   // jusqu'à ce que Windows dise si la fin de session a lieu (OnSessionEnded)
        if (bar == null)
        {
            if (taskbarRestorePending && Native.WindowsTaskbarResponds()) RestoreTaskbar();
            return;
        }
        if (!Program.WatchdogRunning || !bar.Registered || !WindowsBar.Reapply()) { RefuseTaskbar(); return; }
        if (!bar.Visible) bar.Show();
        bar.RetryWindowTracking();
    }

    void OnDisplaysChangedForBar()
    {
        if (bar == null || sessionEnding) return;
        bar.Reposition();
        if (!Program.WatchdogRunning || !WindowsBar.Reapply()) RefuseTaskbar();
    }

    // Fin de session (fil de SystemEvents) : on arrête de masquer avant de rendre, chaque étape isolée des autres
    // Réponse finale de Windows (fil d'interface). Session qui continue (déconnexion ou arrêt annulé) : la barre
    // BureauCache revient. Session qui se termine : on ne masque plus rien. Remplace le délai d'une minute de QA-66,
    // qui pouvait recacher la barre pendant une fin de session encore en négociation (CDX-05).
    void OnSessionEnded(bool ending)
    {
        if (ending) { sessionEnding = true; try { WindowsBar.Restore(); } catch (Exception ex) { Log.Warn("barre.restauration", ex); } return; }
        if (!sessionEnding) return;
        sessionEnding = false;
        CloseBar();
        taskbarRefused = false;
        ApplyTaskbar();
    }

    void OnSessionEnding()
    {
        sessionEnding = true;
        try { Settings.Flush(); } catch { }
        try { Native.RestoreDesktopIcons(); } catch (Exception ex) { Log.Warn("bureau.icones", ex); }
        try { TaskbandRoute.Restore(); } catch (Exception ex) { Log.Warn("barre.badges", ex); }
        try { WindowsBar.Restore(); } catch (Exception ex) { Log.Warn("barre.restauration", ex); }
    }
}
