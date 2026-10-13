#!/usr/bin/env bash
# AC-16.1: the API started with `dotnet run --project src/EventTicketing.Api` answers GET /api/v1/events with 200 and at
# least one event (the development seed) within 120 s. Tries every 5 s, 24 times.
set -u

base_url="${1:-http://localhost:8080}"
url="$base_url/api/v1/events"

for attempt in $(seq 1 24); do
  body=$(curl --silent --show-error --max-time 10 --write-out '\n%{http_code}' "$url" 2>/dev/null) || body=$'\n000'
  status="${body##*$'\n'}"
  json="${body%$'\n'*}"
  if [ "$status" = "200" ]; then
    count=$(printf '%s' "$json" | grep -o '"totalCount":[0-9]*' | cut -d: -f2)
    if [ "${count:-0}" -ge 1 ]; then
      echo "smoke: OK after attempt $attempt ($url → 200, totalCount=$count)"
      exit 0
    fi
    echo "smoke: attempt $attempt: 200 but no events yet"
  else
    echo "smoke: attempt $attempt: HTTP $status"
  fi
  sleep 5
done

echo "smoke: FAILED, $url did not return 200 with events within 120 s" >&2
exit 1
