// BureauCache — BarForm : masquage automatique et molette sur le volume (T-043, D-019)
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

// Masquage automatique : Windows ne réserve qu'un mince liseré en bas de l'écran ; la barre s'y replie et revient
// quand la souris touche le bord. Elle reste dépliée tant qu'un de ses panneaux, son aperçu ou une fenêtre de
// BureauCache (menu Démarrer, Paramètres) est au premier plan.
partial class BarForm
{
    readonly Timer autoHide = new Timer { Interval = 120 };
    bool collapsedBar;
    int outside;   // relevés consécutifs souris hors de la barre

    internal static int Edge { get { return Math.Max(2, Ui.S(2)); } }
    // Hauteur que Windows réserve en bas de l'écran
    internal static int ReservedHeight { get { return Settings.TaskbarAutoHide ? Edge : BarHeight; } }

    // Fenêtre de la barre pour la réservation accordée par Windows : repliée, elle n'occupe que le liseré
    Rectangle VisibleBounds(Rectangle reserved)
    {
        if (!Settings.TaskbarAutoHide) return reserved;
        int height = collapsedBar ? Edge : BarHeight;
        return new Rectangle(reserved.Left, reserved.Bottom - height, reserved.Width, height);
    }

    void UpdateAutoHide()
    {
        bool on = Settings.TaskbarAutoHide && !Sandbox.Active;
        if (on && !autoHide.Enabled) { collapsedBar = false; outside = 0; autoHide.Start(); }
        else if (!on) { autoHide.Stop(); collapsedBar = false; }
    }

    void AutoHideTick()
    {
        if (!Settings.TaskbarAutoHide || !Visible) return;
        Point mouse = Cursor.Position;
        Rectangle screen = Screen.PrimaryScreen.Bounds;
        Rectangle band = new Rectangle(Left, screen.Bottom - BarHeight, Width, BarHeight);
        if (collapsedBar)
        {
            if (mouse.Y >= screen.Bottom - Edge - 1 && mouse.X >= Left && mouse.X < Right) SetCollapsed(false);
            return;
        }
        bool keep = band.Contains(mouse) || PanelOpen || (preview != null && preview.Visible) || dragging || OwnWindowInFront();
        outside = keep ? 0 : outside + 1;
        if (outside >= 5) SetCollapsed(true);   // environ 0,6 s hors de la barre
    }

    // Une fenêtre de BureauCache au premier plan (menu Démarrer, Paramètres, menu contextuel) garde la barre dépliée
    static bool OwnWindowInFront()
    {
        IntPtr front = Native.GetForegroundWindow();
        if (front == IntPtr.Zero) return false;
        uint pid;
        Native.GetWindowThreadProcessId(front, out pid);
        using (System.Diagnostics.Process me = System.Diagnostics.Process.GetCurrentProcess())
            return pid == (uint)me.Id;
    }

    void SetCollapsed(bool collapse)
    {
        if (collapsedBar == collapse) return;
        collapsedBar = collapse;
        outside = 0;
        if (collapse) { HidePreview(); tip.Hide(this); }
        Rectangle reserved = approvedBarBounds.IsEmpty ? Bounds : approvedBarBounds;
        Rectangle target = VisibleBounds(reserved);
        Bounds = target;   // directement : ApplyBarBounds fermerait les panneaux
        Invalidate();
    }

    // Molette sur réseau et son : volume de 2 en 2, sans ouvrir de panneau
    int pendingVolume;
    Task volumeWork;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!Settings.TaskbarWheel || Sandbox.Active || zones[1].IsEmpty || !zones[1].Contains(e.Location)) return;
        System.Threading.Interlocked.Add(ref pendingVolume, e.Delta > 0 ? 2 : -2);
        if (volumeWork != null && !volumeWork.IsCompleted) return;
        volumeWork = Task.Factory.StartNew(() =>
        {
            while (true)
            {
                int step = System.Threading.Interlocked.Exchange(ref pendingVolume, 0);
                if (step == 0) return;
                bool muted;
                int? current = Native.QuickVolume(null, false, out muted);
                if (!current.HasValue) return;
                Native.QuickVolume(Math.Max(0, Math.Min(100, current.Value + step)), false, out muted);
            }
        });
    }

    void DisposeBehavior()
    {
        autoHide.Dispose();
    }
}
