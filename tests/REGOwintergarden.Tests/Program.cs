using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using REGOwintergarden.App;
using REGOwintergarden.Knx;
using REGOwintergarden.Model;

namespace REGOwintergarden.Tests;

/// <summary>Minimale Zusicherungen mit Zaehler - wie in den Schwesterprojekten.</summary>
public static class Check
{
    private static int _bestanden;
    private static readonly List<string> Fehler = new();
    private static string _abschnitt = "";

    public static void Abschnitt(string name)
    {
        _abschnitt = name;
        Console.WriteLine();
        Console.WriteLine($"=== {name} ===");
    }

    public static void Das(bool bedingung, string was)
    {
        if (bedingung) { _bestanden++; return; }
        Fehler.Add($"{_abschnitt}: {was}");
        Console.WriteLine($"  FEHLGESCHLAGEN  {was}");
    }

    public static void Gleich<T>(T erwartet, T tatsaechlich, string was)
    {
        var ok = EqualityComparer<T>.Default.Equals(erwartet, tatsaechlich);
        if (!ok) Console.WriteLine($"  erwartet <{erwartet}>, war <{tatsaechlich}>");
        Das(ok, was);
    }

    public static void Nahe(double erwartet, double tatsaechlich, double abweichung, string was)
    {
        var ok = Math.Abs(erwartet - tatsaechlich) <= abweichung;
        if (!ok)
        {
            Console.WriteLine("  erwartet " + erwartet.ToString("0.###", CultureInfo.InvariantCulture)
                              + " ± " + abweichung.ToString("0.###", CultureInfo.InvariantCulture)
                              + ", war " + tatsaechlich.ToString("0.###", CultureInfo.InvariantCulture));
        }
        Das(ok, was);
    }

