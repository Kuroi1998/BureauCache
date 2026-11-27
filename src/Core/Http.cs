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
        Sandbox.Guard("accès réseau");   // les tests n'appellent jamais les vrais services
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
        string firstHost = null;
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();   // 30 s au total (SEC-12)
        for (int redirect = 0; redirect <= 3; redirect++)
        {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u) || u.Scheme != Uri.UriSchemeHttps || !u.IsDefaultPort ||
                Array.IndexOf(hosts, u.Host.ToLowerInvariant()) < 0)
                throw new IOException(L.T("Adresse refusée : ", "Address refused: ", "Dirección rechazada: ") + url);
            // Une redirection reste sur le même service : la requête (coordonnées, ville) ne part pas ailleurs (revue QA-23)
            if (firstHost == null) firstHost = u.Host;
            else if (!string.Equals(u.Host, firstHost, StringComparison.OrdinalIgnoreCase))
                throw new IOException(L.T("Redirection refusée : ", "Redirect refused: ", "Redirección rechazada: ") + url);
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
                    if (string.IsNullOrEmpty(location)) throw new IOException(L.T("Redirection sans adresse.", "Missing redirect location.", "Redirección sin dirección."));
                    url = new Uri(u, location).AbsoluteUri;
                    continue;
                }
                if (status != 200 || response.ContentLength > maximum) throw new IOException(L.T("Réponse inattendue du serveur.", "Unexpected server response.", "Respuesta inesperada del servidor."));
                using (Stream input = response.GetResponseStream())
                using (MemoryStream output = new MemoryStream())
                {
                    Updater.CopyLimited(input, output, maximum, clock, 30000);
                    return Encoding.UTF8.GetString(output.ToArray());
                }
            }
        }
        throw new IOException(L.T("Trop de redirections.", "Too many redirects.", "Demasiadas redirecciones."));
    }
    // Message court et traduit pour une erreur réseau, au lieu du texte système souvent en anglais (GEM-12).
    // Les messages déjà rédigés par BureauCache (IOException levées ici ou par Updater) sont gardés tels quels.
    public static string FriendlyError(Exception ex)
    {
        WebException web = ex as WebException;
        if (web == null && ex != null) web = ex.InnerException as WebException;
        if (web == null) return ex == null ? "" : ex.Message;
        switch (web.Status)
        {
            case WebExceptionStatus.NameResolutionFailure:
            case WebExceptionStatus.ConnectFailure:
            case WebExceptionStatus.ProxyNameResolutionFailure:
                return L.T("Pas de connexion à Internet. Vérifie ta connexion puis réessaie.", "No Internet connection. Check your connection and try again.", "Sin conexión a Internet. Comprueba la conexión y vuelve a intentarlo.");
            case WebExceptionStatus.Timeout:
                return L.T("Le service ne répond pas. Réessaie dans un moment.", "The service isn't responding. Try again in a moment.", "El servicio no responde. Vuelve a intentarlo en un momento.");
            case WebExceptionStatus.TrustFailure:
            case WebExceptionStatus.SecureChannelFailure:
                return L.T("Connexion sécurisée impossible (date de l'ordinateur ou réseau filtré ?).", "Secure connection failed (computer clock or filtered network?).", "No se pudo establecer una conexión segura (¿fecha del equipo o red filtrada?).");
            case WebExceptionStatus.ProtocolError:
                HttpWebResponse r = web.Response as HttpWebResponse;
                int code = r != null ? (int)r.StatusCode : 0;
                if (code == 403 || code == 429)
                    return L.T("Trop de demandes envoyées au service. Réessaie plus tard.", "Too many requests sent to the service. Try again later.", "Se enviaron demasiadas solicitudes al servicio. Inténtalo más tarde.");
                if (code >= 500)
                    return L.T("Le service est momentanément indisponible. Réessaie plus tard.", "The service is temporarily unavailable. Try again later.", "El servicio no está disponible por ahora. Inténtalo más tarde.");
                return L.T("Le service a refusé la demande (erreur ", "The service refused the request (error ", "El servicio rechazó la solicitud (error ") + code + ").";
        }
        return L.T("Erreur réseau. Réessaie plus tard.", "Network error. Try again later.", "Error de red. Inténtalo más tarde.");
    }
}
