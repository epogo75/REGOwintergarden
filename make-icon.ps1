<#
.SYNOPSIS
  Schreibt src\REGOwintergarden\app.ico neu.

.DESCRIPTION
  Das Symbol der EXE wird nicht von Hand gemalt, sondern in
  Ui\AppIcons.cs gezeichnet. Der Build braucht es allerdings als Datei -
  und zwar bevor irgendetwas laufen kann, das es erzeugen koennte. Deshalb
  liegt app.ico eingecheckt im Repository, und dieses Skript erneuert es,
  wenn sich die Zeichnung aendert.

  Reihenfolge: bauen (mit dem alten Symbol), neues schreiben, noch einmal
  bauen. Der erste Build ist noetig, weil der Zeichencode im Programm selbst
  steckt.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $dotnetDir 'dotnet.exe')) {
  $env:Path = "$dotnetDir;$env:Path"
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw "dotnet nicht gefunden. .NET SDK 8 installieren, siehe README."
}

$target = Join-Path $root 'src\REGOwintergarden\app.ico'
$tests = Join-Path $root 'tests\REGOwintergarden.Tests'

Write-Host "Baue den Zeichencode..." -ForegroundColor Cyan
& dotnet build $tests -c Release -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen." }

& dotnet run --project $tests -c Release --no-build -v q -- --symbol $target
if ($LASTEXITCODE -ne 0) { throw "Symbol liess sich nicht schreiben." }

Write-Host "Baue mit dem neuen Symbol..." -ForegroundColor Cyan
& dotnet build (Join-Path $root 'src\REGOwintergarden\REGOwintergarden.csproj') -c Release -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "Build mit neuem Symbol fehlgeschlagen." }

Write-Host "$target geschrieben." -ForegroundColor Green
