using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using REGOwintergarden.App;
using REGOwintergarden.Model;

namespace REGOwintergarden.Ui;

/// <summary>Eine Zeile der Antriebsliste.</summary>
public sealed class Antriebszeile
{
    public Antriebszeile(Motor motor)
    {
        Motor = motor;
    }

    public Motor Motor { get; }
    public string Name => Motor.Name;
    public string Art => Motor.Art.ToString();
    public string Richtung => Motor.Richtung + "  "
                              + Math.Round(Motor.Ausrichtung).ToString("0", CultureInfo.CurrentCulture) + "°";
    public string Adresse => Motor.AdressePosition.Length > 0 ? Motor.AdressePosition : "—";
}

/// <summary>
/// Der Einrichtebereich fuer die Antriebe: links die Liste, rechts alles zu
/// dem, was gerade gewaehlt ist.
///
/// Die Ausrichtung steht als Zahl in Grad und nicht als Auswahl aus acht
/// Richtungen: ein Wintergarten steht selten genau nach Sueden, und 205 Grad
/// sind etwas anderes als „Sued". Daneben steht das Kuerzel, damit die Zahl
/// nicht abstrakt bleibt.
/// </summary>
public sealed class Antriebsseite : UserControl
{
    private readonly Wintergartendienst _dienst;
    private readonly Window _besitzer;

    private readonly ObservableCollection<Antriebszeile> _zeilen = new();
    private readonly ListView _liste = new();
    private readonly StackPanel _form = new();
    private readonly TextBlock _leer = new();

    private readonly TextBox _name = new();
    private readonly ComboBox _art = new();
    private readonly TextBox _ausrichtung = new();
    private readonly TextBlock _richtung = new();
    private readonly TextBox _oeffnung = new();
    private readonly TextBox _elevationMin = new();
    private readonly TextBox _elevationMax = new();
    private readonly TextBox _beschattung = new();
    private readonly TextBox _lamelle = new();
    private readonly TextBox _frei = new();
    private readonly TextBox _wind = new();
    private readonly TextBox _frost = new();
    private readonly CheckBox _regen = new() { Content = "faehrt bei Regen in Sicherheit" };
    private readonly CheckBox _beschattungAktiv = new() { Content = "Beschattung" };
    private readonly CheckBox _lueftungAktiv = new() { Content = "Lueftung" };
    private readonly CheckBox _zeitAktiv = new() { Content = "Zeitschaltuhr" };

    private readonly TextBox _adrFahren = new();
    private readonly TextBox _adrStopp = new();
    private readonly TextBox _adrPosition = new();
    private readonly TextBox _adrPositionStatus = new();
    private readonly TextBox _adrLamelle = new();
    private readonly TextBox _adrLamelleStatus = new();

    private bool _fuellt;

    public Antriebsseite(Wintergartendienst dienst, Window besitzer)
    {
        _dienst = dienst;
        _besitzer = besitzer;

        Content = Aufbau();
        Auffrischen();
    }

    public event Action? Gespeichert;

    private Motor? Gewaehlt => (_liste.SelectedItem as Antriebszeile)?.Motor;

