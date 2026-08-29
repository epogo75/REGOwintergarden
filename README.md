# REGOwintergarden

Steuert einen **Wintergarten über KNX**: Beschattung nach Sonnenstand,
Lüftung nach Innentemperatur, Wind-, Regen- und Frostschutz, Zeitschaltuhr mit
Bezug auf Sonnenauf- und -untergang. Ein Windows-Programm, eigenständig
lauffähig, wahlweise als Dienst.

## Wozu

Ein Wintergarten ist der Raum, in dem alles zusammenkommt: acht Antriebe,
darunter Markisen und Fenster, eine Wetterstation, die Sonne, die im Juni von
halb sechs bis halb zehn wandert, und drinnen dreißig Grad, wenn niemand
aufpasst. Die Aktoren können jeder für sich fahren — aber keiner weiß, dass die
Sonne inzwischen auf der Westseite steht und dass für elf Uhr Böen angesagt
sind.

Das ist der Sinn dieses Programms: es hält die Regeln an einer Stelle, und es
sagt bei jedem Antrieb dazu, **warum** er gerade dort steht, wo er steht.

## Vier Seiten, und das ist Absicht

Drei für den Endkunden, eine für den Errichter:

| Seite | Beantwortet |
|---|---|
| **Bedienung** | Was ist gerade, und was passiert als Nächstes? |
| **Automatik** | Was tut die Steuerung von selbst, und warum? |
| **Verlauf** | Wie war es die letzten Tage — und wann hat die Steuerung eingegriffen? |
| **Konfiguration** | Anschluss, Antriebe, Grenzen, Schaltzeiten |

Wer den Wintergarten benutzt, will wissen, ob die Markise gleich ausfährt. Wer
ihn eingerichtet hat, will Gruppenadressen eintragen. Das sind zwei
verschiedene Leute mit zwei verschiedenen Fragen, und eine Oberfläche, die
beides mischt, bedient keinen von beiden.

Die Bedienseite passt **ohne Scrollen** auf einen Bildschirm: Statusband,
Wetterzeile, darunter Sonne und Antriebe nebeneinander. Wer sehen will, ob die
Markise draußen ist, soll nicht erst rollen müssen.

Gatewayadresse, Verbinden, Projektimport und Dienst liegen deshalb **nicht** in
der Kopfzeile, sondern unter Konfiguration → Anschluss. Eine Gatewayadresse
trägt man einmal ein; sie danach jeden Tag anzuzeigen sagt niemandem etwas. In
der Fußzeile steht dafür in einem Satz, ob überhaupt gefahren werden kann — und
daneben die zuletzt ausgeführte Aktion.

## Die Bedienseite

Sie ist für den Alltag gemacht und nicht für den Errichter.

**Ganz oben ein Statusband** in einem Satz: „Alles ruhig", „Beschattung läuft",
„Windschutz aktiv" — mit Symbol, in Farbe, und darunter die Erklärung dazu:

> 2 von 8 Antrieben sind in Sicherheit gefahren. Die Sonne steht im Südwesten,
> 34 Grad über dem Horizont; Untergang um 20:41.

Fehlt ein Wert, steht das im selben Satz: „Es fehlt ein aktueller Wert für Wind
— solange fährt die Anlage vorsichtig."

**„Als Nächstes"** zeigt, was von selbst passieren wird, mit Uhrzeit und
Abstand:

> in 4 min · 14:38 — Markise Süd: fährt zum Beschatten aus
> in 12 min · 14:46 — Jalousie West: Windalarm endet, danach entscheidet wieder die Automatik
> um 20:11 — alle Antriebe: Zeitschaltuhr fährt auf 100 %

Das ist der Unterschied zwischen einer Steuerung, die willkürlich wirkt, und
einer, die man versteht: nicht nur den Zustand zeigen, sondern die **Absicht**.

