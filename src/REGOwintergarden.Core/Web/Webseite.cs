using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using REGOwintergarden.App;
using REGOwintergarden.Model;

namespace REGOwintergarden.Web;

/// <summary>
/// Die Bedienseite als HTML - dieselben Angaben wie im Windows-Fenster.
///
/// <b>Warum ueberhaupt eine Oberflaeche auf dem Pi:</b> ein Geraet ohne
/// Bildschirm braucht trotzdem eine Antwort auf „warum ist die Markise
/// eingefahren". Ohne sie ist die Steuerung eine schwarze Kiste, und die
/// erste Stoerung endet in einer SSH-Sitzung im Protokoll.
///
/// Alles in einer Datei, ohne Rahmenwerk, ohne CDN: die Seite muss auch dann
/// laden, wenn der Wintergarten kein Internet hat. Das Bild vom Sonnenstand
/// wird als SVG erzeugt - dieselbe Darstellung wie der Kompass im Fenster.
/// </summary>
public static class Webseite
{
    /// <summary>
    /// Die vier Seiten - dieselben wie im Windows-Fenster.
    ///
    /// Drei fuer den, der den Wintergarten benutzt, eine fuer den, der ihn
    /// eingerichtet hat. Das sind zwei verschiedene Leute mit zwei
    /// verschiedenen Fragen, und eine Seite, die beides mischt, bedient
    /// keinen von beiden.
    /// </summary>
    private static readonly (string Pfad, string Name)[] Seiten =
    {
        ("/", "Bedienung"),
        ("/automatik", "Automatik"),
        ("/verlauf", "Verlauf"),
        ("/konfig", "Konfiguration"),
    };

    /// <summary>
    /// Der Rahmen um jede Seite: Kopf, Navigation, Inhalt, Fuss.
    ///
    /// <paramref name="neuladen"/> gilt nur fuer die Seiten, die etwas
    /// anzeigen. Wer gerade eine Zahl eintippt, will nicht mitten im Feld
    /// stehen, wenn sich die Seite unter ihm neu laedt.
    /// </summary>
    private static void Rahmen(StringBuilder html, Anlage anlage, string pfad, int neuladen)
    {
        html.Append("<!doctype html><html lang=\"de\"><head><meta charset=\"utf-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.Append("<title>REGOwintergarden</title>");

        // Das Symbol im Reiter: dieselbe gelbe Sonne wie auf der EXE, nur als
        // SVG statt als .ico. Eingebettet und nicht nachgeladen - sonst
        // fehlte es genau dort, wo der Wintergarten kein Internet hat, und
        // der Browser fragte bei jedem Neuladen vergebens nach favicon.ico.
        html.Append("<link rel=\"icon\" href=\"data:image/svg+xml,").Append(Sonnensymbol)
            .Append("\">");

        // Kein Skript: was nicht da ist, kann nicht kaputtgehen, und auf einem
        // Tablet an der Wand reicht ein Neuladen.
        if (neuladen > 0)
        {
            html.Append("<meta http-equiv=\"refresh\" content=\"")
                .Append(neuladen.ToString(CultureInfo.InvariantCulture)).Append("\">");
        }
        html.Append("<style>").Append(Stil).Append("</style></head><body>");

        // Nur der Programmname. Wie die Anlage heisst, steht im Statusband und
        // auf der Konfigurationsseite - zweimal dasselbe in zwei Zeilen
        // uebereinander sagt beim zweiten Mal nichts mehr.
        html.Append("<div class=\"kopfzeile\">");
        Bild(html, Sinnbilder.Sonne, 22);
        html.Append("<span class=\"marke\">REGOwintergarden</span></div>");

        html.Append("<nav class=\"menue\">");
        foreach (var (ziel, name) in Seiten)
        {
            html.Append("<a href=\"").Append(ziel).Append('"');
            if (string.Equals(ziel, pfad, StringComparison.Ordinal)) html.Append(" class=\"hier\"");
            html.Append('>').Append(name).Append("</a>");
        }
        html.Append("</nav>");
    }

    private static void Fuss(StringBuilder html, string dazu)
    {
        html.Append("<p class=\"fuss\">REGOwintergarden ").Append(Sicher(Programmstand.Version));
        if (dazu.Length > 0) html.Append(" &middot; ").Append(dazu);
        html.Append("</p></body></html>");
    }

    public static string Bauen(Wintergartendienst dienst, DateTime jetzt)
    {
        var anlage = dienst.Anlage;
        var wetter = dienst.Wetter();
        var lagen = dienst.Lagen;
        var sonne = dienst.Sonne;
        var (ueberschrift, ton) = Lagebericht.Ueberschrift(anlage, lagen);

        var html = new StringBuilder();
        Rahmen(html, anlage, "/", 30);

        // ---- Statusband ----
        //
        // Ein Band, nicht zwei. Ob der Dienst rechnet, steht in der kleinen
        // Zeile mit - solange die Antwort „ja" lautet, ist es keine
        // Schlagzeile wert. Erst wenn ein Takt ausbleibt, wird daraus eine
        // eigene Zeile in Rot, und dann faellt sie auch auf.
        var dienstton = Dienstton(dienst, jetzt);
        var stoerung = string.Equals(dienstton, "warn", StringComparison.Ordinal);

        html.Append("<div class=\"band ").Append(stoerung ? "warn" : ton switch
        {
            Lagebericht.Ton.Warnung => "warn",
            Lagebericht.Ton.Taetig => "aktiv",
            _ => "ruhig",
        }).Append("\">");
        html.Append("<h1>").Append(Sicher(ueberschrift)).Append("</h1>");
        html.Append("<p>").Append(Sicher(Lagebericht.Erklaerung(anlage, lagen, wetter, sonne, jetzt)))
            .Append("</p>");
        if (stoerung)
        {
            html.Append("<p class=\"stoerung\">").Append(Sicher(Dienstzeile(dienst, jetzt)))
                .Append("</p>");
        }
        html.Append("<p class=\"klein\">").Append(Sicher(anlage.Name)).Append(" &middot; ")
            .Append(lagen.Count.ToString(CultureInfo.InvariantCulture)).Append(" Antriebe &middot; ")
            .Append(dienst.Stand == Busstand.Verbunden ? "mit dem Bus verbunden" : "keine Busverbindung")
            .Append(" &middot; ").Append(Sicher(Dienstzeile(dienst, jetzt)))
            .Append(" Seit ").Append(Sicher(Dauer(jetzt - dienst.Gestartet)))
            .Append(", Fassung ").Append(Sicher(Programmstand.Version))
            .Append(" &middot; ").Append(jetzt.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture))
            .Append("</p></div>");

        // ---- Wetterleuchten ----
        //
        // Vorne die Verbindung: alle anderen Leuchten zeigen Messwerte, und
        // fehlt der Bus, sind die nicht falsch, sondern gar nicht da. Eine
        // Anlage, die stillsteht, weil das Gateway aus ist, saehe sonst
        // genauso ruhig aus wie eine, bei der alles stimmt.
        var anschluss = Anschlussbild.Bilden(dienst);
        html.Append("<div class=\"reihe\">");
        Leuchte(html, anschluss.Name, anschluss.Wert, anschluss.Alarm,
            dienst.IstFern ? Sinnbilder.Haus : Sinnbilder.Warnung, anschluss.Erklaerung,
            anschluss.Bekannt);
        Leuchte(html, "Wind", Windtext(anlage, wetter, jetzt, out var windAlarm), windAlarm,
            Sinnbilder.Wind);
        Leuchte(html, "Regen", Regentext(anlage, wetter, jetzt, out var nass), nass,
            Sinnbilder.Regen);
        Leuchte(html, "draussen", Grad(wetter.Aussen, anlage.HoechstalterTemperatur, jetzt), false,
            Sinnbilder.Thermometer);
        Leuchte(html, "drinnen", Grad(wetter.Innen, anlage.HoechstalterTemperatur, jetzt), false,
            Sinnbilder.Haus);
        Leuchte(html, "Helligkeit", Lux(wetter.HellsteRichtung(), anlage.HoechstalterHelligkeit, jetzt),
            false, Sinnbilder.Sonne);
        Leuchte(html, "an die Aktoren", Ausgabetext(anlage, dienst), dienst.Sicherheitslage.Alarm,
            Sinnbilder.Warnung);
        html.Append("</div>");

        // ---- Sonne und Antriebe ----
        html.Append("<div class=\"spalten\"><div class=\"links\">");
        html.Append("<h2>Sonne</h2><div class=\"karte\">");
        html.Append(Kompass(anlage, lagen, sonne));
        html.Append("</div>");

        html.Append("<h2>Als N&auml;chstes</h2><div class=\"karte\">");
        var vorschau = Lagebericht.Naechstes(anlage, lagen,
            zeit => Astro.Berechnen(zeit, anlage.Breite, anlage.Laenge), jetzt);
        if (vorschau.Count == 0)
        {
            html.Append("<p class=\"klein\">Nichts angek&uuml;ndigt. Was als N&auml;chstes geschieht, "
                        + "entscheidet das Wetter.</p>");
        }
        foreach (var punkt in vorschau)
        {
            html.Append("<p class=\"vor\"><b>").Append(Sicher(punkt.In(jetzt))).Append(" &middot; ")
                .Append(Sicher(punkt.Uhrzeit)).Append("</b><br><span class=\"klein\">")
                .Append(Sicher(punkt.Antrieb)).Append(": ").Append(Sicher(punkt.Was))
                .Append("</span></p>");
        }
        html.Append("</div></div>");

        // ---- Kacheln ----
        html.Append("<div class=\"rechts\"><h2>Antriebe</h2><div class=\"kacheln\">");
        foreach (var lage in lagen) Kachel(html, dienst, lage);
        html.Append("</div></div></div>");

        Fuss(html, "Seite l&auml;dt sich alle 30 Sekunden neu");
        return html.ToString();
    }

    // ===================================================================
    // Automatik
    // ===================================================================

