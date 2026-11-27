// BureauCache — App : barre des tâches BureauCache à la place de celle de Windows (D-013)
using System;
using System.Drawing;
using System.Windows.Forms;

partial class App
{
    BarForm bar;
    bool taskbarRefused;            // un échec n'est signalé qu'une fois, jusqu'à ce que l'option soit coupée
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
            if (!Program.WatchdogStarted || !WindowsBar.Hide()) { RefuseTaskbar(); return; }
            BarForm created = null;
            try
            {
                created = new BarForm();
                created.StartClicked += StartFromBar;
                created.MenuRequested += ShowTrayMenu;
                created.ExplorerRestarted += OnExplorerRestarted;
                created.Show();
            }
            catch { }
            if (created == null || !created.Registered)
            {
                try { if (created != null) { created.Close(); created.Dispose(); } } catch { }
                RefuseTaskbar();
                return;
            }
            bar = created;
        }
        else if (!want && bar != null) CloseBar();
        else if (!want) WindowsBar.Restore();   // reste d'un plantage ou d'un arrêt brutal : la barre Windows revient
        if (bar != null) bar.ApplyTheme();
    }

    void RefuseTaskbar()
    {
        WindowsBar.Restore();
        if (taskbarRefused) return;
        taskbarRefused = true;
        Balloon(L.T("La barre des tâches de Windows n'a pas pu être remplacée. Elle reste affichée.",
                    "The Windows taskbar couldn't be replaced. It stays visible.",
                    "No se pudo reemplazar la barra de tareas de Windows. Sigue visible."), null);
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
        WindowsBar.Restore();
    }

    // Explorer a redémarré : on ne recache sa barre que si notre réservation a été acceptée de nouveau (BAR-09)
    void OnExplorerRestarted()
    {
        if (bar == null) return;
        if (bar.Registered) { WindowsBar.Reapply(); return; }
        CloseBar();
        RefuseTaskbar();
    }

    StartMenuForm startMenu;

    // Bouton Démarrer : ouvre ou ferme le menu. Un menu qui vient de se fermer sur ce même clic (perte du focus) ne
    // se rouvre pas, comme pour l'icône près de l'horloge.
    void StartFromBar()
    {
        if (startMenu != null && !startMenu.IsDisposed) { startMenu.Close(); return; }
        if ((DateTime.Now - StartMenuForm.LastClosed).TotalMilliseconds < 250) return;
        if (bar == null) return;
        startMenu = new StartMenuForm();
        startMenu.OpenPanel += () => { OpenManually(Screen.PrimaryScreen); Native.SetForegroundWindow(panel.Handle); };
        startMenu.SearchRequested += text =>
        {
            OpenManually(Screen.PrimaryScreen);
            Native.SetForegroundWindow(panel.Handle);
            panel.SearchFor(text);
        };
        bar.HidePreview();
        startMenu.FormClosed += (s, e) => { startMenu = null; };
        Rectangle start = bar.StartButtonBounds;
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        startMenu.Location = new Point(Math.Max(wa.Left, start.Left), start.Top - Ui.S(8) - startMenu.Height);
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
        if (bar == null || sessionEnding) return;
        WindowsBar.Reapply();
        if (!bar.Visible) bar.Show();
    }

    void OnDisplaysChangedForBar()
    {
        if (bar == null || sessionEnding) return;
        bar.Reposition();
        WindowsBar.Reapply();
    }

    // Fin de session (fil de SystemEvents) : on arrête de masquer avant de rendre, chaque étape isolée des autres
    void OnSessionEnding()
    {
        sessionEnding = true;
        try { Settings.Flush(); } catch { }
        try { Native.RestoreDesktopIcons(); } catch { }
        try { WindowsBar.Restore(); } catch { }
    }
}
