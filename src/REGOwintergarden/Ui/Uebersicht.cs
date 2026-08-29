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
    private readonly TextBlock _vorhersage = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _naechste = new() { TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _automatik = new() { Content = "Automatik eingeschaltet" };

    private readonly Symbole.Leuchte _wind = new(Symbole.Wind, "Wind");
    private readonly Symbole.Leuchte _regen = new(Symbole.Regen, "Regen");
    private readonly Symbole.Leuchte _aussen = new(Symbole.Thermometer, "draussen");
    private readonly Symbole.Leuchte _innen = new(Symbole.Haus, "drinnen");
    private readonly Symbole.Leuchte _hell = new(Symbole.Sonne, "Helligkeit");

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
        var aussen = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(12),
        };
        var spalte = new StackPanel();

        _kopfzeile.FontSize = 15;
        _kopfzeile.Foreground = Farbe("Schrift");
        _kopfzeile.Margin = new Thickness(0, 0, 0, 4);
        spalte.Children.Add(_kopfzeile);

        _automatik.Margin = new Thickness(0, 0, 0, 12);
        _automatik.Click += (_, _) =>
        {
            if (_fuellt) return;
            _dienst.Anlage.AutomatikAktiv = _automatik.IsChecked == true;
            _dienst.Melden("Automatik", _dienst.Anlage.AutomatikAktiv ? "eingeschaltet" : "ausgeschaltet");
            Gespeichert?.Invoke();
            Auffrischen();
        };
        spalte.Children.Add(_automatik);

        _leuchten.Children.Add(_wind);
        _leuchten.Children.Add(_regen);
        _leuchten.Children.Add(_aussen);
        _leuchten.Children.Add(_innen);
        _leuchten.Children.Add(_hell);
        spalte.Children.Add(_leuchten);

        _vorhersage.Style = (Style)Application.Current.Resources["Hinweis"];
        _vorhersage.Margin = new Thickness(0, 0, 0, 12);
        spalte.Children.Add(_vorhersage);

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
        _naechste.Style = (Style)Application.Current.Resources["Hinweis"];
        _naechste.Margin = new Thickness(0, 0, 12, 0);
        links.Children.Add(_naechste);
        Grid.SetColumn(links, 0);
        reihe.Children.Add(links);

        var rechts = new StackPanel();
        rechts.Children.Add(new TextBlock
        {
            Text = "Antriebe",
            Style = (Style)Application.Current.Resources["Ueberschrift"],
            Margin = new Thickness(0, 0, 0, 4),
        });
        rechts.Children.Add(_kacheln);
        Grid.SetColumn(rechts, 1);
        reihe.Children.Add(rechts);

        spalte.Children.Add(reihe);
        aussen.Content = spalte;
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
                              + " Antriebe · Sonnenstand " + _dienst.Sonnenquelle;

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

    private void Leuchten(Anlage anlage, Wetterlage wetter, DateTime jetzt)
    {
        // Wind: drei Zustaende. Ein fehlender Wert ist kein Windstille-Wert,
        // und die Anzeige sagt das auch so.
        if (wetter.Wind is { } wind && wind.IstFrisch(jetzt, anlage.HoechstalterWind))
        {
            var alarm = false;
            foreach (var motor in anlage.Motoren)
            {
                if (motor.Sicherheitsposition is not null && wind.Wert >= motor.Windgrenze) alarm = true;
            }
            _wind.Zeigen(Zahl(wind.Wert) + " m/s", alarm);
        }
        else
        {
            _wind.Zeigen(wetter.Wind is null ? "kein Wert" : "veraltet", true, bekannt: false);
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
