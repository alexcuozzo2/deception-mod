#!/bin/bash
# Deception M1E: offline build/test, install own mod only, explicit reports.
set -euo pipefail
umask 077
HERE="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PY="$(command -v python3 || true)"
if [ -z "$PY" ]; then PY="$(/usr/bin/xcrun --find python3 2>/dev/null || true)"; fi
if [ -z "$PY" ]; then printf 'STOP: Existing Python 3 was not found.\n' >&2; exit 1; fi
exec "$PY" "$HERE/tools/manage.py" "$@"
