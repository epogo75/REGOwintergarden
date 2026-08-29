using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using REGOwintergarden.App;
using REGOwintergarden.Model;

namespace REGOwintergarden.Ui;

/// <summary>Eine Zeile der Schaltzeitenliste.</summary>
public sealed class Zeitzeile
{
    public Zeitzeile(Schaltzeit zeit, string motor)
    {
        Schaltzeit = zeit;
        Antrieb = motor;
    }

    public Schaltzeit Schaltzeit { get; }
    public string Aktiv => Schaltzeit.Aktiv ? "an" : "aus";
    public string Wann => Schaltzeit.Bezug switch
    {
        Zeitbezug.Sonnenaufgang => "Sonnenaufgang",
        Zeitbezug.Sonnenuntergang => "Sonnenuntergang",
        _ => Schaltzeit.Zeit,
    };
    public string Versatz => Schaltzeit.Versatz == 0
        ? ""
        : (Schaltzeit.Versatz > 0 ? "+" : "−")
          + Math.Abs(Schaltzeit.Versatz).ToString("0", CultureInfo.CurrentCulture) + " min";
    public string Tage => Schaltzeit.Tagesnamen();
    public string Antrieb { get; }
    public string Position => Schaltzeit.Position.ToString("0", CultureInfo.CurrentCulture) + " %";
    public string Bemerkung => Schaltzeit.Bemerkung;
}

/// <summary>
/// Die Zeitschaltuhr.
///
/// Der Bezug auf Sonnenauf- und -untergang ist der Grund, warum es sie
/// ueberhaupt gibt: „abends zu" ist im Juni etwas anderes als im Dezember,
/// und eine feste Uhrzeit liegt dann zwei Stunden daneben.
/// </summary>
public sealed class Zeitseite : UserControl
{
    private readonly Wintergartendienst _dienst;
    private readonly Window _besitzer;

    private readonly ObservableCollection<Zeitzeile> _zeilen = new();
    private readonly ListView _liste = new();
    private readonly StackPanel _form = new();
    private readonly TextBlock _leer = new();
    private readonly TextBlock _naechste = new() { TextWrapping = TextWrapping.Wrap };

    private readonly CheckBox _aktiv = new() { Content = "Schaltzeit ist eingeschaltet" };
    private readonly ComboBox _bezug = new();
    private readonly TextBox _zeit = new();
    private readonly TextBox _versatz = new();
    private readonly ComboBox _motor = new();
    private readonly TextBox _position = new();
    private readonly TextBox _bemerkung = new();
    private readonly CheckBox[] _tage = new CheckBox[7];

    private bool _fuellt;

    public Zeitseite(Wintergartendienst dienst, Window besitzer)
    {
        _dienst = dienst;
        _besitzer = besitzer;

        Content = Aufbau();
        Auffrischen();
    }

    public event Action? Gespeichert;

    private Schaltzeit? Gewaehlt => (_liste.SelectedItem as Zeitzeile)?.Schaltzeit;

