using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using REGOwintergarden.Model;

namespace REGOwintergarden.App;

/// <summary>Ein aufgezeichneter Zeitpunkt mit allem, was gerade galt.</summary>
public sealed record Messpunkt(
    DateTime Zeit,
    double? Innen,
    double? Aussen,
    double? Wind,
    double? Helligkeit,
    bool Regen,
    bool Windalarm);

/// <summary>Ein Ereignis auf der Zeitachse - was die Steuerung getan hat.</summary>
public sealed record Ereignis(DateTime Zeit, string Antrieb, Stufe Stufe, string Grund, double? Ziel);

/// <summary>
/// Die Aufzeichnung: Messwerte und Ereignisse ueber die Zeit.
///
/// <b>Warum als Datei und nicht im Arbeitsspeicher:</b> ein Langzeittrend ist
/// erst einer, wenn er einen Neustart ueberlebt. Und wenn im August die Frage
/// kommt, warum die Markise am Dienstag nicht ausgefahren ist, hilft nur eine
/// Aufzeichnung, die den Dienstag noch kennt.
///
/// Das Format ist Text mit Strichpunkten - eine Zeile je Zeitpunkt, eine
/// Datei je Monat. Das laesst sich mit jedem Tabellenprogramm oeffnen,
/// braucht keine Datenbank und ist in zehn Jahren noch lesbar. Ein leeres
/// Feld heisst „dazu war nichts bekannt" und ist etwas anderes als eine Null.
/// </summary>
public sealed class Aufzeichnung
{
    private readonly string _ordner;
    private DateTime _zuletzt = DateTime.MinValue;

    public Aufzeichnung(string ordner)
    {
        _ordner = Path.Combine(ordner, "verlauf");
        Directory.CreateDirectory(_ordner);
    }

    /// <summary>Wie dicht aufgezeichnet wird.</summary>
    public TimeSpan Abstand { get; set; } = TimeSpan.FromMinutes(1);

    private string Messdatei(DateTime zeit) =>
        Path.Combine(_ordner, "messwerte-" + zeit.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".csv");

    private string Ereignisdatei(DateTime zeit) =>
        Path.Combine(_ordner, "ereignisse-" + zeit.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".csv");

    /// <summary>
    /// Schreibt einen Messpunkt - hoechstens einmal je <see cref="Abstand"/>.
    ///
    /// Der Takt der Automatik ist zwanzig Sekunden; jede Runde zu schreiben
    /// ergaebe im Jahr eineinhalb Millionen Zeilen, ohne dass ein Trend
    /// dadurch besser wuerde.
    /// </summary>
    public void Merken(Wetterlage wetter, DateTime jetzt)
    {
        if (jetzt - _zuletzt < Abstand) return;
        _zuletzt = jetzt;

        var zeile = string.Join(";",
            jetzt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Wert(wetter.Innen),
            Wert(wetter.Aussen),
            Wert(wetter.Wind),
            Wert(wetter.HellsteRichtung()),
            Bit(wetter.Regen),
            Bit(wetter.Windalarm));

        Anhaengen(Messdatei(jetzt), "zeit;innen;aussen;wind;helligkeit;regen;windalarm", zeile);
    }

    /// <summary>Schreibt ein Ereignis - eine Aenderung, die die Steuerung veranlasst hat.</summary>
    public void Merken(Ereignis ereignis)
    {
        var zeile = string.Join(";",
            ereignis.Zeit.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Sauber(ereignis.Antrieb),
            ereignis.Stufe.ToString(),
            ereignis.Ziel is null ? "" : ereignis.Ziel.Value.ToString("0", CultureInfo.InvariantCulture),
            Sauber(ereignis.Grund));

        Anhaengen(Ereignisdatei(ereignis.Zeit), "zeit;antrieb;stufe;ziel;grund", zeile);
    }

    private static string Wert(Messwert? wert) =>
        wert is null ? "" : wert.Value.Wert.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Bit(Messwert? wert) => wert is null ? "" : wert.Value.Wert > 0.5 ? "1" : "0";

    /// <summary>Strichpunkte und Zeilenumbrueche heraus - sie wuerden das Format zerlegen.</summary>
    private static string Sauber(string text) =>
        text.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');

