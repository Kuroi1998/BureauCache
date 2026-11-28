# Génère les images de l'assistant d'installation (BMP 24 bits) à partir de BureauCache.ico (T-058).
# Usage : .\installer\generer-images.ps1   -> installer\images\bandeau.bmp (164x314) et petit.bmp (55x58)
# Relancer ce script si l'icône change. Aucun secret, aucune dépendance hors Windows (System.Drawing).
Add-Type -AssemblyName System.Drawing
$dossier = Join-Path $PSScriptRoot 'images'
New-Item -ItemType Directory -Force -Path $dossier | Out-Null
$icone = Join-Path (Split-Path $PSScriptRoot -Parent) 'BureauCache.ico'
# System.Drawing.Icon décode mal les entrées PNG d'un .ico (les tailles 256 et 128 apparaissent en bruit coloré).
# On lit donc directement l'entrée PNG la plus grande, puis on la décode comme une image.
function Get-LogoPng([string]$chemin) {
    $octets = [IO.File]::ReadAllBytes($chemin)
    $n = [BitConverter]::ToUInt16($octets, 4)
    $meilleure = $null; $taille = -1
    for ($i = 0; $i -lt $n; $i++) {
        $o = 6 + 16 * $i
        $w = if ($octets[$o] -eq 0) { 256 } else { [int]$octets[$o] }
        if ($w -gt $taille) {
            $taille = $w
            $meilleure = @{ Off = [BitConverter]::ToInt32($octets, $o + 12); Len = [BitConverter]::ToInt32($octets, $o + 8) }
        }
    }
    $flux = New-Object IO.MemoryStream (, $octets[$meilleure.Off..($meilleure.Off + $meilleure.Len - 1)])
    return [System.Drawing.Bitmap]::FromStream($flux)
}
$logoBmp = Get-LogoPng $icone

function Get-Degrade([System.Drawing.Graphics]$g, [int]$w, [int]$h) {
    $haut = [System.Drawing.Color]::FromArgb(255, 22, 34, 52)
    $bas = [System.Drawing.Color]::FromArgb(255, 45, 90, 138)
    $pinceau = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle(0, 0, $w, $h)), $haut, $bas, 90)
    $g.FillRectangle($pinceau, 0, 0, $w, $h)
    $pinceau.Dispose()
}

function New-Bandeau([string]$chemin) {
    $bmp = New-Object System.Drawing.Bitmap(164, 314, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'
    Get-Degrade $g 164 314
    $g.DrawImage($logoBmp, 22, 60, 120, 120)
    $titre = New-Object System.Drawing.Font('Segoe UI Semibold', 15, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Point)
    $blanc = [System.Drawing.Brushes]::White
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = 'Center'
    $g.DrawString('BureauCache', $titre, $blanc, (New-Object System.Drawing.RectangleF(0, 196, 164, 40)), $format)
    $sous = New-Object System.Drawing.Font('Segoe UI', 8.5, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Point)
    $couleur = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 170, 200, 230))
    $g.DrawString('Ton bureau, rangé sur le côté', $sous, $couleur, (New-Object System.Drawing.RectangleF(8, 230, 148, 40)), $format)
    $bmp.Save($chemin, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $g.Dispose(); $bmp.Dispose()
}

function New-Petit([string]$chemin) {
    $bmp = New-Object System.Drawing.Bitmap(55, 58, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'
    $g.Clear([System.Drawing.Color]::White)
    $g.DrawImage($logoBmp, 5, 5, 46, 46)
    $bmp.Save($chemin, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $g.Dispose(); $bmp.Dispose()
}

New-Bandeau (Join-Path $dossier 'bandeau.bmp')
New-Petit (Join-Path $dossier 'petit.bmp')
$logoBmp.Dispose()
Write-Output "Images générées dans $dossier"
