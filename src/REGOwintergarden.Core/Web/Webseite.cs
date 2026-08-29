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
    public static string Bauen(Wintergartendienst dienst, DateTime jetzt)
    {
        var anlage = dienst.Anlage;
        var wetter = dienst.Wetter();
        var lagen = dienst.Lagen;
        var sonne = dienst.Sonne;
        var (ueberschrift, ton) = Lagebericht.Ueberschrift(anlage, lagen);

        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"de\"><head><meta charset=\"utf-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.Append("<title>").Append(Sicher(anlage.Name)).Append("</title>");

        // Alle dreissig Sekunden neu laden. Kein Skript: was nicht da ist,
        // kann nicht kaputtgehen, und auf einem Tablet an der Wand reicht
        // das.
        html.Append("<meta http-equiv=\"refresh\" content=\"30\">");
        html.Append("<style>").Append(Stil).Append("</style></head><body>");

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

        html.Append("<p class=\"fuss\">REGOwintergarden ")
            .Append(Sicher(Programmstand.Version))
            .Append(" &middot; Seite l&auml;dt sich alle 30 Sekunden neu</p>");
        html.Append("</body></html>");
        return html.ToString();
    }

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
        @media (max-width:640px){.links,.rechts{flex:1 1 100%}.kachel{width:100%}}
        """;
}
