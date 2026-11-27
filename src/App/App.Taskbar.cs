// BureauCache — App : barre des tâches BureauCache à la place de celle de Windows (D-013)
using System;
using System.Drawing;
using System.Windows.Forms;

partial class App
{
    BarForm bar;
    bool taskbarRefused;            // un échec n'est signalé qu'une fois, jusqu'à ce que l'option soit coupée
    bool taskbarRestorePending;
    volatile bool sessionEnding;    // fin de session : plus aucun masquage (BAR-11)

    // Ordre imposé par D-013 : la barre Windows est cachée AVANT que la nôtre ne s'installe, et rendue APRÈS son départ.
    // Tout échec en chemin rend immédiatement la barre Windows (BAR-09).
    void ApplyTaskbar()
    {
        bool want = Settings.TaskbarOn && !Sandbox.Active && !sessionEnding;
        if (!want) taskbarRefused = false;
        if (want && bar == null && !taskbarRefused)
        {
            // Sans le processus de surveillance, un plantage laisserait la barre Windows cachée (BAR-07)
            if (!Program.WatchdogRunning || !WindowsBar.Hide()) { RefuseTaskbar(); return; }
            BarForm created = null;
            bool ready = false;
            try
            {
                created = new BarForm();
                created.StartClicked += StartFromBar;
                created.MenuRequested += ShowTrayMenu;
                created.ExplorerRestarted += OnExplorerRestarted;
                created.Show();
                ready = created.Registered;
            }
            catch { }
            if (!ready)
            {
                try { if (created != null) { created.Close(); created.Dispose(); } } catch { }
                RefuseTaskbar();
                return;
            }
            bar = created;
        }
        else if (!want && bar != null) CloseBar();
        else if (!want) RestoreTaskbar();   // reste d'un plantage ou d'un arrêt brutal : la barre Windows revient
        if (bar != null) { bar.ApplyTheme(); bar.RebuildButtons(); }   // épingles changées par une restauration (D-014)
    }

    void RefuseTaskbar()
    {
        CloseBar();
        if (taskbarRefused) return;
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
            if (taskbarRestorePending) RestoreTaskbar();
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
        if (ending) { sessionEnding = true; try { WindowsBar.Restore(); } catch { } return; }
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
        try { Native.RestoreDesktopIcons(); } catch { }
        try { WindowsBar.Restore(); } catch { }
    }
}
