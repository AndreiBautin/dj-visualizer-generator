#!/usr/bin/env bash
# Regenerates the bundled demo assets from scratch with ffmpeg.
#
# Every byte of the demo dataset is SYNTHESIZED here - tones from ffmpeg's `sine` source and a
# drawn gradient. Nothing is exported, copied or captured from a real device or a real mix, so
# there is no pipeline by which personal audio or artwork could reach the public deployment.
# See docs/DEMO_DATA.md.
#
# Usage: bash scripts/generate-demo-assets.sh   (run from anywhere; paths resolve to the repo)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
mkdir -p assets/demo

DURATION=24
# Relative path on purpose: an absolute Windows path would need drive-colon escaping inside the
# filter graph, and ffmpeg segfaults on this build if drawtext gets no usable font at all.
FONT="assets/fonts/Poppins-ExtraBold.ttf"

# --- Artwork -------------------------------------------------------------------------------
# A 1000x1000 gradient with a caption, so it is instantly recognisable as a stand-in rather than
# looking like someone's real cover art.
ffmpeg -y -loglevel error \
  -f lavfi -i "gradients=s=1000x1000:c0=0x1B1035:c1=0xE94560:c2=0x0F3460:c3=0x16C79A:n=4:type=radial:duration=1" \
  -frames:v 1 \
  -vf "drawtext=fontfile=$FONT:text='SAMPLE':fontcolor=white@0.92:fontsize=140:x=(w-text_w)/2:y=(h/2)-170,drawtext=fontfile=$FONT:text='DEMO MIX':fontcolor=white@0.92:fontsize=140:x=(w-text_w)/2:y=(h/2)+10,drawtext=fontfile=$FONT:text='synthesized audio':fontcolor=white@0.55:fontsize=38:x=(w-text_w)/2:y=h-150" \
  assets/demo/sample-artwork.png

# --- Audio ---------------------------------------------------------------------------------
# A looping pad chord (A2/E3/C#4/A4) plus a four-on-the-floor thump, so the demo video has
# something to actually play rather than silence.
ffmpeg -y -loglevel error \
  -f lavfi -i "sine=frequency=110:duration=$DURATION" \
  -f lavfi -i "sine=frequency=164.81:duration=$DURATION" \
  -f lavfi -i "sine=frequency=277.18:duration=$DURATION" \
  -f lavfi -i "sine=frequency=440:duration=$DURATION" \
  -f lavfi -i "sine=frequency=55:duration=$DURATION" \
  -filter_complex "\
    [0:a]volume=0.28[a0];[1:a]volume=0.22[a1];[2:a]volume=0.16[a2];[3:a]volume=0.10[a3];\
    [a0][a1][a2][a3]amix=inputs=4:normalize=0,tremolo=f=2:d=0.35[pad];\
    [4:a]volume=0.9,tremolo=f=2:d=0.95,highpass=f=30[kick];\
    [pad][kick]amix=inputs=2:normalize=0,alimiter=limit=0.9,afade=t=in:d=1,afade=t=out:st=$((DURATION-2)):d=2[out]" \
  -map "[out]" -c:a libmp3lame -b:a 192k -ar 44100 -ac 2 \
  assets/demo/sample-mix.mp3

echo "Generated:"
ls -la assets/demo
