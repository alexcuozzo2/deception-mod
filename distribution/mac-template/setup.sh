#!/bin/bash
set -euo pipefail
TASK_DIR="$(cd -- "$(dirname -- "$0")" && pwd)"
TASK_PYTHON="$(command -v python3 || true)"
if [ -z "$TASK_PYTHON" ]; then
  printf 'Python 3 is required. See START-HERE.md for installation instructions.\n' >&2
  exit 1
fi
exec "$TASK_PYTHON" "$TASK_DIR/setup.py" "$@"
