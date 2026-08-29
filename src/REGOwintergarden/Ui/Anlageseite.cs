using System;
using System.Windows;
using System.Windows.Controls;
using REGOwintergarden.App;
using REGOwintergarden.Model;
using REGOwintergarden.Service;

namespace REGOwintergarden.Ui;

/// <summary>
/// Die Anlage einrichten: Standort, Wetterstation, Grenzen, Schalter, Dienst.
///
/// Alles, was fuer die ganze Anlage gilt - was je Antrieb gilt, steht im
/// Reiter daneben. Die Trennung ist die eines Wintergartens und nicht die
/// einer Datenbank: „ab wieviel Lux wird beschattet" ist eine Frage an das
/// Haus, „wohin faehrt die Markise Sued dabei" eine an den Antrieb.
/// </summary>
public sealed class Anlageseite : UserControl
{
    private readonly Wintergartendienst _dienst;
    private readonly Window _besitzer;

    private readonly TextBox _name = new();
    private readonly TextBox _ort = new();
    private readonly TextBox _breite = new();
    private readonly TextBox _laenge = new();
    private readonly TextBlock _sonnenprobe = new() { TextWrapping = TextWrapping.Wrap };

    private readonly TextBox _adrRegen = new();
    private readonly TextBox _adrWind = new();
    private readonly TextBox _adrAussen = new();
    private readonly TextBox _adrInnen = new();
    private readonly TextBox _adrOst = new();
    private readonly TextBox _adrSued = new();
    private readonly TextBox _adrWest = new();
    private readonly TextBox _adrAzimut = new();
    private readonly TextBox _adrElevation = new();

    private readonly CheckBox _beschattung = new() { Content = "Beschattung" };
    private readonly CheckBox _lueftung = new() { Content = "Lueftung" };
    private readonly CheckBox _wind = new() { Content = "Windschutz" };
    private readonly CheckBox _regen = new() { Content = "Regenschutz" };
    private readonly CheckBox _frost = new() { Content = "Frostschutz" };
    private readonly CheckBox _uhr = new() { Content = "Zeitschaltuhr" };
    private readonly CheckBox _vorhersage = new() { Content = "Vorhersage aus dem Netz holen" };

    private readonly TextBox _schwelle = new();
    private readonly TextBox _ein = new();
    private readonly TextBox _aus = new();
    private readonly TextBox _innenWarm = new();
    private readonly TextBox _warmFaktor = new();
    private readonly TextBox _lueftenAb = new();
    private readonly TextBox _lueftenHyst = new();
    private readonly TextBox _lueftenDelta = new();
    private readonly TextBox _lueftenPos = new();
    private readonly TextBox _windNachlauf = new();
    private readonly TextBox _regenNachlauf = new();
    private readonly TextBox _handsperre = new();
    private readonly TextBox _alterWind = new();
    private readonly TextBox _takt = new();

    private readonly TextBlock _dienststand = new() { TextWrapping = TextWrapping.Wrap };

    private bool _fuellt;

    public Anlageseite(Wintergartendienst dienst, Window besitzer)
    {
        _dienst = dienst;
        _besitzer = besitzer;

        Content = Aufbau();
        Auffrischen();
    }

    public event Action? Gespeichert;

    private UIElement Aufbau()
    {
        var aussen = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(12),
        };
        var spalte = new StackPanel { MaxWidth = 780, HorizontalAlignment = HorizontalAlignment.Left };

        // ---- Standort ----
        spalte.Children.Add(Bausteine.Ueberschrift("Anlage", 0));
        spalte.Children.Add(Bausteine.Zeile("Name", Bausteine.Feld(_name)));
        spalte.Children.Add(Bausteine.Zeile("Ort", Bausteine.Feld(_ort, 220)));
        spalte.Children.Add(Bausteine.Zeile("Breite", Bausteine.Feld(_breite, 120)));
        spalte.Children.Add(Bausteine.Zeile("Laenge", Bausteine.Feld(_laenge, 120)));
        spalte.Children.Add(Bausteine.Hinweis(
            "Breite noerdlich positiv, Laenge oestlich positiv - etwa 48,70 und 8,14 fuer Buehl. "
            + "Daraus rechnet das Programm Sonnenstand, Auf- und Untergang. Meldet die Wetterstation "
            + "Azimut und Elevation, gelten deren Werte; Auf- und Untergang kommen in jedem Fall aus "
            + "dieser Rechnung, denn die meldet keine Station."));

