using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using REGOwintergarden.App;
using REGOwintergarden.Model;

namespace REGOwintergarden.Ui;

/// <summary>
/// Der Langzeittrend: Temperaturen, Wind und Helligkeit als Kurven, darueber
/// die Ereignisse der Steuerung.
///
/// <b>Warum ueberblendet:</b> eine Kurve allein beantwortet die Frage nicht,
/// die gestellt wird. „Am Dienstag war es doch heiss - warum war die Markise
/// oben?" laesst sich nur beantworten, wenn man sieht, dass um 11:20 ein
/// Windalarm kam. Deshalb liegen die Ereignisse als senkrechte Striche in
/// derselben Zeitachse.
///
/// <b>Warum jede Kurve ihre eigene Skala hat:</b> Grad, Meter je Sekunde und
/// Lux in eine Achse zu zwingen macht aus der Helligkeit einen Strich am
/// oberen Rand und aus der Temperatur eine Linie am unteren. Jede Kurve wird
/// deshalb auf ihren eigenen Bereich gespreizt, und der Bereich steht in der
/// Beschriftung - dort liest man die Zahlen ohnehin ab.
/// </summary>
public sealed class Verlaufsseite : UserControl
{
    private readonly Wintergartendienst _dienst;

    private readonly Canvas _bild = new();
    private readonly StackPanel _beschriftung = new();
    private readonly TextBlock _stand = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _zeitraum = new();

    private readonly CheckBox _innen = new() { Content = "Innen", IsChecked = true };
    private readonly CheckBox _aussen = new() { Content = "Aussen", IsChecked = true };
    private readonly CheckBox _wind = new() { Content = "Wind" };
    private readonly CheckBox _hell = new() { Content = "Helligkeit", IsChecked = true };
    private readonly CheckBox _ereignisse = new() { Content = "Steuerung einblenden", IsChecked = true };

    private IReadOnlyList<Messpunkt> _punkte = Array.Empty<Messpunkt>();
    private IReadOnlyList<Ereignis> _marken = Array.Empty<Ereignis>();

    public Verlaufsseite(Wintergartendienst dienst)
    {
        _dienst = dienst;
        Content = Aufbau();
        Laden();
    }

    private Brush Farbe(string name) => (Brush)Application.Current.Resources[name];

    private static readonly Color Innenfarbe = Color.FromRgb(0xCF, 0x22, 0x2E);
    private static readonly Color Aussenfarbe = Color.FromRgb(0x1F, 0x6F, 0xB2);
    private static readonly Color Windfarbe = Color.FromRgb(0x6B, 0x4E, 0xA8);
    private static readonly Color Hellfarbe = Color.FromRgb(0xE0, 0x9F, 0x00);

    // ---- Aufbau -----------------------------------------------------------

    private UIElement Aufbau()
    {
        var aussen = new DockPanel { Margin = new Thickness(12) };

        var kopf = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        var reihe = new StackPanel { Orientation = Orientation.Horizontal };

        reihe.Children.Add(new TextBlock
        {
            Text = "Zeitraum",
            Style = (Style)Application.Current.Resources["Beschriftung"],
            Width = 70,
        });
        foreach (var name in new[] { "heute", "letzte 24 Stunden", "7 Tage", "30 Tage" }) _zeitraum.Items.Add(name);
        _zeitraum.Style = (Style)Application.Current.Resources["Auswahlfeld"];
        _zeitraum.Width = 170;
        _zeitraum.SelectedIndex = 1;
        _zeitraum.SelectionChanged += (_, _) => Laden();
        _zeitraum.Margin = new Thickness(0, 0, 16, 0);
        reihe.Children.Add(_zeitraum);

        foreach (var haken in new[] { _innen, _aussen, _wind, _hell, _ereignisse })
        {
            haken.Margin = new Thickness(0, 0, 14, 0);
            haken.VerticalAlignment = VerticalAlignment.Center;
            haken.Click += (_, _) => Zeichnen();
            reihe.Children.Add(haken);
        }

        reihe.Children.Add(Bausteine.Knopf("Neu laden", Laden));
        kopf.Children.Add(reihe);

        _stand.Style = (Style)Application.Current.Resources["Hinweis"];
        _stand.Margin = new Thickness(70, 6, 0, 0);
        kopf.Children.Add(_stand);
        DockPanel.SetDock(kopf, Dock.Top);
        aussen.Children.Add(kopf);

        _beschriftung.Orientation = Orientation.Horizontal;
        _beschriftung.Margin = new Thickness(0, 8, 0, 0);
        DockPanel.SetDock(_beschriftung, Dock.Bottom);
        aussen.Children.Add(_beschriftung);

        _bild.Background = Brushes.Transparent;
        _bild.ClipToBounds = true;
        _bild.SizeChanged += (_, _) => Zeichnen();
        aussen.Children.Add(new Border
        {
            Style = (Style)Application.Current.Resources["Gruppenkarte"],
            Child = _bild,
        });
        return aussen;
    }

    // ---- Laden ------------------------------------------------------------

