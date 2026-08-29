<#
.SYNOPSIS
  Baut den REGOwintergarden als einzelne, eigenstaendige .exe.

.DESCRIPTION
  Der gewoehnliche Build erzeugt vier Dateien: .exe (nur ein Starter), .dll
  (der eigentliche Code), .deps.json und .runtimeconfig.json. Kopiert jemand
  nur die .exe, passiert beim Doppelklick nichts - ohne Fehlermeldung. Diese
  Falle raeumt das Skript aus dem Weg: eine Datei, die alles enthaelt, samt
  .NET-Laufzeit, also auch auf einem Rechner ohne installiertes .NET.

  Das zaehlt hier mehr als anderswo: der Helfer laeuft beim Kunden, oft auf
  einem Rechner, auf dem sonst nichts installiert werden darf.

  Preis dafuer sind rund 70 MB und ein etwas langsamerer erster Start
  (die Laufzeit wird einmalig entpackt).

.PARAMETER OutputDirectory
  Zielverzeichnis. Vorgabe: dist\ neben diesem Skript.
#>
[CmdletBinding()]
param(
  [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'dist' }

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $dotnetDir 'dotnet.exe')) {
  $env:Path = "$dotnetDir;$env:Path"
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw "dotnet nicht gefunden. .NET SDK 8 installieren, siehe README."
}

& dotnet publish (Join-Path $root 'src\REGOwintergarden\REGOwintergarden.csproj') `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o $OutputDirectory -v q --nologo

if ($LASTEXITCODE -ne 0) { throw "Veroeffentlichen fehlgeschlagen." }

# Die .pdb ist nur fuer die Fehlersuche und hat im Zielverzeichnis nichts
# verloren - sie waere die zweite Datei, die niemand braucht.
Remove-Item (Join-Path $OutputDirectory 'REGOwintergarden.pdb') -Force -ErrorAction SilentlyContinue

$exe = Join-Path $OutputDirectory 'REGOwintergarden.exe'
# Die Fassung mit ausgeben: auf dem Ablageordner liegen regelmaessig mehrere
# .exe nebeneinander, weil sich eine laufende nicht ueberschreiben laesst.
# Dann ist die Nummer hier die einzige Stelle, an der man sie vor dem
# Kopieren sieht.
# Bis zum Pluszeichen: dahinter haengt das SDK die Quelltextkennung an, und
# die vierzig Zeichen helfen beim Kopieren einer Datei niemandem.
$fassung = ((Get-Item $exe).VersionInfo.ProductVersion -split '\+')[0]
Write-Host ""
Write-Host ("{0}  ({1:N1} MB, Fassung {2})" -f $exe, ((Get-Item $exe).Length / 1MB), $fassung) -ForegroundColor Green
Write-Host "Einzelne Datei, laeuft ohne installiertes .NET." -ForegroundColor Green
