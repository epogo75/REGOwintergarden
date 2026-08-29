using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using REGOwintergarden.App;
using REGOwintergarden.Knx;
using REGOwintergarden.Model;

namespace REGOwintergarden.Ui;

/// <summary>
/// Macht aus einem Adressfeld ein suchbares Feld: waehrend des Tippens
/// erscheinen die passenden Gruppenadressen aus dem geladenen KNX-Projekt.
///
/// Uebernommen aus REGOsimulator und dort nach dem Waehler in REGOdeploy
/// gebaut, mit denselben zwei Grundsaetzen: das Feld bleibt <b>frei
/// eintippbar</b> - der Vorschlag ist ein Angebot, keine Pflicht -, und ohne
/// geladenes Projekt verhaelt es sich wie ein gewoehnliches Textfeld.
///
/// Der Unterschied zum Simulator: dort steht der erwartete Datenpunkttyp als
/// Aufzaehlung fest, hier kommt er als Zeichenkette aus REGOdeploy. Deshalb
/// liest der Typfilter ihn ueber eine Funktion nach, statt ihn zu kennen.
/// </summary>
public static class AddressSuggest
{
    /// <summary>
    /// Mehr als zwanzig Vorschlaege liest niemand. Wer sie nicht darunter
    /// findet, tippt einen Buchstaben mehr - das ist schneller als scrollen.
    /// </summary>
    private const int MaxVorschlaege = 20;