    private UIElement Aufbau()
    {
        var raster = new Grid { Margin = new Thickness(12) };
        raster.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        raster.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(430) });

        // ---- links: Liste ----
        var links = new DockPanel { Margin = new Thickness(0, 0, 12, 0) };

        var knoepfe = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        knoepfe.Children.Add(Knopf("Antrieb anlegen", Anlegen));
        knoepfe.Children.Add(Knopf("Verdoppeln", Verdoppeln));
        knoepfe.Children.Add(Knopf("Loeschen", Loeschen));
        knoepfe.Children.Add(Knopf("KNX-Projekt…", () =>
        {
            AddressSuggest.Laden(_besitzer, _dienst);
            Gespeichert?.Invoke();
        }));
        DockPanel.SetDock(knoepfe, Dock.Bottom);
        links.Children.Add(knoepfe);

        _liste.ItemsSource = _zeilen;
        _liste.BorderThickness = new Thickness(0);
        _liste.Background = Brushes.Transparent;
        _liste.SelectionChanged += (_, _) => Fuellen();
        var spalten = new GridView();
        spalten.Columns.Add(Spalte("Name", 190, nameof(Antriebszeile.Name)));
        spalten.Columns.Add(Spalte("Art", 120, nameof(Antriebszeile.Art)));
        spalten.Columns.Add(Spalte("Richtung", 110, nameof(Antriebszeile.Richtung)));
        spalten.Columns.Add(Spalte("Position", 110, nameof(Antriebszeile.Adresse)));
        _liste.View = spalten;
        links.Children.Add(new Border
        {
            Style = (Style)Application.Current.Resources["Listenkarte"],
            Child = _liste,
        });

        Grid.SetColumn(links, 0);
        raster.Children.Add(links);

        // ---- rechts: Form ----
        _leer.Text = "Links einen Antrieb waehlen oder einen anlegen.";
        _leer.Style = (Style)Application.Current.Resources["Hinweis"];

        var rechts = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel { Children = { _leer, _form } },
        };
        Formular();
        Grid.SetColumn(rechts, 1);
        raster.Children.Add(rechts);
        return raster;
    }

    private void Formular()
    {
        foreach (Antriebsart art in Enum.GetValues(typeof(Antriebsart))) _art.Items.Add(art);

        _form.Children.Add(Ueberschrift("Antrieb", 0));
        _form.Children.Add(Zeile("Name", Feld(_name)));
        _form.Children.Add(Zeile("Art", Auswahl(_art)));

        var richtungsreihe = new StackPanel { Orientation = Orientation.Horizontal };
        _ausrichtung.Style = (Style)Application.Current.Resources["Adressfeld"];
        _ausrichtung.Width = 80;
        _ausrichtung.TextChanged += (_, _) => RichtungZeigen();
        richtungsreihe.Children.Add(_ausrichtung);
        _richtung.Style = (Style)Application.Current.Resources["Hinweis"];
        _richtung.VerticalAlignment = VerticalAlignment.Center;
        _richtung.Margin = new Thickness(8, 0, 0, 0);
        richtungsreihe.Children.Add(_richtung);
        _form.Children.Add(Zeile("Ausrichtung", richtungsreihe));
        _form.Children.Add(Hinweis("In Grad: 0 ist Nord, 90 Ost, 180 Sued, 270 West. "
                                   + "Frei einstellbar - die Beschattung rechnet damit."));

        _form.Children.Add(Ueberschrift("Beschattung", 12));
        _form.Children.Add(Zeile("Oeffnungswinkel", Feld(_oeffnung, 80)));
        _form.Children.Add(Hinweis("Wie weit die Sonne seitlich stehen darf und noch auf die Flaeche "
                                   + "scheint, in Grad nach jeder Seite. 90 waere streifender Einfall."));
        _form.Children.Add(Zeile("Sonne ab", Feld(_elevationMin, 80)));
        _form.Children.Add(Zeile("Sonne bis", Feld(_elevationMax, 80)));
        _form.Children.Add(Hinweis("Sonnenhoehe in Grad. Unter dem ersten Wert steht die Sonne hinter "
                                   + "Nachbarhaeusern, ueber dem zweiten scheint sie ueber die Flaeche hinweg."));
        _form.Children.Add(Zeile("Beschattet auf", Feld(_beschattung, 80)));
        _form.Children.Add(Zeile("Lamelle auf", Feld(_lamelle, 80)));
        _form.Children.Add(Zeile("Danach auf", Feld(_frei, 80)));

        _form.Children.Add(Ueberschrift("Schutz", 12));
        _form.Children.Add(Zeile("Windgrenze", Feld(_wind, 80)));
        _form.Children.Add(Hinweis("In m/s. Darueber faehrt der Antrieb in Sicherheit - eine Markise "
                                   + "ein, ein Fenster zu. Ein Rollladen hat keine sichere Seite und "
                                   + "bleibt stehen."));
        _form.Children.Add(Zeile("Frostgrenze", Feld(_frost, 80)));
        _regen.Margin = new Thickness(100, 0, 0, 8);
        _form.Children.Add(_regen);

        _form.Children.Add(Ueberschrift("Automatik", 12));
        var haken = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(100, 0, 0, 8) };
        _beschattungAktiv.Margin = new Thickness(0, 0, 12, 0);
        _lueftungAktiv.Margin = new Thickness(0, 0, 12, 0);
        haken.Children.Add(_beschattungAktiv);
        haken.Children.Add(_lueftungAktiv);
        haken.Children.Add(_zeitAktiv);
        _form.Children.Add(haken);

        _form.Children.Add(Ueberschrift("Gruppenadressen", 12));
        _form.Children.Add(Zeile("Auf/Ab", Adressfeld(_adrFahren, "1.008")));
        _form.Children.Add(Zeile("Stopp", Adressfeld(_adrStopp, "1.007")));
        _form.Children.Add(Zeile("Position", Adressfeld(_adrPosition, "5.001")));
        _form.Children.Add(Zeile("Position Rueckm.", Adressfeld(_adrPositionStatus, "5.001")));
        _form.Children.Add(Zeile("Lamelle", Adressfeld(_adrLamelle, "5.001")));
        _form.Children.Add(Zeile("Lamelle Rueckm.", Adressfeld(_adrLamelleStatus, "5.001")));
        _form.Children.Add(Hinweis("Ist ein KNX-Projekt geladen, schlagen die Felder beim Tippen vor - "
                                   + "gefiltert auf den passenden Datenpunkttyp."));

        var uebernehmen = new Button
        {
            Content = "Uebernehmen",
            Style = (Style)Application.Current.Resources["KnopfStark"],
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(100, 12, 0, 0),
        };
        uebernehmen.Click += (_, _) => Uebernehmen();
        _form.Children.Add(uebernehmen);
    }

    // ---- Fuellen -----------------------------------------------------------

    public void Auffrischen()
    {
        var gewaehlt = Gewaehlt?.Id;
        _zeilen.Clear();
        foreach (var motor in _dienst.Anlage.Motoren)
        {
            var zeile = new Antriebszeile(motor);
            _zeilen.Add(zeile);
            if (motor.Id == gewaehlt) _liste.SelectedItem = zeile;
        }
        if (_liste.SelectedItem is null && _zeilen.Count > 0) _liste.SelectedIndex = 0;
        Fuellen();
    }

    private void Fuellen()
    {
        var motor = Gewaehlt;
        _form.Visibility = motor is null ? Visibility.Collapsed : Visibility.Visible;
        _leer.Visibility = motor is null ? Visibility.Visible : Visibility.Collapsed;
        if (motor is null) return;

        _fuellt = true;
        try
        {
            _name.Text = motor.Name;
            _art.SelectedItem = motor.Art;
            _ausrichtung.Text = Zahl(motor.Ausrichtung);
            _oeffnung.Text = Zahl(motor.Oeffnungswinkel);
            _elevationMin.Text = Zahl(motor.ElevationMin);
            _elevationMax.Text = Zahl(motor.ElevationMax);
            _beschattung.Text = Zahl(motor.Beschattungsposition);
            _lamelle.Text = Zahl(motor.Lamellenposition);
            _frei.Text = Zahl(motor.Freiposition);
            _wind.Text = Zahl(motor.Windgrenze);
            _frost.Text = Zahl(motor.Frostgrenze);
            _regen.IsChecked = motor.Regenschutz;
            _beschattungAktiv.IsChecked = motor.BeschattungAktiv;
            _lueftungAktiv.IsChecked = motor.LueftungAktiv;
            _zeitAktiv.IsChecked = motor.ZeitAktiv;

            _adrFahren.Text = motor.AdresseFahren;
            _adrStopp.Text = motor.AdresseStopp;
            _adrPosition.Text = motor.AdressePosition;
            _adrPositionStatus.Text = motor.AdressePositionStatus;
            _adrLamelle.Text = motor.AdresseLamelle;
            _adrLamelleStatus.Text = motor.AdresseLamelleStatus;

            RichtungZeigen();
        }
        finally
        {
            _fuellt = false;
        }
    }

    private void RichtungZeigen()
    {
        if (TryZahl(_ausrichtung.Text, out var grad))
        {
            _richtung.Text = Motor.Richtungsname(grad) + "  ·  " + Beispiel(grad);
        }
        else
        {
            _richtung.Text = "keine Zahl";
        }
    }

    /// <summary>Was eine Ausrichtung im Tagesverlauf bedeutet - als Merkhilfe.</summary>
    private static string Beispiel(double grad) => Motor.Normiert(grad) switch
    {
        >= 45 and < 135 => "Morgensonne",
        >= 135 and < 225 => "Mittagssonne",
        >= 225 and < 315 => "Abendsonne",
        _ => "kaum direkte Sonne",
    };

    private void Uebernehmen()
    {
        if (_fuellt) return;
        if (Gewaehlt is not { } motor) return;

        motor.Name = _name.Text.Trim().Length > 0 ? _name.Text.Trim() : motor.Name;
        if (_art.SelectedItem is Antriebsart art) motor.Art = art;
        Setze(_ausrichtung, wert => motor.Ausrichtung = Motor.Normiert(wert));
        Setze(_oeffnung, wert => motor.Oeffnungswinkel = Math.Clamp(wert, 5, 180));
        Setze(_elevationMin, wert => motor.ElevationMin = Math.Clamp(wert, 0, 90));
        Setze(_elevationMax, wert => motor.ElevationMax = Math.Clamp(wert, 0, 90));
        Setze(_beschattung, wert => motor.Beschattungsposition = Math.Clamp(wert, 0, 100));
        Setze(_lamelle, wert => motor.Lamellenposition = Math.Clamp(wert, 0, 100));
        Setze(_frei, wert => motor.Freiposition = Math.Clamp(wert, 0, 100));
        Setze(_wind, wert => motor.Windgrenze = Math.Clamp(wert, 0, 50));
        Setze(_frost, wert => motor.Frostgrenze = Math.Clamp(wert, -30, 30));

        motor.Regenschutz = _regen.IsChecked == true;
        motor.BeschattungAktiv = _beschattungAktiv.IsChecked == true;
        motor.LueftungAktiv = _lueftungAktiv.IsChecked == true;
        motor.ZeitAktiv = _zeitAktiv.IsChecked == true;

        motor.AdresseFahren = _adrFahren.Text.Trim();
        motor.AdresseStopp = _adrStopp.Text.Trim();
        motor.AdressePosition = _adrPosition.Text.Trim();
        motor.AdressePositionStatus = _adrPositionStatus.Text.Trim();
        motor.AdresseLamelle = _adrLamelle.Text.Trim();
        motor.AdresseLamelleStatus = _adrLamelleStatus.Text.Trim();

        Gespeichert?.Invoke();
        Auffrischen();
    }

    private static void Setze(TextBox feld, Action<double> was)
    {
        if (TryZahl(feld.Text, out var wert)) was(wert);
    }

    private void Anlegen()
    {
        var motor = new Motor { Name = "Antrieb " + (_dienst.Anlage.Motoren.Count + 1) };
        _dienst.Anlage.Motoren.Add(motor);
        Gespeichert?.Invoke();
        Auffrischen();
        foreach (var zeile in _zeilen)
        {
            if (zeile.Motor.Id == motor.Id) _liste.SelectedItem = zeile;
        }
    }

    private void Verdoppeln()
    {
        if (Gewaehlt is not { } motor) return;
        var kopie = motor.Clone();
        kopie.Id = Guid.NewGuid().ToString("N");
        kopie.Name = motor.Name + " (Kopie)";
        _dienst.Anlage.Motoren.Add(kopie);
        Gespeichert?.Invoke();
        Auffrischen();
    }

    private void Loeschen()
    {
        if (Gewaehlt is not { } motor) return;
        var frage = MessageBox.Show(_besitzer,
            motor.Name + " loeschen? Schaltzeiten, die darauf zeigen, laufen danach ins Leere.",
            "Antrieb loeschen", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (frage != MessageBoxResult.Yes) return;

        _dienst.Anlage.Motoren.Remove(motor);
        Gespeichert?.Invoke();
        Auffrischen();
    }

    // ---- Bausteine ---------------------------------------------------------

    private static GridViewColumn Spalte(string kopf, double breite, string pfad) => new()
    {
        Header = kopf,
        Width = breite,
        DisplayMemberBinding = new Binding(pfad),
    };

    private Button Knopf(string text, Action was)
    {
        var knopf = new Button
        {
            Content = text,
            Style = (Style)Application.Current.Resources["Knopf"],
        };
        knopf.Click += (_, _) => was();
        return knopf;
    }

    private TextBox Feld(TextBox feld, double breite = 0)
    {
        feld.Style = (Style)Application.Current.Resources["Eingabefeld"];
        if (breite > 0)
        {
            feld.Width = breite;
            feld.HorizontalAlignment = HorizontalAlignment.Left;
        }
        return feld;
    }

    private ComboBox Auswahl(ComboBox feld)
    {
        feld.Style = (Style)Application.Current.Resources["Auswahlfeld"];
        return feld;
    }

    private TextBox Adressfeld(TextBox feld, string dpt)
    {
        feld.Style = (Style)Application.Current.Resources["Adressfeld"];
        AddressSuggest.Attach(feld, _dienst, () => dpt);
        return feld;
    }

    private static TextBlock Ueberschrift(string text, double oben) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["Ueberschrift"],
        Margin = new Thickness(0, oben, 0, 4),
    };

    private static TextBlock Hinweis(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["Hinweis"],
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(100, 0, 0, 8),
    };

    private static Grid Zeile(string beschriftung, UIElement feld)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var text = new TextBlock
        {
            Text = beschriftung,
            Style = (Style)Application.Current.Resources["Beschriftung"],
        };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(feld, 1);
        g.Children.Add(text);
        g.Children.Add(feld);
        return g;
    }

    private static string Zahl(double wert) => wert.ToString("0.##", CultureInfo.CurrentCulture);

    private static bool TryZahl(string text, out double wert) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out wert)
        || double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out wert);
}
