// BureauCache — Updater
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// =====================================================================
//  Mises à jour via GitHub Releases
// =====================================================================
static class Updater
{
    public const string DefaultRepo = AppInfo.Repo;
    public sealed class Release
    {
        public string Version, Url, ShaUrl, SigUrl, Notes, Error;
        public string Repository;
        public bool Available;
        // Version plus récente publiée sans ses fichiers d'installation : la vérification a réussi (Q26-04)
        public bool Incomplete;
    }

    static Release current = new Release();
    public static string Latest { get { return current.Available ? current.Version : null; } }
    public static Release AvailableRelease
    {
        get
        {
            Release release = current;
            return release.Available && release.Repository == RepoName ? release : null;
        }
    }

    public static string RepoName
    {
        get
        {
            string r = (Settings.UpdateRepo ?? "").Trim().TrimEnd('/');
            if (r.Length == 0) r = DefaultRepo;
            if (r.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) r = r.Substring(19);
            return r;
        }
    }

    public static bool RepoValid { get { return ValidRepository(RepoName); } }

    internal static bool ValidRepository(string name)
    {
        return name != null && Regex.IsMatch(name, @"\A[A-Za-z0-9_-]+/[A-Za-z0-9_.-]+\z") &&
            !name.EndsWith("/.", StringComparison.Ordinal) && !name.EndsWith("/..", StringComparison.Ordinal);
    }

    const long MaxDownload = 50L * 1024 * 1024;
    static readonly string[] AllowedHosts = { "api.github.com", "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com" };

    // Durée totale d'un téléchargement, redirections comprises : un serveur qui envoie au compte-gouttes ne bloque
    // pas indéfiniment (SEC-12)
    const long TextBudgetMs = 60000, DownloadBudgetMs = 1800000;   // 30 min : 50 Mo à environ 28 Ko/s (QA52-06)

    static void Fetch(string url, Stream output, long maximum)
    {
        Fetch(url, output, maximum, maximum > 4 * 1024 * 1024 ? DownloadBudgetMs : TextBudgetMs);
    }

