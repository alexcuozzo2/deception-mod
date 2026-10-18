#!/bin/bash
# Finder entry point for the existing, guarded mod-only updater.
set -u
TASK_DIR="$(cd -- "$(dirname -- "$0")" && pwd)" || exit 1

printf 'Deception upgrade\nQuit Polytopia before continuing. Steam may stay open.\n\n'
/bin/bash "$TASK_DIR/Deception-Mac-Setup/setup.sh" update-deception "$@"
TASK_RESULT=$?

if [ "$TASK_RESULT" -eq 0 ]; then
  printf '\nUpgrade complete. Launch Polytopia using your usual modded shortcut.\nBoth players should use this same package.\n'
else
  printf '\nUpgrade did not complete. Send Alex the complete message above.\n' >&2
fi

if [ -t 0 ]; then
  printf '\nPress Return to close this window. '
  read -r TASK_REPLY || true
fi
exit "$TASK_RESULT"
