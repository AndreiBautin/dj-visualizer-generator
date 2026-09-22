#!/usr/bin/env bash
# Verifies a LIVE deployment end to end - not that a deploy step went green, but that the site
# actually answers and can still render a video.
#
#   bash scripts/verify-deployment.sh https://your-service.onrender.com
#
# Exits non-zero on the first failure. Safe to run against a free instance that has spun down:
# the first request is given a long timeout to cover the cold start.
set -euo pipefail

BASE="${1:-}"
if [ -z "$BASE" ]; then
  echo "usage: $0 <base-url>" >&2
  exit 2
fi
BASE="${BASE%/}"

EXPECTED="${2:?Pass the expected git commit as the second argument}"
EXPECTED="${EXPECTED:0:7}"
for _ in $(seq 1 120); do
  ACTUAL=$(curl -fsS --max-time 10 "$BASE/version" 2>/dev/null | grep -o '"commit":"[^"]*"' | cut -d'"' -f4 || true)
  [ "$ACTUAL" = "$EXPECTED" ] && break
  sleep 5
done
[ "$ACTUAL" = "$EXPECTED" ] || { echo "Expected $EXPECTED, found $ACTUAL" >&2; exit 1; }
command -v ffmpeg >/dev/null
command -v ffprobe >/dev/null

PASS=0
FAIL=0

ok()   { printf '  \033[32mok\033[0m   %s\n' "$1"; PASS=$((PASS + 1)); }
bad()  { printf '  \033[31mFAIL\033[0m %s\n' "$1"; FAIL=$((FAIL + 1)); }
note() { printf '\n\033[1m%s\033[0m\n' "$1"; }

note "Waking the instance (a free service spins down after 15 min idle; allow ~90s)"
START=$(date +%s)
HEALTH=""
for _ in $(seq 1 45); do
  HEALTH=$(curl -fsS --max-time 10 "$BASE/health" 2>/dev/null) && break
  sleep 2
done
WOKE=$(( $(date +%s) - START ))
if [ -n "$HEALTH" ]; then
  ok "/health responded in ${WOKE}s: $HEALTH"
else
  bad "/health never responded (waited ${WOKE}s)"
  echo; echo "Nothing else can be checked while the service is down." >&2
  exit 1
fi

note "The page a reviewer actually lands on"
HOME_BODY=$(curl -fsS --max-time 30 "$BASE/")
if printf '%s' "$HOME_BODY" | grep -q '<div id="root">'; then
  ok "SPA document served"
else
  bad "SPA document not served (is Jobs__SingleContainer=true?)"
fi

CSP=$(curl -fsS -D - -o /dev/null --max-time 30 "$BASE/" | tr -d '\r' | grep -i '^content-security-policy:')
if printf '%s' "$CSP" | grep -q "script-src 'self'"; then
  ok "HTML document got the SPA content-security-policy"
else
  bad "HTML document CSP looks wrong: ${CSP:-<none>}"
fi

API_CSP=$(curl -fsS -D - -o /dev/null --max-time 30 "$BASE/limits" | tr -d '\r' | grep -i '^content-security-policy:')
if printf '%s' "$API_CSP" | grep -q "default-src 'none'"; then
  ok "API response kept the strict content-security-policy"
else
  bad "API CSP looks wrong: ${API_CSP:-<none>}"
fi

DEEP=$(curl -s -o /dev/null -w '%{http_code}' --max-time 30 "$BASE/some/client/route")
if [ "$DEEP" = "200" ]; then
  ok "deep link falls back to the SPA (200)"
else
  bad "deep link returned $DEEP, expected 200"
fi

note "Limits the UI will advertise"
LIMITS=$(curl -fsS --max-time 30 "$BASE/limits")
if printf '%s' "$LIMITS" | grep -q '"maxAudioBytes"'; then
  ok "/limits: $LIMITS"
else
  bad "/limits did not return the expected shape: $LIMITS"
fi

note "A real render, start to finish"
# A title full of filtergraph metacharacters, so this also re-proves the drawtext fix (SECURITY
# F-1) against the deployed build rather than only in CI.
JOB=$(curl -fsS --max-time 60 -X POST "$BASE/jobs/sample" \
  -H 'Content-Type: application/json' \
  -d '{"title":"Live check: It'"'"'s 100%","preset":"720p"}' \
  | grep -o '"jobId":"[^"]*"' | cut -d'"' -f4)

if [ -z "$JOB" ]; then
  bad "POST /jobs/sample did not return a job id (is Demo__Enabled=true?)"
else
  ok "sample job created: $JOB"
  RENDER_START=$(date +%s)
  STATUS=""
  VANISHED=0
  # Generous: a free instance is roughly a tenth of a CPU.
  for _ in $(seq 1 150); do
    CODE=$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "$BASE/jobs/$JOB")
    # A job that existed and now 404s did not fail - the instance restarted underneath it and
    # took the ephemeral disk with it, which is a deploy racing this check rather than a broken
    # app. Distinguishing the two matters, and polling on for ten more minutes helps nobody.
    if [ "$CODE" = "404" ]; then
      VANISHED=1
      break
    fi
    STATUS=$(curl -fsS --max-time 15 "$BASE/jobs/$JOB" 2>/dev/null | grep -o '"status":"[^"]*"' | cut -d'"' -f4)
    case "$STATUS" in Completed|Failed) break;; esac
    sleep 4
  done
  ELAPSED=$(( $(date +%s) - RENDER_START ))

  if [ "$VANISHED" = "1" ]; then
    bad "the job disappeared after ${ELAPSED}s - the instance restarted mid-render (its disk is ephemeral)"
    echo "       Almost always a deploy landing while this ran. Re-run once it has settled." >&2
    STATUS="vanished"
  fi

  if [ "$STATUS" = "Completed" ]; then
    ok "rendered in ${ELAPSED}s"
    TMP=$(mktemp); trap 'rm -f "$TMP"' EXIT
    if curl -fsS --max-time 300 -o "$TMP" "$BASE/jobs/$JOB/download"; then
      SIZE=$(wc -c < "$TMP" | tr -d ' ')
      if [ "$SIZE" -gt 100000 ]; then
        ffprobe -v error -show_entries stream=codec_name,width,height -of json "$TMP"
        ffmpeg -v error -xerror -i "$TMP" -f null -
        ok "downloaded and decoded a ${SIZE}-byte mp4"
      else
        bad "download was only ${SIZE} bytes"
      fi
    else
      bad "download request failed"
    fi
  elif [ "$VANISHED" != "1" ]; then
    bad "render ended as '${STATUS:-no status}' after ${ELAPSED}s"
    curl -fsS --max-time 15 "$BASE/jobs/$JOB" 2>/dev/null | sed 's/^/       /'
  fi
fi

note "Result"
printf '  %d passed, %d failed\n\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ] || exit 1
echo "Live deployment verified: $BASE"