    private void Anhaengen(string pfad, string kopf, string zeile)
    {
        try
        {
            if (!File.Exists(pfad)) File.WriteAllText(pfad, kopf + Environment.NewLine);
            File.AppendAllText(pfad, zeile + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Eine Aufzeichnung, die sich nicht schreiben laesst, darf die
            // Steuerung nicht anhalten.
        }
    }

    // ---- Lesen -------------------------------------------------------------

    /// <summary>Alle Messpunkte eines Zeitraums, nach Zeit sortiert.</summary>
    public IReadOnlyList<Messpunkt> Messwerte(DateTime von, DateTime bis)
    {
        var liste = new List<Messpunkt>();
        foreach (var pfad in Dateien("messwerte-", von, bis))
        {
            foreach (var zeile in Zeilen(pfad))
            {
                var teile = zeile.Split(';');
                if (teile.Length < 7) continue;
                if (!Zeit(teile[0], out var zeit) || zeit < von || zeit > bis) continue;

                liste.Add(new Messpunkt(zeit,
                    Zahl(teile[1]), Zahl(teile[2]), Zahl(teile[3]), Zahl(teile[4]),
                    teile[5] == "1", teile[6] == "1"));
            }
        }
        liste.Sort((a, b) => a.Zeit.CompareTo(b.Zeit));
        return liste;
    }

    /// <summary>Alle Ereignisse eines Zeitraums.</summary>
    public IReadOnlyList<Ereignis> Ereignisse(DateTime von, DateTime bis)
    {
        var liste = new List<Ereignis>();
        foreach (var pfad in Dateien("ereignisse-", von, bis))
        {
            foreach (var zeile in Zeilen(pfad))
            {
                var teile = zeile.Split(';');
                if (teile.Length < 5) continue;
                if (!Zeit(teile[0], out var zeit) || zeit < von || zeit > bis) continue;
                if (!Enum.TryParse<Stufe>(teile[2], out var stufe)) continue;

                liste.Add(new Ereignis(zeit, teile[1], stufe, teile[4], Zahl(teile[3])));
            }
        }
        liste.Sort((a, b) => a.Zeit.CompareTo(b.Zeit));
        return liste;
    }

    private IEnumerable<string> Dateien(string beginn, DateTime von, DateTime bis)
    {
        // Eine Datei je Monat - also alle Monate des Zeitraums durchgehen.
        var monat = new DateTime(von.Year, von.Month, 1);
        while (monat <= bis)
        {
            var pfad = Path.Combine(_ordner,
                beginn + monat.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".csv");
            if (File.Exists(pfad)) yield return pfad;
            monat = monat.AddMonths(1);
        }
    }

    private static IEnumerable<string> Zeilen(string pfad)
    {
        string[] zeilen;
        try { zeilen = File.ReadAllLines(pfad); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { yield break; }

        foreach (var zeile in zeilen)
        {
            if (zeile.Length == 0 || zeile.StartsWith("zeit;", StringComparison.Ordinal)) continue;
            yield return zeile;
        }
    }

    private static bool Zeit(string text, out DateTime zeit) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out zeit);

    private static double? Zahl(string text) =>
        text.Length == 0 ? null
        : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var wert) ? wert
        : null;

    /// <summary>
    /// Duennt eine Reihe auf hoechstens so viele Punkte aus.
    ///
    /// Ein Monat mit Minutenwerten sind vierzigtausend Punkte auf tausend
    /// Bildpunkte Breite - vierzig davon liegen uebereinander. Genommen wird
    /// je Abschnitt der Mittelwert, damit ein Ausreisser nicht die ganze
    /// Kurve traegt, aber die Spitzen bleiben als Hoechst- und Tiefstwert im
    /// Kopf der Anzeige erhalten.
    /// </summary>
    public static IReadOnlyList<Messpunkt> Ausduennen(IReadOnlyList<Messpunkt> punkte, int hoechstens)
    {
        if (punkte.Count <= hoechstens || hoechstens < 2) return punkte;

        var ergebnis = new List<Messpunkt>(hoechstens);
        var breite = (double)punkte.Count / hoechstens;

        for (var i = 0; i < hoechstens; i++)
        {
            var von = (int)(i * breite);
            var bis = Math.Min(punkte.Count, (int)((i + 1) * breite));
            if (bis <= von) continue;

            double? innen = null, aussen = null, wind = null, hell = null;
            var regen = false;
            var alarm = false;
            var mitte = punkte[(von + bis) / 2].Zeit;

            innen = Mittel(punkte, von, bis, p => p.Innen);
            aussen = Mittel(punkte, von, bis, p => p.Aussen);
            wind = Mittel(punkte, von, bis, p => p.Wind);
            hell = Mittel(punkte, von, bis, p => p.Helligkeit);

            for (var j = von; j < bis; j++)
            {
                // Bei Regen und Alarm zaehlt das Vorkommen, nicht der
                // Mittelwert: ein Schauer von fuenf Minuten darf in der
                // Monatsansicht nicht verschwinden.
                if (punkte[j].Regen) regen = true;
                if (punkte[j].Windalarm) alarm = true;
            }

            ergebnis.Add(new Messpunkt(mitte, innen, aussen, wind, hell, regen, alarm));
        }
        return ergebnis;
    }

    private static double? Mittel(IReadOnlyList<Messpunkt> punkte, int von, int bis,
        Func<Messpunkt, double?> welcher)
    {
        var summe = 0.0;
        var anzahl = 0;
        for (var i = von; i < bis; i++)
        {
            if (welcher(punkte[i]) is not { } wert) continue;
            summe += wert;
            anzahl++;
        }
        return anzahl == 0 ? null : summe / anzahl;
    }
}
