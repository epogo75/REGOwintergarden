using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace REGOwintergarden.Ui;

/// <summary>
/// Erzeugt die Symbole des Programms zur Laufzeit â€” gezeichnet, nicht als
/// Binaerdatei im Repository, damit sie sich aendern lassen, ohne ein
/// Bildbearbeitungsprogramm zu oeffnen.
///
/// Windows kennt dafuer <b>zwei verschiedene Sorten</b>, und sie zu
/// verwechseln ist der Grund, warum ein Symbol "fehlt", obwohl ein anderes
/// laengst stimmt:
///
/// <list type="bullet">
/// <item><b>Traybar</b> â€” einfarbig und dem Design der Taskleiste folgend.
/// Weiss auf heller Taskleiste ist unsichtbar, also richtet sich die Farbe
/// nach <c>SystemUsesLightTheme</c>.</item>
/// <item><b>Anwendung</b> â€” farbig, feststehend. Das ist das Symbol der
/// EXE-Datei im Explorer, in der Taskleiste und in Alt-Tab. Es muss auf
/// hellem wie dunklem Grund gleichermassen erkennbar sein, deshalb eine
/// eigene Flaeche mit Farbe statt einer blossen Silhouette.</item>
/// </list>
///
/// Gebaut wird jeweils ein vollstaendiges ICO im Speicher, aus dem sich
/// <see cref="Icon"/> seine Daten selbst kopiert. Der naheliegende Weg ueber
/// <c>Icon.FromHandle(bitmap.GetHicon())</c> funktioniert hier gerade nicht:
/// ein so erzeugtes Icon haelt nur das GDI-Handle, und <c>Clone()</c>
/// kopiert dieses Handle mit, statt die Bilddaten zu uebernehmen. Wird das
/// Handle danach freigegeben, zeigt die Traybar nichts an â€” ohne dass
/// irgendwo ein Fehler auftaucht.
/// </summary>
internal static class AppIcons
{
    // Windows waehlt aus dem ICO die passende Groesse selbst: 16 bei 100 %,
    // 20 bei 125 %, 24 bei 150 %, 32 bei 200 %.
    private static readonly int[] TraySizes = { 16, 20, 24, 32, 48, 64 };

    // Fuer die Datei zusaetzlich die grossen Stufen - der Explorer zeigt in
    // der Ansicht "Extra grosse Symbole" 256 px.
    private static readonly int[] AppSizes = { 16, 20, 24, 32, 48, 64, 128, 256 };

    /// <summary>
    /// Ob die Taskleiste hell ist. Windows fuehrt das getrennt von der
    /// Farbgebung der Anwendungen: <c>SystemUsesLightTheme</c> gilt fuer
    /// Taskleiste und Startmenue, <c>AppsUseLightTheme</c> fuer Fenster. Fuer
    /// ein Traysymbol zaehlt der erste Wert.
    /// </summary>
    public static bool TaskbarUsesLightTheme
    {
        get
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                // Fehlt der Wert, ist Windows hell - so liefert es aus.
                return key?.GetValue("SystemUsesLightTheme") is not int v || v != 0;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    // ---- Traybar ---------------------------------------------------------

    public static Icon CreateTrayIcon(bool muted = false)
    {
        var light = TaskbarUsesLightTheme;
        using var stream = BuildIco(TraySizes, size => DrawGlyph(size, muted, Tint(light, muted), null));
        return new Icon(stream);
    }

    private static Color Tint(bool lightTaskbar, bool muted) => lightTaskbar
        ? (muted ? Color.FromArgb(130, 130, 130) : Color.FromArgb(26, 26, 26))
        : (muted ? Color.FromArgb(140, 140, 140) : Color.White);

    // ---- Anwendung -------------------------------------------------------

    /// <summary>Das farbige Symbol als vollstaendige ICO-Daten.</summary>
    public static byte[] CreateAppIcoBytes()
    {
        using var stream = BuildIco(AppSizes, size => DrawGlyph(size, muted: false, Color.White, BackgroundFor(size)));
        return stream.ToArray();
    }

    /// <summary>
    /// Schreibt das farbige Symbol als <c>.ico</c>. Der Build bettet es als
    /// Symbol der EXE ein â€” dafuer braucht er eine Datei, ein zur Laufzeit
    /// gezeichnetes Symbol kommt zu spaet.
    /// </summary>
    public static void WriteAppIco(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, CreateAppIcoBytes());
    }

    /// <summary>
    /// Symbol fuer Fenster: Titelleiste, Taskleistenschaltflaeche, Alt-Tab.
    /// WPF nimmt dafuer keine <see cref="Icon"/>, sondern eine ImageSource â€”
    /// ohne diese Umwandlung bleibt das Fenster beim WPF-Standardsymbol, und
    /// zwar unabhaengig davon, ob das Traysymbol stimmt.
    /// </summary>
    public static System.Windows.Media.ImageSource CreateWindowIcon()
    {
        using var stream = new MemoryStream(CreateAppIcoBytes());
        var decoder = new System.Windows.Media.Imaging.IconBitmapDecoder(
            stream,
            System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);

        // Den groessten Rahmen nehmen: die Taskleiste zeigt bei hoher
        // Skalierung 32 px und mehr, und herunterrechnen sieht besser aus als
        // ein 16-px-Symbol aufzublasen.
        var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        frame.Freeze();
        return frame;
    }

