// BureauCache — ShellWorker
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

// =====================================================================
//  Fil STA dédié aux verbes du shell (« Plus d'options », R-12)
// =====================================================================
// Une extension du shell lente (fichier réseau, hors ligne) ne fige plus le panneau. Les objets COM des verbes
// restent sur ce fil, qui les a créés et qui possède sa propre boucle de messages (dialogues ouverts par un
// verbe). L'interface ne reçoit que des libellés et un numéro de menu ; le choix revient ici sous forme d'indice.
// Un appel du shell déjà bloqué occupe toujours ce fil : les demandes suivantes attendent derrière lui, mais
// le panneau reste utilisable.
static class ShellWorker
{
    static readonly object gate = new object();
    static Control invoker;   // créé sur le fil dédié : BeginInvoke y exécute le travail
    static int requested;     // dernière énumération demandée (fil appelant, Interlocked)

    // Accédés uniquement sur le fil dédié
    static int session;
    static List<object> verbs;
    static bool busy;
    static readonly Queue<Action> waiting = new Queue<Action>();

    static void Post(Action work)
    {
        Control target;
        lock (gate)
        {
            if (invoker == null || invoker.IsDisposed) invoker = Start();
            target = invoker;
        }
        if (target == null) return;
        try { target.BeginInvoke(new Action(() => Run(work))); } catch { }
    }

    // Démarre le fil et attend sa fenêtre de messages, 5 s au plus. Quand sa boucle s'arrête, le fil se retire :
    // la demande suivante en relance un (Q28-01).
    static Control Start()
    {
        Control created = null;
        // Pas de using : le fil signale aussi la fin de démarrage dans son finally, après le retour de Start ;
        // un événement déjà libéré y lèverait une exception et le fil ne se retirerait pas du cache
        ManualResetEvent ready = new ManualResetEvent(false);
        Thread t = new Thread(() =>
        {
            Control c = null;
            try
            {
                c = new Control();
                IntPtr h = c.Handle;   // crée la fenêtre de messages sur ce fil
                created = c;
                ready.Set();
                Application.Run();
            }
            catch (Exception ex) { Log.Warn("shell.fil", ex); }   // fil des verbes du shell absent : « Plus d'options » sans effet (T-060)
            finally
            {
                ready.Set();
                lock (gate) { if (c != null && invoker == c) invoker = null; }
                if (c != null) try { c.Dispose(); } catch { }
            }
        });
        t.IsBackground = true;
        t.Name = "BureauCache shell";
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        return ready.WaitOne(5000) ? created : null;
    }

    // Un verbe qui ouvre un dialogue fait tourner une boucle de messages imbriquée : une autre demande y entrerait
    // avant la fin de la première. Elle est mise en attente et exécutée ensuite, dans l'ordre (Q28-03).
    static void Run(Action work)
    {
        if (busy) { waiting.Enqueue(work); return; }
        busy = true;
        try
        {
            try { work(); } catch { }   // une exception remonterait à la boucle et ouvrirait le dialogue d'erreur
            while (waiting.Count > 0)
            {
                Action next = waiting.Dequeue();
                try { next(); } catch { }
            }
        }
        finally { busy = false; }
    }

    // Énumère les verbes de path sur le fil dédié. done(numéro de menu, libellés) est appelé sur ce même fil :
    // l'appelant le ramène sur son propre fil. Chaque énumération remplace la précédente, et une demande déjà
    // dépassée par une plus récente n'interroge pas le shell (Q28-04).
    public static void LoadVerbs(string path, Action<int, List<string>> done)
    {
        int ticket = Interlocked.Increment(ref requested);
        Post(() =>
        {
            if (ticket != Thread.VolatileRead(ref requested)) return;
            int my = ++session;
            verbs = null;
            List<KeyValuePair<string, object>> found = ShellUtil.Verbs(path);
            if (my != session) return;
            verbs = new List<object>();
            List<string> names = new List<string>();
            foreach (KeyValuePair<string, object> v in found) { names.Add(v.Key); verbs.Add(v.Value); }
            done(my, names);
        });
    }

    // Exécute le verbe choisi, seulement s'il appartient encore au dernier menu énuméré. finished, appelé sur ce
    // fil une fois le verbe terminé, permet de recharger le panneau après son effet (Q28-08).
    public static void Invoke(int menu, int index, Action finished = null)
    {
        Post(() =>
        {
            if (menu != session || verbs == null || index < 0 || index >= verbs.Count) return;
            ShellUtil.InvokeVerb(verbs[index]);
            if (finished != null) finished();
        });
    }
}
