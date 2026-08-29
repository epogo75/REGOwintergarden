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

## Die Übersicht

Der erste Reiter ist für den Alltag gemacht und nicht für den Errichter.

**Fünf Leuchten** oben: Wind, Regen, draußen, drinnen, Helligkeit. Jede mit
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

Eine ausgefahrene Markise im Sturm ist ein Schaden, eine unbeschattete Scheibe
ist keiner. Deshalb steht Wind ganz oben — auch über dem Handgriff.

### Wind

**Ein stiller Windmesser ist keine Windstille.** Kommt länger als zehn Minuten
kein Wert, fährt alles mit sicherer Seite ein, und der Grund sagt es:
„Windwert ist zu alt (23 min) — zur Sicherheit eingefahren". Wer stattdessen
den letzten bekannten Wert weiterlaufen lässt, baut eine Steuerung, die nach
dem Ausfall der Station im nächsten Sturm nichts tut.

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

Von der KNX-Wetterstation: Regen, Wind, Außentemperatur, Innentemperatur,
Helligkeit Ost/Süd/West, wahlweise Azimut und Elevation.

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

## Bauen

Gebraucht wird das **.NET SDK 8**. Kein NuGet-Paket.

```powershell
.\test.ps1        # alle Pruefungen
.\publish.ps1     # eine einzelne .exe nach dist\
.\make-icon.ps1   # das Symbol neu zeichnen
```

## Aufbau

```
src\REGOwintergarden\
  Knx\        KNXnet/IP - Tunnel, Rahmen, Datenpunkttypen  (aus REGOsimulator)
  Model\      Antriebe, Wetter, Sonnenstand, Regeln, Zeiten - reine Logik
  App\        Bus, Vorhersage, Einstellungen, Ablauf
  Service\    Windows-Dienst: Geruest ueber advapi32
  Ui\         Fenster, Kompass, Sinnbilder
tests\REGOwintergarden.Tests\
```

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
