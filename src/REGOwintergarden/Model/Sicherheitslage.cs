using System;
using System.Globalization;

namespace REGOwintergarden.Model;

/// <summary>
/// Das Urteil der Anlage über Wind und Regen - das, was an die Aktoren geht.
///
/// <b>Warum ein eigenes Urteil und nicht das Bit der Station durchreichen:</b>
/// weil die Station ausfallen kann. Reicht man ihr Bit durch, merkt niemand
/// den Ausfall - es kommt einfach nichts mehr, und die Aktoren fahren
/// weiterhin aus. Hier wird stattdessen entschieden, und die Entscheidung
/// kennt auch den Fall „von der Station kommt seit zehn Minuten nichts".
/// </summary>
public sealed record Sicherheitslage(bool Wind, bool Regen, string Grund, bool Stationsausfall)
{
    /// <summary>Ob überhaupt etwas festhält.</summary>
    public bool Alarm => Wind || Regen;

    public override string ToString() =>
        (Wind ? "Windalarm" : "Wind ruhig") + ", " + (Regen ? "Regen" : "trocken") + " - " + Grund;
}

/// <summary>
/// Bildet aus dem, was die Wetterstation meldet, das Urteil der Anlage.
///
/// Die Kette ist: Station meldet zyklisch an dieses Programm, dieses Programm
/// meldet zyklisch an die Aktoren. Jede Stufe überwacht die vorige, und die
/// Aktoren überwachen diese hier. Fällt irgendwo etwas aus, hört die nächste
/// Stufe auf, Entwarnung zu senden - und alles fährt von selbst in
/// Sicherheit. Das ist der Grund, warum zyklisch gesendet wird und nicht nur
/// bei Änderung.
/// </summary>
public static class Sicherheit
{
    /// <summary>
    /// Das Urteil zum Zeitpunkt <paramref name="jetzt"/>.
    ///
    /// Windalarm gilt, wenn eines davon zutrifft:
    /// die Station meldet Alarm; die Geschwindigkeit liegt über der Grenze;
    /// die Vorhersage kündigt Böen an; oder von der Station kommt nichts
    /// Frisches mehr.
    /// </summary>
    public static Sicherheitslage Bewerten(Anlage anlage, Wetterlage wetter, DateTime jetzt)
    {
        var alarmbit = wetter.Windalarm;
        var alarmFrisch = alarmbit is not null && alarmbit.Value.IstFrisch(jetzt, anlage.HoechstalterWind);

        var wind = wetter.Wind;
        var windFrisch = wind is not null && wind.Value.IstFrisch(jetzt, anlage.HoechstalterWind);

        var regen = wetter.Regen;
        var regenFrisch = regen is not null && regen.Value.IstFrisch(jetzt, anlage.HoechstalterRegen);

        // Der Ausfall zuerst: er ist der Fall, für den es diese Klasse gibt.
        // Ein stiller Windmesser ist keine Windstille, und ein stiller
        // Regensensor ist kein schöner Tag.
        var ausfall = !alarmFrisch && !windFrisch;
        if (ausfall)
        {
            var grund = alarmbit is null && wind is null
                ? "keine Windmeldung von der Wetterstation eingetragen"
                : "von der Wetterstation kommt seit " + Alter(alarmbit ?? wind, jetzt) + " nichts mehr";
            return new Sicherheitslage(true, !regenFrisch || regen!.Value.Wert > 0.5, grund, true);
        }

        if (alarmFrisch && alarmbit!.Value.Wert > 0.5)
        {
            return new Sicherheitslage(true, regenFrisch && regen!.Value.Wert > 0.5,
                "Windalarm von der Wetterstation"
                + (windFrisch ? " (" + Zahl(wind!.Value.Wert) + " m/s)" : ""), false);
        }

        if (windFrisch && wind!.Value.Wert >= anlage.WindgrenzeAusgabe)
        {
            return new Sicherheitslage(true, regenFrisch && regen!.Value.Wert > 0.5,
                "Wind " + Zahl(wind.Value.Wert) + " m/s über der Anlagengrenze von "
                + Zahl(anlage.WindgrenzeAusgabe) + " m/s", false);
        }

        if (anlage.VorhersageAktiv && anlage.Vorhersage is { } sicht && sicht.IstFrisch(jetzt)
            && sicht.WindSpitze is { } spitze && spitze >= anlage.WindgrenzeAusgabe)
        {
            return new Sicherheitslage(true, regenFrisch && regen!.Value.Wert > 0.5,
                "Vorhersage meldet Böen bis " + Zahl(spitze) + " m/s", false);
        }

        // Kein Wind. Bleibt der Regen - und auch dort gilt: keine Meldung ist
        // keine Entwarnung.
        if (!regenFrisch)
        {
            return new Sicherheitslage(false, true,
                regen is null
                    ? "keine Regenmeldung von der Wetterstation eingetragen"
                    : "keine frische Regenmeldung", true);
        }

        var nass = regen!.Value.Wert > 0.5;
        return new Sicherheitslage(false, nass,
            nass
                ? "Regenmeldung von der Wetterstation"
                : "Wetterstation meldet ruhig"
                  + (windFrisch ? ", " + Zahl(wind!.Value.Wert) + " m/s" : ""),
            false);
    }