        _sonnenprobe.Style = (Style)Application.Current.Resources["Hinweis"];
        _sonnenprobe.Margin = new Thickness(Bausteine.Beschriftungsbreite, 0, 0, 8);
        spalte.Children.Add(_sonnenprobe);

        // ---- Wetterstation ----
        spalte.Children.Add(Bausteine.Ueberschrift("Wetterstation"));
        spalte.Children.Add(Bausteine.Zeile("Regen", Adressfeld(_adrRegen, "1.001")));
        spalte.Children.Add(Bausteine.Zeile("Wind", Adressfeld(_adrWind, "9.005")));
        spalte.Children.Add(Bausteine.Zeile("Aussen", Adressfeld(_adrAussen, "9.001")));
        spalte.Children.Add(Bausteine.Zeile("Innen", Adressfeld(_adrInnen, "9.001")));
        spalte.Children.Add(Bausteine.Zeile("Hell Ost", Adressfeld(_adrOst, "9.004")));
        spalte.Children.Add(Bausteine.Zeile("Hell Sued", Adressfeld(_adrSued, "9.004")));
        spalte.Children.Add(Bausteine.Zeile("Hell West", Adressfeld(_adrWest, "9.004")));
        spalte.Children.Add(Bausteine.Zeile("Azimut", Adressfeld(_adrAzimut, "14.007")));
        spalte.Children.Add(Bausteine.Zeile("Elevation", Adressfeld(_adrElevation, "14.007")));
        spalte.Children.Add(Bausteine.Hinweis(
            "Azimut und Elevation sind freiwillig - ohne sie rechnet das Programm sie selbst."));

        // ---- Schalter ----
        spalte.Children.Add(Bausteine.Ueberschrift("Was laufen soll"));
        var haken = new WrapPanel { Margin = new Thickness(Bausteine.Beschriftungsbreite, 0, 0, 8) };
        foreach (var kasten in new[] { _beschattung, _lueftung, _wind, _regen, _frost, _uhr })
        {
            kasten.Margin = new Thickness(0, 0, 16, 6);
            haken.Children.Add(kasten);
        }
        spalte.Children.Add(haken);
        _vorhersage.Margin = new Thickness(Bausteine.Beschriftungsbreite, 0, 0, 8);
        spalte.Children.Add(_vorhersage);
        spalte.Children.Add(Bausteine.Hinweis(
            "Die Vorhersage kommt von Open-Meteo, ohne Anmeldung und ohne Schluessel. Sie ersetzt die "
            + "Wetterstation nicht - sie warnt vor: eine Markise, die ausfaehrt, obwohl in einer Stunde "
            + "Boeen angesagt sind, faehrt zweimal umsonst und einmal zu spaet."));

        // ---- Grenzen ----
        spalte.Children.Add(Bausteine.Ueberschrift("Beschattung"));
        spalte.Children.Add(Bausteine.Zeile("ab Helligkeit", Bausteine.Feld(_schwelle, 120)));
        spalte.Children.Add(Bausteine.Zeile("Verzoegerung ein", Bausteine.Feld(_ein, 120)));
        spalte.Children.Add(Bausteine.Zeile("Verzoegerung aus", Bausteine.Feld(_aus, 120)));
        spalte.Children.Add(Bausteine.Hinweis(
            "In Lux und Minuten. Das Ausschalten dauert laenger als das Einschalten, und das mit "
            + "Absicht: eine einzelne Wolke soll die Markise nicht ein- und wieder ausfahren. Jede "
            + "Fahrt kostet Mechanik, und nichts stoert im Wintergarten mehr als ein Behang, der alle "
            + "drei Minuten wandert."));
        spalte.Children.Add(Bausteine.Zeile("drinnen warm ab", Bausteine.Feld(_innenWarm, 120)));
        spalte.Children.Add(Bausteine.Zeile("Faktor dann", Bausteine.Feld(_warmFaktor, 120)));
        spalte.Children.Add(Bausteine.Hinweis(
            "Ist es drinnen bereits warm, sinkt die Helligkeitsschwelle auf diesen Faktor - 0,7 heisst "
            + "dreissig Prozent frueher beschatten."));

