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

# Der Ordner fuer Einstellungen, Protokoll und Verlauf. Als Datentraeger
# eingehaengt ueberlebt er jede Aktualisierung des Bildes.
ENV REGOWINTERGARDEN_HOME=/daten
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
VOLUME /daten

# Nicht als root. Der Container braucht nichts vom System ausser dem Netz.
RUN useradd --system --no-create-home --shell /usr/sbin/nologin regowg \
 && mkdir -p /daten && chown regowg:regowg /daten
USER regowg

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s \
  CMD ["/programm/regowintergarden", "--pruefen"]

ENTRYPOINT ["/programm/regowintergarden"]
CMD ["--port", "8080"]