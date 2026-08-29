using System;
using System.Globalization;

namespace REGOwintergarden.Model;

/// <summary>
/// Wo die Sonne steht und wann sie auf- und untergeht.
///
/// <see cref="Azimut"/> ist die Himmelsrichtung in Grad - 90 ist Ost, 180
/// Sued, 270 West. <see cref="Elevation"/> ist die Hoehe ueber dem Horizont;
/// negativ heisst, die Sonne ist unter.
/// </summary>
public sealed record Sonnenstand(double Azimut, double Elevation, DateTime? Aufgang, DateTime? Untergang)
{
    /// <summary>Ob die Sonne ueber dem Horizont steht.</summary>
    public bool Tag => Elevation > 0;

    public override string ToString() =>
        "Azimut " + Azimut.ToString("0", CultureInfo.CurrentCulture)
        + "°, Hoehe " + Elevation.ToString("0.#", CultureInfo.CurrentCulture) + "°";
}

/// <summary>
/// Rechnet den Sonnenstand aus Datum, Uhrzeit und Standort.
///
/// <b>Warum selbst rechnen,</b> wo die Wetterstation Azimut und Elevation
/// doch liefert: sie liefert eben nur den Stand von jetzt. Fuer eine
/// Zeitschaltuhr, die „eine halbe Stunde vor Sonnenuntergang" schalten soll,
/// braucht es den Untergang - und den meldet keine Station. Ausserdem faellt
/// eine Station aus, und dann ist eine gerechnete Sonne besser als gar keine.
/// Liegen beide vor, gilt die Station: sie misst, wir rechnen.
///
/// Das Verfahren ist das der NOAA, wie es in deren Tabellenblatt steht.
/// Genauigkeit ueber ein paar Bogenminuten - fuer eine Beschattung mehr als
/// genug, fuer Astronomie zu wenig.
/// </summary>
public static class Astro
{
    /// <summary>
    /// Der Sonnenstand zu einem Zeitpunkt in <b>Ortszeit</b>. Auf- und
    /// Untergang kommen ebenfalls als Ortszeit zurueck.
    /// </summary>
    public static Sonnenstand Berechnen(DateTime ortszeit, double breite, double laenge)
    {
        var zone = TimeZoneInfo.Local;
        var utc = ortszeit.Kind == DateTimeKind.Utc
            ? ortszeit
            : TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(ortszeit, DateTimeKind.Unspecified), zone);

