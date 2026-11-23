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
    bool busy;

    public static void Run()
    {
        using (CleanupDialog d = new CleanupDialog()) d.ShowDialog();
    }

    CleanupDialog() : base(L.T("Nettoyage rapide", "Quick clean-up"), Ui.S(560), Ui.S(140) + Ui.S(64) * 6)
    {
        int pad = Ui.S(20), w = ClientSize.Width - 2 * pad, y = Ui.S(14);
        Label intro = AddLabel(L.T("Seuls des caches et des fichiers temporaires de ton compte sont supprimés. Windows, les programmes et tes documents ne sont jamais touchés.",
            "Only caches and temporary files of your account are deleted. Windows, programs and your documents are never touched."), pad, y);
        intro.MaximumSize = new Size(w, 0);
        y = Ui.S(62);
        foreach (CleanCategory c in categories)
        {
            Label title = AddLabel(c.Title, pad, y);
            title.ForeColor = Theme.Fg;
            title.Font = Ui.Font("Segoe UI Semibold", 10f);
            Label detail = AddLabel(c.Detail, pad, y + Ui.S(21));
            detail.Font = Ui.Font("Segoe UI", 8.25f);
            detail.MaximumSize = new Size(w - Ui.S(170), Ui.S(34));
            Label size = AddLabel(L.T("analyse…", "scanning…"), pad + w - Ui.S(160), y + Ui.S(3));
            size.AutoSize = false;
            size.Size = new Size(Ui.S(105), Ui.S(20));
            size.TextAlign = ContentAlignment.MiddleRight;
            Toggle t = new Toggle { Checked = c.Checked, BackColor = Theme.Bg, Location = new Point(pad + w - Ui.S(44), y + Ui.S(2)) };
            CleanCategory cat = c;
            t.Changed += (s, e) => { cat.Checked = t.Checked; UpdateTotal(); };
            Controls.Add(t);
            toggles.Add(t);
            sizes.Add(size);
            y += Ui.S(64);
        }
        status = AddLabel("", pad, y + Ui.S(4));
        status.MaximumSize = new Size(w - Ui.S(250), 0);
        AddButtons(DoClean, L.T("Nettoyer", "Clean up"));
        bClean = Controls.OfType<FlatButton>().First(b => b.Primary);
        Controls.OfType<FlatButton>().First(b => !b.Primary).Text = L.T("Fermer", "Close");
        Shown += (s, e) => Analyse();
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
        status.Text = L.T("Analyse en cours…", "Scanning…");
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

    void UpdateTotal()
    {
        if (busy) return;
        long total = categories.Where(c => c.Checked).Sum(c => c.Size);
        List<string> running = categories.Where(c => c.Checked).SelectMany(c => c.Running).Distinct().ToList();
        status.Text = L.T("À libérer : ", "To free: ") + L.Size(total) +
            (running.Count > 0 ? L.T("\n* Ferme ", "\n* Close ") + string.Join(", ", running) + L.T(" pour nettoyer son cache.", " to clean its cache.") : "");
    }

    void DoClean()
    {
        if (busy) return;
        List<CleanCategory> chosen = categories.Where(c => c.Checked).ToList();
        long total = chosen.Sum(c => c.Size);
        if (chosen.Count == 0 || total == 0) { status.Text = L.T("Rien à nettoyer dans les catégories cochées.", "Nothing to clean in the selected categories."); return; }
        string list = string.Join("\n", chosen.Where(c => c.Size > 0).Select(c => "• " + c.Title + " (" + L.Size(c.Size) + ")"));
        if (!Dialogs.Ask(this, L.T("Supprimer définitivement ", "Permanently delete ") + L.Size(total) + L.T(" ?\n\n", "?\n\n") + list)) return;
        SetBusy(true);
        status.Text = L.T("Nettoyage en cours…", "Cleaning up…");
        Background.Run(() =>
        {
            CleanResult r = null;
            try { r = Cleaner.Clean(chosen); } catch { }
            Done(() =>
            {
                SetBusy(false);
                Analyse();
                if (r != null)
                    Dialogs.Info(this, L.Size(r.Freed) + L.T(" libérés.", " freed.") +
                        (r.Skipped > 0 ? "\n" + r.Skipped + L.T(" fichier(s) en cours d'utilisation ont été laissés en place.", " file(s) in use were left in place.") : ""));
            });
        });
    }

    void Done(Action a)
    {
        try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); } catch { }
    }
}
