// BureauCache — Http
using System;
using System.IO;
using System.Net;
using System.Text;

// =====================================================================
//  Téléchargement de texte limité à une liste d'hôtes HTTPS (météo)
// =====================================================================
static class Http
{
    // Redirections suivies une à une : chaque saut doit rester en HTTPS sur un hôte autorisé
    public static string FetchText(string url, string[] hosts, long maximum)
    {
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
        string firstHost = null;
        for (int redirect = 0; redirect <= 3; redirect++)
        {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u) || u.Scheme != Uri.UriSchemeHttps || !u.IsDefaultPort ||
                Array.IndexOf(hosts, u.Host.ToLowerInvariant()) < 0)
                throw new IOException(L.T("Adresse refusée : ", "Address refused: ") + url);
            // Une redirection reste sur le même service : la requête (coordonnées, ville) ne part pas ailleurs (revue QA-23)
            if (firstHost == null) firstHost = u.Host;
            else if (!string.Equals(u.Host, firstHost, StringComparison.OrdinalIgnoreCase))
                throw new IOException(L.T("Redirection refusée : ", "Redirect refused: ") + url);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(u);
            request.AllowAutoRedirect = false;
            request.UserAgent = "BureauCache/" + AppInfo.Version;
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                int status = (int)response.StatusCode;
                if (status >= 300 && status < 400)
                {
                    string location = response.Headers[HttpResponseHeader.Location];
                    if (string.IsNullOrEmpty(location)) throw new IOException("Missing redirect location.");
                    url = new Uri(u, location).AbsoluteUri;
                    continue;
                }
                if (status != 200 || response.ContentLength > maximum) throw new IOException("Unexpected response.");
                using (Stream input = response.GetResponseStream())
                using (MemoryStream output = new MemoryStream())
                {
                    Updater.CopyLimited(input, output, maximum);
                    return Encoding.UTF8.GetString(output.ToArray());
                }
            }
        }
        throw new IOException("Too many redirects.");
    }
}
