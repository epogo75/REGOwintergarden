<#
.SYNOPSIS
  Fuehrt die Pruefungen des REGOwintergardens aus.

.DESCRIPTION
  Kein Testframework: die Pruefungen laufen als gewoehnliches Programm und
  melden ueber den Rueckgabewert, ob alles bestanden hat - gleiche Bauart wie
  die Tests in REGOsound und AB_Gira. Das haelt das Projekt frei von
  NuGet-Paketen.

  Ein Teil der Pruefungen spricht wirklich UDP ueber 127.0.0.1 gegen ein
  nachgebildetes Gateway. Sie brauchen also keine Freigabe in der Firewall,
  aber ein Virenscanner, der Schleifenverkehr abfaengt, kann sie ausbremsen.
#>
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $dotnetDir 'dotnet.exe')) {
  $env:Path = "$dotnetDir;$env:Path"
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw "dotnet nicht gefunden. .NET SDK 8 installieren, siehe README."
}

$project = Join-Path $root 'tests\REGOwintergarden.Tests\REGOwintergarden.Tests.csproj'

& dotnet build $project -c $Configuration -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "Testprojekt liess sich nicht bauen." }

& dotnet run --project $project -c $Configuration --no-build -v q
exit $LASTEXITCODE
