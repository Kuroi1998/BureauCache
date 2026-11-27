// BureauCache — BarForm : réordonner les boutons en les glissant (D-014)
using System;
using System.Drawing;
using System.Windows.Forms;

partial class BarForm
{
    bool dragging;
    int dragIndex = -1, pressIndex = -1;
    Point pressPoint;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        pressIndex = -1;
        if (e.Button != MouseButtons.Left) return;
        int i = WindowZoneAt(e.Location) - WindowZone;
        if (i < 0 || i >= shown || i >= buttons.Count) return;
        pressIndex = i;
        pressPoint = e.Location;
    }

    // Vrai si le déplacement de la souris sert à glisser un bouton. Le glissement ne commence qu'au-delà du seuil de
    // Windows, pour qu'un clic un peu tremblé reste un clic.
    bool DragMove(Point p)
    {
        if (pressIndex < 0 || (MouseButtons & MouseButtons.Left) == 0) return false;
        if (!dragging)
        {
            Size d = SystemInformation.DragSize;
            if (Math.Abs(p.X - pressPoint.X) < d.Width && Math.Abs(p.Y - pressPoint.Y) < d.Height) return false;
            dragging = true;
            dragIndex = pressIndex;
            HidePreview();
            Cursor = Cursors.SizeWE;
        }
        int target = Math.Max(0, Math.Min(shown - 1, (p.X - buttonsLeft) / ButtonWidth));
        if (target != dragIndex && dragIndex >= 0 && dragIndex < buttons.Count)
        {
            BarButton moved = buttons[dragIndex];
            buttons.RemoveAt(dragIndex);
            buttons.Insert(target, moved);
            dragIndex = target;
            Invalidate();
        }
        return true;
    }

    // Vrai si un glissement vient de se terminer : ce relâchement n'est pas un clic. L'ordre des épingles est
    // enregistré, celui des fenêtres vaut pour la session.
    bool DragEnd()
    {
        pressIndex = -1;
        if (!dragging) return false;
        dragging = false;
        dragIndex = -1;
        Cursor = Cursors.Default;
        Settings.TaskbarPins = TaskbarModel.PinsInOrder(buttons);
        Settings.SaveQuiet();
        if (windows != null) windows.Reorder(TaskbarModel.WindowsInOrder(buttons));   // reconstruit les boutons
        else RebuildButtons();
        return true;
    }

    // Capture perdue en plein glissement (Alt+Tab, fenêtre qui s'ouvre) : on garde l'ordre atteint
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (dragging && !Capture) DragEnd();
    }
}
