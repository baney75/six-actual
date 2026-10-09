#!/usr/bin/env bash
# Idempotent project bootstrap. System packages are printed, not installed,
# unless SIX_ACTUAL_INSTALL_SYSTEM=1.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
# shellcheck disable=SC1091
source tools/versions.env

# Blender is not here: the pinned tarball goes to tools/blender/ (below), so
# `yay -Syu` cannot change the asset build. GPU drivers are never swapped.
SYSTEM_PACKAGES=(
  git git-lfs base-devel python uv ffmpeg sox imagemagick jq unzip p7zip
  vulkan-tools mesa-utils fastfetch grim slurp wl-clipboard libxkbcommon-x11
  noto-fonts openal cage
)

echo "System packages (one sudo prompt, run by the user):"
echo "  yay -S --needed ${SYSTEM_PACKAGES[*]}"
echo "  git lfs install"
if [[ "${SIX_ACTUAL_INSTALL_SYSTEM:-0}" == "1" ]]; then
  yay -S --needed "${SYSTEM_PACKAGES[@]}"
  git lfs install
fi

if command -v mise >/dev/null 2>&1; then
  mise install
  mise use -y "dotnet@${DOTNET_SDK}"
  mise use -y "uv@${UV_VERSION}"
fi

DOTNET_ROOT="${HOME}/.local/share/mise/dotnet-root"
if [[ ! -x "${DOTNET_ROOT}/sdk/${DOTNET_TARGETING_SDK}/dotnet.dll" && ! -d "${DOTNET_ROOT}/sdk/${DOTNET_TARGETING_SDK}" ]]; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --version "${DOTNET_TARGETING_SDK}" --install-dir "${DOTNET_ROOT}"
fi

mkdir -p tools/cache
ZIP="tools/cache/${GODOT_LINUX_ZIP}"
if [[ ! -f "${ZIP}" ]]; then
  curl -fL --retry 3 -o "${ZIP}" \
    "https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}-${GODOT_CHANNEL}/${GODOT_LINUX_ZIP}"
fi
echo "${GODOT_LINUX_SHA512}  ${ZIP}" | sha512sum -c -

if [[ ! -x tools/godot/godot ]]; then
  rm -rf tools/godot
  unzip -q -o "${ZIP}" -d tools/godot-unpacked
  mv tools/godot-unpacked/Godot_v"${GODOT_VERSION}"-stable_mono_linux_x86_64 tools/godot
  ln -s Godot_v"${GODOT_VERSION}"-stable_mono_linux.x86_64 tools/godot/godot
  rm -rf tools/godot-unpacked
fi

TPZ="tools/cache/${GODOT_TEMPLATES}"
if [[ ! -f "${TPZ}" ]]; then
  curl -fL --retry 3 -o "${TPZ}" \
    "https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}-${GODOT_CHANNEL}/${GODOT_TEMPLATES}"
fi
echo "${GODOT_TEMPLATES_SHA512}  ${TPZ}" | sha512sum -c -

TEMPLATE_DIR="${HOME}/.local/share/godot/export_templates/${GODOT_TEMPLATE_DIR}"
if [[ ! -d "${TEMPLATE_DIR}" ]]; then
  mkdir -p "${HOME}/.local/share/godot/export_templates"
  tmp="$(mktemp -d)"
  unzip -q "${TPZ}" -d "${tmp}"
  # The archive contains a templates/ directory.
  mv "${tmp}/templates" "${TEMPLATE_DIR}"
  rm -rf "${tmp}"
fi

BLENDER_XZ="tools/cache/${BLENDER_TARBALL}"
if [[ ! -f "${BLENDER_XZ}" ]]; then
  curl -fL --retry 3 -o "${BLENDER_XZ}" "${BLENDER_URL}"
fi
echo "${BLENDER_SHA256}  ${BLENDER_XZ}" | sha256sum -c -
if [[ "$(tools/blender/blender --version 2>/dev/null | head -1)" != "Blender ${BLENDER_VERSION}"* ]]; then
  rm -rf tools/blender tools/blender-unpacked
  mkdir -p tools/blender-unpacked
  tar -xJf "${BLENDER_XZ}" -C tools/blender-unpacked
  mv tools/blender-unpacked/blender-"${BLENDER_VERSION}"-linux-x64 tools/blender
  rm -rf tools/blender-unpacked
fi

git config core.hooksPath scripts/githooks
python3 scripts/generate_catalog.py
echo "bootstrap complete"
tools/godot/godot --version
tools/blender/blender --version | head -1
mise exec -- dotnet --version