**Fünf Leuchten**: Wind, Regen, draußen, drinnen, Helligkeit. Jede mit
gezeichnetem Sinnbild, jede mit drei Zuständen — ruhig, rot, ausgegraut. Der
dritte ist der wichtige: **ausgegraut heißt „dazu weiß ich nichts"**, und das
ist etwas anderes als Windstille.

**Der Sonnenkompass** zeigt, was zwei Zahlen nicht zeigen. Der Strahl ist die
Sonne, sein Winkel der Azimut; der Abstand zur Mitte ist die Höhe — außen am
Rand steht sie am Horizont, in der Mitte im Zenit. Die farbigen Sektoren sind
die Flächen der Antriebe, und ein Sektor färbt sich, sobald dort beschattet
wird. Damit beantwortet das Bild die Frage, die man wirklich hat: scheint die
Sonne auf die Südseite, und warum ist die Markise dort draußen?

**Eine Kachel je Antrieb** mit Sinnbild, Position, dem Symbol der wirksamen
Regel und dem Grund im Klartext:

> Markise Süd — eingefahren
> Windschutz: Wind 11,4 m/s über der Grenze von 8 m/s

Nicht „Position 0 %". Das beantwortet die Frage nicht, die morgens gestellt
wird.

Dazu drei Knöpfe je Kachel: Auf, Stopp, Ab. Ein Handgriff hält die Automatik
für zwei Stunden zurück — und schreibt das in den Grund, damit niemand das
Programm für kaputt hält.

## Die Seite Automatik

Eine Karte je Regel: Sinnbild, Schalter, zwei bis vier Sätze Erklärung — und
darunter in einer Zeile, was die Regel **gerade** tut. „Wirkt gerade auf 2
Antriebe" schafft Vertrauen, „ist eingeschaltet" nicht.

Erklärt wird dort, wo der Schalter sitzt, und nicht in einer Anleitung, die
niemand liest. Denn die Frage kommt beim Schalter: „Warum ist die Markise im
Winter oben, obwohl die Sonne scheint?"

## Der Verlauf

Innen- und Außentemperatur, Wind und Helligkeit als Kurven über heute,
24 Stunden, 7 oder 30 Tage — und **darüber die Eingriffe der Steuerung** als
senkrechte Striche, je Regel in ihrer Farbe, mit Zeit und Grund im
Kurzhinweis.

Das ist der Punkt an der Überblendung: eine Kurve allein beantwortet die Frage
nicht. „Am Dienstag war es doch heiß — warum war die Markise oben?" lässt sich
nur beantworten, wenn man sieht, dass um 11:20 ein Windalarm kam.

Jede Kurve hat ihre **eigene Skala**, der Bereich steht in der Beschriftung.
Grad, Meter je Sekunde und Lux in eine Achse zu zwingen macht aus der
Helligkeit einen Strich am oberen Rand und aus der Temperatur eine Linie am
unteren.

Aufgezeichnet wird je Minute in eine Textdatei je Monat, dazu die Eingriffe in
einer zweiten. Ein Langzeittrend ist erst einer, wenn er einen Neustart
überlebt — und die Dateien lassen sich mit jedem Tabellenprogramm öffnen. Eine
Lücke bleibt eine Lücke: sie zu überbrücken hieße, einen Messwert zu erfinden,
den es nie gab.

## Die Regeln

Es gibt eine **Rangfolge**, und sie ist die Sicherheitsvorschrift des
Programms:

| Rang | Regel | Was sie tut |
|---|---|---|
| 1 | **Wind** | Markise ein, Fenster zu. Schlägt alles. |
| 2 | **Regen** | dasselbe, für alles mit Regenschutz |
| 3 | **Frost** | dasselbe, unter der Frostgrenze |
| 4 | **Hand** | Automatik pausiert nach einem Handgriff |
| 5 | **Beschattung** | nach Sonnenstand und Helligkeit |
| 6 | **Lüftung** | Fenster nach Innentemperatur |
| 7 | **Zeitschaltuhr** | feste Zeiten und Astro-Zeiten |

