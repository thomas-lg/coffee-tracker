#!/usr/bin/env bash
# Runs once after the dev container is created (see devcontainer.json).
# Installs the toolchain the base image + features don't already provide, then
# prints a summary so the developer can confirm everything resolved.
set -euo pipefail

# EF Core CLI for migrations (dotnet ef ...). install fails if already present,
# so fall back to update to make this script idempotent across rebuilds.
dotnet tool install --global dotnet-ef || dotnet tool update --global dotnet-ef
export PATH="$PATH:$HOME/.dotnet/tools"

# Angular CLI matching the project's Angular 22.
npm install -g @angular/cli@22

echo "=== Coffee Tracker dev container toolchain ==="
dotnet --info | head -n 4
echo "dotnet-ef: $(dotnet ef --version)"
echo "node: $(node --version)  npm: $(npm --version)"
echo "ng: $(ng version --skip-confirmation 2>/dev/null | head -n 1 || ng --version 2>/dev/null | head -n 1)"
echo "tesseract langs: $(tesseract --list-langs 2>&1 | tr '\n' ' ')"
