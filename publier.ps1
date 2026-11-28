# Publie une nouvelle version de BureauCache.
# Usage : .\publier.ps1 -Version 3.3
# 1. Ajoute d'abord une section « ## 3.3 — Titre » en haut de CHANGELOG.md
# 2. Le script met la version dans src\Properties\AssemblyInfo.cs, compile, crée le commit et le tag, puis pousse.
#    GitHub Actions compile ensuite le programme et crée la version en brouillon.
# 3. Une fois la compilation terminée, lance .\outils\signer-version.ps1 -Version 3.3 : il recompile le tag sur ce PC,
#    signe cette compilation avec ta clé et publie la version (les installations la proposeront alors automatiquement).
#    Avant la signature de mise à jour, signer-version.ps1 signe aussi l'exe avec le certificat de code (Authenticode,
#    outils\signer-code.ps1, configuration par variables d'environnement), construit l'installateur
#    BureauCache-Setup-3.3.exe (installer\BureauCache.iss, Inno Setup 6) et publie les deux fichiers.
#    Sans certificat configuré, le script le signale et demande une confirmation explicite (voir installer\README.md).
param([Parameter(Mandatory = $true)][string]$Version)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if ($Version -notmatch '^[0-9]+\.[0-9]+(?:\.[0-9]+)?$') { throw "Format attendu : X.Y ou X.Y.Z (ex. 3.7.1)" }
if (-not (Select-String -Path CHANGELOG.md -Pattern "^## $([regex]::Escape($Version))(?=\s|$)" -Quiet)) {
    throw "Ajoute d'abord une section '## $Version' dans CHANGELOG.md"
}
if ((git branch --show-current) -ne 'main') { throw "Publie depuis la branche main" }

$pending = git status --porcelain
if ($LASTEXITCODE -ne 0) { throw "Impossible de lire l'état du dépôt" }
if (@($pending | Where-Object { $_ -notin @(' M CHANGELOG.md', 'M  CHANGELOG.md', 'MM CHANGELOG.md') }).Count -gt 0) {
    throw "Enregistre les autres changements avant de publier"
}

$info = Join-Path $PSScriptRoot 'src\Properties\AssemblyInfo.cs'
$tagRef = "refs/tags/v$Version"

function Send-Release {
    git push --atomic origin HEAD $tagRef
    if ($LASTEXITCODE -ne 0) { throw "Échec de l'envoi du commit et du tag. Relance la même commande pour réessayer." }
    "Le commit et le tag v$Version sont disponibles sur origin. Consulte GitHub Actions pour suivre la compilation."
    "Quand le brouillon est prêt, signe et publie avec : .\outils\signer-version.ps1 -Version $Version"
}

$existingTag = git tag -l "v$Version"
if ($LASTEXITCODE -ne 0) { throw "Impossible de lire les tags du dépôt" }
if ($existingTag) {
    $tagCommit = git rev-parse --verify "$tagRef^{commit}"
    if ($LASTEXITCODE -ne 0) { throw "Impossible de lire le commit de v$Version" }
    $headCommit = git rev-parse --verify HEAD
    if ($LASTEXITCODE -ne 0) { throw "Impossible de lire le commit courant" }
    $versionPattern = 'public const string Version = "' + [regex]::Escape($Version) + '"'
    if ($pending -or $tagCommit -ne $headCommit -or -not (Select-String -Path $info -Pattern $versionPattern -Quiet)) {
        throw "Le tag v$Version existe déjà et ne correspond pas à un envoi reprenable depuis HEAD"
    }
    Send-Release
    return
}

$original = [IO.File]::ReadAllBytes($info)
$src = [IO.File]::ReadAllText($info)
$src = $src -replace 'public const string Version = "[^"]+"', "public const string Version = `"$Version`""
try {
    [IO.File]::WriteAllText($info, $src, (New-Object Text.UTF8Encoding $true))
    & "$PSScriptRoot\build.cmd"
    if ($LASTEXITCODE) { throw "La compilation a échoué" }
} catch {
    [IO.File]::WriteAllBytes($info, $original)
    throw
}

git add -- CHANGELOG.md src/Properties/AssemblyInfo.cs
if ($LASTEXITCODE -ne 0) { throw "Échec de la préparation du commit" }
git commit -m "Version $Version"
if ($LASTEXITCODE -ne 0) { throw "Échec du commit" }
git tag "v$Version"
if ($LASTEXITCODE -ne 0) { throw "Échec de la création du tag" }
Send-Release