    public static int Bericht()
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        if (Fehler.Count == 0)
        {
            Console.WriteLine($"Alle {_bestanden} Pruefungen bestanden.");
            return 0;
        }
        Console.WriteLine($"{Fehler.Count} von {_bestanden + Fehler.Count} Pruefungen fehlgeschlagen:");
        foreach (var f in Fehler) Console.WriteLine($"  - {f}");
        return 1;
    }
}

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Das Symbol der EXE wird gezeichnet und nicht gemalt - siehe
        // Ui\AppIcons.cs. Geschrieben wird es von hier aus, weil der
        // Zeichencode im Programm steckt und der Build die fertige Datei
        // schon braucht, bevor irgendetwas laufen kann.
        if (args.Length == 2 && args[0] == "--symbol")
        {
            REGOwintergarden.Ui.AppIcons.WriteAppIco(args[1]);
            Console.WriteLine("Symbol geschrieben: " + args[1]);
            return 0;
        }

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("REGOwintergarden — Pruefungen");

        Winkel();
        Sonne();
        Wetter();
        Beschattung();
        Schutz();
        Lueften();
        Zeiten();
        Vorhersage();
        Werte();
        Symbol();

        return Check.Bericht();
    }

    // ===================================================================
    // Winkel und Richtungen
    // ===================================================================

    private static void Winkel()
    {
        Check.Abschnitt("Winkel");

        Check.Gleich(0.0, Motor.Normiert(360), "360 Grad sind null");
        Check.Gleich(350.0, Motor.Normiert(-10), "minus zehn sind 350");

        // Ueber Nord hinweg zu rechnen ist der Fehler, den man einmal macht.
        Check.Gleich(20.0, Motor.Abstand(350, 10), "zwischen 350 und 10 liegen zwanzig Grad");
        Check.Gleich(180.0, Motor.Abstand(0, 180), "Nord und Sued liegen 180 auseinander");
        Check.Gleich(90.0, Motor.Abstand(90, 180), "Ost und Sued neunzig");

        Check.Gleich("S", Motor.Richtungsname(180), "180 Grad ist Sued");
        Check.Gleich("SSW", Motor.Richtungsname(205), "205 Grad ist SSW und nicht Sued");
        Check.Gleich("N", Motor.Richtungsname(358), "358 Grad ist noch Nord");

        // Eine Suedflaeche mit 75 Grad Oeffnung sieht die Sonne von Ost bis
        // West, aber nicht im Norden.
        var sued = new Motor { Ausrichtung = 180, Oeffnungswinkel = 75, ElevationMin = 8, ElevationMax = 90 };
        Check.Das(sued.SonneAufDerFlaeche(180, 40), "Mittagssonne steht auf der Suedflaeche");
        Check.Das(sued.SonneAufDerFlaeche(110, 30), "Vormittagssonne noch");
        Check.Das(!sued.SonneAufDerFlaeche(90, 30), "Ost gerade nicht mehr");
        Check.Das(!sued.SonneAufDerFlaeche(180, 5), "und flach stehende Sonne nicht");
    }

    // ===================================================================
    // Sonnenstand
    // ===================================================================

    private static void Sonne()
    {
        Check.Abschnitt("Sonnenstand");

        // Buehl, Baden: 48,70 Nord, 8,14 Ost.
        const double breite = 48.70;
        const double laenge = 8.14;

        // Sonnenwende, wahrer Mittag. Die Hoehe im Mittag ist
        // 90 - Breite + Deklination, und die Deklination ist zur
        // Sommersonnenwende 23,44 Grad: 90 - 48,7 + 23,44 = 64,7.
        var mittagSommer = Astro.BerechnenUtc(new DateTime(2026, 6, 21, 11, 27, 0, DateTimeKind.Utc),
            breite, laenge);
        Check.Nahe(64.7, mittagSommer.Elevation, 0.6, "Sommersonnenwende: Mittagshoehe");
        Check.Nahe(180, mittagSommer.Azimut, 2.0, "und die Sonne steht im Sueden");

        // Wintersonnenwende: 90 - 48,7 - 23,44 = 17,9.
        var mittagWinter = Astro.BerechnenUtc(new DateTime(2026, 12, 21, 11, 20, 0, DateTimeKind.Utc),
            breite, laenge);
        Check.Nahe(17.9, mittagWinter.Elevation, 0.8, "Wintersonnenwende: Mittagshoehe");

        // Zur Tagundnachtgleiche geht die Sonne im Osten auf. Geprueft wird
        // gegen den selbst gerechneten Aufgang und nicht gegen eine von Hand
        // gesuchte Uhrzeit: das prueft zugleich, dass Sonnenstand und
        // Aufgangszeit zueinander passen - genau dort liegt der Fehler, den
        // man sonst nicht sieht.
        var gleiche = Astro.BerechnenUtc(new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc), breite, laenge);
        Check.Das(gleiche.Aufgang is not null, "zur Tagundnachtgleiche gibt es einen Aufgang");

        var beimAufgang = Astro.BerechnenUtc(gleiche.Aufgang!.Value, breite, laenge);
        Check.Nahe(90, beimAufgang.Azimut, 3.0, "beim Aufgang steht die Sonne im Osten");

        // Beim Aufgang steht die Sonnenmitte gut ein halbes Grad unter dem
        // Horizont - halbe Scheibe plus Brechung der Luft. Das ist die
        // uebliche Festlegung und der Grund, warum die Sonne frueher
        // erscheint, als die Geometrie es erlaubt.
        Check.Nahe(-0.833, beimAufgang.Elevation, 0.6, "und zwar dicht am Horizont");

        // Am Nachmittag steht sie im Westen, am Vormittag im Osten - das ist
        // die Probe darauf, dass der Stundenwinkel richtig herum eingeht.
        var nachmittag = Astro.BerechnenUtc(new DateTime(2026, 6, 21, 15, 0, 0, DateTimeKind.Utc),
            breite, laenge);
        Check.Das(nachmittag.Azimut > 200, "nachmittags steht die Sonne im Westen");
        var vormittag = Astro.BerechnenUtc(new DateTime(2026, 6, 21, 7, 0, 0, DateTimeKind.Utc),
            breite, laenge);
        Check.Das(vormittag.Azimut < 130, "vormittags im Osten");

        // Auf- und Untergang: im Juni ist der Tag lang, im Dezember kurz.
        var juni = Astro.BerechnenUtc(new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc), breite, laenge);
        var dezember = Astro.BerechnenUtc(new DateTime(2026, 12, 21, 12, 0, 0, DateTimeKind.Utc), breite, laenge);
        Check.Das(juni.Aufgang is not null && juni.Untergang is not null, "im Juni gibt es Auf- und Untergang");

        var tagJuni = (juni.Untergang!.Value - juni.Aufgang!.Value).TotalHours;
        var tagDezember = (dezember.Untergang!.Value - dezember.Aufgang!.Value).TotalHours;
        Check.Nahe(16.0, tagJuni, 0.5, "der laengste Tag dauert rund sechzehn Stunden");
        Check.Nahe(8.4, tagDezember, 0.5, "der kuerzeste rund achteinhalb");
        Check.Das(tagJuni > tagDezember, "und der Sommertag ist der laengere");

        // Nordkap im Juni: Mitternachtssonne. Dann ist null die ehrliche
        // Antwort und keine erfundene Uhrzeit.
        var nordkap = Astro.BerechnenUtc(new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc), 71.17, 25.78);
        Check.Das(nordkap.Aufgang is null && nordkap.Untergang is null,
            "am Nordkap gibt es im Juni keinen Untergang");

        // Nachts steht die Sonne unter dem Horizont.
        var nacht = Astro.BerechnenUtc(new DateTime(2026, 1, 15, 1, 0, 0, DateTimeKind.Utc), breite, laenge);
        Check.Das(nacht.Elevation < 0, "um zwei Uhr nachts ist die Sonne unter");
        Check.Das(!nacht.Tag, "und der Tag hat noch nicht begonnen");
    }

    // ===================================================================
    // Wetterlage
    // ===================================================================

    private static Wetterlage Lage(DateTime jetzt, double wind = 2, double aussen = 18, double innen = 22,
        double hell = 60000, bool regen = false) => new()
    {
        Wind = new Messwert(wind, jetzt),
        Aussen = new Messwert(aussen, jetzt),
        Innen = new Messwert(innen, jetzt),
        HellOst = new Messwert(hell, jetzt),
        HellSued = new Messwert(hell, jetzt),
        HellWest = new Messwert(hell, jetzt),
        Regen = new Messwert(regen ? 1 : 0, jetzt),
    };

    private static void Wetter()
    {
        Check.Abschnitt("Wetterlage");

        var jetzt = new DateTime(2026, 7, 1, 12, 0, 0);
        var wetter = new Wetterlage
        {
            HellOst = new Messwert(10000, jetzt),
            HellSued = new Messwert(80000, jetzt),
            HellWest = new Messwert(30000, jetzt),
        };

        // Drei Fuehler, acht Ausrichtungen: genommen wird der naechste.
        Check.Gleich(10000.0, wetter.Helligkeit(80)!.Value.Wert, "eine Ostflaeche bekommt den Ostwert");
        Check.Gleich(80000.0, wetter.Helligkeit(190)!.Value.Wert, "eine Suedflaeche den Suedwert");
        Check.Gleich(30000.0, wetter.Helligkeit(280)!.Value.Wert, "eine Westflaeche den Westwert");
        Check.Gleich(80000.0, wetter.HellsteRichtung()!.Value.Wert, "und die hellste Richtung ist Sued");

        // Das Alter zaehlt mit: ein Wert von vor drei Stunden ist keiner.
        var alt = new Messwert(3, jetzt.AddHours(-3));
        Check.Das(!alt.IstFrisch(jetzt, TimeSpan.FromMinutes(10)), "ein drei Stunden alter Wert ist nicht frisch");
        Check.Das(alt.IstFrisch(jetzt, TimeSpan.FromHours(4)), "bei vier Stunden Toleranz schon");
    }

    // ===================================================================
    // Beschattung
    // ===================================================================

    private static (Anlage, Motor) Wintergarten()
    {
        var anlage = new Anlage
        {
            Helligkeitsschwelle = 35000,
            EinschaltverzoegerungMinuten = 3,
            AusschaltverzoegerungMinuten = 15,
        };
        var motor = new Motor
        {
            Name = "Markise Sued",
            Art = Antriebsart.Markise,
            Ausrichtung = 180,
            Oeffnungswinkel = 75,
            Windgrenze = 8,
            Beschattungsposition = 100,
            Freiposition = 0,
        };
        anlage.Motoren.Add(motor);
        return (anlage, motor);
    }

    private static void Beschattung()
    {
        Check.Abschnitt("Beschattung");

        var (anlage, motor) = Wintergarten();
        var automatik = new Automatik();
        var jetzt = new DateTime(2026, 7, 1, 12, 0, 0);
        var sonne = new Sonnenstand(180, 45, null, null);

        // Erst wenn es lange genug hell ist, faehrt die Markise aus. Sofort
        // zu fahren hiesse, auf jede Wolkenluecke zu reagieren.
        var erste = automatik.Bewerten(anlage, Lage(jetzt), sonne, jetzt)[0];
        Check.Gleich(Stufe.Frei, erste.Stufe, "die Verzoegerung laeuft noch");
        Check.Das(erste.Ziel is null, "und es wird noch nicht gefahren");
        Check.Das(erste.Grund.Contains("wartet"), "der Grund sagt, worauf gewartet wird");

        var spaeter = jetzt.AddMinutes(4);
        var zweite = automatik.Bewerten(anlage, Lage(spaeter), sonne, spaeter)[0];
        Check.Gleich(Stufe.Beschattung, zweite.Stufe, "nach der Verzoegerung wird beschattet");
        Check.Gleich(100.0, zweite.Ziel, "die Markise faehrt aus");
        Check.Das(zweite.Grund.Contains("Sonne aus"), "und der Grund nennt den Sonnenstand");

        // Eine Wolke soll die Markise nicht sofort einfahren.
        var wolke = spaeter.AddMinutes(1);
        var dritte = automatik.Bewerten(anlage, Lage(wolke, hell: 5000), sonne, wolke)[0];
        Check.Gleich(100.0, dritte.Ziel, "bei einer Wolke bleibt sie erst einmal draussen");
        Check.Das(dritte.Grund.Contains("oeffnet in"), "der Grund sagt, wann sie einfaehrt");

        var lange = spaeter.AddMinutes(20);
        var vierte = automatik.Bewerten(anlage, Lage(lange, hell: 5000), sonne, lange)[0];
        Check.Gleich(0.0, vierte.Ziel, "nach der Ausschaltverzoegerung faehrt sie ein");

        // Sonne von Osten trifft die Suedflaeche nicht.
        var automatik2 = new Automatik();
        var ost = new Sonnenstand(70, 30, null, null);
        var lage = automatik2.Bewerten(anlage, Lage(jetzt), ost, jetzt)[0];
        Check.Gleich(Stufe.Frei, lage.Stufe, "steht die Sonne im Osten, bleibt die Suedmarkise oben");

        // Ist es drinnen warm, wird frueher beschattet.
        var automatik3 = new Automatik();
        var knapp = Lage(jetzt, hell: 30000, innen: 27);
        automatik3.Bewerten(anlage, knapp, sonne, jetzt);
        var warm = automatik3.Bewerten(anlage, Lage(jetzt.AddMinutes(4), hell: 30000, innen: 27),
            sonne, jetzt.AddMinutes(4))[0];
        Check.Gleich(Stufe.Beschattung, warm.Stufe, "bei warmem Wintergarten sinkt die Schwelle");
        Check.Das(warm.Grund.Contains("Schwelle gesenkt"), "und das steht im Grund");

        // Ohne Helligkeitswert wird nicht beschattet - die freundliche Seite
        // der Unwissenheit.
        var automatik4 = new Automatik();
        var blind = new Wetterlage { Wind = new Messwert(2, jetzt) };
        Check.Gleich(Stufe.Frei, automatik4.Bewerten(anlage, blind, sonne, jetzt)[0].Stufe,
            "ohne Helligkeitswert wird nicht beschattet");
    }

    // ===================================================================
    // Wind, Regen, Frost, Hand
    // ===================================================================

    private static void Schutz()
    {
        Check.Abschnitt("Schutz");

        var (anlage, motor) = Wintergarten();
        var jetzt = new DateTime(2026, 7, 1, 12, 0, 0);
        var sonne = new Sonnenstand(180, 45, null, null);

        // Wind schlaegt alles.
        var automatik = new Automatik();
        var sturm = automatik.Bewerten(anlage, Lage(jetzt, wind: 12), sonne, jetzt)[0];
        Check.Gleich(Stufe.Wind, sturm.Stufe, "Wind ueber der Grenze faehrt ein");
        Check.Gleich(0.0, sturm.Ziel, "und zwar in die Sicherheitsposition");
        Check.Das(sturm.Grund.Contains("12"), "der Grund nennt die Geschwindigkeit");

        // Nach dem Abflauen laeuft der Alarm nach: sofort wieder auszufahren
        // hiesse, in die naechste Boe zu fahren.
        var danach = jetzt.AddMinutes(5);
        var nachlauf = automatik.Bewerten(anlage, Lage(danach, wind: 1), sonne, danach)[0];
        Check.Gleich(Stufe.Wind, nachlauf.Stufe, "der Windalarm laeuft nach");
        Check.Das(nachlauf.Grund.Contains("nach"), "und sagt es");

        var spaet = jetzt.AddMinutes(25);
        var frei = automatik.Bewerten(anlage, Lage(spaet, wind: 1), sonne, spaet)[0];
        Check.Das(frei.Stufe != Stufe.Wind, "nach dem Nachlauf ist der Alarm vorbei");

        // Ein fehlender Windwert ist keine Windstille.
        var automatik2 = new Automatik();
        var ohneWind = new Wetterlage { HellSued = new Messwert(80000, jetzt) };
        var unbekannt = automatik2.Bewerten(anlage, ohneWind, sonne, jetzt)[0];
        Check.Gleich(Stufe.Wind, unbekannt.Stufe, "ohne Windwert faehrt die Markise ein");
        Check.Das(unbekannt.Grund.Contains("kein Windwert"), "und sagt warum");

        var automatik3 = new Automatik();
        var alterWind = new Wetterlage
        {
            Wind = new Messwert(1, jetzt.AddHours(-2)),
            HellSued = new Messwert(80000, jetzt),
        };
        var veraltet = automatik3.Bewerten(anlage, alterWind, sonne, jetzt)[0];
        Check.Gleich(Stufe.Wind, veraltet.Stufe, "ein zwei Stunden alter Windwert zaehlt nicht");
        Check.Das(veraltet.Grund.Contains("alt"), "und das steht im Grund");

        // Regen.
        var automatik4 = new Automatik();
        var nass = automatik4.Bewerten(anlage, Lage(jetzt, regen: true), sonne, jetzt)[0];
        Check.Gleich(Stufe.Regen, nass.Stufe, "Regen faehrt ein");

        // Frost.
        var automatik5 = new Automatik();
        var kalt = automatik5.Bewerten(anlage, Lage(jetzt, aussen: 1), sonne, jetzt)[0];
        Check.Gleich(Stufe.Frost, kalt.Stufe, "unter der Frostgrenze wird eingefahren");

        // Wind schlaegt Regen schlaegt Frost - die Reihenfolge ist die
        // Sicherheitsvorschrift.
        var automatik6 = new Automatik();
        var alles = automatik6.Bewerten(anlage, Lage(jetzt, wind: 20, aussen: 0, regen: true), sonne, jetzt)[0];
        Check.Gleich(Stufe.Wind, alles.Stufe, "bei allem zusammen gewinnt der Wind");

        // Ein Rollladen hat keine sichere Seite und bleibt stehen.
        var rollladen = new Anlage();
        rollladen.Motoren.Add(new Motor { Name = "Rollladen", Art = Antriebsart.Rollladen, Windgrenze = 8 });
        var automatik7 = new Automatik();
        var steht = automatik7.Bewerten(rollladen, Lage(jetzt, wind: 20), sonne, jetzt)[0];
        Check.Das(steht.Stufe != Stufe.Wind, "ein Rollladen faehrt bei Wind nicht");

        // Handgriff: die Automatik haelt sich zurueck - aber nicht bei Wind.
        var automatik8 = new Automatik();
        automatik8.VonHand(motor, jetzt, TimeSpan.FromHours(2));
        var hand = automatik8.Bewerten(anlage, Lage(jetzt), sonne, jetzt)[0];
        Check.Gleich(Stufe.Hand, hand.Stufe, "nach einem Handgriff pausiert die Automatik");
        Check.Das(hand.Ziel is null, "und faehrt nicht");

        var handSturm = automatik8.Bewerten(anlage, Lage(jetzt, wind: 20), sonne, jetzt)[0];
        Check.Gleich(Stufe.Wind, handSturm.Stufe, "der Windschutz gilt trotzdem");

        var nachher = jetzt.AddHours(3);
        var wiederAuto = automatik8.Bewerten(anlage, Lage(nachher, hell: 5000), sonne, nachher)[0];
        Check.Das(wiederAuto.Stufe != Stufe.Hand, "nach der Sperre uebernimmt die Automatik wieder");
    }

    // ===================================================================
    // Lueften
    // ===================================================================

    private static void Lueften()
    {
        Check.Abschnitt("Lueften");

        var anlage = new Anlage { LueftungAb = 26, LueftungHysterese = 2, LueftungUnterschied = 2 };
        var fenster = new Motor
        {
            Name = "Dachfenster",
            Art = Antriebsart.Fenster,
            LueftungAktiv = true,
            BeschattungAktiv = false,
            Windgrenze = 6,
        };
        anlage.Motoren.Add(fenster);

        var jetzt = new DateTime(2026, 7, 1, 14, 0, 0);
        var sonne = new Sonnenstand(200, 50, null, null);
        var automatik = new Automatik();

        // Drinnen warm, draussen kuehler: Fenster auf.
        var auf = automatik.Bewerten(anlage, Lage(jetzt, innen: 28, aussen: 22), sonne, jetzt)[0];
        Check.Gleich(Stufe.Lueftung, auf.Stufe, "bei Waerme wird gelueftet");
        Check.Gleich(40.0, auf.Ziel, "das Fenster oeffnet auf die eingestellte Position");

        // Draussen waermer als drinnen: Lueften brächte nur Waerme herein.
        var automatik2 = new Automatik();
        var heiss = automatik2.Bewerten(anlage, Lage(jetzt, innen: 28, aussen: 33), sonne, jetzt)[0];
        Check.Gleich(Stufe.Frei, heiss.Stufe, "ist es draussen waermer, bleibt zu");

        // Wieder kuehl: Fenster zu, aber erst unter der Hysterese.
        var mittel = jetzt.AddMinutes(30);
        var bleibt = automatik.Bewerten(anlage, Lage(mittel, innen: 25, aussen: 20), sonne, mittel)[0];
        Check.Gleich(40.0, bleibt.Ziel, "bei 25 Grad bleibt es offen - die Hysterese laeuft");

        var kuehl = jetzt.AddMinutes(60);
        var zu = automatik.Bewerten(anlage, Lage(kuehl, innen: 23, aussen: 19), sonne, kuehl)[0];
        Check.Gleich(0.0, zu.Ziel, "unter der Hysterese wird geschlossen");

        // Regen schlaegt Lueften.
        var automatik3 = new Automatik();
        var nass = automatik3.Bewerten(anlage, Lage(jetzt, innen: 30, aussen: 20, regen: true), sonne, jetzt)[0];
        Check.Gleich(Stufe.Regen, nass.Stufe, "bei Regen wird nicht gelueftet, sondern geschlossen");
        Check.Gleich(0.0, nass.Ziel, "und zwar zu");
    }

    // ===================================================================
    // Zeitschaltuhr
    // ===================================================================

    private static void Zeiten()
    {
        Check.Abschnitt("Zeitschaltuhr");

        var anlage = new Anlage();
        var motor = new Motor { Name = "Jalousie", ZeitAktiv = true };
        anlage.Motoren.Add(motor);

        var abends = new Schaltzeit
        {
            Bezug = Zeitbezug.Sonnenuntergang,
            Versatz = -30,
            Tage = "1234567",
            Position = 100,
            Bemerkung = "abends zu",
        };
        anlage.Schaltzeiten.Add(abends);

        // Der 1.7.2026 ist ein Mittwoch. Untergang um 21:30 heisst: Schaltung
        // um 21:00.
        var tag = new DateTime(2026, 7, 1);
        var sonne = new Sonnenstand(270, 5, tag.AddHours(5).AddMinutes(30), tag.AddHours(21).AddMinutes(30));

        var uhr = new Zeitschaltuhr();
        Check.Gleich(0, uhr.Faellige(anlage, sonne, tag.AddHours(20).AddMinutes(59)).Count,
            "eine Minute vorher ist noch nichts faellig");

        var treffer = uhr.Faellige(anlage, sonne, tag.AddHours(21));
        Check.Gleich(1, treffer.Count, "eine halbe Stunde vor Untergang wird geschaltet");
        Check.Gleich(motor.Id, treffer[0].Motor.Id, "und zwar der Antrieb");

        // In derselben Minute kein zweites Mal - sonst liefe es sechzigmal.
        Check.Gleich(0, uhr.Faellige(anlage, sonne, tag.AddHours(21).AddSeconds(30)).Count,
            "in derselben Minute nur einmal");

        // Feste Uhrzeit, nur werktags.
        var morgens = new Schaltzeit { Bezug = Zeitbezug.Uhrzeit, Zeit = "07:00", Tage = "12345", Position = 0 };
        anlage.Schaltzeiten.Clear();
        anlage.Schaltzeiten.Add(morgens);

        var uhr2 = new Zeitschaltuhr();
        Check.Gleich(1, uhr2.Faellige(anlage, sonne, tag.AddHours(7)).Count, "am Mittwoch um sieben");
        var samstag = new DateTime(2026, 7, 4, 7, 0, 0);
        Check.Gleich(0, uhr2.Faellige(anlage, sonne, samstag).Count, "am Samstag nicht");

        // Ein abgeschalteter Eintrag laeuft nicht - und bleibt trotzdem stehen.
        morgens.Aktiv = false;
        Check.Gleich(0, new Zeitschaltuhr().Faellige(anlage, sonne, tag.AddHours(7)).Count,
            "eine abgeschaltete Schaltzeit laeuft nicht");
        Check.Gleich(1, anlage.Schaltzeiten.Count, "sie bleibt aber in der Liste");

        // Die ganze Uhr abschalten.
        morgens.Aktiv = true;
        anlage.ZeitschaltuhrAktiv = false;
        Check.Gleich(0, new Zeitschaltuhr().Faellige(anlage, sonne, tag.AddHours(7)).Count,
            "abgeschaltete Zeitschaltuhr schaltet nichts");
    }

    // ===================================================================
    // Vorhersage
    // ===================================================================

    private static void Vorhersage()
    {
        Check.Abschnitt("Vorhersage");

        var jetzt = new DateTime(2026, 7, 1, 9, 0, 0);
        const string antwort = """
        {
          "hourly": {
            "time": ["2026-07-01T09:00", "2026-07-01T10:00", "2026-07-01T11:00"],
            "wind_gusts_10m": [4.2, 9.8, 16.4],
            "precipitation_probability": [0, 20, 65],
            "temperature_2m": [21.0, 24.5, 27.8]
          }
        }
        """;

        var sicht = Wetterabruf.Lesen(antwort, jetzt);
        Check.Das(sicht is not null, "die Antwort wird gelesen");
        Check.Gleich(16.4, sicht!.WindSpitze, "die staerkste Boe zaehlt, nicht der Mittelwert");
        Check.Gleich(65.0, sicht.Regenwahrscheinlichkeit, "und die hoechste Regenwahrscheinlichkeit");
        Check.Gleich(27.8, sicht.Hoechsttemperatur, "und die Hoechsttemperatur");
        Check.Das(sicht.IstFrisch(jetzt), "frisch geholt ist sie frisch");
        Check.Das(!sicht.IstFrisch(jetzt.AddHours(5)), "nach fuenf Stunden nicht mehr");

        // Kaputte Antwort: lieber keine Vorhersage als eine erfundene.
        Check.Das(Wetterabruf.Lesen("{}", jetzt) is null, "ohne Stundenwerte gibt es keine Vorhersage");

        // Die Vorhersage haelt die Markise drinnen, bevor die Boe da ist.
        var (anlage, _) = Wintergarten();
        anlage.Vorhersage = sicht;
        var sonne = new Sonnenstand(180, 45, null, null);
        var automatik = new Automatik();
        var lage = automatik.Bewerten(anlage, Lage(jetzt, wind: 2), sonne, jetzt)[0];
        Check.Gleich(Stufe.Wind, lage.Stufe, "angesagte Boeen halten die Markise drinnen");
        Check.Das(lage.Grund.Contains("Vorhersage"), "und der Grund sagt, dass es die Vorhersage war");

        anlage.VorhersageAktiv = false;
        var ohne = new Automatik().Bewerten(anlage, Lage(jetzt, wind: 2), sonne, jetzt)[0];
        Check.Das(ohne.Stufe != Stufe.Wind, "abgeschaltet zaehlt sie nicht");
    }

    // ===================================================================
    // Das Symbol
    // ===================================================================

    /// <summary>
    /// Prueft, dass das Symbol wirklich gezeichnet wird.
    ///
    /// Ein leeres Bild faellt sonst erst auf, wenn die fertige EXE in der
    /// Taskleiste steht - und dann ist der Zeichencode laengst wieder aus dem
    /// Kopf. Geprueft wird der Anteil deckender Punkte: ein gezeichnetes
    /// Symbol fuellt seine Flaeche, ein leeres nicht.
    /// </summary>
    private static void Symbol()
    {
        Check.Abschnitt("Symbol");

        // Gezeichnet wird mit System.Drawing und nicht mit WPF: ein Programm
        // ohne Fenster bekommt von RenderTargetBitmap ein leeres Bild
        // zurueck, und zwar ohne Fehlermeldung. Genau das faellt sonst erst
        // auf, wenn die fertige EXE in der Taskleiste steht.
        var bytes = REGOwintergarden.Ui.AppIcons.CreateAppIcoBytes();
        Check.Das(bytes.Length > 3000, "die .ico hat Inhalt");
        Check.Gleich((short)1, BitConverter.ToInt16(bytes, 2), "sie ist als Symbol gekennzeichnet");
        Check.Das(BitConverter.ToInt16(bytes, 4) >= 6, "und enthaelt mehrere Groessen");

        // Der Anteil deckender Punkte: ein gezeichnetes Symbol fuellt seine
        // Flaeche, ein leeres nicht.
        using var symbol = REGOwintergarden.Ui.AppIcons.CreateTrayIcon();
        using var abzug = symbol.ToBitmap();
        var deckend = 0;
        for (var x = 0; x < abzug.Width; x++)
        {
            for (var y = 0; y < abzug.Height; y++)
            {
                if (abzug.GetPixel(x, y).A > 200) deckend++;
            }
        }
        var anteil = deckend / (double)(abzug.Width * abzug.Height);
        Console.WriteLine("  deckende Punkte: " + (anteil * 100).ToString("0", CultureInfo.InvariantCulture) + " %");
        Check.Das(anteil > 0.1, "das Symbol ist gezeichnet und nicht leer");
    }

    // ===================================================================
    // Werte auf der Leitung
    // ===================================================================

    private static void Werte()
    {
        Check.Abschnitt("Werte");

        // Vier Byte Gleitkomma - so meldet eine Wetterstation Azimut und
        // Elevation.
        var azimut = Dpt.Dpt14Encode(213.5);
        Check.Gleich(4, azimut.Bytes.Length, "ein Wert vom Typ 14 ist vier Byte lang");
        Check.Nahe(213.5, Dpt.Dpt14Decode(azimut), 0.01, "und kommt unveraendert zurueck");
        Check.Nahe(-12.25, Dpt.Dpt14Decode(Dpt.Dpt14Encode(-12.25)), 0.01, "auch negativ");

        // Die Umrechnung, mit der die Automatik rechnet.
        Check.Nahe(21.5, Wintergartendienst.Zahl("9.001", Dpt.Dpt9Encode(21.5f))!.Value, 0.01,
            "eine Temperatur wird zur Zahl");
        Check.Nahe(50, Wintergartendienst.Zahl("5.001", Dpt.Dpt5Encode(50))!.Value, 0.5,
            "ein Prozentwert auch");
        Check.Gleich(1.0, Wintergartendienst.Zahl("1.001", Dpt.Dpt1Encode(true)), "ein gesetztes Bit ist die Eins");
        Check.Gleich(0.0, Wintergartendienst.Zahl("1.001", Dpt.Dpt1Encode(false)), "ein geloeschtes die Null");
        Check.Das(Wintergartendienst.Zahl("16.000", Dpt.Dpt16Encode("Text")) is null,
            "aus einem Text wird keine Zahl");

        // Die Anlage aus der Vorlage: acht Antriebe, darunter Markise und
        // Fenster - so, wie ein Wintergarten aussieht.
        var beispiel = Anlage.Beispiel();
        Check.Gleich(8, beispiel.Motoren.Count, "die Vorlage hat acht Antriebe");
        Check.Gleich(2, beispiel.Motoren.Count(m => m.Art == Antriebsart.Markise), "zwei Markisen");
        Check.Gleich(2, beispiel.Motoren.Count(m => m.Art == Antriebsart.Fenster), "zwei Fenster");
        Check.Das(beispiel.Motoren.All(m => m.AdressePosition.Length == 0),
            "und keine Adressen - die traegt man selbst ein");
    }
}
