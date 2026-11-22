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
    public static string Latest, Url, ShaUrl, Notes, PageUrl, Error;

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

    public static bool RepoValid { get { return Regex.IsMatch(RepoName, @"^[\w.-]+/[\w.-]+$"); } }

    const long MaxDownload = 50L * 1024 * 1024;
    static readonly string[] AllowedHosts = { "api.github.com", "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com" };

    static WebClient Client()
    {
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2 (et plus récent si Windows le propose)
        WebClient wc = new WebClient();
        wc.Headers[HttpRequestHeader.UserAgent] = "BureauCache-Updater";
        wc.Encoding = Encoding.UTF8;
        return wc;
    }

    // Seules les adresses HTTPS de GitHub sont acceptées pour les téléchargements
    static void RequireTrustedUrl(string url)
    {
        Uri u;
        if (!Uri.TryCreate(url, UriKind.Absolute, out u) || u.Scheme != Uri.UriSchemeHttps ||
            !AllowedHosts.Contains(u.Host.ToLowerInvariant()))
            throw new Exception(L.T("Adresse de téléchargement refusée : ", "Download address refused: ") + url);
    }

    // Vrai si une version plus récente avec un BureauCache.exe est disponible
    public static bool Check()
    {
        Latest = Url = ShaUrl = Notes = PageUrl = Error = null;
        if (!RepoValid) { Error = L.T("Dépôt invalide (format : utilisateur/projet).", "Invalid repository (format: owner/name)."); return false; }
        try
        {
            string json;
            using (WebClient wc = Client()) json = wc.DownloadString("https://api.github.com/repos/" + RepoName + "/releases/latest");
            Match t = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            Match a = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+/BureauCache\\.exe)\"");
            Match h = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+/BureauCache\\.exe\\.sha256)\"");
            Match b = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            Match p = Regex.Match(json, "\"html_url\"\\s*:\\s*\"([^\"]+/releases/tag/[^\"]+)\"");
            if (!t.Success) { Error = L.T("Aucune version publiée.", "No release published."); return false; }
            Latest = t.Groups[1].Value.TrimStart('v', 'V');
            Url = a.Success ? a.Groups[1].Value : null;
            ShaUrl = h.Success ? h.Groups[1].Value : null;
            Notes = b.Success ? Unescape(b.Groups[1].Value) : null;
            PageUrl = p.Success ? p.Groups[1].Value : "https://github.com/" + RepoName + "/releases";
            Settings.LastUpdateCheck = DateTime.Now.Ticks;
            Settings.SaveQuiet();
            return IsNewer(Latest, Installer.Version) && Url != null && ShaUrl != null;
        }
        catch (Exception ex) { Error = ex.Message; return false; }
    }

    static string Unescape(string s)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\' || i + 1 >= s.Length) { sb.Append(s[i]); continue; }
            char c = s[++i];
            switch (c)
            {
                case 'n': sb.Append('\n'); break;
                case 'r': break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (i + 4 < s.Length) { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString().Trim();
    }

    // Notes de version raccourcies pour une boîte de dialogue
    public static string ShortNotes
    {
        get
        {
            if (string.IsNullOrEmpty(Notes)) return "";
            string n = Regex.Replace(Notes, @"[#*`]", "").Trim();
            return n.Length > 700 ? n.Substring(0, 700) + "…" : n;
        }
    }

    static bool IsNewer(string a, string b)
    {
        Version va, vb;
        if (a.IndexOf('.') < 0) a += ".0";
        if (b.IndexOf('.') < 0) b += ".0";
        return Version.TryParse(a, out va) && Version.TryParse(b, out vb) && va > vb;
    }

    // Télécharge la nouvelle version et la met à la place de l'exe actuel (appliquée au redémarrage)
    public static void Download() { Download(Application.ExecutablePath); App.RelaunchPath = Application.ExecutablePath; }

    public static string Sha256(string file)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream fs = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
    }

    // Télécharge, vérifie l'empreinte SHA-256, puis remplace l'exe (l'ancien est gardé en .old)
    public static void Download(string exe)
    {
        RequireTrustedUrl(Url);
        RequireTrustedUrl(ShaUrl);
        // Nom aléatoire : un autre programme ne peut pas préparer un faux fichier à cet emplacement
        string tmp = Path.Combine(Path.GetTempPath(), "BureauCache-" + Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            string expected;
            using (WebClient wc = Client())
            {
                expected = wc.DownloadString(ShaUrl).Trim().Split(' ', '\t', '\r', '\n')[0].ToLowerInvariant();
                wc.DownloadFile(Url, tmp);
            }
            long size = new FileInfo(tmp).Length;
            byte[] head = new byte[2];
            using (FileStream fs = File.OpenRead(tmp)) fs.Read(head, 0, 2);
            if (head[0] != 'M' || head[1] != 'Z' || size < 50000 || size > MaxDownload)
                throw new Exception(L.T("Le fichier téléchargé n'est pas un programme valide.", "The downloaded file is not a valid program."));
            if (!Regex.IsMatch(expected, "^[0-9a-f]{64}$") || Sha256(tmp) != expected)
                throw new Exception(L.T("L'empreinte du fichier téléchargé ne correspond pas : mise à jour annulée.", "The downloaded file's checksum doesn't match: update cancelled."));
            string fileVersion = FileVersionInfo.GetVersionInfo(tmp).ProductVersion ?? "";
            if (!fileVersion.StartsWith(Latest))
                throw new Exception(L.T("La version du fichier téléchargé ne correspond pas à la version annoncée.", "The downloaded file's version doesn't match the announced version."));
            string old = exe + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            File.Copy(tmp, exe, true);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    public static void CleanupOld()
    {
        try { string old = Application.ExecutablePath + ".old"; if (File.Exists(old)) File.Delete(old); } catch { }
    }
}
