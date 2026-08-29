using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using REGOwintergarden.App;
using REGOwintergarden.Model;

namespace REGOwintergarden.Ui;

/// <summary>
/// Die Startseite: Wetter, Sonne, Antriebe - und zu jedem Antrieb der
/// <b>Grund</b>, warum er dort steht, wo er steht.
///
/// Das ist der Anspruch dieser Seite: sie soll jemand verstehen, der die
/// Anlage nicht gebaut hat. „Markise Sued: eingefahren, weil Wind 11 m/s ueber
/// der Grenze von 8" beantwortet die Frage, die morgens gestellt wird.
/// „Position 0 %" beantwortet sie nicht.
/// </summary>
public sealed class Uebersicht : UserControl
{
    private readonly Wintergartendienst _dienst;
    private readonly Window _besitzer;

    private readonly WrapPanel _leuchten = new();
    private readonly Kompass _kompass = new();
    private readonly WrapPanel _kacheln = new();
    private readonly TextBlock _kopfzeile = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Border _band = new();
    private readonly TextBlock _ueberschrift = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _erklaerung = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ContentControl _bandbild = new();
    private readonly StackPanel _vorschau = new() { Width = 300 };
    private readonly TextBlock _vorhersage = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _naechste = new() { TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _automatik = new() { Content = "Automatik eingeschaltet" };

    private readonly Symbole.Leuchte _anschluss = new(Symbole.Warnung, "KNX-Bus");
    private readonly Symbole.Leuchte _wind = new(Symbole.Wind, "Wind");
    private readonly Symbole.Leuchte _regen = new(Symbole.Regen, "Regen");
    private readonly Symbole.Leuchte _aussen = new(Symbole.Thermometer, "draussen");
    private readonly Symbole.Leuchte _innen = new(Symbole.Haus, "drinnen");
    private readonly Symbole.Leuchte _hell = new(Symbole.Sonne, "Helligkeit");
    private readonly Symbole.Leuchte _ausgabe = new(Symbole.Warnung, "an die Aktoren");

    private bool _fuellt;

    public Uebersicht(Wintergartendienst dienst, Window besitzer)
    {
        _dienst = dienst;
        _besitzer = besitzer;

        Content = Aufbau();
        Auffrischen();

        _dienst.Aufgefrischt += () => Dispatcher.BeginInvoke(new Action(Auffrischen));
    }

    /// <summary>Etwas hat sich geaendert, das gespeichert gehoert.</summary>
    public event Action? Gespeichert;

    private Brush Farbe(string name) => (Brush)Application.Current.Resources[name];

    private UIElement Aufbau()
    {
        // Ein Raster statt einer Rolle: Statusband, Wetterzeile und darunter
        // der Rest, der sich den Platz teilt. So passt die Seite auf einen
        // Bildschirm - und wer den Wintergarten bedient, soll nicht scrollen
        // muessen, um zu sehen, ob die Markise draussen ist.
        var aussen = new Grid { Margin = new Thickness(12) };
        aussen.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        aussen.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        aussen.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var spalte = new StackPanel();

        // Das Statusband: ein Satz, der sagt, was gerade gilt - und einer,
        // der ihn erklaert. Das ist die erste Zeile, die jemand liest, und
        // sie soll ohne Vorkenntnis verstaendlich sein.
        _ueberschrift.FontSize = 24;
        _ueberschrift.FontWeight = FontWeights.SemiBold;
        _erklaerung.Style = (Style)Application.Current.Resources["Hinweis"];
        _erklaerung.Margin = new Thickness(0, 4, 0, 0);
        _erklaerung.FontSize = 13;

        var bandtext = new StackPanel();
        bandtext.Children.Add(_ueberschrift);
        bandtext.Children.Add(_erklaerung);

        var band = new DockPanel { LastChildFill = true };
        _bandbild.Margin = new Thickness(0, 0, 14, 0);
        _bandbild.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(_bandbild, Dock.Left);
        band.Children.Add(_bandbild);
        band.Children.Add(bandtext);

        _band.Style = (Style)Application.Current.Resources["Gruppenkarte"];
        _band.Margin = new Thickness(0, 0, 0, 12);
        _band.Child = band;
        spalte.Children.Add(_band);

        _kopfzeile.Style = (Style)Application.Current.Resources["Hinweis"];
        _kopfzeile.Margin = new Thickness(0, 6, 0, 0);
        bandtext.Children.Add(_kopfzeile);

        _automatik.Margin = new Thickness(0, 0, 0, 8);
        _automatik.HorizontalAlignment = HorizontalAlignment.Right;
        _automatik.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(_automatik, Dock.Right);
        band.Children.Insert(1, _automatik);
        _automatik.ToolTip = "Aus heisst: es wird nichts von selbst gefahren - auch kein Wind- oder "
                             + "Regenschutz.";
        _automatik.Click += (_, _) =>
        {
            if (_fuellt) return;
            _dienst.Anlage.AutomatikAktiv = _automatik.IsChecked == true;
            _dienst.Melden("Automatik", _dienst.Anlage.AutomatikAktiv ? "eingeschaltet" : "ausgeschaltet");
            Gespeichert?.Invoke();
            Auffrischen();
        };

        // Vorne die Verbindung: alle anderen Leuchten zeigen Messwerte, und
        // fehlt der Bus, sind die nicht falsch, sondern gar nicht da. Eine
        // Anlage, die stillsteht, weil das Gateway aus ist, saehe sonst
        // genauso ruhig aus wie eine, bei der alles stimmt.
        _leuchten.Children.Add(_anschluss);
        _leuchten.Children.Add(_wind);
        _leuchten.Children.Add(_regen);
        _leuchten.Children.Add(_aussen);
        _leuchten.Children.Add(_innen);
        _leuchten.Children.Add(_hell);
        _leuchten.Children.Add(_ausgabe);

        // Sonne und Antriebe nebeneinander: der Kompass erklaert die Kacheln,
        // und die Kacheln erklaeren den Kompass.
        var reihe = new Grid();
        reihe.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        reihe.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var links = new StackPanel();
        links.Children.Add(new TextBlock
        {
            Text = "Sonne",
            Style = (Style)Application.Current.Resources["Ueberschrift"],
            Margin = new Thickness(0, 0, 0, 4),
        });
        links.Children.Add(new Border
        {
            Style = (Style)Application.Current.Resources["Gruppenkarte"],
            Margin = new Thickness(0, 0, 12, 12),
            Child = _kompass,
        });

        // Was als Naechstes von selbst passiert. Eine Steuerung, die nur
        // ihren Zustand zeigt, wirkt willkuerlich - eine, die ihre Absicht
        // zeigt, wird nachvollziehbar.
        links.Children.Add(new TextBlock
        {
            Text = "Als Naechstes",
            Style = (Style)Application.Current.Resources["Ueberschrift"],
            Margin = new Thickness(0, 0, 0, 4),
        });
        links.Children.Add(new Border
        {
            Style = (Style)Application.Current.Resources["Gruppenkarte"],
            Margin = new Thickness(0, 0, 12, 12),
            Child = _vorschau,
        });

        _naechste.Style = (Style)Application.Current.Resources["Hinweis"];
        _naechste.Margin = new Thickness(0, 0, 12, 0);
        links.Children.Add(_naechste);
        Grid.SetColumn(links, 0);
        reihe.Children.Add(links);

        var rechts = new DockPanel { LastChildFill = true };
        var antriebskopf = new TextBlock
        {
            Text = "Antriebe",
            Style = (Style)Application.Current.Resources["Ueberschrift"],
            Margin = new Thickness(0, 0, 0, 4),
        };
        DockPanel.SetDock(antriebskopf, Dock.Top);
        rechts.Children.Add(antriebskopf);
        rechts.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _kacheln,
        });
        Grid.SetColumn(rechts, 1);
        reihe.Children.Add(rechts);

