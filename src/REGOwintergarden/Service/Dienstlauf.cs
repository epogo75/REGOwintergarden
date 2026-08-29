using System;
using System.Threading;
using System.Windows;
using REGOwintergarden.App;

namespace REGOwintergarden.Service;

/// <summary>
/// Das Programm als Windows-Dienst.
///
/// <b>Warum ueberhaupt:</b> ein Wintergarten wartet nicht darauf, dass jemand
/// ein Fenster offen hat. Beschattung, Wind- und Regenschutz muessen laufen,
/// wenn niemand angemeldet ist - sonst ist der Windschutz genau dann weg,
/// wenn er gebraucht wird, naemlich nachts und im Urlaub.
///
/// Die Oberflaeche kann dieselbe Automatik mitlaufen lassen. Beides
/// gleichzeitig waere doppelt gefahren: laeuft der Dienst, haelt sich das
/// Fenster zurueck und zeigt nur an.
/// </summary>
public static class Dienstlauf
{
    /// <summary>Laeuft als Dienst, bis Windows ihn anhaelt.</summary>
    public static int Starten()
    {
        return WindowsService.Run(Arbeiten) ? 0 : 1;
    }

    private static void Arbeiten(CancellationToken ct)
    {
        var ordner = Einstellungen.StandardOrdner;
        var einstellungen = Einstellungen.Laden(ordner);
        var dienst = new Wintergartendienst(einstellungen, ordner);

        dienst.Melden("Dienst", "gestartet, Ordner " + ordner);

        try
        {
            if (einstellungen.Gateway.Length > 0)
            {
                dienst.VerbindenAsync(einstellungen.Gateway).GetAwaiter().GetResult();
            }
            else
            {
                dienst.Melden("Dienst", "kein Gateway eingetragen - es wird nur gerechnet", true);
            }

            dienst.Starten();

            // Warten, bis Windows anhaelt. Die Arbeit macht der Takt im
            // Dienst selbst; hier steht nur der Faden still.
            ct.WaitHandle.WaitOne();
        }
        finally
        {
            dienst.Melden("Dienst", "wird beendet");
            dienst.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>Richtet den Dienst ein. Braucht Administratorrechte.</summary>
    public static int Einrichten()
    {
        var pfad = Environment.ProcessPath ?? "";
        var meldung = WindowsService.Install(pfad, Einstellungen.StandardOrdner);
        MessageBox.Show(meldung, "REGOwintergarden", MessageBoxButton.OK,
            meldung.StartsWith("Fehler", StringComparison.OrdinalIgnoreCase)
                ? MessageBoxImage.Warning
                : MessageBoxImage.Information);
        return 0;
    }

    public static int Entfernen()
    {
        var meldung = WindowsService.Uninstall();
        MessageBox.Show(meldung, "REGOwintergarden", MessageBoxButton.OK, MessageBoxImage.Information);
        return 0;
    }

    /// <summary>Ob der Dienst eingerichtet ist - fuer die Anzeige.</summary>
    public static bool Eingerichtet()
    {
        try { return WindowsService.IsInstalled(); }
        catch (Exception) { return false; }
    }
}
