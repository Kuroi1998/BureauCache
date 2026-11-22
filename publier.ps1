# Publie une nouvelle version de BureauCache.
# Usage : .\publier.ps1 -Version 3.3
# 1. Ajoute d'abord une section « ## 3.3 — Titre » en haut de CHANGELOG.md
# 2. Le script met la version dans src\Properties\AssemblyInfo.cs, compile, crée le commit et le tag, puis pousse.
#    GitHub Actions compile ensuite le programme et publie la version (les installations la proposeront automatiquement).
param([Parameter(Mandatory = $true)][string]$Version)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if ($Version -notmatch '^\d+\.\d+$') { throw "Format attendu : X.Y (ex. 3.3)" }
if (-not (Select-String -Path CHANGELOG.md -Pattern "^## $([regex]::Escape($Version))\b" -Quiet)) {
    throw "Ajoute d'abord une section '## $Version' dans CHANGELOG.md"
}
if (git tag -l "v$Version") { throw "Le tag v$Version existe déjà" }
if ((git branch --show-current) -ne 'main') { throw "Publie depuis la branche main" }

$info = Join-Path $PSScriptRoot 'src\Properties\AssemblyInfo.cs'
$src = [IO.File]::ReadAllText($info)
$src = $src -replace 'public const string Version = "[^"]+"', "public const string Version = `"$Version`""
[IO.File]::WriteAllText($info, $src, (New-Object Text.UTF8Encoding $true))

& "$PSScriptRoot\build.cmd"
if ($LASTEXITCODE) { throw "La compilation a échoué" }

git add -A
git commit -m "Version $Version"
git tag "v$Version"
git push origin HEAD "v$Version"
"Version $Version envoyée : GitHub Actions va la compiler et la publier."