        Grid.SetRow(spalte, 0);
        aussen.Children.Add(spalte);

        Grid.SetRow(_leuchten, 1);
        aussen.Children.Add(_leuchten);

        Grid.SetRow(reihe, 2);
        aussen.Children.Add(reihe);
        return aussen;
    }

    // ---- Auffrischen -------------------------------------------------------

    public void Auffrischen()
    {
        if (_fuellt) return;
        _fuellt = true;
        try
        {
            var jetzt = DateTime.Now;
            var anlage = _dienst.Anlage;
            var wetter = _dienst.Wetter();
            var sonne = _dienst.Sonne;
            var lagen = _dienst.Lagen;

            _automatik.IsChecked = anlage.AutomatikAktiv;
            _kopfzeile.Text = anlage.Name + " · " + anlage.Motoren.Count.ToString(CultureInfo.CurrentCulture)
                              + " Antriebe · Sonnenstand " + _dienst.Sonnenquelle
                              + " · " + Busstand();

            Statusband(anlage, lagen, wetter, sonne, jetzt);
            Vorschau(anlage, lagen, jetzt);

            Leuchten(anlage, wetter, jetzt);
            _vorhersage.Text = anlage.Vorhersage is { } sicht
                ? "Vorhersage (" + sicht.Quelle + "): " + sicht
                : "Keine Vorhersage geholt.";

            _kompass.Zeigen(anlage.Motoren, lagen, sonne);
            _naechste.Text = "Naechste Schaltzeit: "
                             + Zeitschaltuhr.NaechsteText(anlage,
                                 zeit => Astro.Berechnen(zeit, anlage.Breite, anlage.Laenge), jetzt);

            Kacheln(lagen);
        }
        finally
        {
            _fuellt = false;
        }
    }

    /// <summary>Ob ueberhaupt gefahren werden kann - in einem Halbsatz.</summary>
    private string Busstand() => _dienst.Stand switch
    {
        App.Busstand.Verbunden => "mit dem Bus verbunden",
        App.Busstand.Verbinde => "verbindet sich gerade",
        App.Busstand.Fehler => "keine Busverbindung",
        _ => "nicht mit dem Bus verbunden",
    };

    /// <summary>
    /// Das Statusband: was gerade gilt und was das heisst.
    ///
    /// Die Farbe traegt die halbe Auskunft - rot heisst „etwas haelt gerade
    /// fest", gruen heisst „es passiert etwas", grau heisst „nichts zu tun".
    /// </summary>
    private void Statusband(Anlage anlage, IReadOnlyList<Lage> lagen, Wetterlage wetter,
        Sonnenstand sonne, DateTime jetzt)
    {
        var (text, ton) = Lagebericht.Ueberschrift(anlage, lagen);
        _ueberschrift.Text = text;
        _erklaerung.Text = Lagebericht.Erklaerung(anlage, lagen, wetter, sonne, jetzt);

        var farbe = ton switch
        {
            Lagebericht.Ton.Warnung => Farbe("Fehler"),
            Lagebericht.Ton.Taetig => Farbe("Betont"),
            _ => Farbe("Nebenschrift"),
        };
        _ueberschrift.Foreground = farbe;
        _band.BorderBrush = ton == Lagebericht.Ton.Ruhig ? Farbe("Linie") : farbe;
        _band.BorderThickness = new Thickness(ton == Lagebericht.Ton.Ruhig ? 1 : 2);

        var bild = ton switch
        {
            Lagebericht.Ton.Warnung => Symbole.Warnung,
            Lagebericht.Ton.Taetig => Symbole.Sonne,
            _ => Symbole.Haus,
        };
        _bandbild.Content = Symbole.Zeichnen(bild, farbe, 40, 1.8);
    }

    /// <summary>Was als Naechstes von selbst passiert.</summary>
    private void Vorschau(Anlage anlage, IReadOnlyList<Lage> lagen, DateTime jetzt)
    {
        _vorschau.Children.Clear();

        var punkte = Lagebericht.Naechstes(anlage, lagen,
            zeit => Astro.Berechnen(zeit, anlage.Breite, anlage.Laenge), jetzt);

        if (punkte.Count == 0)
        {
            _vorschau.Children.Add(new TextBlock
            {
                Text = "Nichts angekuendigt. Was als Naechstes geschieht, entscheidet das Wetter.",
                Style = (Style)Application.Current.Resources["Hinweis"],
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        foreach (var punkt in punkte)
        {
            var zeile = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            zeile.Children.Add(new TextBlock
            {
                Text = punkt.In(jetzt) + "  ·  " + punkt.Uhrzeit,
                FontWeight = FontWeights.SemiBold,
                Foreground = Farbe("Betont"),
                FontSize = 12,
            });
            zeile.Children.Add(new TextBlock
            {
                Text = punkt.Antrieb + ": " + punkt.Was,
                Style = (Style)Application.Current.Resources["Hinweis"],
                TextWrapping = TextWrapping.Wrap,
            });
            _vorschau.Children.Add(zeile);
        }
    }

    private void Leuchten(Anlage anlage, Wetterlage wetter, DateTime jetzt)
    {
        // Bewertet wird im Kern, damit Fenster und Browser dasselbe sagen -
        // zwei Bewertungen derselben Frage widersprechen sich frueher oder
        // spaeter, und dann weiss niemand, welcher zu glauben ist.
        var anschluss = Anschlussbild.Bilden(_dienst);
        _anschluss.Beschriftung = anschluss.Name;
        _anschluss.Zeigen(anschluss.Wert, anschluss.Alarm, anschluss.Bekannt);
        _anschluss.ToolTip = anschluss.Erklaerung + "\n"
                             + Anschlussbild.Taktalter(_dienst, jetzt);

        // Wind: drei Zustaende. Ein fehlender Wert ist kein Windstille-Wert,
        // und die Anzeige sagt das auch so.
        //
        // Der Alarm kommt als Bit von der Wetterstation - er zaehlt. Die
        // Geschwindigkeit steht daneben, weil eine Zahl mehr sagt als ein
        // Bit: „Windalarm" beruhigt niemanden, „Windalarm, 14 m/s" schon.
        var alarmbit = wetter.Windalarm;
        var alarmFrisch = alarmbit is not null && alarmbit.Value.IstFrisch(jetzt, anlage.HoechstalterWind);
        var windwert = wetter.Wind;
        var windFrisch = windwert is not null && windwert.Value.IstFrisch(jetzt, anlage.HoechstalterWind);

        if (alarmFrisch || windFrisch)
        {
            var alarm = alarmFrisch && alarmbit!.Value.Wert > 0.5;
            if (!alarm && windFrisch)
            {
                foreach (var motor in anlage.Motoren)
                {
                    if (motor.Sicherheitsposition is not null && windwert!.Value.Wert >= motor.Windgrenze)
                    {
                        alarm = true;
                    }
                }
            }

            var text = windFrisch ? Zahl(windwert!.Value.Wert) + " m/s" : alarm ? "Alarm" : "ruhig";
            if (alarm && windFrisch) text += "  Alarm";
            _wind.Zeigen(text, alarm);
        }
        else
        {
            _wind.Zeigen(alarmbit is null && windwert is null ? "kein Wert" : "veraltet", true, bekannt: false);
        }

        if (wetter.Regen is { } regen && regen.IstFrisch(jetzt, anlage.HoechstalterRegen))
        {
            _regen.Zeigen(regen.Wert > 0.5 ? "es regnet" : "trocken", regen.Wert > 0.5);
        }
        else
        {
            _regen.Zeigen("kein Wert", false, bekannt: false);
        }

        Temperatur(_aussen, wetter.Aussen, anlage, jetzt, frost: true);
        Temperatur(_innen, wetter.Innen, anlage, jetzt, frost: false);

        // Was dieses Programm selbst an die Aktoren meldet. Es steht neben
        // den Messwerten und nicht in der Konfiguration: es ist der Wert, an
        // dem die ganze Anlage haengt, und man soll ihn sehen, ohne danach zu
        // suchen.
        var sicherheit = _dienst.Sicherheitslage;
        var alter = _dienst.LetzteAusgabe == DateTime.MinValue
            ? "noch nichts gesendet"
            : "zuletzt " + _dienst.LetzteAusgabe.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        var ziel = anlage.AdresseWindausgabe.Trim().Length > 0 || anlage.AdresseRegenausgabe.Trim().Length > 0;
        if (!ziel)
        {
            _ausgabe.Zeigen("nicht eingerichtet", false, bekannt: false);
        }
        else
        {
            _ausgabe.Zeigen(
                sicherheit.Wind && sicherheit.Regen ? "Wind + Regen"
                : sicherheit.Wind ? "Windalarm"
                : sicherheit.Regen ? "Regen"
                : "ruhig",
                sicherheit.Alarm);
            _ausgabe.ToolTip = sicherheit.Grund + "\n" + alter
                               + "\nWiederholung alle "
                               + Zahl(anlage.AusgabetaktSekunden) + " s";
        }

        var hell = wetter.HellsteRichtung();
        if (hell is { } wert && wert.IstFrisch(jetzt, anlage.HoechstalterHelligkeit))
        {
            _hell.Zeigen(Lux(wert.Wert), false);
        }
        else
        {
            _hell.Zeigen("kein Wert", false, bekannt: false);
        }
    }

    private void Temperatur(Symbole.Leuchte leuchte, Messwert? wert, Anlage anlage, DateTime jetzt, bool frost)
    {
        if (wert is not { } messwert || !messwert.IstFrisch(jetzt, anlage.HoechstalterTemperatur))
        {
            leuchte.Zeigen("kein Wert", false, bekannt: false);
            return;
        }

        var alarm = frost
            ? messwert.Wert <= 3
            : messwert.Wert >= anlage.LueftungAb;
        leuchte.Zeigen(Zahl(messwert.Wert) + " °C", alarm);
    }

    private void Kacheln(IReadOnlyList<Lage> lagen)
    {
        _kacheln.Children.Clear();
        if (lagen.Count == 0)
        {
            _kacheln.Children.Add(new TextBlock
            {
                Text = "Noch kein Antrieb eingerichtet - im Reiter Antriebe anlegen.",
                Style = (Style)Application.Current.Resources["Hinweis"],
            });
            return;
        }
        foreach (var lage in lagen) _kacheln.Children.Add(Kachel(lage));
    }

    private UIElement Kachel(Lage lage)
    {
        var motor = lage.Motor;
        var spalte = new StackPanel { Width = 260 };

        // Kopf: Sinnbild der Art, Name, Richtung.
        var kopf = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
        var bild = Symbole.Zeichnen(Symbole.FuerArt(motor.Art), Farbe("Nebenschrift"), 26);
        bild.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(bild, Dock.Left);
        kopf.Children.Add(bild);

        var titel = new StackPanel();
        titel.Children.Add(new TextBlock
        {
            Text = motor.Name,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Farbe("Schrift"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        titel.Children.Add(new TextBlock
        {
            Text = motor.Art + " · " + motor.Richtung + " "
                   + Math.Round(motor.Ausrichtung).ToString("0", CultureInfo.CurrentCulture) + "°",
            Style = (Style)Application.Current.Resources["Hinweis"],
        });
        kopf.Children.Add(titel);
        spalte.Children.Add(kopf);

        // Die Position, gross - und daneben, woher sie kommt.
        var stand = Position(motor);
        var wertzeile = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
        var zahl = new TextBlock
        {
            Text = stand is null ? "—" : Math.Round(stand.Value).ToString("0", CultureInfo.CurrentCulture) + " %",
            FontSize = 24,
            Foreground = Farbe("Schrift"),
        };
        DockPanel.SetDock(zahl, Dock.Left);
        wertzeile.Children.Add(zahl);

        var stufenbild = Symbole.Zeichnen(Symbole.FuerStufe(lage.Stufe), StufenFarbe(lage.Stufe), 22);
        stufenbild.HorizontalAlignment = HorizontalAlignment.Right;
        stufenbild.VerticalAlignment = VerticalAlignment.Bottom;
        wertzeile.Children.Add(stufenbild);
        spalte.Children.Add(wertzeile);

        // Der Grund. Das ist die wichtigste Zeile der ganzen Seite.
        spalte.Children.Add(new TextBlock
        {
            Text = Stufentext(lage.Stufe) + lage.Grund,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = StufenFarbe(lage.Stufe),
            Margin = new Thickness(0, 0, 0, 8),
        });

        // Von Hand: drei Knoepfe. Sie halten die Automatik zurueck - das steht
        // danach im Grund, damit niemand das Programm fuer kaputt haelt.
        var knoepfe = new StackPanel { Orientation = Orientation.Horizontal };
        knoepfe.Children.Add(Knopf("Auf", async () => await _dienst.BefehlAsync(motor, "auf")));
        knoepfe.Children.Add(Knopf("Stopp", async () => await _dienst.BefehlAsync(motor, "stopp")));
        knoepfe.Children.Add(Knopf("Ab", async () => await _dienst.BefehlAsync(motor, "ab")));
        spalte.Children.Add(knoepfe);

        return new Border
        {
            Style = (Style)Application.Current.Resources["Gruppenkarte"],
            Margin = new Thickness(0, 0, 12, 12),
            BorderBrush = lage.Stufe >= Stufe.Frost ? Farbe("Fehler") : Farbe("Linie"),
            BorderThickness = new Thickness(lage.Stufe >= Stufe.Frost ? 2 : 1),
            Child = spalte,
        };
    }

    private Button Knopf(string text, Func<System.Threading.Tasks.Task> was)
    {
        var knopf = new Button
        {
            Content = text,
            Style = (Style)Application.Current.Resources["Knopf"],
            MinWidth = 0,
            Width = 72,
        };
        knopf.Click += async (_, _) =>
        {
            knopf.IsEnabled = false;
            try { await was(); }
            finally { knopf.IsEnabled = true; }
        };
        return knopf;
    }

    private double? Position(Motor motor)
    {
        var roh = _dienst.Roh(motor.AdressePositionStatus.Length > 0
            ? motor.AdressePositionStatus
            : motor.AdressePosition);
        return roh is null ? null : Wintergartendienst.Zahl("5.001", roh);
    }

    private Brush StufenFarbe(Stufe stufe) => stufe switch
    {
        Stufe.Wind or Stufe.Regen or Stufe.Frost => Farbe("Fehler"),
        Stufe.Beschattung or Stufe.Lueftung => Farbe("Betont"),
        Stufe.Hand => Farbe("Schrift"),
        _ => Farbe("Nebenschrift"),
    };

    private static string Stufentext(Stufe stufe) => stufe switch
    {
        Stufe.Wind => "Windschutz: ",
        Stufe.Regen => "Regenschutz: ",
        Stufe.Frost => "Frostschutz: ",
        Stufe.Beschattung => "Beschattung: ",
        Stufe.Lueftung => "Lueftung: ",
        Stufe.Zeit => "Zeitschaltuhr: ",
        Stufe.Hand => "Hand: ",
        _ => "",
    };

    private static string Zahl(double wert) => wert.ToString("0.#", CultureInfo.CurrentCulture);

    private static string Lux(double wert) => wert >= 1000
        ? (wert / 1000).ToString("0.#", CultureInfo.CurrentCulture) + " kLux"
        : wert.ToString("0", CultureInfo.CurrentCulture) + " Lux";
}
