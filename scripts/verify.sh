#!/usr/bin/env bash
# One entry point for Six Actual verification.
#   scripts/verify.sh --quick | --phase N | --full | --gates G2,G4,...
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
export PATH="${HOME}/.local/share/mise/shims:${PATH}"
if command -v mise >/dev/null 2>&1; then
  eval "$(mise activate bash --shims)"
fi

MODE="quick"
PHASE=""
GATES=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --quick) MODE="quick" ;;
    --full) MODE="full" ;;
    --gates)
      MODE="gates"
      GATES=" ${2//,/ } "
      shift
      ;;
    --phase)
      MODE="phase"
      PHASE="${2:?phase number}"
      shift
      ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
REPORT_DIR="${ROOT}/build/reports"
mkdir -p "${REPORT_DIR}"
SUFFIX="${PHASE}$(tr -d ' ' <<<"${GATES//G/-G}")"
REPORT="${REPORT_DIR}/${STAMP}-${MODE}${SUFFIX}.md"
LOG="${REPORT_DIR}/${STAMP}-${MODE}${SUFFIX}.log"
: > "${LOG}"

FAILURES=0
ran_gates=""

note() { printf '%s\n' "$1" | tee -a "${LOG}"; }

run_gate() {
  local id="$1"
  shift
  note ""
  note "## ${id}"
  ran_gates="${ran_gates} ${id}"
  # errexit in a subshell outside any `if`: every command in the gate must succeed, not just its last one
  # (D058: G2 reported pass while dotnet test failed, because the coverage check ran last).
  # Plain statement, not part of `||`: bash ignores errexit inside an AND-OR list, subshells included.
  ( set -e; "$@" ) >>"${LOG}" 2>&1
  local rc=$?
  if [[ ${rc} -eq 0 ]]; then
    note "${id}: pass"
  else
    note "${id}: FAIL"
    FAILURES=$((FAILURES + 1))
  fi
}

gate_g1() {
  dotnet build SixActual.slnx -c Release -warnaserror
  local godot="${ROOT}/tools/godot/godot"
  if [[ ! -x "${godot}" ]]; then
    echo "Godot binary missing at tools/godot/godot" >&2
    return 1
  fi
  local import_log="${ROOT}/build/reports/godot-import.log"
  if ! "${godot}" --headless --path game --import >"${import_log}" 2>&1; then
    echo "Godot import failed" >&2
    return 1
  fi
  if grep -E 'ERROR:' "${import_log}" >/dev/null; then
    echo "Godot import reported errors" >&2
    return 1
  fi
  mkdir -p build/linux build/windows build/reports
  local export_log="${ROOT}/build/reports/godot-export.log"
  : > "${export_log}"
  if ! "${godot}" --headless --path game --export-release "Linux" "${ROOT}/build/linux/SixActual.x86_64" >>"${export_log}" 2>&1; then
    echo "Linux export failed" >&2
    return 1
  fi
  if ! "${godot}" --headless --path game --export-release "Windows" "${ROOT}/build/windows/SixActual.exe" >>"${export_log}" 2>&1; then
    echo "Windows export failed" >&2
    return 1
  fi
  mkdir -p build/macos
  rm -f build/macos/SixActual.zip
  if ! "${godot}" --headless --path game --export-release "macOS" "${ROOT}/build/macos/SixActual.zip" >>"${export_log}" 2>&1; then
    echo "macOS export failed" >&2
    return 1
  fi
  if grep -E 'ERROR:' "${import_log}" "${export_log}" >/dev/null; then
    echo "Godot import or export reported errors" >&2
    return 1
  fi
  [[ -f build/linux/SixActual.x86_64 && -f build/windows/SixActual.exe ]]
  [[ -f build/linux/data_SixActual_linuxbsd_x86_64/SixActual.Sim.dll ]]
  [[ -f build/windows/data_SixActual_windows_x86_64/SixActual.Sim.dll ]]
  local mac_listing
  mac_listing="$(unzip -l build/macos/SixActual.zip)"
  grep -q 'Contents/MacOS/Six Actual$' <<<"${mac_listing}"
  grep -q 'data_SixActual_macos_arm64/SixActual.Sim.dll' <<<"${mac_listing}"
  grep -q 'data_SixActual_macos_x86_64/SixActual.Sim.dll' <<<"${mac_listing}"
}

gate_g2() {
  rm -rf build/coverage
  dotnet test sim.Tests/SixActual.Sim.Tests.csproj -c Release --collect:"XPlat Code Coverage" --results-directory build/coverage
  python3 scripts/check_coverage.py
}

gate_g3() {
  local first second
  first="$(dotnet run --project sim/Headless/SixActual.Headless.csproj -c Release -- --determinism)"
  printf '%s\n' "${first}"
  second="$(dotnet run --project sim/Headless/SixActual.Headless.csproj -c Release -- --determinism)"
  if [[ "${first}" != "${second}" ]]; then
    echo "determinism reports differ across processes" >&2
    return 1
  fi
  local golden="verify/golden/determinism.txt"
  if [[ ! -f "${golden}" ]]; then
    echo "missing ${golden}" >&2
    return 1
  fi
  local expected
  expected="$(tr -d '\r' < "${golden}")"
  expected="${expected%$'\n'}"
  if [[ "${first}" != "${expected}" ]]; then
    echo "determinism report does not match golden hashes" >&2
    printf '%s\n' "${first}" > build/reports/determinism-actual.txt
    return 1
  fi
  dotnet run --project sim/Headless/SixActual.Headless.csproj -c Release -- --selftest
}

gate_g10() {
  python3 scripts/generate_catalog.py build/catalog.generated.json
  if ! cmp -s data/catalog.json build/catalog.generated.json; then
    echo "data/catalog.json does not match scripts/generate_catalog.py" >&2
    return 1
  fi
  python3 scripts/generate_units.py build/units.generated.json
  if ! cmp -s data/units.json build/units.generated.json; then
    echo "data/units.json does not match scripts/generate_units.py" >&2
    return 1
  fi
  if ! cmp -s game/data/units.json build/units.generated.json; then
    echo "game/data/units.json does not match scripts/generate_units.py" >&2
    return 1
  fi
  mise exec -- uvx --from check-jsonschema check-jsonschema --schemafile data/schema/catalog.schema.json data/catalog.json
  mise exec -- uvx --from check-jsonschema check-jsonschema --schemafile data/schema/units.schema.json data/units.json
  python3 scripts/validate_data.py
  python3 scripts/generate_rules.py build/rules.generated.json
  if ! cmp -s data/rules.json build/rules.generated.json; then
    echo "data/rules.json does not match scripts/generate_rules.py" >&2
    return 1
  fi
  python3 scripts/generate_roster.py build/roster.generated.json build/weapons.generated.json
  if ! cmp -s data/roster.json build/roster.generated.json || ! cmp -s data/weapons.json build/weapons.generated.json; then
    echo "data/roster.json or data/weapons.json does not match scripts/generate_roster.py" >&2
    return 1
  fi
  python3 scripts/generate_support.py build/support.generated.json
  if ! cmp -s data/support.json build/support.generated.json; then
    echo "data/support.json does not match scripts/generate_support.py" >&2
    return 1
  fi
  python3 scripts/generate_theaters.py build/theaters.generated.json
  if ! cmp -s data/theaters.json build/theaters.generated.json; then
    echo "data/theaters.json does not match scripts/generate_theaters.py" >&2
    return 1
  fi
  python3 scripts/generate_operation_events.py build/operation_events.generated.json
  if ! cmp -s data/operation_events.json build/operation_events.generated.json; then
    echo "data/operation_events.json does not match scripts/generate_operation_events.py" >&2
    return 1
  fi
  python3 scripts/generate_field_requests.py build/field_requests.generated.json
  if ! cmp -s data/field_requests.json build/field_requests.generated.json; then
    echo "data/field_requests.json does not match scripts/generate_field_requests.py" >&2
    return 1
  fi
  python3 scripts/balance/cost_model.py build/decks.generated.json
  if ! cmp -s data/decks.json build/decks.generated.json; then
    echo "data/decks.json does not match scripts/balance/cost_model.py" >&2
    return 1
  fi
  python3 scripts/check_decks.py
  python3 scripts/check_era.py
  python3 scripts/generate_campaign.py --check
  python3 scripts/sync_game_data.py --check
  python3 scripts/seed_requirements.py --check
  python3 scripts/assets/seed_registry.py --check
}

gate_g11() {
  python3 scripts/check_licenses.py
}

gate_g12() {
  dotnet format SixActual.slnx --verify-no-changes --severity warn
  python3 scripts/check_code.py
}

gate_g4() {
  python3 scripts/check_scenarios.py
}

gate_g5() {
  python3 scripts/analyze_soak.py
}

gate_g6() {
  python3 scripts/analyze_perf.py
}

gate_g9() {
  # G9 audio (D066): the dialogue matrix is current, every trigger and variant has a file, loudness, peak, edge
  # silence, duration and transcription targets hold (scripts/check_audio.py against audio/manifest.json).
  python3 scripts/generate_dialogue.py --check
  python3 scripts/check_audio.py
}

gate_g13() {
  # G13 UX smoke (D060): a fresh Linux export, then the recorded input scripts in verify/ux/ against it.
  local godot="${ROOT}/tools/godot/godot"
  mkdir -p build/linux build/reports
  local export_log="${ROOT}/build/reports/godot-export-g13.log"
  "${godot}" --headless --path game --import >"${export_log}" 2>&1
  "${godot}" --headless --path game --export-release "Linux" "${ROOT}/build/linux/SixActual.x86_64" >>"${export_log}" 2>&1
  if grep -E 'ERROR:|Exception' "${export_log}" >/dev/null; then
    echo "Godot export for G13 reported errors (see build/reports/godot-export-g13.log)" >&2
    return 1
  fi
  python3 scripts/check_ux.py
}

gate_g14() {
  python3 scripts/refs/check_coverage.py
}

gate_missing() {
  echo "$1 is not implemented yet" >&2
  return 1
}

should_run() {
  local id="$1"
  case "${MODE}" in
    quick) [[ " G1 G2 G3 G10 G14 " == *" ${id} "* ]] ;;
    full) return 0 ;;
    gates) [[ "${GATES}" == *" ${id} "* ]] ;;
    phase)
      case "${PHASE}" in
        0) [[ " G1 G2 G3 G10 G11 G12 G14 " == *" ${id} "* ]] ;;
        1) [[ " G1 G2 G3 G4 G5 G6 G7 G8 G9 G10 G11 G12 G13 G14 G15 " == *" ${id} "* ]] ;;
        *) echo "phase ${PHASE} gate map is not defined" >&2; return 1 ;;
      esac
      ;;
  esac
}

