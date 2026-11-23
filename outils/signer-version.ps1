# Compile, signe et publie une version, à partir du brouillon créé par GitHub Actions (décision D-004, revue QA-21).
# Déroulement : publier.ps1 pousse le tag -> GitHub Actions vérifie que le tag compile et crée un BROUILLON (invisible
# des installations) -> ce script COMPILE LUI-MÊME le tag sur ce PC, remplace l'exe du brouillon par cette
# compilation, la signe avec la clé principale et publie.
# Pourquoi recompiler : un GitHub compromis peut glisser un exe piégé dans le brouillon, et la compilation de la CI
# ne peut pas être comparée octet par octet à une compilation locale (versions du compilateur différentes). En
# signant sa propre compilation, un piège doit passer par le code source du tag, que ce script te montre.
# Le code du tag est seulement lu (git archive) et compilé : aucun script du dépôt n'est exécuté.
# Usage : .\outils\signer-version.ps1 -Version 3.4  [-Cle chemin\principale.cle]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Cle
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\cles-commun.ps1"
. "$PSScriptRoot\compilation-commun.ps1"
$depot = Split-Path $PSScriptRoot -Parent
if (-not $Cle) { $Cle = Join-Path (Get-DossierClesParDefaut) 'principale.cle' }
if ($Version -notmatch '^[0-9]+\.[0-9]+(?:\.[0-9]+)?$') { throw "Format attendu : X.Y ou X.Y.Z (ex. 3.7.1)" }
if (-not (Test-Path $Cle)) { throw "Clé introuvable : $Cle (voir outils\creer-cles.ps1)" }
$publiques = Get-ClesPubliquesDuCode $depot
if ($publiques.Count -eq 0) { throw "src\Core\UpdateKeys.cs ne contient aucune clé publique." }
$tag = "v$Version"
# Toutes les commandes gh visent le dépôt d'origin, celui où le tag est contrôlé, et non le dépôt du dossier
# courant : lancé depuis un autre dépôt, le script agirait sinon sur un brouillon de celui-ci (R-28)
$origine = git -C $depot remote get-url origin
# Hôte exact (Q24-06) : https://github.com/…, git@github.com:…, ssh://git@github.com/…, ssh://git@ssh.github.com:443/…
if ($origine -notmatch '^(?:https://github\.com/|git@github\.com:|ssh://git@github\.com/|ssh://git@ssh\.github\.com:443/)([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+?)(?:\.git)?/?$') { throw "Remote origin inattendu : $origine" }
$repo = $Matches[1]
Write-Host "Dépôt GitHub : $repo"

# 1. Le tag local doit être celui de GitHub (un tag déplacé côté GitHub ferait compiler autre chose)
$local = git -C $depot rev-parse --verify --quiet "$tag^{commit}"
if (-not $local) { throw "Le tag $tag n'existe pas dans ce dépôt local (git fetch --tags)." }
# Pour un tag annoté, la ligne « ^{} » donne le commit pointé
$lignes = @(git -C $depot ls-remote origin "refs/tags/$tag" "refs/tags/$tag^{}")
$ligne = @($lignes | Where-Object { $_ -match '\^\{\}$' }) + @($lignes) | Select-Object -First 1
$distant = if ($ligne) { ($ligne -split '\s+')[0] } else { $null }
if ($distant -ne $local) { throw "Le tag $tag de GitHub ($distant) diffère du tag local ($local) : n'utilise pas ce brouillon." }

# 2. Le brouillon doit exister et ne pas être publié
$infos = gh release view $tag --repo $repo --json isDraft,assets | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw "Version $tag introuvable sur GitHub (la compilation de la CI est-elle terminée ?)" }
if (-not $infos.isDraft) { throw "La version $tag est déjà publiée : rien à signer." }

# 3. Ce qui a changé depuis la version précédente : à relire avant de signer
$precedent = git -C $depot describe --tags --abbrev=0 "$tag^" 2>$null
if ($precedent) {
    Write-Host "Changements depuis $precedent :" -ForegroundColor Cyan
    git -C $depot --no-pager diff --stat $precedent $tag
    $sensibles = @(git -C $depot diff --name-only $precedent $tag -- .github build.cmd publier.ps1 outils src/Core/UpdateKeys.cs src/Core/Updater.cs)
    if ($sensibles.Count -gt 0) {
        Write-Host "ATTENTION, fichiers sensibles modifiés (publication, signature, mise à jour) :" -ForegroundColor Yellow
        $sensibles | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
        Write-Host "Relis-les (git diff $precedent $tag -- <fichier>) avant de continuer." -ForegroundColor Yellow
    }
}

