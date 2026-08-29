using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using REGOwintergarden.App;

namespace REGOwintergarden.Daemon;

/// <summary>
/// Die Wintergartensteuerung ohne Fenster - für Linux und den Raspberry Pi.
///
/// Derselbe Kern wie das Windows-Programm: dieselben Regeln, derselbe
/// Sonnenstand, dasselbe Einstellungsformat. Nur die Oberfläche ist eine
/// andere - hier eine Seite im Browser statt eines Fensters.
///
/// <b>Warum ein eigenes Programm und nicht dasselbe mit Schalter:</b> WPF
/// gibt es auf Linux nicht. Alles, was kein Fenster braucht, liegt deshalb im
/// Kern; hier bleibt der Start, die Schleife und der Webserver.
/// </summary>
public static class Programm
{
    public static async Task<int> Main(string[] args)
    {
        if (Hat(args, "--hilfe") || Hat(args, "--help") || Hat(args, "-h"))
        {
            Console.WriteLine(Hilfe);
            return 0;
        }

        var ordner = Argument(args, "--home") ?? Einstellungen.StandardOrdner;
        var port = 8080;
        if (Argument(args, "--port") is { } text
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var gelesen))
        {
            port = gelesen;
        }

        // Vor allem anderen, denn hier soll nichts geladen und nichts
        // verbunden werden: das ist die Frage von Docker an einen schon
        // laufenden Dienst.
        if (Hat(args, "--gesundheit")) return await GesundheitAsync(port).ConfigureAwait(false);

        var einstellungen = Einstellungen.Laden(ordner);
        var dienst = new Wintergartendienst(einstellungen, ordner);

        // Auf einem Dienst gibt es niemanden, der ein Protokollfenster
        // aufmacht. Also auf die Standardausgabe - dort holt es sich journald
        // ab, und in Docker steht es im Containerprotokoll.
        dienst.Protokolliert += zeile => Console.WriteLine(zeile.ToString());

        dienst.Melden("Start", "REGOwintergarden " + Programmstand.Version + ", Ordner " + ordner);

        // Ein nicht beschreibbarer Ordner ist der haeufigste Stolperstein in
        // Docker: der Datentraeger vom Wirt gehoert dort einem anderen
        // Benutzer, und der Container darf nicht hinein. Gemeldet wird es
        // deutlich - abgebrochen wird deswegen nicht. Ein Wintergarten, der
        // die Markise stehen laesst, weil er sein Protokoll nicht schreiben
        // kann, waere die schlechtere Wahl.
        if (!Beschreibbar(ordner))
        {
            dienst.Melden("Ordner nicht beschreibbar",
                ordner + " - Einstellungen und Verlauf bleiben ungesichert. "
                + "Bei Docker auf dem Wirt einmal: sudo chown -R 1000:1000 daten", true);
        }

        if (Hat(args, "--pruefen"))
        {
            // Nur nachsehen, ob die Einstellungen lesbar sind und was darin
            // steht. Das ist der erste Aufruf nach der Installation, und er
            // soll nichts fahren.
            Console.WriteLine("Anlage:    " + einstellungen.Anlage.Name);
            Console.WriteLine("Antriebe:  " + einstellungen.Anlage.Motoren.Count
                                            .ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("Gateway:   " + (einstellungen.Gateway.Length > 0
                ? einstellungen.Gateway
                : "nicht eingetragen"));
            Console.WriteLine("Standort:  " + einstellungen.Anlage.Ort + ", "
                              + einstellungen.Anlage.Breite.ToString("0.####", CultureInfo.InvariantCulture)
                              + " / "
                              + einstellungen.Anlage.Laenge.ToString("0.####", CultureInfo.InvariantCulture));
            Console.WriteLine("Sonne:     " + dienst.Sonnenstand(DateTime.Now));
            await dienst.DisposeAsync().ConfigureAwait(false);
            return 0;
        }

        using var web = new Webserver(dienst, port, dienst.Melden);
        web.Starten();

        if (einstellungen.Gateway.Length > 0)
        {
            await dienst.VerbindenAsync(einstellungen.Gateway).ConfigureAwait(false);
        }
        else
        {
            dienst.Melden("kein Gateway", "unter " + ordner + "/einstellungen.json eintragen", true);
        }

        dienst.Starten();

        // Auf das Ende warten. systemd schickt SIGTERM, Docker auch - beides
        // kommt hier als ProcessExit beziehungsweise als Strg+C an.
        var ende = new TaskCompletionSource();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => ende.TrySetResult();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            ende.TrySetResult();
        };
        await ende.Task.ConfigureAwait(false);

        dienst.Melden("Ende", "wird beendet");
        await dienst.DisposeAsync().ConfigureAwait(false);
        return 0;
    }

    private const string Hilfe =
        "REGOwintergarden - Wintergartensteuerung ueber KNX\n\n"
        + "  regowintergarden                 startet Steuerung und Weboberflaeche\n"
        + "  regowintergarden --port 8080     andere Portnummer\n"
        + "  regowintergarden --home <Ordner> anderer Einstellungsordner\n"
        + "  regowintergarden --pruefen       liest die Einstellungen und beendet sich\n"
        + "  regowintergarden --gesundheit    fragt einen laufenden Dienst, 0 wenn er lebt\n\n"
        + "Der Ordner laesst sich auch ueber REGOWINTERGARDEN_HOME vorgeben.\n"
        + "Eingerichtet wird in einstellungen.json - dieselbe Datei wie unter Windows.";

    /// <summary>
    /// Fragt den laufenden Dienst und meldet über den Rückgabewert, ob er
    /// lebt. Genau das will Docker wissen - und zwar vom laufenden Dienst und
    /// nicht von den Einstellungen auf der Platte.
    ///
    /// <b>Warum nicht curl:</b> in den Laufzeitbildern gibt es keines. Eines
    /// dazuzunehmen hieße, sich für eine einzige Zeile eine Paketquelle samt
    /// Aktualisierungen ans Bein zu binden - das Programm kann selbst fragen.
    /// </summary>
    private static async Task<int> GesundheitAsync(int port)
    {
        var adresse = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture) + "/gesundheit";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            var antwort = await http.GetAsync(adresse).ConfigureAwait(false);
            var text = (await antwort.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim();
            Console.WriteLine(((int)antwort.StatusCode).ToString(CultureInfo.InvariantCulture) + " " + text);
            return antwort.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine("keine Antwort von " + adresse + ": " + ex.Message);
            return 1;
        }
    }

    /// <summary>Lässt sich in den Ordner schreiben? Gefragt wird, indem man es tut.</summary>
    private static bool Beschreibbar(string ordner)
    {
        try
        {
            Directory.CreateDirectory(ordner);
            var probe = Path.Combine(ordner, ".schreibprobe");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool Hat(string[] args, string name)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string? Argument(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }
        return null;
    }
}