Dazu drei Regeln, die mitdenken statt nur zu reagieren:

**Wärmegewinn.** An kalten Tagen wird nicht beschattet, solange es drinnen
kühl ist. Ein Wintergarten ist im Winter eine Heizung — wer im Januar bei
Sonnenschein die Markise ausfährt, weil es hell genug ist, wirft die einzige
kostenlose Wärme des Tages weg und heizt abends nach. Beides muss stimmen:
draußen kalt **und** drinnen kühl. Ein Wintergarten mit 26 Grad braucht auch im
Februar Schatten.

**Hitzevorsorge.** Sagt die Vorhersage einen heißen Tag an, wird früher
beschattet — bei weniger Helligkeit. Wer erst beschattet, wenn es drinnen warm
ist, kommt zu spät: die Wärme steckt dann in Boden und Möbeln und geht den
ganzen Abend nicht mehr heraus.

**Nachtauskühlung.** Nach einem heißen Tag werden die Fenster nachts geöffnet,
solange es draußen kühler ist — bis die Zieltemperatur erreicht ist. Das ist
die wirksamste Kühlung, die ein Wintergarten hat, und sie kostet nichts.
Tagsüber bringt Lüften wenig; draußen ist es dann wärmer.

Eine ausgefahrene Markise im Sturm ist ein Schaden, eine unbeschattete Scheibe
ist keiner. Deshalb steht Wind ganz oben — auch über dem Handgriff.

### Wind und Regen — die Sicherheitskette

**Dieses Programm ist der Chef für Wind und Regen.** Es hört die Wetterstation
ab, bildet daraus ein Urteil und meldet es **zyklisch** an die Aktoren — auf
eigenen Adressen, nicht auf denen der Station.

```
Wetterstation  ──zyklisch──▶  REGOwintergarden  ──zyklisch──▶  Aktoren
   Windalarm (Bit)               bewertet,                  eigene
   Regen (Bit)                   erkennt Ausfall,           zyklische
   Wind (m/s)                    entscheidet                Überwachung
```

Jede Stufe überwacht die vorige, und das ergibt eine lückenlose Kette:

- **Fällt die Wetterstation aus**, bleiben ihre zyklischen Telegramme aus. Das
  merkt dieses Programm — und meldet Alarm, statt weiter Entwarnung zu geben.
- **Fällt dieses Programm aus** (Rechner aus, Netz weg, Dienst gestorben),
  bleibt *seine* Wiederholung aus. Das merken die Aktoren über ihre eigene
  zyklische Überwachung und fahren von selbst in Sicherheit.

Deshalb wird **zyklisch** gesendet und nicht nur bei Änderung: ein Signal, das
nur bei Änderung kommt, ist kein Lebenszeichen. Ein stillstehendes Programm
sähe für die Aktoren aus wie schönes Wetter.

Die Wiederholung gehört deutlich kürzer eingestellt als die Überwachungszeit in
den Aktoren — ein Drittel bis ein Viertel ist die Faustregel: bei 60 Sekunden
Wiederholung also drei bis vier Minuten Überwachung. Sonst löst deren
Überwachung aus, obwohl alles läuft.

Das Signal geht auch hinaus, wenn die **Automatik abgeschaltet** ist. Wer den
Komfort abschaltet, schaltet nicht den Windschutz ab.

**Alarm gilt**, wenn eines davon zutrifft: die Station meldet Alarm; die
Geschwindigkeit liegt über der Anlagengrenze; die Vorhersage kündigt Böen an;
oder von der Station kommt nichts Frisches mehr. Die Anlagengrenze ist getrennt
von den Grenzen der einzelnen Antriebe — die gelten für das, was dieses
Programm selbst fährt, die Anlagengrenze ist die Notbremse für alles.

### Was die Station liefert