    private static string Zahl(double wert) => wert.ToString("0.#", CultureInfo.CurrentCulture);

    private static string Alter(Messwert? wert, DateTime jetzt)
    {
        if (wert is null) return "unbekannt lange";
        var spanne = jetzt - wert.Value.Zeit;
        return spanne.TotalMinutes >= 1
            ? Math.Floor(spanne.TotalMinutes).ToString("0", CultureInfo.CurrentCulture) + " Minuten"
            : Math.Floor(spanne.TotalSeconds).ToString("0", CultureInfo.CurrentCulture) + " Sekunden";
    }
}

/// <summary>
/// Der Zyklusgeber: entscheidet, wann ein Wert wieder hinaus muss.
///
/// Zwei Fälle - der Wert hat sich geändert, oder die Zykluszeit ist um. Der
/// zweite ist der wichtigere: die Aktoren erkennen den Ausfall dieses
/// Programms daran, dass die Wiederholung ausbleibt. Ein Signal, das nur bei
/// Änderung gesendet wird, ist kein Lebenszeichen.
///
/// Bewusst ohne Uhr und ohne Bus, damit sich beides prüfen lässt.
/// </summary>
public sealed class Zyklusgeber
{
    private bool? _zuletzt;
    private DateTime _gesendet = DateTime.MinValue;

    public Zyklusgeber(TimeSpan takt)
    {
        Takt = takt;
    }

    public TimeSpan Takt { get; set; }

    /// <summary>Der zuletzt gesendete Wert - für die Anzeige.</summary>
    public bool? Wert => _zuletzt;

    /// <summary>Wann zuletzt gesendet wurde.</summary>
    public DateTime Gesendet => _gesendet;

    /// <summary>
    /// Ob jetzt gesendet werden muss. Sagt der Aufrufer ja, gilt es als
    /// gesendet.
    /// </summary>
    public bool Faellig(bool wert, DateTime jetzt)
    {
        var geaendert = _zuletzt != wert;
        var ueberfaellig = jetzt - _gesendet >= Takt;
        if (!geaendert && !ueberfaellig) return false;

        _zuletzt = wert;
        _gesendet = jetzt;
        return true;
    }

    /// <summary>Ob der letzte Aufruf eine Änderung war - für das Protokoll.</summary>
    public bool WarAenderung(bool wert) => _zuletzt != wert;

    public void Vergessen()
    {
        _zuletzt = null;
        _gesendet = DateTime.MinValue;
    }
}
