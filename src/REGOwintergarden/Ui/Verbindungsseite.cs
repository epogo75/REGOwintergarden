using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using REGOwintergarden.App;
using REGOwintergarden.Service;

namespace REGOwintergarden.Ui;

/// <summary>
/// Alles, was mit dem Anschluss zu tun hat: Bus, KNX-Projekt, Dienst.
///
/// Bewusst hinter der Konfiguration und nicht in der Kopfzeile. Eine
/// Gatewayadresse trägt man einmal ein; sie danach jeden Tag anzuzeigen sagt
/// dem, der den Wintergarten benutzt, nichts - und dem, der ihn eingerichtet
/// hat, auch nicht mehr.
/// </summary>
public sealed class Verbindungsseite : UserControl
{
    private readonly Wintergartendienst _dienst;
    private readonly Window _besitzer;

    private readonly TextBox _gateway = new();
    private readonly Button _verbinden = new();
    private readonly TextBlock _stand = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _projekt = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _dienststand = new() { TextWrapping = TextWrapping.Wrap };

    public Verbindungsseite(Wintergartendienst dienst, Window besitzer)
    {
        _dienst = dienst;
        _besitzer = besitzer;

        Content = Aufbau();
        Auffrischen();

        _dienst.StandGeaendert += (_, _) => Dispatcher.BeginInvoke(new Action(Auffrischen));
    }

    public event Action? Gespeichert;

    /// <summary>Die eingetragene Gatewayadresse - das Hauptfenster speichert sie.</summary>
    public string Gateway => _gateway.Text.Trim();

    private UIElement Aufbau()
    {
        var aussen = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(12),
        };
        var spalte = new StackPanel { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };

        spalte.Children.Add(Bausteine.Ueberschrift("KNX-Anschluss", 0));
        spalte.Children.Add(Bausteine.Hinweis(
            "Die Adresse des KNX/IP-Gateways, ueber das gefahren wird - etwa 192.168.1.10:3671. "
            + "Ohne Portangabe wird 3671 angenommen.", eingerueckt: false));

        _gateway.Style = (Style)Application.Current.Resources["Adressfeld"];
        _gateway.Width = 220;
        _gateway.HorizontalAlignment = HorizontalAlignment.Left;
        spalte.Children.Add(Bausteine.Zeile("Gateway", _gateway));

