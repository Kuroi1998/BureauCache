// BureauCache — CleanupDialog
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Nettoyage rapide : catégories à cocher, analyse, puis confirmation
// =====================================================================
class CleanupDialog : ThemedDialog
{
    readonly List<CleanCategory> categories = Cleaner.Categories();
    readonly List<Toggle> toggles = new List<Toggle>();
    readonly List<Label> sizes = new List<Label>();
    readonly FlatButton bClean;
    readonly Label status;
    bool busy, cleaning;
    string lastResult = "";   // bilan du dernier nettoyage, affiché devant le total

    public static void Run(IWin32Window owner = null)
    {
        using (CleanupDialog d = new CleanupDialog()) { if (owner != null) d.ShowDialog(owner); else d.ShowDialog(); }
    }

    CleanupDialog() : base(L.T("Nettoyage rapide", "Quick clean-up", "Limpieza rápida"), Ui.S(560), Ui.S(140) + Ui.S(64) * 6)
    {
        int pad = Ui.S(20), w = ClientSize.Width - 2 * pad, y = Ui.S(14);
        Label intro = AddLabel(L.T("Seuls des caches et des fichiers temporaires de ton compte sont supprimés. Windows, les programmes et tes documents ne sont jamais touchés.",
            "Only caches and temporary files of your account are deleted. Windows, programs and your documents are never touched.", "Solo se borran las cachés y los archivos temporales de tu cuenta. Se conservan Windows, los programas y tus documentos."), pad, y);
        intro.MaximumSize = new Size(w, 0);
        // Le texte d'introduction peut prendre trois lignes (espagnol, grand zoom) : la liste commence dessous
        y = Math.Max(Ui.S(62), y + intro.GetPreferredSize(new Size(w, 0)).Height + Ui.S(14));
        // Les catégories sont dans une zone qui défile : sur un petit écran (768 px à 150 %), la fenêtre est
        // bornée à la zone de travail et ses boutons restent visibles (Q30-03)
        int rowH = Ui.S(56), contentH = rowH * categories.Count, footer = Ui.S(78);
        int frame = Height - ClientSize.Height;
        int room = Screen.FromPoint(Cursor.Position).WorkingArea.Height - frame - Ui.S(16) - y - footer;
        Panel list = new Panel { Location = new Point(0, y), Size = new Size(ClientSize.Width, Math.Max(rowH * 2, Math.Min(contentH, room))), AutoScroll = true, BackColor = Theme.Bg };
        Controls.Add(list);
        int ly = 0;
        foreach (CleanCategory c in categories)
        {
            Label title = ListLabel(list, c.Title, pad, ly);
            title.ForeColor = Theme.Fg;
            title.Font = Ui.Font("Segoe UI Semibold", 10f);
            Label detail = ListLabel(list, c.Detail, pad, ly + Ui.S(21));
            detail.Font = Ui.Font("Segoe UI", 8.25f);
            detail.MaximumSize = new Size(w - Ui.S(170), Ui.S(30));
            Label size = ListLabel(list, L.T("analyse…", "scanning…", "analizando…"), pad + w - Ui.S(160), ly + Ui.S(3));
            size.AutoSize = false;
            size.Size = new Size(Ui.S(105), Ui.S(20));
            size.TextAlign = ContentAlignment.MiddleRight;
            Toggle t = new Toggle { Checked = c.Checked, BackColor = Theme.Bg, Location = new Point(pad + w - Ui.S(44), ly + Ui.S(2)) };
            CleanCategory cat = c;
            t.Changed += (s, e) => { cat.Checked = t.Checked; UpdateTotal(); };
            list.Controls.Add(t);
            toggles.Add(t);
            sizes.Add(size);
            ly += rowH;
        }
        y = list.Bottom;
        status = AddLabel("", pad, y + Ui.S(4));
        status.MaximumSize = new Size(w - Ui.S(250), 0);
        ClientSize = new Size(ClientSize.Width, y + footer);   // hauteur suivant le contenu réel
        AddButtons(DoClean, L.T("Nettoyer", "Clean up", "Limpiar"));
        bClean = Controls.OfType<FlatButton>().First(b => b.Primary);
        Controls.OfType<FlatButton>().First(b => !b.Primary).Text = L.T("Fermer", "Close", "Cerrar");
        // Le texte d'état peut prendre plusieurs lignes (applications à fermer, résultat précédent, espagnol) :
        // la fenêtre grandit avec lui et les boutons descendent d'autant, au lieu de couper le texte (GEM-11)
        baseHeight = ClientSize.Height;
        status.SizeChanged += (s, e) => FitStatus();
        Shown += (s, e) => Analyse();
    }

