# Signature Authenticode de BureauCache.exe et de l'installateur (T-056).
# Aucun secret dans le dépôt : les réglages viennent de variables d'environnement, jamais de fichiers versionnés.
# Voir installer\README.md pour le choix du certificat et les étapes.
#
# Variables (BC_SIGN_MODE choisit le mode) :
#   BC_SIGN_MODE = azure  : Azure Trusted Signing (recommandé pour ce projet, la signature reste locale, voir D-004)
#                           BC_AZURE_DLIB      : chemin d'Azure.CodeSigning.Dlib.dll (paquet NuGet Trusted Signing)
#                           BC_AZURE_METADATA  : chemin du fichier metadata.json (compte, profil, point de terminaison)
#                           L'authentification se fait par « az login » sur ce PC.
#   BC_SIGN_MODE = store  : certificat installé dans le magasin Windows — BC_CERT_SHA1 = empreinte du certificat
#   BC_SIGN_MODE = pfx    : fichier .pfx — BC_PFX_PATH = chemin ; BC_PFX_PASSWORD = mot de passe (variable d'environnement
#                           seulement, jamais écrite). Attention : signtool reçoit le mot de passe sur sa ligne de commande.
#   (absent)              : aucune signature. Le build reste utilisable mais le signale clairement.
# BC_TIMESTAMP_URL (facultatif) : serveur d'horodatage RFC 3161 ; par défaut selon le mode.

$ErrorActionPreference = 'Stop'

function Get-SignTool {
    if ($env:BC_SIGNTOOL -and (Test-Path $env:BC_SIGNTOOL)) { return $env:BC_SIGNTOOL }
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $trouve = Get-ChildItem $kits -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $trouve) { throw "signtool.exe introuvable. Installe le Kit de développement Windows (SDK), ou définis BC_SIGNTOOL." }
    return $trouve
}

# Renvoie le mode demandé, ou $null si aucune signature n'est configurée. Un mode inconnu est une erreur.
function Get-SignMode {
    $mode = $env:BC_SIGN_MODE
    if (-not $mode) { return $null }
    if ($mode -notin @('azure', 'store', 'pfx')) { throw "BC_SIGN_MODE inconnu : « $mode » (valeurs : azure, store, pfx)." }
    return $mode
}

# Arguments signtool communs à tous les modes : SHA-256 partout, horodatage RFC 3161 (/tr) avec le SHA-256 (/td)
function Get-SignArguments {
    $mode = Get-SignMode
    switch ($mode) {
        'azure' {
            $dlib = $env:BC_AZURE_DLIB; $meta = $env:BC_AZURE_METADATA
            if (-not $dlib -or -not (Test-Path $dlib)) { throw "BC_AZURE_DLIB absent ou introuvable." }
            if (-not $meta -or -not (Test-Path $meta)) { throw "BC_AZURE_METADATA absent ou introuvable." }
            $url = if ($env:BC_TIMESTAMP_URL) { $env:BC_TIMESTAMP_URL } else { 'http://timestamp.acs.microsoft.com' }
            return @('sign', '/fd', 'SHA256', '/tr', $url, '/td', 'SHA256', '/dlib', $dlib, '/dmdf', $meta)
        }
        'store' {
            $empreinte = $env:BC_CERT_SHA1
            if (-not $empreinte) { throw "BC_CERT_SHA1 absent." }
            $url = if ($env:BC_TIMESTAMP_URL) { $env:BC_TIMESTAMP_URL } else { 'http://timestamp.digicert.com' }
            return @('sign', '/fd', 'SHA256', '/sha1', $empreinte, '/tr', $url, '/td', 'SHA256')
        }
        'pfx' {
            $pfx = $env:BC_PFX_PATH
            if (-not $pfx -or -not (Test-Path $pfx)) { throw "BC_PFX_PATH absent ou introuvable." }
            if (-not $env:BC_PFX_PASSWORD) { throw "BC_PFX_PASSWORD absent." }
            $url = if ($env:BC_TIMESTAMP_URL) { $env:BC_TIMESTAMP_URL } else { 'http://timestamp.digicert.com' }
            return @('sign', '/fd', 'SHA256', '/f', $pfx, '/p', $env:BC_PFX_PASSWORD, '/tr', $url, '/td', 'SHA256')
        }
        default { return $null }
    }
}

# Signe un fichier et vérifie la signature. Renvoie $false si aucune signature n'est configurée (build non signé).
function Invoke-SignCode {
    param([Parameter(Mandatory = $true)][string]$Fichier)
    $arguments = Get-SignArguments
    if (-not $arguments) {
        Write-Warning "NON SIGNÉ : $(Split-Path $Fichier -Leaf). Aucun certificat configuré (BC_SIGN_MODE absent). Ce build ne passera pas SmartScreen ni Smart App Control."
        return $false
    }
    $signtool = Get-SignTool
    & $signtool @arguments $Fichier
    if ($LASTEXITCODE -ne 0) { throw "La signature de $(Split-Path $Fichier -Leaf) a échoué (signtool, code $LASTEXITCODE)." }
    & $signtool verify /pa /all $Fichier | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "La signature de $(Split-Path $Fichier -Leaf) ne se vérifie pas (signtool verify /pa, code $LASTEXITCODE)." }
    Write-Host "Signé et vérifié : $(Split-Path $Fichier -Leaf)" -ForegroundColor Green
    return $true
}

# Fichier de commande pour ISCC (/Sbc) : il reçoit le chemin à signer en premier argument.
# Les arguments de signtool sont écrits sans secret : le mot de passe pfx n'y figure pas (il est lu dans l'environnement).
function New-CommandeSignatureIscc {
    param([Parameter(Mandatory = $true)][string]$Dossier)
    $arguments = Get-SignArguments
    if (-not $arguments) { return $null }
    $signtool = Get-SignTool
    $ligne = ($arguments | ForEach-Object {
        if ($env:BC_PFX_PASSWORD -and $_ -ceq $env:BC_PFX_PASSWORD) { '"%BC_PFX_PASSWORD%"' } else { '"' + $_ + '"' }
    }) -join ' '
    $fichier = Join-Path $Dossier 'signer-bc.cmd'
    [IO.File]::WriteAllText($fichier, "@echo off`r`n`"$signtool`" $ligne `"%~1`"`r`nexit /b %ERRORLEVEL%`r`n", (New-Object Text.ASCIIEncoding))
    return $fichier
}