    static void Fetch(string url, Stream output, long maximum, long budgetMs)
    {
        Sandbox.Guard("accès réseau (mises à jour)");
        Stopwatch clock = Stopwatch.StartNew();
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
        for (int redirect = 0; redirect <= 5; redirect++)
        {
            if (clock.ElapsedMilliseconds > budgetMs) throw new IOException(L.T("Le téléchargement est trop lent : réessaie plus tard.", "The download is too slow: try again later.", "La descarga es demasiado lenta. Inténtalo más tarde."));
            RequireTrustedUrl(url);
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.AllowAutoRedirect = false;
            request.UserAgent = "BureauCache-Updater";
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                int status = (int)response.StatusCode;
                if (status >= 300 && status < 400)
                {
                    string location = response.Headers[HttpResponseHeader.Location];
                    if (string.IsNullOrEmpty(location)) throw new IOException(L.T("Redirection sans adresse.", "Missing redirect location.", "Redirección sin dirección."));
                    url = new Uri(request.RequestUri, location).AbsoluteUri;
                    continue;
                }
                if (status != 200 || response.ContentLength > maximum)
                    throw new IOException(L.T("Réponse ou taille de téléchargement inattendue.", "Unexpected download response or file size.", "Respuesta o tamaño de descarga inesperados."));
                using (Stream input = response.GetResponseStream()) CopyLimited(input, output, maximum, clock, budgetMs);
                return;
            }
        }
        throw new IOException(L.T("Trop de redirections pendant le téléchargement.", "Too many download redirects.", "Demasiadas redirecciones durante la descarga."));
    }

    internal static void CopyLimited(Stream input, Stream output, long maximum)
    {
        CopyLimited(input, output, maximum, null, 0);
    }

    internal static void CopyLimited(Stream input, Stream output, long maximum, Stopwatch clock, long budgetMs)
    {
        byte[] buffer = new byte[32768];
        long total = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (clock != null && clock.ElapsedMilliseconds > budgetMs)
                throw new IOException(L.T("Le téléchargement est trop lent : réessaie plus tard.", "The download is too slow: try again later.", "La descarga es demasiado lenta. Inténtalo más tarde."));
            total += read;
            if (total > maximum) throw new IOException(L.T("Le téléchargement dépasse la taille autorisée.", "Download size limit exceeded.", "La descarga supera el tamaño permitido."));
            output.Write(buffer, 0, read);
        }
        // Fin du flux atteinte après l'échéance : ce n'est pas un succès (QA52-06)
        if (clock != null && clock.ElapsedMilliseconds > budgetMs)
            throw new IOException(L.T("Le téléchargement est trop lent : réessaie plus tard.", "The download is too slow: try again later.", "La descarga es demasiado lenta. Inténtalo más tarde."));
    }

    static string FetchText(string url, long maximum)
    {
        using (var output = new MemoryStream())
        {
            Fetch(url, output, maximum);
            return Encoding.UTF8.GetString(output.ToArray());
        }
    }

    // Seules les adresses HTTPS de GitHub sont acceptées pour les téléchargements
    static void RequireTrustedUrl(string url)
    {
        if (!TrustedUrl(url))
            throw new IOException(L.T("Adresse de téléchargement refusée.", "Download address refused.", "Dirección de descarga rechazada."));
    }

    internal static bool TrustedUrl(string url)
    {
        Uri u;
        return !string.IsNullOrEmpty(url) && !url.Any(char.IsControl) &&
            Uri.TryCreate(url, UriKind.Absolute, out u) && u.Scheme == Uri.UriSchemeHttps &&
            u.IsDefaultPort && u.UserInfo.Length == 0 && AllowedHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase);
    }

    // Vrai si une version plus récente avec un BureauCache.exe est disponible
    public static Release Check()
    {
        var result = new Release { Repository = RepoName };
        if (!ValidRepository(result.Repository))
        {
            result.Error = L.T("Dépôt invalide.", "Invalid repository.", "Repositorio no válido.");
            return result;
        }
        try
        {
            string json = FetchText("https://api.github.com/repos/" + result.Repository + "/releases/latest", 2 * 1024 * 1024);
            result = ParseRelease(json, result.Repository);
        }
        catch (Exception ex) { result.Error = Http.FriendlyError(ex); }
        return result;
    }

    public static string IncompleteText(Release r)
    {
        return L.T("La version " + r.Version + " est publiée, mais pas encore installable. Réessaie plus tard.",
                   "Version " + r.Version + " is published but can't be installed yet. Try again later.",
                   "La versión " + r.Version + " está publicada, pero aún no se puede instalar. Inténtalo más tarde.");
    }

    public static bool AcceptCheck(Release result)
    {
        if (result.Repository != RepoName) return false;
        current = result;
        if (result.Error == null)
        {
            Settings.LastUpdateCheck = DateTime.Now.Ticks;
            Settings.SaveQuiet();
        }
        return true;
    }

    internal static bool AutomaticCheckDue(long lastCheck, DateTime now)
    {
        return lastCheck <= 0 || lastCheck > now.Ticks || now.Ticks - lastCheck >= TimeSpan.TicksPerDay;
    }

    internal static Release ParseRelease(string json, string repository)
    {
        object root = Json.Parse(json);
        string tag = Json.Str(root, "tag_name");
        if (string.IsNullOrEmpty(tag)) throw new IOException(L.T("Aucune version publiée.", "No release published.", "No hay ninguna versión publicada."));
        Release result = new Release { Repository = repository, Version = tag.TrimStart('v', 'V'), Notes = Json.Str(root, "body") };
        Version published, installed;
        if (!TryReleaseVersion(result.Version, out published) || !TryReleaseVersion(Installer.Version, out installed))
            throw new IOException(L.T("Numéro de version invalide.", "Invalid version number.", "Número de versión no válido."));
        var assets = Json.List(root, "assets");
        if (assets != null)
        {
            foreach (object asset in assets)
            {
                string url = Json.Str(asset, "browser_download_url");
                if (!TrustedUrl(url)) continue;
                switch (Json.Str(asset, "name"))
                {
                    case "BureauCache.exe": result.Url = url; break;
                    case "BureauCache.exe.sha256": result.ShaUrl = url; break;
                    case "BureauCache.exe.sig": result.SigUrl = url; break;
                }
            }
        }
        bool newer = published > installed;
        result.Available = newer && result.Url != null && result.ShaUrl != null &&
            (result.SigUrl != null || !IsOfficial(repository));
        result.Incomplete = newer && !result.Available;
        return result;
    }

    // Notes de version raccourcies pour une boîte de dialogue
    public static string ShortNotes(Release release)
    {
        if (string.IsNullOrEmpty(release.Notes)) return "";
        string n = Regex.Replace(release.Notes, @"[#*`]", "").Trim();
        return n.Length > 700 ? n.Substring(0, 700) + "…" : n;
    }

    static bool TryReleaseVersion(string text, out Version version)
    {
        version = null;
        if (!Regex.IsMatch(text, @"\A[0-9]+(?:\.[0-9]+){0,3}\z")) return false;
        return Version.TryParse(text.IndexOf('.') < 0 ? text + ".0" : text, out version);
    }

    // Télécharge la nouvelle version et la met à la place de l'exe actuel (appliquée au redémarrage)

    public static string Sha256(string file)
    {
        return BitConverter.ToString(HashBytes(file)).Replace("-", "").ToLowerInvariant();
    }

    static byte[] HashBytes(string file)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream fs = File.OpenRead(file))
            return sha.ComputeHash(fs);
    }

    // Le dépôt officiel exige une signature ; un dépôt personnalisé (fork) n'a pas la clé de l'éditeur
    public static bool IsOfficial(string repository)
    {
        return string.Equals(repository, DefaultRepo, StringComparison.OrdinalIgnoreCase);
    }

    // Signature RSA (PKCS#1 v1.5, SHA-256) de l'exe, encodée en Base64, vérifiée contre les clés publiques de confiance
    internal static bool VerifySignature(byte[] sha256, string signatureText, string[] publicKeys)
    {
        if (sha256 == null || sha256.Length != 32 || string.IsNullOrEmpty(signatureText)) return false;
        byte[] signature;
        // Un BOM ajouté par un éditeur ne change pas la signature : il est ignoré (revue QA-21)
        try { signature = Convert.FromBase64String(signatureText.Trim().Trim('﻿').Trim()); }
        catch (FormatException) { return false; }
        string oid = CryptoConfig.MapNameToOID("SHA256");
        foreach (string key in publicKeys)
        {
            try
            {
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(new CspParameters(24)))
                {
                    rsa.PersistKeyInCsp = false;   // ne rien laisser dans le magasin de clés de Windows
                    rsa.FromXmlString(key);
                    if (rsa.KeySize >= 3072 && rsa.VerifyHash(sha256, oid, signature)) return true;
                }
            }
            catch (CryptographicException) { }
        }
        return false;
    }

    // Télécharge, vérifie l'empreinte SHA-256, puis remplace l'exe (l'ancien est gardé en .old)
    public static void Download(Release release, string exe)
    {
        RequireTrustedUrl(release.Url);
        RequireTrustedUrl(release.ShaUrl);
        // Nom aléatoire : un autre programme ne peut pas préparer un faux fichier à cet emplacement
        string tmp = Path.Combine(Path.GetDirectoryName(exe), "BureauCache-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            string expected = FetchText(release.ShaUrl, 4096).Trim().Split(' ', '\t', '\r', '\n')[0].ToLowerInvariant();
            using (var output = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Fetch(release.Url, output, MaxDownload);
                output.Flush(true);
            }
            long size = new FileInfo(tmp).Length;
            byte[] head = new byte[2];
            using (FileStream fs = File.OpenRead(tmp)) fs.Read(head, 0, 2);
            if (head[0] != 'M' || head[1] != 'Z' || size < 50000 || size > MaxDownload)
                throw new Exception(L.T("Le fichier téléchargé n'est pas un programme valide.", "The downloaded file is not a valid program.", "El archivo descargado no es un programa válido."));
            if (!Regex.IsMatch(expected, "^[0-9a-f]{64}$") || Sha256(tmp) != expected)
                throw new Exception(L.T("L'empreinte du fichier téléchargé ne correspond pas : mise à jour annulée.", "The downloaded file's checksum doesn't match: update cancelled.", "La suma de comprobación del archivo descargado no coincide. Se canceló la actualización."));
            // Dépôt officiel : la signature, faite avec une clé privée qui ne passe jamais par GitHub, protège même
            // contre un dépôt compromis qui remplacerait à la fois l'exe et son empreinte (R-04, D-004)
            if (IsOfficial(release.Repository))
            {
                if (release.SigUrl == null)
                    throw new Exception(L.T("Cette version n'est pas signée : mise à jour annulée.", "This version is not signed: update cancelled.", "La versión carece de firma. Se canceló la actualización."));
                RequireTrustedUrl(release.SigUrl);
                string signature = FetchText(release.SigUrl, 4096);
                if (!VerifySignature(HashBytes(tmp), signature, UpdateKeys.Trusted))
                    throw new Exception(L.T("La signature du fichier téléchargé n'est pas valide : mise à jour annulée.", "The downloaded file's signature is not valid: update cancelled.", "La firma del archivo descargado no es válida. Se canceló la actualización."));
            }
            string fileVersion = FileVersionInfo.GetVersionInfo(tmp).ProductVersion ?? "";
            if (!string.Equals(fileVersion, release.Version, StringComparison.Ordinal))
                throw new Exception(L.T("La version du fichier téléchargé ne correspond pas à la version annoncée.", "The downloaded file's version doesn't match the announced version.", "La versión del archivo descargado no coincide con la anunciada."));
            ReplaceExecutable(tmp, exe);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    internal static void ReplaceExecutable(string staged, string exe)
    {
        Sandbox.GuardPath(exe, "remplacement de l'exe");
        Sandbox.GuardPath(staged, "fichier de mise à jour");
        // .old est la dernière version sûre dès le remplacement, avant même la création de .trial.
        string backup = File.Exists(exe + ".old") ? exe + ".replaced" : exe + ".old";
        TryDelete(exe + ".replaced");
        try { File.Replace(staged, exe, backup); }
        catch
        {
            if (!File.Exists(exe) && File.Exists(backup)) File.Move(backup, exe);
            throw;
        }
        TryDelete(exe + ".trial");   // la nouvelle version commence son essai à zéro
    }

    // ---------- Retour arrière après une mise à jour (R-07) ----------
    // Après un remplacement, l'ancienne version reste à côté en « .old » et la nouvelle est à l'essai. Chaque
    // démarrage à l'essai pose un marqueur « .trial », effacé avec « .old » une fois le démarrage confirmé.
    // Un marqueur encore présent au démarrage suivant signifie que l'essai précédent n'a pas abouti (plantage) :
    // l'ancienne version est remise en place. La version fautive est gardée en « .bad ».

    internal enum StartState { Normal, Trial, RolledBack }

    // Vrai si ce processus a commencé un essai : lui seul peut le confirmer (pas l'ancienne version encore en cours)
    public static bool TrialStarted;

    internal static StartState BeginStart(string exe)
    {
        Sandbox.GuardPath(exe, "essai de mise à jour");
        trialPath = trialHash = null;
        string old = exe + ".old", marker = exe + ".trial";
        TryDelete(exe + ".replaced");
        if (!File.Exists(old))
        {
            TryDelete(marker);
            TryDelete(exe + ".bad");
            return StartState.Normal;
        }
        if (File.Exists(marker))
        {
            try { RollBack(exe); return StartState.RolledBack; }
            catch { RememberTrialExe(exe); return StartState.Trial; }   // retour impossible : on retente la nouvelle version
        }
        // Sans marqueur, un plantage ne serait pas détecté : pas d'essai, « .old » est simplement gardé (revue QA-21)
        try { File.WriteAllText(marker, DateTime.Now.ToString("o")); }
        catch { return StartState.Normal; }
        RememberTrialExe(exe);
        return StartState.Trial;
    }

    // Le processus ne confirme que le fichier dont il a mémorisé le chemin et les octets au début de l'essai.
    static string trialPath, trialHash;

    static void RememberTrialExe(string exe)
    {
        try
        {
            trialHash = Sha256(exe);
            trialPath = Path.GetFullPath(exe);
        }
        catch { trialPath = trialHash = null; }
    }

    // L'exe en cours d'exécution peut être renommé (pas supprimé) : la version fautive devient « .bad ».
    // Reprend aussi un retour interrompu entre les deux déplacements (exe déjà parti en « .bad »).
    internal static void RollBack(string exe)
    {
        Sandbox.GuardPath(exe, "retour arrière");
        string old = exe + ".old", bad = exe + ".bad";
        if (File.Exists(exe))
        {
            TryDelete(bad);
            File.Move(exe, bad);
        }
        try { File.Move(old, exe); }
        catch
        {
            if (!File.Exists(exe) && File.Exists(bad)) File.Move(bad, exe);
            throw;
        }
        TryDelete(exe + ".trial");
    }

    // Démarrage réussi : l'ancienne version et le marqueur d'essai ne servent plus. Sans marqueur, il n'y a pas
    // d'essai en cours (par exemple une mise à jour vient de remplacer l'exe) : on ne touche à rien.
    internal static void ConfirmStart(string exe)
    {
        Sandbox.GuardPath(exe, "confirmation de l'essai");
        if (!string.Equals(Path.GetFullPath(exe), trialPath, StringComparison.OrdinalIgnoreCase)) return;
        if (!File.Exists(exe + ".trial")) return;
        if (!string.Equals(Sha256(exe), trialHash, StringComparison.Ordinal)) return;
        TryDelete(exe + ".old");
        TryDelete(exe + ".trial");
    }

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
