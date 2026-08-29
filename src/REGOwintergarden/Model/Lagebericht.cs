using System;
using System.Collections.Generic;
using System.Globalization;

namespace REGOwintergarden.Model;

/// <summary>Was als Naechstes ansteht - Zeitpunkt, Antrieb, Grund.</summary>
public sealed record Vorschau(DateTime Wann, string Antrieb, string Was)
{
    public string Uhrzeit => Wann.ToString("HH:mm", CultureInfo.CurrentCulture);

    /// <summary>„in 12 min" - naeher am Denken als eine Uhrzeit.</summary>
    public string In(DateTime jetzt)
    {
        var spanne = Wann - jetzt;
        if (spanne <= TimeSpan.Zero) return "gleich";
        if (spanne.TotalMinutes < 1) return "in " + Math.Ceiling(spanne.TotalSeconds)
            .ToString("0", CultureInfo.CurrentCulture) + " s";
        if (spanne.TotalHours < 1) return "in " + Math.Ceiling(spanne.TotalMinutes)
            .ToString("0", CultureInfo.CurrentCulture) + " min";
        return "um " + Uhrzeit;
    }
}

/// <summary>
/// Der Lagebericht in Alltagssprache.
///
/// <b>Warum eine eigene Klasse:</b> die Regelmaschine liefert Stufen und
/// Gruende - richtig, aber technisch. Wer den Wintergarten nur benutzt, will
/// einen Satz: „Alles ruhig" oder „Windalarm - die Markisen sind
/// eingefahren". Diesen Satz zu bilden ist eine Entscheidung fuer sich, und
/// sie gehoert nicht ins Fenster, sondern dorthin, wo man sie pruefen kann.
///
/// Der zweite Teil ist der wichtigere: <see cref="Naechstes"/> sagt, was das
/// Programm als Naechstes vorhat. Eine Steuerung, die nur ihren Zustand
/// zeigt, wirkt willkuerlich; eine, die ihre Absicht zeigt, wird
/// nachvollziehbar.
/// </summary>
public static class Lagebericht
{
    /// <summary>Wie ernst die Lage ist - fuer die Farbe des Kopfbandes.</summary>
    public enum Ton
    {
        Ruhig,
        Taetig,
        Warnung,
    }

    /// <summary>Die Ueberschrift: ein Zustand in zwei bis vier Worten.</summary>
    public static (string Text, Ton Ton) Ueberschrift(Anlage anlage, IReadOnlyList<Lage> lagen)
    {
        if (!anlage.AutomatikAktiv) return ("Automatik ist ausgeschaltet", Ton.Warnung);
        if (lagen.Count == 0) return ("Noch kein Antrieb eingerichtet", Ton.Warnung);

        var hoechste = Stufe.Frei;
        foreach (var lage in lagen)
        {
            if (lage.Stufe > hoechste) hoechste = lage.Stufe;
        }

        return hoechste switch
        {
            Stufe.Wind => ("Windschutz aktiv", Ton.Warnung),
            Stufe.Regen => ("Regenschutz aktiv", Ton.Warnung),
            Stufe.Frost => ("Frostschutz aktiv", Ton.Warnung),
            Stufe.Hand => ("Von Hand bedient", Ton.Taetig),
            Stufe.Beschattung => ("Beschattung laeuft", Ton.Taetig),
            Stufe.Lueftung => ("Es wird gelueftet", Ton.Taetig),
            _ => ("Alles ruhig", Ton.Ruhig),
        };
    }

    /// <summary>
    /// Ein erklaerender Satz zur Ueberschrift - was das fuer den Wintergarten
    /// heisst.
    /// </summary>
    public static string Erklaerung(Anlage anlage, IReadOnlyList<Lage> lagen, Wetterlage wetter,
        Sonnenstand sonne, DateTime jetzt)
    {
        if (!anlage.AutomatikAktiv)
        {
            return "Es wird nichts von selbst gefahren. Auch Wind- und Regenschutz sind damit aus.";
        }
        if (lagen.Count == 0)
        {
            return "Unter Konfiguration die Antriebe anlegen und ihre Gruppenadressen eintragen.";
        }

        var (betroffen, hoechste) = Zaehlen(lagen);
        var was = hoechste switch
        {
            Stufe.Wind => betroffen + " von " + lagen.Count + " Antrieben sind in Sicherheit gefahren.",
            Stufe.Regen => betroffen + " von " + lagen.Count + " Antrieben sind wegen Regen zu.",
            Stufe.Frost => betroffen + " von " + lagen.Count + " Antrieben sind wegen Frost zu.",
            Stufe.Hand => betroffen + " Antrieb(e) wurden von Hand gefahren - dort haelt sich die "
                          + "Automatik zurueck.",
            Stufe.Beschattung => betroffen + " von " + lagen.Count + " Antrieben beschatten gerade.",
            Stufe.Lueftung => betroffen + " Fenster steht zum Lueften offen.",
            _ => "Kein Antrieb muss gerade etwas tun.",
        };

        return was + " " + Sonnensatz(sonne, jetzt) + Wettersatz(anlage, wetter, jetzt);
    }

    private static (int Betroffen, Stufe Hoechste) Zaehlen(IReadOnlyList<Lage> lagen)
    {
        var hoechste = Stufe.Frei;
        foreach (var lage in lagen)
        {
            if (lage.Stufe > hoechste) hoechste = lage.Stufe;
        }

        var betroffen = 0;
        foreach (var lage in lagen)
        {
            if (lage.Stufe == hoechste) betroffen++;
        }
        return (betroffen, hoechste);
    }