    /// <summary>
    /// Haengt die Vorschlagsliste an ein Textfeld.
    /// </summary>
    /// <param name="box">Das Adressfeld.</param>
    /// <param name="service">Haelt den Adresspool.</param>
    /// <param name="dpt">
    /// Der erwartete Datenpunkttyp, etwa <c>1.001</c>. Wird beim Tippen neu
    /// gelesen, denn er steht in einem Nachbarfeld und kann sich aendern,
    /// waehrend das Fenster offen ist.
    /// </param>
    /// <param name="uebernommen">Wird gerufen, wenn ein Vorschlag gewaehlt wurde.</param>
    public static void Attach(TextBox box, Wintergartendienst service, Func<string> dpt,
        Action? uebernommen = null)
    {
        var treffer = new ObservableCollection<GroupAddressEntry>();

        var kopf = new TextBlock
        {
            FontSize = 11,
            Margin = new Thickness(8, 6, 8, 2),
            Foreground = (Brush)Application.Current.Resources["Nebenschrift"],
        };

        var nurTyp = new CheckBox
        {
            Content = "Nur passender Typ",
            IsChecked = true,
            FontSize = 11,
            Margin = new Thickness(8, 2, 8, 6),
        };

        var liste = new ListBox
        {
            ItemsSource = treffer,
            MaxHeight = 260,
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            ItemTemplate = Vorlage(),
        };

        var inhalt = new StackPanel();
        inhalt.Children.Add(kopf);
        inhalt.Children.Add(nurTyp);
        inhalt.Children.Add(liste);

        var popup = new Popup
        {
            PlacementTarget = box,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = new Border
            {
                Background = (Brush)Application.Current.Resources["Flaeche"],
                BorderBrush = (Brush)Application.Current.Resources["Linie"],
                BorderThickness = new Thickness(1),
                MinWidth = 420,
                Child = inhalt,
            },
        };

        void Fuellen()
        {
            treffer.Clear();
            if (service.Adresspool.Count == 0) return;

            var erwartet = ValueCodec.MainNumber(dpt());
            nurTyp.Content = erwartet == 0
                ? "Nur passender Typ"
                : "Nur passender Typ (" + erwartet.ToString(CultureInfo.InvariantCulture) + ")";
            nurTyp.IsEnabled = erwartet != 0;

            var suche = box.Text.Trim().ToLowerInvariant();
            var gefiltert = new List<GroupAddressEntry>();
            var ohneTyp = new List<GroupAddressEntry>();

            foreach (var eintrag in service.Adresspool)
            {
                if (suche.Length > 0 && !eintrag.Suchtext.Contains(suche, StringComparison.Ordinal)) continue;
                ohneTyp.Add(eintrag);
                if (Passt(eintrag, erwartet)) gefiltert.Add(eintrag);
            }

            // Der Typfilter darf nie in eine leere Liste fuehren. Viele
            // Projekte tragen den Datenpunkttyp gar nicht ein; dann waere ein
            // stiller Filter das Ende der Suche.
            var filtern = nurTyp.IsChecked == true && erwartet != 0 && gefiltert.Count > 0;
            var quelle = filtern ? gefiltert : ohneTyp;
            var ausgewichen = nurTyp.IsChecked == true && erwartet != 0
                              && gefiltert.Count == 0 && ohneTyp.Count > 0;

            foreach (var eintrag in quelle)
            {
                if (treffer.Count >= MaxVorschlaege) break;
                treffer.Add(eintrag);
            }

            kopf.Text = ausgewichen
                ? "Kein Eintrag mit passendem Typ - alle "
                  + ohneTyp.Count.ToString(CultureInfo.CurrentCulture) + " Treffer gezeigt"
                : treffer.Count < quelle.Count
                    ? treffer.Count.ToString(CultureInfo.CurrentCulture) + " von "
                      + quelle.Count.ToString(CultureInfo.CurrentCulture) + " Treffern - weiter tippen"
                    : quelle.Count.ToString(CultureInfo.CurrentCulture) + " Treffer";
            if (treffer.Count > 0) liste.SelectedIndex = 0;
        }

        void Oeffnen()
        {
            if (service.Adresspool.Count == 0) return;
            Fuellen();
            if (treffer.Count == 0) return;
            popup.IsOpen = true;
        }

        void Nehmen()
        {
            if (liste.SelectedItem is not GroupAddressEntry eintrag) return;
            box.Text = eintrag.Address.ToString();
            box.CaretIndex = box.Text.Length;
            popup.IsOpen = false;
            uebernommen?.Invoke();
        }

        box.TextChanged += (_, _) =>
        {
            if (!box.IsKeyboardFocusWithin) return;
            Oeffnen();
        };

        // Ein Doppelklick oeffnet die ganze Liste - fuer den Fall, dass man
        // nicht weiss, wonach man tippen soll.
        box.PreviewMouseDoubleClick += (_, e) =>
        {
            e.Handled = true;
            box.SelectAll();
            Oeffnen();
        };

        box.PreviewKeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Down when !popup.IsOpen:
                    Oeffnen();
                    e.Handled = true;
                    break;
                case Key.Down when popup.IsOpen:
                    if (liste.SelectedIndex < treffer.Count - 1) liste.SelectedIndex++;
                    liste.ScrollIntoView(liste.SelectedItem);
                    e.Handled = true;
                    break;
                case Key.Up when popup.IsOpen:
                    if (liste.SelectedIndex > 0) liste.SelectedIndex--;
                    liste.ScrollIntoView(liste.SelectedItem);
                    e.Handled = true;
                    break;
                case Key.Enter when popup.IsOpen:
                    Nehmen();
                    e.Handled = true;
                    break;
                case Key.Escape when popup.IsOpen:
                    popup.IsOpen = false;
                    e.Handled = true;
                    break;
                default:
                    break;
            }
        };

        liste.PreviewMouseLeftButtonUp += (_, _) => Nehmen();
        nurTyp.Click += (_, _) => Fuellen();
        box.LostKeyboardFocus += (_, _) =>
        {
            if (!popup.IsKeyboardFocusWithin) popup.IsOpen = false;
        };
    }

    /// <summary>
    /// Ob der Datenpunkttyp einer Adresse zum erwarteten passt. Verglichen
    /// wird nur die Hauptnummer: ob eine Temperatur als 9.001 oder 9.002
    /// gefuehrt wird, aendert nichts daran, dass sie zwei Byte Gleitkomma ist.
    /// </summary>
    public static bool Passt(GroupAddressEntry eintrag, int erwartet)
    {
        if (erwartet == 0) return true;
        return ValueCodec.MainNumber(eintrag.Datapoint) == erwartet;
    }

    private static DataTemplate Vorlage()
    {
        var reihe = new FrameworkElementFactory(typeof(StackPanel));
        reihe.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        FrameworkElementFactory Feld(string pfad, double breite, bool betont)
        {
            var block = new FrameworkElementFactory(typeof(TextBlock));
            block.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(pfad));
            block.SetValue(FrameworkElement.WidthProperty, breite);
            block.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            block.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 8, 0));
            if (betont) block.SetValue(TextBlock.ForegroundProperty, Application.Current.Resources["Betont"]);
            return block;
        }

        reihe.AppendChild(Feld(nameof(GroupAddressEntry.Address), 80, true));
        reihe.AppendChild(Feld(nameof(GroupAddressEntry.Name), 230, false));
        reihe.AppendChild(Feld(nameof(GroupAddressEntry.Datapoint), 60, false));
        return new DataTemplate { VisualTree = reihe };
    }

    /// <summary>
    /// Ein KNX-Projekt oeffnen. Steht hier, weil es von mehreren Fenstern
    /// aus gebraucht wird.
    /// </summary>
    public static bool Laden(Window besitzer, Wintergartendienst service)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "KNX-Projekt oder Gruppenadressexport oeffnen",
            Filter = "KNX-Projekt und Exporte (*.knxproj;*.xml;*.csv;*.esf)|*.knxproj;*.xml;*.csv;*.esf"
                     + "|ETS-Projekt (*.knxproj)|*.knxproj"
                     + "|Gruppenadressexport (*.xml;*.csv)|*.xml;*.csv"
                     + "|OPC-Export (*.esf)|*.esf"
                     + "|Alle Dateien (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(besitzer) != true) return false;

        var geladen = service.ProjektLaden(dialog.FileName, out var hinweis);
        MessageBox.Show(besitzer, hinweis, "KNX-Projekt",
            MessageBoxButton.OK, geladen ? MessageBoxImage.Information : MessageBoxImage.Warning);
        return geladen;
    }
}
