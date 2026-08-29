using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace REGOwintergarden.Service;

/// <summary>
/// Das Nötigste, um als Windows-Dienst zu laufen - von Hand ueber advapi32
/// statt ueber ein Fremdpaket.
///
/// <c>System.ServiceProcess</c> liegt fuer .NET in einem NuGet-Paket, und das
/// waere die einzige Abhaengigkeit im ganzen Programm. Die drei Aufrufe, um
/// die es geht, sind seit Windows NT unveraendert: den Dispatcher starten,
/// einen Steuerungsempfaenger anmelden, den Zustand melden. Dieselbe Technik
/// benutzt REGOdeployHelper fuer die Anmeldeinformationsverwaltung.
///
/// Ein Dienst muss seinen Zustand <b>zuegig</b> melden. Meldet er nach dem
/// Start nicht binnen kurzem „laeuft", beendet ihn der Dienststeuerungs-
/// manager wieder - und im Ereignisprotokoll steht dann eine Zeitueberschreitung
/// statt der eigentlichen Ursache.
/// </summary>
public static class WindowsService
{
    public const string ServiceName = "REGOwintergarden";
    public const string DisplayName = "REGOwintergarden";

    private const int ServiceWin32OwnProcess = 0x00000010;
    private const int ServiceStopped = 0x00000001;
    private const int ServiceStartPending = 0x00000002;
    private const int ServiceStopPending = 0x00000003;
    private const int ServiceRunning = 0x00000004;
    private const int ServiceAcceptStop = 0x00000001;
    private const int ServiceAcceptShutdown = 0x00000004;

    private const int ControlStop = 0x00000001;
    private const int ControlShutdown = 0x00000005;
    private const int ControlInterrogate = 0x00000004;

