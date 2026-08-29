#!/bin/sh
# REGOwintergarden auf einem Raspberry Pi oder einem anderen Linux einrichten.
#
#   curl -fsSL https://raw.githubusercontent.com/epogo75/REGOwintergarden/main/linux/install.sh | sudo sh
#
# Das Skript ist bewusst in POSIX-sh geschrieben und nicht in bash: auf einem
# frisch aufgesetzten Pi ist /bin/sh vorhanden, alles andere ist eine Annahme.
#
# Es tut vier Dinge und sagt jedes davon an:
#   1. passendes Programm holen (arm64, armhf oder x86-64)
#   2. nach /opt/regowintergarden legen
#   3. Einstellungen unter /etc/regowintergarden anlegen, falls sie fehlen
#   4. systemd-Dienst einrichten und starten
#
# Nichts davon geschieht heimlich, und alles laesst sich mit dem beiliegenden
# uninstall.sh wieder entfernen.

set -eu

REPO="epogo75/REGOwintergarden"
ZWEIG="${REGOWG_ZWEIG:-main}"
# Das Programm kann auch schon dasein - dann wird nichts geladen. Der Weg
# fuer ein privates Verzeichnis und fuer einen Pi ohne Internet:
#   scp dist/linux-arm64/regowintergarden linux/install.sh pi@wintergarten:/tmp/
#   ssh pi@wintergarten 'sudo REGOWG_DATEI=/tmp/regowintergarden sh /tmp/install.sh'
DATEI="${REGOWG_DATEI:-}"
# Fuer ein privates Verzeichnis: ein Lesezeichen ("token") mit Leserecht.
MARKE="${REGOWG_TOKEN:-}"
ZIEL="/opt/regowintergarden"
DATEN="${REGOWG_HOME:-/etc/regowintergarden}"
DIENST="regowintergarden"
BENUTZER="regowg"
PORT="${REGOWG_PORT:-8080}"

sagen() { printf '\033[1;32m==>\033[0m %s\n' "$*"; }
warnen() { printf '\033[1;33m==>\033[0m %s\n' "$*"; }
sterben() { printf '\033[1;31mAbbruch:\033[0m %s\n' "$*" >&2; exit 1; }

[ "$(id -u)" = "0" ] || sterben "Bitte mit sudo starten."

# ---- 1. Architektur -------------------------------------------------------
case "$(uname -m)" in
  aarch64|arm64)  RID="linux-arm64" ;;
  armv7l|armv6l)  RID="linux-arm" ;;
  x86_64|amd64)   RID="linux-x64" ;;
  *) sterben "Unbekannte Architektur: $(uname -m). Von Hand bauen, siehe README." ;;
esac
sagen "Architektur $(uname -m) erkannt: $RID"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

holen() {
  # $1 = Adresse, $2 = Zieldatei
  if command -v curl >/dev/null 2>&1; then
    if [ -n "$MARKE" ]; then curl -fsSL -H "Authorization: Bearer $MARKE" "$1" -o "$2"
    else curl -fsSL "$1" -o "$2"; fi
  else
    if [ -n "$MARKE" ]; then wget -q --header="Authorization: Bearer $MARKE" -O "$2" "$1"
    else wget -qO "$2" "$1"; fi
  fi
}

# ---- 2. Programm ablegen --------------------------------------------------
if [ -n "$DATEI" ]; then
  # Schon mitgebracht - nichts zu laden.
  [ -f "$DATEI" ] || sterben "REGOWG_DATEI zeigt auf nichts: $DATEI"
  cp "$DATEI" "$TMP/regowintergarden"
  sagen "Nehme das mitgebrachte Programm: $DATEI"
