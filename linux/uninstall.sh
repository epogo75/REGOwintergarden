#!/bin/sh
# REGOwintergarden wieder entfernen.
#
# Die Einstellungen bleiben stehen - wer sie loswerden will, loescht
# /etc/regowintergarden von Hand. Ein Installationsskript, das beim
# Entfernen ungefragt die Anlagendaten mitnimmt, hat schon manchen Abend
# gekostet.

set -eu

DIENST="regowintergarden"
ZIEL="/opt/regowintergarden"
DATEN="/etc/regowintergarden"

[ "$(id -u)" = "0" ] || { echo "Bitte mit sudo starten." >&2; exit 1; }

systemctl stop "$DIENST" 2>/dev/null || true
systemctl disable "$DIENST" 2>/dev/null || true
rm -f "/etc/systemd/system/$DIENST.service"
systemctl daemon-reload
rm -rf "$ZIEL"

echo "Entfernt. Die Einstellungen liegen weiterhin in $DATEN."
echo "Ganz loeschen:  sudo rm -rf $DATEN  &&  sudo userdel regowg"