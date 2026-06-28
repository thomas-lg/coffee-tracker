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

# If a GitHub token was provided via the host's GH_TOKEN (see devcontainer.json),
# gh is already authenticated through the env var. Wire it into git so `git push`
# over HTTPS uses the same credentials. No token => skip, leaving git untouched.
if [ -n "${GH_TOKEN:-}" ]; then
  gh auth setup-git
  echo "gh: authenticated via GH_TOKEN ($(gh api user --jq .login 2>/dev/null || echo '?'))"
else
  echo "gh: no GH_TOKEN set on host — run 'gh auth login' manually if needed"
fi

echo "=== Coffee Tracker dev container toolchain ==="
dotnet --info | head -n 4
echo "dotnet-ef: $(dotnet ef --version)"
echo "node: $(node --version)  npm: $(npm --version)"
echo "gh: $(gh --version | head -n 1)"
echo "ng: $(ng version --skip-confirmation 2>/dev/null | head -n 1 || ng --version 2>/dev/null | head -n 1)"
echo "tesseract langs: $(tesseract --list-langs 2>&1 | tr '\n' ' ')"
