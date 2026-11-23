# Signe une version préparée par GitHub Actions, puis la publie (décision D-004).
# Déroulement : publier.ps1 pousse le tag -> GitHub Actions compile et crée la version en BROUILLON (invisible des
# installations) -> ce script télécharge l'exe du brouillon, le contrôle, le signe avec la clé principale, ajoute
# BureauCache.exe.sig et publie. La clé privée ne quitte jamais ce PC.
# Usage : .\outils\signer-version.ps1 -Version 3.4  [-Cle chemin\principale.cle]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Cle
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\cles-commun.ps1"
$depot = Split-Path $PSScriptRoot -Parent
if (-not $Cle) { $Cle = Join-Path (Get-DossierClesParDefaut) 'principale.cle' }
if ($Version -notmatch '^[0-9]+\.[0-9]+$') { throw "Format attendu : X.Y (ex. 3.4)" }
if (-not (Test-Path $Cle)) { throw "Clé introuvable : $Cle (voir outils\creer-cles.ps1)" }
$publiques = Get-ClesPubliquesDuCode $depot
if ($publiques.Count -eq 0) { throw "src\Core\UpdateKeys.cs ne contient aucune clé publique." }
$tag = "v$Version"

$infos = gh release view $tag --json isDraft,assets | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw "Version $tag introuvable sur GitHub (la compilation est-elle terminée ?)" }
if (-not $infos.isDraft) { throw "La version $tag est déjà publiée : rien à signer." }
$noms = @($infos.assets | ForEach-Object { $_.name })
foreach ($n in @('BureauCache.exe', 'BureauCache.exe.sha256')) { if ($noms -notcontains $n) { throw "Fichier manquant dans le brouillon : $n" } }
if ($noms -contains 'BureauCache.exe.sig') { throw "Le brouillon contient déjà une signature : vérifie-le sur GitHub." }

$tmp = Join-Path $env:TEMP ("bc-signature-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $tmp | Out-Null
try {
    gh release download $tag -p BureauCache.exe -p BureauCache.exe.sha256 -D $tmp
    if ($LASTEXITCODE -ne 0) { throw "Téléchargement du brouillon impossible." }
    $exe = Join-Path $tmp 'BureauCache.exe'

    $produit = (Get-Item $exe).VersionInfo.ProductVersion
    if ($produit -ne $Version) { throw "L'exe du brouillon annonce la version $produit au lieu de $Version." }
    $sha = [Security.Cryptography.SHA256]::Create()
    $flux = [IO.File]::OpenRead($exe)
    try { $empreinte = $sha.ComputeHash($flux) } finally { $flux.Dispose() }
    $hex = -join ($empreinte | ForEach-Object { $_.ToString('x2') })
    $annonce = ((Get-Content (Join-Path $tmp 'BureauCache.exe.sha256') -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
    if ($annonce -ne $hex) { throw "L'empreinte publiée ne correspond pas à l'exe du brouillon." }

    Write-Host "Version  : $produit"
    Write-Host "SHA-256  : $hex"
    Write-Host "Compilé par GitHub Actions à partir du tag $tag ($(git rev-list -n 1 $tag 2>$null))."
    if ((Read-Host "Signer et publier cette version ? (o/n)") -ne 'o') { throw "Annulé : rien n'a été signé." }

    $mdp = ConvertTo-OctetsMotDePasse (Read-Host -AsSecureString "Mot de passe de la clé")
    try { $xml = Unprotect-Cle ([IO.File]::ReadAllBytes($Cle)) $mdp }
    finally { [Array]::Clear($mdp, 0, $mdp.Length) }
    $rsa = New-Object Security.Cryptography.RSACryptoServiceProvider((New-Object Security.Cryptography.CspParameters(24)))
    $rsa.PersistKeyInCsp = $false
    try {
        $rsa.FromXmlString($xml)
        $signature = [Convert]::ToBase64String($rsa.SignHash($empreinte, [Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256')))
    } finally { $rsa.Dispose(); $xml = $null }

    # Contrôle avec les clés publiques de l'application, exactement comme le fera l'updater
    if (-not (Test-Signature $empreinte $signature $publiques)) { throw "La signature ne correspond à aucune clé de src\Core\UpdateKeys.cs : mauvaise clé ?" }

    $fichierSig = Join-Path $tmp 'BureauCache.exe.sig'
    [IO.File]::WriteAllText($fichierSig, $signature, (New-Object Text.ASCIIEncoding))
    gh release upload $tag $fichierSig
    if ($LASTEXITCODE -ne 0) { throw "Envoi de la signature impossible." }
    gh release edit $tag --draft=false
    if ($LASTEXITCODE -ne 0) { throw "La signature est envoyée mais la publication a échoué : publie le brouillon sur GitHub." }
    Write-Host "Version $Version signée et publiée." -ForegroundColor Green
} finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
