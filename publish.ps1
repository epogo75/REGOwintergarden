<#
.SYNOPSIS
  Baut REGOwintergarden als eine einzelne .exe nach dist\.

.DESCRIPTION
  EINE Datei, aber RAHMENABHAENGIG: die .NET-Laufzeit steckt seit dem
  03.09.2026 nicht mehr mit drin, sondern wird installiert vorausgesetzt.

  Grund ist Windows 11 "Smart App Control". Es blockt unsignierte Programme
  ohne Ruf in der Microsoft-Cloud, und jeder neue Bau hat einen neuen Hash --
  dieselbe Datei laeuft heute und wird morgen abgewiesen. Bei REGOtvtest und
  REGOwintergarden ist genau das passiert.

  NICHT geholfen haetten: selbst signieren (SAC ignoriert lokal als
  vertrauenswuerdig eingetragene Zertifikate), ein Defender-Ausschluss
  (eigener Mechanismus) und das Entfernen der Herkunftsmarkierung (die
  Dateien trugen gar keine).

  Der Gewinn ist die Groesse: aus rund 70 MB werden unter 1 MB. Das
  Installationsprogramm liegt auf dem NAS unter dev\_.NET10.

  MIT -Eigenstaendig entsteht wieder die alte Fassung mit eingebauter
  Laufzeit -- fuer den Kundenbesuch, wo auf dem Rechner nichts installiert
  werden darf. Der Grund dafuer ist nicht weg, nur nicht mehr die Vorgabe.
.PARAMETER OutputDirectory
  Zielverzeichnis. Vorgabe: dist\ neben diesem Skript.
