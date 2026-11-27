// BureauCache — PanelForm (partie 4)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

partial class PanelForm
{
    int verbMenu;   // dernier sous-menu « Plus d'options » ouvert (R-12)

    // ---------- Actions ----------
    void Launch(DesktopItem it, string verb)
    {
        if (it.Kind == ItemKind.Calc)
        {
            try { Clipboard.SetText(it.Args); } catch { }
            HidePanel();
            return;
        }
        if (it.Run != null)
        {
            HidePanel();
            try { it.Run(); } catch (Exception ex) { ShowError(ex.Message); }
            return;
        }
        Settings.RecordUse(it.Key);
        HidePanel();
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(it.Target);
            if (it.Args != null) psi.Arguments = it.Args;
            psi.UseShellExecute = true;
            if (verb != null) psi.Verb = verb;
            if (it.HasPath)
            {
                string wd = Path.GetDirectoryName(it.Target);
                if (!string.IsNullOrEmpty(wd)) psi.WorkingDirectory = wd;
            }
            Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            if (ex.NativeErrorCode != 1223) ShowError(L.T("Impossible d'ouvrir « ", "Can't open \"", "No se pudo abrir «") + it.Name + L.T(" » :\n", "\":\n", "»:\n") + ex.Message);
        }
        catch (Exception ex) { ShowError(L.T("Impossible d'ouvrir « ", "Can't open \"", "No se pudo abrir «") + it.Name + L.T(" » :\n", "\":\n", "»:\n") + ex.Message); }
        Rebuild(true);
    }

    static void ShowError(string msg)
    {
        Dialogs.Warn(null, msg);
    }

    void ShowItemMenu(DesktopItem it, Point pt)
    {
        Menus.Clear(menu.Items);
        ToolStripMenuItem open = Menus.Item(it.Kind == ItemKind.Calc ? L.T("Copier le résultat", "Copy result", "Copiar resultado")
                                          : it.Kind == ItemKind.Clip ? L.T("Copier", "Copy", "Copiar") : L.T("Ouvrir", "Open", "Abrir"),
                                            it.Kind == ItemKind.Clip ? Glyphs.Copy : Glyphs.Open, () => Launch(it, null));
        open.Font = Ui.Font(menu.Font.Name, menu.Font.Size, FontStyle.Bold);
        menu.Items.Add(open);
        if (it.Kind == ItemKind.Clip)
            menu.Items.Add(Menus.Item(L.T("Retirer de l'historique", "Remove from history", "Quitar del historial"), Glyphs.Delete, () => RemoveClip(it)));
        if (it.HasPath && Catalog.IsExecutable(it.Target))
            menu.Items.Add(Menus.Item(L.T("Exécuter en tant qu'administrateur", "Run as administrator", "Ejecutar como administrador"), Glyphs.Shield, () => Launch(it, "runas")));
        if (it.HasPath)
            menu.Items.Add(Menus.Item(L.T("Ouvrir l'emplacement du fichier", "Open file location", "Abrir la ubicación del archivo"), Glyphs.Folder, () =>
            {
                HidePanel();
                ShellUtil.Reveal(it.Target);
            }));
        if (it.Kind == ItemKind.Desktop)
        {
            menu.Items.Add(new ToolStripSeparator());
            bool fav = Settings.FavIndex(it.Key) >= 0;
            menu.Items.Add(Menus.Item(fav ? L.T("Retirer des favoris", "Remove from favorites", "Quitar de favoritos") : L.T("Ajouter aux favoris", "Add to favorites", "Añadir a favoritos"), fav ? Glyphs.Unfavorite : Glyphs.Star, () => ToggleFavorite(it)));
        }
        if (it.IsDesktopFile)
        {
            ToolStripMenuItem mv = Menus.Sub(menu, L.T("Déplacer vers", "Move to", "Mover a"), Glyphs.Move);
            foreach (string cat in Catalog.AllCats())
            {
                string c = cat;
                ToolStripMenuItem ci = Menus.Item(Catalog.Display(c), Catalog.GlyphOf(c), () => MoveToCategory(it, c));
                ci.Checked = string.Equals(it.Category, c, StringComparison.OrdinalIgnoreCase);
                mv.DropDownItems.Add(ci);
            }
            mv.DropDownItems.Add(new ToolStripSeparator());
            mv.DropDownItems.Add(Menus.Item(L.T("Nouvelle catégorie…", "New category…", "Nueva categoría…"), Glyphs.Add, () =>
            {
                HidePanel();
                string name = CategoryDialog.Run(null);
                if (name == null) return;
                SetCategory(it, name, false);
                Settings.Notify();
            }));
            mv.DropDownItems.Add(Menus.Item(L.T("Automatique (", "Automatic (", "Automático (") + Catalog.Display(it.AutoCategory) + ")", null, () =>
            {
                Settings.CatOverride.Remove(it.Key);
                Settings.SaveQuiet();
                Rebuild(true);
            }));
            menu.Items.Add(mv);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Menus.Item(L.T("Renommer…", "Rename…", "Cambiar nombre…"), Glyphs.Rename, () => Rename(it)));
            menu.Items.Add(Menus.Item(L.T("Supprimer", "Delete", "Eliminar"), Glyphs.Delete, () => Delete(it)));
        }
        if (it.HasPath)
        {
            menu.Items.Add(Menus.Item(L.T("Propriétés", "Properties", "Propiedades"), Glyphs.Info, () => { HidePanel(); ShellUtil.Properties(it.Target); }));
            ToolStripMenuItem more = Menus.Sub(menu, L.T("Plus d'options", "More options", "Más opciones"), Glyphs.More);
            more.DropDownItems.Add(new ToolStripMenuItem(L.T("Chargement…", "Loading…", "Cargando…")) { Enabled = false });
            // Les verbes sont énumérés et exécutés sur le fil du shell (R-12) : le panneau reste utilisable pendant
            // qu'une extension lente répond. Seul le dernier sous-menu ouvert est rempli.
            more.DropDownOpening += (s, e) =>
            {
                int gen = ++verbMenu;
                Menus.Clear(more.DropDownItems);
                more.DropDownItems.Add(new ToolStripMenuItem(L.T("Chargement…", "Loading…", "Cargando…")) { Enabled = false });
                ShellWorker.LoadVerbs(it.Target, (menuId, names) =>
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (gen != verbMenu || more.IsDisposed) return;
                            Menus.Clear(more.DropDownItems);
                            for (int i = 0; i < names.Count; i++)
                            {
                                int index = i;
                                more.DropDownItems.Add(new ToolStripMenuItem(names[i], null, (s2, e2) => { HidePanel(); ShellWorker.Invoke(menuId, index, () => Post(() => Dirty = true)); Dirty = true; }));
                            }
                            if (names.Count == 0) more.DropDownItems.Add(new ToolStripMenuItem(L.T("Aucune option", "No options", "Sin opciones")) { Enabled = false });
                        }));
                    }
                    catch { }
                });
            };
            // Sous-menu refermé avant la réponse : elle ne le remplit plus. Le choix déjà fait n'en dépend pas,
            // il porte le numéro de menu du fil du shell (Q28-04)
            more.DropDownClosed += (s, e) => verbMenu++;
            menu.Items.Add(more);
        }
        ShowPanelMenu(View, pt);
    }

    void ShowPanelMenu(Control source, Point position)
    {
        showingPanelPopup = true;
        try { menu.Show(source, position); }
        finally { showingPanelPopup = false; }
    }

    void Rename(DesktopItem it)
    {
        preview.HidePreview();
        string ext = Path.GetExtension(it.Target);
        bool hiddenExt = !it.IsFolder && (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || ext.Equals(".url", StringComparison.OrdinalIgnoreCase));
        Busy = true;
        try
        {
            using (RenameDialog d = new RenameDialog(it.Name, !it.IsFolder && !hiddenExt))
            {
                if (d.ShowDialog(this) != DialogResult.OK || d.Value == it.Name) return;
                string oldPath = it.Target, oldName = it.Name;
                string newPath = Path.Combine(Path.GetDirectoryName(oldPath), d.Value + (hiddenExt ? ext : ""));
                bool folder = it.IsFolder;
                try
                {
                    FileOps.Move(oldPath, newPath, folder);
                    Settings.RenameKey(oldPath, newPath);
                    ReloadNow();
                    Toast(L.T("Renommé en « " + d.Value + " »", "Renamed to \"" + d.Value + "\"", "Nuevo nombre: «" + d.Value + "»"), () =>
                    {
                        FileOps.Move(newPath, oldPath, folder);
                        Settings.RenameKey(newPath, oldPath);
                        ReloadNow();
                    }, null);
                }
                catch (Exception ex) { ShowError(L.T("Impossible de renommer :\n", "Can't rename:\n", "No se pudo cambiar el nombre:\n") + ex.Message); }
            }
        }
        finally
        {
            Busy = false;
            if (Visible) { Activate(); search.Focus(); }
        }
    }

    public static string StashRoot { get { return Path.Combine(Settings.Dir, "suppression"); } }

    static void Recycle(string stash)
    {
        foreach (string p in Directory.GetFileSystemEntries(stash)) FileOps.Recycle(p, false);
        Directory.Delete(stash);
    }

    // Éléments laissés en attente après une fermeture brutale : on finit de les envoyer à la corbeille
    public static void RecycleLeftovers()
    {
        try
        {
            if (!Directory.Exists(StashRoot)) return;
            foreach (string d in Directory.GetDirectories(StashRoot)) try { Recycle(d); } catch { }
        }
        catch { }
    }

    // Suppression annulable : l'élément est mis de côté quelques secondes, puis envoyé à la corbeille
    void Delete(DesktopItem it)
    {
        preview.HidePreview();
        Settings.Snap snap = Settings.Snapshot();
        string original = it.Target, name = it.Name;
        bool folder = it.IsFolder;
        string stash = Path.Combine(StashRoot, Guid.NewGuid().ToString("N"));
        string held = Path.Combine(stash, Path.GetFileName(original));
        try
        {
            Directory.CreateDirectory(stash);
            FileOps.Move(original, held, folder);
        }
        catch
        {
            // Impossible de le mettre de côté (autre disque, droits…) : corbeille directe, avec confirmation de Windows
            try { Directory.Delete(stash, true); } catch { }
            Busy = true;
            try
            {
                FileOps.Recycle(original, true);
                Settings.RemoveKey(it.Key);
                Toast(L.T("« " + name + " » placé dans la corbeille", "\"" + name + "\" moved to the Recycle Bin", "«" + name + "» se movió a la papelera"), null, null);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ShowError(L.T("Impossible de supprimer :\n", "Can't delete:\n", "No se pudo eliminar:\n") + ex.Message); }
            finally { Busy = false; }
            ReloadNow();
            return;
        }
        Settings.RemoveKey(it.Key);
        ReloadNow();
        Toast(L.T("« " + name + " » supprimé", "\"" + name + "\" deleted", "«" + name + "» se eliminó"),
            () =>
            {
                FileOps.Move(held, original, folder);
                try { Directory.Delete(stash, true); } catch { }
                Settings.Restore(snap);
                ReloadNow();
            },
            () => Recycle(stash));
    }

    void AddFilesToDesktop(string[] files, DragDropEffects eff)
    {
        string desk = Paths.Desktop;
        if (string.IsNullOrEmpty(desk)) return;
        Busy = true;
        int added = 0;
        try
        {
            foreach (string f0 in files)
            {
                if (string.IsNullOrWhiteSpace(f0)) continue;
                string name = f0;
                try
                {
                    string f = Path.GetFullPath(f0);
                    if (Paths.SameFolder(Path.GetDirectoryName(f), desk)) continue;
                    bool root = Paths.SameFolder(f, Path.GetPathRoot(f));
                    if (root && eff != DragDropEffects.Link) continue;
                    name = Path.GetFileName(f.TrimEnd('\\')).TrimEnd(':');
                    if (string.IsNullOrEmpty(name)) continue;
                    string dest = Path.Combine(desk, name);
                    if (eff == DragDropEffects.Link)
                    {
                        string lnk = Path.Combine(desk, name + ".lnk");
                        for (int i = 2; File.Exists(lnk) || Directory.Exists(lnk); i++)
                            lnk = Path.Combine(desk, name + " (" + i + ").lnk");
                        ShellUtil.CreateShortcut(lnk, f, null);
                    }
                    else FileOps.CopyOrMove(f, dest, eff == DragDropEffects.Move);
                    added++;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { ShowError(L.T("Impossible d'ajouter « ", "Can't add \"", "No se pudo añadir «") + name + L.T(" » :\n", "\":\n", "»:\n") + ex.Message); }
            }
        }
        finally { Busy = false; }
        ReloadNow();
        if (added > 0) Toast(added + L.T(added > 1 ? " éléments ajoutés au bureau" : " élément ajouté au bureau", added > 1 ? " items added to the desktop" : " item added to the desktop", added > 1 ? " elementos añadidos al escritorio" : " elemento añadido al escritorio"), null, null);
    }

    void SearchKeyDown(object sender, KeyEventArgs e)
    {
        if (View.Help != null && e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            return;
        }
        bool emptyText = search.Text.Length == 0;
        if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up || (Settings.GridView && emptyText && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)))
        {
            View.MoveSelection(e.KeyCode);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (View.Selected == null) View.SelectFirst();
            if (View.Selected != null) Launch(View.Selected, e.Control && e.Shift ? "runas" : null);
        }
    }
}