    /// <summary>
    /// Was die Steuerung von selbst tut - je Regel eine Karte mit Schalter,
    /// Erklaerung und dem, was sie gerade bewirkt.
    ///
    /// Erklaert wird dort, wo der Schalter sitzt, und nicht in einer
    /// Anleitung, die niemand liest. Die Frage kommt beim Schalter: „Warum ist
    /// die Markise im Winter oben, obwohl die Sonne scheint?"
    /// </summary>
    public static string Automatikseite(Wintergartendienst dienst, DateTime jetzt)
    {
        var anlage = dienst.Anlage;
        var lagen = dienst.Lagen;

        var html = new StringBuilder();
        Rahmen(html, anlage, "/automatik", 60);

        html.Append("<div class=\"band ruhig\"><h1>Automatik</h1>");
        html.Append("<p>Es gibt eine Rangfolge, und sie ist die Sicherheitsvorschrift dieses "
                    + "Programms: Wind schl&auml;gt alles, danach Regen, Frost, ein Handgriff, "
                    + "die Beschattung, die L&uuml;ftung und zuletzt die Zeitschaltuhr.</p>");
        html.Append("<p class=\"klein\">Eine ausgefahrene Markise im Sturm ist ein Schaden, eine "
                    + "unbeschattete Scheibe ist keiner.</p></div>");

        if (!anlage.AutomatikAktiv)
        {
            html.Append("<div class=\"band warn\"><h1>Automatik ist aus</h1>");
            html.Append("<p>Es wird nichts von selbst gefahren. Wind- und Regenschutz gehen "
                        + "trotzdem hinaus - wer den Komfort abschaltet, schaltet nicht den "
                        + "Windschutz ab.</p></div>");
        }

        html.Append("<div class=\"kacheln\">");
        Regelkarte(html, "hauptschalter", "Automatik", anlage.AutomatikAktiv,
            "Der Hauptschalter. Aus heisst: nichts f&auml;hrt von selbst - ausser dem Wind- und "
            + "Regenschutz, der geht auch dann hinaus.",
            anlage.AutomatikAktiv ? "l&auml;uft" : "steht", Sinnbilder.Haus);

        Regelkarte(html, "wind", "Windschutz", anlage.WindschutzAktiv,
            "F&auml;hrt Markisen ein und Fenster zu, sobald die Station Windalarm meldet oder die "
            + "Grenze &uuml;berschritten ist. Danach l&auml;uft der Schutz noch "
            + Zahl(anlage.WindNachlaufMinuten) + " Minuten nach.",
            Wirkt(lagen, Stufe.Wind), Sinnbilder.Wind);

        Regelkarte(html, "regen", "Regenschutz", anlage.RegenschutzAktiv,
            "Dasselbe bei Regen - f&uuml;r alles, was Regenschutz eingetragen hat. Nachlauf "
            + Zahl(anlage.RegenNachlaufMinuten) + " Minuten.",
            Wirkt(lagen, Stufe.Regen), Sinnbilder.Regen);

        Regelkarte(html, "frost", "Frostschutz", anlage.FrostschutzAktiv,
            "Unter der Frostgrenze bleibt die Markise eingefahren. Eine vereiste Markise "
            + "auszufahren kostet das Tuch.",
            Wirkt(lagen, Stufe.Frost), Sinnbilder.Frost);

        Regelkarte(html, "beschattung", "Beschattung", anlage.BeschattungAktiv,
            "Beschattet, wenn die Sonne auf der Fl&auml;che steht und es heller ist als "
            + Zahl(anlage.Helligkeitsschwelle) + " Lux - nach "
            + Zahl(anlage.EinschaltverzoegerungMinuten) + " Minuten, damit eine einzelne Wolke "
            + "nichts ausl&ouml;st.",
            Wirkt(lagen, Stufe.Beschattung), Sinnbilder.Sonne);

        Regelkarte(html, "lueftung", "L&uuml;ftung", anlage.LueftungAktiv,
            "&Ouml;ffnet die Fenster ab " + Zahl(anlage.LueftungAb) + " Grad drinnen, solange es "
            + "draussen mindestens " + Zahl(anlage.LueftungUnterschied) + " Grad k&uuml;hler ist. "
            + "W&auml;re es draussen w&auml;rmer, brächte L&uuml;ften nur mehr Hitze herein.",
            Wirkt(lagen, Stufe.Lueftung), Sinnbilder.Fenster);

        Regelkarte(html, "uhr", "Zeitschaltuhr", anlage.ZeitschaltuhrAktiv,
            anlage.Schaltzeiten.Count.ToString(CultureInfo.InvariantCulture)
            + " Schaltzeiten, feste und solche mit Bezug auf Sonnenauf- und -untergang.",
            Wirkt(lagen, Stufe.Zeit), Sinnbilder.Uhr);

        Regelkarte(html, "waermegewinn", "W&auml;rmegewinn", anlage.WaermegewinnAktiv,
            "An kalten Tagen wird nicht beschattet, solange es drinnen k&uuml;hl ist: unter "
            + Zahl(anlage.WaermegewinnAussen) + " Grad draussen und unter "
            + Zahl(anlage.WaermegewinnInnen) + " Grad drinnen. Ein Wintergarten ist im Winter "
            + "eine Heizung - wer im Januar die Markise ausf&auml;hrt, wirft die einzige "
            + "kostenlose W&auml;rme des Tages weg.",
            anlage.WaermegewinnAktiv ? "wacht mit" : "aus", Sinnbilder.Thermometer);

        Regelkarte(html, "hitzevorsorge", "Hitzevorsorge", anlage.HitzevorsorgeAktiv,
            "Sagt die Vorhersage &uuml;ber " + Zahl(anlage.HitzevorsorgeAb) + " Grad an, wird "
            + "fr&uuml;her beschattet. Wer erst beschattet, wenn es drinnen warm ist, kommt zu "
            + "sp&auml;t - die W&auml;rme steckt dann in Boden und M&ouml;beln.",
            anlage.Vorhersage is null ? "keine Vorhersage da" : Sicher(anlage.Vorhersage.ToString()),
            Sinnbilder.Sonne);

        Regelkarte(html, "nachtauskuehlung", "Nachtausk&uuml;hlung", anlage.NachtauskuehlungAktiv,
            "Nach einem Tag &uuml;ber " + Zahl(anlage.NachtauskuehlungAb) + " Grad werden die "
            + "Fenster nachts ge&ouml;ffnet, bis drinnen " + Zahl(anlage.NachtauskuehlungZiel)
            + " Grad erreicht sind. Die wirksamste K&uuml;hlung, die ein Wintergarten hat - und "
            + "sie kostet nichts.",
            anlage.NachtauskuehlungAktiv ? "wacht mit" : "aus", Sinnbilder.Fenster);

        Regelkarte(html, "vorhersage", "Wettervorhersage", anlage.VorhersageAktiv,
            "Holt zweimal je Stunde die Vorhersage f&uuml;r " + Sicher(anlage.Ort)
            + " von Open-Meteo. Ohne Schl&uuml;ssel, ohne Anmeldung.",
            anlage.Vorhersage is null ? "noch nichts geholt" : Sicher(anlage.Vorhersage.ToString()),
            Sinnbilder.Wolke);
        html.Append("</div>");

        Fuss(html, "Stand " + jetzt.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        return html.ToString();
    }

    /// <summary>
    /// In einem Satz, ob der Dienst arbeitet.
    ///
    /// Gemessen wird am letzten Takt und nicht daran, dass die Seite
    /// ueberhaupt antwortet: den Webserver bedient ein anderer Faden. Er
    /// antwortet auch dann noch freundlich, wenn die Automatik laengst steht -
    /// und genau das waere die Stoerung, die man nicht sehen will.
    /// </summary>
    public static string Dienstzeile(Wintergartendienst dienst, DateTime jetzt)
    {
        if (!dienst.Laeuft) return "Die Automatik steht - es wird nichts gerechnet und nichts gefahren.";
        if (dienst.LetzterTakt is not { } takt) return "Die Automatik ist gestartet, rechnet aber noch.";

        var her = jetzt - takt;
        var erwartet = TimeSpan.FromSeconds(Math.Clamp(dienst.Anlage.TaktSekunden, 5, 300));
        // Klartext und keine HTML-Entitaeten: die Zeile geht durch Sicher(),
        // und ein „&auml;" waere dort zweimal entschaerft im Browser gelandet.
        return her > erwartet + erwartet + TimeSpan.FromSeconds(30)
            ? "Der letzte Rechendurchgang ist " + Dauer(her) + " her - erwartet wird alle "
              + Zahl(dienst.Anlage.TaktSekunden) + " Sekunden einer."
            : "Der Dienst läuft. Letzter Rechendurchgang vor " + Dauer(her) + ".";
    }

    private static string Dienstton(Wintergartendienst dienst, DateTime jetzt)
    {
        if (!dienst.Laeuft) return "warn";
        if (dienst.LetzterTakt is not { } takt) return "ruhig";

        var erwartet = TimeSpan.FromSeconds(Math.Clamp(dienst.Anlage.TaktSekunden, 5, 300));
        return jetzt - takt > erwartet + erwartet + TimeSpan.FromSeconds(30) ? "warn" : "aktiv";
    }

    /// <summary>Eine Zeitspanne, wie man sie sagt - nicht wie man sie rechnet.</summary>
    public static string Dauer(TimeSpan spanne)
    {
        if (spanne < TimeSpan.Zero) spanne = TimeSpan.Zero;
        if (spanne.TotalSeconds < 90)
        {
            return ((int)spanne.TotalSeconds).ToString(CultureInfo.InvariantCulture) + " Sekunden";
        }
        if (spanne.TotalMinutes < 90)
        {
            return ((int)spanne.TotalMinutes).ToString(CultureInfo.InvariantCulture) + " Minuten";
        }
        if (spanne.TotalHours < 48)
        {
            return ((int)spanne.TotalHours).ToString(CultureInfo.InvariantCulture) + " Stunden";
        }
        return ((int)spanne.TotalDays).ToString(CultureInfo.InvariantCulture) + " Tagen";
    }

    private static string Wirkt(IReadOnlyList<Lage> lagen, Stufe stufe)
    {
        var anzahl = 0;
        foreach (var lage in lagen) if (lage.Stufe == stufe) anzahl++;
        return anzahl == 0
            ? "wirkt gerade nicht"
            : "wirkt gerade auf " + anzahl.ToString(CultureInfo.InvariantCulture)
              + (anzahl == 1 ? " Antrieb" : " Antriebe");
    }

    private static void Regelkarte(StringBuilder html, string schluessel, string name, bool an,
        string erklaerung, string tut, string sinnbild)
    {
        html.Append("<div class=\"kachel regel").Append(an ? "" : " aus").Append("\">");
        html.Append("<div class=\"kopf\">");
        Bild(html, sinnbild, 24);
        html.Append("<div class=\"name\">").Append(name).Append("</div></div>");
        html.Append("<p class=\"klein\">").Append(erklaerung).Append("</p>");
        html.Append("<p class=\"tut\">").Append(tut).Append("</p>");
        html.Append("<form method=\"post\" action=\"/schalten\">");
        html.Append("<input type=\"hidden\" name=\"regel\" value=\"").Append(schluessel).Append("\">");
        html.Append("<input type=\"hidden\" name=\"an\" value=\"").Append(an ? "0" : "1").Append("\">");
        html.Append("<button type=\"submit\">").Append(an ? "ausschalten" : "einschalten")
            .Append("</button></form></div>");
    }

    // ===================================================================
    // Verlauf
    // ===================================================================

    /// <summary>
    /// Der Langzeittrend: Temperatur, Wind und Helligkeit als Kurven, darueber
    /// die Eingriffe der Steuerung als senkrechte Striche.
    ///
    /// Das ist der Punkt an der Ueberblendung: eine Kurve allein beantwortet
    /// die Frage nicht. „Am Dienstag war es doch heiss - warum war die Markise
    /// oben?" laesst sich nur beantworten, wenn man sieht, dass um 11:20 ein
    /// Windalarm kam.
    /// </summary>
    public static string Verlaufsseite(Wintergartendienst dienst, DateTime jetzt, int stunden)
    {
        var anlage = dienst.Anlage;
        var von = jetzt.AddHours(-Math.Clamp(stunden, 1, 24 * 30));
        var punkte = Aufzeichnung.Ausduennen(dienst.Verlauf.Messwerte(von, jetzt), 480);
        var ereignisse = dienst.Verlauf.Ereignisse(von, jetzt);

        var html = new StringBuilder();
        Rahmen(html, anlage, "/verlauf", 0);

        html.Append("<div class=\"band ruhig\"><h1>Verlauf</h1>");
        html.Append("<p>").Append(punkte.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" Messpunkte und ").Append(ereignisse.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" Eingriffe seit ").Append(von.ToString("dd.MM. HH:mm", CultureInfo.InvariantCulture))
            .Append(".</p>");
        html.Append("<p class=\"zeitraum\">");
        foreach (var (h, name) in new[] { (6, "6 Stunden"), (24, "24 Stunden"), (24 * 7, "7 Tage"),
                     (24 * 30, "30 Tage") })
        {
            html.Append("<a href=\"/verlauf?stunden=").Append(h.ToString(CultureInfo.InvariantCulture))
                .Append('"');
            if (h == stunden) html.Append(" class=\"hier\"");
            html.Append('>').Append(name).Append("</a>");
        }
        html.Append("</p></div>");

        if (punkte.Count < 2)
        {
            html.Append("<div class=\"karte\"><p>Noch nichts aufgezeichnet. Die Aufzeichnung "
                        + "beginnt mit dem ersten Takt und schreibt je Minute eine Zeile.</p></div>");
            Fuss(html, "");
            return html.ToString();
        }

        Kurve(html, "Temperatur", "&deg;C", punkte, ereignisse, von, jetzt,
            p => p.Innen, p => p.Aussen, "drinnen", "draussen");
        Kurve(html, "Wind", "m/s", punkte, ereignisse, von, jetzt,
            p => p.Wind, _ => null, "Wind", "");
        Kurve(html, "Helligkeit", "Lux", punkte, ereignisse, von, jetzt,
            p => p.Helligkeit, _ => null, "Helligkeit", "");

        if (ereignisse.Count > 0)
        {
            html.Append("<h2>Eingriffe</h2><div class=\"karte\"><table class=\"liste\">");
            var gezeigt = 0;
            for (var i = ereignisse.Count - 1; i >= 0 && gezeigt < 40; i--, gezeigt++)
            {
                var e = ereignisse[i];
                html.Append("<tr><td class=\"klein\">")
                    .Append(e.Zeit.ToString("dd.MM. HH:mm", CultureInfo.InvariantCulture))
                    .Append("</td><td>").Append(Sicher(e.Antrieb))
                    .Append("</td><td class=\"klein ").Append(Klasse(e.Stufe)).Append("\">");
                Bild(html, Sinnbilder.FuerStufe(e.Stufe), 16);
                html.Append(' ').Append(Sicher(Stufentext(e.Stufe)))
                    .Append("</td><td class=\"klein\">").Append(Sicher(e.Grund))
                    .Append("</td></tr>");
            }
            html.Append("</table></div>");
        }

        Fuss(html, "Aufzeichnung je Minute, eine Datei je Monat");
        return html.ToString();
    }

    /// <summary>
    /// Eine Kurve mit eigener Skala. Grad, Meter je Sekunde und Lux in eine
    /// Achse zu zwingen macht aus der Helligkeit einen Strich am oberen Rand
    /// und aus der Temperatur eine Linie am unteren.
    /// </summary>
    private static void Kurve(StringBuilder html, string titel, string einheit,
        IReadOnlyList<Messpunkt> punkte, IReadOnlyList<Ereignis> ereignisse,
        DateTime von, DateTime bis,
        Func<Messpunkt, double?> erste, Func<Messpunkt, double?> zweite,
        string nameEins, string nameZwei)
    {
        const int breite = 900, hoehe = 180, rand = 4;

        double? klein = null, gross = null;
        foreach (var p in punkte)
        {
            foreach (var wert in new[] { erste(p), zweite(p) })
            {
                if (wert is not { } w) continue;
                klein = klein is null ? w : Math.Min(klein.Value, w);
                gross = gross is null ? w : Math.Max(gross.Value, w);
            }
        }
        if (klein is null || gross is null) return;
        if (Math.Abs(gross.Value - klein.Value) < 0.001) { klein -= 1; gross += 1; }

        var spanne = (bis - von).TotalSeconds;
        if (spanne <= 0) return;

        html.Append("<h2>").Append(titel).Append(" &middot; ").Append(Zahl(klein.Value))
            .Append(" bis ").Append(Zahl(gross.Value)).Append(' ').Append(einheit).Append("</h2>");
        html.Append("<div class=\"karte\"><svg class=\"kurve\" viewBox=\"0 0 ")
            .Append(breite.ToString(CultureInfo.InvariantCulture)).Append(' ')
            .Append(hoehe.ToString(CultureInfo.InvariantCulture))
            .Append("\" preserveAspectRatio=\"none\">");

        // Erst die Eingriffe, damit die Kurven darueber liegen.
        foreach (var e in ereignisse)
        {
            if (e.Zeit < von || e.Zeit > bis) continue;
            var x = (e.Zeit - von).TotalSeconds / spanne * breite;
            html.Append("<line class=\"eingriff ").Append(Klasse(e.Stufe)).Append("\" x1=\"")
                .Append(Zahl(x)).Append("\" y1=\"0\" x2=\"").Append(Zahl(x)).Append("\" y2=\"")
                .Append(hoehe.ToString(CultureInfo.InvariantCulture)).Append("\"><title>")
                .Append(Sicher(e.Zeit.ToString("dd.MM. HH:mm", CultureInfo.InvariantCulture)
                               + " " + e.Antrieb + ": " + e.Grund))
                .Append("</title></line>");
        }

        Linie(html, punkte, erste, von, spanne, klein.Value, gross.Value, breite, hoehe, rand, "eins");
        if (nameZwei.Length > 0)
        {
            Linie(html, punkte, zweite, von, spanne, klein.Value, gross.Value, breite, hoehe, rand, "zwei");
        }

        html.Append("</svg><p class=\"klein\"><span class=\"punkt eins\"></span>").Append(nameEins);
        if (nameZwei.Length > 0)
        {
            html.Append(" <span class=\"punkt zwei\"></span>").Append(nameZwei);
        }
        html.Append(" &middot; senkrechte Striche sind Eingriffe der Steuerung</p></div>");
    }

    private static void Linie(StringBuilder html, IReadOnlyList<Messpunkt> punkte,
        Func<Messpunkt, double?> wert, DateTime von, double spanne, double klein, double gross,
        int breite, int hoehe, int rand, string klasse)
    {
        var pfad = new StringBuilder();
        var offen = false;
        foreach (var p in punkte)
        {
            if (wert(p) is not { } w)
            {
                // Eine Luecke bleibt eine Luecke. Sie zu ueberbruecken hiesse,
                // einen Messwert zu erfinden, den es nie gab.
                offen = false;
                continue;
            }
            var x = (p.Zeit - von).TotalSeconds / spanne * breite;
            var y = hoehe - rand - (w - klein) / (gross - klein) * (hoehe - 2 * rand);
            pfad.Append(offen ? 'L' : 'M').Append(Zahl(x)).Append(' ').Append(Zahl(y)).Append(' ');
            offen = true;
        }
        if (pfad.Length == 0) return;

        html.Append("<path class=\"linie ").Append(klasse).Append("\" d=\"").Append(pfad).Append("\"/>");
    }

    // ===================================================================
    // Konfiguration
    // ===================================================================

    /// <summary>
    /// Die Unterseiten der Konfiguration - dieselbe Aufteilung wie die Reiter
    /// im Fenster. Anlage, Antriebe, Zeiten, Anschluss und Protokoll sind
    /// fuenf verschiedene Fragen; sie auf eine Seite zu legen hiesse, die
    /// Anlagenwerte hinter dreissig Adressfeldern zu verstecken.
    /// </summary>
    private static readonly (string Pfad, string Name)[] Unterseiten =
    {
        ("/konfig", "Anlage"),
        ("/konfig/antriebe", "Antriebe"),
        ("/konfig/zeiten", "Schaltzeiten"),
        ("/konfig/anschluss", "Anschluss"),
        ("/konfig/protokoll", "Protokoll"),
    };

    private static void Unternavigation(StringBuilder html, string pfad, string meldung)
    {
        html.Append("<nav class=\"untermenue\">");
        foreach (var (ziel, name) in Unterseiten)
        {
            html.Append("<a href=\"").Append(ziel).Append('"');
            if (string.Equals(ziel, pfad, StringComparison.Ordinal)) html.Append(" class=\"hier\"");
            html.Append('>').Append(name).Append("</a>");
        }
        html.Append("</nav>");
        if (meldung.Length > 0)
        {
            html.Append("<p class=\"gut\">").Append(Sicher(meldung)).Append("</p>");
        }
    }

    private static void Konfigkopf(StringBuilder html, Wintergartendienst dienst, string pfad,
        string titel, string meldung)
    {
        var anlage = dienst.Anlage;
        Rahmen(html, anlage, "/konfig", 0);

        html.Append("<div class=\"band ruhig\"><h1>").Append(titel).Append("</h1>");
        html.Append("<p class=\"klein\">").Append(Sicher(anlage.Name)).Append(" in ")
            .Append(Sicher(anlage.Ort)).Append(" &middot; ")
            .Append(anlage.Motoren.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" Antriebe &middot; ")
            .Append(anlage.Schaltzeiten.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" Schaltzeiten &middot; ").Append(dienst.Stand == Busstand.Verbunden
                ? "mit dem Bus verbunden"
                : "keine Busverbindung").Append("</p></div>");

        Unternavigation(html, pfad, meldung);
    }

    /// <summary>
    /// Standort, Adressen der Wetterstation, Ausgabe an die Aktoren und alle
    /// Grenzen - dieselben Felder wie auf der Anlageseite im Fenster.
    /// </summary>
    public static string Konfigseite(Wintergartendienst dienst, DateTime jetzt, string meldung)
    {
        var anlage = dienst.Anlage;

        var html = new StringBuilder();
        Konfigkopf(html, dienst, "/konfig", "Anlage", meldung);

        html.Append("<form method=\"post\" action=\"/einstellen\"><div class=\"spalten\">");

        // ---- linke Spalte: Standort und Station ----
        html.Append("<div class=\"halb\">");
        html.Append("<h2>Standort</h2><div class=\"karte\"><table class=\"felder\">");
        Text(html, "name", "Name", anlage.Name, "");
        Text(html, "ort", "Ort", anlage.Ort, "");
        Feld(html, "breite", "Breite", anlage.Breite, "&deg; Nord");
        Feld(html, "laenge", "L&auml;nge", anlage.Laenge, "&deg; Ost");
        html.Append("</table><p class=\"klein\">Breite n&ouml;rdlich positiv, L&auml;nge &ouml;stlich "
                    + "positiv - etwa 48,70 und 8,14 f&uuml;r B&uuml;hl. Daraus rechnet das Programm "
                    + "Sonnenstand, Auf- und Untergang. Meldet die Station Azimut und Elevation, "
                    + "gelten deren Werte; Auf- und Untergang kommen in jedem Fall aus dieser "
                    + "Rechnung - die meldet keine Station.</p>");
        html.Append("<p class=\"klein\">Gerade: ").Append(Sicher(dienst.Sonnenquelle)).Append(", ")
            .Append(Sicher(dienst.Sonne.ToString())).Append("</p></div>");

        html.Append("<h2>Wetterstation</h2><div class=\"karte\"><table class=\"felder\">");
        Text(html, "adr_windalarm", "Windalarm", anlage.AdresseWindalarm, "1.001");
        Text(html, "adr_wind", "Wind m/s", anlage.AdresseWind, "9.005");
        Text(html, "adr_regen", "Regen", anlage.AdresseRegen, "1.001");
        Text(html, "adr_aussen", "draussen", anlage.AdresseAussen, "9.001");
        Text(html, "adr_innen", "drinnen", anlage.AdresseInnen, "9.001");
        Text(html, "adr_ost", "hell Ost", anlage.AdresseHellOst, "9.004");
        Text(html, "adr_sued", "hell S&uuml;d", anlage.AdresseHellSued, "9.004");
        Text(html, "adr_west", "hell West", anlage.AdresseHellWest, "9.004");
        Text(html, "adr_azimut", "Azimut", anlage.AdresseAzimut, "14.007");
        Text(html, "adr_elevation", "Elevation", anlage.AdresseElevation, "14.007");
        html.Append("</table><p class=\"klein\">Regen und Windalarm kommen als fertige Bits von der "
                    + "Station - dort l&auml;uft die &Uuml;berwachung mit B&ouml;enerkennung, Grenze "
                    + "und Nachlauf. Dieses Programm wertet das Ergebnis aus, statt daneben einen "
                    + "zweiten W&auml;chter mit anderen Grenzen zu bauen. Azimut und Elevation sind "
                    + "freiwillig - ohne sie rechnet das Programm sie selbst.</p></div>");

        html.Append("<h2>Sicherheitssignal an die Aktoren</h2><div class=\"karte\">");
        html.Append("<p class=\"klein\">Dieses Programm ist der Chef f&uuml;r Wind und Regen: es "
                    + "h&ouml;rt die Station ab, bildet ein Urteil und meldet es zyklisch an die "
                    + "Aktoren - auf eigenen Adressen. So &uuml;berwacht jede Stufe die vorige. "
                    + "F&auml;llt die Station aus, merkt es dieses Programm. F&auml;llt dieses "
                    + "Programm aus, bleibt die Wiederholung aus, und die Aktoren fahren von selbst "
                    + "in Sicherheit.</p><table class=\"felder\">");
        Text(html, "adr_windaus", "Wind an Aktoren", anlage.AdresseWindausgabe, "1.001");
        Text(html, "adr_regenaus", "Regen an Aktoren", anlage.AdresseRegenausgabe, "1.001");
        Feld(html, "ausgabetakt", "Wiederholung", anlage.AusgabetaktSekunden, "s");
        Feld(html, "windausgabe", "Alarm ab", anlage.WindgrenzeAusgabe, "m/s");
        Haken(html, "invertiert", "Signal invertiert senden", anlage.AusgabeInvertiert);
        html.Append("</table><p class=\"klein\">Die Wiederholung deutlich k&uuml;rzer als die "
                    + "&Uuml;berwachungszeit in den Aktoren - sonst l&ouml;st deren &Uuml;berwachung "
                    + "aus, obwohl alles l&auml;uft. Ein Drittel bis ein Viertel ist die Faustregel: "
                    + "bei 60 Sekunden Wiederholung also drei bis vier Minuten &Uuml;berwachung."
                    + "</p></div></div>");

        // ---- rechte Spalte: Grenzen ----
        html.Append("<div class=\"halb\">");
        html.Append("<h2>Beschattung</h2><div class=\"karte\"><table class=\"felder\">");
        Feld(html, "helligkeit", "ab Helligkeit", anlage.Helligkeitsschwelle, "Lux");
        Feld(html, "ein", "Verz&ouml;gerung ein", anlage.EinschaltverzoegerungMinuten, "min");
        Feld(html, "aus", "Verz&ouml;gerung aus", anlage.AusschaltverzoegerungMinuten, "min");
        Feld(html, "innenwarm", "drinnen warm ab", anlage.InnenWarm, "&deg;C");
        Feld(html, "warmfaktor", "Faktor dann", anlage.WarmFaktor, "");
        html.Append("</table><p class=\"klein\">Das Ausschalten dauert l&auml;nger als das "
                    + "Einschalten, und das mit Absicht: eine einzelne Wolke soll die Markise nicht "
                    + "ein- und wieder ausfahren. Jede Fahrt kostet Mechanik. Ist es drinnen bereits "
                    + "warm, sinkt die Schwelle auf den Faktor - 0,7 heisst dreissig Prozent "
                    + "fr&uuml;her beschatten.</p></div>");

        html.Append("<h2>L&uuml;ften</h2><div class=\"karte\"><table class=\"felder\">");
        Feld(html, "lueftungab", "ab drinnen", anlage.LueftungAb, "&deg;C");
        Feld(html, "lueftunghyst", "Hysterese", anlage.LueftungHysterese, "K");
        Feld(html, "lueftungdelta", "draussen k&uuml;hler", anlage.LueftungUnterschied, "K");
        Feld(html, "lueftungspos", "Fenster auf", anlage.Lueftungsposition, "%");
        html.Append("</table><p class=\"klein\">Gel&uuml;ftet wird nur, wenn es draussen wirklich "
                    + "k&uuml;hler ist - sonst holt das offene Fenster die W&auml;rme herein, statt "
                    + "sie hinauszulassen.</p></div>");

        html.Append("<h2>Klug mitdenken</h2><div class=\"karte\"><table class=\"felder\">");
        Feld(html, "waermeaussen", "W&auml;rmegewinn: draussen unter", anlage.WaermegewinnAussen, "&deg;C");
        Feld(html, "waermeinnen", "und drinnen unter", anlage.WaermegewinnInnen, "&deg;C");
        Feld(html, "hitzeab", "Hitzevorsorge ab Vorhersage", anlage.HitzevorsorgeAb, "&deg;C");
        Feld(html, "nachtab", "Nachtausk&uuml;hlung ab Tagesh&ouml;chstwert", anlage.NachtauskuehlungAb, "&deg;C");
        Feld(html, "nachtziel", "Nachtausk&uuml;hlung bis drinnen", anlage.NachtauskuehlungZiel, "&deg;C");
        html.Append("</table><p class=\"klein\">Ein- und ausgeschaltet werden diese drei Regeln auf "
                    + "der Seite Automatik - hier stehen ihre Grenzen.</p></div>");

        html.Append("<h2>Schutz und Zeiten</h2><div class=\"karte\"><table class=\"felder\">");
        Feld(html, "windnachlauf", "Wind Nachlauf", anlage.WindNachlaufMinuten, "min");
        Feld(html, "regennachlauf", "Regen Nachlauf", anlage.RegenNachlaufMinuten, "min");
        Feld(html, "handsperre", "Handsperre", anlage.HandsperreMinuten, "min");
        Feld(html, "alterwind", "Wind h&ouml;chstens", anlage.HoechstalterWindMinuten, "min");
        Feld(html, "alterregen", "Regen h&ouml;chstens", anlage.HoechstalterRegenMinuten, "min");
        Feld(html, "altertemp", "Temperatur h&ouml;chstens", anlage.HoechstalterTemperaturMinuten, "min");
        Feld(html, "alterhell", "Helligkeit h&ouml;chstens", anlage.HoechstalterHelligkeitMinuten, "min");
        Feld(html, "takt", "Rechentakt", anlage.TaktSekunden, "s");
        Feld(html, "pause", "Mindestpause je Antrieb", anlage.MindestpauseSekunden, "s");
        html.Append("</table><p class=\"klein\">„Wind h&ouml;chstens\" ist das Alter, bis zu dem ein "
                    + "Windwert gilt: kommt l&auml;nger nichts, f&auml;hrt die Anlage in Sicherheit. "
                    + "Ein stiller Windmesser ist keine Windstille.</p></div></div>");

        html.Append("</div><div class=\"karte\"><button type=\"submit\">&Uuml;bernehmen</button>");
        html.Append("<p class=\"klein\">Gespeichert wird sofort in die einstellungen.json - "
                    + "ein Neustart des Dienstes ist nicht n&ouml;tig.</p></div></form>");

        Fuss(html, "");
        return html.ToString();
    }

    /// <summary>Alle Antriebe auf einen Blick, jeder anklickbar.</summary>
    public static string Antriebsliste(Wintergartendienst dienst, DateTime jetzt, string meldung)
    {
        var html = new StringBuilder();
        Konfigkopf(html, dienst, "/konfig/antriebe", "Antriebe", meldung);

        html.Append("<div class=\"karte\"><table class=\"liste\">");
        html.Append("<tr><th>Name</th><th>Art</th><th>Richtung</th><th>Wind</th><th>Fahren</th>"
                    + "<th>Position</th><th>R&uuml;ckmeldung</th><th>Lamelle</th><th></th></tr>");
        foreach (var motor in dienst.Anlage.Motoren)
        {
            html.Append("<tr><td><a href=\"/konfig/antrieb?id=").Append(Sicher(motor.Id))
                .Append("\">").Append(Sicher(motor.Name)).Append("</a>")
                .Append("</td><td class=\"klein\">").Append(Sicher(motor.Art.ToString()))
                .Append("</td><td class=\"klein\">").Append(Sicher(Motor.Richtungsname(motor.Ausrichtung)))
                .Append(' ').Append(Zahl(motor.Ausrichtung)).Append("&deg;")
                .Append("</td><td class=\"klein\">").Append(Zahl(motor.Windgrenze)).Append(" m/s")
                .Append("</td><td class=\"klein\">").Append(Adresse(motor.AdresseFahren))
                .Append("</td><td class=\"klein\">").Append(Adresse(motor.AdressePosition))
                .Append("</td><td class=\"klein\">").Append(Adresse(motor.AdressePositionStatus))
                .Append("</td><td class=\"klein\">").Append(motor.HatLamelle
                    ? Adresse(motor.AdresseLamelle)
                    : "<i class=\"blass\">keine</i>")
                .Append("</td><td class=\"klein\"><a href=\"/konfig/antrieb?id=")
                .Append(Sicher(motor.Id)).Append("\">&auml;ndern</a></td></tr>");
        }
        html.Append("</table></div>");
        html.Append("<p class=\"klein\">Antriebe anlegen und l&ouml;schen bleibt im Windows-Fenster: "
                    + "eine Anlage baut man einmal auf, und ein Formular im Browser w&auml;re dabei "
                    + "das schlechtere Werkzeug. Ge&auml;ndert wird hier alles.</p>");

        Fuss(html, "");
        return html.ToString();
    }

    /// <summary>Ein Antrieb mit allen Feldern - wie die Antriebsseite im Fenster.</summary>
    public static string Antriebsformular(Wintergartendienst dienst, DateTime jetzt, string id,
        string meldung)
    {
        var motor = dienst.Anlage.Finde(id);

        var html = new StringBuilder();
        Konfigkopf(html, dienst, "/konfig/antriebe",
            motor is null ? "Antrieb" : Sicher(motor.Name), meldung);

        if (motor is null)
        {
            html.Append("<div class=\"karte\"><p>Diesen Antrieb gibt es nicht (mehr). "
                        + "<a href=\"/konfig/antriebe\">Zur&uuml;ck zur Liste</a></p></div>");
            Fuss(html, "");
            return html.ToString();
        }

        html.Append("<form method=\"post\" action=\"/antrieb\">");
        html.Append("<input type=\"hidden\" name=\"id\" value=\"").Append(Sicher(motor.Id)).Append("\">");
        html.Append("<div class=\"spalten\"><div class=\"halb\">");

        html.Append("<h2>Antrieb</h2><div class=\"karte\"><table class=\"felder\">");
        Text(html, "name", "Name", motor.Name, "");
        html.Append("<tr><td>Art</td><td><select name=\"art\">");
        foreach (Antriebsart art in Enum.GetValues(typeof(Antriebsart)))
        {
            html.Append("<option value=\"").Append(art.ToString()).Append('"')
                .Append(art == motor.Art ? " selected" : "").Append('>').Append(art.ToString())
                .Append("</option>");
        }
        html.Append("</select></td><td></td></tr>");
        Feld(html, "ausrichtung", "Ausrichtung", motor.Ausrichtung,
            "&deg; = " + Motor.Richtungsname(motor.Ausrichtung));
        html.Append("</table><p class=\"klein\">0 ist Nord, 90 Ost, 180 S&uuml;d, 270 West. Frei "
                    + "einstellbar - die Beschattung rechnet damit.</p></div>");

        html.Append("<h2>Beschattung</h2><div class=\"karte\"><table class=\"felder\">");
        Feld(html, "oeffnung", "&Ouml;ffnungswinkel", motor.Oeffnungswinkel, "&deg;");
        Feld(html, "elevmin", "Sonne ab", motor.ElevationMin, "&deg;");
        Feld(html, "elevmax", "Sonne bis", motor.ElevationMax, "&deg;");
        Feld(html, "beschattung", "Beschattet auf", motor.Beschattungsposition, "%");
        Feld(html, "lamelle", "Lamelle auf", motor.Lamellenposition, "%");
        Feld(html, "frei", "Danach auf", motor.Freiposition, "%");
        html.Append("</table><p class=\"klein\">Der &Ouml;ffnungswinkel sagt, wie weit die Sonne "
                    + "seitlich stehen darf und noch auf die Fl&auml;che scheint - 90 w&auml;re "
                    + "streifender Einfall. Unter „Sonne ab\" steht sie hinter Nachbarh&auml;usern, "
                    + "&uuml;ber „Sonne bis\" scheint sie &uuml;ber die Fl&auml;che hinweg."
                    + "</p></div></div>");

        html.Append("<div class=\"halb\">");
        html.Append("<h2>Schutz</h2><div class=\"karte\"><table class=\"felder\">");
        Feld(html, "wind", "Windgrenze", motor.Windgrenze, "m/s");
        Feld(html, "frost", "Frostgrenze", motor.Frostgrenze, "&deg;C");
        Haken(html, "regenschutz", "Regenschutz", motor.Regenschutz);
        html.Append("</table><p class=\"klein\">&Uuml;ber der Windgrenze f&auml;hrt der Antrieb in "
                    + "Sicherheit - eine Markise ein, ein Fenster zu. Ein Rollladen hat keine sichere "
                    + "Seite und bleibt stehen.</p></div>");

        html.Append("<h2>Automatik</h2><div class=\"karte\"><table class=\"felder\">");
        Haken(html, "beschattungaktiv", "Beschattung", motor.BeschattungAktiv);
        Haken(html, "lueftungaktiv", "L&uuml;ftung", motor.LueftungAktiv);
        Haken(html, "zeitaktiv", "Zeitschaltuhr", motor.ZeitAktiv);
        html.Append("</table></div>");

        html.Append("<h2>Gruppenadressen</h2><div class=\"karte\"><table class=\"felder\">");
        Text(html, "adr_fahren", "Auf/Ab", motor.AdresseFahren, "1.008");
        Text(html, "adr_stopp", "Stopp", motor.AdresseStopp, "1.007");
        Text(html, "adr_position", "Position", motor.AdressePosition, "5.001");
        Text(html, "adr_positionstatus", "Position R&uuml;ckm.", motor.AdressePositionStatus, "5.001");
        Text(html, "adr_lamelle", "Lamelle", motor.AdresseLamelle, "5.001");
        Text(html, "adr_lamellestatus", "Lamelle R&uuml;ckm.", motor.AdresseLamelleStatus, "5.001");
        html.Append("</table></div></div></div>");

        html.Append("<div class=\"karte\"><button type=\"submit\">&Uuml;bernehmen</button> ");
        html.Append("<a class=\"klein\" href=\"/konfig/antriebe\">zur&uuml;ck zur Liste</a></div></form>");

        Fuss(html, "");
        return html.ToString();
    }

    /// <summary>Die Schaltzeiten - anlegen, aendern, loeschen.</summary>
    public static string Zeitenseite(Wintergartendienst dienst, DateTime jetzt, string meldung)
    {
        var anlage = dienst.Anlage;

        var html = new StringBuilder();
        Konfigkopf(html, dienst, "/konfig/zeiten", "Schaltzeiten", meldung);

        html.Append("<div class=\"karte\"><table class=\"liste\">");
        html.Append("<tr><th>Zeit</th><th>Tage</th><th>Antrieb</th><th>auf</th><th>Bemerkung</th>"
                    + "<th></th><th></th></tr>");
        if (anlage.Schaltzeiten.Count == 0)
        {
            html.Append("<tr><td colspan=\"7\" class=\"klein\">Noch keine Schaltzeit eingetragen."
                        + "</td></tr>");
        }
        foreach (var zeit in anlage.Schaltzeiten)
        {
            var motor = anlage.Finde(zeit.MotorId);
            html.Append("<tr").Append(zeit.Aktiv ? "" : " class=\"aus\"").Append("><td>")
                .Append(Sicher(zeit.Zeit))
                .Append(zeit.Versatz == 0 ? "" : " " + (zeit.Versatz > 0 ? "+" : "")
                                                    + zeit.Versatz.ToString(CultureInfo.InvariantCulture)
                                                    + " min")
                .Append("</td><td class=\"klein\">").Append(Sicher(zeit.Tagesnamen()))
                .Append("</td><td class=\"klein\">")
                .Append(motor is null ? "<i class=\"blass\">alle</i>" : Sicher(motor.Name))
                .Append("</td><td class=\"klein\">").Append(Zahl(zeit.Position)).Append(" %")
                .Append("</td><td class=\"klein\">").Append(Sicher(zeit.Bemerkung))
                .Append("</td><td class=\"klein\"><form method=\"post\" action=\"/zeit\">")
                .Append("<input type=\"hidden\" name=\"was\" value=\"schalten\">")
                .Append("<input type=\"hidden\" name=\"id\" value=\"").Append(Sicher(zeit.Id))
                .Append("\"><button type=\"submit\">").Append(zeit.Aktiv ? "aus" : "ein")
                .Append("</button></form></td><td class=\"klein\">")
                .Append("<form method=\"post\" action=\"/zeit\">")
                .Append("<input type=\"hidden\" name=\"was\" value=\"loeschen\">")
                .Append("<input type=\"hidden\" name=\"id\" value=\"").Append(Sicher(zeit.Id))
                .Append("\"><button type=\"submit\">l&ouml;schen</button></form></td></tr>");
        }
        html.Append("</table></div>");

        html.Append("<h2>Neue Schaltzeit</h2><div class=\"karte\">");
        html.Append("<form method=\"post\" action=\"/zeit\">");
        html.Append("<input type=\"hidden\" name=\"was\" value=\"neu\"><table class=\"felder\">");
        Text(html, "zeit", "Zeit", "07:00", "hh:mm, oder Aufgang / Untergang");
        Feld(html, "versatz", "Versatz", 0, "min");
        Text(html, "tage", "Tage", "1234567", "1 = Montag");
        html.Append("<tr><td>Antrieb</td><td><select name=\"motor\">");
        html.Append("<option value=\"\">alle</option>");
        foreach (var motor in anlage.Motoren)
        {
            html.Append("<option value=\"").Append(Sicher(motor.Id)).Append("\">")
                .Append(Sicher(motor.Name)).Append("</option>");
        }
        html.Append("</select></td><td></td></tr>");
        Feld(html, "position", "auf", 100, "%");
        Text(html, "bemerkung", "Bemerkung", "", "");
        html.Append("</table><button type=\"submit\">Anlegen</button></form>");
        html.Append("<p class=\"klein\">„Aufgang\" und „Untergang\" beziehen sich auf die Sonne am "
                    + "eingetragenen Standort; der Versatz verschiebt sie in Minuten. Bei den Tagen "
                    + "steht jede Ziffer f&uuml;r einen Wochentag, 1 ist Montag.</p></div>");

        Fuss(html, "");
        return html.ToString();
    }

    /// <summary>Gateway, Fernbedienung, Ausgabe - was am Anschluss haengt.</summary>
    public static string Anschlussseite(Wintergartendienst dienst, DateTime jetzt, string meldung)
    {
        var anlage = dienst.Anlage;

        var html = new StringBuilder();
        Konfigkopf(html, dienst, "/konfig/anschluss", "Anschluss", meldung);

        html.Append("<div class=\"spalten\"><div class=\"halb\">");
        html.Append("<h2>KNX-Bus</h2><div class=\"karte\">");
        html.Append("<form method=\"post\" action=\"/anschluss\"><table class=\"felder\">");
        Text(html, "gateway", "Gateway", dienst.Einstellungen.Gateway, "IP:Port");
        html.Append("</table><button type=\"submit\">Verbinden</button></form>");
        html.Append("<p class=\"klein\">Stand: ").Append(dienst.Stand switch
        {
            Busstand.Verbunden => "verbunden - Telegramme gehen hinaus und werden mitgeh&ouml;rt",
            Busstand.Verbinde => "Verbindung wird aufgebaut",
            Busstand.Fehler => "nicht verbunden, siehe Protokoll",
            _ => "nicht verbunden - ohne Bus rechnet die Automatik zwar, f&auml;hrt aber nichts",
        }).Append("</p>");
        html.Append("<form method=\"post\" action=\"/abfragen\">"
                    + "<button type=\"submit\">Zustand abfragen</button></form>");
        html.Append("<p class=\"klein\">Fragt jede eingetragene Adresse einmal ab. Ein Bus "
                    + "erz&auml;hlt seinen Zustand nicht von selbst - er meldet nur "
                    + "&Auml;nderungen.</p></div>");

        html.Append("<h2>Dienst</h2><div class=\"karte\">");
        html.Append("<p>").Append(Sicher(Dienstzeile(dienst, jetzt))).Append("</p>");
        html.Append("<p class=\"klein\">L&auml;uft seit ")
            .Append(dienst.Gestartet.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture))
            .Append(", also seit ").Append(Sicher(Dauer(jetzt - dienst.Gestartet)))
            .Append(" &middot; Fassung ").Append(Sicher(Programmstand.Version))
            .Append(" &middot; Rechentakt alle ").Append(Zahl(anlage.TaktSekunden))
            .Append(" Sekunden</p></div></div>");

        html.Append("<div class=\"halb\">");
        html.Append("<h2>Zweite Oberfl&auml;che</h2><div class=\"karte\">");
        html.Append("<p class=\"klein\">Das Windows-Fenster kann diesem Dienst zusehen und ihn "
                    + "bedienen, statt selbst zu steuern. Dort unter Konfiguration &rarr; Wer steuert "
                    + "auf „Ein anderer Rechner f&uuml;hrt\" stellen und die Adresse dieses "
                    + "Rechners eintragen.</p>");
        html.Append("<table class=\"liste\">");
        html.Append("<tr><td class=\"klein\">/bus.json</td><td class=\"klein\">Rohwerte mit "
                    + "Zeitstempel und die laufenden Handsperren</td></tr>");
        html.Append("<tr><td class=\"klein\">/einstellungen.json</td><td class=\"klein\">die Anlage, "
                    + "wie sie hier eingerichtet ist</td></tr>");
        html.Append("<tr><td class=\"klein\">/lage.json</td><td class=\"klein\">der ausgewertete "
                    + "Stand f&uuml;r eine Visualisierung</td></tr>");
        html.Append("<tr><td class=\"klein\">/gesundheit</td><td class=\"klein\">eine Zeile, ob "
                    + "gerechnet wird</td></tr>");
        html.Append("</table><p class=\"klein\">Gesteuert wird trotzdem nur an einer Stelle: hier. "
                    + "Zwei Automatiken auf denselben Adressen w&uuml;rden sich gegenseitig "
                    + "&uuml;berfahren.</p></div>");

        html.Append("<h2>Ausgabe an die Aktoren</h2><div class=\"karte\"><table class=\"liste\">");
        html.Append("<tr><td class=\"klein\">Wind</td><td class=\"klein\">")
            .Append(Adresse(anlage.AdresseWindausgabe)).Append("</td></tr>");
        html.Append("<tr><td class=\"klein\">Regen</td><td class=\"klein\">")
            .Append(Adresse(anlage.AdresseRegenausgabe)).Append("</td></tr>");
        html.Append("<tr><td class=\"klein\">zuletzt gesendet</td><td class=\"klein\">")
            .Append(dienst.LetzteAusgabe == DateTime.MinValue
                ? "noch nie"
                : Sicher(Dauer(jetzt - dienst.LetzteAusgabe)) + " her")
            .Append("</td></tr></table><p class=\"klein\">Zyklisch alle ")
            .Append(Zahl(anlage.AusgabetaktSekunden))
            .Append(" Sekunden - das Lebenszeichen, an dem die Aktoren einen Ausfall dieses "
                    + "Programms erkennen.</p></div></div></div>");

        Fuss(html, "");
        return html.ToString();
    }