#>
[CmdletBinding()]
param(
  # EIGENSTAENDIG BAUEN -- mit eingebauter .NET-Laufzeit, rund 70 MB.
  #
  # Seit 03.09.2026 NICHT mehr die Vorgabe: Windows 11 blockt mit Smart App
  # Control unsignierte Programme ohne Ruf in der Microsoft-Cloud, und jeder
  # neue Bau hat einen neuen Hash. Der Betreiber hat entschieden, .NET 10 zu
  # installieren; das Werkzeug schrumpft dabei von rund 70 MB auf unter 1 MB.
  #
  # DER SCHALTER BLEIBT, weil der Grund fuer die alte Fassung nicht weg ist:
  # beim Kunden steht oft ein Rechner, auf dem nichts installiert werden darf.
  # Dann diesen Schalter setzen -- und wissen, dass Windows die Datei
  # abweisen kann.
  [switch]$Eigenstaendig,

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

# RAHMENABHAENGIG UND SEIT DEM 04.09.2026 NICHT MEHR GEBUENDELT.
#
# Hier entstand bis dahin eine einzelne Datei (PublishSingleFile zusammen mit
# --self-contained false). Das ist zurueckgenommen, und zwar aus einem Grund,
# der nichts mit dem Programm zu tun hat:
#
# Eine gebuendelte .NET-Einzeldatei ENTPACKT SICH BEIM START in ein
# Temp-Verzeichnis und fuehrt sich von dort aus. Fuer die
# Verhaltenserkennung von Defender ist das nicht von einem Packer zu
# unterscheiden. Am 03.09.2026 hat sie REGOsound dreimal hintereinander
# geloescht -- Behavior:Win32/DefenseEvasion.A!ml, die Endung !ml heisst
# Mustererkennung und damit Fehlalarm.
#
# GETROFFEN HAT ES ZUERST REGOsound, DER AUSLOESER STECKT ABER IN JEDEM
# GEBUENDELTEN WERKZEUG GLEICHERMASSEN. Deshalb vorbeugend abgeschaltet,
# statt zu warten, bis es mitten in der Arbeit zuschlaegt -- und das tut es
# dann beim Kunden, nicht hier.
#
# Der Preis sind ein paar kleine Dateien neben der EXE. Dafuer kommt der
# Rueckfallweg zurueck: liegt die .dll daneben, laesst sich das Programm
# ueber das von Microsoft signierte dotnet.exe starten, falls Windows die
# EXE einmal abweist.
#
# EIGENSTAENDIG WIRD WEITER GEBUENDELT: dort ist die eine Datei der ganze
# Zweck, und wer sie waehlt, nimmt den Fehlalarm bewusst in Kauf.
#
# Beide Schalter haengen am selben Hebel, meinen aber Verschiedenes:
# --self-contained bestimmt, ob die Laufzeit mitkommt, PublishSingleFile, ob
# alles in eine Datei wandert. Nur fuer den Kundenbesuch wird beides bejaht.
$selbst   = if ($Eigenstaendig) { 'true' } else { 'false' }
$buendeln = $selbst

$argumente = @(
  'publish', (Join-Path $root 'src\REGOwintergarden\REGOwintergarden.csproj'),
  '-c', 'Release', '-r', 'win-x64', "--self-contained=$selbst",
  "-p:PublishSingleFile=$buendeln",
  '-p:DebugType=none',
  '-p:AllowedReferenceRelatedFileExtensions=none'
)

# Diese beiden gelten NUR fuer die eigenstaendige Fassung. Bei einer
# rahmenabhaengigen bringt das Buendeln keine nativen Bibliotheken mit (die
# kommen aus der installierten Laufzeit), und das Komprimieren wird vom SDK
# abgelehnt.
if ($Eigenstaendig) {
  $argumente += '-p:IncludeNativeLibrariesForSelfExtract=true'
  $argumente += '-p:EnableCompressionInSingleFile=true'
}

$argumente += @('-o', $OutputDirectory, '-v', 'q', '--nologo')

& dotnet @argumente

if ($LASTEXITCODE -ne 0) { throw "Veroeffentlichen fehlgeschlagen." }

# Die .pdb ist nur fuer die Fehlersuche und hat im Zielverzeichnis nichts
# verloren - sie waere die zweite Datei, die niemand braucht.
Remove-Item (Join-Path $OutputDirectory 'REGOwintergarden.pdb') -Force -ErrorAction SilentlyContinue


# EINE NOTIZ IN DEN ORDNER, weil er auf das NAS gespiegelt wird.
$liesmich = @'
# REGOwintergarden

Steuert einen Wintergarten ueber KNX: Beschattung nach Sonnenstand.

## Starten

**REGOwintergarden.exe** anklicken. Die kleinen Dateien daneben gehoeren dazu
und muessen mitkopiert werden.

**Verlangt installiertes .NET 10.** Fehlt es, sagt Windows das beim Start.
Das Installationsprogramm liegt auf dem NAS unter
`dev\_.NET10\windowsdesktop-runtime-10-win-x64.exe` -- von Microsoft
signiert, einmal installieren, kein Neustart noetig.

## Warum die Datei so klein ist

Bis zum 03.09.2026 war sie rund 70 MB gross: die .NET-Laufzeit steckte mit
drin, damit sie ohne Installation laeuft. Dann wies Windows 11 eine solche
Datei ab -- dieselbe, die vorher lief.

Ursache ist **Smart App Control**: es blockt unsignierte Programme ohne Ruf in
der Microsoft-Cloud, und jeder neue Bau hat einen neuen Hash und ist damit
unbekannt. Seitdem wird die Laufzeit vorausgesetzt statt mitgeliefert.

## Warum mehrere Dateien und nicht eine

Bis zum 04.09.2026 war es eine einzige Datei. Eine solche entpackt sich beim
Start selbst in ein Temp-Verzeichnis -- und Defender hielt das bei REGOsound
fuer einen Packer und loeschte das Programm. Ohne Buendelung entfaellt das
Entpacken.

Die Dateien neben der EXE gehoeren dazu. Einzeln kopiert laeuft nichts.

Nicht geholfen haetten: selbst signieren (Smart App Control ignoriert lokal
vertrauenswuerdige Zertifikate), ein Defender-Ausschluss (eigener Mechanismus)
oder das Entfernen der Herkunftsmarkierung.

## Wenn Windows die Datei trotzdem abweist

Sie bleibt eine unsignierte EXE. Beim Kopieren **vom NAS** bekommt sie
ausserdem eine Herkunftsmarkierung, und Windows prueft dann schaerfer -- ein
Fehlalarm ist moeglich.

Drei Wege:

- Ueber das signierte dotnet.exe starten -- das wird nie blockiert:
  `dotnet REGOwintergarden.dll`
- Die Herkunftsmarkierung entfernen:
  `Unblock-File .\REGOwintergarden.exe`
- Oder eigenstaendig bauen (`publish.ps1 -Eigenstaendig`): dann ist die
  Laufzeit wieder drin, die Datei rund 70 MB gross -- und laeuft auf einem
  Rechner, auf dem nichts installiert werden darf.
'@

$liesmich.Replace('`', [string][char]96) | Set-Content (Join-Path $OutputDirectory 'LIESMICH.md') -Encoding UTF8
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
Write-Host $(if ($Eigenstaendig) { "Einzelne Datei, laeuft ohne installiertes .NET." } else { "REGOwintergarden.exe plus die kleinen Dateien daneben - NICHT gebuendelt. VERLANGT installiertes .NET 10 (NAS: dev\_.NET10)." }) -ForegroundColor Green