**Windalarm und Regen kommen als fertige Bits von der Wetterstation.** Dort
läuft die eigentliche Überwachung: Böenerkennung, Grenze, Nachlauf, beim Regen
der beheizte Sensor. Dieses Programm wertet das Ergebnis aus, statt daneben
einen zweiten Wächter mit anderen Grenzen zu bauen — zwei Wächter, die sich
uneinig sind, sind schlimmer als einer.

Die **Geschwindigkeit in m/s** wird trotzdem gelesen. Sie steht in der Anzeige
(„Windalarm" beruhigt niemanden, „Windalarm, 14 m/s" schon) und dient als
zusätzliche Grenze je Antrieb: eine empfindliche Markise darf früher einfahren
als die eine Grenze, die in der Station eingestellt ist. Das Alarmbit schlägt
diese Grenze immer.

**Ein stiller Windmesser ist keine Windstille.** Kommt weder Bit noch Wert
länger als zehn Minuten, fährt alles mit sicherer Seite ein, und der Grund sagt
es: „kein Windwert von der Wetterstation — zur Sicherheit eingefahren". Wer
stattdessen den letzten bekannten Wert weiterlaufen lässt, baut eine Steuerung,
die nach dem Ausfall der Station im nächsten Sturm nichts tut.

Nach dem Abflauen **läuft der Alarm nach** (20 min): sofort wieder auszufahren
hieße, in die nächste Böe zu fahren.

Die **Vorhersage** warnt vor. Sind für die nächsten Stunden Böen über der
Grenze angesagt, bleibt die Markise gleich drinnen — sie führe sonst zweimal
umsonst und einmal zu spät.

### Beschattung

Beschattet wird, wenn drei Dinge zusammenkommen: die Sonne steht **auf der
Fläche** (Azimut im Öffnungswinkel um die Ausrichtung), sie steht **hoch
genug** und nicht zu hoch, und es ist **hell genug** — und zwar lange genug.

Die Ausrichtung ist **frei in Grad** einstellbar, nicht als Auswahl aus acht
Richtungen: ein Wintergarten steht selten genau nach Süden, und 205 Grad sind
etwas anderes als „Süd".

Das Ausschalten dauert länger als das Einschalten (15 min gegen 3), und das mit
Absicht: eine einzelne Wolke soll die Markise nicht ein- und wieder ausfahren.
Jede Fahrt kostet Mechanik, und nichts stört im Wintergarten mehr als ein
Behang, der alle drei Minuten wandert.

Ist es **drinnen schon warm**, sinkt die Helligkeitsschwelle auf einen Faktor
(0,7 heißt dreißig Prozent früher). Die Verzögerung bleibt — die Wolke bleibt
eine Wolke.

Von drei Helligkeitsfühlern (Ost, Süd, West) bekommt jede Fläche den, dessen
Richtung am nächsten liegt.

### Lüften

Fenster öffnen, wenn es drinnen über der Grenze ist **und draußen wirklich
kühler** — sonst holt das offene Fenster die Wärme herein, statt sie
hinauszulassen. Geschlossen wird erst unter der Hysterese.

### Zeitschaltuhr

Feste Uhrzeit oder **Sonnenaufgang/-untergang mit Versatz**. „Eine halbe Stunde
vor Sonnenuntergang" ist der Fall, um den es geht: eine feste Uhrzeit liegt im
Juni zwei Stunden daneben. Ausgelöst wird einmal je Minute — die Uhr wird
sekündlich befragt, eine Schaltzeit gilt aber eine ganze Minute lang.

## Sonnenstand

Azimut und Elevation kommen **von der Wetterstation**, wenn sie sie frisch
liefert (DPT 14.007), sonst aus der eigenen Rechnung. Auf- und Untergang kommen
**immer** aus der Rechnung: die meldet keine Station, und die Zeitschaltuhr
braucht sie.

