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
        html.Append("<title>").Append(Sicher(anlage.Name)).Append("</title>");

        // Kein Skript: was nicht da ist, kann nicht kaputtgehen, und auf einem
        // Tablet an der Wand reicht ein Neuladen.
        if (neuladen > 0)
        {
            html.Append("<meta http-equiv=\"refresh\" content=\"")
                .Append(neuladen.ToString(CultureInfo.InvariantCulture)).Append("\">");
        }
        html.Append("<style>").Append(Stil).Append("</style></head><body>");

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
        html.Append("<div class=\"band ").Append(ton switch
        {
            Lagebericht.Ton.Warnung => "warn",
            Lagebericht.Ton.Taetig => "aktiv",
            _ => "ruhig",
        }).Append("\">");
        html.Append("<h1>").Append(Sicher(ueberschrift)).Append("</h1>");
        html.Append("<p>").Append(Sicher(Lagebericht.Erklaerung(anlage, lagen, wetter, sonne, jetzt)))
            .Append("</p>");
        html.Append("<p class=\"klein\">").Append(Sicher(anlage.Name)).Append(" &middot; ")
            .Append(lagen.Count.ToString(CultureInfo.InvariantCulture)).Append(" Antriebe &middot; ")
            .Append(dienst.Stand == Busstand.Verbunden ? "mit dem Bus verbunden" : "keine Busverbindung")
            .Append(" &middot; ").Append(jetzt.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture))
            .Append("</p></div>");

        // ---- Laeuft der Dienst ----
        //
        // Die Frage, die man einem Geraet ohne Bildschirm wirklich stellt.
        // Nicht „laeuft der Prozess" - der laeuft auch, wenn die Schleife
        // haengt -, sondern „wann hat er zuletzt gerechnet".
        html.Append("<div class=\"band ").Append(Dienstton(dienst, jetzt)).Append("\">");
        html.Append("<p><b>").Append(Sicher(Dienstzeile(dienst, jetzt))).Append("</b></p>");
        html.Append("<p class=\"klein\">L&auml;uft seit ")
            .Append(dienst.Gestartet.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture))
            .Append(" &middot; ").Append(Sicher(Dauer(jetzt - dienst.Gestartet)))
            .Append(" &middot; Fassung ").Append(Sicher(Programmstand.Version))
            .Append("</p></div>");

        // ---- Wetterleuchten ----
        html.Append("<div class=\"reihe\">");
        Leuchte(html, "Wind", Windtext(anlage, wetter, jetzt, out var windAlarm), windAlarm);
        Leuchte(html, "Regen", Regentext(anlage, wetter, jetzt, out var nass), nass);
        Leuchte(html, "draussen", Grad(wetter.Aussen, anlage.HoechstalterTemperatur, jetzt), false);
        Leuchte(html, "drinnen", Grad(wetter.Innen, anlage.HoechstalterTemperatur, jetzt), false);
        Leuchte(html, "Helligkeit", Lux(wetter.HellsteRichtung(), anlage.HoechstalterHelligkeit, jetzt), false);
        Leuchte(html, "an die Aktoren", Ausgabetext(anlage, dienst), dienst.Sicherheitslage.Alarm);
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
            anlage.AutomatikAktiv ? "l&auml;uft" : "steht");

        Regelkarte(html, "wind", "Windschutz", anlage.WindschutzAktiv,
            "F&auml;hrt Markisen ein und Fenster zu, sobald die Station Windalarm meldet oder die "
            + "Grenze &uuml;berschritten ist. Danach l&auml;uft der Schutz noch "
            + Zahl(anlage.WindNachlaufMinuten) + " Minuten nach.",
            Wirkt(lagen, Stufe.Wind));

        Regelkarte(html, "regen", "Regenschutz", anlage.RegenschutzAktiv,
            "Dasselbe bei Regen - f&uuml;r alles, was Regenschutz eingetragen hat. Nachlauf "
            + Zahl(anlage.RegenNachlaufMinuten) + " Minuten.",
            Wirkt(lagen, Stufe.Regen));

        Regelkarte(html, "frost", "Frostschutz", anlage.FrostschutzAktiv,
            "Unter der Frostgrenze bleibt die Markise eingefahren. Eine vereiste Markise "
            + "auszufahren kostet das Tuch.",
            Wirkt(lagen, Stufe.Frost));

        Regelkarte(html, "beschattung", "Beschattung", anlage.BeschattungAktiv,
            "Beschattet, wenn die Sonne auf der Fl&auml;che steht und es heller ist als "
            + Zahl(anlage.Helligkeitsschwelle) + " Lux - nach "
            + Zahl(anlage.EinschaltverzoegerungMinuten) + " Minuten, damit eine einzelne Wolke "
            + "nichts ausl&ouml;st.",
            Wirkt(lagen, Stufe.Beschattung));

        Regelkarte(html, "lueftung", "L&uuml;ftung", anlage.LueftungAktiv,
            "&Ouml;ffnet die Fenster ab " + Zahl(anlage.LueftungAb) + " Grad drinnen, solange es "
            + "draussen mindestens " + Zahl(anlage.LueftungUnterschied) + " Grad k&uuml;hler ist. "
            + "W&auml;re es draussen w&auml;rmer, brächte L&uuml;ften nur mehr Hitze herein.",
            Wirkt(lagen, Stufe.Lueftung));

        Regelkarte(html, "uhr", "Zeitschaltuhr", anlage.ZeitschaltuhrAktiv,
            anlage.Schaltzeiten.Count.ToString(CultureInfo.InvariantCulture)
            + " Schaltzeiten, feste und solche mit Bezug auf Sonnenauf- und -untergang.",
            Wirkt(lagen, Stufe.Zeit));

        Regelkarte(html, "waermegewinn", "W&auml;rmegewinn", anlage.WaermegewinnAktiv,
            "An kalten Tagen wird nicht beschattet, solange es drinnen k&uuml;hl ist: unter "
            + Zahl(anlage.WaermegewinnAussen) + " Grad draussen und unter "
            + Zahl(anlage.WaermegewinnInnen) + " Grad drinnen. Ein Wintergarten ist im Winter "
            + "eine Heizung - wer im Januar die Markise ausf&auml;hrt, wirft die einzige "
            + "kostenlose W&auml;rme des Tages weg.",
            anlage.WaermegewinnAktiv ? "wacht mit" : "aus");

        Regelkarte(html, "hitzevorsorge", "Hitzevorsorge", anlage.HitzevorsorgeAktiv,
            "Sagt die Vorhersage &uuml;ber " + Zahl(anlage.HitzevorsorgeAb) + " Grad an, wird "
            + "fr&uuml;her beschattet. Wer erst beschattet, wenn es drinnen warm ist, kommt zu "
            + "sp&auml;t - die W&auml;rme steckt dann in Boden und M&ouml;beln.",
            anlage.Vorhersage is null ? "keine Vorhersage da" : Sicher(anlage.Vorhersage.ToString()));

        Regelkarte(html, "nachtauskuehlung", "Nachtausk&uuml;hlung", anlage.NachtauskuehlungAktiv,
            "Nach einem Tag &uuml;ber " + Zahl(anlage.NachtauskuehlungAb) + " Grad werden die "
            + "Fenster nachts ge&ouml;ffnet, bis drinnen " + Zahl(anlage.NachtauskuehlungZiel)
            + " Grad erreicht sind. Die wirksamste K&uuml;hlung, die ein Wintergarten hat - und "
            + "sie kostet nichts.",
            anlage.NachtauskuehlungAktiv ? "wacht mit" : "aus");

        Regelkarte(html, "vorhersage", "Wettervorhersage", anlage.VorhersageAktiv,
            "Holt zweimal je Stunde die Vorhersage f&uuml;r " + Sicher(anlage.Ort)
            + " von Open-Meteo. Ohne Schl&uuml;ssel, ohne Anmeldung.",
            anlage.Vorhersage is null ? "noch nichts geholt" : Sicher(anlage.Vorhersage.ToString()));
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
        string erklaerung, string tut)
    {
        html.Append("<div class=\"kachel regel").Append(an ? "" : " aus").Append("\">");
        html.Append("<div class=\"name\">").Append(name).Append("</div>");
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
                    .Append("</td><td class=\"klein\">").Append(Sicher(Stufentext(e.Stufe)))
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
    /// Die Seite fuer den Errichter: Anschluss, Standort, Grenzen, Antriebe.
    ///
    /// Geaendert wird hier, was sich mit einer Zahl aendern laesst. Antriebe
    /// anzulegen und Gruppenadressen einzutragen bleibt beim Windows-Fenster
    /// oder in der einstellungen.json - dafuer waere ein Formular im Browser
    /// das schlechtere Werkzeug.
    /// </summary>
    public static string Konfigseite(Wintergartendienst dienst, DateTime jetzt, string meldung)
    {
        var anlage = dienst.Anlage;

        var html = new StringBuilder();
        Rahmen(html, anlage, "/konfig", 0);

        html.Append("<div class=\"band ruhig\"><h1>Konfiguration</h1>");
        html.Append("<p>Anlage <b>").Append(Sicher(anlage.Name)).Append("</b> in ")
            .Append(Sicher(anlage.Ort)).Append(" &middot; ")
            .Append(anlage.Motoren.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" Antriebe &middot; ").Append(dienst.Stand == Busstand.Verbunden
                ? "mit dem Bus verbunden"
                : "keine Busverbindung").Append("</p>");
        if (meldung.Length > 0) html.Append("<p class=\"gut\">").Append(Sicher(meldung)).Append("</p>");
        html.Append("</div>");

        html.Append("<div class=\"spalten\"><div class=\"rechts\">");
        html.Append("<h2>Grenzen und Zeiten</h2><div class=\"karte\">");
        html.Append("<form method=\"post\" action=\"/einstellen\"><table class=\"felder\">");
        Feld(html, "helligkeit", "Beschatten ab", anlage.Helligkeitsschwelle, "Lux");
        Feld(html, "ein", "Einschaltverz&ouml;gerung", anlage.EinschaltverzoegerungMinuten, "min");
        Feld(html, "aus", "Ausschaltverz&ouml;gerung", anlage.AusschaltverzoegerungMinuten, "min");
        Feld(html, "lueftungab", "L&uuml;ften ab drinnen", anlage.LueftungAb, "&deg;C");
        Feld(html, "lueftungspos", "L&uuml;ftungsposition", anlage.Lueftungsposition, "%");
        Feld(html, "windausgabe", "Windgrenze f&uuml;r die Ausgabe", anlage.WindgrenzeAusgabe, "m/s");
        Feld(html, "ausgabetakt", "Ausgabetakt an die Aktoren", anlage.AusgabetaktSekunden, "s");
        Feld(html, "handsperre", "Handsperre", anlage.HandsperreMinuten, "min");
        Feld(html, "takt", "Rechentakt", anlage.TaktSekunden, "s");
        html.Append("</table><button type=\"submit\">&Uuml;bernehmen</button></form>");
        html.Append("<p class=\"klein\">Gespeichert wird sofort in die einstellungen.json. "
                    + "Ein Neustart des Dienstes ist nicht n&ouml;tig.</p></div>");

        html.Append("<h2>Antriebe</h2><div class=\"karte\"><table class=\"liste\">");
        html.Append("<tr><th>Name</th><th>Art</th><th>Richtung</th><th>Fahren</th>"
                    + "<th>Position</th><th>R&uuml;ckmeldung</th></tr>");
        foreach (var motor in anlage.Motoren)
        {
            html.Append("<tr><td>").Append(Sicher(motor.Name))
                .Append("</td><td class=\"klein\">").Append(Sicher(motor.Art.ToString()))
                .Append("</td><td class=\"klein\">").Append(Sicher(Motor.Richtungsname(motor.Ausrichtung)))
                .Append(' ').Append(Zahl(motor.Ausrichtung)).Append("&deg;")
                .Append("</td><td class=\"klein\">").Append(Adresse(motor.AdresseFahren))
                .Append("</td><td class=\"klein\">").Append(Adresse(motor.AdressePosition))
                .Append("</td><td class=\"klein\">").Append(Adresse(motor.AdressePositionStatus))
                .Append("</td></tr>");
        }
        html.Append("</table></div></div>");

        html.Append("<div class=\"links\"><h2>Anschluss</h2><div class=\"karte\">");
        Zeile(html, "Gateway", Adresse(dienst.Einstellungen.Gateway));
        Zeile(html, "Standort", Sicher(anlage.Ort) + " &middot; " + Zahl(anlage.Breite) + " / "
                                + Zahl(anlage.Laenge));
        Zeile(html, "Sonne", Sicher(dienst.Sonnenquelle));
        html.Append("</div>");

        html.Append("<h2>Adressen der Station</h2><div class=\"karte\"><table class=\"liste\">");
        foreach (var (name, adresse) in new[]
                 {
                     ("Windalarm", anlage.AdresseWindalarm), ("Windgeschwindigkeit", anlage.AdresseWind),
                     ("Regen", anlage.AdresseRegen), ("draussen", anlage.AdresseAussen),
                     ("drinnen", anlage.AdresseInnen), ("hell Ost", anlage.AdresseHellOst),
                     ("hell S&uuml;d", anlage.AdresseHellSued), ("hell West", anlage.AdresseHellWest),
                     ("Azimut", anlage.AdresseAzimut), ("Elevation", anlage.AdresseElevation),
                 })
        {
            html.Append("<tr><td class=\"klein\">").Append(name).Append("</td><td class=\"klein\">")
                .Append(Adresse(adresse)).Append("</td></tr>");
        }
        html.Append("</table></div>");

        html.Append("<h2>Ausgabe an die Aktoren</h2><div class=\"karte\"><table class=\"liste\">");
        html.Append("<tr><td class=\"klein\">Wind</td><td class=\"klein\">")
            .Append(Adresse(anlage.AdresseWindausgabe)).Append("</td></tr>");
        html.Append("<tr><td class=\"klein\">Regen</td><td class=\"klein\">")
            .Append(Adresse(anlage.AdresseRegenausgabe)).Append("</td></tr>");
        html.Append("</table><p class=\"klein\">Zyklisch alle ").Append(Zahl(anlage.AusgabetaktSekunden))
            .Append(" Sekunden - das ist das Lebenszeichen, an dem die Aktoren einen Ausfall "
                    + "dieses Programms erkennen.</p></div></div></div>");

        Fuss(html, "Antriebe anlegen und Gruppenadressen eintragen: im Windows-Fenster oder in "
                   + "der einstellungen.json");
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

    private static void Zeile(StringBuilder html, string name, string wert) =>
        html.Append("<p><span class=\"klein\">").Append(name).Append("</span><br>").Append(wert)
            .Append("</p>");

    private static string Adresse(string adresse) =>
        adresse.Trim().Length == 0 ? "<i class=\"blass\">nicht eingetragen</i>" : Sicher(adresse);

    // ---- Bausteine ---------------------------------------------------------

    private static void Leuchte(StringBuilder html, string name, string wert, bool alarm)
    {
        html.Append("<div class=\"leuchte").Append(alarm ? " alarm" : "").Append("\">");
        html.Append("<div class=\"wert\">").Append(Sicher(wert)).Append("</div>");
        html.Append("<div class=\"klein\">").Append(Sicher(name)).Append("</div></div>");
    }

    private static void Kachel(StringBuilder html, Wintergartendienst dienst, Lage lage)
    {
        var motor = lage.Motor;
        var stand = Position(dienst, motor);

        html.Append("<div class=\"kachel").Append(lage.Stufe >= Stufe.Frost ? " alarm" : "").Append("\">");
        html.Append("<div class=\"name\">").Append(Sicher(motor.Name)).Append("</div>");
        html.Append("<div class=\"klein\">").Append(Sicher(motor.Art.ToString())).Append(" &middot; ")
            .Append(Sicher(motor.Richtung)).Append(" ")
            .Append(Math.Round(motor.Ausrichtung).ToString("0", CultureInfo.InvariantCulture))
            .Append("&deg;</div>");
        html.Append("<div class=\"position\">")
            .Append(stand is null ? "&mdash;" : Math.Round(stand.Value).ToString("0", CultureInfo.InvariantCulture) + " %")
            .Append("</div>");
        html.Append("<div class=\"grund ").Append(Klasse(lage.Stufe)).Append("\">")
            .Append(Sicher(Stufentext(lage.Stufe) + lage.Grund)).Append("</div>");

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
