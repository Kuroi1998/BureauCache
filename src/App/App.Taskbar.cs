// BureauCache — App : barre des tâches BureauCache à la place de celle de Windows (D-013)
using System;
using System.Windows.Forms;

partial class App
{
    BarForm bar;

    // Ordre imposé par D-013 : la barre Windows est cachée AVANT que la nôtre ne s'installe, et rendue APRÈS son départ
    void ApplyTaskbar()
    {
        bool want = Settings.TaskbarOn && !Sandbox.Active;
        if (want && bar == null)
        {
            if (!WindowsBar.Hide())
            {
                Balloon(L.T("La barre des tâches de Windows n'a pas pu être remplacée. Elle reste affichée.",
                            "The Windows taskbar couldn't be replaced. It stays visible.",
                            "No se pudo reemplazar la barra de tareas de Windows. Sigue visible."), null);
                return;
            }
            bar = new BarForm();
            bar.StartClicked += StartFromBar;
            bar.MenuRequested += ShowTrayMenu;
            bar.ExplorerRestarted += WindowsBar.Reapply;
            bar.Show();
        }
        else if (!want && bar != null) CloseBar();
        else if (!want) WindowsBar.Restore();   // reste d'un plantage ou d'un arrêt brutal : la barre Windows revient
        if (bar != null) bar.ApplyTheme();
    }

    void CloseBar()
    {
        if (bar != null)
        {
            BarForm old = bar;
            bar = null;
            try { old.Close(); old.Dispose(); } catch { }
        }
        WindowsBar.Restore();
    }

    // Comme pour l'icône près de l'horloge : un panneau qui vient de se fermer sur ce même clic ne se rouvre pas
    void StartFromBar()
    {
        bool wasOpen = panel.IsOpen || (DateTime.Now - panel.LastHidden).TotalMilliseconds < 200;
        if (wasOpen && !Settings.Pinned) panel.HidePanel();
        else
        {
            OpenManually(Screen.PrimaryScreen);
            Native.SetForegroundWindow(panel.Handle);
        }
    }

    void RestoreWindowsBarByUser()
    {
        Settings.TaskbarOn = false;
        Settings.Notify();
    }

    // Appelé par Poll environ une fois par seconde
    void KeepWindowsBarHidden()
    {
        if (bar == null) return;
        WindowsBar.Reapply();
        if (!bar.Visible) bar.Show();
    }

    void OnDisplaysChangedForBar()
    {
        if (bar == null) return;
        bar.Reposition();
        WindowsBar.Reapply();
    }
}