        var reihe = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(Bausteine.Beschriftungsbreite, 0, 0, 8),
        };
        _verbinden.Content = "Verbinden";
        _verbinden.Style = (Style)Application.Current.Resources["KnopfStark"];
        _verbinden.Click += async (_, _) => await Umschalten();
        reihe.Children.Add(_verbinden);
        reihe.Children.Add(Bausteine.Knopf("Zustand abfragen", async () => await _dienst.AbfragenAsync()));
        spalte.Children.Add(reihe);

        _stand.Style = (Style)Application.Current.Resources["Hinweis"];
        _stand.Margin = new Thickness(Bausteine.Beschriftungsbreite, 0, 0, 8);
        spalte.Children.Add(_stand);

        spalte.Children.Add(Bausteine.Hinweis(
            "„Zustand abfragen\" fragt jede eingetragene Adresse einmal ab. Ein Bus erzaehlt seinen "
            + "Zustand nicht von selbst - er meldet nur Aenderungen. Nach dem Verbinden geschieht das "
            + "automatisch.", eingerueckt: false));

        spalte.Children.Add(Bausteine.Ueberschrift("KNX-Projekt"));
        spalte.Children.Add(Bausteine.Hinweis(
            "Ist eine ETS-Projektdatei geladen, schlagen alle Adressfelder beim Tippen daraus vor - "
            + "gefiltert auf den passenden Datenpunkttyp. Gelesen werden .knxproj, .xml, .csv und "
            + ".esf. Von Hand eintragen bleibt moeglich.", eingerueckt: false));

        _projekt.Style = (Style)Application.Current.Resources["Hinweis"];
        _projekt.Margin = new Thickness(0, 0, 0, 8);
        spalte.Children.Add(_projekt);

        var projektreihe = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        projektreihe.Children.Add(Bausteine.Knopf("Projekt laden…", () =>
        {
            AddressSuggest.Laden(_besitzer, _dienst);
            Gespeichert?.Invoke();
            Auffrischen();
        }));
        projektreihe.Children.Add(Bausteine.Knopf("Projekt vergessen", () =>
        {
            _dienst.ProjektVergessen();
            Gespeichert?.Invoke();
            Auffrischen();
        }));
        spalte.Children.Add(projektreihe);

        spalte.Children.Add(Bausteine.Ueberschrift("Dauerbetrieb"));
        _dienststand.Style = (Style)Application.Current.Resources["Hinweis"];
        _dienststand.Margin = new Thickness(0, 0, 0, 8);
        spalte.Children.Add(_dienststand);
        spalte.Children.Add(Bausteine.Hinweis(
            "Ein Wintergarten wartet nicht darauf, dass jemand ein Fenster offen hat. Als Dienst "
            + "laeuft die Automatik weiter, wenn niemand angemeldet ist - sonst fehlt der Windschutz "
            + "genau dann, wenn er gebraucht wird: nachts und im Urlaub. Einrichten und Entfernen "
            + "brauchen Administratorrechte.", eingerueckt: false));

        var dienstreihe = new StackPanel { Orientation = Orientation.Horizontal };
        dienstreihe.Children.Add(Bausteine.Knopf("Dienst einrichten", () =>
        {
            Dienstlauf.Einrichten();
            Auffrischen();
        }));
        dienstreihe.Children.Add(Bausteine.Knopf("Dienst entfernen", () =>
        {
            Dienstlauf.Entfernen();
            Auffrischen();
        }));
        spalte.Children.Add(dienstreihe);

        aussen.Content = spalte;
        return aussen;
    }

    private async System.Threading.Tasks.Task Umschalten()
    {
        _verbinden.IsEnabled = false;
        try
        {
            if (_dienst.Stand == Busstand.Verbunden)
            {
                await _dienst.TrennenAsync();
            }
            else
            {
                Gespeichert?.Invoke();
                await _dienst.VerbindenAsync(Gateway);
            }
        }
        finally
        {
            _verbinden.IsEnabled = true;
            Auffrischen();
        }
    }

    public void Auffrischen()
    {
        _gateway.Text = _dienst.Einstellungen.Gateway;
        _verbinden.Content = _dienst.Stand == Busstand.Verbunden ? "Trennen" : "Verbinden";

        _stand.Text = _dienst.Stand switch
        {
            Busstand.Verbunden => "Verbunden. Telegramme gehen hinaus und werden mitgehoert.",
            Busstand.Verbinde => "Verbindung wird aufgebaut…",
            Busstand.Fehler => "Nicht verbunden - siehe Protokoll.",
            _ => "Nicht verbunden. Ohne Bus rechnet die Automatik zwar, faehrt aber nichts.",
        };
        _stand.Foreground = (Brush)Application.Current.Resources[_dienst.Stand switch
        {
            Busstand.Verbunden => "Gut",
            Busstand.Fehler => "Fehler",
            _ => "Nebenschrift",
        }];

        _projekt.Text = _dienst.Adresspool.Count == 0
            ? "Kein Projekt geladen - die Adressfelder sind gewoehnliche Textfelder."
            : _dienst.Adresspool.Count.ToString(CultureInfo.CurrentCulture)
              + " Gruppenadressen geladen aus " + _dienst.Projektquelle;

        _dienststand.Text = Dienstlauf.Eingerichtet()
            ? "Der Dienst ist eingerichtet. Er rechnet rund um die Uhr; dieses Fenster zeigt dann nur an."
            : "Der Dienst ist nicht eingerichtet. Die Automatik laeuft nur, solange dieses Fenster "
              + "offen ist.";
    }
}