        spalte.Children.Add(Bausteine.Ueberschrift("Lueften"));
        spalte.Children.Add(Bausteine.Zeile("ab drinnen", Bausteine.Feld(_lueftenAb, 120)));
        spalte.Children.Add(Bausteine.Zeile("Hysterese", Bausteine.Feld(_lueftenHyst, 120)));
        spalte.Children.Add(Bausteine.Zeile("draussen kuehler", Bausteine.Feld(_lueftenDelta, 120)));
        spalte.Children.Add(Bausteine.Zeile("Fenster auf", Bausteine.Feld(_lueftenPos, 120)));
        spalte.Children.Add(Bausteine.Hinweis(
            "Gelueftet wird nur, wenn es draussen wirklich kuehler ist - sonst holt das offene Fenster "
            + "die Waerme herein, statt sie hinauszulassen."));

        spalte.Children.Add(Bausteine.Ueberschrift("Schutz und Zeiten"));
        spalte.Children.Add(Bausteine.Zeile("Wind Nachlauf", Bausteine.Feld(_windNachlauf, 120)));
        spalte.Children.Add(Bausteine.Zeile("Regen Nachlauf", Bausteine.Feld(_regenNachlauf, 120)));
        spalte.Children.Add(Bausteine.Zeile("Handsperre", Bausteine.Feld(_handsperre, 120)));
        spalte.Children.Add(Bausteine.Zeile("Wind hoechstens", Bausteine.Feld(_alterWind, 120)));
        spalte.Children.Add(Bausteine.Zeile("Takt", Bausteine.Feld(_takt, 120)));
        spalte.Children.Add(Bausteine.Hinweis(
            "Alles in Minuten, der Takt in Sekunden. „Wind hoechstens\" ist das Alter, bis zu dem ein "
            + "Windwert gilt: kommt laenger nichts, faehrt die Anlage in Sicherheit. Ein stiller "
            + "Windmesser ist keine Windstille."));

        var uebernehmen = Bausteine.Knopf("Uebernehmen", Uebernehmen, stark: true);
        uebernehmen.HorizontalAlignment = HorizontalAlignment.Left;
        uebernehmen.Margin = new Thickness(Bausteine.Beschriftungsbreite, 4, 0, 0);
        spalte.Children.Add(uebernehmen);

        // ---- Dienst ----
        spalte.Children.Add(Bausteine.Ueberschrift("Windows-Dienst"));
        _dienststand.Style = (Style)Application.Current.Resources["Hinweis"];
        _dienststand.Margin = new Thickness(Bausteine.Beschriftungsbreite, 0, 0, 8);
        spalte.Children.Add(_dienststand);
        spalte.Children.Add(Bausteine.Hinweis(
            "Ein Wintergarten wartet nicht darauf, dass jemand ein Fenster offen hat. Als Dienst laeuft "
            + "die Automatik weiter, wenn niemand angemeldet ist - sonst fehlt der Windschutz genau "
            + "dann, wenn er gebraucht wird: nachts und im Urlaub. Einrichten und Entfernen brauchen "
            + "Administratorrechte."));