    private static string Sonnensatz(Sonnenstand sonne, DateTime jetzt)
    {
        if (!sonne.Tag)
        {
            return sonne.Aufgang is { } auf && auf > jetzt
                ? "Die Sonne geht um " + auf.ToString("HH:mm", CultureInfo.CurrentCulture) + " auf."
                : "Die Sonne ist unter.";
        }

        var richtung = Motor.Richtungsname(sonne.Azimut);
        var satz = "Die Sonne steht im " + Langform(richtung) + ", "
                   + Math.Round(sonne.Elevation).ToString("0", CultureInfo.CurrentCulture)
                   + " Grad ueber dem Horizont";
        return sonne.Untergang is { } unter
            ? satz + "; Untergang um " + unter.ToString("HH:mm", CultureInfo.CurrentCulture) + "."
            : satz + ".";
    }

    private static string Wettersatz(Anlage anlage, Wetterlage wetter, DateTime jetzt)
    {
        // Was fehlt, ist wichtiger als was da ist: eine stumme Wetterstation
        // gehoert auf die erste Seite und nicht ins Protokoll.
        var fehlt = new List<string>();
        if (wetter.Wind is not { } wind || !wind.IstFrisch(jetzt, anlage.HoechstalterWind)) fehlt.Add("Wind");
        if (wetter.Regen is not { } regen || !regen.IstFrisch(jetzt, anlage.HoechstalterRegen)) fehlt.Add("Regen");
        if (wetter.HellsteRichtung() is not { } hell || !hell.IstFrisch(jetzt, anlage.HoechstalterHelligkeit))
        {
            fehlt.Add("Helligkeit");
        }

        if (fehlt.Count == 0) return "";
        return " Es fehlt ein aktueller Wert fuer " + string.Join(" und ", fehlt)
               + " - solange faehrt die Anlage vorsichtig.";
    }

    private static string Langform(string kuerzel) => kuerzel switch
    {
        "N" => "Norden",
        "O" => "Osten",
        "S" => "Sueden",
        "W" => "Westen",
        "NO" => "Nordosten",
        "SO" => "Suedosten",
        "SW" => "Suedwesten",
        "NW" => "Nordwesten",
        _ => kuerzel,
    };

    /// <summary>
    /// Was als Naechstes von selbst passiert - aus den laufenden
    /// Verzoegerungen und aus der Zeitschaltuhr, nach Zeit sortiert.
    /// </summary>
    public static IReadOnlyList<Vorschau> Naechstes(Anlage anlage, IReadOnlyList<Lage> lagen,
        Func<DateTime, Sonnenstand> sonne, DateTime jetzt, int hoechstens = 5)
    {
        var liste = new List<Vorschau>();

        foreach (var lage in lagen)
        {
            if (lage.Naechstes is not { } wann || wann <= jetzt) continue;
            liste.Add(new Vorschau(wann, lage.Motor.Name, Absicht(lage)));
        }

        // Die naechste Schaltzeit dazu - sie ist der einzige Punkt, an dem
        // etwas ohne Wetteraenderung passiert.
        var naechsteZeit = NaechsteSchaltzeit(anlage, sonne, jetzt);
        if (naechsteZeit is not null) liste.Add(naechsteZeit);

        liste.Sort((a, b) => a.Wann.CompareTo(b.Wann));
        return liste.Count <= hoechstens ? liste : liste.GetRange(0, hoechstens);
    }

    private static string Absicht(Lage lage) => lage.Stufe switch
    {
        Stufe.Wind => "Windalarm endet, danach entscheidet wieder die Automatik",
        Stufe.Regen => "Regenschutz endet, danach entscheidet wieder die Automatik",
        Stufe.Hand => "Handbedienung endet, danach uebernimmt die Automatik",
        Stufe.Beschattung when lage.Ziel is > 0 => "oeffnet wieder",
        Stufe.Frei => "faehrt zum Beschatten aus",
        _ => "aendert sich",
    };

    private static Vorschau? NaechsteSchaltzeit(Anlage anlage, Func<DateTime, Sonnenstand> sonne, DateTime jetzt)
    {
        if (!anlage.ZeitschaltuhrAktiv) return null;

        var start = new DateTime(jetzt.Year, jetzt.Month, jetzt.Day, jetzt.Hour, jetzt.Minute, 0).AddMinutes(1);
        for (var i = 0; i < 2 * 24 * 60; i++)
        {
            var zeitpunkt = start.AddMinutes(i);
            var stand = sonne(zeitpunkt);
            foreach (var zeit in anlage.Schaltzeiten)
            {
                if (!zeit.Aktiv || !zeit.GiltAm(zeitpunkt)) continue;
                var gemeint = zeit.Zeitpunkt(zeitpunkt, stand);
                if (gemeint is null) continue;
                if (gemeint.Value.Hour != zeitpunkt.Hour || gemeint.Value.Minute != zeitpunkt.Minute) continue;
                if (gemeint.Value.Date != zeitpunkt.Date) continue;

                var wer = zeit.MotorId.Length == 0
                    ? "alle Antriebe"
                    : anlage.Finde(zeit.MotorId)?.Name ?? "— fehlt —";
                return new Vorschau(zeitpunkt, wer,
                    "Zeitschaltuhr faehrt auf "
                    + zeit.Position.ToString("0", CultureInfo.CurrentCulture) + " %");
            }
        }
        return null;
    }
}
