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

    private readonly RadioButton _selbst = new() { Content = "Dieses Programm steuert selbst" };
    private readonly RadioButton _fern = new() { Content = "Ein anderer Rechner fuehrt - hier nur zusehen und bedienen" };
    private readonly TextBox _fernadresse = new();
    private readonly StackPanel _busteil = new();
    private readonly StackPanel _fernteil = new();
    private bool _fuellt;

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

        spalte.Children.Add(Bausteine.Ueberschrift("Wer steuert", 0));
        spalte.Children.Add(Bausteine.Hinweis(
            "Steuern darf immer nur einer. Laeuft die Anlage schon auf einem anderen Rechner - etwa "
            + "auf einem Raspberry Pi -, dann sieht dieses Fenster ihm zu und bedient ihn, statt "
            + "selbst auf den Bus zu gehen. Zwei Automatiken auf denselben Adressen wuerden sich "
            + "gegenseitig ueberfahren, und beim zyklischen Windtelegramm waere das gefaehrlich.",
            eingerueckt: false));

        _selbst.Margin = new Thickness(0, 0, 0, 4);
        _fern.Margin = new Thickness(0, 0, 0, 8);
        _selbst.Checked += (_, _) => Umgeschaltet();
        _fern.Checked += (_, _) => Umgeschaltet();
        spalte.Children.Add(_selbst);
        spalte.Children.Add(_fern);

        // ---- selbst steuern ------------------------------------------------
        _busteil.Children.Add(Bausteine.Ueberschrift("KNX-Anschluss"));
        _busteil.Children.Add(Bausteine.Hinweis(
            "Die Adresse des KNX/IP-Gateways, ueber das gefahren wird - etwa 192.168.1.10:3671. "
            + "Ohne Portangabe wird 3671 angenommen.", eingerueckt: false));

        _gateway.Style = (Style)Application.Current.Resources["Adressfeld"];
        _gateway.Width = 220;
        _gateway.HorizontalAlignment = HorizontalAlignment.Left;
        _busteil.Children.Add(Bausteine.Zeile("Gateway", _gateway));
        spalte.Children.Add(_busteil);

        // ---- fernbedienen --------------------------------------------------
        _fernteil.Children.Add(Bausteine.Ueberschrift("Fuehrender Rechner"));
        _fernteil.Children.Add(Bausteine.Hinweis(
            "Adresse des Dienstes, etwa 192.168.1.229:5195. Von dort kommen die Messwerte, dorthin "
            + "gehen die Knoepfe. Gerechnet wird hier trotzdem alles selbst - mit denselben Werten "
            + "und demselben Programm, damit beide Fenster dasselbe zeigen und nicht zwei Wahrheiten "
            + "entstehen.", eingerueckt: false));

        _fernadresse.Style = (Style)Application.Current.Resources["Adressfeld"];
        _fernadresse.Width = 220;
        _fernadresse.HorizontalAlignment = HorizontalAlignment.Left;
        _fernteil.Children.Add(Bausteine.Zeile("Dienst", _fernadresse));
        _fernteil.Children.Add(Bausteine.Hinweis(
            "„Anlage uebernehmen\" holt Antriebe, Adressen und Grenzen von dort. Ohne das rechnet "
            + "dieses Fenster zwar richtig, aber ueber eine andere Anlage.", eingerueckt: false));
        _fernteil.Children.Add(Bausteine.Knopf("Anlage uebernehmen", async () => await UebernehmenAsync()));
        spalte.Children.Add(_fernteil);

        var reihe = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(Bausteine.Beschriftungsbreite, 8, 0, 8),
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

    /// <summary>Die eingetragene Adresse des fuehrenden Dienstes.</summary>
    public string Fernadresse => _fernadresse.Text.Trim();

    /// <summary>Ob dieses Fenster nur zusieht.</summary>
    public bool Fernbedienung => _fern.IsChecked == true;

    /// <summary>Schreibt die Wahl in die Einstellungen - das Hauptfenster sichert sie.</summary>
    public void Uebernehmen()
    {
        _dienst.Einstellungen.Gateway = Gateway;
        _dienst.Einstellungen.Fernbedienung = Fernbedienung;
        _dienst.Einstellungen.Fernadresse = Fernadresse;
    }

    private void Umgeschaltet()
    {
        if (_fuellt) return;

        _busteil.Visibility = _fern.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        _fernteil.Visibility = _fern.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        // Die Betriebsart zu wechseln, ohne die alte Verbindung zu loesen,
        // hiesse: erst am Bus haengen und dann noch fernbedienen. Also
        // trennen - verbunden wird auf Knopfdruck.
        if (_dienst.Stand == Busstand.Verbunden) _ = _dienst.TrennenAsync();

        Uebernehmen();
        Gespeichert?.Invoke();
        Auffrischen();
    }

    /// <summary>
    /// Holt die Anlage vom fuehrenden Dienst. Ohne das rechnete dieses Fenster
    /// richtig, aber ueber die falsche Anlage - andere Antriebe, andere
    /// Grenzen, andere Himmelsrichtungen.
    /// </summary>
    private async System.Threading.Tasks.Task UebernehmenAsync()
    {
        using var fern = new Fernsteuerung(Fernadresse);
        var text = await fern.EinstellungenAsync();
        if (text is null)
        {
            MessageBox.Show(_besitzer, fern.Adresse + " antwortet nicht.\n\n"
                                       + "Laeuft der Dienst dort, und stimmt die Portnummer?",
                "Anlage uebernehmen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_dienst.Einstellungen.AnlageUebernehmen(text, out var fehler))
        {
            MessageBox.Show(_besitzer, "Die Antwort war nicht zu lesen: " + fehler,
                "Anlage uebernehmen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Gespeichert?.Invoke();
        Auffrischen();
        MessageBox.Show(_besitzer,
            "Uebernommen: " + _dienst.Anlage.Name + " mit "
            + _dienst.Anlage.Motoren.Count.ToString(CultureInfo.CurrentCulture) + " Antrieben.",
            "Anlage uebernehmen", MessageBoxButton.OK, MessageBoxImage.Information);
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
                Uebernehmen();
                Gespeichert?.Invoke();
                if (Fernbedienung) await _dienst.VerbindenFernAsync(Fernadresse);
                else await _dienst.VerbindenAsync(Gateway);
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
        _fuellt = true;
        try
        {
            _gateway.Text = _dienst.Einstellungen.Gateway;
            _fernadresse.Text = _dienst.Einstellungen.Fernadresse;
            _fern.IsChecked = _dienst.Einstellungen.Fernbedienung;
            _selbst.IsChecked = !_dienst.Einstellungen.Fernbedienung;
            _busteil.Visibility = _dienst.Einstellungen.Fernbedienung
                ? Visibility.Collapsed : Visibility.Visible;
            _fernteil.Visibility = _dienst.Einstellungen.Fernbedienung
                ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _fuellt = false;
        }

        _verbinden.Content = _dienst.Stand == Busstand.Verbunden ? "Trennen" : "Verbinden";

        var fernbetrieb = _dienst.Einstellungen.Fernbedienung;
        _stand.Text = _dienst.Stand switch
        {
            Busstand.Verbunden => fernbetrieb
                ? "Verbunden mit " + _dienst.Einstellungen.Fernadresse
                  + ". Die Werte kommen von dort; gefahren wird ueber ihn."
                : "Verbunden. Telegramme gehen hinaus und werden mitgehoert.",
            Busstand.Verbinde => "Verbindung wird aufgebaut…",
            Busstand.Fehler => "Nicht verbunden - siehe Protokoll.",
            _ => fernbetrieb
                ? "Nicht verbunden. Ohne den fuehrenden Rechner bleibt die Anzeige stehen."
                : "Nicht verbunden. Ohne Bus rechnet die Automatik zwar, faehrt aber nichts.",
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

        _dienststand.Text = fernbetrieb
            ? "In der Fernbedienung wird hier kein Dienst gebraucht - es laeuft ja schon einer, und "
              + "zwar auf dem fuehrenden Rechner. Ein zweiter waere ein zweiter Absender auf "
              + (Dienstlauf.Eingerichtet()
                  ? "denselben Adressen. Der eingerichtete Dienst sollte entfernt werden."
                  : "denselben Adressen.")
            : Dienstlauf.Eingerichtet()
                ? "Der Dienst ist eingerichtet. Er rechnet rund um die Uhr; dieses Fenster zeigt dann nur an."
                : "Der Dienst ist nicht eingerichtet. Die Automatik laeuft nur, solange dieses Fenster "
                  + "offen ist.";
    }
}
