#!/usr/bin/env bash
# Stop hook: block the stop while `scripts/verify.sh --quick` is red, unless
# docs/STATE.md records the failure on a `KNOWN-RED:` line. Skips the run when
# the tree fingerprint matches the last green run.
set -uo pipefail
ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"
cd "$ROOT" || exit 0
cat >/dev/null  # hook input is not needed

fingerprint() {
  { git rev-parse HEAD 2>/dev/null; git status --porcelain=v1 -uall 2>/dev/null; git diff HEAD 2>/dev/null
    git ls-files --others --exclude-standard -z 2>/dev/null | xargs -0 -r sha256sum 2>/dev/null; } | sha256sum | cut -d' ' -f1
}
STAMP_FILE="build/.last-quick-green"
fp="$(fingerprint)"
if [[ -f "$STAMP_FILE" && "$(cat "$STAMP_FILE")" == "$fp" ]]; then
  exit 0
fi
if out="$(scripts/verify.sh --quick 2>&1)"; then
  mkdir -p build && printf '%s' "$(fingerprint)" > "$STAMP_FILE"
  exit 0
fi
report="$(grep -o 'report: .*' <<<"$out" | tail -1)"
if grep -q '^KNOWN-RED:' docs/STATE.md 2>/dev/null; then
  echo "verify --quick is red (${report}); allowed because docs/STATE.md has a KNOWN-RED line." >&2
  exit 0
fi
{
  echo "scripts/verify.sh --quick is red. ${report}"
  grep -E '^G[0-9]+: FAIL' <<<"$out"
  echo "Fix it, or record it in docs/STATE.md as a 'KNOWN-RED: <gate> <reason>' line under Blockers before stopping."
} >&2
exit 2
