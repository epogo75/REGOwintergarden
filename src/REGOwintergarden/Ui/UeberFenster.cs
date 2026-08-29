using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using REGOwintergarden.App;

namespace REGOwintergarden.Ui;

/// <summary>
/// Das Ueber-Feld: Fassung, Build und die beiden Angaben, nach denen bei
/// einer Rueckfrage als Erstes gesucht wird - wo die Einstellungen liegen und
/// worauf das Programm laeuft.
/// </summary>
public sealed class UeberFenster : Window
{
    public UeberFenster()
    {
        Title = "Ueber " + Programmstand.Name;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.Resources["Flaeche"];

        var spalte = new StackPanel { Margin = new Thickness(16) };

        spalte.Children.Add(new TextBlock
        {
            Text = Programmstand.Name,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["Schrift"],
        });
        spalte.Children.Add(new TextBlock
        {
            Text = "Fassung " + Programmstand.Version,
            FontSize = 14,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = (Brush)Application.Current.Resources["Betont"],
        });

        var gebaut = Programmstand.Baudatum;
        spalte.Children.Add(Klein(
            gebaut.Length == 0
                ? "Build " + Programmstand.Build
                : "Build " + Programmstand.Build + "  ·  gebaut am " + gebaut, 4));

        spalte.Children.Add(new TextBlock
        {
            Text = Programmstand.Urheber,
            FontSize = 13,
            Margin = new Thickness(0, 12, 0, 0),
            Foreground = (Brush)Application.Current.Resources["Schrift"],
        });
        spalte.Children.Add(Klein(
            "Wintergartensteuerung ueber KNX: Beschattung nach Sonnenstand, Lueftung nach "
            + "Innentemperatur, Wind-, Regen- und Frostschutz, Zeitschaltuhr mit Bezug auf Sonnenauf- "
            + "und -untergang.", 12));

        spalte.Children.Add(Klein("Einstellungen und Protokoll", 16));
        spalte.Children.Add(Pfad(Einstellungen.StandardOrdner));
        spalte.Children.Add(Klein(
            "Ueber die Umgebungsvariable REGOWINTERGARDEN_HOME laesst sich ein anderer Ordner "
            + "vorgeben - der Dienst braucht das, weil er unter einem anderen Konto laeuft.", 4));

        spalte.Children.Add(Klein("Laufzeit", 16));
        spalte.Children.Add(Pfad(Programmstand.Laufzeit));

        var ok = new Button
        {
            Content = "Schliessen",
            Style = (Style)Application.Current.Resources["Knopf"],
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            IsDefault = true,
            IsCancel = true,
        };
        ok.Click += (_, _) => Close();
        spalte.Children.Add(ok);

        Content = spalte;
    }

    private static TextBlock Klein(string text, double oben) => new()
    {
        Text = text,
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, oben, 0, 0),
        Foreground = (Brush)Application.Current.Resources["Nebenschrift"],
    };

    private static TextBox Pfad(string text) => new()
    {
        // Als Feld und nicht als Beschriftung: einen Pfad will man kopieren.
        Text = text,
        IsReadOnly = true,
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 11,
        Margin = new Thickness(0, 4, 0, 0),
        TextWrapping = TextWrapping.Wrap,
    };
}