{
  echo "# Verify ${MODE}${PHASE}"
  echo
  echo "UTC: ${STAMP}"
  echo
  echo '```'
  sha256sum verify/thresholds.toml
  find verify -type f ! -path 'verify/golden/*' -print0 | sort -z | xargs -0 sha256sum
  echo '```'
} > "${REPORT}"

if should_run G1; then run_gate G1 gate_g1; fi
if should_run G2; then run_gate G2 gate_g2; fi
if should_run G3; then run_gate G3 gate_g3; fi
if should_run G4; then run_gate G4 gate_g4; fi
if should_run G5; then run_gate G5 gate_g5; fi
if should_run G6; then run_gate G6 gate_g6; fi
if should_run G9; then run_gate G9 gate_g9; fi
if should_run G10; then run_gate G10 gate_g10; fi
if should_run G11; then run_gate G11 gate_g11; fi
if should_run G12; then run_gate G12 gate_g12; fi
if should_run G13; then run_gate G13 gate_g13; fi
if should_run G14; then run_gate G14 gate_g14; fi

if [[ "${MODE}" == "full" || ( "${MODE}" == "phase" && "${PHASE}" != "0" ) ]]; then
  for id in G7 G8 G15; do
    run_gate "${id}" gate_missing "${id}"
  done
fi

{
  echo
  echo "## Result"
  echo
  echo "Gates:${ran_gates}"
  echo "Failures: ${FAILURES}"
  echo "Log: ${LOG}"
} >> "${REPORT}"

{
  echo
  echo "## Log"
  echo
  echo '```'
  cat "${LOG}"
  echo '```'
} >> "${REPORT}"

echo "report: ${REPORT}"
echo "failures: ${FAILURES}"
exit $(( FAILURES > 0 ? 1 : 0 ))
