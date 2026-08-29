using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace REGOwintergarden.Model;

/// <summary>Woran sich eine Schaltzeit haengt.</summary>
public enum Zeitbezug
{
    /// <summary>Feste Uhrzeit.</summary>
    Uhrzeit,

    /// <summary>Sonnenaufgang, mit Versatz.</summary>
    Sonnenaufgang,

    /// <summary>Sonnenuntergang, mit Versatz.</summary>
    Sonnenuntergang,
}

/// <summary>
/// Eine Schaltzeit: wann ein Antrieb von selbst faehrt.
///
/// „Eine halbe Stunde vor Sonnenuntergang" ist der Fall, um den es geht -
/// eine feste Uhrzeit taugt dafuer nicht, weil sie im Juni zwei Stunden
/// danebenliegt. Deshalb der Bezug und der Versatz in Minuten.
/// </summary>
public sealed class Schaltzeit
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("aktiv")]
    public bool Aktiv { get; set; } = true;

    [JsonPropertyName("bezug")]
    public Zeitbezug Bezug { get; set; } = Zeitbezug.Uhrzeit;

    /// <summary>Bei festem Bezug: die Uhrzeit als <c>07:30</c>.</summary>
    [JsonPropertyName("zeit")]
    public string Zeit { get; set; } = "07:00";

    /// <summary>Versatz in Minuten, auch negativ - minus 30 heisst „eine halbe Stunde vorher".</summary>
    [JsonPropertyName("versatz")]
    public int Versatz { get; set; }

    /// <summary>Tage als Ziffern, 1 ist Montag, 7 ist Sonntag.</summary>
    [JsonPropertyName("tage")]
    public string Tage { get; set; } = "1234567";

    /// <summary>Welcher Antrieb - leer heisst: alle.</summary>
    [JsonPropertyName("motor")]
    public string MotorId { get; set; } = "";

    /// <summary>Wohin, in Prozent.</summary>
    [JsonPropertyName("position")]
    public double Position { get; set; }

    /// <summary>Freie Bemerkung, damit man eine lange Liste noch versteht.</summary>
    [JsonPropertyName("bemerkung")]
    public string Bemerkung { get; set; } = "";

    /// <summary>
    /// Der Zeitpunkt am angegebenen Tag - oder <c>null</c>, wenn er nicht zu
    /// bestimmen ist (Polarnacht, unlesbare Uhrzeit).
    /// </summary>
    public DateTime? Zeitpunkt(DateTime tag, Sonnenstand sonne)
    {
        switch (Bezug)
        {
            case Zeitbezug.Sonnenaufgang:
                return sonne.Aufgang?.AddMinutes(Versatz);
            case Zeitbezug.Sonnenuntergang:
                return sonne.Untergang?.AddMinutes(Versatz);
            default:
                if (!TryUhrzeit(out var stunde, out var minute)) return null;
                return tag.Date.AddHours(stunde).AddMinutes(minute + Versatz);
        }
    }

    public bool TryUhrzeit(out int stunde, out int minute)
    {
        stunde = 0;
        minute = 0;
        var teile = (Zeit ?? "").Split(':');
        if (teile.Length != 2) return false;
        return int.TryParse(teile[0], NumberStyles.None, CultureInfo.InvariantCulture, out stunde)
               && int.TryParse(teile[1], NumberStyles.None, CultureInfo.InvariantCulture, out minute)
               && stunde is >= 0 and <= 23 && minute is >= 0 and <= 59;
    }

    /// <summary>Ob die Schaltzeit an diesem Wochentag gilt.</summary>
    public bool GiltAm(DateTime tag)
    {
        var nummer = tag.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)tag.DayOfWeek;
        return Tage.Contains((char)('0' + nummer));
    }

    public string Tagesnamen()
    {
        var namen = new[] { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };
        var text = new System.Text.StringBuilder();
        for (var tag = 1; tag <= 7; tag++)
        {
            if (!Tage.Contains((char)('0' + tag))) continue;
            if (text.Length > 0) text.Append(' ');
            text.Append(namen[tag - 1]);
        }
        return text.Length == 0 ? "nie" : text.ToString();
    }

    public string Beschreibung()
    {
        var wann = Bezug switch
        {
            Zeitbezug.Sonnenaufgang => "Sonnenaufgang" + Versatztext(),
            Zeitbezug.Sonnenuntergang => "Sonnenuntergang" + Versatztext(),
            _ => Zeit + (Versatz == 0 ? "" : Versatztext()),
        };
        return wann + "  ·  " + Tagesnamen() + "  ·  "
               + Position.ToString("0", CultureInfo.CurrentCulture) + " %";
    }

    private string Versatztext() => Versatz == 0
        ? ""
        : (Versatz > 0 ? " + " : " − ") + Math.Abs(Versatz).ToString("0", CultureInfo.CurrentCulture) + " min";

    public Schaltzeit Clone() => (Schaltzeit)MemberwiseClone();
}

