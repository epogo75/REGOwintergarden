#!/bin/sh
# REGOwintergarden auf den neuesten Stand bringen.
#
#   sudo regowintergarden-update.sh
#
# Holt den Quelltext, baut ihn, legt das Ergebnis nach /opt/regowintergarden
# und startet den Dienst neu. Dieselben vier Schritte, die man sonst von Hand
# tippt - nur ohne die Gelegenheit, einen davon zu vergessen.
#
# Absichtlich wird gebaut und nicht heruntergeladen: das Verzeichnis ist
# privat, und ein Bauen aus dem Quelltext braucht kein Lesezeichen. Das SDK
# holt sich das Skript einmalig selbst, falls keines da ist.
#
# Nichts geschieht heimlich: jeder Schritt sagt an, was er tut, und am Ende
# steht die Fassung, die jetzt laeuft.

set -eu

REPO="https://github.com/epogo75/REGOwintergarden"
ZWEIG="${REGOWG_ZWEIG:-main}"
QUELLE="${REGOWG_QUELLE:-/opt/regowintergarden/quelle}"
ZIEL="${REGOWG_ZIEL:-/opt/regowintergarden}"
DIENST="regowintergarden"
DOTNET_DIR="${REGOWG_DOTNET:-/opt/dotnet}"

sagen() { printf '\033[1;32m==>\033[0m %s\n' "$*"; }
warnen() { printf '\033[1;33m==>\033[0m %s\n' "$*"; }
sterben() { printf '\033[1;31mAbbruch:\033[0m %s\n' "$*" >&2; exit 1; }

[ "$(id -u)" = "0" ] || sterben "Bitte mit sudo starten."

# ---- 1. Architektur -------------------------------------------------------
case "$(uname -m)" in
  aarch64|arm64)  RID="linux-arm64" ;;
  armv7l|armv6l)  RID="linux-arm" ;;
  x86_64|amd64)   RID="linux-x64" ;;
  *) sterben "Unbekannte Architektur: $(uname -m)." ;;
esac

# ---- 2. Quelltext ---------------------------------------------------------
# Immer aus dem eigenen Ordner heraus arbeiten und nie aus dem, in dem die
# Shell gerade steht: ein "rm -rf" auf das eigene Arbeitsverzeichnis nimmt
# einem den Boden unter den Fuessen weg, und danach scheitert jeder weitere
# Befehl mit einem raetselhaften getcwd-Fehler.
cd /

if [ -d "$QUELLE/.git" ]; then
  sagen "Hole Aenderungen in $QUELLE"
  git -C "$QUELLE" fetch --quiet origin "$ZWEIG"
  git -C "$QUELLE" reset --quiet --hard "origin/$ZWEIG"
else
  sagen "Hole den Quelltext nach $QUELLE"
  rm -rf "$QUELLE"
  mkdir -p "$(dirname "$QUELLE")"
  git clone --quiet --branch "$ZWEIG" --depth 50 "$REPO" "$QUELLE"
fi

STAND="$(git -C "$QUELLE" log -1 --format='%h %s')"
sagen "Stand: $STAND"

# ---- 3. Bauwerkzeug -------------------------------------------------------
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  DOTNET="$(command -v dotnet)"
elif [ -x "$DOTNET_DIR/dotnet" ]; then
  DOTNET="$DOTNET_DIR/dotnet"
else
  sagen "Kein .NET SDK 8 gefunden - hole es nach $DOTNET_DIR"
  command -v curl >/dev/null 2>&1 || sterben "curl wird zum Holen des SDK gebraucht."
  curl -fsSL https://dot.net/v1/dotnet-install.sh \
    | bash -s -- --channel 8.0 --install-dir "$DOTNET_DIR" --no-path >/dev/null \
    || sterben "Das SDK liess sich nicht holen."
  DOTNET="$DOTNET_DIR/dotnet"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# ---- 4. Bauen -------------------------------------------------------------
# Nur den Dienst, nicht die ganze Projektmappe: Oberflaeche und Pruefungen
# sind net8.0-windows und lassen sich auf Linux gar nicht bauen.
BAU="$(mktemp -d)"
trap 'rm -rf "$BAU"' EXIT

sagen "Baue fuer $RID - das dauert eine Minute"
"$DOTNET" publish "$QUELLE/src/REGOwintergarden.Daemon/REGOwintergarden.Daemon.csproj" \
  -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -o "$BAU" -v q --nologo \
  || sterben "Das Bauen ist fehlgeschlagen - die Meldung steht darueber."

[ -f "$BAU/regowintergarden" ] || sterben "Gebaut, aber kein Programm entstanden."

# ---- 5. Austauschen -------------------------------------------------------
# Erst anhalten, dann tauschen: eine laufende Datei laesst sich unter Linux
# zwar ueberschreiben, aber der laufende Dienst arbeitet dann noch mit der
# alten - und beim naechsten Absturz mit einer halben.
LIEF=0
if command -v systemctl >/dev/null 2>&1 && systemctl is-active --quiet "$DIENST"; then
  LIEF=1
  sagen "Halte den Dienst an"
  systemctl stop "$DIENST"
fi

mkdir -p "$ZIEL"
install -m 0755 "$BAU/regowintergarden" "$ZIEL/regowintergarden"
[ -f "$QUELLE/linux/uninstall.sh" ] && install -m 0755 "$QUELLE/linux/uninstall.sh" "$ZIEL/uninstall.sh"
[ -f "$QUELLE/linux/update.sh" ] && install -m 0755 "$QUELLE/linux/update.sh" "$ZIEL/update.sh"
sagen "Programm ausgetauscht"

# ---- 6. Wieder anwerfen ---------------------------------------------------
if [ "$LIEF" = "1" ]; then
  systemctl start "$DIENST"
  sagen "Dienst gestartet"
elif command -v systemctl >/dev/null 2>&1 && [ -f "/etc/systemd/system/$DIENST.service" ]; then
  warnen "Der Dienst lief nicht - er bleibt aus. Starten mit: systemctl start $DIENST"
else
  warnen "Kein systemd-Dienst eingerichtet. Von Hand starten:"
  warnen "  REGOWINTERGARDEN_HOME=/etc/regowintergarden $ZIEL/regowintergarden --port 5160"
  exit 0
fi

# ---- 7. Nachsehen, ob er auch antwortet -----------------------------------
# Die Portnummer steht in der Diensteinheit - dieselbe, mit der eingerichtet
# wurde. Sie hier erneut zu raten waere die Gelegenheit, sich zu irren.
PORT="$(sed -n 's/.*--port[= ]\([0-9]\+\).*/\1/p' "/etc/systemd/system/$DIENST.service" 2>/dev/null | head -n1)"
[ -n "${PORT:-}" ] || PORT=5160

sleep 3
if "$ZIEL/regowintergarden" --gesundheit --port "$PORT" >/dev/null 2>&1; then
  sagen "Laeuft und antwortet auf Port $PORT"
else
  warnen "Der Dienst antwortet noch nicht auf Port $PORT."
  warnen "Nachsehen mit: journalctl -u $DIENST -n 40 --no-pager"
fi

cat <<EOF

  Fertig. Jetzt laeuft: $STAND

  Oberflaeche:   http://$(hostname -I 2>/dev/null | awk '{print $1}'):$PORT
  Protokoll:     journalctl -u $DIENST -f

EOF
