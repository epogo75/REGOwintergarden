using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using REGOwintergarden.Model;

namespace REGOwintergarden.Ui;

/// <summary>
/// Der Sonnenkompass: wo die Sonne steht, wie hoch sie steht und auf welche
/// Flaechen sie gerade scheint.
///
/// <b>Warum ein Bild und keine zwei Zahlen:</b> „Azimut 213 Grad, Elevation
/// 41 Grad" beantwortet die Frage nicht, die man hat - naemlich ob die Sonne
/// auf die Suedseite scheint und warum die Markise dort draussen steht. Im
/// Kompass sieht man beides auf einen Blick: den Strahl und den Sektor, den
/// er trifft.
///
/// Die Hoehe steckt im Abstand zur Mitte: aussen am Rand steht die Sonne am
/// Horizont, in der Mitte im Zenit. Das ist die uebliche Darstellung eines
/// Sonnenwegs und liest sich nach einmal Hinsehen von selbst.
/// </summary>
public sealed class Kompass : Canvas
{
    private IReadOnlyList<Motor> _motoren = Array.Empty<Motor>();
    private IReadOnlyList<Lage> _lagen = Array.Empty<Lage>();
    private Sonnenstand _sonne = new(180, 0, null, null);

    public Kompass()
    {
        Width = 320;
        Height = 320;
        Background = Brushes.Transparent;
        SizeChanged += (_, _) => Zeichnen();
    }

    public void Zeigen(IReadOnlyList<Motor> motoren, IReadOnlyList<Lage> lagen, Sonnenstand sonne)
    {
        _motoren = motoren;
        _lagen = lagen;
        _sonne = sonne;
        Zeichnen();
    }

    private Brush Farbe(string name) => (Brush)Application.Current.Resources[name];

    private void Zeichnen()
    {
        Children.Clear();

        var mitte = new Point(Width / 2, Height / 2);
        var rand = Math.Min(Width, Height) / 2 - 26;

        // Der Kreis ist der Horizont, die inneren Ringe sind je 30 Grad Hoehe.
        for (var i = 3; i >= 1; i--)
        {
            var r = rand * i / 3.0;
            Children.Add(Kreis(mitte, r, Farbe(i == 3 ? "Linie" : "Ruhe"), i == 3 ? 1.5 : 1));
        }

        // Die Sektoren der Flaechen: wohin ein Antrieb zeigt und wie weit die
        // Sonne dabei seitlich stehen darf.
        foreach (var motor in _motoren)
        {
            if (!motor.KannBeschatten) continue;
            var beschattet = Beschattet(motor);
            Children.Add(Sektor(mitte, rand, motor.Ausrichtung, motor.Oeffnungswinkel,
                beschattet ? Farbe("Betont") : Farbe("Ruhe"), beschattet ? 0.28 : 0.16));
        }

        // Die Striche der Himmelsrichtungen.
        foreach (var (grad, name) in new[] { (0.0, "N"), (90.0, "O"), (180.0, "S"), (270.0, "W") })
        {
            var aussen = Punkt(mitte, rand, grad);
            var innen = Punkt(mitte, rand - 8, grad);
            Children.Add(Strich(innen, aussen, Farbe("Nebenschrift"), 1));

            var beschriftung = new TextBlock
            {
                Text = name,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Farbe("Nebenschrift"),
            };
            var stelle = Punkt(mitte, rand + 13, grad);
            beschriftung.Measure(new Size(100, 100));
            SetLeft(beschriftung, stelle.X - beschriftung.DesiredSize.Width / 2);
            SetTop(beschriftung, stelle.Y - beschriftung.DesiredSize.Height / 2);
            Children.Add(beschriftung);
        }

        // Auf- und Untergang als kurze Marken am Horizont. Sie stehen dort,
        // wo die Sonne die Kreislinie kreuzt - nicht als Uhrzeit, sondern als
        // Richtung, denn genau die aendert sich uebers Jahr.
        Marke(mitte, rand, _sonne.Aufgang, "auf", Farbe("Gut"));
        Marke(mitte, rand, _sonne.Untergang, "unter", Farbe("Nebenschrift"));

        // Die Sonne. Unter dem Horizont wird sie nicht gezeichnet - sie waere
        // sonst ausserhalb des Kreises, und ein Punkt neben dem Bild sagt
        // weniger als seine Abwesenheit.
        if (_sonne.Elevation > 0)
        {
            var abstand = rand * (1 - Math.Clamp(_sonne.Elevation, 0, 90) / 90.0);
            var stelle = Punkt(mitte, abstand, _sonne.Azimut);

            Children.Add(Strich(mitte, stelle, Farbe("Blass"), 1));

            var sonne = Symbole.Zeichnen(Symbole.Sonne, Farbe("Fehler"), 30, 1.8);
            SetLeft(sonne, stelle.X - 15);
            SetTop(sonne, stelle.Y - 15);
            Children.Add(sonne);
        }

        // In der Mitte die Zahlen - das Bild zeigt das Wo, die Zahlen das
        // Genau.
        var text = new StackPanel { Width = 130 };
        text.Children.Add(new TextBlock
        {
            Text = _sonne.Elevation > 0 ? "Sonne" : "Sonne unter",
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            Foreground = Farbe("Nebenschrift"),
        });
        text.Children.Add(new TextBlock
        {
            Text = Grad(_sonne.Azimut) + "  ·  " + Grad(_sonne.Elevation),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            Foreground = Farbe("Schrift"),
        });
        text.Children.Add(new TextBlock
        {
            Text = Auf() + "  " + Unter(),
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            Foreground = Farbe("Nebenschrift"),
        });
        text.Measure(new Size(200, 200));
        SetLeft(text, mitte.X - 65);
        SetTop(text, mitte.Y - text.DesiredSize.Height / 2);
        Children.Add(text);
    }