    private (DateTime Von, DateTime Bis) Zeitspanne()
    {
        var jetzt = DateTime.Now;
        return _zeitraum.SelectedIndex switch
        {
            0 => (jetzt.Date, jetzt),
            2 => (jetzt.AddDays(-7), jetzt),
            3 => (jetzt.AddDays(-30), jetzt),
            _ => (jetzt.AddHours(-24), jetzt),
        };
    }

    public void Laden()
    {
        var (von, bis) = Zeitspanne();
        _punkte = _dienst.Verlauf.Messwerte(von, bis);
        _marken = _dienst.Verlauf.Ereignisse(von, bis);

        _stand.Text = _punkte.Count == 0
            ? "Noch keine Aufzeichnung fuer diesen Zeitraum. Aufgezeichnet wird, sobald die Automatik "
              + "laeuft - je Minute ein Wert."
            : _punkte.Count.ToString(CultureInfo.CurrentCulture) + " Messpunkte von "
              + _punkte[0].Zeit.ToString("dd.MM. HH:mm", CultureInfo.CurrentCulture) + " bis "
              + _punkte[^1].Zeit.ToString("dd.MM. HH:mm", CultureInfo.CurrentCulture)
              + "  ·  " + _marken.Count.ToString(CultureInfo.CurrentCulture) + " Ereignisse";

        Zeichnen();
    }

    // ---- Zeichnen ---------------------------------------------------------

    private sealed record Reihe(string Name, Color Farbe, Func<Messpunkt, double?> Wert, string Einheit);

    private void Zeichnen()
    {
        _bild.Children.Clear();
        _beschriftung.Children.Clear();

        var breite = _bild.ActualWidth;
        var hoehe = _bild.ActualHeight;
        if (breite < 40 || hoehe < 40) return;

        if (_punkte.Count < 2)
        {
            _bild.Children.Add(new TextBlock
            {
                Text = "Noch nichts aufgezeichnet.",
                Foreground = Farbe("Blass"),
                Margin = new Thickness(12),
            });
            return;
        }

        // Auf die Bildbreite ausduennen: mehr Punkte als Bildpunkte ergeben
        // keine feinere Kurve, nur eine langsamere.
        var punkte = Aufzeichnung.Ausduennen(_punkte, Math.Max(2, (int)breite));
        var (von, bis) = (punkte[0].Zeit, punkte[^1].Zeit);
        var spanne = (bis - von).TotalSeconds;
        if (spanne <= 0) return;

        const double links = 8;
        const double rechts = 8;
        const double oben = 10;
        const double unten = 22;
        var flaeche = new Rect(links, oben, Math.Max(1, breite - links - rechts),
            Math.Max(1, hoehe - oben - unten));

        Zeitachse(punkte, flaeche, von, spanne);

        // Erst die Ereignisse, dann die Kurven - so liegen die Linien oben.
        if (_ereignisse.IsChecked == true) Ereignisse(flaeche, von, spanne);

        var reihen = new List<Reihe>();
        if (_innen.IsChecked == true) reihen.Add(new Reihe("Innen", Innenfarbe, p => p.Innen, "°C"));
        if (_aussen.IsChecked == true) reihen.Add(new Reihe("Aussen", Aussenfarbe, p => p.Aussen, "°C"));
        if (_wind.IsChecked == true) reihen.Add(new Reihe("Wind", Windfarbe, p => p.Wind, "m/s"));
        if (_hell.IsChecked == true) reihen.Add(new Reihe("Helligkeit", Hellfarbe, p => p.Helligkeit, "Lux"));

        foreach (var reihe in reihen) Kurve(punkte, reihe, flaeche, von, spanne);
    }

    private void Zeitachse(IReadOnlyList<Messpunkt> punkte, Rect flaeche, DateTime von, double spanne)
    {
        // Vier bis fuenf Striche reichen. Mehr Beschriftung macht die Achse
        // nicht genauer, nur unruhiger.
        const int striche = 4;
        for (var i = 0; i <= striche; i++)
        {
            var anteil = (double)i / striche;
            var x = flaeche.Left + flaeche.Width * anteil;
            var zeit = von.AddSeconds(spanne * anteil);

            _bild.Children.Add(new Line
            {
                X1 = x,
                Y1 = flaeche.Top,
                X2 = x,
                Y2 = flaeche.Bottom,
                Stroke = Farbe("Ruhe"),
                StrokeThickness = 1,
            });

            var text = new TextBlock
            {
                Text = spanne > 3 * 24 * 3600
                    ? zeit.ToString("dd.MM.", CultureInfo.CurrentCulture)
                    : zeit.ToString("dd.MM. HH:mm", CultureInfo.CurrentCulture),
                FontSize = 10,
                Foreground = Farbe("Blass"),
            };
            text.Measure(new Size(200, 40));
            Canvas.SetLeft(text, Math.Min(flaeche.Right - text.DesiredSize.Width,
                Math.Max(0, x - text.DesiredSize.Width / 2)));
            Canvas.SetTop(text, flaeche.Bottom + 4);
            _bild.Children.Add(text);
        }
    }