    private UIElement Aufbau()
    {
        var raster = new Grid { Margin = new Thickness(12) };
        raster.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        raster.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });

        var links = new DockPanel { Margin = new Thickness(0, 0, 12, 0) };

        _naechste.Style = (Style)Application.Current.Resources["Hinweis"];
        _naechste.Margin = new Thickness(0, 0, 0, 8);
        DockPanel.SetDock(_naechste, Dock.Top);
        links.Children.Add(_naechste);

        var knoepfe = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        knoepfe.Children.Add(Bausteine.Knopf("Schaltzeit anlegen", Anlegen));
        knoepfe.Children.Add(Bausteine.Knopf("Loeschen", Loeschen));
        DockPanel.SetDock(knoepfe, Dock.Bottom);
        links.Children.Add(knoepfe);

        _liste.ItemsSource = _zeilen;
        _liste.BorderThickness = new Thickness(0);
        _liste.Background = Brushes.Transparent;
        _liste.SelectionChanged += (_, _) => Fuellen();
        var spalten = new GridView();
        spalten.Columns.Add(Spalte("", 44, nameof(Zeitzeile.Aktiv)));
        spalten.Columns.Add(Spalte("Wann", 130, nameof(Zeitzeile.Wann)));
        spalten.Columns.Add(Spalte("Versatz", 80, nameof(Zeitzeile.Versatz)));
        spalten.Columns.Add(Spalte("Tage", 150, nameof(Zeitzeile.Tage)));
        spalten.Columns.Add(Spalte("Antrieb", 160, nameof(Zeitzeile.Antrieb)));
        spalten.Columns.Add(Spalte("Ziel", 70, nameof(Zeitzeile.Position)));
        spalten.Columns.Add(Spalte("Bemerkung", 160, nameof(Zeitzeile.Bemerkung)));
        _liste.View = spalten;
        links.Children.Add(new Border
        {
            Style = (Style)Application.Current.Resources["Listenkarte"],
            Child = _liste,
        });
        Grid.SetColumn(links, 0);
        raster.Children.Add(links);

        _leer.Text = "Links eine Schaltzeit waehlen oder eine anlegen.";
        _leer.Style = (Style)Application.Current.Resources["Hinweis"];

        Formular();
        var rechts = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel { Children = { _leer, _form } },
        };
        Grid.SetColumn(rechts, 1);
        raster.Children.Add(rechts);
        return raster;
    }

    private void Formular()
    {
        _form.Children.Add(Bausteine.Ueberschrift("Schaltzeit", 0));
        _aktiv.Margin = new Thickness(Bausteine.Beschriftungsbreite, 0, 0, 8);
        _form.Children.Add(_aktiv);

        _bezug.Items.Add("feste Uhrzeit");
        _bezug.Items.Add("Sonnenaufgang");
        _bezug.Items.Add("Sonnenuntergang");
        _bezug.Style = (Style)Application.Current.Resources["Auswahlfeld"];
        _bezug.SelectionChanged += (_, _) => BezugZeigen();
        _form.Children.Add(Bausteine.Zeile("Bezug", _bezug));

        _form.Children.Add(Bausteine.Zeile("Uhrzeit", Bausteine.Feld(_zeit, 100)));
        _form.Children.Add(Bausteine.Zeile("Versatz", Bausteine.Feld(_versatz, 100)));
        _form.Children.Add(Bausteine.Hinweis(
            "Versatz in Minuten, auch negativ: minus 30 heisst eine halbe Stunde vorher."));

        var tagereihe = new StackPanel { Orientation = Orientation.Horizontal };
        var kuerzel = new[] { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };
        for (var i = 0; i < 7; i++)
        {
            _tage[i] = new CheckBox { Content = kuerzel[i], Margin = new Thickness(0, 0, 10, 0) };
            tagereihe.Children.Add(_tage[i]);
        }
        _form.Children.Add(Bausteine.Zeile("Tage", tagereihe));

        _motor.Style = (Style)Application.Current.Resources["Auswahlfeld"];
        _form.Children.Add(Bausteine.Zeile("Antrieb", _motor));
        _form.Children.Add(Bausteine.Zeile("Ziel", Bausteine.Feld(_position, 100)));
        _form.Children.Add(Bausteine.Hinweis("In Prozent. 0 ist offen, 100 ist zu."));
        _form.Children.Add(Bausteine.Zeile("Bemerkung", Bausteine.Feld(_bemerkung)));

        var uebernehmen = Bausteine.Knopf("Uebernehmen", Uebernehmen, stark: true);
        uebernehmen.HorizontalAlignment = HorizontalAlignment.Left;
        uebernehmen.Margin = new Thickness(Bausteine.Beschriftungsbreite, 4, 0, 0);
        _form.Children.Add(uebernehmen);
    }

    private void BezugZeigen()
    {
        _zeit.IsEnabled = _bezug.SelectedIndex == 0;
    }

    public void Auffrischen()
    {
        var gewaehlt = Gewaehlt?.Id;
        _zeilen.Clear();
        foreach (var zeit in _dienst.Anlage.Schaltzeiten)
        {
            var name = zeit.MotorId.Length == 0
                ? "alle"
                : _dienst.Anlage.Finde(zeit.MotorId)?.Name ?? "— fehlt —";
            var zeile = new Zeitzeile(zeit, name);
            _zeilen.Add(zeile);
            if (zeit.Id == gewaehlt) _liste.SelectedItem = zeile;
        }
        if (_liste.SelectedItem is null && _zeilen.Count > 0) _liste.SelectedIndex = 0;

        var anlage = _dienst.Anlage;
        _naechste.Text = "Naechste Schaltung: " + Zeitschaltuhr.NaechsteText(anlage,
            zeit => Astro.Berechnen(zeit, anlage.Breite, anlage.Laenge), DateTime.Now);

        Motoren();
        Fuellen();
    }

    private void Motoren()
    {
        var vorher = (_motor.SelectedItem as Eintrag)?.Id;
        _motor.Items.Clear();
        _motor.Items.Add(new Eintrag("", "alle Antriebe"));
        foreach (var motor in _dienst.Anlage.Motoren) _motor.Items.Add(new Eintrag(motor.Id, motor.Name));
        _motor.DisplayMemberPath = nameof(Eintrag.Titel);
        Waehle(vorher ?? "");
    }

    private sealed record Eintrag(string Id, string Titel);

    private void Waehle(string id)
    {
        foreach (Eintrag eintrag in _motor.Items)
        {
            if (eintrag.Id != id) continue;
            _motor.SelectedItem = eintrag;
            return;
        }
        if (_motor.Items.Count > 0) _motor.SelectedIndex = 0;
    }

    private void Fuellen()
    {
        var zeit = Gewaehlt;
        _form.Visibility = zeit is null ? Visibility.Collapsed : Visibility.Visible;
        _leer.Visibility = zeit is null ? Visibility.Visible : Visibility.Collapsed;
        if (zeit is null) return;

        _fuellt = true;
        try
        {
            _aktiv.IsChecked = zeit.Aktiv;
            _bezug.SelectedIndex = (int)zeit.Bezug;
            _zeit.Text = zeit.Zeit;
            _versatz.Text = zeit.Versatz.ToString("0", CultureInfo.CurrentCulture);
            for (var tag = 1; tag <= 7; tag++)
            {
                _tage[tag - 1].IsChecked = zeit.Tage.Contains((char)('0' + tag));
            }
            Waehle(zeit.MotorId);
            _position.Text = Bausteine.Zahl(zeit.Position);
            _bemerkung.Text = zeit.Bemerkung;
            BezugZeigen();
        }
        finally
        {
            _fuellt = false;
        }
    }

    private void Uebernehmen()
    {
        if (_fuellt) return;
        if (Gewaehlt is not { } zeit) return;

        zeit.Aktiv = _aktiv.IsChecked == true;
        zeit.Bezug = (Zeitbezug)Math.Max(0, _bezug.SelectedIndex);
        zeit.Zeit = _zeit.Text.Trim();
        if (int.TryParse(_versatz.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var versatz))
        {
            zeit.Versatz = Math.Clamp(versatz, -720, 720);
        }

        var tage = new System.Text.StringBuilder();
        for (var tag = 1; tag <= 7; tag++)
        {
            if (_tage[tag - 1].IsChecked == true) tage.Append((char)('0' + tag));
        }
        zeit.Tage = tage.ToString();

        zeit.MotorId = (_motor.SelectedItem as Eintrag)?.Id ?? "";
        Bausteine.Setze(_position, wert => zeit.Position = Math.Clamp(wert, 0, 100));
        zeit.Bemerkung = _bemerkung.Text.Trim();

        if (zeit.Bezug == Zeitbezug.Uhrzeit && !zeit.TryUhrzeit(out _, out _))
        {
            MessageBox.Show(_besitzer, "Die Uhrzeit " + zeit.Zeit + " laesst sich nicht lesen. Erwartet wird 07:30.",
                "Schaltzeit", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (zeit.Tagesnamen() == "nie")
        {
            MessageBox.Show(_besitzer, "Es ist kein Tag angekreuzt - dann laeuft die Schaltzeit nie.",
                "Schaltzeit", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Gespeichert?.Invoke();
        Auffrischen();
    }

    private void Anlegen()
    {
        var zeit = new Schaltzeit { Bemerkung = "neu" };
        _dienst.Anlage.Schaltzeiten.Add(zeit);
        Gespeichert?.Invoke();
        Auffrischen();
        foreach (var zeile in _zeilen)
        {
            if (zeile.Schaltzeit.Id == zeit.Id) _liste.SelectedItem = zeile;
        }
    }

    private void Loeschen()
    {
        if (Gewaehlt is not { } zeit) return;
        _dienst.Anlage.Schaltzeiten.Remove(zeit);
        Gespeichert?.Invoke();
        Auffrischen();
    }

    private static GridViewColumn Spalte(string kopf, double breite, string pfad) => new()
    {
        Header = kopf,
        Width = breite,
        DisplayMemberBinding = new Binding(pfad),
    };
}
