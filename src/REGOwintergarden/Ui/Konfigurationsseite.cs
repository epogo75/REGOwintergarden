using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using REGOwintergarden.App;

namespace REGOwintergarden.Ui;

/// <summary>
/// Der Konfigurationsbereich - alles, was einmal eingerichtet und danach
/// selten angefasst wird.
///
/// <b>Warum getrennt:</b> wer den Wintergarten benutzt, will wissen, ob die
/// Markise gleich ausfaehrt. Wer ihn eingerichtet hat, will Gruppenadressen
/// eintragen. Das sind zwei verschiedene Leute mit zwei verschiedenen Fragen,
/// und eine Oberflaeche, die beides mischt, bedient keinen von beiden. Vorn
/// steht deshalb nur die Bedienung; hier liegt der Rest, deutlich als
/// Fachbereich gekennzeichnet.
/// </summary>
public sealed class Konfigurationsseite : UserControl
{
    private readonly Verbindungsseite _verbindung;
    private readonly Antriebsseite _antriebe;
    private readonly Anlageseite _anlage;
    private readonly Zeitseite _zeiten;

    public Konfigurationsseite(Wintergartendienst dienst, Window besitzer, UIElement protokoll)
    {
        _verbindung = new Verbindungsseite(dienst, besitzer);
        _antriebe = new Antriebsseite(dienst, besitzer);
        _anlage = new Anlageseite(dienst, besitzer);
        _zeiten = new Zeitseite(dienst, besitzer);

        _verbindung.Gespeichert += () => Gespeichert?.Invoke();
        _antriebe.Gespeichert += () =>
        {
            Gespeichert?.Invoke();
            _zeiten.Auffrischen();
        };
        _anlage.Gespeichert += () => Gespeichert?.Invoke();
        _zeiten.Gespeichert += () => Gespeichert?.Invoke();

        Content = Aufbau(protokoll);
    }

    public event Action? Gespeichert;

    /// <summary>Die eingetragene Gatewayadresse.</summary>
    public string Gateway => _verbindung.Gateway;

    /// <summary>Schreibt Anschluss und Betriebsart in die Einstellungen.</summary>
    public void Uebernehmen() => _verbindung.Uebernehmen();

    private UIElement Aufbau(UIElement protokoll)
    {
        var aussen = new DockPanel();

        // Ein Band, das sagt, wo man hier ist. Ohne das landet jemand, der
        // eigentlich nur die Markise einfahren wollte, in den
        // Gruppenadressen und haelt das Programm fuer kompliziert.
        var band = new Border
        {
            Background = (Brush)Application.Current.Resources["Ruhe"],
            BorderBrush = (Brush)Application.Current.Resources["Linie"],
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 8, 12, 8),
        };
        var bandtext = new StackPanel();
        bandtext.Children.Add(new TextBlock
        {
            Text = "Konfiguration",
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["Schrift"],
        });
        bandtext.Children.Add(new TextBlock
        {
            Text = "Hier wird die Anlage eingerichtet - Anschluss, Antriebe, Grenzen, Schaltzeiten. "
                   + "Fuer den taeglichen Gebrauch reicht der Reiter Bedienung.",
            Style = (Style)Application.Current.Resources["Hinweis"],
            TextWrapping = TextWrapping.Wrap,
        });
        band.Child = bandtext;
        DockPanel.SetDock(band, Dock.Top);
        aussen.Children.Add(band);

        var reiter = new TabControl
        {
            Background = (Brush)Application.Current.Resources["Flaeche"],
            BorderBrush = (Brush)Application.Current.Resources["Linie"],
            BorderThickness = new Thickness(0),
        };
        reiter.Items.Add(Reiter("Anschluss", _verbindung));
        reiter.Items.Add(Reiter("Antriebe", _antriebe));
        reiter.Items.Add(Reiter("Wetter und Grenzen", _anlage));
        reiter.Items.Add(Reiter("Zeitschaltuhr", _zeiten));
        reiter.Items.Add(Reiter("Protokoll", protokoll));
        aussen.Children.Add(reiter);
        return aussen;
    }

    private static TabItem Reiter(string kopf, object inhalt) => new()
    {
        Header = kopf,
        Padding = new Thickness(12, 6, 12, 6),
        Content = inhalt,
    };

    public void Auffrischen()
    {
        _verbindung.Auffrischen();
        _antriebe.Auffrischen();
        _anlage.Auffrischen();
        _zeiten.Auffrischen();
    }
}
