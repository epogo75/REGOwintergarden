using System;
using System.Windows;

namespace REGOwintergarden;

/// <summary>
/// Der Einstieg. Dasselbe Programm laeuft in zwei Rollen - mit Oberflaeche
/// und als Windows-Dienst -, und die Weiche muss fallen, bevor WPF anlaeuft.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        foreach (var arg in args)
        {
            switch (arg.ToLowerInvariant())
            {
                case "--dienst":
                case "--service":
                    return Service.Dienstlauf.Starten();
                case "--einrichten":
                case "--install":
                    return Service.Dienstlauf.Einrichten();
                case "--entfernen":
                case "--uninstall":
                    return Service.Dienstlauf.Entfernen();
                case "--hilfe":
                case "--help":
                case "-h":
                    Hilfe();
                    return 0;
            }
        }

        var anwendung = new WintergartenApplication();
        anwendung.InitializeComponent();
        return anwendung.Run();
    }

    private static void Hilfe()
    {
        MessageBox.Show(
            "REGOwintergarden\n\n"
            + "ohne Schalter    Oberflaeche\n"
            + "--dienst         laeuft als Windows-Dienst\n"
            + "--einrichten     richtet den Dienst ein (als Administrator)\n"
            + "--entfernen      entfernt den Dienst wieder\n\n"
            + "Der Ordner fuer Einstellungen und Protokoll laesst sich ueber die\n"
            + "Umgebungsvariable REGOWINTERGARDEN_HOME vorgeben.",
            "REGOwintergarden", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
