using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace REGOwintergarden.Model;

/// <summary>
/// Was fuer ein Antrieb das ist.
///
/// Der Unterschied ist nicht die Mechanik, sondern was die Automatik damit
/// tun darf: eine Markise muss bei Wind <b>ein</b>, ein Fenster bei Regen
/// <b>zu</b>, und ein Rollladen darf bei beidem stehen bleiben. Wer das nicht
/// je Antrieb trennt, faehrt bei der ersten Boe den ganzen Wintergarten in
/// die falsche Richtung.
/// </summary>
public enum Antriebsart
{
    /// <summary>Behang von oben, 0 % ist offen, 100 % ist zu.</summary>
    Rollladen,

    /// <summary>Wie der Rollladen, dazu eine Lamellenstellung.</summary>
    Jalousie,

    /// <summary>Faehrt aus, um zu beschatten. Wind und Regen fahren sie ein.</summary>
    Markise,

    /// <summary>Zum Lueften. Regen und Wind machen es zu.</summary>
    Fenster,

    /// <summary>Lamellendach: beschattet ueber den Winkel, nicht ueber die Hoehe.</summary>
    Lamellendach,
}

/// <summary>
/// Ein Antrieb am Wintergarten.
///
/// Die <see cref="Ausrichtung"/> ist der Kern der Beschattung: nach welcher
/// Himmelsrichtung die Flaeche zeigt, in Grad. 0 ist Nord, 90 Ost, 180 Sued,
/// 270 West. Frei einstellbar und nicht als Auswahl aus acht Richtungen -
/// ein Wintergarten steht selten genau nach Sueden, und 205 Grad sind etwas
/// anderes als „Sued".
/// </summary>
public sealed class Motor
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Neuer Antrieb";

    [JsonPropertyName("art")]
    public Antriebsart Art { get; set; } = Antriebsart.Rollladen;

    /// <summary>Wohin die Flaeche zeigt, in Grad. 0 Nord, 90 Ost, 180 Sued, 270 West.</summary>
    [JsonPropertyName("ausrichtung")]
    public double Ausrichtung { get; set; } = 180;

    /// <summary>
    /// Wie weit die Sonne seitlich stehen darf und noch auf die Flaeche
    /// scheint, in Grad nach jeder Seite.
    ///
    /// 90 Grad ist die Lehrbuchantwort - dann steht die Sonne gerade noch
    /// streifend auf der Flaeche. In der Praxis nimmt man weniger, weil bei
    /// streifendem Einfall kaum noch Waerme hereinkommt und die Markise sonst
    /// den halben Tag draussen steht.
    /// </summary>
    [JsonPropertyName("oeffnungswinkel")]
    public double Oeffnungswinkel { get; set; } = 75;

    /// <summary>Unterhalb dieser Sonnenhoehe wird nicht beschattet - die Sonne steht dann hinter Nachbarhaeusern.</summary>
    [JsonPropertyName("elevation_min")]
    public double ElevationMin { get; set; } = 8;

    /// <summary>Oberhalb dieser Sonnenhoehe scheint die Sonne ueber die Flaeche hinweg.</summary>
    [JsonPropertyName("elevation_max")]
    public double ElevationMax { get; set; } = 90;

    /// <summary>Wohin gefahren wird, wenn beschattet werden soll - in Prozent.</summary>
    [JsonPropertyName("beschattungsposition")]
    public double Beschattungsposition { get; set; } = 100;

    /// <summary>Lamellenstellung waehrend der Beschattung, in Prozent.</summary>
    [JsonPropertyName("lamellenposition")]
    public double Lamellenposition { get; set; } = 60;

    /// <summary>Wohin gefahren wird, wenn die Beschattung endet.</summary>
    [JsonPropertyName("freiposition")]
    public double Freiposition { get; set; }

    // ---- Schutz ------------------------------------------------------------

    /// <summary>Ab dieser Windgeschwindigkeit in m/s faehrt der Antrieb in Sicherheit.</summary>
    [JsonPropertyName("windgrenze")]
    public double Windgrenze { get; set; } = 8;

    /// <summary>Ob Regen diesen Antrieb in Sicherheit faehrt.</summary>
    [JsonPropertyName("regenschutz")]
    public bool Regenschutz { get; set; } = true;

    /// <summary>
    /// Unterhalb dieser Aussentemperatur faehrt der Antrieb in Sicherheit.
    ///
    /// Eine vereiste Markise reisst beim Ausfahren die Mechanik, und ein
    /// offenes Fenster kuehlt den Wintergarten in einer Nacht aus.
    /// </summary>
    [JsonPropertyName("frostgrenze")]
    public double Frostgrenze { get; set; } = 3;

    // ---- Automatik ---------------------------------------------------------

    [JsonPropertyName("beschattung_aktiv")]
    public bool BeschattungAktiv { get; set; } = true;

    [JsonPropertyName("lueftung_aktiv")]
    public bool LueftungAktiv { get; set; }

    [JsonPropertyName("zeit_aktiv")]
    public bool ZeitAktiv { get; set; } = true;

    // ---- Adressen ----------------------------------------------------------

    /// <summary>Auf und Ab, DPT 1.008. Null ist auf, eins ist ab.</summary>
    [JsonPropertyName("adresse_fahren")]
    public string AdresseFahren { get; set; } = "";

    /// <summary>Stopp und Schrittverstellung, DPT 1.007.</summary>
    [JsonPropertyName("adresse_stopp")]
    public string AdresseStopp { get; set; } = "";

    /// <summary>Position anfahren, DPT 5.001 in Prozent.</summary>
    [JsonPropertyName("adresse_position")]
    public string AdressePosition { get; set; } = "";

    /// <summary>Gemeldete Position, DPT 5.001.</summary>
    [JsonPropertyName("adresse_position_status")]
    public string AdressePositionStatus { get; set; } = "";

    /// <summary>Lamellenstellung, DPT 5.001.</summary>
    [JsonPropertyName("adresse_lamelle")]
    public string AdresseLamelle { get; set; } = "";

    /// <summary>Gemeldete Lamellenstellung, DPT 5.001.</summary>
    [JsonPropertyName("adresse_lamelle_status")]
    public string AdresseLamelleStatus { get; set; } = "";

    // ---- Abgeleitetes ------------------------------------------------------

    /// <summary>Ob dieser Antrieb ueberhaupt beschattet.</summary>
    [JsonIgnore]
    public bool KannBeschatten => Art is Antriebsart.Rollladen or Antriebsart.Jalousie
        or Antriebsart.Markise or Antriebsart.Lamellendach;

    /// <summary>Ob dieser Antrieb lueftet.</summary>
    [JsonIgnore]
    public bool KannLueften => Art == Antriebsart.Fenster;

    /// <summary>Ob es eine Lamelle gibt, die mitgestellt wird.</summary>
    [JsonIgnore]
    public bool HatLamelle => Art is Antriebsart.Jalousie or Antriebsart.Lamellendach;

    /// <summary>
    /// Wohin der Antrieb bei Wind, Regen oder Frost faehrt.
    ///
    /// Markise und Fenster haben eine sichere Seite, und sie ist bei beiden
    /// die Null: die Markise eingefahren, das Fenster geschlossen. Ein
    /// Rollladen dagegen ist bei Wind da am sichersten, wo er ist - ihn
    /// hochzufahren gaebe dem Wind erst die Flaeche.
    /// </summary>
    [JsonIgnore]
    public double? Sicherheitsposition => Art switch
    {
        Antriebsart.Markise => 0,
        Antriebsart.Fenster => 0,
        Antriebsart.Lamellendach => 0,
        _ => null,
    };

    /// <summary>Die Himmelsrichtung als Wort - fuer die Uebersicht.</summary>
    [JsonIgnore]
    public string Richtung => Richtungsname(Ausrichtung);

    /// <summary>
    /// Aus Grad ein Kuerzel. Sechzehn Striche, weil acht bei 205 Grad
    /// „Sued" saegen und damit die Haelfte der Auskunft verlieren.
    /// </summary>
    public static string Richtungsname(double grad)
    {
        var namen = new[]
        {
            "N", "NNO", "NO", "ONO", "O", "OSO", "SO", "SSO",
            "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW",
        };
        var stelle = (int)Math.Round(Normiert(grad) / 22.5) % 16;
        return namen[stelle];
    }

    /// <summary>Ein Winkel auf 0 bis unter 360 gebracht.</summary>
    public static double Normiert(double grad)
    {
        var wert = grad % 360;
        return wert < 0 ? wert + 360 : wert;
    }

    /// <summary>
    /// Der kuerzeste Abstand zweier Winkel, 0 bis 180.
    ///
    /// Ueber Nord hinweg zu rechnen ist der Fehler, den man einmal macht:
    /// zwischen 350 und 10 Grad liegen zwanzig Grad und nicht dreihundertvierzig.
    /// </summary>
    public static double Abstand(double a, double b)
    {
        var unterschied = Math.Abs(Normiert(a) - Normiert(b));
        return unterschied > 180 ? 360 - unterschied : unterschied;
    }

    /// <summary>Ob die Sonne aus dieser Richtung auf die Flaeche scheint.</summary>
    public bool SonneAufDerFlaeche(double azimut, double elevation) =>
        Abstand(azimut, Ausrichtung) <= Oeffnungswinkel
        && elevation >= ElevationMin
        && elevation <= ElevationMax;

    public Motor Clone() => (Motor)MemberwiseClone();

    public override string ToString() =>
        Name + " (" + Art + ", " + Richtung + " "
        + Math.Round(Ausrichtung).ToString("0", CultureInfo.CurrentCulture) + "°)";
}
