# Publie une nouvelle version de BureauCache.
# Usage : .\publier.ps1 -Version 3.2
param([Parameter(Mandatory = $true)][string]$Version)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if ($Version -notmatch '^\d+\.\d+$') { throw "Format attendu : X.Y (ex. 3.2)" }
if (-not (Select-String -Path CHANGELOG.md -Pattern "^## $([regex]::Escape($Version))\b" -Quiet)) {
    throw "Ajoute d'abord une section '## $Version' dans CHANGELOG.md"
}
if (git tag -l "v$Version") { throw "Le tag v$Version existe déjà" }

$src = [IO.File]::ReadAllText("$PSScriptRoot\BureauCache.cs")
$src = $src -replace 'public const string Version = "[^"]+"', "public const string Version = `"$Version`""
$src = $src -replace 'AssemblyVersion\("[^"]+"\)', "AssemblyVersion(`"$Version.0.0`")"
$src = $src -replace 'AssemblyFileVersion\("[^"]+"\)', "AssemblyFileVersion(`"$Version.0.0`")"
$src = $src -replace 'AssemblyInformationalVersion\("[^"]+"\)', "AssemblyInformationalVersion(`"$Version`")"
[IO.File]::WriteAllText("$PSScriptRoot\BureauCache.cs", $src, (New-Object Text.UTF8Encoding $true))

& "$PSScriptRoot\build.cmd"
if ($LASTEXITCODE) { throw "La compilation a échoué" }

git add -A
git commit -m "Version $Version"
git tag "v$Version"
git push origin HEAD "v$Version"
"Version $Version envoyée : GitHub Actions va la compiler et la publier."