    private string Auf() => _sonne.Aufgang is { } zeit
        ? "↑ " + zeit.ToString("HH:mm", CultureInfo.CurrentCulture)
        : "";

    private string Unter() => _sonne.Untergang is { } zeit
        ? "↓ " + zeit.ToString("HH:mm", CultureInfo.CurrentCulture)
        : "";

    private bool Beschattet(Motor motor)
    {
        foreach (var lage in _lagen)
        {
            if (lage.Motor.Id == motor.Id) return lage.Stufe == Stufe.Beschattung && lage.Ziel > 0;
        }
        return false;
    }

    private void Marke(Point mitte, double rand, DateTime? zeit, string was, Brush farbe)
    {
        if (zeit is null) return;

        // Die Richtung des Auf- oder Untergangs: der Azimut zu dieser Zeit.
        // Gerechnet wird sie nicht noch einmal - genaehert ueber die Uhrzeit
        // waere sie falsch. Stattdessen steht die Marke am Rand des Kreises
        // dort, wo der Horizont liegt, und die Uhrzeit dazu in der Mitte.
        var beschriftung = new TextBlock
        {
            Text = was + " " + zeit.Value.ToString("HH:mm", CultureInfo.CurrentCulture),
            FontSize = 10,
            Foreground = farbe,
        };
        beschriftung.Measure(new Size(120, 40));
        SetLeft(beschriftung, was == "auf" ? 2 : Width - beschriftung.DesiredSize.Width - 2);
        SetTop(beschriftung, Height - beschriftung.DesiredSize.Height - 2);
        Children.Add(beschriftung);
    }

    private static string Grad(double wert) =>
        Math.Round(wert).ToString("0", CultureInfo.CurrentCulture) + "°";

    /// <summary>Ein Punkt auf dem Kreis. Null Grad ist oben, im Uhrzeigersinn.</summary>
    private static Point Punkt(Point mitte, double abstand, double grad)
    {
        var bogen = (grad - 90) * Math.PI / 180.0;
        return new Point(mitte.X + abstand * Math.Cos(bogen), mitte.Y + abstand * Math.Sin(bogen));
    }

    private static Ellipse Kreis(Point mitte, double r, Brush farbe, double staerke)
    {
        var kreis = new Ellipse
        {
            Width = r * 2,
            Height = r * 2,
            Stroke = farbe,
            StrokeThickness = staerke,
        };
        SetLeft(kreis, mitte.X - r);
        SetTop(kreis, mitte.Y - r);
        return kreis;
    }

    private static Line Strich(Point von, Point bis, Brush farbe, double staerke) => new()
    {
        X1 = von.X,
        Y1 = von.Y,
        X2 = bis.X,
        Y2 = bis.Y,
        Stroke = farbe,
        StrokeThickness = staerke,
    };

    /// <summary>Ein Kreisausschnitt - die Flaeche, die ein Antrieb sieht.</summary>
    private static Path Sektor(Point mitte, double r, double richtung, double halbeBreite,
        Brush farbe, double deckung)
    {
        var von = Punkt(mitte, r, richtung - halbeBreite);
        var bis = Punkt(mitte, r, richtung + halbeBreite);
        var gross = halbeBreite * 2 > 180;

        var figur = new PathFigure { StartPoint = mitte, IsClosed = true };
        figur.Segments.Add(new LineSegment(von, isStroked: false));
        figur.Segments.Add(new ArcSegment(bis, new Size(r, r), 0, gross,
            SweepDirection.Clockwise, isStroked: false));

        var geometrie = new PathGeometry();
        geometrie.Figures.Add(figur);

        return new Path { Data = geometrie, Fill = farbe, Opacity = deckung };
    }
}