$tmp = Join-Path $env:TEMP ("bc-signature-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $tmp | Out-Null
try {
    # 4. Compilation de référence, sur ce PC
    $exe = New-CompilationDeReference $depot $tag $tmp
    $final = Join-Path $tmp 'BureauCache.exe'
    Move-Item $exe $final
    $produit = (Get-Item $final).VersionInfo.ProductVersion
    if ($produit -ne $Version) { throw "Le tag $tag compile une version $produit au lieu de $Version." }
    $sha = [Security.Cryptography.SHA256]::Create()
    $flux = [IO.File]::OpenRead($final)
    try { $empreinte = $sha.ComputeHash($flux) } finally { $flux.Dispose() }
    $hex = -join ($empreinte | ForEach-Object { $_.ToString('x2') })

    Write-Host ""
    Write-Host "Version  : $produit (compilée sur ce PC depuis le tag $tag, commit $local)"
    Write-Host "SHA-256  : $hex"
    if ((Read-Host "Signer et publier cette compilation ? (o/n)") -ne 'o') { throw "Annulé : rien n'a été signé." }

    # 5. Signature
    $mdp = ConvertTo-OctetsMotDePasse (Read-Host -AsSecureString "Mot de passe de la clé")
    try { $xml = Unprotect-Cle ([IO.File]::ReadAllBytes($Cle)) $mdp }
    finally { [Array]::Clear($mdp, 0, $mdp.Length) }
    $rsa = New-Object Security.Cryptography.RSACryptoServiceProvider((New-Object Security.Cryptography.CspParameters(24)))
    $rsa.PersistKeyInCsp = $false
    try {
        $rsa.FromXmlString($xml)
        $signature = [Convert]::ToBase64String($rsa.SignHash($empreinte, [Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256')))
    } finally { $rsa.Dispose(); $xml = $null }
    if (-not (Test-Signature $empreinte $signature $publiques)) { throw "La signature ne correspond à aucune clé de src\Core\UpdateKeys.cs : mauvaise clé ?" }

    # 6. Remplacement des fichiers du brouillon par la compilation locale
    $fichierSha = Join-Path $tmp 'BureauCache.exe.sha256'
    $fichierSig = Join-Path $tmp 'BureauCache.exe.sig'
    [IO.File]::WriteAllText($fichierSha, "$hex  BureauCache.exe", (New-Object Text.ASCIIEncoding))
    [IO.File]::WriteAllText($fichierSig, $signature, (New-Object Text.ASCIIEncoding))
    gh release upload $tag --repo $repo $final $fichierSha $fichierSig --clobber
    if ($LASTEXITCODE -ne 0) { throw "Envoi des fichiers impossible : le brouillon n'est pas publié." }

    # 7. Relecture de ce que GitHub sert réellement, avant de publier
    $verif = Join-Path $tmp 'verification'
    New-Item -ItemType Directory $verif | Out-Null
    gh release download $tag --repo $repo -p BureauCache.exe -p BureauCache.exe.sig -p BureauCache.exe.sha256 -D $verif
    if ($LASTEXITCODE -ne 0) { throw "Relecture du brouillon impossible : il n'est pas publié." }
    $flux = [IO.File]::OpenRead((Join-Path $verif 'BureauCache.exe'))
    try { $relu = $sha.ComputeHash($flux) } finally { $flux.Dispose() }
    if ((-join ($relu | ForEach-Object { $_.ToString('x2') })) -ne $hex -or
        -not (Test-Signature $relu ([IO.File]::ReadAllText((Join-Path $verif 'BureauCache.exe.sig'))) $publiques)) {
        throw "Le brouillon ne sert pas les fichiers envoyés : il n'est pas publié. Vérifie-le sur GitHub."
    }
    # L'empreinte publiée est relue aussi : fausse, elle rendrait la version inutilisable par les clients (R-29)
    # Octet par octet avec le fichier produit ici : un séparateur exotique passerait un découpage (Q24-07)
    $shaRelu = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $verif 'BureauCache.exe.sha256')))
    if ($shaRelu -cne [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $tmp 'BureauCache.exe.sha256')))) { throw "Le brouillon sert une empreinte .sha256 différente : il n'est pas publié. Vérifie-le sur GitHub." }

    gh release edit $tag --repo $repo --draft=false
    if ($LASTEXITCODE -ne 0) { throw "Fichiers en place, mais la publication a échoué : publie le brouillon sur GitHub." }
    Write-Host "Version $Version compilée, signée et publiée." -ForegroundColor Green
} finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
