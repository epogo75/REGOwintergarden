using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using REGOwintergarden.App;
using REGOwintergarden.Service;

namespace REGOwintergarden.Ui;

public partial class MainWindow : Window
{
    private readonly Einstellungen _einstellungen;
    private readonly Wintergartendienst _dienst;
    private readonly string _ordner = Einstellungen.StandardOrdner;

    private readonly Uebersicht _uebersicht;
    private readonly Antriebsseite _antriebe;
    private readonly Anlageseite _anlage;
    private readonly Zeitseite _zeiten;

    private readonly ObservableCollection<Protokollzeile> _protokoll = new();
    private const int HoechstensZeilen = 2000;

    public MainWindow()
    {
        InitializeComponent();
        Title = Programmstand.Titel();

        _einstellungen = Einstellungen.Laden(_ordner);
        _dienst = new Wintergartendienst(_einstellungen, _ordner);

        GatewayBox.Text = _einstellungen.Gateway;
        _dienst.StandGeaendert += (stand, text) => AufOberflaeche(() => Verbindung(stand, text));
        _dienst.Protokolliert += zeile => AufOberflaeche(() => Melden(zeile));

        _uebersicht = new Uebersicht(_dienst, this);
        _uebersicht.Gespeichert += Speichern;
        TabUebersicht.Content = _uebersicht;

        _antriebe = new Antriebsseite(_dienst, this);
        TabAntriebe.Content = _antriebe;

        _anlage = new Anlageseite(_dienst, this);
        _anlage.Gespeichert += () =>
        {
            Speichern();
            _uebersicht.Auffrischen();
        };
        TabAnlage.Content = _anlage;

        _zeiten = new Zeitseite(_dienst, this);
        _zeiten.Gespeichert += () =>
        {
            Speichern();
            _uebersicht.Auffrischen();
        };
        TabZeiten.Content = _zeiten;

        // Erst jetzt anhaengen: die Antriebsseite frischt die Zeitseite mit
        // auf, und die gibt es vorher noch nicht.
        _antriebe.Gespeichert += () =>
        {
            Speichern();
            _uebersicht.Auffrischen();
            _zeiten.Auffrischen();
        };

        TabProtokoll.Content = Protokollseite();

        Verbindung(Busstand.Getrennt, null);
        Dienstlage();

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

    // ---- Kopfzeile ---------------------------------------------------------

    private void Verbindung(Busstand stand, string? text)
    {
        ButtonConnect.Content = stand == Busstand.Verbunden ? "Trennen" : "Verbinden";
        LinkText.Text = stand switch
        {
            Busstand.Verbunden => "verbunden — eigene Adresse " + text,
            Busstand.Verbinde => "verbinde…",
            Busstand.Fehler => text ?? "Fehler",
            _ => "getrennt",
        };
        LinkText.Foreground = (Brush)FindResource(stand switch
        {
            Busstand.Verbunden => "Gut",
            Busstand.Fehler => "Fehler",
            _ => "Nebenschrift",
        });
    }

    private void Dienstlage()
    {
        StatusText.Text = Dienstlauf.Eingerichtet()
            ? "Der Windows-Dienst ist eingerichtet und rechnet - dieses Fenster zeigt nur an."
            : "Die Automatik laeuft in diesem Fenster. Fuer den Dauerbetrieb den Dienst einrichten "
              + "(Reiter Anlage).";
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        ButtonConnect.IsEnabled = false;
        try
        {
            if (_dienst.Stand == Busstand.Verbunden)
            {
                await _dienst.TrennenAsync();
            }
            else
            {
                _einstellungen.Gateway = GatewayBox.Text.Trim();
                Speichern();
                await _dienst.VerbindenAsync(_einstellungen.Gateway);
            }
        }
        finally
        {
            ButtonConnect.IsEnabled = true;
        }
    }

    private async void OnReadClick(object sender, RoutedEventArgs e) => await _dienst.AbfragenAsync();

    private void OnAboutClick(object sender, RoutedEventArgs e) =>
        new UeberFenster { Owner = this }.ShowDialog();

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
            Width = 200,
            DisplayMemberBinding = new Binding(nameof(Protokollzeile.Was)),
        });
        spalten.Columns.Add(new GridViewColumn
        {
            Header = "Dazu",
            Width = 800,
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
    }

    // ---- Sichern -----------------------------------------------------------

    private void Speichern()
    {
        _einstellungen.Gateway = GatewayBox.Text.Trim();
        _einstellungen.Speichern(_ordner);
    }

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Speichern();
        await _dienst.DisposeAsync();
    }
}
