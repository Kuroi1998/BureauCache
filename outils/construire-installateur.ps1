# Construit l'installateur BureauCache-Setup-X.Y.Z.exe avec Inno Setup 6 (T-056).
# Usage : .\outils\construire-installateur.ps1 -Version 3.11.1 -Exe <chemin de BureauCache.exe> [-Sortie <fichier>]
# L'exe doit déjà être signé (Authenticode) s'il doit l'être : l'installateur le contient tel quel.
# L'installateur lui-même est signé ici si une signature est configurée (voir signer-code.ps1).
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Sortie
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\signer-code.ps1"

if ($Version -notmatch '^[0-9]+\.[0-9]+(?:\.[0-9]+)?$') { throw "Format attendu : X.Y ou X.Y.Z" }
if (-not (Test-Path $Exe)) { throw "Exécutable introuvable : $Exe" }
$depot = Split-Path $PSScriptRoot -Parent
$script = Join-Path $depot 'installer\BureauCache.iss'
if (-not (Test-Path $script)) { throw "Script introuvable : $script" }

# ISCC : variable BC_ISCC, sinon les emplacements standards d'Inno Setup 6
$iscc = $env:BC_ISCC
if (-not $iscc) {
    $iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
              "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc -or -not (Test-Path $iscc)) {
    throw "ISCC.exe (Inno Setup 6) introuvable. Installe-le (winget install JRSoftware.InnoSetup) ou définis BC_ISCC."
}

if (-not $Sortie) { $Sortie = Join-Path $depot "dist\BureauCache-Setup-$Version.exe" }
New-Item -ItemType Directory -Force -Path (Split-Path $Sortie -Parent) | Out-Null
$tmp = Join-Path $env:TEMP ("bc-installateur-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $tmp | Out-Null
try {
    $arguments = @('/Q', "/DAppVersion=$Version", "/DSourceExe=$((Resolve-Path $Exe).Path)", "/O$((Split-Path $Sortie -Parent))", "/F$((Split-Path $Sortie -Leaf) -replace '\.exe$', '')")

    $commande = New-CommandeSignatureIscc -Dossier $tmp
    if ($commande) {
        # La commande ne doit pas contenir d'espace : elle est passée à ISCC sans guillemets
        if ($commande -match '\s') { throw "Le dossier temporaire contient un espace ($commande) : définis TEMP sans espace." }
        # Inno Setup remplace `$f par le chemin du fichier à signer : sans ce marqueur, la commande ne reçoit rien (T-059)
        $arguments += '/DSIGN', "/Sbc=$commande `$f"
    }
    & $iscc @arguments $script
    if ($LASTEXITCODE -ne 0) { throw "La compilation de l'installateur a échoué (ISCC, code $LASTEXITCODE)." }
    if (-not (Test-Path $Sortie)) { throw "ISCC n'a pas produit $Sortie." }

    # Signature de l'installateur, si ISCC ne l'a pas déjà fait par /Sbc
    if (Get-SignMode) {
        & (Get-SignTool) verify /pa /all $Sortie | Out-Null
        if ($LASTEXITCODE -ne 0) { Invoke-SignCode -Fichier $Sortie | Out-Null }
    } else {
        Write-Warning "Installateur NON SIGNÉ : aucun certificat configuré."
    }
    Write-Host "Installateur : $Sortie" -ForegroundColor Cyan
    return $Sortie
} finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
