# Genera el paquete de una versión de FinderCast listo para subir a GitHub Releases: publica la
# app autocontenida (no necesita .NET ni el Windows App SDK instalados) y la comprime en
# artifacts/FinderCast-<versión>-portable.zip. La versión sale de Directory.Build.props.
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

Write-Host '-> Comprimiendo'
if (Test-Path $zip) { Remove-Item -Force $zip }
# Dentro del .zip, todo en una carpeta FinderCast: al descomprimir no se desparraman los archivos.
$carpeta = Join-Path $artifacts 'FinderCast'
if (Test-Path $carpeta) { Remove-Item -Recurse -Force $carpeta }
Copy-Item $destino $carpeta -Recurse
Compress-Archive -Path $carpeta -DestinationPath $zip
Remove-Item -Recurse -Force $carpeta

$mb = [Math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Listo: $zip ($mb MB)" -ForegroundColor Green