        var dienstknoepfe = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(Bausteine.Beschriftungsbreite, 0, 0, 0),
        };
        dienstknoepfe.Children.Add(Bausteine.Knopf("Dienst einrichten", () =>
        {
            Dienstlauf.Einrichten();
            Auffrischen();
        }));
        dienstknoepfe.Children.Add(Bausteine.Knopf("Dienst entfernen", () =>
        {
            Dienstlauf.Entfernen();
            Auffrischen();
        }));
        spalte.Children.Add(dienstknoepfe);

        aussen.Content = spalte;
        return aussen;
    }

    private TextBox Adressfeld(TextBox feld, string dpt)
    {
        feld.Style = (Style)Application.Current.Resources["Adressfeld"];
        AddressSuggest.Attach(feld, _dienst, () => dpt);
        return feld;
    }

    public void Auffrischen()
    {
        _fuellt = true;
        try
        {
            var anlage = _dienst.Anlage;
            _name.Text = anlage.Name;
            _ort.Text = anlage.Ort;
            _breite.Text = Bausteine.Zahl(anlage.Breite);
            _laenge.Text = Bausteine.Zahl(anlage.Laenge);

            _adrRegen.Text = anlage.AdresseRegen;
            _adrWind.Text = anlage.AdresseWind;
            _adrAussen.Text = anlage.AdresseAussen;
            _adrInnen.Text = anlage.AdresseInnen;
            _adrOst.Text = anlage.AdresseHellOst;
            _adrSued.Text = anlage.AdresseHellSued;
            _adrWest.Text = anlage.AdresseHellWest;
            _adrAzimut.Text = anlage.AdresseAzimut;
            _adrElevation.Text = anlage.AdresseElevation;

            _beschattung.IsChecked = anlage.BeschattungAktiv;
            _lueftung.IsChecked = anlage.LueftungAktiv;
            _wind.IsChecked = anlage.WindschutzAktiv;
            _regen.IsChecked = anlage.RegenschutzAktiv;
            _frost.IsChecked = anlage.FrostschutzAktiv;
            _uhr.IsChecked = anlage.ZeitschaltuhrAktiv;
            _vorhersage.IsChecked = _dienst.Einstellungen.VorhersageHolen;

            _schwelle.Text = Bausteine.Zahl(anlage.Helligkeitsschwelle);
            _ein.Text = Bausteine.Zahl(anlage.EinschaltverzoegerungMinuten);
            _aus.Text = Bausteine.Zahl(anlage.AusschaltverzoegerungMinuten);
            _innenWarm.Text = Bausteine.Zahl(anlage.InnenWarm);
            _warmFaktor.Text = Bausteine.Zahl(anlage.WarmFaktor);
            _lueftenAb.Text = Bausteine.Zahl(anlage.LueftungAb);
            _lueftenHyst.Text = Bausteine.Zahl(anlage.LueftungHysterese);
            _lueftenDelta.Text = Bausteine.Zahl(anlage.LueftungUnterschied);
            _lueftenPos.Text = Bausteine.Zahl(anlage.Lueftungsposition);
            _windNachlauf.Text = Bausteine.Zahl(anlage.WindNachlaufMinuten);
            _regenNachlauf.Text = Bausteine.Zahl(anlage.RegenNachlaufMinuten);
            _handsperre.Text = Bausteine.Zahl(anlage.HandsperreMinuten);
            _alterWind.Text = Bausteine.Zahl(anlage.HoechstalterWindMinuten);
            _takt.Text = Bausteine.Zahl(anlage.TaktSekunden);

            Sonnenprobe();
            _dienststand.Text = Dienstlauf.Eingerichtet()
                ? "Der Dienst ist eingerichtet."
                : "Der Dienst ist nicht eingerichtet - die Automatik laeuft nur, solange dieses "
                  + "Fenster offen ist.";
        }
        finally
        {
            _fuellt = false;
        }
    }

    private void Sonnenprobe()
    {
        if (!Bausteine.TryZahl(_breite.Text, out var breite) || !Bausteine.TryZahl(_laenge.Text, out var laenge))
        {
            _sonnenprobe.Text = "";
            return;
        }
        var jetzt = DateTime.Now;
        var stand = Astro.Berechnen(jetzt, breite, laenge);
        _sonnenprobe.Text = "Jetzt: " + stand
                            + (stand.Aufgang is { } auf && stand.Untergang is { } unter
                                ? "  ·  auf " + auf.ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture)
                                  + ", unter " + unter.ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture)
                                : "");
    }

    private void Uebernehmen()
    {
        if (_fuellt) return;
        var anlage = _dienst.Anlage;

        anlage.Name = _name.Text.Trim().Length > 0 ? _name.Text.Trim() : anlage.Name;
        anlage.Ort = _ort.Text.Trim();
        Bausteine.Setze(_breite, wert => anlage.Breite = Math.Clamp(wert, -90, 90));
        Bausteine.Setze(_laenge, wert => anlage.Laenge = Math.Clamp(wert, -180, 180));

        anlage.AdresseRegen = _adrRegen.Text.Trim();
        anlage.AdresseWind = _adrWind.Text.Trim();
        anlage.AdresseAussen = _adrAussen.Text.Trim();
        anlage.AdresseInnen = _adrInnen.Text.Trim();
        anlage.AdresseHellOst = _adrOst.Text.Trim();
        anlage.AdresseHellSued = _adrSued.Text.Trim();
        anlage.AdresseHellWest = _adrWest.Text.Trim();
        anlage.AdresseAzimut = _adrAzimut.Text.Trim();
        anlage.AdresseElevation = _adrElevation.Text.Trim();

        anlage.BeschattungAktiv = _beschattung.IsChecked == true;
        anlage.LueftungAktiv = _lueftung.IsChecked == true;
        anlage.WindschutzAktiv = _wind.IsChecked == true;
        anlage.RegenschutzAktiv = _regen.IsChecked == true;
        anlage.FrostschutzAktiv = _frost.IsChecked == true;
        anlage.ZeitschaltuhrAktiv = _uhr.IsChecked == true;
        _dienst.Einstellungen.VorhersageHolen = _vorhersage.IsChecked == true;

        Bausteine.Setze(_schwelle, wert => anlage.Helligkeitsschwelle = Math.Clamp(wert, 0, 150000));
        Bausteine.Setze(_ein, wert => anlage.EinschaltverzoegerungMinuten = Math.Clamp(wert, 0, 120));
        Bausteine.Setze(_aus, wert => anlage.AusschaltverzoegerungMinuten = Math.Clamp(wert, 0, 240));
        Bausteine.Setze(_innenWarm, wert => anlage.InnenWarm = Math.Clamp(wert, 0, 60));
        Bausteine.Setze(_warmFaktor, wert => anlage.WarmFaktor = Math.Clamp(wert, 0.1, 1));
        Bausteine.Setze(_lueftenAb, wert => anlage.LueftungAb = Math.Clamp(wert, 0, 60));
        Bausteine.Setze(_lueftenHyst, wert => anlage.LueftungHysterese = Math.Clamp(wert, 0, 20));
        Bausteine.Setze(_lueftenDelta, wert => anlage.LueftungUnterschied = Math.Clamp(wert, 0, 20));
        Bausteine.Setze(_lueftenPos, wert => anlage.Lueftungsposition = Math.Clamp(wert, 0, 100));
        Bausteine.Setze(_windNachlauf, wert => anlage.WindNachlaufMinuten = Math.Clamp(wert, 0, 240));
        Bausteine.Setze(_regenNachlauf, wert => anlage.RegenNachlaufMinuten = Math.Clamp(wert, 0, 240));
        Bausteine.Setze(_handsperre, wert => anlage.HandsperreMinuten = Math.Clamp(wert, 0, 1440));
        Bausteine.Setze(_alterWind, wert => anlage.HoechstalterWindMinuten = Math.Clamp(wert, 1, 240));
        Bausteine.Setze(_takt, wert => anlage.TaktSekunden = Math.Clamp(wert, 5, 300));

        Gespeichert?.Invoke();
        Auffrischen();
        _dienst.Melden("Einstellungen", "uebernommen");
    }
}
