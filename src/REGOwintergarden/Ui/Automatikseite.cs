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
/// Die zweite Endkundenseite: was die Steuerung koennen soll, in ganzen
/// Saetzen.
///
/// <b>Warum eine eigene Seite:</b> die Bedienseite zeigt, was gerade ist. Sie
/// erklaert nicht, warum es Regeln gibt und was sie tun, wenn niemand
/// hinsieht. Genau danach wird aber gefragt - „warum ist die Markise im
/// Winter oben, obwohl die Sonne scheint" -, und die Antwort gehoert nicht in
/// eine Anleitung, die niemand liest, sondern neben den Schalter, der sie
/// betrifft.
///
/// Jede Karte hat denselben Aufbau: Sinnbild, Name, ein Schalter, zwei bis
/// vier Saetze Erklaerung, und darunter in einer Zeile, was die Regel
/// <b>gerade</b> tut. Der letzte Teil ist der, der Vertrauen schafft.
/// </summary>
public sealed class Automatikseite : UserControl
{
    private readonly Wintergartendienst _dienst;
    private readonly List<Karte> _karten = new();
    private readonly WrapPanel _flaeche = new();

    private bool _fuellt;

    public Automatikseite(Wintergartendienst dienst)
    {
        _dienst = dienst;
        Content = Aufbau();
        Auffrischen();

        _dienst.Aufgefrischt += () => Dispatcher.BeginInvoke(new Action(Auffrischen));
    }

    public event Action? Gespeichert;

    private Brush Farbe(string name) => (Brush)Application.Current.Resources[name];

    // ---- Aufbau -----------------------------------------------------------

    private UIElement Aufbau()
    {
        var aussen = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(12),
        };
        var spalte = new StackPanel();

