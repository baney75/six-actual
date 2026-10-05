#!/usr/bin/env bash
# One entry point for Six Actual verification.
#   scripts/verify.sh --quick | --phase N | --full
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
export PATH="${HOME}/.local/share/mise/shims:${PATH}"
if command -v mise >/dev/null 2>&1; then
  eval "$(mise activate bash --shims)"
fi

MODE="quick"
PHASE=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --quick) MODE="quick" ;;
    --full) MODE="full" ;;
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
REPORT="${REPORT_DIR}/${STAMP}-${MODE}${PHASE}.md"
LOG="${REPORT_DIR}/${STAMP}-${MODE}${PHASE}.log"
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
  if "$@" >>"${LOG}" 2>&1; then
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
  if grep -E 'ERROR:' "${import_log}" "${export_log}" >/dev/null; then
    echo "Godot import or export reported errors" >&2
    return 1
  fi
  [[ -f build/linux/SixActual.x86_64 && -f build/windows/SixActual.exe ]]
  [[ -f build/linux/data_SixActual_linuxbsd_x86_64/SixActual.Sim.dll ]]
  [[ -f build/windows/data_SixActual_windows_x86_64/SixActual.Sim.dll ]]
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
  mise exec -- uvx --from check-jsonschema check-jsonschema --schemafile data/schema/catalog.schema.json data/catalog.json
  python3 scripts/validate_data.py
}

gate_g11() {
  python3 scripts/check_licenses.py
}

gate_g12() {
  dotnet format SixActual.slnx --verify-no-changes --severity warn
  python3 scripts/check_code.py
}

gate_missing() {
  echo "$1 is not implemented yet" >&2
  return 1
}

should_run() {
  local id="$1"
  case "${MODE}" in
    quick) [[ " G1 G2 G3 G10 " == *" ${id} "* ]] ;;
    full) return 0 ;;
    phase)
      case "${PHASE}" in
        0) [[ " G1 G2 G3 G10 G11 G12 " == *" ${id} "* ]] ;;
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
if should_run G10; then run_gate G10 gate_g10; fi
if should_run G11; then run_gate G11 gate_g11; fi
if should_run G12; then run_gate G12 gate_g12; fi

if [[ "${MODE}" == "full" ]]; then
  for id in G4 G5 G6 G7 G8 G9 G13; do
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
