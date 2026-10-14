#!/usr/bin/env bash
# NFR-05 gate (ISKELET R-12): fails when the line coverage in a merged Cobertura report is below the threshold.
# Usage: scripts/check-coverage.sh <Cobertura.xml> <minimum line coverage in percent>
set -euo pipefail

report="${1:?usage: check-coverage.sh <Cobertura.xml> <percent>}"
threshold="${2:?usage: check-coverage.sh <Cobertura.xml> <percent>}"

# The root element carries the merged rate: <coverage line-rate="0.917" ...>.
rate=$(grep -o '<coverage [^>]*line-rate="[0-9.]*"' "$report" | head -n 1 | sed -E 's/.*line-rate="([0-9.]*)"/\1/')
if [ -z "$rate" ]; then
  echo "coverage: no line-rate found in $report" >&2
  exit 2
fi

awk -v rate="$rate" -v threshold="$threshold" 'BEGIN {
  percent = rate * 100
  printf "coverage: line %.1f%% (minimum %s%%)\n", percent, threshold
  exit (percent + 1e-9 >= threshold) ? 0 : 1
}'