    private const int ScManagerAllAccess = 0xF003F;
    private const int ServiceAllAccess = 0xF01FF;
    private const int ServiceAutoStart = 0x00000002;
    private const int ServiceErrorNormal = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public int ServiceType;
        public int CurrentState;
        public int ControlsAccepted;
        public int Win32ExitCode;
        public int ServiceSpecificExitCode;
        public int CheckPoint;
        public int WaitHint;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ServiceTableEntry
    {
        public IntPtr Name;
        public IntPtr Proc;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ServiceDescription
    {
        public IntPtr Description;
    }

    private delegate void ServiceMain(int argc, IntPtr argv);

    private delegate int HandlerEx(int control, int eventType, IntPtr eventData, IntPtr context);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool StartServiceCtrlDispatcherW(ServiceTableEntry[] table);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr RegisterServiceCtrlHandlerExW(string name, HandlerEx handler, IntPtr context);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus status);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManagerW(string? machine, string? database, int access);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateServiceW(
        IntPtr manager, string name, string displayName, int access, int serviceType,
        int startType, int errorControl, string binaryPath, string? loadOrderGroup,
        IntPtr tagId, string? dependencies, string? account, string? password);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenServiceW(IntPtr manager, string name, int access);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DeleteService(IntPtr service);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ChangeServiceConfig2W(IntPtr service, int info, ref ServiceDescription value);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr handle);

    private static IntPtr _statusHandle = IntPtr.Zero;
    private static ServiceStatus _status;
    private static ManualResetEventSlim? _stop;
    private static HandlerEx? _handler;
    private static ServiceMain? _main;
    private static Action<CancellationToken>? _arbeit;
    private static CancellationTokenSource? _abbruch;

    /// <summary>
    /// Laeuft als Dienst. Kehrt erst zurueck, wenn Windows den Dienst
    /// beendet. Ausserhalb eines Dienstes schlaegt der Aufruf fehl - dann
    /// wurde das Programm von Hand mit dem falschen Schalter gestartet.
    /// </summary>
    public static bool Run(Action<CancellationToken> arbeit)
    {
        _arbeit = arbeit;
        _main = ServiceMainProc;

        var eintraege = new[]
        {
            new ServiceTableEntry
            {
                Name = Marshal.StringToHGlobalUni(ServiceName),
                Proc = Marshal.GetFunctionPointerForDelegate(_main),
            },
            default,
        };
        return StartServiceCtrlDispatcherW(eintraege);
    }

    private static void ServiceMainProc(int argc, IntPtr argv)
    {
        _handler = HandlerProc;
        _statusHandle = RegisterServiceCtrlHandlerExW(ServiceName, _handler, IntPtr.Zero);
        if (_statusHandle == IntPtr.Zero) return;

        _status = new ServiceStatus
        {
            ServiceType = ServiceWin32OwnProcess,
            CurrentState = ServiceStartPending,
            ControlsAccepted = 0,
            WaitHint = 10000,
        };
        Melden();

        _stop = new ManualResetEventSlim(false);
        _abbruch = new CancellationTokenSource();

        _status.CurrentState = ServiceRunning;
        _status.ControlsAccepted = ServiceAcceptStop | ServiceAcceptShutdown;
        _status.WaitHint = 0;
        Melden();

        try
        {
            _arbeit?.Invoke(_abbruch.Token);
        }
        catch (Exception ex)
        {
            // Ein Dienst, der still stirbt, ist das Schlimmste: im
            // Dienstemanager steht dann nur „beendet". Deshalb landet die
            // Ursache im Ereignisprotokoll, bevor der Zustand gemeldet wird.
            Ereignis("REGOwintergarden ist beendet worden: " + ex);
            _status.Win32ExitCode = 1;
        }

        _status.CurrentState = ServiceStopped;
        _status.ControlsAccepted = 0;
        Melden();
    }

    private static int HandlerProc(int control, int eventType, IntPtr eventData, IntPtr context)
    {
        switch (control)
        {
            case ControlStop:
            case ControlShutdown:
                _status.CurrentState = ServiceStopPending;
                _status.WaitHint = 15000;
                Melden();
                _abbruch?.Cancel();
                _stop?.Set();
                break;
            case ControlInterrogate:
                Melden();
                break;
            default:
                break;
        }
        return 0;
    }

    private static void Melden()
    {
        if (_statusHandle != IntPtr.Zero) SetServiceStatus(_statusHandle, ref _status);
    }

    private static void Ereignis(string text)
    {
        try
        {
            if (!EventLog.SourceExists(ServiceName)) EventLog.CreateEventSource(ServiceName, "Application");
            EventLog.WriteEntry(ServiceName, text, EventLogEntryType.Error);
        }
        catch (Exception)
        {
            // Ohne Rechte gibt es kein Ereignisprotokoll - dann bleibt die
            // Textdatei des Dienstes. Hier noch einmal zu scheitern brächte
            // niemanden weiter.
        }
    }

    // ---- Einrichten -------------------------------------------------------

    /// <summary>
    /// Traegt den Dienst ein. Der Pfad zur EXE bekommt den Schalter
    /// <c>--service</c> und den Einstellungsordner mit - so laeuft der Dienst
    /// mit genau dem Aufbau, den man vorher eingerichtet hat, statt mit einem
    /// leeren aus einem anderen Profil.
    /// </summary>
    public static string Install(string exePath, string home)
    {
        var befehl = "\"" + exePath + "\" --service --home \"" + home + "\"";

        var manager = OpenSCManagerW(null, null, ScManagerAllAccess);
        if (manager == IntPtr.Zero) return "Kein Zugriff auf die Dienstverwaltung. Als Administrator starten.";

        try
        {
            var vorhanden = OpenServiceW(manager, ServiceName, ServiceAllAccess);
            if (vorhanden != IntPtr.Zero)
            {
                CloseServiceHandle(vorhanden);
                return "Der Dienst ist schon eingetragen. Erst entfernen, dann neu eintragen.";
            }

            var dienst = CreateServiceW(
                manager, ServiceName, DisplayName, ServiceAllAccess, ServiceWin32OwnProcess,
                ServiceAutoStart, ServiceErrorNormal, befehl, null, IntPtr.Zero, null, null, null);
            if (dienst == IntPtr.Zero)
            {
                return "Der Dienst liess sich nicht eintragen: Fehler "
                       + Marshal.GetLastWin32Error().ToString(System.Globalization.CultureInfo.InvariantCulture)
                       + ".";
            }

            try
            {
                var text = Marshal.StringToHGlobalUni(
                    "Loest REGOwintergarden-Szenen aus - ueber Gruppenadressen und nach Uhrzeit.");
                var beschreibung = new ServiceDescription { Description = text };
                ChangeServiceConfig2W(dienst, 1, ref beschreibung);
                Marshal.FreeHGlobal(text);
            }
            finally
            {
                CloseServiceHandle(dienst);
            }

            return "Der Dienst ist eingetragen und startet beim naechsten Hochfahren. "
                   + "Jetzt starten mit: net start " + ServiceName;
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    /// <summary>Entfernt den Dienst wieder.</summary>
    public static string Uninstall()
    {
        var manager = OpenSCManagerW(null, null, ScManagerAllAccess);
        if (manager == IntPtr.Zero) return "Kein Zugriff auf die Dienstverwaltung. Als Administrator starten.";

        try
        {
            var dienst = OpenServiceW(manager, ServiceName, ServiceAllAccess);
            if (dienst == IntPtr.Zero) return "Der Dienst ist nicht eingetragen.";

            try
            {
                return DeleteService(dienst)
                    ? "Der Dienst ist entfernt."
                    : "Der Dienst liess sich nicht entfernen: Fehler "
                      + Marshal.GetLastWin32Error().ToString(System.Globalization.CultureInfo.InvariantCulture)
                      + ". Laeuft er noch? Erst: net stop " + ServiceName;
            }
            finally
            {
                CloseServiceHandle(dienst);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    /// <summary>Ob der Dienst eingetragen ist.</summary>
    public static bool IsInstalled()
    {
        var manager = OpenSCManagerW(null, null, ScManagerAllAccess);
        if (manager == IntPtr.Zero) return false;
        try
        {
            var dienst = OpenServiceW(manager, ServiceName, ServiceAllAccess);
            if (dienst == IntPtr.Zero) return false;
            CloseServiceHandle(dienst);
            return true;
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }
}
