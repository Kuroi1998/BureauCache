// BureauCache — SettingsForm (encombrement du bureau)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// Paramètres → Organisation → « Encombrement du bureau » (D-010) : nombre d'éléments, taille totale, inutilisés
// depuis 30 et 90 jours, et les plus volumineux. Calculé sur un fil de fond à l'ouverture de la page et après
// chaque changement de réglage ; un compteur écarte les résultats périmés.
partial class SettingsForm
{
    Label lClutter, lBiggest;
    int clutterCount;

    void BuildClutterSection()
    {
        Section(T("ENCOMBREMENT DU BUREAU", "DESKTOP CLUTTER", "SATURACIÓN DEL ESCRITORIO"));
        lClutter = Row(T("État du bureau", "Desktop status", "Estado del escritorio"), "-",
            Button(T("Ranger maintenant", "Tidy up now", "Archivar ahora"), false, S(170), TidyNow), null);
        lBiggest = Row(T("Les plus volumineux", "Largest items", "Los más grandes"), "-",
            Button(T("Ouvrir le bureau", "Open the desktop", "Abrir el escritorio"), false, S(170), () => ShellUtil.Open(Paths.Desktop, null)), null);
    }

    void UpdateClutter()
    {
        if (lClutter == null) return;
        int my = ++clutterCount;
        string desk = Paths.Desktop, folder = Archiver.Folder;
        var favorites = new HashSet<string>(Settings.Favorites, StringComparer.OrdinalIgnoreCase);
        var usage = new Dictionary<string, long[]>(Settings.Usage, StringComparer.OrdinalIgnoreCase);
        lClutter.Text = T("Analyse du bureau…", "Checking the desktop…", "Analizando el escritorio…");
        Background.Run(() =>
        {
            Clutter.Report r = Clutter.Measure(desk, favorites, usage, folder, DateTime.UtcNow);
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (my != clutterCount || IsDisposed) return;
                    lClutter.Text = DescribeClutter(r);
                    lBiggest.Text = r.Biggest.Count == 0 ? T("Aucun fichier.", "No files.", "Ningún archivo.")
                        : string.Join("\n", r.Biggest.Select(k => "• " + Path.GetFileName(k.Key) + " — " + L.Size(k.Value)).ToArray());
                }));
            }
            catch { }
        });
    }

    string DescribeClutter(Clutter.Report r)
    {
        if (r.Unreadable) return T("Le dossier du bureau ne peut pas être lu pour l'instant.", "The desktop folder can't be read right now.", "No se puede leer la carpeta del escritorio por ahora.");
        if (r.Count == 0) return T("Le bureau est vide.", "The desktop is empty.", "El escritorio está vacío.");
        string size = (r.Partial ? T("au moins ", "at least ", "al menos ") : "") + L.Size(r.Bytes);
        return r.Count + T(" élément(s), ", " item(s), ", " elemento(s), ") + size + ".\n" +
               r.Unused30 + T(" inutilisé(s) depuis 30 jours, dont ", " unused for 30 days, including ", " sin usar durante 30 días, de ellos ") +
               r.Unused90 + T(" depuis 90 jours.", " for 90 days.", " durante 90 días.");
    }

    void TidyNow()
    {
        List<string> c = Archiver.Candidates();
        // Résultats affichés sous les réglages, sans boîte à valider (Gemini A4, A5) ; la confirmation du déplacement reste
        if (c.Count == 0) { lArchive.Text = T("Rien à ranger pour le moment.", "Nothing to tidy up right now.", "Por ahora no hay nada que archivar."); return; }
        string list = string.Join("\n", c.Take(12).Select(p => "• " + Path.GetFileName(p))) + (c.Count > 12 ? "\n…" : "");
        if (!Ask(T(c.Count + " élément(s) seront déplacés selon les règles de rangement :\n\n", c.Count + " item(s) will be moved according to the tidy-up rules:\n\n", c.Count + " elemento(s) se moverán según las reglas de organización:\n\n") + list)) return;
        Begin(lArchiveUndo);
        int n = Archiver.Run(c);
        // Rien déplacé : le journal d'annulation reste celui du rangement précédent, on ne propose pas de l'annuler (QA34-07)
        if (n == 0) { lArchive.Text = T("Aucun élément n'a pu être rangé (déjà déplacés ou en cours d'utilisation).", "No item could be moved (already moved or in use).", "No se pudo mover ningún elemento (ya movidos o en uso)."); return; }
        Done(lArchiveUndo, T(n + " élément(s) rangé(s). Annuler les remet sur le bureau.", n + " item(s) moved. Undo puts them back on the desktop.", n + " elemento(s) movido(s). Deshacer los devuelve al escritorio."));
        Settings.Fire();
    }
}
