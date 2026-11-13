#!/usr/bin/env bash
# TabDeck Agent'i Ubuntu'dan, Windows'un hazir csc.exe derleyicisiyle (WSL interop) derler.
# Cikti Windows tarafinda calisan programin yanina gider; Windows'a hicbir sey kurulmaz.
# Kullanim: build.sh [cikti.exe]   (calisan agent'a dokunmadan test icin farkli ad verilebilir)
set -euo pipefail

SRC_DIR="/home/orhan/projects/tabdeck/agent"
OUT_DIR="/mnt/d/OKWorks/Ai Projects/TabDeck/agent"
OUT_EXE="${1:-TabDeckAgent.exe}"

mkdir -p "$OUT_DIR"
cd "$OUT_DIR"
[ "$(pwd)" = "$OUT_DIR" ] || { echo "HATA: yanlis klasor: $(pwd)"; exit 1; }

bash "$SRC_DIR/compile.sh" "$SRC_DIR" "$OUT_DIR" "$OUT_EXE"
ls -l "$OUT_DIR/$OUT_EXE"
