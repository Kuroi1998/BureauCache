// BureauCache — PanelForm (partie 2)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

partial class PanelForm
{
    // Enregistre une nouvelle largeur (en pixels « 100 % ») et l'applique
    void ApplyWidth(int logical)
    {
        logical = Math.Max(260, Math.Min(560, logical));
        int right = Right;
        Width = S(logical);
        if (Settings.RightSide) { Left = right - Width; openLeft = Left; }
        if (Settings.Glass && !liveBlurActive) RefreshStaticGlass();
        if (appbar) AppBarSetPos();
        Settings.PanelWidth = logical;
        Settings.Notify();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hoverBtn != -1 || hoverGrip) { hoverBtn = -1; hoverGrip = false; Invalidate(); }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int h = HitButton(e.Location);
        if (h == 5 && e.Button == MouseButtons.Left) TogglePin();
        else if (h == 0 && e.Button == MouseButtons.Left) { Settings.GridView = !Settings.GridView; Settings.Notify(); }
        else if (h == 1 && e.Button == MouseButtons.Left) { HidePanel(); if (SettingsRequested != null) SettingsRequested(this, EventArgs.Empty); }
        else if (h == 3 && e.Button == MouseButtons.Left) { HidePanel(); try { ShellUtil.Open("explorer.exe", "shell:MyComputerFolder"); } catch { } }
        else if (h == 3 && e.Button == MouseButtons.Right) DiskMenu(e.Location);
        else if (h == 6) WeatherClick(e.Button, e.Location);
        else if (h == 4 && e.Button == MouseButtons.Left) { HidePanel(); try { ShellUtil.Open("explorer.exe", "shell:RecycleBinFolder"); } catch { } }
        else if (h == 4 && e.Button == MouseButtons.Right)
        {
            Menus.Clear(menu.Items);
            menu.Items.Add(Menus.Item(L.T("Ouvrir la corbeille", "Open Recycle Bin"), Glyphs.Open, () => { HidePanel(); ShellUtil.Open("explorer.exe", "shell:RecycleBinFolder"); }));
            ToolStripMenuItem empty = Menus.Item(L.T("Vider la corbeille", "Empty Recycle Bin"), Glyphs.Delete, () =>
            {
                Busy = true;
                int r = Native.SHEmptyRecycleBin(Handle, null, 0);
                Busy = false;
                RefreshWidgetData();
                if (r == 0) Toast(L.T("Corbeille vidée", "Recycle Bin emptied"), null, null);
            });
            empty.Enabled = binCount > 0;
            menu.Items.Add(empty);
            ShowPanelMenu(this, e.Location);
        }
        else if (rcSearch.Contains(e.Location)) search.Focus();
    }

    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); View.WheelFromParent(e.Delta); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            if (search.Text.Length > 0) search.Text = ""; else HidePanel();
            e.Handled = true;
        }
        if (e.KeyCode == Keys.ControlKey && !View.ShowNumbers) { View.ShowNumbers = true; View.Invalidate(); }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.ControlKey && View.ShowNumbers) { View.ShowNumbers = false; View.Invalidate(); }
        base.OnKeyUp(e);
    }

    // Raccourcis clavier du panneau
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys k = keyData & Keys.KeyCode;
        bool ctrl = (keyData & Keys.Control) != 0, shift = (keyData & Keys.Shift) != 0, alt = (keyData & Keys.Alt) != 0;
        DesktopItem sel = View.Help == null ? View.Selected : null;
        if (alt) return base.ProcessCmdKey(ref msg, keyData);
        if (k == Keys.Tab && !ctrl) { View.JumpGroup(shift ? -1 : 1); return true; }
        if (k == Keys.F1) { search.Text = search.Text == "?" ? "" : "?"; search.SelectionStart = search.Text.Length; return true; }
        if (ctrl && !shift && ((k >= Keys.D1 && k <= Keys.D9) || (k >= Keys.NumPad1 && k <= Keys.NumPad9)))
        {
            if (View.Help != null) return true;
            int n = k >= Keys.NumPad1 ? k - Keys.NumPad1 : k - Keys.D1;
            List<DesktopItem> favs = Settings.Favorites.Select(f => All.FirstOrDefault(i => string.Equals(i.Key, f, StringComparison.OrdinalIgnoreCase)))
                                                       .Where(i => i != null).ToList();
            if (n < favs.Count) Launch(favs[n], null);
            else Toast(L.T("Pas de favori n°" + (n + 1) + " : glisse un élément dans « Favoris ».", "No favorite #" + (n + 1) + ": drag an item into \"Favorites\"."), null, null);
            return true;
        }
        if (ctrl && !shift && k == Keys.Z && toast.CanUndo) { toast.Undo(); return true; }
        if (ctrl && !shift && k == Keys.P) { TogglePin(); return true; }
        if (ctrl && !shift && k == Keys.G) { Settings.GridView = !Settings.GridView; Settings.Notify(); return true; }
        if (ctrl && !shift && k == Keys.F) { if (sel != null && sel.Kind == ItemKind.Desktop) ToggleFavorite(sel); return true; }
        if (!ctrl && k == Keys.F2) { if (sel != null && sel.IsDesktopFile) Rename(sel); return true; }
        if (!ctrl && !shift && k == Keys.Delete && search.Text.Length == 0) { if (sel != null && sel.IsDesktopFile) Delete(sel); return true; }
        if (k == Keys.Apps || (shift && k == Keys.F10)) { if (sel != null) ShowItemMenu(sel, View.SelectedPoint()); return true; }
        if (ctrl && !shift && k == Keys.Enter)
        {
            if (sel != null && sel.HasPath) { HidePanel(); ShellUtil.Reveal(sel.Target); }
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    List<KeyValuePair<string, string>> HelpLines()
    {
        Func<string, string, string, KeyValuePair<string, string>> h = (k, fr, en) => new KeyValuePair<string, string>(k, L.T(fr, en));
        string ent = L.T("Entrée", "Enter"), maj = L.T("Maj", "Shift"), sup = L.T("Suppr", "Del"), esc = L.T("Échap", "Esc");
        return new List<KeyValuePair<string, string>> {
            h(ent, "Ouvrir l'élément sélectionné", "Open the selected item"),
            h("↑ ↓ ← →", "Se déplacer dans la liste", "Move around"),
            h("Tab / " + maj + "+Tab", "Section suivante / précédente", "Next / previous section"),
            h("Ctrl+1 … 9", "Ouvrir un favori (maintiens Ctrl pour voir les numéros)", "Open a favorite (hold Ctrl to see numbers)"),
            h("Ctrl+F", "Ajouter ou retirer des favoris", "Add to / remove from favorites"),
            h("F2", "Renommer", "Rename"),
            h(sup, "Supprimer (annulable)", "Delete (can be undone)"),
            h("Ctrl+Z", "Annuler la dernière action", "Undo the last action"),
            h("Ctrl+" + ent, "Ouvrir l'emplacement du fichier", "Open file location"),
            h("Ctrl+" + maj + "+" + ent, "Exécuter en administrateur", "Run as administrator"),
            h(maj + "+F10", "Options de l'élément (clic droit)", "Item options (right-click)"),
            h("Ctrl+P", "Épingler / détacher le panneau", "Pin / unpin the panel"),
            h("Ctrl+G", "Liste ou grille", "List or grid"),
            h("= ? >", "Calcul, recherche web, commandes", "Math, web search, commands"),
            h(esc, "Effacer la recherche, puis fermer", "Clear the search, then close"),
            h("F1", "Afficher / masquer cette aide", "Show / hide this help"),
        };
    }

    public void Toast(string text, Action undo, Action commit)
    {
        if (!Visible) { if (commit != null) try { commit(); } catch { } return; }
        toast.Show(text, undo, commit);
    }

    public void FlushToast() { toast.Flush(); }

    // Une action sur un fichier (renommage, suppression) peut encore être annulée
    public bool UndoPending { get { return toast.CanUndo; } }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (View.ShowNumbers) { View.ShowNumbers = false; View.Invalidate(); }
        if (Busy || MenuOpen || !IsOpen || !IsHandleCreated) return;
        BeginInvoke(new Action(() =>
        {
            if (IsOpen && !Busy && !MenuOpen && !ContainsFocus &&
                !preview.ContainsFocus && !Bounds.Contains(Cursor.Position)) HidePanel();
        }));
    }

    // ---------- Épinglage (barre d'application Windows qui réserve la place) ----------
    public void TogglePin()
    {
        Settings.Pinned = !Settings.Pinned;
        Settings.SaveQuiet();
        if (Settings.Pinned)
        {
            if (!IsOpen) ShowPanel(Screen.FromPoint(Cursor.Position));
            Pin();
            Toast(L.T("Panneau épinglé : il reste ouvert et les fenêtres lui laissent la place.", "Panel pinned: it stays open and windows make room for it."), null, null);
        }
        else
        {
            Unpin();
            Toast(L.T("Panneau détaché.", "Panel unpinned."), null, null);
        }
        Invalidate();
    }

    void Pin()
    {
        pinScreen = Screen.FromControl(this);
        if (liveBlurActive)
        {
            liveBlurActive = false;
            blurTimer.Stop();
        }
        AppBarRegister();
        RefreshStaticGlass();
    }

    public void ShowPinned(Screen scr)
    {
        ShowPanel(scr);
        Pin();
    }

    public void Unpin()
    {
        AppBarRemove();
        TopMost = true;
    }

    void RefreshStaticGlass()
    {
        if (!Settings.Glass) return;
        try { Native.SetWindowDisplayAffinity(Handle, 0x11); } catch { }
        Bitmap g = MakeGlass(new Rectangle(Left, Top, Width, Height));
        try { Native.SetWindowDisplayAffinity(Handle, liveBlurActive ? 0x11u : 0u); } catch { }
        if (g == null) return;
        Bitmap old = glass;
        glass = g;
        if (old != null) old.Dispose();
        Invalidate(true);
    }

    void AppBarRegister()
    {
        if (appbar) return;
        Native.APPBARDATA d = new Native.APPBARDATA();
        d.cbSize = Marshal.SizeOf(d);
        d.hWnd = Handle;
        d.uCallbackMessage = abMsg;
        Native.SHAppBarMessage(0 /*ABM_NEW*/, ref d);
        appbar = true;
        AppBarSetPos();
    }

    void AppBarSetPos()
    {
        if (!appbar) return;
        Native.APPBARDATA d = new Native.APPBARDATA();
        d.cbSize = Marshal.SizeOf(d);
        d.hWnd = Handle;
        d.uEdge = Settings.RightSide ? 2u : 0u;
        Rectangle b = pinScreen.Bounds;
        d.rc = new Native.RECT { Left = b.Left, Top = b.Top, Right = b.Right, Bottom = b.Bottom };
        if (Settings.RightSide) d.rc.Left = d.rc.Right - Width; else d.rc.Right = d.rc.Left + Width;
        Native.SHAppBarMessage(2 /*ABM_QUERYPOS*/, ref d);
        if (Settings.RightSide) d.rc.Left = d.rc.Right - Width; else d.rc.Right = d.rc.Left + Width;
        Native.SHAppBarMessage(3 /*ABM_SETPOS*/, ref d);
        Rectangle wa = pinScreen.WorkingArea;
        openLeft = d.rc.Left;
        anim.Stop();
        Opacity = 1;
        Bounds = new Rectangle(d.rc.Left, wa.Top, Width, wa.Height);
    }

    void AppBarRemove()
    {
        if (!appbar) return;
        Native.APPBARDATA d = new Native.APPBARDATA();
        d.cbSize = Marshal.SizeOf(d);
        d.hWnd = Handle;
        Native.SHAppBarMessage(1 /*ABM_REMOVE*/, ref d);
        appbar = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (appbar && m.Msg == (int)abMsg)
        {
            int code = m.WParam.ToInt32();
            if (code == 1) AppBarSetPos();                                   // ABN_POSCHANGED
            else if (code == 2) TopMost = m.LParam == IntPtr.Zero;          // ABN_FULLSCREENAPP : s'efface devant un jeu plein écran
        }
        base.WndProc(ref m);
    }
}
