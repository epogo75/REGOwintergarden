using System;
using System.Collections.Generic;

namespace REGOwintergarden.Model;

/// <summary>Eine faellige Schaltzeit mit dem Antrieb, den sie meint.</summary>
public sealed record Faellig(Schaltzeit Zeit, Motor Motor)
{
    public override string ToString() => Motor.Name + ": " + Zeit.Beschreibung();
}

/// <summary>
/// Entscheidet, welche Schaltzeit jetzt faellig ist.
///
/// Wie in REGOcontroller mit Gedaechtnis: die Uhr wird sekuendlich befragt,
/// eine Schaltzeit gilt aber eine ganze Minute. Ohne das Gedaechtnis liefe
/// jede Schaltzeit sechzigmal an - und das faellt nicht beim Pruefen auf,
/// sondern morgens um sieben.
/// </summary>
public sealed class Zeitschaltuhr
{
    private readonly Dictionary<string, DateTime> _zuletzt = new(StringComparer.Ordinal);

    /// <summary>
    /// Was jetzt faellig ist. <paramref name="sonne"/> wird fuer die Bezuege
    /// auf Sonnenauf- und -untergang gebraucht.
    /// </summary>
    public IReadOnlyList<Faellig> Faellige(Anlage anlage, Sonnenstand sonne, DateTime jetzt)
    {
        var treffer = new List<Faellig>();
        if (!anlage.ZeitschaltuhrAktiv) return treffer;

        var minute = new DateTime(jetzt.Year, jetzt.Month, jetzt.Day, jetzt.Hour, jetzt.Minute, 0);

        foreach (var zeit in anlage.Schaltzeiten)
        {
            if (!zeit.Aktiv) continue;
            if (!zeit.GiltAm(jetzt)) continue;

            var zeitpunkt = zeit.Zeitpunkt(jetzt, sonne);
            if (zeitpunkt is null) continue;

            var gemeint = new DateTime(zeitpunkt.Value.Year, zeitpunkt.Value.Month, zeitpunkt.Value.Day,
                zeitpunkt.Value.Hour, zeitpunkt.Value.Minute, 0);
            if (gemeint != minute) continue;

            if (_zuletzt.TryGetValue(zeit.Id, out var vorher) && vorher == minute) continue;
            _zuletzt[zeit.Id] = minute;

            foreach (var motor in anlage.Motoren)
            {
                // Leer heisst: alle. Das ist der haeufigste Fall - „abends
                // alles zu" meint nicht einen Antrieb.
                if (zeit.MotorId.Length > 0 && zeit.MotorId != motor.Id) continue;
                if (!motor.ZeitAktiv) continue;
                treffer.Add(new Faellig(zeit, motor));
            }
        }
        return treffer;
    }

    /// <summary>
    /// Die naechste Schaltung in Worten - fuer die Uebersicht.
    /// Gesucht wird minutenweise ueber acht Tage.
    /// </summary>
    public static string NaechsteText(Anlage anlage, Func<DateTime, Sonnenstand> sonne, DateTime von)
    {
        if (!anlage.ZeitschaltuhrAktiv) return "Zeitschaltuhr aus";

        var start = new DateTime(von.Year, von.Month, von.Day, von.Hour, von.Minute, 0).AddMinutes(1);
        for (var i = 0; i < 8 * 24 * 60; i++)
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

                var tage = (zeitpunkt.Date - von.Date).Days;
                var uhrzeit = zeitpunkt.ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture);
                var wann = tage switch
                {
                    0 => "heute " + uhrzeit,
                    1 => "morgen " + uhrzeit,
                    _ => new[] { "So", "Mo", "Di", "Mi", "Do", "Fr", "Sa" }[(int)zeitpunkt.DayOfWeek]
                         + " " + uhrzeit,
                };
                return wann + " · " + zeit.Beschreibung();
            }
        }
        return "keine Schaltzeit";
    }

    public void Vergessen() => _zuletzt.Clear();
}
