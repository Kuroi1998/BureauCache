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
        public string Version, Url, ShaUrl, Notes, Error;
        public string Repository;
        public bool Available;
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

    public static bool RepoValid { get { return Regex.IsMatch(RepoName, @"^[\w.-]+/[\w.-]+$"); } }

    const long MaxDownload = 50L * 1024 * 1024;
    static readonly string[] AllowedHosts = { "api.github.com", "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com" };

    static void Fetch(string url, Stream output, long maximum)
    {
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
        for (int redirect = 0; redirect <= 5; redirect++)
        {
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
                    if (string.IsNullOrEmpty(location)) throw new IOException("Missing redirect location.");
                    url = new Uri(request.RequestUri, location).AbsoluteUri;
                    continue;
                }
                if (status != 200 || response.ContentLength > maximum)
                    throw new IOException("Unexpected download response or file size.");
                using (Stream input = response.GetResponseStream()) CopyLimited(input, output, maximum);
                return;
            }
        }
        throw new IOException("Too many download redirects.");
    }

    internal static void CopyLimited(Stream input, Stream output, long maximum)
    {
        byte[] buffer = new byte[32768];
        long total = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maximum) throw new IOException("Download size limit exceeded.");
            output.Write(buffer, 0, read);
        }
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
        Uri u;
        if (!Uri.TryCreate(url, UriKind.Absolute, out u) || u.Scheme != Uri.UriSchemeHttps ||
            !AllowedHosts.Contains(u.Host.ToLowerInvariant()))
            throw new Exception(L.T("Adresse de téléchargement refusée : ", "Download address refused: ") + url);
    }

    // Vrai si une version plus récente avec un BureauCache.exe est disponible
    public static Release Check()
    {
        var result = new Release { Repository = RepoName };
        if (!Regex.IsMatch(result.Repository, @"^[\w.-]+/[\w.-]+$"))
        {
            result.Error = L.T("Dépôt invalide.", "Invalid repository.");
            return result;
        }
        try
        {
            string json = FetchText("https://api.github.com/repos/" + result.Repository + "/releases/latest", 2 * 1024 * 1024);
            Match t = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            Match a = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+/BureauCache\\.exe)\"");
            Match h = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+/BureauCache\\.exe\\.sha256)\"");
            Match b = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!t.Success) throw new IOException(L.T("Aucune version publiée.", "No release published."));
            result.Version = t.Groups[1].Value.TrimStart('v', 'V');
            result.Url = a.Success ? a.Groups[1].Value : null;
            result.ShaUrl = h.Success ? h.Groups[1].Value : null;
            result.Notes = b.Success ? Unescape(b.Groups[1].Value) : null;
            result.Available = IsNewer(result.Version, Installer.Version) && result.Url != null && result.ShaUrl != null;
        }
        catch (Exception ex) { result.Error = ex.Message; }
        return result;
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
    public static string ShortNotes(Release release)
    {
        if (string.IsNullOrEmpty(release.Notes)) return "";
        string n = Regex.Replace(release.Notes, @"[#*`]", "").Trim();
        return n.Length > 700 ? n.Substring(0, 700) + "…" : n;
    }

    static bool IsNewer(string a, string b)
    {
        Version va, vb;
        if (a.IndexOf('.') < 0) a += ".0";
        if (b.IndexOf('.') < 0) b += ".0";
        return Version.TryParse(a, out va) && Version.TryParse(b, out vb) && va > vb;
    }

    // Télécharge la nouvelle version et la met à la place de l'exe actuel (appliquée au redémarrage)

    public static string Sha256(string file)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream fs = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
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
                throw new Exception(L.T("Le fichier téléchargé n'est pas un programme valide.", "The downloaded file is not a valid program."));
            if (!Regex.IsMatch(expected, "^[0-9a-f]{64}$") || Sha256(tmp) != expected)
                throw new Exception(L.T("L'empreinte du fichier téléchargé ne correspond pas : mise à jour annulée.", "The downloaded file's checksum doesn't match: update cancelled."));
            string fileVersion = FileVersionInfo.GetVersionInfo(tmp).ProductVersion ?? "";
            if (!string.Equals(fileVersion, release.Version, StringComparison.Ordinal))
                throw new Exception(L.T("La version du fichier téléchargé ne correspond pas à la version annoncée.", "The downloaded file's version doesn't match the announced version."));
            ReplaceExecutable(tmp, exe);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    internal static void ReplaceExecutable(string staged, string exe)
    {
        string backup = exe + ".old";
        try { File.Replace(staged, exe, backup); }
        catch
        {
            if (!File.Exists(exe) && File.Exists(backup)) File.Move(backup, exe);
            throw;
        }
    }

    public static void CleanupOld()
    {
        try { string old = Application.ExecutablePath + ".old"; if (File.Exists(old)) File.Delete(old); } catch { }
    }
}