    /// <summary>Das Protokoll - was das Programm zuletzt getan und gemeldet hat.</summary>
    public static string Protokollseite(Wintergartendienst dienst, DateTime jetzt, string meldung)
    {
        var html = new StringBuilder();
        Konfigkopf(html, dienst, "/konfig/protokoll", "Protokoll", meldung);

        var zeilen = dienst.Protokoll;
        html.Append("<div class=\"karte\">");
        if (zeilen.Count == 0)
        {
            html.Append("<p class=\"klein\">Noch nichts gemeldet.</p>");
        }
        else
        {
            html.Append("<table class=\"liste\">");
            for (var i = zeilen.Count - 1; i >= 0; i--)
            {
                var z = zeilen[i];
                html.Append("<tr").Append(z.Problem ? " class=\"problem\"" : "")
                    .Append("><td class=\"klein\">")
                    .Append(z.Zeit.ToString("dd.MM. HH:mm:ss", CultureInfo.InvariantCulture))
                    .Append("</td><td>").Append(Sicher(z.Was))
                    .Append("</td><td class=\"klein\">").Append(Sicher(z.Dazu))
                    .Append("</td></tr>");
            }
            html.Append("</table>");
        }
        html.Append("</div><p class=\"klein\">Die letzten ")
            .Append(zeilen.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" Meldungen. Vollst&auml;ndig steht alles in der Protokolldatei neben der "
                    + "einstellungen.json - und bei einem Dienst unter Linux zus&auml;tzlich im "
                    + "journal: journalctl -u regowintergarden</p>");

        Fuss(html, "");
        return html.ToString();
    }

