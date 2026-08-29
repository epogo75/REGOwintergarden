using System;
using System.Collections.Generic;
using System.Globalization;

namespace REGOwintergarden.Model;

/// <summary>
/// Warum ein Antrieb dort steht, wo er steht - in der Reihenfolge, in der es
/// zaehlt.
///
/// Die Reihenfolge <b>ist</b> die Sicherheitsvorschrift dieses Programms.
/// Wind schlaegt alles: eine ausgefahrene Markise im Sturm ist ein Schaden,
/// eine unbeschattete Scheibe ist es nicht. Danach Regen und Frost. Erst
/// darunter kommt, was ein Mensch will, und ganz unten der Komfort.
/// </summary>
public enum Stufe
{
    /// <summary>Nichts zu tun.</summary>
    Frei = 0,

    /// <summary>Die Zeitschaltuhr.</summary>
    Zeit = 1,

    /// <summary>Lueften nach Innentemperatur.</summary>
    Lueftung = 2,

    /// <summary>Beschatten nach Sonne und Helligkeit.</summary>
    Beschattung = 3,

    /// <summary>Von Hand bedient - die Automatik haelt sich zurueck.</summary>
    Hand = 4,

    /// <summary>Zu kalt fuer offene Fenster und ausgefahrene Markisen.</summary>
    Frost = 5,

    /// <summary>Es regnet.</summary>
    Regen = 6,

    /// <summary>Wind ueber der Grenze - oder kein brauchbarer Windwert.</summary>
    Wind = 7,
}

/// <summary>
/// Was fuer einen Antrieb gilt: wohin er soll und warum.
///
/// <see cref="Ziel"/> ist <c>null</c>, wenn nichts zu tun ist - der Grund
/// steht trotzdem da. Das ist die Angabe, nach der in der Uebersicht als
/// Erstes gefragt wird: nicht „wo steht die Markise", sondern „warum ist sie
/// eingefahren, draussen scheint doch die Sonne".
/// </summary>
public sealed record Lage(Motor Motor, Stufe Stufe, double? Ziel, double? Lamelle, string Grund)
{
    public override string ToString() =>
        Motor.Name + ": " + Grund + (Ziel is null
            ? ""
            : " → " + Ziel.Value.ToString("0", CultureInfo.CurrentCulture) + " %");
}

/// <summary>Der Merkzettel je Antrieb - was die Automatik ueber die Zeit hinweg weiss.</summary>
public sealed class MotorMerker
{
    /// <summary>Seit wann es hell genug zum Beschatten ist.</summary>
    public DateTime? HellSeit { get; set; }

    /// <summary>Seit wann es zu dunkel ist.</summary>
    public DateTime? DunkelSeit { get; set; }

    /// <summary>Bis wann der Windalarm noch nachlaeuft.</summary>
    public DateTime? WindBis { get; set; }

    /// <summary>Bis wann der Regenschutz noch nachlaeuft.</summary>
    public DateTime? RegenBis { get; set; }

    /// <summary>Bis wann die Automatik nach einem Handgriff pausiert.</summary>
    public DateTime? HandBis { get; set; }

    /// <summary>Ob gerade beschattet wird.</summary>
    public bool Beschattet { get; set; }

    /// <summary>Ob gerade gelueftet wird.</summary>
    public bool Lueftet { get; set; }

    /// <summary>Was zuletzt gesendet wurde - damit nicht jede Minute dasselbe hinausgeht.</summary>
    public double? Gesendet { get; set; }

    /// <summary>Wann zuletzt gesendet wurde.</summary>
    public DateTime? Gefahren { get; set; }
}

/// <summary>
/// Die Regelmaschine.
///
/// Bewusst ohne Bus und ohne Uhr: hinein gehen Anlage, Wetter, Sonnenstand
/// und ein Zeitpunkt, heraus kommt je Antrieb eine <see cref="Lage"/>. Nur so
/// laesst sich pruefen, dass eine Boe die Markise einfaehrt, dass sie nach
/// dem Abflauen nicht sofort wieder ausfaehrt und dass ein Handgriff zwei
/// Stunden lang gilt - ohne dafuer im Wintergarten zu stehen und zu warten.
/// </summary>
public sealed class Automatik
{
    private readonly Dictionary<string, MotorMerker> _merker = new(StringComparer.Ordinal);

