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

    readonly string message;

    MessageDialog(string message, Kind kind, bool safeDefault) : base("BureauCache", Ui.S(460), Ui.S(170))
    {
        this.message = message;
        // Bornée à la zone de travail : sur un petit écran ou à 200 %, un long message défile et les boutons restent
        // visibles (QA36-04)
        Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
        int frameW = Width - ClientSize.Width, frameH = Height - ClientSize.Height;
        ClientSize = new Size(Math.Min(ClientSize.Width, work.Width - frameW - Ui.S(16)), ClientSize.Height);
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
        int scrollW = SystemInformation.VerticalScrollBarWidth;
        int textAreaMax = Math.Max(Ui.S(60), work.Height - frameH - Ui.S(16) - Ui.S(22) - Ui.S(78));
        text.MaximumSize = new Size(textW, 0);
        int textH = text.GetPreferredSize(new Size(textW, 0)).Height;
        if (textH > textAreaMax)
        {
            // Trop long pour l'écran : le texte passe dans une zone qui défile
            Controls.Remove(text);
            text.MaximumSize = new Size(textW - scrollW, 0);
            text.Location = Point.Empty;
            Panel scroller = new Panel { Location = new Point(textX, Ui.S(22)), Size = new Size(textW, textAreaMax), AutoScroll = true, BackColor = Theme.Bg };
            scroller.Controls.Add(text);
            Controls.Add(scroller);
            textH = textAreaMax;
        }
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

    // Ctrl+C copie le message, comme les boîtes de Windows : utile pour transmettre une erreur (QA36-05)
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.C))
        {
            try { Clipboard.SetText(message); } catch { }
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // Vrai si l'utilisateur a validé. owner peut être nul ou caché : la fenêtre est alors centrée sur l'écran.
    // Appelée depuis un autre fil, elle s'affiche sur celui de la fenêtre propriétaire (QA36-06).
    public static bool Show(IWin32Window owner, string message, Kind kind, bool safeDefault = false)
    {
        Form parent = null;
        Control c = owner as Control;
        if (c != null) parent = c.FindForm();
        if (parent != null && (!parent.IsHandleCreated || !parent.Visible || parent.IsDisposed)) parent = null;
        if (parent != null && parent.InvokeRequired)
        {
            Form target = parent;
            return (bool)target.Invoke(new Func<bool>(() => Show(target, message, kind, safeDefault)));
        }
        using (MessageDialog d = new MessageDialog(message, kind, safeDefault))
        {
            if (parent != null) d.StartPosition = FormStartPosition.CenterParent;
            return (parent != null ? d.ShowDialog(parent) : d.ShowDialog()) == DialogResult.OK;
        }
    }
}