    private void Ereignisse(Rect flaeche, DateTime von, double spanne)
    {
        foreach (var ereignis in _marken)
        {
            var anteil = (ereignis.Zeit - von).TotalSeconds / spanne;
            if (anteil is < 0 or > 1) continue;

            var x = flaeche.Left + flaeche.Width * anteil;
            var farbe = ereignis.Stufe switch
            {
                Stufe.Wind or Stufe.Regen or Stufe.Frost => (Brush)new SolidColorBrush(
                    Color.FromArgb(0x90, 0xCF, 0x22, 0x2E)),
                Stufe.Beschattung => new SolidColorBrush(Color.FromArgb(0x70, 0xE0, 0x9F, 0x00)),
                Stufe.Lueftung => new SolidColorBrush(Color.FromArgb(0x70, 0x1F, 0x6F, 0xB2)),
                Stufe.Hand => new SolidColorBrush(Color.FromArgb(0x70, 0x65, 0x65, 0x6D)),
                Stufe.Zeit => new SolidColorBrush(Color.FromArgb(0x70, 0x4D, 0x76, 0x16)),
                _ => new SolidColorBrush(Color.FromArgb(0x30, 0x8B, 0x8B, 0x93)),
            };

            var strich = new Line
            {
                X1 = x,
                Y1 = flaeche.Top,
                X2 = x,
                Y2 = flaeche.Bottom,
                Stroke = farbe,
                StrokeThickness = ereignis.Stufe >= Stufe.Frost ? 2 : 1,
                ToolTip = ereignis.Zeit.ToString("dd.MM. HH:mm", CultureInfo.CurrentCulture)
                          + "  " + ereignis.Antrieb + "\n" + ereignis.Stufe + ": " + ereignis.Grund,
            };
            _bild.Children.Add(strich);
        }
    }

    private void Kurve(IReadOnlyList<Messpunkt> punkte, Reihe reihe, Rect flaeche, DateTime von, double spanne)
    {
        double? kleinster = null;
        double? groesster = null;
        foreach (var punkt in punkte)
        {
            if (reihe.Wert(punkt) is not { } wert) continue;
            kleinster = kleinster is null ? wert : Math.Min(kleinster.Value, wert);
            groesster = groesster is null ? wert : Math.Max(groesster.Value, wert);
        }

        if (kleinster is null || groesster is null)
        {
            Beschriftung(reihe, "kein Wert");
            return;
        }

        // Ein flacher Verlauf braucht trotzdem Hoehe, sonst liegt die Kurve
        // als Strich auf der Achse und sieht aus wie ein Ausfall.
        var unterschied = groesster.Value - kleinster.Value;
        if (unterschied < 1e-6)
        {
            kleinster -= 1;
            groesster += 1;
            unterschied = 2;
        }

        var figur = new PathFigure();
        var begonnen = false;
        var geometrie = new PathGeometry();

        foreach (var punkt in punkte)
        {
            if (reihe.Wert(punkt) is not { } wert)
            {
                // Eine Luecke bleibt eine Luecke. Sie zu ueberbruecken hiesse,
                // einen Messwert zu erfinden, den es nie gab.
                if (begonnen)
                {
                    geometrie.Figures.Add(figur);
                    figur = new PathFigure();
                    begonnen = false;
                }
                continue;
            }

            var x = flaeche.Left + flaeche.Width * ((punkt.Zeit - von).TotalSeconds / spanne);
            var y = flaeche.Bottom - flaeche.Height * ((wert - kleinster.Value) / unterschied);
            var stelle = new Point(x, y);

            if (!begonnen)
            {
                figur.StartPoint = stelle;
                begonnen = true;
            }
            else
            {
                figur.Segments.Add(new LineSegment(stelle, isStroked: true));
            }
        }
        if (begonnen) geometrie.Figures.Add(figur);

        _bild.Children.Add(new Path
        {
            Data = geometrie,
            Stroke = new SolidColorBrush(reihe.Farbe),
            StrokeThickness = 1.6,
            StrokeLineJoin = PenLineJoin.Round,
        });

        Beschriftung(reihe, Zahl(kleinster.Value) + " bis " + Zahl(groesster.Value) + " " + reihe.Einheit);
    }

    private void Beschriftung(Reihe reihe, string bereich)
    {
        var zeile = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
        zeile.Children.Add(new Rectangle
        {
            Width = 18,
            Height = 3,
            Fill = new SolidColorBrush(reihe.Farbe),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });
        zeile.Children.Add(new TextBlock
        {
            Text = reihe.Name + "  " + bereich,
            FontSize = 11,
            Foreground = Farbe("Nebenschrift"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        _beschriftung.Children.Add(zeile);
    }

    private static string Zahl(double wert) => Math.Abs(wert) >= 1000
        ? (wert / 1000).ToString("0.#", CultureInfo.CurrentCulture) + "k"
        : wert.ToString("0.#", CultureInfo.CurrentCulture);
}
