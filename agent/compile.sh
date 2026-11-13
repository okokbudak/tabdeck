#!/usr/bin/env bash
# Ortak derleme adimi (build.sh ve release script'i kullanir).
# Kullanim: compile.sh <kaynak klasoru> <cikti klasoru (/mnt/... Windows yolu)> <exe adi> [surum]
# csc, \\wsl.localhost yolundaki kaynaklari okuyamadigi icin kaynaklar once cikti klasorundeki
# gecici .src klasorune kopyalanir ve derlemeden sonra silinir.
set -euo pipefail

SRC="${1:?kaynak klasoru}"
OUT="${2:?cikti klasoru}"
EXE="${3:?exe adi}"
VER="${4:-}"
CSC="/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
FW='C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
TMP="$OUT/.src"

case "$OUT" in /mnt/d/OKWorks/*) ;; *) echo "HATA: beklenmeyen cikti klasoru: $OUT"; exit 1 ;; esac
test -x "$CSC"

mkdir -p "$TMP"
cp "$SRC"/*.cs "$TMP/"
ICON_ARGS=()
if [ -f "$SRC/tabdeck.ico" ]; then
  cp "$SRC/tabdeck.ico" "$TMP/"
  ICON_ARGS=("/win32icon:.src\\tabdeck.ico" "/resource:.src\\tabdeck.ico,TabDeck.tabdeck.ico")
fi
if [ -n "$VER" ]; then
  sed -i "s/AssemblyFileVersion(\"[^\"]*\")/AssemblyFileVersion(\"$VER.0\")/; s/AssemblyInformationalVersion(\"[^\"]*\")/AssemblyInformationalVersion(\"$VER\")/" \
    "$TMP/TabDeckAgent.cs"
fi

cd "$OUT"
[ "$(pwd)" = "$OUT" ] || { echo "HATA: yanlis klasor: $(pwd)"; exit 1; }
SOURCES=()
for f in "$TMP"/*.cs; do SOURCES+=(".src\\$(basename "$f")"); done

status=0
"$CSC" /nologo /optimize /codepage:65001 /target:winexe "/out:$EXE" "${ICON_ARGS[@]}" \
  "/r:$FW\\System.Web.Extensions.dll" "/r:$FW\\System.Drawing.dll" "/r:$FW\\System.Windows.Forms.dll" \
  "/r:$FW\\Microsoft.CSharp.dll" "/r:$FW\\System.Core.dll" "${SOURCES[@]}" || status=$?

# Gecici kaynak kopyalarini kaldir (yalnizca bu script'in olusturdugu .src klasoru)
rm -f -- "$TMP"/*.cs "$TMP"/tabdeck.ico
rmdir -- "$TMP"
exit $status