    /// <summary>Der Merkzettel eines Antriebs - fuer die Anzeige und die Pruefung.</summary>
    public MotorMerker Merker(string motorId)
    {
        if (!_merker.TryGetValue(motorId, out var merker))
        {
            merker = new MotorMerker();
            _merker[motorId] = merker;
        }
        return merker;
    }

    /// <summary>Ein Handgriff: die Automatik haelt sich fuer die eingestellte Zeit zurueck.</summary>
    public void VonHand(Motor motor, DateTime jetzt, TimeSpan dauer) =>
        Merker(motor.Id).HandBis = jetzt + dauer;

    /// <summary>Vergisst alles - beim Neuladen der Einstellungen.</summary>
    public void Vergessen() => _merker.Clear();

    /// <summary>
    /// Bewertet die ganze Anlage.
    ///
    /// Jeder Antrieb bekommt genau eine Lage. Ob daraus ein Telegramm wird,
    /// entscheidet der Dienst - hier wird nur gesagt, was gelten soll.
    /// </summary>
    public IReadOnlyList<Lage> Bewerten(Anlage anlage, Wetterlage wetter, Sonnenstand sonne, DateTime jetzt)
    {
        var lagen = new List<Lage>();
        foreach (var motor in anlage.Motoren) lagen.Add(Bewerten(anlage, motor, wetter, sonne, jetzt));
        return lagen;
    }

    private Lage Bewerten(Anlage anlage, Motor motor, Wetterlage wetter, Sonnenstand sonne, DateTime jetzt)
    {
        var merker = Merker(motor.Id);
        var sicher = motor.Sicherheitsposition;

        // ---- Wind ---------------------------------------------------------
        if (anlage.WindschutzAktiv && sicher is not null)
        {
            var wind = wetter.Wind;
            var frisch = wind is not null && wind.Value.IstFrisch(jetzt, anlage.HoechstalterWind);

            if (!frisch)
            {
                // Kein brauchbarer Windwert. Das ist keine Ruhe, sondern
                // Unwissenheit - und bei Unwissenheit faehrt eine Markise ein.
                // Wer hier den letzten bekannten Wert weiterlaufen laesst,
                // baut eine Steuerung, die nach dem Ausfall der Station im
                // naechsten Sturm nichts tut.
                merker.WindBis = jetzt + anlage.WindNachlauf;
                return new Lage(motor, Stufe.Wind, sicher, null,
                    wind is null
                        ? "kein Windwert - zur Sicherheit eingefahren"
                        : "Windwert ist zu alt (" + Alter(wind.Value, jetzt) + ") - zur Sicherheit eingefahren");
            }

            if (wind!.Value.Wert >= motor.Windgrenze)
            {
                merker.WindBis = jetzt + anlage.WindNachlauf;
                return new Lage(motor, Stufe.Wind, sicher, null,
                    "Wind " + Zahl(wind.Value.Wert) + " m/s ueber der Grenze von "
                    + Zahl(motor.Windgrenze) + " m/s");
            }

            // Vorhersage: wer Boeen erwartet, faehrt gar nicht erst aus.
            if (anlage.VorhersageAktiv && anlage.Vorhersage is { } sicht
                && sicht.IstFrisch(jetzt) && sicht.WindSpitze is { } spitze
                && spitze >= motor.Windgrenze)
            {
                return new Lage(motor, Stufe.Wind, sicher, null,
                    "Vorhersage meldet Boeen bis " + Zahl(spitze) + " m/s - eingefahren");
            }

            if (merker.WindBis is { } bis && bis > jetzt)
            {
                return new Lage(motor, Stufe.Wind, sicher, null,
                    "Windalarm laeuft noch nach bis " + Uhr(bis));
            }
        }

        // ---- Regen --------------------------------------------------------
        if (anlage.RegenschutzAktiv && motor.Regenschutz && sicher is not null)
        {
            var regen = wetter.Regen;
            if (regen is not null && regen.Value.IstFrisch(jetzt, anlage.HoechstalterRegen)
                && regen.Value.Wert > 0.5)
            {
                merker.RegenBis = jetzt + anlage.RegenNachlauf;
                return new Lage(motor, Stufe.Regen, sicher, null, "es regnet");
            }
            if (merker.RegenBis is { } bis && bis > jetzt)
            {
                return new Lage(motor, Stufe.Regen, sicher, null,
                    "Regenschutz laeuft noch nach bis " + Uhr(bis));
            }
        }

        // ---- Frost --------------------------------------------------------
        if (anlage.FrostschutzAktiv && sicher is not null)
        {
            var aussen = wetter.Aussen;
            if (aussen is not null && aussen.Value.IstFrisch(jetzt, anlage.HoechstalterTemperatur)
                && aussen.Value.Wert <= motor.Frostgrenze)
            {
                return new Lage(motor, Stufe.Frost, sicher, null,
                    "nur " + Zahl(aussen.Value.Wert) + " °C draussen - unter der Frostgrenze von "
                    + Zahl(motor.Frostgrenze) + " °C");
            }
        }

        // ---- Hand ---------------------------------------------------------
        if (merker.HandBis is { } handBis && handBis > jetzt)
        {
            return new Lage(motor, Stufe.Hand, null, null,
                "von Hand bedient - Automatik pausiert bis " + Uhr(handBis));
        }

        // ---- Beschattung --------------------------------------------------
        if (anlage.BeschattungAktiv && motor.BeschattungAktiv && motor.KannBeschatten)
        {
            var lage = Beschattung(anlage, motor, wetter, sonne, jetzt, merker);
            if (lage is not null) return lage;
        }

        // ---- Lueften ------------------------------------------------------
        if (anlage.LueftungAktiv && motor.LueftungAktiv && motor.KannLueften)
        {
            var lage = Lueften(anlage, motor, wetter, jetzt, merker);
            if (lage is not null) return lage;
        }

        return new Lage(motor, Stufe.Frei, null, null, "nichts zu tun");
    }

