using System;
using System.Globalization;

namespace REGOwintergarden.Model;

/// <summary>
/// Ein Messwert mit dem Zeitpunkt, zu dem er kam.
///
/// Der Zeitpunkt ist nicht Beiwerk, sondern der halbe Sinn: eine
/// Windgeschwindigkeit von vor drei Stunden ist keine Windgeschwindigkeit.
/// Wer nur den Wert speichert, baut eine Steuerung, die bei ausgefallener
/// Wetterstation seelenruhig die Markise draussen laesst.
/// </summary>
public readonly record struct Messwert(double Wert, DateTime Zeit)
{
    public bool IstFrisch(DateTime jetzt, TimeSpan hoechstalter) => jetzt - Zeit <= hoechstalter;

    public override string ToString() =>
        Wert.ToString("0.#", CultureInfo.CurrentCulture) + " ("
        + Zeit.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + ")";
}

/// <summary>
/// Was die Wetterstation gerade meldet.
///
/// Alle Werte koennen fehlen - eine Station wird nachgeruestet, eine Adresse
/// ist noch nicht eingetragen, ein Geraet ist ausgefallen. Deshalb ist jeder
/// Wert <c>null</c>-faehig, und die Automatik muss mit jedem Loch umgehen
/// koennen. Sie tut das nicht, indem sie raet, sondern indem sie den
/// sicheren Weg nimmt.
/// </summary>
public sealed class Wetterlage
{
    /// <summary>Regen, 0 oder 1.</summary>
    public Messwert? Regen { get; set; }

    /// <summary>Windgeschwindigkeit in m/s.</summary>
    public Messwert? Wind { get; set; }

    /// <summary>Aussentemperatur in Grad.</summary>
    public Messwert? Aussen { get; set; }

    /// <summary>Innentemperatur im Wintergarten, in Grad.</summary>
    public Messwert? Innen { get; set; }

    /// <summary>Helligkeit Ost in Lux.</summary>
    public Messwert? HellOst { get; set; }

    /// <summary>Helligkeit Sued in Lux.</summary>
    public Messwert? HellSued { get; set; }

    /// <summary>Helligkeit West in Lux.</summary>
    public Messwert? HellWest { get; set; }

    /// <summary>Sonnenazimut, falls die Station ihn liefert.</summary>
    public Messwert? Azimut { get; set; }

    /// <summary>Sonnenhoehe, falls die Station sie liefert.</summary>
    public Messwert? Elevation { get; set; }

    /// <summary>
    /// Die Helligkeit, die fuer eine Flaeche dieser Ausrichtung zaehlt.
    ///
    /// Drei Fuehler, aber acht Ausrichtungen: genommen wird der, dessen
    /// Richtung am naechsten liegt. Ein Nordfenster bekommt so den Ostwert -
    /// und beschattet trotzdem nie, weil die Sonne dort nie auf der Flaeche
    /// steht.
    /// </summary>
    public Messwert? Helligkeit(double ausrichtung)
    {
        var ost = Motor.Abstand(ausrichtung, 90);
        var sued = Motor.Abstand(ausrichtung, 180);
        var west = Motor.Abstand(ausrichtung, 270);

        if (ost <= sued && ost <= west) return HellOst ?? HellSued ?? HellWest;
        if (sued <= west) return HellSued ?? HellOst ?? HellWest;
        return HellWest ?? HellSued ?? HellOst;
    }

    /// <summary>Der hoechste der drei Helligkeitswerte - fuer die Uebersicht.</summary>
    public Messwert? HellsteRichtung()
    {
        Messwert? beste = null;
        foreach (var wert in new[] { HellOst, HellSued, HellWest })
        {
            if (wert is null) continue;
            if (beste is null || wert.Value.Wert > beste.Value.Wert) beste = wert;
        }
        return beste;
    }

    public Wetterlage Clone() => (Wetterlage)MemberwiseClone();
}

/// <summary>
/// Was die Vorhersage fuer die naechsten Stunden sagt.
///
/// Sie ersetzt die Station nicht - sie ergaenzt sie. Der Nutzen ist die
/// Vorwarnung: eine Markise, die um zehn Uhr ausfaehrt, obwohl um elf Boeen
/// mit 15 m/s angesagt sind, faehrt zweimal umsonst und einmal zu spaet.
/// </summary>
public sealed class Vorhersage
{
    public DateTime Stand { get; set; }

    /// <summary>Hoechste erwartete Boe in den naechsten Stunden, in m/s.</summary>
    public double? WindSpitze { get; set; }

    /// <summary>Hoechste Regenwahrscheinlichkeit in den naechsten Stunden, in Prozent.</summary>
    public double? Regenwahrscheinlichkeit { get; set; }

    /// <summary>Hoechste erwartete Temperatur des Tages, in Grad.</summary>
    public double? Hoechsttemperatur { get; set; }

    /// <summary>Woher sie kommt - fuer das Protokoll und die Uebersicht.</summary>
    public string Quelle { get; set; } = "";

    public bool IstFrisch(DateTime jetzt) => jetzt - Stand < TimeSpan.FromHours(3);

    public override string ToString()
    {
        var teile = new System.Collections.Generic.List<string>();
        if (WindSpitze is not null)
        {
            teile.Add("Boeen bis " + WindSpitze.Value.ToString("0.#", CultureInfo.CurrentCulture) + " m/s");
        }
        if (Regenwahrscheinlichkeit is not null)
        {
            teile.Add("Regen " + Regenwahrscheinlichkeit.Value.ToString("0", CultureInfo.CurrentCulture) + " %");
        }
        if (Hoechsttemperatur is not null)
        {
            teile.Add("bis " + Hoechsttemperatur.Value.ToString("0.#", CultureInfo.CurrentCulture) + " °C");
        }
        return teile.Count == 0 ? "keine Vorhersage" : string.Join("  ·  ", teile);
    }
}