        var stand = BerechnenUtc(utc, breite, laenge);
        return stand with
        {
            Aufgang = NachOrtszeit(stand.Aufgang, zone),
            Untergang = NachOrtszeit(stand.Untergang, zone),
        };
    }

    private static DateTime? NachOrtszeit(DateTime? utc, TimeZoneInfo zone) =>
        utc is null ? null : TimeZoneInfo.ConvertTimeFromUtc(utc.Value, zone);

    /// <summary>Derselbe Rechenweg, aber alles in UTC - so laesst er sich pruefen.</summary>
    public static Sonnenstand BerechnenUtc(DateTime utc, double breite, double laenge)
    {
        var jahrhundert = Jahrhundert(utc);

        var mittlereLaenge = Grad360(280.46646 + jahrhundert * (36000.76983 + jahrhundert * 0.0003032));
        var mittlereAnomalie = 357.52911 + jahrhundert * (35999.05029 - 0.0001537 * jahrhundert);
        var exzentrizitaet = 0.016708634 - jahrhundert * (0.000042037 + 0.0000001267 * jahrhundert);

        var mittelpunkt =
            Math.Sin(Bogen(mittlereAnomalie)) * (1.914602 - jahrhundert * (0.004817 + 0.000014 * jahrhundert))
            + Math.Sin(Bogen(2 * mittlereAnomalie)) * (0.019993 - 0.000101 * jahrhundert)
            + Math.Sin(Bogen(3 * mittlereAnomalie)) * 0.000289;

        var wahreLaenge = mittlereLaenge + mittelpunkt;
        var scheinbareLaenge = wahreLaenge - 0.00569
                               - 0.00478 * Math.Sin(Bogen(125.04 - 1934.136 * jahrhundert));

        var schiefeMittel = 23 + (26 + (21.448 - jahrhundert
            * (46.815 + jahrhundert * (0.00059 - jahrhundert * 0.001813))) / 60) / 60;
        var schiefe = schiefeMittel + 0.00256 * Math.Cos(Bogen(125.04 - 1934.136 * jahrhundert));

        var deklination = Winkel(Math.Asin(Math.Sin(Bogen(schiefe)) * Math.Sin(Bogen(scheinbareLaenge))));

        // Zeitgleichung in Minuten: der Unterschied zwischen der Sonnenuhr
        // und der Uhr an der Wand. Bis zu einer Viertelstunde - wer sie
        // weglaesst, liegt beim Sonnenstand um mehrere Grad daneben.
        var y = Math.Pow(Math.Tan(Bogen(schiefe) / 2), 2);
        var zeitgleichung = 4 * Winkel(
            y * Math.Sin(2 * Bogen(mittlereLaenge))
            - 2 * exzentrizitaet * Math.Sin(Bogen(mittlereAnomalie))
            + 4 * exzentrizitaet * y * Math.Sin(Bogen(mittlereAnomalie)) * Math.Cos(2 * Bogen(mittlereLaenge))
            - 0.5 * y * y * Math.Sin(4 * Bogen(mittlereLaenge))
            - 1.25 * exzentrizitaet * exzentrizitaet * Math.Sin(2 * Bogen(mittlereAnomalie)));

        var minutenUtc = utc.TimeOfDay.TotalMinutes;
        var wahreSonnenzeit = (minutenUtc + zeitgleichung + 4 * laenge + 1440) % 1440;
        var stundenwinkel = wahreSonnenzeit / 4 - 180;

        var zenitCos = Math.Sin(Bogen(breite)) * Math.Sin(Bogen(deklination))
                       + Math.Cos(Bogen(breite)) * Math.Cos(Bogen(deklination)) * Math.Cos(Bogen(stundenwinkel));
        var zenit = Winkel(Math.Acos(Math.Clamp(zenitCos, -1, 1)));
        var hoehe = 90 - zenit;

        // Die Luft bricht das Licht: eine Sonne, die geometrisch schon unter
        // dem Horizont steht, ist noch zu sehen. Ohne diese Korrektur gingen
        // Auf- und Untergang um Minuten daneben.
        hoehe += Brechung(hoehe);

        double azimut;
        var nenner = Math.Cos(Bogen(breite)) * Math.Sin(Bogen(zenit));
        if (Math.Abs(nenner) < 1e-9)
        {
            // Genau im Zenit oder am Pol - dann ist die Richtung beliebig.
            azimut = 180;
        }
        else
        {
            var wert = Math.Clamp(
                (Math.Sin(Bogen(breite)) * Math.Cos(Bogen(zenit)) - Math.Sin(Bogen(deklination))) / nenner,
                -1, 1);
            var roh = Winkel(Math.Acos(wert));
            azimut = stundenwinkel > 0 ? Motor.Normiert(roh + 180) : Motor.Normiert(540 - roh);
        }

        var (aufgang, untergang) = AufUnter(utc, breite, laenge, deklination, zeitgleichung);
        return new Sonnenstand(azimut, hoehe, aufgang, untergang);
    }

    /// <summary>
    /// Auf- und Untergang des Tages, in UTC.
    ///
    /// Gerechnet wird mit 90,833 Grad Zenit: die halbe Sonnenscheibe plus die
    /// Brechung der Luft. Das ist die uebliche Festlegung, und sie ist der
    /// Grund, warum die Sonne „vor" ihrem geometrischen Aufgang erscheint.
    /// </summary>
    private static (DateTime? Auf, DateTime? Unter) AufUnter(DateTime utc, double breite, double laenge,
        double deklination, double zeitgleichung)
    {
        var wert = Math.Cos(Bogen(90.833)) / (Math.Cos(Bogen(breite)) * Math.Cos(Bogen(deklination)))
                   - Math.Tan(Bogen(breite)) * Math.Tan(Bogen(deklination));

        // Ausserhalb von minus eins bis eins gibt es keinen Auf- oder
        // Untergang: Mitternachtssonne oder Polarnacht. Dann ist null die
        // ehrliche Antwort und nicht eine erfundene Uhrzeit.
        if (wert is < -1 or > 1) return (null, null);

        var stundenwinkel = Winkel(Math.Acos(wert));
        var mittag = 720 - 4 * laenge - zeitgleichung;
        var tag = utc.Date;

        return (tag.AddMinutes(mittag - 4 * stundenwinkel), tag.AddMinutes(mittag + 4 * stundenwinkel));
    }

    /// <summary>Die Brechung der Luft in Grad, abhaengig von der Hoehe.</summary>
    private static double Brechung(double hoehe)
    {
        if (hoehe > 85) return 0;
        var t = Math.Tan(Bogen(hoehe));
        double sekunden;
        if (hoehe > 5) sekunden = 58.1 / t - 0.07 / Math.Pow(t, 3) + 0.000086 / Math.Pow(t, 5);
        else if (hoehe > -0.575) sekunden = 1735 + hoehe * (-518.2 + hoehe * (103.4 + hoehe * (-12.79 + hoehe * 0.711)));
        else sekunden = -20.772 / t;
        return sekunden / 3600;
    }

    private static double Jahrhundert(DateTime utc)
    {
        // Julianisches Datum, dann Jahrhunderte seit J2000.
        var julianisch = utc.ToOADate() + 2415018.5;
        return (julianisch - 2451545.0) / 36525.0;
    }

    private static double Bogen(double grad) => grad * Math.PI / 180.0;

    private static double Winkel(double bogen) => bogen * 180.0 / Math.PI;

    private static double Grad360(double grad) => Motor.Normiert(grad);
}
