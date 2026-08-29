using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace REGOwintergarden.Ui;

/// <summary>
/// Die immer gleichen Teile eines Einrichteformulars.
///
/// An einer Stelle, weil sie sonst auf jeder Seite leicht anders aussehen:
/// eine Beschriftungsspalte mit 100, ein Zeilenabstand von 8, Hinweise unter
/// dem Feld auf derselben Spalte. Das ist kein Geschmack, sondern der
/// Unterschied zwischen einer Oberflaeche und vier Oberflaechen.
/// </summary>
public static class Bausteine
{
    public const double Beschriftungsbreite = 100;

    public static Grid Zeile(string beschriftung, UIElement feld)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Beschriftungsbreite) });
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

    public static TextBlock Ueberschrift(string text, double oben = 12) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["Ueberschrift"],
        Margin = new Thickness(0, oben, 0, 4),
    };

    public static TextBlock Hinweis(string text, bool eingerueckt = true) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["Hinweis"],
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(eingerueckt ? Beschriftungsbreite : 0, 0, 0, 8),
    };

    public static TextBox Feld(TextBox feld, double breite = 0)
    {
        feld.Style = (Style)Application.Current.Resources["Eingabefeld"];
        if (breite > 0)
        {
            feld.Width = breite;
            feld.HorizontalAlignment = HorizontalAlignment.Left;
        }
        return feld;
    }

    public static Button Knopf(string text, Action was, bool stark = false)
    {
        var knopf = new Button
        {
            Content = text,
            Style = (Style)Application.Current.Resources[stark ? "KnopfStark" : "Knopf"],
        };
        knopf.Click += (_, _) => was();
        return knopf;
    }

    public static string Zahl(double wert) => wert.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>
    /// Liest eine Zahl - mit Komma und mit Punkt.
    ///
    /// Wer 8,5 tippt, meint achteinhalb, und wer 8.5 tippt, auch. Eine
    /// Steuerung, die daran scheitert, laesst den Anwender raten, was sie
    /// erwartet.
    /// </summary>
    public static bool TryZahl(string? text, out double wert) =>
        double.TryParse((text ?? "").Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out wert)
        || double.TryParse((text ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out wert);

    public static void Setze(TextBox feld, Action<double> was)
    {
        if (TryZahl(feld.Text, out var wert)) was(wert);
    }
}