    private static void Feld(StringBuilder html, string name, string beschriftung, double wert,
        string einheit)
    {
        html.Append("<tr><td>").Append(beschriftung).Append("</td><td>");
        html.Append("<input type=\"text\" name=\"").Append(name).Append("\" value=\"")
            .Append(Zahl(wert)).Append("\"></td><td class=\"klein\">").Append(einheit)
            .Append("</td></tr>");
    }

    private static void Text(StringBuilder html, string name, string beschriftung, string wert,
        string dazu)
    {
        html.Append("<tr><td>").Append(beschriftung).Append("</td><td>");
        html.Append("<input type=\"text\" name=\"").Append(name).Append("\" value=\"")
            .Append(Sicher(wert)).Append("\"></td><td class=\"klein\">").Append(dazu)
            .Append("</td></tr>");
    }

    /// <summary>
    /// Ein Haken. Das versteckte Feld davor ist der Trick, den HTML braucht:
    /// ein nicht angehakter Kasten wird gar nicht mitgeschickt, und ohne den
    /// Vorgaenger liesse sich ein Haken nie wieder entfernen.
    /// </summary>
    private static void Haken(StringBuilder html, string name, string beschriftung, bool an)
    {
        html.Append("<tr><td>").Append(beschriftung).Append("</td><td>");
        html.Append("<input type=\"hidden\" name=\"").Append(name).Append("\" value=\"0\">");
        html.Append("<input type=\"checkbox\" name=\"").Append(name).Append("\" value=\"1\"")
            .Append(an ? " checked" : "").Append("></td><td></td></tr>");
    }