Gerechnet wird nach dem Verfahren der NOAA, mit Zeitgleichung und Brechung der
Luft — genau genug für eine Beschattung, zu ungenau für Astronomie. Die
Prüfungen messen es an sich selbst: der Sonnenstand **zum berechneten Aufgang**
muss −0,833° ergeben (halbe Sonnenscheibe plus Brechung) und im Osten stehen.
Dazu die Mittagshöhen zu den Sonnenwenden, die Taglängen und der Fall Nordkap
im Juni, wo `null` die ehrliche Antwort ist und keine erfundene Uhrzeit.

## Wetter

Von der KNX-Wetterstation: **Windalarm und Regen als Bit**, dazu Wind in m/s,
Außentemperatur, Innentemperatur, Helligkeit Ost/Süd/West und wahlweise Azimut
und Elevation.

Die Aufteilung ist Absicht: die Überwachung gehört in die KNX-Logik, wo sie auch
dann läuft, wenn dieses Programm nicht läuft. Hier wird ausgewertet, angezeigt
und erklärt.

**Jeder Wert trägt seinen Zeitpunkt.** Das ist nicht Beiwerk, sondern der halbe
Sinn — siehe Wind. Wie alt ein Wert höchstens sein darf, steht je Größe im
Setup.

Dazu wahlweise eine **Vorhersage von Open-Meteo**, ohne Anmeldung und ohne
Schlüssel. Genommen wird das Maximum der nächsten zwölf Stunden, nicht der
Mittelwert: für die Frage, ob die Markise draußen bleiben darf, zählt die
stärkste Böe. Fällt das Netz aus, zählt weiter allein die Station.

## Einrichten

**Antriebe**: Name, Art, Ausrichtung, Öffnungswinkel, Sonnenhöhen, Positionen,
Windgrenze, Frostgrenze, was mitmachen soll — und die Gruppenadressen. Ist ein
ETS-Projekt geladen (`.knxproj`, `.xml`, `.csv`, `.esf`), schlagen die
Adressfelder beim Tippen vor, gefiltert auf den passenden Datenpunkttyp.

Fünf Arten, weil die Automatik sie unterschiedlich behandeln muss:

| Art | Beschattet | Lüftet | Sichere Seite |
|---|---|---|---|
| Rollladen | ja | — | keine — bleibt stehen |
| Jalousie | ja, mit Lamelle | — | keine |
| Markise | ja | — | eingefahren |
| Fenster | — | ja | geschlossen |
| Lamellendach | ja, mit Lamelle | — | geschlossen |

Ein Rollladen hat bei Wind keine sichere Seite: ihn hochzufahren gäbe dem Wind
erst die Fläche.

**Anlage**: Standort für die Sonnenrechnung, Adressen der Wetterstation,
Grenzen und Zeiten, die Hauptschalter je Regel, der Dienst.

## Betrieb

Die Automatik läuft **im Fenster**, solange es offen ist — oder als
**Windows-Dienst**, dann rund um die Uhr. Ein Wintergarten wartet nicht darauf,
dass jemand angemeldet ist; sonst fehlt der Windschutz genau dann, wenn er
gebraucht wird: nachts und im Urlaub.

```
REGOwintergarden.exe                Oberfläche
REGOwintergarden.exe --einrichten   Dienst einrichten (als Administrator)
REGOwintergarden.exe --entfernen    Dienst wieder entfernen
REGOwintergarden.exe --dienst       läuft als Dienst (startet Windows selbst)
```

Ist der Dienst eingerichtet, rechnet **nur er** — das Fenster zeigt dann an,
statt als zweiter Absender auf denselben Adressen zu senden.

Für das Dienstgerüst ist kein NuGet-Paket im Spiel: `System.ServiceProcess`
läge in einem Paket, und das wäre die einzige Abhängigkeit im ganzen Programm.
Die drei Aufrufe, um die es geht, stehen als P/Invoke da.