        spalte.Children.Add(new TextBlock
        {
            Text = "Was die Steuerung von selbst tut",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = Farbe("Schrift"),
        });
        spalte.Children.Add(new TextBlock
        {
            Text = "Jede Regel laesst sich einzeln abschalten. Was abgeschaltet ist, passiert nicht "
                   + "mehr - auch dann nicht, wenn es sinnvoll waere. Unten in jeder Karte steht, was "
                   + "die Regel gerade tut.",
            Style = (Style)Application.Current.Resources["Hinweis"],
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 12),
            MaxWidth = 900,
        });

        _flaeche.Children.Add(Neu("Beschattung", Symbole.Sonne,
            "Faehrt Markisen und Jalousien aus, sobald die Sonne wirklich auf die jeweilige Flaeche "
            + "scheint - nicht schon, wenn es irgendwo hell ist. Dafuer zaehlen drei Dinge zusammen: "
            + "die Himmelsrichtung der Flaeche, die Hoehe der Sonne und die Helligkeit.\n\n"
            + "Beim Einfahren wird laenger gewartet als beim Ausfahren. Eine einzelne Wolke soll "
            + "den Behang nicht hin- und herfahren: jede Fahrt kostet Mechanik.",
            () => _dienst.Anlage.BeschattungAktiv,
            wert => _dienst.Anlage.BeschattungAktiv = wert,
            Stufe.Beschattung));

        _flaeche.Children.Add(Neu("Waermegewinn", Symbole.Thermometer,
            "An kalten Tagen wird nicht beschattet, solange es drinnen kuehl ist. Ein Wintergarten "
            + "ist im Winter eine Heizung.\n\n"
            + "Wer im Januar bei Sonnenschein die Markise ausfaehrt, weil es hell genug ist, wirft "
            + "die einzige kostenlose Waerme des Tages weg - und heizt abends nach. Sobald es "
            + "drinnen warm genug ist, beschattet die Anlage wieder ganz normal.",
            () => _dienst.Anlage.WaermegewinnAktiv,
            wert => _dienst.Anlage.WaermegewinnAktiv = wert,
            null,
            () => "bis " + Zahl(_dienst.Anlage.WaermegewinnAussen) + " °C draussen und "
                  + Zahl(_dienst.Anlage.WaermegewinnInnen) + " °C drinnen"));

        _flaeche.Children.Add(Neu("Hitzevorsorge", Symbole.Wolke,
            "Sagt die Vorhersage einen heissen Tag an, wird frueher beschattet - schon bei weniger "
            + "Helligkeit.\n\n"
            + "Ein Wintergarten, der erst beschattet, wenn es drinnen bereits warm ist, kommt zu "
            + "spaet: die Waerme steckt dann in Boden und Moebeln und geht den ganzen Abend nicht "
            + "mehr heraus.",
            () => _dienst.Anlage.HitzevorsorgeAktiv,
            wert => _dienst.Anlage.HitzevorsorgeAktiv = wert,
            null,
            () => "ab " + Zahl(_dienst.Anlage.HitzevorsorgeAb) + " °C Tageshoechstwert"));

        _flaeche.Children.Add(Neu("Lueften", Symbole.Fenster,
            "Oeffnet die Fenster, wenn es drinnen zu warm wird - aber nur, wenn es draussen wirklich "
            + "kuehler ist.\n\n"
            + "Sonst holt das offene Fenster die Waerme herein, statt sie hinauszulassen. Das ist "
            + "der Unterschied zwischen Lueften und Waermetauschen mit dem Garten.",
            () => _dienst.Anlage.LueftungAktiv,
            wert => _dienst.Anlage.LueftungAktiv = wert,
            Stufe.Lueftung,
            () => "ab " + Zahl(_dienst.Anlage.LueftungAb) + " °C drinnen"));

        _flaeche.Children.Add(Neu("Nachtauskuehlung", Symbole.Uhr,
            "Nach einem heissen Tag werden die Fenster nachts geoeffnet, solange es draussen kuehler "
            + "ist als drinnen.\n\n"
            + "Das ist die wirksamste Kuehlung, die ein Wintergarten hat, und sie kostet nichts. "
            + "Tagsueber bringt Lueften wenig - draussen ist es dann waermer als drinnen. Bei Regen "
            + "oder Wind bleiben die Fenster natuerlich zu.",
            () => _dienst.Anlage.NachtauskuehlungAktiv,
            wert => _dienst.Anlage.NachtauskuehlungAktiv = wert,
            null,
            () => "ab " + Zahl(_dienst.Anlage.NachtauskuehlungAb) + " °C, bis "
                  + Zahl(_dienst.Anlage.NachtauskuehlungZiel) + " °C erreicht sind"));

        _flaeche.Children.Add(Neu("Windschutz", Symbole.Wind,
            "Faehrt Markisen ein und schliesst Fenster, sobald die Wetterstation Windalarm meldet.\n\n"
            + "Die Ueberwachung selbst laeuft in der Wetterstation - sie kennt ihre Boeenerkennung "
            + "und meldet das Ergebnis als Signal. Diese Steuerung wertet es aus, statt daneben eine "
            + "zweite Ueberwachung zu bauen. Kommt vom Wind laenger gar nichts, faehrt die Anlage "
            + "trotzdem in Sicherheit: ein stiller Windmesser ist keine Windstille.",
            () => _dienst.Anlage.WindschutzAktiv,
            wert => _dienst.Anlage.WindschutzAktiv = wert,
            Stufe.Wind,
            () => "Nachlauf " + Zahl(_dienst.Anlage.WindNachlaufMinuten) + " min"));

        _flaeche.Children.Add(Neu("Regenschutz", Symbole.Regen,
            "Schliesst die Fenster und faehrt Markisen ein, sobald die Wetterstation Regen meldet.\n\n"
            + "Auch das kommt als fertiges Signal von der Station - sie hat den beheizten Sensor und "
            + "die Nachlaufzeit. Nach dem Aufhoeren bleibt der Schutz noch eine Weile bestehen, "
            + "damit nicht der naechste Schauer ins offene Fenster faellt.",
            () => _dienst.Anlage.RegenschutzAktiv,
            wert => _dienst.Anlage.RegenschutzAktiv = wert,
            Stufe.Regen,
            () => "Nachlauf " + Zahl(_dienst.Anlage.RegenNachlaufMinuten) + " min"));

        _flaeche.Children.Add(Neu("Frostschutz", Symbole.Frost,
            "Haelt Markisen eingefahren und Fenster geschlossen, solange es draussen zu kalt ist.\n\n"
            + "Eine vereiste Markise reisst beim Ausfahren die Mechanik, und ein offenes Fenster "
            + "kuehlt den Wintergarten in einer Nacht aus. Die Grenze laesst sich je Antrieb "
            + "einstellen.",
            () => _dienst.Anlage.FrostschutzAktiv,
            wert => _dienst.Anlage.FrostschutzAktiv = wert,
            Stufe.Frost));

        _flaeche.Children.Add(Neu("Zeitschaltuhr", Symbole.Uhr,
            "Faehrt zu festen Uhrzeiten - oder zu Sonnenaufgang und Sonnenuntergang, mit Versatz.\n\n"
            + "„Eine halbe Stunde vor Sonnenuntergang zu\" ist der Fall, um den es geht: eine feste "
            + "Uhrzeit liegt im Juni zwei Stunden daneben. Die Zeiten stehen unter Konfiguration.",
            () => _dienst.Anlage.ZeitschaltuhrAktiv,
            wert => _dienst.Anlage.ZeitschaltuhrAktiv = wert,
            Stufe.Zeit));

        _flaeche.Children.Add(Neu("Vorhersage", Symbole.Wolke,
            "Holt Wind, Regen und Temperatur der naechsten Stunden aus dem Netz - ohne Anmeldung, "
            + "ohne Zugangsdaten.\n\n"
            + "Sie ersetzt die Wetterstation nicht, sie warnt vor: eine Markise, die ausfaehrt, "
            + "obwohl in einer Stunde Boeen angesagt sind, faehrt zweimal umsonst und einmal zu "
            + "spaet. Faellt das Netz aus, zaehlt weiter allein die Station.",
            () => _dienst.Anlage.VorhersageAktiv,
            wert => _dienst.Anlage.VorhersageAktiv = wert,
            null,
            () => _dienst.Anlage.Vorhersage is { } sicht ? sicht.ToString() : "noch nichts geholt"));

        spalte.Children.Add(_flaeche);
        aussen.Content = spalte;
        return aussen;
    }

    // ---- Eine Karte --------------------------------------------------------

    /// <summary>Eine Regel als Karte: Schalter, Erklaerung, Live-Zeile.</summary>
    private sealed class Karte : Border
    {
        public Karte(CheckBox schalter, TextBlock stand, Func<bool> lesen, Func<string>? zusatz,
            Stufe? stufe)
        {
            Schalter = schalter;
            Stand = stand;
            Lesen = lesen;
            Zusatz = zusatz;
            Stufe = stufe;
        }

        public CheckBox Schalter { get; }
        public TextBlock Stand { get; }
        public Func<bool> Lesen { get; }
        public Func<string>? Zusatz { get; }
        public Stufe? Stufe { get; }
    }

    private Karte Neu(string name, string sinnbild, string erklaerung, Func<bool> lesen,
        Action<bool> schreiben, Stufe? stufe, Func<string>? zusatz = null)
    {
        var spalte = new StackPanel { Width = 360 };

        var kopf = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 8) };
        var bild = Symbole.Zeichnen(sinnbild, Farbe("Nebenschrift"), 30);
        bild.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(bild, Dock.Left);
        kopf.Children.Add(bild);
        kopf.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Farbe("Schrift"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        spalte.Children.Add(kopf);

        var schalter = new CheckBox { Content = "eingeschaltet", Margin = new Thickness(0, 0, 0, 8) };
        schalter.Click += (_, _) =>
        {
            if (_fuellt) return;
            schreiben(schalter.IsChecked == true);
            _dienst.Melden(name, schalter.IsChecked == true ? "eingeschaltet" : "ausgeschaltet");
            Gespeichert?.Invoke();
            Auffrischen();
        };
        spalte.Children.Add(schalter);

        spalte.Children.Add(new TextBlock
        {
            Text = erklaerung,
            Style = (Style)Application.Current.Resources["Hinweis"],
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });

        var stand = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
        };
        spalte.Children.Add(stand);

        var karte = new Karte(schalter, stand, lesen, zusatz, stufe)
        {
            Style = (Style)Application.Current.Resources["Gruppenkarte"],
            Margin = new Thickness(0, 0, 12, 12),
            Child = spalte,
        };
        _karten.Add(karte);
        return karte;
    }

    // ---- Auffrischen -------------------------------------------------------

    public void Auffrischen()
    {
        if (_fuellt) return;
        _fuellt = true;
        try
        {
            var lagen = _dienst.Lagen;
            foreach (var karte in _karten)
            {
                var an = karte.Lesen();
                karte.Schalter.IsChecked = an;

                var betroffen = 0;
                if (karte.Stufe is { } stufe)
                {
                    foreach (var lage in lagen)
                    {
                        if (lage.Stufe == stufe) betroffen++;
                    }
                }

                // Die Zeile, die Vertrauen schafft: nicht was die Regel tun
                // wuerde, sondern was sie gerade tut.
                if (!an)
                {
                    karte.Stand.Text = "Abgeschaltet - passiert gerade nicht.";
                    karte.Stand.Foreground = Farbe("Blass");
                }
                else if (betroffen > 0)
                {
                    karte.Stand.Text = "Wirkt gerade auf "
                                       + betroffen.ToString(CultureInfo.CurrentCulture)
                                       + (betroffen == 1 ? " Antrieb." : " Antriebe.");
                    karte.Stand.Foreground = karte.Stufe >= Stufe.Frost ? Farbe("Fehler") : Farbe("Betont");
                }
                else
                {
                    karte.Stand.Text = karte.Zusatz is null
                        ? "Eingeschaltet, wirkt gerade nicht."
                        : "Eingeschaltet: " + karte.Zusatz();
                    karte.Stand.Foreground = Farbe("Nebenschrift");
                }

                karte.BorderBrush = an && betroffen > 0
                    ? (karte.Stufe >= Stufe.Frost ? Farbe("Fehler") : Farbe("Betont"))
                    : Farbe("Linie");
                karte.BorderThickness = new Thickness(an && betroffen > 0 ? 2 : 1);
            }
        }
        finally
        {
            _fuellt = false;
        }
    }

    private static string Zahl(double wert) => wert.ToString("0.#", CultureInfo.CurrentCulture);
}