    static Label ListLabel(Panel list, string text, int x, int y)
    {
        Label l = new Label { Text = text, AutoSize = true, Font = Ui.Font("Segoe UI", 9.75f), ForeColor = Theme.FgDim, BackColor = Theme.Bg, Location = new Point(x, y) };
        list.Controls.Add(l);
        return l;
    }

    void SetBusy(bool on)
    {
        busy = on;
        bClean.Enabled = !on;
        foreach (Toggle t in toggles) t.Enabled = !on;
    }

    void Analyse()
    {
        SetBusy(true);
        status.Text = lastResult + L.T("Analyse en cours…", "Scanning…", "Analizando…");
        Background.Run(() =>
        {
            foreach (CleanCategory c in categories)
            {
                CleanCategory cat = c;
                try { Cleaner.Measure(cat); } catch { }
                Done(() => ShowSize(cat));
            }
            Done(() => { SetBusy(false); UpdateTotal(); });
        });
    }

    void ShowSize(CleanCategory c)
    {
        int i = categories.IndexOf(c);
        if (i < 0) return;
        string text = L.Size(c.Size);
        if (c.Running.Count > 0)
            text += " *";
        sizes[i].Text = text;
        sizes[i].ForeColor = c.Size > 0 ? Theme.Fg : Theme.FgDim;
    }

    int baseHeight;

    internal void FitStatus()
    {
        int want = Math.Max(baseHeight, status.Bottom + Ui.S(16));
        int delta = want - ClientSize.Height;
        if (delta == 0) return;
        ClientSize = new Size(ClientSize.Width, want);
        foreach (FlatButton b in Controls.OfType<FlatButton>()) b.Top += delta;
    }

    void UpdateTotal()
    {
        if (busy) return;
        long total = categories.Where(c => c.Checked).Sum(c => c.Size);
        List<string> running = categories.Where(c => c.Checked).SelectMany(c => c.Running).Distinct().ToList();
        status.Text = lastResult + L.T("À libérer : ", "To free: ", "Espacio recuperable: ") + L.Size(total) +
            (running.Count > 0 ? L.T("\n* Ferme ", "\n* Close ", "\n* Cierra ") + string.Join(", ", running) + L.T(" pour nettoyer son cache.", " to clean its cache.", " para limpiar su caché.") : "");
    }

    void DoClean()
    {
        if (busy) return;
        List<CleanCategory> chosen = categories.Where(c => c.Checked).ToList();
        long total = chosen.Sum(c => c.Size);
        if (chosen.Count == 0 || total == 0) { status.Text = L.T("Rien à nettoyer dans les catégories cochées.", "Nothing to clean in the selected categories.", "No hay nada que limpiar en las categorías seleccionadas."); return; }
        string list = string.Join("\n", chosen.Where(c => c.Size > 0).Select(c => "• " + c.Title + " (" + L.Size(c.Size) + ")"));
        if (!Dialogs.Ask(this, L.T("Supprimer définitivement ", "Permanently delete ", "¿Eliminar de forma permanente ") + L.Size(total) + L.T(" ?\n\n", "?\n\n", "?\n\n") + list)) return;
        SetBusy(true);
        cleaning = true;
        status.Text = L.T("Nettoyage en cours…", "Cleaning up…", "Limpiando…");
        Background.Run(() =>
        {
            CleanResult r = null;
            try { r = Cleaner.Clean(chosen); } catch { }
            Done(() =>
            {
                cleaning = false;
                // Bilan affiché dans la fenêtre, devant le nouveau total, sans boîte à valider (Gemini A10). Il est
                // remplacé à chaque tentative, échec compris, et reste visible pendant l'analyse qui suit (QA34-08).
                if (r == null) lastResult = L.T("Nettoyage interrompu · ", "Clean-up interrupted · ", "Limpieza interrumpida · ");
                else
                    lastResult = "✓ " + L.Size(r.Freed) + L.T(" libérés", " freed", " liberados") +
                        (r.Skipped > 0 ? " (" + r.Skipped + L.T(" en cours d'utilisation, laissés en place)", " in use, left in place)", " en uso, conservados)") : "") + " · ";
                SetBusy(false);
                Analyse();
            });
        });
    }

    // Pendant la suppression, la fenêtre reste ouverte : la fermer laissait le nettoyage continuer sans résultat
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (cleaning && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        base.OnFormClosing(e);
    }

    void Done(Action a)
    {
        try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); } catch { }
    }
}
