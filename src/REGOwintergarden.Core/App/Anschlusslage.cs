using System;

namespace REGOwintergarden.App;

/// <summary>
/// Der Anschluss in einem Wort: steht der Bus, und - in der Fernbedienung -
/// ist der fuehrende Rechner da.
///
/// <b>Warum das eine eigene Anzeige verdient:</b> alle anderen Leuchten zeigen
/// Messwerte. Fehlt der Bus, sind die Messwerte nicht falsch, sondern gar
/// nicht da - und eine Anlage, die stillsteht, weil das Gateway aus ist, sieht
/// auf der Uebersicht sonst genauso ruhig aus wie eine, bei der alles stimmt.
/// Genau diese Verwechslung soll die Kachel unmoeglich machen.
///
/// <b>Warum im Kern:</b> beide Oberflaechen zeigen sie, und zwei Bewertungen
/// derselben Frage wuerden sich frueher oder spaeter widersprechen. Dann
/// stuende im Fenster „verbunden" und im Browser „kein Bus", und niemand
/// wuesste, welchem der beiden zu glauben ist.
/// </summary>
public readonly record struct Anschlussbild(string Name, string Wert, bool Alarm, bool Bekannt,
    string Erklaerung)
{
    /// <summary>Bewertet den Anschluss - fuer die Kachel in der Wetterzeile.</summary>
    public static Anschlussbild Bilden(Wintergartendienst dienst)
    {
        // ---- Fernbedienung: zwei Fragen auf einmal ----
        //
        // Der Server kann antworten und trotzdem keinen Bus haben. Das ist
        // kein Randfall, sondern der haeufigste: der Pi laeuft, das Gateway
        // ist aus. Beides in einer Kachel, und zwar mit dem Schlimmeren
        // zuerst.
        if (dienst.IstFern)
        {
            if (dienst.Stand != Busstand.Verbunden)
            {
                return new Anschlussbild("Server", "kein Server", true, true,
                    "Der fuehrende Rechner antwortet nicht. Die Anzeige steht auf dem letzten "
                    + "bekannten Stand, und von hier aus laesst sich nichts fahren.");
            }
            if (!dienst.FernBus)
            {
                return new Anschlussbild("Server + Bus", "Server ok, kein Bus", true, true,
                    "Der fuehrende Rechner antwortet, hat aber selbst keine Busverbindung. Er "
                    + "rechnet weiter, faehrt aber nichts - meist ist das Gateway aus.");
            }
            return new Anschlussbild("Server + Bus", "beides da", false, true,
                "Der fuehrende Rechner antwortet und haengt am Bus. Befehle von hier gehen ueber "
                + "ihn hinaus.");
        }

        // ---- Selbst steuern: nur der Bus ----
        return dienst.Stand switch
        {
            Busstand.Verbunden => new Anschlussbild("KNX-Bus", "verbunden", false, true,
                "Telegramme gehen hinaus und werden mitgehoert."),
            Busstand.Verbinde => new Anschlussbild("KNX-Bus", "verbinde…", false, false,
                "Die Verbindung wird gerade aufgebaut."),
            Busstand.Fehler => new Anschlussbild("KNX-Bus", "Fehler", true, true,
                "Die Verbindung zum Gateway ist fehlgeschlagen - siehe Protokoll."),
            _ => new Anschlussbild("KNX-Bus", "getrennt", true, true,
                "Ohne Bus rechnet die Automatik zwar, faehrt aber nichts. Das Gateway steht "
                + "unter Konfiguration → Anschluss."),
        };
    }

    /// <summary>Wie lange der letzte Rechendurchgang her ist - fuer den Kurzhinweis.</summary>
    public static string Taktalter(Wintergartendienst dienst, DateTime jetzt) =>
        dienst.LetzterTakt is { } takt
            ? "letzter Rechendurchgang vor "
              + Math.Max(0, (int)(jetzt - takt).TotalSeconds).ToString(
                  System.Globalization.CultureInfo.CurrentCulture) + " s"
            : "noch kein Rechendurchgang";
}
