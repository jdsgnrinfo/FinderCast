# Genera los paquetes de una versión de FinderCast listos para subir a GitHub Releases: publica la
# app en un único FinderCast.exe autocontenido y comprimido (no necesita .NET ni el Windows App
# SDK instalados) y lo guarda, con la licencia, en artifacts/FinderCast-<versión>-portable.zip.
# Con Inno Setup 6 instalado genera además el instalador, artifacts/FinderCast-<versión>-setup.exe
# (installer/FinderCast.iss). La versión sale de Directory.Build.props.
#
# Uso (desde la raíz del repositorio):
#   powershell -ExecutionPolicy Bypass -File scripts/publicar.ps1

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

[xml]$props = Get-Content (Join-Path $raiz 'Directory.Build.props')
$version = $props.Project.PropertyGroup.Version
$nombre = "FinderCast-$version-portable"
$artifacts = Join-Path $raiz 'artifacts'
$destino = Join-Path $artifacts $nombre
$zip = Join-Path $artifacts "$nombre.zip"

Write-Host "== FinderCast $version ==" -ForegroundColor Cyan

Write-Host '-> Publicando'
if (Test-Path $destino) { Remove-Item -Recurse -Force $destino }
dotnet publish src/FinderCast -p:PublishProfile=Portable
if ($LASTEXITCODE -ne 0) { throw 'La publicacion ha fallado.' }

# La licencia viaja junto al .exe, como pide la licencia MIT a las copias.
Copy-Item (Join-Path $raiz 'LICENSE') (Join-Path $destino 'LICENSE.txt')
# Y el aviso de los datos de terceros que van dentro (emojibase-data, MIT).
Copy-Item (Join-Path $raiz 'THIRD-PARTY-NOTICES.md') (Join-Path $destino 'THIRD-PARTY-NOTICES.txt')

Write-Host '-> Comprimiendo'
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $destino '*') -DestinationPath $zip

$mb = [Math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Listo: $zip ($mb MB)" -ForegroundColor Green

# Inno Setup se instala por usuario (winget --scope user) o para todos.
$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    Write-Host 'Sin Inno Setup 6: no se genera el instalador (winget install JRSoftware.InnoSetup).' -ForegroundColor Yellow
    return
}

Write-Host '-> Instalador'
& $iscc /Q "/DVersion=$version" "/DSource=$destino" (Join-Path $raiz 'installer\FinderCast.iss')
if ($LASTEXITCODE -ne 0) { throw 'El instalador ha fallado.' }
$setup = Join-Path $artifacts "FinderCast-$version-setup.exe"
$mb = [Math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host "Listo: $setup ($mb MB)" -ForegroundColor Green