    // ---- Beschattung -------------------------------------------------------

    private static Lage? Beschattung(Anlage anlage, Motor motor, Wetterlage wetter, Sonnenstand sonne,
        DateTime jetzt, MotorMerker merker)
    {
        var helligkeit = wetter.Helligkeit(motor.Ausrichtung);
        if (helligkeit is null || !helligkeit.Value.IstFrisch(jetzt, anlage.HoechstalterHelligkeit))
        {
            // Ohne Helligkeitswert wird nicht beschattet. Das ist die
            // freundliche Seite der Unwissenheit: eine Markise, die nicht
            // ausfaehrt, schadet niemandem.
            merker.HellSeit = null;
            return null;
        }

        var aufDerFlaeche = motor.SonneAufDerFlaeche(sonne.Azimut, sonne.Elevation);

        // Ist es drinnen schon warm, wird frueher beschattet. Die Schwelle
        // sinkt, nicht die Verzoegerung: eine Wolke soll auch dann nicht
        // sofort alles aufreissen.
        var schwelle = anlage.Helligkeitsschwelle;
        var warm = wetter.Innen is { } innen && innen.IstFrisch(jetzt, anlage.HoechstalterTemperatur)
                                             && innen.Wert >= anlage.InnenWarm;
        if (warm) schwelle *= anlage.WarmFaktor;

        var hellGenug = aufDerFlaeche && helligkeit.Value.Wert >= schwelle;

        if (hellGenug)
        {
            merker.DunkelSeit = null;
            merker.HellSeit ??= jetzt;

            var wartet = anlage.Einschaltverzoegerung - (jetzt - merker.HellSeit.Value);
            if (wartet > TimeSpan.Zero && !merker.Beschattet)
            {
                return new Lage(motor, Stufe.Frei, null, null,
                    "Sonne auf der Flaeche, wartet noch " + Minuten(wartet) + " vor dem Beschatten");
            }

            merker.Beschattet = true;
            return new Lage(motor, Stufe.Beschattung, motor.Beschattungsposition,
                motor.HatLamelle ? motor.Lamellenposition : null,
                "Sonne aus " + Grad(sonne.Azimut) + " auf " + Grad(motor.Ausrichtung)
                + ", " + Lux(helligkeit.Value.Wert) + " ueber der Schwelle von " + Lux(schwelle)
                + (warm ? " (drinnen warm, Schwelle gesenkt)" : ""));
        }

        merker.HellSeit = null;
        if (!merker.Beschattet) return null;

        merker.DunkelSeit ??= jetzt;
        var restzeit = anlage.Ausschaltverzoegerung - (jetzt - merker.DunkelSeit.Value);
        if (restzeit > TimeSpan.Zero)
        {
            return new Lage(motor, Stufe.Beschattung, motor.Beschattungsposition,
                motor.HatLamelle ? motor.Lamellenposition : null,
                "beschattet, oeffnet in " + Minuten(restzeit)
                + (aufDerFlaeche ? " (zu dunkel)" : " (Sonne nicht mehr auf der Flaeche)"));
        }

        merker.Beschattet = false;
        merker.DunkelSeit = null;
        return new Lage(motor, Stufe.Beschattung, motor.Freiposition, null,
            aufDerFlaeche ? "Beschattung beendet - zu dunkel" : "Beschattung beendet - Sonne weitergezogen");
    }