    private static string Adresse(string adresse) =>
        adresse.Trim().Length == 0 ? "<i class=\"blass\">nicht eingetragen</i>" : Sicher(adresse);

    // ---- Bausteine ---------------------------------------------------------

    /// <summary>
    /// Ein Sinnbild als SVG - dieselben Striche wie im Fenster.
    ///
    /// Moeglich ist das, weil WPF seine Pfadsyntax von SVG uebernommen hat:
    /// derselbe Text laesst sich hier wie dort zeichnen. Die Pfade stehen
    /// deshalb im Kern und nicht in einer der beiden Oberflaechen.
    /// </summary>
    private static void Bild(StringBuilder html, string pfad, double groesse)
    {
        html.Append("<svg class=\"sinnbild\" viewBox=\"0 0 24 24\" width=\"")
            .Append(Zahl(groesse)).Append("\" height=\"").Append(Zahl(groesse))
            .Append("\"><path d=\"").Append(pfad).Append("\"/></svg>");
    }

    private static void Leuchte(StringBuilder html, string name, string wert, bool alarm,
        string sinnbild, string hinweis = "", bool bekannt = true)
    {
        html.Append("<div class=\"leuchte").Append(alarm ? " alarm" : "")
            .Append(bekannt ? "" : " blind").Append('"');
        if (hinweis.Length > 0) html.Append(" title=\"").Append(Sicher(hinweis)).Append('"');
        html.Append('>');
        Bild(html, sinnbild, 26);
        html.Append("<div class=\"wert\">").Append(Sicher(wert)).Append("</div>");
        html.Append("<div class=\"klein\">").Append(Sicher(name)).Append("</div></div>");
    }

