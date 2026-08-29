using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using REGOwintergarden.App;
using REGOwintergarden.Service;

namespace REGOwintergarden.Ui;

/// <summary>
/// Das Hauptfenster - zwei Reiter, und das ist Absicht.
///
/// Vorn <b>Bedienung</b>: was gerade gilt, was als Naechstes passiert, und
/// drei Knoepfe je Antrieb. Dahinter <b>Konfiguration</b>: Anschluss,
/// Antriebe, Grenzen, Schaltzeiten, Protokoll. Wer den Wintergarten benutzt,
/// braucht den zweiten Reiter nie.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Einstellungen _einstellungen;
    private readonly Wintergartendienst _dienst;
    private readonly string _ordner = Einstellungen.StandardOrdner;

    private readonly Uebersicht _bedienung;
    private readonly Konfigurationsseite _konfiguration;

    private readonly ObservableCollection<Protokollzeile> _protokoll = new();
    private const int HoechstensZeilen = 2000;

    public MainWindow()
    {
        InitializeComponent();
        Title = Programmstand.Titel();
        Icon = AppIcons.CreateWindowIcon();

        _einstellungen = Einstellungen.Laden(_ordner);
        _dienst = new Wintergartendienst(_einstellungen, _ordner);

        _dienst.StandGeaendert += (stand, text) => AufOberflaeche(() => Verbindung(stand, text));
        _dienst.Protokolliert += zeile => AufOberflaeche(() => Melden(zeile));

        _bedienung = new Uebersicht(_dienst, this);
        _bedienung.Gespeichert += Speichern;
        TabBedienung.Content = _bedienung;

        _konfiguration = new Konfigurationsseite(_dienst, this, Protokollseite());
        _konfiguration.Gespeichert += () =>
        {
            Speichern();
            _bedienung.Auffrischen();
        };
        TabKonfiguration.Content = _konfiguration;

        Verbindung(Busstand.Getrennt, null);

        // Die Automatik laeuft mit, solange das Fenster offen ist. Ist der
        // Dienst eingerichtet, rechnet der ohnehin - dann waere das hier ein
        // zweiter Absender auf denselben Adressen.
        if (!Dienstlauf.Eingerichtet()) _dienst.Starten();

        if (_einstellungen.Gateway.Length > 0) _ = _dienst.VerbindenAsync(_einstellungen.Gateway);
    }

    private void AufOberflaeche(Action was)
    {
        if (Dispatcher.CheckAccess()) was();
        else Dispatcher.BeginInvoke(was);
    }

    // ---- Fusszeile ---------------------------------------------------------

    private void Verbindung(Busstand stand, string? text)
    {
        LinkText.Text = stand switch
        {
            Busstand.Verbunden => "Mit dem KNX-Bus verbunden",
            Busstand.Verbinde => "Verbindung wird aufgebaut…",
            Busstand.Fehler => "Keine Busverbindung — " + (text ?? "Fehler"),
            _ => "Nicht mit dem Bus verbunden — unter Konfiguration einrichten",
        };
        LinkText.Foreground = (Brush)FindResource(stand switch
        {
            Busstand.Verbunden => "Gut",
            Busstand.Fehler => "Fehler",
            _ => "Nebenschrift",
        });

        _konfiguration?.Auffrischen();
    }

    // ---- Protokoll ---------------------------------------------------------

    private UIElement Protokollseite()
    {
        var aussen = new DockPanel { Margin = new Thickness(12) };

        var kopf = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        kopf.Children.Add(Bausteine.Knopf("Leeren", () => _protokoll.Clear()));
        kopf.Children.Add(Bausteine.Knopf("Ordner oeffnen", () =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _ordner,
                    UseShellExecute = true,
                });
            }
            catch (System.ComponentModel.Win32Exception) { }
        }));
        kopf.Children.Add(new TextBlock
        {
            Text = "Dasselbe steht in protokoll.log im Einstellungsordner - dort auch das, was der "
                   + "Dienst gemeldet hat.",
            Style = (Style)Application.Current.Resources["Hinweis"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        });
        DockPanel.SetDock(kopf, Dock.Top);
        aussen.Children.Add(kopf);

        var liste = new ListView
        {
            ItemsSource = _protokoll,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
        };
        var spalten = new GridView();
        spalten.Columns.Add(new GridViewColumn
        {
            Header = "Zeit",
            Width = 80,
            DisplayMemberBinding = new Binding(nameof(Protokollzeile.Uhrzeit)),
        });
        spalten.Columns.Add(new GridViewColumn
        {
            Header = "Was",
            Width = 220,
            DisplayMemberBinding = new Binding(nameof(Protokollzeile.Was)),
        });
        spalten.Columns.Add(new GridViewColumn
        {
            Header = "Dazu",
            Width = 820,
            DisplayMemberBinding = new Binding(nameof(Protokollzeile.Dazu)),
        });
        liste.View = spalten;

        aussen.Children.Add(new Border
        {
            Style = (Style)Application.Current.Resources["Listenkarte"],
            Child = liste,
        });
        return aussen;
    }

    private void Melden(Protokollzeile zeile)
    {
        _protokoll.Insert(0, zeile);
        while (_protokoll.Count > HoechstensZeilen) _protokoll.RemoveAt(_protokoll.Count - 1);

        // Die Fusszeile zeigt immer die letzte Meldung. Das ist die zweite
        // Haelfte der Durchschaubarkeit: oben steht, was gilt, unten, was
        // zuletzt wirklich getan wurde.
        StatusText.Text = zeile.Uhrzeit + "  " + zeile.Was + ": " + zeile.Dazu;
        StatusText.Foreground = (Brush)FindResource(zeile.Problem ? "Fehler" : "Nebenschrift");
    }

    // ---- Sichern -----------------------------------------------------------

    private void Speichern()
    {
        _einstellungen.Gateway = _konfiguration?.Gateway ?? _einstellungen.Gateway;
        _einstellungen.Speichern(_ordner);
    }

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Speichern();
        await _dienst.DisposeAsync();
    }
}