    // ---- Lueften -----------------------------------------------------------

    private static Lage? Lueften(Anlage anlage, Motor motor, Wetterlage wetter, DateTime jetzt,
        MotorMerker merker)
    {
        var innen = wetter.Innen;
        if (innen is null || !innen.Value.IstFrisch(jetzt, anlage.HoechstalterTemperatur))
        {
            merker.Lueftet = false;
            return null;
        }

        var aussen = wetter.Aussen;
        var draussenKuehler = aussen is not null
                              && aussen.Value.IstFrisch(jetzt, anlage.HoechstalterTemperatur)
                              && aussen.Value.Wert <= innen.Value.Wert - anlage.LueftungUnterschied;

        if (!merker.Lueftet && innen.Value.Wert >= anlage.LueftungAb && draussenKuehler)
        {
            merker.Lueftet = true;
            return new Lage(motor, Stufe.Lueftung, anlage.Lueftungsposition, null,
                "drinnen " + Zahl(innen.Value.Wert) + " °C, draussen "
                + Zahl(aussen!.Value.Wert) + " °C - Fenster geoeffnet");
        }

        if (merker.Lueftet)
        {
            var zuKalt = innen.Value.Wert <= anlage.LueftungAb - anlage.LueftungHysterese;
            if (zuKalt || !draussenKuehler)
            {
                merker.Lueftet = false;
                return new Lage(motor, Stufe.Lueftung, 0, null,
                    zuKalt
                        ? "drinnen wieder " + Zahl(innen.Value.Wert) + " °C - Fenster geschlossen"
                        : "draussen nicht mehr kuehler - Fenster geschlossen");
            }
            return new Lage(motor, Stufe.Lueftung, anlage.Lueftungsposition, null,
                "lueftet, drinnen " + Zahl(innen.Value.Wert) + " °C");
        }

        return null;
    }

    // ---- Text --------------------------------------------------------------

    private static string Zahl(double wert) => wert.ToString("0.#", CultureInfo.CurrentCulture);

    private static string Grad(double wert) => wert.ToString("0", CultureInfo.CurrentCulture) + "°";

    private static string Lux(double wert) => wert >= 1000
        ? (wert / 1000).ToString("0.#", CultureInfo.CurrentCulture) + " kLux"
        : wert.ToString("0", CultureInfo.CurrentCulture) + " Lux";

    private static string Uhr(DateTime zeit) => zeit.ToString("HH:mm", CultureInfo.CurrentCulture);

    private static string Minuten(TimeSpan spanne) => spanne.TotalMinutes >= 1
        ? Math.Ceiling(spanne.TotalMinutes).ToString("0", CultureInfo.CurrentCulture) + " min"
        : Math.Ceiling(spanne.TotalSeconds).ToString("0", CultureInfo.CurrentCulture) + " s";

    private static string Alter(Messwert wert, DateTime jetzt) => Minuten(jetzt - wert.Zeit) + " alt";
}