else
  command -v curl >/dev/null 2>&1 || command -v wget >/dev/null 2>&1 \
    || sterben "Weder curl noch wget gefunden."

  QUELLE="https://github.com/$REPO/releases/latest/download/regowintergarden-$RID.tar.gz"
  sagen "Lade $QUELLE"
  if holen "$QUELLE" "$TMP/programm.tar.gz"; then
    tar -xzf "$TMP/programm.tar.gz" -C "$TMP"
  else
    # Fast immer derselbe Grund: das Verzeichnis ist privat, und ohne
    # Lesezeichen sieht curl dort gar nichts - auch keinen Fehler, der das
    # sagen wuerde. Deshalb steht der Weg hier ausgeschrieben.
    sterben "Programm nicht geladen.

  Ist das Verzeichnis privat, braucht es ein Lesezeichen:
      sudo REGOWG_TOKEN=ghp_... sh install.sh

  Oder das Programm gleich mitbringen - das geht immer, auch ohne Internet
  am Wintergarten:
      scp dist/$RID/regowintergarden linux/install.sh pi@wintergarten:/tmp/
      ssh pi@wintergarten 'sudo REGOWG_DATEI=/tmp/regowintergarden sh /tmp/install.sh'

  Selbst bauen steht im README."
  fi
fi

mkdir -p "$ZIEL"
install -m 0755 "$TMP/regowintergarden" "$ZIEL/regowintergarden"
sagen "Programm liegt in $ZIEL"

# Das Gegenstueck gleich mitlegen. Ein Einrichter, der keinen Weg zurueck
# hinterlaesst, ist eine halbe Sache - und gesucht wird er genau dann, wenn
# man am wenigsten Lust hat, ihn erst herunterzuladen.
NEBENAN="$(dirname -- "$0")/uninstall.sh"
if [ -f "$NEBENAN" ]; then
  install -m 0755 "$NEBENAN" "$ZIEL/uninstall.sh"
elif holen "https://raw.githubusercontent.com/$REPO/$ZWEIG/linux/uninstall.sh" "$TMP/uninstall.sh"; then
  install -m 0755 "$TMP/uninstall.sh" "$ZIEL/uninstall.sh"
else
  warnen "uninstall.sh nicht gefunden - Entfernen steht im README."
fi

# ---- 3. Benutzer und Einstellungen ---------------------------------------
# Ein eigener Benutzer ohne Anmeldung: die Steuerung braucht nichts vom
# System ausser einem Netzzugang und ihrem eigenen Ordner. Als root zu laufen
# waere bequemer und genau deshalb falsch.
if ! id "$BENUTZER" >/dev/null 2>&1; then
  useradd --system --no-create-home --shell /usr/sbin/nologin "$BENUTZER"
  sagen "Benutzer $BENUTZER angelegt"
fi

mkdir -p "$DATEN"
chown -R "$BENUTZER:$BENUTZER" "$DATEN"
sagen "Einstellungen liegen in $DATEN"

# ---- 4. Dienst ------------------------------------------------------------
cat > "/etc/systemd/system/$DIENST.service" <<EOF
[Unit]
Description=REGOwintergarden - Wintergartensteuerung ueber KNX
Documentation=https://github.com/$REPO
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=$BENUTZER
Group=$BENUTZER
Environment=REGOWINTERGARDEN_HOME=$DATEN
Environment=DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ExecStart=$ZIEL/regowintergarden --port $PORT
Restart=always
RestartSec=10

# Nur so viel Zugriff wie noetig. Die Steuerung schreibt in ihren eigenen
# Ordner und redet ueber das Netz - mehr braucht sie nicht.
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=$DATEN

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable "$DIENST" >/dev/null 2>&1 || true
systemctl restart "$DIENST"
sagen "Dienst $DIENST laeuft"

# ---- Fertig ---------------------------------------------------------------
ADRESSE="$(hostname -I 2>/dev/null | awk '{print $1}')"
[ -n "${ADRESSE:-}" ] || ADRESSE="$(hostname)"

cat <<EOF

  Fertig.

  Oberflaeche:     http://$ADRESSE:$PORT
  Einstellungen:   $DATEN/einstellungen.json
  Protokoll:       journalctl -u $DIENST -f
  Anhalten:        systemctl stop $DIENST
  Entfernen:       $ZIEL/uninstall.sh

  Als Naechstes das KNX-Gateway und die Gruppenadressen eintragen:
  die Datei einstellungen.json bearbeiten und danach
  systemctl restart $DIENST

  Sie hat dasselbe Format wie unter Windows - eine dort eingerichtete
  Anlage laesst sich einfach herueberkopieren.

EOF
