namespace REGOwintergarden.Model;

/// <summary>
/// Die gezeichneten Sinnbilder als reine Pfadangaben - Sonne, Regen, Wind,
/// Frost, Markise, Fenster.
///
/// <b>Warum hier und nicht in der Oberflaeche:</b> dieselben Striche werden an
/// zwei Stellen gebraucht - im Windows-Fenster als WPF-Pfad und auf der
/// Webseite als SVG. Beide Male dieselbe Syntax, denn WPF hat sie von SVG
/// uebernommen. Zwei Kopien derselben Zeichnung wuerden mit der Zeit
/// auseinander laufen, und dann sieht dieselbe Anlage in zwei Fenstern
/// verschieden aus.
///
/// Alle Pfade liegen in einem Feld von 24 auf 24. Wer einen hinzufuegt,
/// zeichnet in dasselbe Feld - sonst passen die Groessen nicht mehr zusammen.
/// Bewusst nur Striche und keine Flaechen: ein Strichsymbol bleibt auch klein
/// noch erkennbar, und die Farbe kann der Zustand bestimmen.
/// </summary>
public static class Sinnbilder
{
    public const string Sonne =
        "M12 6 A6 6 0 1 1 11.99 6 Z "
        + "M12 0.5 L12 3.5 M12 20.5 L12 23.5 M0.5 12 L3.5 12 M20.5 12 L23.5 12 "
        + "M3.9 3.9 L6 6 M18 18 L20.1 20.1 M20.1 3.9 L18 6 M6 18 L3.9 20.1";

    public const string Wolke =
        "M6.5 17 A4 4 0 0 1 6.8 9 A5.5 5.5 0 0 1 17.4 8.4 A3.8 3.8 0 0 1 17.5 17 Z";

    public const string Regen =
        "M6.5 13 A4 4 0 0 1 6.8 5 A5.5 5.5 0 0 1 17.4 4.4 A3.8 3.8 0 0 1 17.5 13 Z "
        + "M8 16 L6.5 20 M12 16 L10.5 20 M16 16 L14.5 20";

    public const string Wind =
        "M2 8 L13 8 A2.6 2.6 0 1 0 10.5 5 "
        + "M2 12.5 L17 12.5 A2.8 2.8 0 1 1 14.4 16.5 "
        + "M2 17 L9.5 17";

    public const string Frost =
        "M12 2 L12 22 M3.3 7 L20.7 17 M3.3 17 L20.7 7 "
        + "M12 2 L9.8 4.6 M12 2 L14.2 4.6 M12 22 L9.8 19.4 M12 22 L14.2 19.4 "
        + "M3.3 7 L4.2 10.2 M3.3 7 L6.6 6.7 M20.7 17 L17.4 17.3 M20.7 17 L19.8 13.8 "
        + "M3.3 17 L6.6 17.3 M3.3 17 L4.2 13.8 M20.7 7 L19.8 10.2 M20.7 7 L17.4 6.7";

    public const string Markise =
        "M2 6 L22 6 M3 6 L6 14 L18 14 L21 6 "
        + "M6.6 14 L7.6 16 M9.6 14 L10.6 16 M12.6 14 L13.6 16 M15.6 14 L16.6 16 "
        + "M12 16 L12 21";

    public const string Fenster =
        "M4 3 L14 3 L14 21 L4 21 Z M4 12 L14 12 "
        + "M14 3 L21 6 L21 18 L14 21";

    public const string Jalousie =
        "M3 3 L21 3 L21 20 L3 20 Z M3 7 L21 7 M3 11 L21 11 M3 15 L21 15";

    public const string Lamellendach =
        "M2 19 L8 5 L22 5 L16 19 Z M4.6 13 L18.6 13 M6.3 9 L20.3 9";

    public const string Uhr =
        "M12 2.5 A9.5 9.5 0 1 1 11.99 2.5 Z M12 6.5 L12 12 L16 14.5";

    public const string Warnung =
        "M12 2.5 L22.5 21 L1.5 21 Z M12 9 L12 15 M12 17.6 L12 18.4";

    public const string Haus =
        "M3 11 L12 3.5 L21 11 M5.5 9.5 L5.5 20.5 L18.5 20.5 L18.5 9.5";

    public const string Thermometer =
        "M12 3.5 A2.5 2.5 0 0 1 14.5 6 L14.5 14 A4.5 4.5 0 1 1 9.5 14 L9.5 6 "
        + "A2.5 2.5 0 0 1 12 3.5 Z M12 8 L12 15";

    /// <summary>Das Sinnbild einer Antriebsart.</summary>
    public static string FuerArt(Antriebsart art) => art switch
    {
        Antriebsart.Markise => Markise,
        Antriebsart.Fenster => Fenster,
        Antriebsart.Lamellendach => Lamellendach,
        _ => Jalousie,
    };

    /// <summary>Das Sinnbild einer Stufe - warum ein Antrieb steht, wo er steht.</summary>
    public static string FuerStufe(Stufe stufe) => stufe switch
    {
        Stufe.Wind => Wind,
        Stufe.Regen => Regen,
        Stufe.Frost => Frost,
        Stufe.Beschattung => Sonne,
        Stufe.Lueftung => Fenster,
        Stufe.Zeit => Uhr,
        Stufe.Hand => Haus,
        _ => Sonne,
    };
}
