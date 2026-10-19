# Genera el paquete de una versión de FinderCast listo para subir a GitHub Releases: publica la
# app en un único FinderCast.exe autocontenido y comprimido (no necesita .NET ni el Windows App
# SDK instalados) y lo guarda, con la licencia, en artifacts/FinderCast-<versión>-portable.zip.
# La versión sale de Directory.Build.props.
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
