// BureauCache — MessageDialog
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Confirmation et message aux couleurs de l'application (T-022, Gemini A13)
// =====================================================================
// Remplace les MessageBox de Windows : même thème que le reste, centré sur la fenêtre appelante, utilisable au
// clavier (Tab, Entrée, Espace, Échap). Pour une action lourde (éteindre, redémarrer…), « Non » est sélectionné
// par défaut : un Entrée réflexe ne la déclenche pas.
class MessageDialog : ThemedDialog
{
    public enum Kind { Question, Info, Warning }

    MessageDialog(string message, Kind kind, bool safeDefault) : base("BureauCache", Ui.S(460), Ui.S(170))
    {
        int pad = Ui.S(20), textX = pad + Ui.S(40), textW = ClientSize.Width - textX - pad;
        Label icon = new Label
        {
            Text = kind == Kind.Question ? Glyphs.Info : kind == Kind.Warning ? Glyphs.Shield : Glyphs.Info,
            Font = Ui.Font(Ui.GlyphFont, 16f), ForeColor = kind == Kind.Warning ? Theme.Danger : Theme.Accent, BackColor = Theme.Bg,
            AutoSize = false, Size = new Size(Ui.S(32), Ui.S(32)), Location = new Point(pad, Ui.S(18)), TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(icon);
        Label text = AddLabel(message, textX, Ui.S(22));
        text.ForeColor = Theme.Fg;
        text.UseMnemonic = false;
        text.MaximumSize = new Size(textW, 0);
        int textH = text.GetPreferredSize(new Size(textW, 0)).Height;
        ClientSize = new Size(ClientSize.Width, Math.Max(Ui.S(32), textH) + Ui.S(22) + Ui.S(78));

        bool question = kind == Kind.Question;
        AddButtons(() => { DialogResult = DialogResult.OK; Close(); }, question ? L.T("Oui", "Yes", "Sí") : "OK");
        FlatButton ok = Controls.OfType<FlatButton>().First(b => b.Primary);
        FlatButton cancel = Controls.OfType<FlatButton>().First(b => !b.Primary);
        if (question) cancel.Text = L.T("Non", "No", "No");
        else
        {
            // Un seul bouton : il prend la place de « Annuler »
            ok.Location = new Point(ClientSize.Width - Ui.S(20) - ok.Width, cancel.Top);
            Controls.Remove(cancel);
            cancel.Dispose();
        }
        Control first = question && safeDefault ? (Control)cancel : ok;
        Shown += (s, e) => { if (!first.IsDisposed) first.Focus(); };
    }

    // Vrai si l'utilisateur a validé. owner peut être nul ou caché : la fenêtre est alors centrée sur l'écran.
    public static bool Show(IWin32Window owner, string message, Kind kind, bool safeDefault = false)
    {
        Form parent = null;
        Control c = owner as Control;
        if (c != null) parent = c.FindForm();
        if (parent != null && (!parent.IsHandleCreated || !parent.Visible || parent.IsDisposed)) parent = null;
        using (MessageDialog d = new MessageDialog(message, kind, safeDefault))
        {
            if (parent != null) d.StartPosition = FormStartPosition.CenterParent;
            return (parent != null ? d.ShowDialog(parent) : d.ShowDialog()) == DialogResult.OK;
        }
    }
}
