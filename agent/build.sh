#!/usr/bin/env bash
# TabDeck Agent'i Ubuntu'dan, Windows'un hazir csc.exe derleyicisiyle (WSL interop) derler.
# Cikti Windows tarafinda calisan programin yanina gider; Windows'a hicbir sey kurulmaz.
set -euo pipefail

SRC="/home/orhan/projects/tabdeck/agent/TabDeckAgent.cs"
OUT_DIR="/mnt/d/OKWorks/Ai Projects/TabDeck/agent"
CSC="/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
FW='C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
OUT_EXE="${1:-TabDeckAgent.exe}" # calisan agent'a dokunmadan test icin farkli ad verilebilir

test -f "$SRC"
test -x "$CSC"
mkdir -p "$OUT_DIR"
cd "$OUT_DIR"
[ "$(pwd)" = "$OUT_DIR" ] || { echo "HATA: yanlis klasor: $(pwd)"; exit 1; }

# csc, \\wsl.localhost yolundaki kaynagi okuyamayabilir: once Windows tarafina kopyala.
cp "$SRC" "$OUT_DIR/TabDeckAgent.cs"
"$CSC" /nologo /optimize "/out:$OUT_EXE" \
  "/r:$FW\\System.Web.Extensions.dll" "/r:$FW\\System.Drawing.dll" \
  "/r:$FW\\Microsoft.CSharp.dll" "/r:$FW\\System.Core.dll" TabDeckAgent.cs
# Gecici kaynak kopyasini kaldir (yalnizca bu script'in az once kopyaladigi dosya)
rm -f -- "$OUT_DIR/TabDeckAgent.cs"
ls -l "$OUT_DIR/$OUT_EXE"
