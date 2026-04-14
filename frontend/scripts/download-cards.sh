#!/usr/bin/env bash
# Downloads Rider-Waite-Smith tarot card images (public domain) from Wikimedia Commons
# Run from the frontend directory: bash scripts/download-cards.sh

DEST="public/cards"
mkdir -p "$DEST/major" "$DEST/wands" "$DEST/cups" "$DEST/swords" "$DEST/pentacles"

WM="https://commons.wikimedia.org/wiki/Special:FilePath"
UA="Mozilla/5.0 (compatible; tarot-downloader/1.0)"

fetch() {
  local url="$1"
  local out="$2"
  if [ -f "$out" ] && file "$out" | grep -q JPEG; then
    return 0
  fi
  curl -sL --retry 2 -A "$UA" "$url" -o "$out"
  sleep 0.4
  if ! file "$out" | grep -q JPEG; then
    echo "  WARN: not a JPEG → $out ($(file "$out" | cut -d: -f2 | cut -c1-40))"
    rm -f "$out"
    return 1
  fi
}

echo "=== Major Arcana ==="
MAJOR_NAMES=(
  "RWS_Tarot_00_Fool.jpg"
  "RWS_Tarot_01_Magician.jpg"
  "RWS_Tarot_02_High_Priestess.jpg"
  "RWS_Tarot_03_Empress.jpg"
  "RWS_Tarot_04_Emperor.jpg"
  "RWS_Tarot_05_Hierophant.jpg"
  "RWS_Tarot_06_Lovers.jpg"
  "RWS_Tarot_07_Chariot.jpg"
  "RWS_Tarot_08_Strength.jpg"
  "RWS_Tarot_09_Hermit.jpg"
  "RWS_Tarot_10_Wheel_of_Fortune.jpg"
  "RWS_Tarot_11_Justice.jpg"
  "RWS_Tarot_12_Hanged_Man.jpg"
  "RWS_Tarot_13_Death.jpg"
  "RWS_Tarot_14_Temperance.jpg"
  "RWS_Tarot_15_Devil.jpg"
  "RWS_Tarot_16_Tower.jpg"
  "RWS_Tarot_17_Star.jpg"
  "RWS_Tarot_18_Moon.jpg"
  "RWS_Tarot_19_Sun.jpg"
  "RWS_Tarot_20_Judgement.jpg"
  "RWS_Tarot_21_World.jpg"
)
for i in "${!MAJOR_NAMES[@]}"; do
  n=$(printf "%02d" "$i")
  file="${MAJOR_NAMES[$i]}"
  out="$DEST/major/${n}.jpg"
  echo -n "  major/$n ... "
  if fetch "$WM/$file" "$out"; then echo "ok"; else echo "failed"; fi
done

echo "=== Minor Arcana ==="
# Wikimedia prefixes: Wands, Cups, Swords, Pents (for Pentacles)
download_minor() {
  local suit_dir="$1"
  local prefix="$2"
  echo "  --- $suit_dir ---"
  for i in $(seq 1 14); do
    n=$(printf "%02d" "$i")
    out="$DEST/$suit_dir/${n}.jpg"
    echo -n "  $suit_dir/$n ... "
    if fetch "$WM/${prefix}${n}.jpg" "$out"; then echo "ok"; else echo "failed"; fi
  done
}

download_minor "wands"     "Wands"
download_minor "cups"      "Cups"
download_minor "swords"    "Swords"
download_minor "pentacles" "Pents"

echo ""
echo "=== Summary ==="
for suit in major wands cups swords pentacles; do
  count=$(find "$DEST/$suit" -name "*.jpg" 2>/dev/null | wc -l | tr -d ' ')
  echo "  $suit: $count images"
done
