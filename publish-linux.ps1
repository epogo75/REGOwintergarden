<#
.SYNOPSIS
  Baut den Linux-Dienst fuer Raspberry Pi und andere Rechner.

.DESCRIPTION
  Erzeugt je Architektur eine einzelne, eigenstaendige Datei - ohne
  installiertes .NET lauffaehig. Das ist der Grund fuer die dreissig
  Megabyte: die Laufzeit steckt mit drin, und auf einem frisch aufgesetzten
  Pi soll niemand erst ein SDK einrichten muessen.

    linux-arm64   Raspberry Pi 3, 4, 5 mit 64-Bit-System (der Regelfall)
    linux-arm     Raspberry Pi mit 32-Bit-System, Pi Zero 2
    linux-x64     gewoehnliche Rechner, NAS, virtuelle Maschinen

  Daneben werden .tar.gz-Pakete gelegt - die holt install.sh von der
  Veroeffentlichungsseite.
#>
[CmdletBinding()]
param(
  [string[]]$Architekturen = @('linux-arm64', 'linux-arm', 'linux-x64'),
  [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'dist' }

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $dotnetDir 'dotnet.exe')) { $env:Path = "$dotnetDir;$env:Path" }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw "dotnet nicht gefunden. .NET SDK 8 installieren, siehe README."
}

$projekt = Join-Path $root 'src\REGOwintergarden.Daemon\REGOwintergarden.Daemon.csproj'

foreach ($rid in $Architekturen) {
  $ziel = Join-Path $OutputDirectory $rid
  Write-Host "Baue $rid ..." -ForegroundColor Cyan

  & dotnet publish $projekt -c Release -r $rid --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
    -o $ziel -v q --nologo
  if ($LASTEXITCODE -ne 0) { throw "Veroeffentlichen fuer $rid fehlgeschlagen." }

  $datei = Join-Path $ziel 'regowintergarden'
  $paket = Join-Path $OutputDirectory "regowintergarden-$rid.tar.gz"

  # tar liegt seit Windows 10 bei. Fehlt es, bleibt die nackte Datei liegen -
  # die laesst sich genauso auf den Pi kopieren.
  if (Get-Command tar -ErrorAction SilentlyContinue) {
    & tar -czf $paket -C $ziel 'regowintergarden'
  }

  Write-Host ("  {0}  ({1:N1} MB)" -f $datei, ((Get-Item $datei).Length / 1MB)) -ForegroundColor Green
}

Write-Host ""
Write-Host "Auf den Pi bringen:" -ForegroundColor Green
Write-Host "  scp dist\linux-arm64\regowintergarden pi@wintergarten:/tmp/"
Write-Host "  ssh pi@wintergarten 'sudo install -m755 /tmp/regowintergarden /opt/regowintergarden/'"
Write-Host ""
Write-Host "Oder in einem Zug einrichten:" -ForegroundColor Green
Write-Host "  curl -fsSL https://raw.githubusercontent.com/epogo75/REGOwintergarden/main/linux/install.sh | sudo sh"
