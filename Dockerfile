# REGOwintergarden als Container.
#
# Zwei Stufen: bauen mit dem SDK, laufen mit der Laufzeit. Das fertige Bild
# traegt kein SDK mit sich herum - auf einem Raspberry Pi zaehlt jedes
# hundert Megabyte.
#
# Gebaut wird der Dienst, nicht die Windows-Oberflaeche: WPF gibt es hier
# nicht, und gebraucht wird es auch nicht - die Bedienung laeuft im Browser.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS bau
WORKDIR /quelle

# Erst die Projektdateien, dann der Rest: so bleibt die Wiederherstellung im
# Zwischenspeicher, solange sich nur Quelltext aendert.
COPY src/REGOwintergarden.Core/REGOwintergarden.Core.csproj src/REGOwintergarden.Core/
COPY src/REGOwintergarden.Daemon/REGOwintergarden.Daemon.csproj src/REGOwintergarden.Daemon/
RUN dotnet restore src/REGOwintergarden.Daemon/REGOwintergarden.Daemon.csproj

COPY src/REGOwintergarden.Core/ src/REGOwintergarden.Core/
COPY src/REGOwintergarden.Daemon/ src/REGOwintergarden.Daemon/
RUN dotnet publish src/REGOwintergarden.Daemon/REGOwintergarden.Daemon.csproj \
        -c Release -o /programm --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /programm
COPY --from=bau /programm .

ENV REGOWINTERGARDEN_HOME=/daten
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Nicht als root. Der Container braucht nichts vom System ausser dem Netz.
#
# Kennnummer 1000, und das mit Absicht: auf einem Raspberry Pi und auf Ubuntu
# traegt der erste angelegte Mensch dieselbe. Damit gehoert ein eingehaengter
# Ordner vom Wirt schon dem Richtigen, und niemand sucht abends, warum die
# Einstellungen nicht gespeichert werden.
RUN groupadd --gid 1000 regowg \
 && useradd --uid 1000 --gid 1000 --no-create-home --shell /usr/sbin/nologin regowg \
 && mkdir -p /daten && chown regowg:regowg /daten

# Erst jetzt, nach dem chown: was nach einem VOLUME am Pfad geaendert wird,
# landet nicht im Bild. Andersherum bekaeme der Datentraeger root als
# Eigentuemer, und der Dienst duerfte nicht hinein.
VOLUME /daten
USER regowg

EXPOSE 8080

# Gefragt wird der laufende Dienst, nicht die Einstellungen auf der Platte -
# ein "--pruefen" antwortet auch dann noch mit Ja, wenn der Webserver laengst
# steht. Alle sechzig Sekunden, weil jeder Aufruf ein eigener Prozess ist und
# das auf einem Pi nicht nichts kostet.
HEALTHCHECK --interval=60s --timeout=8s --start-period=30s \
  CMD ["/programm/regowintergarden", "--gesundheit", "--port", "8080"]

ENTRYPOINT ["/programm/regowintergarden"]
CMD ["--port", "8080"]