// BureauCache — journal des avertissements (T-048)
using System;
using System.Globalization;
using System.IO;

// =====================================================================
//  Journal minimal des erreurs « qui ne doivent pas disparaître » : réglages, mise à jour, barre.
//  Règles :
//  - seuls les contextes de la liste ci-dessous sont écrits : un contexte inconnu est ignoré ;
//  - une ligne = heure UTC, contexte et catégorie d'erreur. Jamais de message d'exception, de chemin,
//    de titre de fenêtre, de notification, de presse-papiers ni de mot de passe ;
//  - fichier borné (64 Ko, via FileOps.AppendBoundedLog), dans le dossier des réglages ;
//  - désactivé pendant les tests (sauf dossier de réglages isolé) et quand les réglages sont bloqués.
// =====================================================================
static class Log
{
    internal const string FileName = "avertissements.log";
    const string Header = "utc,contexte,cause" + "\r\n";

    // Plusieurs fils écrivent ici (interface, tâches de fond) : une écriture à la fois, pour que la rotation
    // et les lignes ne se mêlent pas (T-060)
    static readonly object gate = new object();

    internal static void Warn(string contexte, Exception ex)
    {
        if (!Allowed(contexte)) return;
        if (Settings.SaveBlocked || (Sandbox.Active && Settings.DirOverride == null)) return;
        try
        {
            string cause = Cause(ex);
            string line = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "," + contexte + "," + cause + "\r\n";
            lock (gate) FileOps.AppendBoundedLog(Path.Combine(Settings.Dir, FileName), Header, line);
        }
        catch (Exception) { }   // le journal ne doit jamais masquer l'erreur d'origine
    }

    internal static bool Allowed(string contexte)
    {
        switch (contexte)
        {
            case "reglages.chargement":
            case "reglages.enregistrement":
            case "reglages.usage_reduit":
            case "maj.nettoyage":
            case "maj.retour":
            case "barre.restauration":
            case "barre.etat":
            case "sauvegarde.reglages":
            case "rangement":
            case "panneau.suppression":
            case "meteo.recuperation":
            case "meteo.lieu":
            case "chargement.elements":
            case "shell.fil":
            case "nettoyage.dossier":
            case "surveillance.dossier":
            case "shell.ouverture":
            case "presse-papiers":
            case "maj.essai":
            case "maj.relance":
            case "surveillance.lancement":
            case "installation.enregistrement":
            case "barre.creation":
            case "barre.badges":
            case "bureau.icones":
                return true;
            default:
                return false;
        }
    }

    // Catégorie fixe, sans le texte de l'exception
    internal static string Cause(Exception ex)
    {
        if (ex == null) return "evenement";
        if (ex is UnauthorizedAccessException) return "acces";
        if (ex is IOException) return "io";
        return "exception";
    }
}
