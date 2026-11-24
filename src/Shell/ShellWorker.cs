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

    // Accédés uniquement sur le fil dédié
    static int session;
    static List<object> verbs;

    static void Post(Action work)
    {
        lock (gate)
        {
            if (invoker == null)
            {
                using (ManualResetEvent ready = new ManualResetEvent(false))
                {
                    Thread t = new Thread(() =>
                    {
                        invoker = new Control();
                        IntPtr h = invoker.Handle;   // crée la fenêtre de messages sur ce fil
                        ready.Set();
                        Application.Run();
                    });
                    t.IsBackground = true;
                    t.Name = "BureauCache shell";
                    t.SetApartmentState(ApartmentState.STA);
                    t.Start();
                    ready.WaitOne();
                }
            }
        }
        // Une exception sur ce fil remonterait à sa boucle de messages et ouvrirait le dialogue d'erreur
        try { invoker.BeginInvoke(new Action(() => { try { work(); } catch { } })); } catch { }
    }

    // Énumère les verbes de path sur le fil dédié. done(numéro de menu, libellés) est appelé sur ce même fil :
    // l'appelant le ramène sur son propre fil. Chaque énumération remplace la précédente.
    public static void LoadVerbs(string path, Action<int, List<string>> done)
    {
        Post(() =>
        {
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

    // Exécute le verbe choisi, seulement s'il appartient encore au dernier menu énuméré
    public static void Invoke(int menu, int index)
    {
        Post(() =>
        {
            if (menu != session || verbs == null || index < 0 || index >= verbs.Count) return;
            ShellUtil.InvokeVerb(verbs[index]);
        });
    }
}
