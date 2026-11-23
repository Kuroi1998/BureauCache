# Publie une nouvelle version de BureauCache.
# Usage : .\publier.ps1 -Version 3.3
# 1. Ajoute d'abord une section « ## 3.3 — Titre » en haut de CHANGELOG.md
# 2. Le script met la version dans src\Properties\AssemblyInfo.cs, compile, crée le commit et le tag, puis pousse.
#    GitHub Actions compile ensuite le programme et publie la version (les installations la proposeront automatiquement).
param([Parameter(Mandatory = $true)][string]$Version)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if ($Version -notmatch '^[0-9]+\.[0-9]+$') { throw "Format attendu : X.Y (ex. 3.3)" }
if (-not (Select-String -Path CHANGELOG.md -Pattern "^## $([regex]::Escape($Version))(?=\s|$)" -Quiet)) {
    throw "Ajoute d'abord une section '## $Version' dans CHANGELOG.md"
}
if (git tag -l "v$Version") { throw "Le tag v$Version existe déjà" }
if ((git branch --show-current) -ne 'main') { throw "Publie depuis la branche main" }

$pending = git status --porcelain
if ($LASTEXITCODE -ne 0) { throw "Impossible de lire l'état du dépôt" }
if (@($pending | Where-Object { $_ -notin @(' M CHANGELOG.md', 'M  CHANGELOG.md', 'MM CHANGELOG.md') }).Count -gt 0) {
    throw "Enregistre les autres changements avant de publier"
}

$info = Join-Path $PSScriptRoot 'src\Properties\AssemblyInfo.cs'
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
git push --atomic origin HEAD "v$Version"
if ($LASTEXITCODE -ne 0) { throw "Échec de l'envoi du commit et du tag" }
"Version $Version envoyée : GitHub Actions va la compiler et la publier."