    private static void Kachel(StringBuilder html, Wintergartendienst dienst, Lage lage)
    {
        var motor = lage.Motor;
        var stand = Position(dienst, motor);

        html.Append("<div class=\"kachel").Append(lage.Stufe >= Stufe.Frost ? " alarm" : "").Append("\">");

        // Die Art als Sinnbild neben den Namen: eine Markise erkennt man so
        // von weitem, ohne das Wort zu lesen.
        html.Append("<div class=\"kopf\">");
        Bild(html, Sinnbilder.FuerArt(motor.Art), 24);
        html.Append("<div><div class=\"name\">").Append(Sicher(motor.Name)).Append("</div>");
        html.Append("<div class=\"klein\">").Append(Sicher(motor.Art.ToString())).Append(" &middot; ")
            .Append(Sicher(motor.Richtung)).Append(" ")
            .Append(Math.Round(motor.Ausrichtung).ToString("0", CultureInfo.InvariantCulture))
            .Append("&deg;</div></div></div>");

        html.Append("<div class=\"position\">")
            .Append(stand is null ? "&mdash;" : Math.Round(stand.Value).ToString("0", CultureInfo.InvariantCulture) + " %")
            .Append("</div>");

        // Und die wirksame Regel als Sinnbild vor dem Grund - warum der
        // Antrieb steht, wo er steht.
        html.Append("<div class=\"grund ").Append(Klasse(lage.Stufe)).Append("\">");
        Bild(html, Sinnbilder.FuerStufe(lage.Stufe), 16);
        html.Append(' ').Append(Sicher(Stufentext(lage.Stufe) + lage.Grund)).Append("</div>");

        // Die drei Knoepfe sind ein Formular - kein Skript, damit die Seite
        // auch in einem alten Tabletbrowser funktioniert.
        html.Append("<form method=\"post\" action=\"/fahren\" class=\"knoepfe\">");
        html.Append("<input type=\"hidden\" name=\"motor\" value=\"").Append(Sicher(motor.Id)).Append("\">");
        html.Append("<button name=\"was\" value=\"auf\">Auf</button>");
        html.Append("<button name=\"was\" value=\"stopp\">Stopp</button>");
        html.Append("<button name=\"was\" value=\"ab\">Ab</button>");
        html.Append("</form></div>");
    }