/// <summary>
/// Die ganze Anlage: Antriebe, Wetterstation, Standort und die Grenzen, nach
/// denen die Automatik entscheidet.
///
/// Alles an einer Stelle und in einer Datei - ein Wintergarten ist keine
/// Datenbank. Wer die Einstellungen sichern will, kopiert eine Datei.
/// </summary>
public sealed class Anlage
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Wintergarten";

    [JsonPropertyName("motoren")]
    public List<Motor> Motoren { get; set; } = new();

    [JsonPropertyName("schaltzeiten")]
    public List<Schaltzeit> Schaltzeiten { get; set; } = new();

    // ---- Standort ----------------------------------------------------------

    /// <summary>Breitengrad, noerdlich positiv.</summary>
    [JsonPropertyName("breite")]
    public double Breite { get; set; } = 48.75;

    /// <summary>Laengengrad, oestlich positiv.</summary>
    [JsonPropertyName("laenge")]
    public double Laenge { get; set; } = 8.24;

    [JsonPropertyName("ort")]
    public string Ort { get; set; } = "Buehl";

    // ---- Adressen der Wetterstation ---------------------------------------

    [JsonPropertyName("adresse_regen")]
    public string AdresseRegen { get; set; } = "";

    /// <summary>
    /// Das Windalarm-Bit der Wetterstation, DPT 1.x.
    ///
    /// Die eigentliche Ueberwachung laeuft in der KNX-Logik - Boeenerkennung,
    /// Grenze, Nachlauf. Dieses Programm wertet das Ergebnis aus, statt
    /// daneben einen zweiten Waechter mit anderen Grenzen zu bauen.
    /// </summary>
    [JsonPropertyName("adresse_windalarm")]
    public string AdresseWindalarm { get; set; } = "";

    /// <summary>Windgeschwindigkeit in m/s - zur Anzeige und als eigene Zusatzgrenze.</summary>
    [JsonPropertyName("adresse_wind")]
    public string AdresseWind { get; set; } = "";

    [JsonPropertyName("adresse_aussen")]
    public string AdresseAussen { get; set; } = "";

    [JsonPropertyName("adresse_innen")]
    public string AdresseInnen { get; set; } = "";

    [JsonPropertyName("adresse_hell_ost")]
    public string AdresseHellOst { get; set; } = "";

    [JsonPropertyName("adresse_hell_sued")]
    public string AdresseHellSued { get; set; } = "";

    [JsonPropertyName("adresse_hell_west")]
    public string AdresseHellWest { get; set; } = "";

    [JsonPropertyName("adresse_azimut")]
    public string AdresseAzimut { get; set; } = "";

    [JsonPropertyName("adresse_elevation")]
    public string AdresseElevation { get; set; } = "";

    // ---- Schalter ----------------------------------------------------------

    [JsonPropertyName("automatik_aktiv")]
    public bool AutomatikAktiv { get; set; } = true;

    [JsonPropertyName("beschattung_aktiv")]
    public bool BeschattungAktiv { get; set; } = true;

    [JsonPropertyName("lueftung_aktiv")]
    public bool LueftungAktiv { get; set; } = true;

    [JsonPropertyName("windschutz_aktiv")]
    public bool WindschutzAktiv { get; set; } = true;

    [JsonPropertyName("regenschutz_aktiv")]
    public bool RegenschutzAktiv { get; set; } = true;

    [JsonPropertyName("frostschutz_aktiv")]
    public bool FrostschutzAktiv { get; set; } = true;

    [JsonPropertyName("zeitschaltuhr_aktiv")]
    public bool ZeitschaltuhrAktiv { get; set; } = true;

    [JsonPropertyName("vorhersage_aktiv")]
    public bool VorhersageAktiv { get; set; } = true;

    // ---- Grenzen -----------------------------------------------------------

    /// <summary>Ab dieser Helligkeit wird beschattet, in Lux.</summary>
    [JsonPropertyName("helligkeitsschwelle")]
    public double Helligkeitsschwelle { get; set; } = 35000;

    /// <summary>Wie lange es hell sein muss, bevor gefahren wird.</summary>
    [JsonPropertyName("einschaltverzoegerung_min")]
    public double EinschaltverzoegerungMinuten { get; set; } = 3;

    /// <summary>
    /// Wie lange es dunkel sein muss, bevor wieder geoeffnet wird.
    ///
    /// Deutlich laenger als das Einschalten: eine einzelne Wolke soll die
    /// Markise nicht ein- und wieder ausfahren. Jede Fahrt kostet Mechanik,
    /// und nichts nervt im Wintergarten mehr als ein Behang, der alle drei
    /// Minuten wandert.
    /// </summary>
    [JsonPropertyName("ausschaltverzoegerung_min")]
    public double AusschaltverzoegerungMinuten { get; set; } = 15;

    /// <summary>Ab dieser Innentemperatur gilt der gesenkte Schwellenwert.</summary>
    [JsonPropertyName("innen_warm")]
    public double InnenWarm { get; set; } = 24;

    /// <summary>Um diesen Faktor sinkt die Helligkeitsschwelle, wenn es drinnen warm ist.</summary>
    [JsonPropertyName("warm_faktor")]
    public double WarmFaktor { get; set; } = 0.7;

    /// <summary>Ab dieser Innentemperatur wird gelueftet.</summary>
    [JsonPropertyName("lueftung_ab")]
    public double LueftungAb { get; set; } = 26;

    /// <summary>Um so viel muss es drinnen wieder kuehler sein, bevor geschlossen wird.</summary>
    [JsonPropertyName("lueftung_hysterese")]
    public double LueftungHysterese { get; set; } = 2;

    /// <summary>So viel kuehler muss es draussen sein, damit Lueften ueberhaupt hilft.</summary>
    [JsonPropertyName("lueftung_unterschied")]
    public double LueftungUnterschied { get; set; } = 2;

    /// <summary>Wie weit die Fenster beim Lueften oeffnen, in Prozent.</summary>
    [JsonPropertyName("lueftungsposition")]
    public double Lueftungsposition { get; set; } = 40;

    /// <summary>Wie lange der Windalarm nach dem Abflauen noch gilt.</summary>
    [JsonPropertyName("wind_nachlauf_min")]
    public double WindNachlaufMinuten { get; set; } = 20;

    /// <summary>Wie lange der Regenschutz nach dem Aufhoeren noch gilt.</summary>
    [JsonPropertyName("regen_nachlauf_min")]
    public double RegenNachlaufMinuten { get; set; } = 15;

    /// <summary>Wie lange die Automatik nach einem Handgriff pausiert.</summary>
    [JsonPropertyName("handsperre_min")]
    public double HandsperreMinuten { get; set; } = 120;

    /// <summary>Wie alt ein Windwert hoechstens sein darf, in Minuten.</summary>
    [JsonPropertyName("hoechstalter_wind_min")]
    public double HoechstalterWindMinuten { get; set; } = 10;

    [JsonPropertyName("hoechstalter_regen_min")]
    public double HoechstalterRegenMinuten { get; set; } = 30;

    [JsonPropertyName("hoechstalter_temperatur_min")]
    public double HoechstalterTemperaturMinuten { get; set; } = 60;

    [JsonPropertyName("hoechstalter_helligkeit_min")]
    public double HoechstalterHelligkeitMinuten { get; set; } = 30;

    /// <summary>Wie oft die Automatik rechnet, in Sekunden.</summary>
    [JsonPropertyName("takt_s")]
    public double TaktSekunden { get; set; } = 20;

    /// <summary>
    /// Wie lange nach einer Fahrt nicht wieder gefahren wird.
    ///
    /// Ohne diese Sperre schickt eine Anlage bei jedem Rechendurchlauf
    /// dasselbe Telegramm, sobald ein Wert um die Schwelle pendelt.
    /// </summary>
    [JsonPropertyName("mindestpause_s")]
    public double MindestpauseSekunden { get; set; } = 30;

    // ---- Abgeleitetes ------------------------------------------------------

    [JsonIgnore]
    public TimeSpan Einschaltverzoegerung => TimeSpan.FromMinutes(EinschaltverzoegerungMinuten);

    [JsonIgnore]
    public TimeSpan Ausschaltverzoegerung => TimeSpan.FromMinutes(AusschaltverzoegerungMinuten);

    [JsonIgnore]
    public TimeSpan WindNachlauf => TimeSpan.FromMinutes(WindNachlaufMinuten);

    [JsonIgnore]
    public TimeSpan RegenNachlauf => TimeSpan.FromMinutes(RegenNachlaufMinuten);

    [JsonIgnore]
    public TimeSpan Handsperre => TimeSpan.FromMinutes(HandsperreMinuten);

    [JsonIgnore]
    public TimeSpan HoechstalterWind => TimeSpan.FromMinutes(HoechstalterWindMinuten);

    [JsonIgnore]
    public TimeSpan HoechstalterRegen => TimeSpan.FromMinutes(HoechstalterRegenMinuten);

    [JsonIgnore]
    public TimeSpan HoechstalterTemperatur => TimeSpan.FromMinutes(HoechstalterTemperaturMinuten);

    [JsonIgnore]
    public TimeSpan HoechstalterHelligkeit => TimeSpan.FromMinutes(HoechstalterHelligkeitMinuten);

    /// <summary>Die zuletzt geholte Vorhersage. Nicht gespeichert - sie ist in Minuten alt.</summary>
    [JsonIgnore]
    public Vorhersage? Vorhersage { get; set; }

    public Motor? Finde(string id)
    {
        foreach (var motor in Motoren)
        {
            if (motor.Id == id) return motor;
        }
        return null;
    }

    /// <summary>
    /// Ein Wintergarten, wie er vorkommt: acht Antriebe, darunter Markise und
    /// Fenster. Als Vorschlag beim ersten Start - abtippen muss man nur noch
    /// die Adressen.
    /// </summary>
    public static Anlage Beispiel()
    {
        var anlage = new Anlage();

        void Dazu(string name, Antriebsart art, double richtung, double wind, bool lueften = false)
        {
            anlage.Motoren.Add(new Motor
            {
                Name = name,
                Art = art,
                Ausrichtung = richtung,
                Windgrenze = wind,
                LueftungAktiv = lueften,
                BeschattungAktiv = art != Antriebsart.Fenster,
                Beschattungsposition = art == Antriebsart.Fenster ? 0 : 100,
            });
        }

        Dazu("Markise Sued", Antriebsart.Markise, 180, 8);
        Dazu("Markise West", Antriebsart.Markise, 270, 8);
        Dazu("Dachbeschattung", Antriebsart.Lamellendach, 180, 12);
        Dazu("Jalousie Ost", Antriebsart.Jalousie, 90, 14);
        Dazu("Jalousie Sued", Antriebsart.Jalousie, 180, 14);
        Dazu("Jalousie West", Antriebsart.Jalousie, 270, 14);
        Dazu("Dachfenster", Antriebsart.Fenster, 180, 6, lueften: true);
        Dazu("Lueftungsfenster Nord", Antriebsart.Fenster, 0, 6, lueften: true);

        return anlage;
    }
}
