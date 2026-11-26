// BureauCache — ClipboardHistory
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Historique du presse-papiers (D-011)
// =====================================================================
// Les 20 derniers textes copiés, en mémoire seulement : rien n'est écrit sur le disque, rien ne passe dans les
// réglages ni dans les sauvegardes. L'historique est vidé quand l'option est désactivée et disparaît à la
// fermeture. Les copies marquées privées par leur application (gestionnaires de mots de passe) sont ignorées.
static class ClipboardHistory
{
    public const int MaxEntries = 20, MaxLength = 10000;

    public class Entry
    {
        public int Id;
        public string Text;
        public DateTime At;
    }

    static readonly List<Entry> entries = new List<Entry>();
    static int nextId;

    public static event Action Changed;

    public static List<Entry> Items { get { lock (entries) return entries.ToList(); } }

    // Ajoute un texte en tête. Un texte déjà présent remonte au lieu d'être dupliqué. Faux si le texte est refusé
    // (vide, blanc, trop long).
    public static bool Add(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > MaxLength || text.Trim().Length == 0) return false;
        lock (entries)
        {
            Entry e = entries.FirstOrDefault(x => x.Text == text);
            if (e != null) entries.Remove(e);
            else e = new Entry { Id = ++nextId, Text = text };
            e.At = DateTime.Now;
            entries.Insert(0, e);
            while (entries.Count > MaxEntries) entries.RemoveAt(entries.Count - 1);
        }
        Fire();
        return true;
    }

    public static void Remove(int id)
    {
        lock (entries) entries.RemoveAll(e => e.Id == id);
        Fire();
    }

    public static void Clear()
    {
        bool had;
        lock (entries) { had = entries.Count > 0; entries.Clear(); }
        if (had) Fire();
    }

    static void Fire()
    {
        Action h = Changed;
        if (h != null) h();
    }

    // Une ligne lisible pour le panneau : premier contenu non vide, espaces réduits, coupé à max caractères
    public static string Preview(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string line = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        line = string.Join(" ", line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= max ? line : line.Substring(0, max - 1) + "…";
    }

    // Formats par lesquels une application demande à ne pas être enregistrée dans un historique
    // (documentés par Microsoft pour l'historique du presse-papiers et les outils de surveillance)
    public static bool IsPrivate(IDataObject data)
    {
        if (data == null) return true;
        try
        {
            if (data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing") || data.GetDataPresent("Clipboard Viewer Ignore")) return true;
            if (data.GetDataPresent("CanIncludeInClipboardHistory"))
            {
                System.IO.MemoryStream ms = data.GetData("CanIncludeInClipboardHistory") as System.IO.MemoryStream;
                // Valeur DWORD : 0 = ne pas garder ; illisible = on s'abstient
                if (ms == null || ms.Length < 4) return true;
                byte[] b = ms.ToArray();
                return BitConverter.ToInt32(b, 0) == 0;
            }
            return false;
        }
        catch { return true; }
    }

    // Lit le presse-papiers (fil d'interface, STA) et garde le texte s'il est permis
    public static bool Capture()
    {
        try
        {
            IDataObject data = Clipboard.GetDataObject();
            if (IsPrivate(data) || !data.GetDataPresent(DataFormats.UnicodeText)) return false;
            return Add(data.GetData(DataFormats.UnicodeText) as string);
        }
        catch { return false; }
    }
}

// Fenêtre de messages seule qui reçoit WM_CLIPBOARDUPDATE : aucune lecture en boucle
class ClipboardWatcher : NativeWindow
{
    const int WM_CLIPBOARDUPDATE = 0x031D;
    bool listening;

    public void Enable(bool on)
    {
        if (on == listening) return;
        if (on)
        {
            Sandbox.Guard("écoute du presse-papiers");
            if (Handle == IntPtr.Zero) CreateHandle(new CreateParams { Parent = new IntPtr(-3) });   // HWND_MESSAGE
            listening = Native.AddClipboardFormatListener(Handle);
        }
        else
        {
            if (Handle != IntPtr.Zero) Native.RemoveClipboardFormatListener(Handle);
            listening = false;
            ClipboardHistory.Clear();
        }
    }

    public void Close()
    {
        Enable(false);
        if (Handle != IntPtr.Zero) DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE && listening && Settings.ClipboardOn) ClipboardHistory.Capture();
        base.WndProc(ref m);
    }
}
