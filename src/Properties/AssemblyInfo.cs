// BureauCache — informations du programme
// Le numéro de version n'est écrit qu'ici : publier.ps1 et GitHub Actions s'en servent.
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyTitle("BureauCache")]
[assembly: AssemblyProduct("BureauCache")]
[assembly: AssemblyDescription("Ton bureau, rangé sur le côté de l'écran")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Kuroi1998 — licence MIT")]
[assembly: AssemblyVersion(AppInfo.Version + ".0.0")]
[assembly: AssemblyFileVersion(AppInfo.Version + ".0.0")]
[assembly: AssemblyInformationalVersion(AppInfo.Version)]
// Les classes de l'application sont internes : le projet tests/BureauCache.Tests y accède par ce biais
[assembly: InternalsVisibleTo("BureauCache.Tests")]

static class AppInfo
{
    public const string Version = "3.6";
    public const string Repo = "Kuroi1998/BureauCache";
}