    /// <summary>
    /// Der Sonnenkompass als SVG - dieselbe Darstellung wie im Fenster: der
    /// Winkel ist der Azimut, der Abstand zur Mitte die Hoehe.
    /// </summary>
    private static string Kompass(Anlage anlage, IReadOnlyList<Lage> lagen, Sonnenstand sonne)
    {
        const double mitte = 150;
        const double rand = 118;

        var svg = new StringBuilder();
        svg.Append("<svg viewBox=\"0 0 300 300\" class=\"kompass\">");

        for (var i = 3; i >= 1; i--)
        {
            svg.Append("<circle cx=\"150\" cy=\"150\" r=\"")
                .Append(Zahl(rand * i / 3.0)).Append("\" fill=\"none\" stroke=\"")
                .Append(i == 3 ? "#dcdcdc" : "#eeeeec").Append("\"/>");
        }

        foreach (var motor in anlage.Motoren)
        {
            if (!motor.KannBeschatten) continue;
            var beschattet = false;
            foreach (var lage in lagen)
            {
                if (lage.Motor.Id == motor.Id) beschattet = lage.Stufe == Stufe.Beschattung && lage.Ziel > 0;
            }

            var (x1, y1) = Punkt(mitte, rand, motor.Ausrichtung - motor.Oeffnungswinkel);
            var (x2, y2) = Punkt(mitte, rand, motor.Ausrichtung + motor.Oeffnungswinkel);
            var gross = motor.Oeffnungswinkel * 2 > 180 ? 1 : 0;

            svg.Append("<path d=\"M150,150 L").Append(Zahl(x1)).Append(',').Append(Zahl(y1))
                .Append(" A").Append(Zahl(rand)).Append(',').Append(Zahl(rand)).Append(" 0 ")
                .Append(gross).Append(",1 ").Append(Zahl(x2)).Append(',').Append(Zahl(y2))
                .Append(" Z\" fill=\"").Append(beschattet ? "#4d7616" : "#e7e7e4")
                .Append("\" opacity=\"").Append(beschattet ? "0.28" : "0.16").Append("\"/>");
        }

        foreach (var (grad, name) in new[] { (0.0, "N"), (90.0, "O"), (180.0, "S"), (270.0, "W") })
        {
            var (x, y) = Punkt(mitte, rand + 14, grad);
            svg.Append("<text x=\"").Append(Zahl(x)).Append("\" y=\"").Append(Zahl(y + 4))
                .Append("\" text-anchor=\"middle\" class=\"himmel\">").Append(name).Append("</text>");
        }

        if (sonne.Elevation > 0)
        {
            var abstand = rand * (1 - Math.Clamp(sonne.Elevation, 0, 90) / 90.0);
            var (x, y) = Punkt(mitte, abstand, sonne.Azimut);
            svg.Append("<line x1=\"150\" y1=\"150\" x2=\"").Append(Zahl(x)).Append("\" y2=\"")
                .Append(Zahl(y)).Append("\" stroke=\"#8b8b93\"/>");
            svg.Append("<circle cx=\"").Append(Zahl(x)).Append("\" cy=\"").Append(Zahl(y))
                .Append("\" r=\"9\" fill=\"#ffc107\" stroke=\"#cf222e\" stroke-width=\"2\"/>");
        }

        svg.Append("<text x=\"150\" y=\"146\" text-anchor=\"middle\" class=\"mitte\">")
            .Append(sonne.Elevation > 0 ? "Sonne" : "Sonne unter").Append("</text>");
        svg.Append("<text x=\"150\" y=\"164\" text-anchor=\"middle\" class=\"mittegross\">")
            .Append(Math.Round(sonne.Azimut).ToString("0", CultureInfo.InvariantCulture)).Append("&#176; &middot; ")
            .Append(Math.Round(sonne.Elevation).ToString("0", CultureInfo.InvariantCulture)).Append("&#176;</text>");

        if (sonne.Aufgang is { } auf && sonne.Untergang is { } unter)
        {
            svg.Append("<text x=\"150\" y=\"182\" text-anchor=\"middle\" class=\"himmel\">&#8593; ")
                .Append(auf.ToString("HH:mm", CultureInfo.InvariantCulture)).Append("  &#8595; ")
                .Append(unter.ToString("HH:mm", CultureInfo.InvariantCulture)).Append("</text>");
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    private static (double X, double Y) Punkt(double mitte, double abstand, double grad)
    {
        var bogen = (grad - 90) * Math.PI / 180.0;
        return (mitte + abstand * Math.Cos(bogen), mitte + abstand * Math.Sin(bogen));
    }

    // ---- Text --------------------------------------------------------------

    private static string Windtext(Anlage anlage, Wetterlage wetter, DateTime jetzt, out bool alarm)
    {
        alarm = false;
        var bit = wetter.Windalarm;
        var bitFrisch = bit is not null && bit.Value.IstFrisch(jetzt, anlage.HoechstalterWind);
        var wert = wetter.Wind;
        var wertFrisch = wert is not null && wert.Value.IstFrisch(jetzt, anlage.HoechstalterWind);

        if (!bitFrisch && !wertFrisch) return "kein Wert";

        alarm = bitFrisch && bit!.Value.Wert > 0.5;
        if (!alarm && wertFrisch && wert!.Value.Wert >= anlage.WindgrenzeAusgabe) alarm = true;

        var text = wertFrisch ? Zahl(wert!.Value.Wert) + " m/s" : alarm ? "Alarm" : "ruhig";
        return alarm && wertFrisch ? text + " Alarm" : text;
    }

    private static string Regentext(Anlage anlage, Wetterlage wetter, DateTime jetzt, out bool nass)
    {
        nass = false;
        if (wetter.Regen is not { } regen || !regen.IstFrisch(jetzt, anlage.HoechstalterRegen)) return "kein Wert";
        nass = regen.Wert > 0.5;
        return nass ? "es regnet" : "trocken";
    }

    private static string Ausgabetext(Anlage anlage, Wintergartendienst dienst)
    {
        if (anlage.AdresseWindausgabe.Trim().Length == 0 && anlage.AdresseRegenausgabe.Trim().Length == 0)
        {
            return "nicht eingerichtet";
        }
        var lage = dienst.Sicherheitslage;
        return lage.Wind && lage.Regen ? "Wind + Regen"
            : lage.Wind ? "Windalarm"
            : lage.Regen ? "Regen"
            : "ruhig";
    }

    private static string Grad(Messwert? wert, TimeSpan hoechstalter, DateTime jetzt) =>
        wert is { } messwert && messwert.IstFrisch(jetzt, hoechstalter)
            ? Zahl(messwert.Wert) + " &deg;C"
            : "kein Wert";

    private static string Lux(Messwert? wert, TimeSpan hoechstalter, DateTime jetzt)
    {
        if (wert is not { } messwert || !messwert.IstFrisch(jetzt, hoechstalter)) return "kein Wert";
        return messwert.Wert >= 1000
            ? Zahl(messwert.Wert / 1000) + " kLux"
            : Zahl(messwert.Wert) + " Lux";
    }

    private static double? Position(Wintergartendienst dienst, Motor motor)
    {
        var roh = dienst.Roh(motor.AdressePositionStatus.Length > 0
            ? motor.AdressePositionStatus
            : motor.AdressePosition);
        return roh is null ? null : Wintergartendienst.Zahl("5.001", roh);
    }

    private static string Klasse(Stufe stufe) => stufe switch
    {
        Stufe.Wind or Stufe.Regen or Stufe.Frost => "rot",
        Stufe.Beschattung or Stufe.Lueftung => "gruen",
        _ => "grau",
    };

    private static string Stufentext(Stufe stufe) => stufe switch
    {
        Stufe.Wind => "Windschutz: ",
        Stufe.Regen => "Regenschutz: ",
        Stufe.Frost => "Frostschutz: ",
        Stufe.Beschattung => "Beschattung: ",
        Stufe.Lueftung => "Lueftung: ",
        Stufe.Zeit => "Zeitschaltuhr: ",
        Stufe.Hand => "Hand: ",
        _ => "",
    };

    private static string Zahl(double wert) => wert.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>
    /// Text, der in HTML gehoert, muss entschaerft werden. Ein Antriebsname
    /// darf ein spitzes Klammerzeichen enthalten, ohne die Seite zu zerlegen.
    /// </summary>
    /// <summary>
    /// Die gelbe Sonne als SVG fuer die Adresszeile - dieselbe wie auf der
    /// EXE, mit demselben Verlauf und demselben Dunkelbraun.
    ///
    /// Als Datenadresse eingebettet und nicht nachgeladen: so fehlt sie auch
    /// dann nicht, wenn der Wintergarten kein Internet hat, und der Browser
    /// fragt nicht bei jedem Neuladen vergebens nach favicon.ico. Die
    /// spitzen Klammern und das Rautezeichen muessen dafuer umschrieben sein
    /// - sonst bricht die Datenadresse mitten im Attribut ab.
    /// </summary>
    public const string Sonnensymbol =
        "%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'%3E"
        + "%3Cdefs%3E%3ClinearGradient id='g' x1='0' y1='0' x2='1' y2='1'%3E"
        + "%3Cstop offset='0' stop-color='%23FFD84D'/%3E"
        + "%3Cstop offset='1' stop-color='%23F09200'/%3E%3C/linearGradient%3E%3C/defs%3E"
        + "%3Crect width='24' height='24' rx='5' fill='url(%23g)'/%3E"
        + "%3Cg fill='none' stroke='%234A2C00' stroke-width='1.7' stroke-linecap='round'%3E"
        + "%3Ccircle cx='12' cy='12' r='4.4'/%3E"
        + "%3Cpath d='M12 3.4 L12 5.6 M12 18.4 L12 20.6 M3.4 12 L5.6 12 M18.4 12 L20.6 12"
        + " M6 6 L7.6 7.6 M16.4 16.4 L18 18 M18 6 L16.4 7.6 M7.6 16.4 L6 18'/%3E"
        + "%3C/g%3E%3C/svg%3E";

    public static string Sicher(string? text) => (text ?? "")
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    private const string Stil = """
        :root{--flaeche:#f7f7f6;--karte:#fff;--linie:#dcdcdc;--schrift:#1a1a1c;
              --leise:#65656d;--blass:#8b8b93;--gruen:#4d7616;--rot:#cf222e;--ruhe:#e7e7e4}
        *{box-sizing:border-box}
        body{margin:0;padding:16px;background:var(--flaeche);color:var(--schrift);
             font-family:Segoe UI,system-ui,-apple-system,sans-serif;font-size:15px}
        h1{margin:0;font-size:26px}
        h2{margin:16px 0 6px;font-size:13px;text-transform:uppercase;letter-spacing:.06em;
           color:var(--leise)}
        p{margin:6px 0}
        .klein{font-size:12px;color:var(--leise)}
        .band{background:var(--karte);border:1px solid var(--linie);border-radius:6px;
              padding:14px 16px;margin-bottom:14px}
        .band.warn{border:2px solid var(--rot)} .band.warn h1{color:var(--rot)}
        .band.aktiv{border:2px solid var(--gruen)} .band.aktiv h1{color:var(--gruen)}
        .band.ruhig h1{color:var(--leise)}
        .reihe{display:flex;flex-wrap:wrap;gap:10px;margin-bottom:14px}
        .leuchte{background:var(--karte);border:1px solid var(--linie);border-radius:6px;
                 padding:10px 14px;min-width:128px}
        .leuchte.alarm{border:2px solid var(--rot)} .leuchte.alarm .wert{color:var(--rot)}
        .leuchte.blind .wert{color:var(--blass)} .leuchte.blind .sinnbild{color:var(--blass)}
        .wert{font-size:19px}
        .spalten{display:flex;flex-wrap:wrap;gap:16px;align-items:flex-start}
        .links{flex:0 0 320px} .rechts{flex:1 1 460px}
        .karte{background:var(--karte);border:1px solid var(--linie);border-radius:6px;padding:12px}
        .kompass{width:100%;height:auto}
        .himmel{font-size:11px;fill:var(--leise)}
        .mitte{font-size:11px;fill:var(--leise)}
        .mittegross{font-size:14px;font-weight:600;fill:var(--schrift)}
        .vor{margin:0 0 8px}
        .kacheln{display:flex;flex-wrap:wrap;gap:12px}
        .kachel{background:var(--karte);border:1px solid var(--linie);border-radius:6px;
                padding:12px;width:250px}
        .kachel.alarm{border:2px solid var(--rot)}
        .name{font-size:15px;font-weight:600}
        .position{font-size:26px;margin:6px 0}
        .grund{font-size:12px;min-height:32px}
        .grund.rot{color:var(--rot)} .grund.gruen{color:var(--gruen)} .grund.grau{color:var(--leise)}
        .knoepfe{display:flex;gap:6px;margin-top:8px}
        button{flex:1;padding:8px 0;font-size:14px;background:var(--karte);color:var(--schrift);
               border:1px solid var(--linie);border-radius:4px;cursor:pointer}
        button:active{background:var(--ruhe)}
        .fuss{margin-top:18px;font-size:11px;color:var(--blass)}
        .kopfzeile{display:flex;align-items:center;gap:9px;margin-bottom:10px}
        .kopfzeile .sinnbild{color:#e0a100}
        .marke{font-size:16px;font-weight:600;letter-spacing:.01em}
        .menue{display:flex;flex-wrap:wrap;gap:2px;margin-bottom:14px;
               border-bottom:1px solid var(--linie)}
        .menue a{padding:9px 16px;text-decoration:none;color:var(--leise);font-size:14px;
                 border:1px solid transparent;border-bottom:none;border-radius:6px 6px 0 0;
                 margin-bottom:-1px}
        .menue a:hover{color:var(--schrift);background:var(--karte)}
        .menue a.hier{background:var(--karte);border-color:var(--linie);color:var(--schrift);
                      font-weight:600}
        .regel{width:290px}
        .regel.aus{opacity:.55}
        .tut{font-size:12px;color:var(--gruen);margin:8px 0 0}
        .regel.aus .tut{color:var(--blass)}
        .regel form{margin:8px 0 0}
        .gut{color:var(--gruen)}
        .stoerung{color:var(--rot);font-weight:600}
        .untermenue{display:flex;flex-wrap:wrap;gap:14px;margin:0 0 14px}
        .untermenue a{font-size:13px;color:var(--leise);text-decoration:none;padding-bottom:3px;
                      border-bottom:2px solid transparent}
        .untermenue a:hover{color:var(--schrift)}
        .untermenue a.hier{color:var(--schrift);font-weight:600;border-bottom-color:var(--gruen)}
        .halb{flex:1 1 420px;min-width:320px}
        select{padding:5px 7px;font-size:14px;border:1px solid var(--linie);border-radius:4px;
               background:var(--flaeche);color:var(--schrift)}
        .felder input[type=checkbox]{width:auto}
        .liste tr.aus{opacity:.5}
        .liste tr.problem td{color:var(--rot)}
        .liste form{display:inline;margin:0}
        .liste button{padding:3px 10px;font-size:12px;width:auto}
        .liste a{color:var(--schrift)}
        .sinnbild{fill:none;stroke:currentColor;stroke-width:1.6;stroke-linecap:round;
                  stroke-linejoin:round;vertical-align:-.15em}
        .leuchte .sinnbild{display:block;color:var(--leise);margin-bottom:6px}
        .leuchte.alarm .sinnbild{color:var(--rot)}
        .kopf{display:flex;gap:9px;align-items:flex-start;color:var(--leise)}
        .kopf .name{color:var(--schrift)}
        .grund .sinnbild{stroke-width:2}
        .blass{color:var(--blass);font-style:normal}
        .zeitraum a{margin-right:12px;font-size:13px;color:var(--leise)}
        .zeitraum a.hier{color:var(--schrift);font-weight:600}
        .kurve{width:100%;height:180px}
        .linie{fill:none;stroke-width:1.6}
        .linie.eins{stroke:var(--gruen)} .linie.zwei{stroke:#2a6099}
        .eingriff{stroke-width:1;opacity:.5}
        .eingriff.rot{stroke:var(--rot)} .eingriff.gruen{stroke:var(--gruen)}
        .eingriff.grau{stroke:var(--blass)}
        .punkt{display:inline-block;width:9px;height:9px;border-radius:50%;margin:0 4px 0 10px}
        .punkt.eins{background:var(--gruen)} .punkt.zwei{background:#2a6099}
        table{border-collapse:collapse;width:100%}
        .liste td,.liste th{padding:4px 8px 4px 0;text-align:left;vertical-align:top;
                            border-bottom:1px solid var(--linie)}
        .liste th{font-size:11px;text-transform:uppercase;letter-spacing:.05em;color:var(--leise);
                  font-weight:600}
        .felder td{padding:3px 8px 3px 0}
        .felder input{width:110px;padding:5px 7px;font-size:14px;border:1px solid var(--linie);
                      border-radius:4px;background:var(--flaeche);color:var(--schrift)}
        .karte button{margin-top:10px;width:auto;padding:8px 18px}
        @media (max-width:640px){.links,.rechts{flex:1 1 100%}.kachel{width:100%}}
        """;
}