## Protokoll

Jede Fahrt mit Grund, jede Meldung, jede Störung — in der Oberfläche und in
`protokoll.log` im Einstellungsordner. Bei einem Megabyte wird umgelegt. Dort
steht auch, was der Dienst gemeldet hat; das ist die Stelle, an der man
morgens nachsieht, warum die Markise nachts eingefahren ist.

## Einstellungen

Eine Datei, lesbar, im Benutzerprofil:

```
%LOCALAPPDATA%\REGOwintergarden\einstellungen.json
```

Über `REGOWINTERGARDEN_HOME` umlenkbar — der Dienst braucht das, weil er unter
einem anderen Konto läuft. Geschrieben wird erst daneben und dann getauscht:
ein Stromausfall mitten im Speichern soll nicht die Anlage kosten.

## Auf dem Raspberry Pi

Der Wintergarten braucht keinen Windows-Rechner, der durchläuft. Dieselbe
Steuerung gibt es als **Linux-Dienst mit Weboberfläche** — für den Pi, für ein
NAS, für jede virtuelle Maschine.

### In einem Zug einrichten

```sh
curl -fsSL https://raw.githubusercontent.com/epogo75/REGOwintergarden/main/linux/install.sh | sudo sh
```

Das Skript erkennt die Architektur (arm64, armhf, x86-64), legt das Programm
nach `/opt/regowintergarden`, richtet einen eigenen Benutzer ohne Anmeldung
ein, legt die Einstellungen unter `/etc/regowintergarden` an und startet den
systemd-Dienst. Danach steht die Oberfläche unter `http://<pi>:8080`.

```sh
journalctl -u regowintergarden -f      # zusehen
systemctl restart regowintergarden     # nach Änderungen an der Einstellung
/opt/regowintergarden/uninstall.sh     # wieder entfernen
```

Das Entfernen lässt die Einstellungen stehen. Ein Skript, das beim
Deinstallieren ungefragt die Anlagendaten mitnimmt, hat schon manchen Abend
gekostet.

### Mit Docker

```sh
git clone https://github.com/epogo75/REGOwintergarden
cd REGOwintergarden
docker compose up -d
```

**Netzwerkmodus `host`, und das mit Absicht:** der KNX-Tunnel spricht UDP, und
die Gatewaysuche arbeitet mit Rundrufen. Beides überlebt eine Adressumsetzung
nicht — in einem eigenen Containernetz findet die Steuerung das Gateway nicht
und bekommt keine Telegramme zurück.

Die Einstellungen liegen im Datenträger `./daten`, damit sie eine
Aktualisierung des Bildes überleben. `/gesundheit` beantwortet die Frage, ob
die Automatik noch rechnet — als `HEALTHCHECK` eingetragen.

### Die Weboberfläche

Dieselben Angaben wie im Windows-Fenster: Statusband mit Erklärung, die
Wetterleuchten, der Sonnenkompass als SVG, „Als Nächstes", und je Antrieb eine
Kachel mit Grund und den drei Knöpfen.

Kein Skript, kein Rahmenwerk, kein CDN: die Seite lädt auch dann, wenn der
Wintergarten kein Internet hat, und sie funktioniert in jedem alten
Tabletbrowser. Sie lädt sich alle dreißig Sekunden selbst neu; `/lage.json`
liefert denselben Stand als JSON, wenn eine Visualisierung ihn abholen will.

**Zugriff hat jeder im Netz.** Das ist eine Entscheidung und keine
Nachlässigkeit: eine Wintergartensteuerung im Heimnetz hinter eine Anmeldung zu
sperren führt dazu, dass das Kennwort auf einem Zettel am Tablet klebt. Wer sie
von außen erreichbar macht, gehört hinter einen Reverse Proxy mit Anmeldung.

### Dieselben Einstellungen

`einstellungen.json` hat auf beiden Systemen dasselbe Format. Eine unter
Windows eingerichtete Anlage lässt sich einfach herüberkopieren:

