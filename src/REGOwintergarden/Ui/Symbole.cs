using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using REGOwintergarden.Model;

namespace REGOwintergarden.Ui;

/// <summary>
/// Gezeichnete Sinnbilder - Sonne, Regen, Wind, Frost, Markise, Fenster.
///
/// Gezeichnet und nicht als Bilddatei: eine EXE ohne Beiwerk laesst sich
/// kopieren, ein Symbol als Pfad skaliert auf jedem Bildschirm, und die Farbe
/// kann der Zustand bestimmen. Ein rotes Windsymbol sagt in einem Blick mehr
/// als die Zeile „Windalarm aktiv" - und darum geht es auf einer Seite, die
/// auch jemand verstehen soll, der die Anlage nicht gebaut hat.
///
/// Alle Pfade liegen in einem Feld von 24 auf 24. Wer einen hinzufuegt,
/// zeichnet in dasselbe Feld - sonst passen die Groessen nicht mehr zusammen.
/// </summary>
public static class Symbole
{
    // Die Striche selbst stehen im Kern, weil sie dort auch die Webseite
    // braucht - WPF und SVG verwenden dieselbe Pfadsyntax. Zwei Kopien
    // derselben Zeichnung liefen mit der Zeit auseinander, und dann saehe
    // dieselbe Anlage in zwei Fenstern verschieden aus.
    public const string Sonne = Sinnbilder.Sonne;
    public const string Wolke = Sinnbilder.Wolke;
    public const string Regen = Sinnbilder.Regen;
    public const string Wind = Sinnbilder.Wind;
    public const string Frost = Sinnbilder.Frost;
    public const string Markise = Sinnbilder.Markise;
    public const string Fenster = Sinnbilder.Fenster;
    public const string Jalousie = Sinnbilder.Jalousie;
    public const string Lamellendach = Sinnbilder.Lamellendach;
    public const string Uhr = Sinnbilder.Uhr;
    public const string Warnung = Sinnbilder.Warnung;
    public const string Haus = Sinnbilder.Haus;
    public const string Thermometer = Sinnbilder.Thermometer;

    /// <summary>Das Sinnbild einer Antriebsart.</summary>
    public static string FuerArt(Antriebsart art) => Sinnbilder.FuerArt(art);

    /// <summary>Das Sinnbild einer Stufe - warum ein Antrieb steht, wo er steht.</summary>
    public static string FuerStufe(Stufe stufe) => Sinnbilder.FuerStufe(stufe);

    /// <summary>
    /// Baut ein Symbol als Zeichnung.
    ///
    /// Bewusst nur Striche und keine Flaechen: gefuellte Symbole muessten je
    /// Farbe zweimal gezeichnet werden, und ein Strichsymbol bleibt auch klein
    /// noch erkennbar.
    /// </summary>
    public static Path Zeichnen(string pfad, Brush farbe, double groesse = 24, double staerke = 1.6)
    {
        var figur = Geometry.Parse(pfad);
        return new Path
        {
            Data = figur,
            Stroke = farbe,
            StrokeThickness = staerke * 24 / groesse,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.Uniform,
            Width = groesse,
            Height = groesse,
            SnapsToDevicePixels = true,
        };
    }

    /// <summary>
    /// Eine Anzeigeleuchte: Symbol, Beschriftung und Wert - ruhig oder rot.
    ///
    /// Das ist der Baustein der Wetterzeile. Rot heisst „das haelt gerade
    /// etwas fest", grau heisst „alles in Ordnung", und ausgegraut heisst
    /// „dazu weiss ich nichts" - drei Zustaende, weil zwei nicht reichen: ein
    /// fehlender Windwert ist etwas anderes als Windstille.
    /// </summary>
    public sealed class Leuchte : Border
    {
        private readonly TextBlock _wert = new();
        private readonly TextBlock _name = new();
        private readonly Path _bild;

        public Leuchte(string pfad, string name)
        {
            _bild = Zeichnen(pfad, (Brush)Application.Current.Resources["Nebenschrift"], 28);

            var spalte = new StackPanel { Width = 116 };
            _bild.HorizontalAlignment = HorizontalAlignment.Left;
            _bild.Margin = new Thickness(0, 0, 0, 6);
            spalte.Children.Add(_bild);

            _wert.FontSize = 19;
            _wert.Foreground = (Brush)Application.Current.Resources["Schrift"];
            spalte.Children.Add(_wert);

            _name.Text = name;
            _name.Style = (Style)Application.Current.Resources["Hinweis"];
            spalte.Children.Add(_name);

            Style = (Style)Application.Current.Resources["Gruppenkarte"];
            Margin = new Thickness(0, 0, 10, 10);
            Child = spalte;
        }

        /// <summary>
        /// Die Beschriftung unter dem Wert. Aenderbar, weil eine Leuchte je
        /// nach Betriebsart etwas anderes zeigt: „KNX-Bus", wenn dieses
        /// Programm selbst steuert, „Server + Bus" in der Fernbedienung.
        /// </summary>
        public string Beschriftung
        {
            get => _name.Text;
            set => _name.Text = value;
        }

        /// <summary>Setzt Wert und Zustand. <paramref name="alarm"/> faerbt rot.</summary>
        public void Zeigen(string wert, bool alarm, bool bekannt = true)
        {
            _wert.Text = wert;

            var farbe = !bekannt
                ? (Brush)Application.Current.Resources["Blass"]
                : alarm
                    ? (Brush)Application.Current.Resources["Fehler"]
                    : (Brush)Application.Current.Resources["Schrift"];

            _wert.Foreground = farbe;
            _bild.Stroke = !bekannt
                ? (Brush)Application.Current.Resources["Blass"]
                : alarm
                    ? (Brush)Application.Current.Resources["Fehler"]
                    : (Brush)Application.Current.Resources["Nebenschrift"];

            BorderBrush = alarm && bekannt
                ? (Brush)Application.Current.Resources["Fehler"]
                : (Brush)Application.Current.Resources["Linie"];
            BorderThickness = new Thickness(alarm && bekannt ? 2 : 1);
        }
    }
}