    /// <summary>
    /// Gruen, nicht blau. REGOsound und REGOsimulator tragen ein blaues
    /// Symbol; drei blaue Kacheln nebeneinander in der Taskleiste sind nicht
    /// auseinanderzuhalten, und die Farbe ist das Erste, was man von einem
    /// Symbol wahrnimmt - lange vor der Form darin.
    /// </summary>
    private static Brush BackgroundFor(int size)
        => new LinearGradientBrush(
            new RectangleF(0, 0, size, size),
            Color.FromArgb(0x2F, 0xA8, 0x60),
            Color.FromArgb(0x14, 0x6B, 0x3C),
            LinearGradientMode.ForwardDiagonal);

    // ---- Zeichnung -------------------------------------------------------

    /// <summary>
    /// Drei Schieberegler. Mit <paramref name="background"/> auf einer
    /// eigenen abgerundeten Flaeche (Anwendungssymbol), ohne als blosse
    /// Silhouette (Traybar).
    /// </summary>
    private static Bitmap DrawGlyph(int size, bool muted, Color tint, Brush? background)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var s = size / 32f;
        var inset = 0f;

        if (background is not null)
        {
            // Abgerundetes Quadrat wie bei Windows-Anwendungssymbolen.
            var radius = size * 0.22f;
            using var shape = RoundedRectangle(0.5f, 0.5f, size - 1f, size - 1f, radius);
            g.FillPath(background, shape);
            // Der Lautsprecher darf die Kante nicht beruehren.
            inset = size * 0.06f;
            g.TranslateTransform(inset, inset);
            s = (size - 2 * inset) / 32f;
        }

        using var body = new SolidBrush(tint);

        // Ein Wintergarten unter der Sonne: das schraege Glasdach mit zwei
        // Sprossen, darueber die Sonne.
        //
        // Warum kein Haus mit Satteldach: das haben zwanzig andere Programme
        // auch. Das schraege Pultdach mit Sprossen ist das, was einen
        // Wintergarten von einem Haus unterscheidet, und es bleibt bis zur
        // kleinsten Stufe lesbar, weil nur drei Formen darin vorkommen -
        // Kreis, Schraege, Strich.
        var dicke = Math.Max(1f, 2.4f * s);
        using var stift = new Pen(tint, dicke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        // Die Sonne oben links - gefuellt, damit sie auch klein eine Sonne
        // bleibt und kein Ring.
        var sonne = 4.6f * s;
        g.FillEllipse(body, 5.5f * s - sonne / 2, 8f * s - sonne / 2, sonne, sonne);

        // Das Dach: von unten links schraeg hoch, dann waagerecht, dann
        // hinunter.
        g.DrawLines(stift, new[]
        {
            new PointF(5f * s, 26f * s),
            new PointF(13f * s, 12f * s),
            new PointF(27f * s, 12f * s),
            new PointF(27f * s, 26f * s),
        });

        // Zwei Sprossen machen aus der Flaeche ein Glasdach.
        using var duenn = new Pen(tint, Math.Max(1f, 1.5f * s))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        g.DrawLine(duenn, 17.6f * s, 12f * s, 17.6f * s, 26f * s);
        g.DrawLine(duenn, 22.3f * s, 12f * s, 22.3f * s, 26f * s);

        // Der Boden - er stellt das Dach auf und nimmt ihm das Schwebende.
        g.DrawLine(stift, 4f * s, 26f * s, 28f * s, 26f * s);
        g.ResetTransform();
        return bitmap;
    }

    private static GraphicsPath RoundedRectangle(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath();
        var d = r * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Schreibt ein ICO mit PNG-codierten Eintraegen. Das versteht Windows
    /// seit Vista fuer jede Groesse und spart die Handarbeit an DIB-Kopf und
    /// AND-Maske.
    /// </summary>
    private static MemoryStream BuildIco(IReadOnlyList<int> sizes, Func<int, Bitmap> render)
    {
        var images = new List<byte[]>(sizes.Count);
        foreach (var size in sizes)
        {
            using var bitmap = render(size);
            using var buffer = new MemoryStream();
            bitmap.Save(buffer, ImageFormat.Png);
            images.Add(buffer.ToArray());
        }

        var target = new MemoryStream();
        using (var writer = new BinaryWriter(target, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            // ICONDIR
            writer.Write((ushort)0);            // reserviert
            writer.Write((ushort)1);            // Typ 1 = Icon
            writer.Write((ushort)sizes.Count);

            // ICONDIRENTRY je Bild, 16 Bytes
            var offset = 6 + (16 * sizes.Count);
            for (var i = 0; i < sizes.Count; i++)
            {
                // 0 steht im Format fuer 256 - groesser geht nicht.
                writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                writer.Write((byte)0);          // Farbanzahl der Palette
                writer.Write((byte)0);          // reserviert
                writer.Write((ushort)1);        // Ebenen
                writer.Write((ushort)32);       // Bit je Bildpunkt
                writer.Write(images[i].Length);
                writer.Write(offset);
                offset += images[i].Length;
            }

            foreach (var image in images) writer.Write(image);
            writer.Flush();
        }

        target.Position = 0;
        return target;
    }
}