```sh
scp "%LOCALAPPDATA%\REGOwintergarden\einstellungen.json" pi@wintergarten:/tmp/
ssh pi@wintergarten 'sudo mv /tmp/einstellungen.json /etc/regowintergarden/ && sudo systemctl restart regowintergarden'
```

Eingerichtet wird also bequem am Windows-Rechner, gelaufen wird auf dem Pi.

## Bauen

Gebraucht wird das **.NET SDK 8**. Kein NuGet-Paket.

```powershell
.\test.ps1           # alle Pruefungen
.\publish.ps1        # eine einzelne .exe nach dist\  (Windows)
.\publish-linux.ps1  # eine Datei je Architektur     (Linux, Pi)
.\make-icon.ps1      # das Symbol neu zeichnen
```

`publish-linux.ps1` baut für `linux-arm64` (Pi 3/4/5 mit 64-Bit-System),
`linux-arm` (32-Bit, Pi Zero 2) und `linux-x64` — je eine einzelne Datei von
rund dreißig Megabyte, die ohne installiertes .NET läuft. Das ist der Grund für
die Größe: auf einem frisch aufgesetzten Pi soll niemand erst ein SDK
einrichten müssen.

## Aufbau

```
src\REGOwintergarden.Core\      net8.0 - laeuft ueberall
  Knx\        KNXnet/IP - Tunnel, Rahmen, Datenpunkttypen  (aus REGOsimulator)
  Model\      Antriebe, Wetter, Sonnenstand, Regeln, Zeiten - reine Logik
  App\        Bus, Vorhersage, Einstellungen, Aufzeichnung, Ablauf
  Web\        die Bedienseite als HTML

src\REGOwintergarden\           net8.0-windows - die WPF-Oberflaeche
  Service\    Windows-Dienst: Geruest ueber advapi32
  Ui\         Fenster, Kompass, Sinnbilder, Verlaufsgrafik

src\REGOwintergarden.Daemon\    net8.0 - der Linux-Dienst
  Webserver, Start, Schleife

linux\        install.sh, uninstall.sh
tests\REGOwintergarden.Tests\
```

Die Aufteilung ist der Grund, warum es beides gibt: der **Kern** kennt keine
Fenster. Regeln, Sonnenstand, KNX-Tunnel und Aufzeichnung haben mit einer
Oberflaeche nichts zu tun, und ein Wintergarten wartet nicht darauf, dass ein
Windows-Rechner läuft. Die WPF-Oberfläche und der Linux-Dienst sind zwei
Anzeigen desselben Kerns — was in einer geprüft ist, gilt in der anderen.

`Model\` weiß nichts vom Netz: hinein gehen Anlage, Wetter, Sonnenstand und ein
Zeitpunkt, heraus kommt je Antrieb eine Lage mit Ziel und Grund. Deshalb lässt
sich prüfen, dass eine Böe die Markise einfährt, dass sie nach dem Abflauen
nicht sofort wieder ausfährt und dass ein Handgriff zwei Stunden gilt — ohne
dafür im Wintergarten zu stehen und zu warten. Die 106 Prüfungen laufen auf
jedem Rechner.

Die KNX-Schicht liegt hier als **Kopie** aus REGOsimulator/REGOcontroller,
damit dieses Programm eine eigenständige EXE bleibt. Sie ist dort gegen ein
nachgebildetes Gateway abgesichert; eine gemeinsame Bibliothek wäre sauberer,
würde aber aus eigenständigen Programmen abhängige machen.

## Später

- Heizung mitstellen: Sollwert je Betriebsart auf eine eigene Adresse.
- Beschattung nach Fassadenverschattung durch Nachbargebäude (Horizontlinie).
- Rückmeldung der Automatik auf den Bus, damit eine Visualisierung den Grund
  ebenfalls anzeigen kann.
