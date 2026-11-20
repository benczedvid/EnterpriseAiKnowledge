#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

required_directories=(
  src/EnterpriseAI.Api
  src/EnterpriseAI.Application
  src/EnterpriseAI.Domain
  src/EnterpriseAI.Infrastructure
  src/EnterpriseAI.Web
  tests/EnterpriseAI.UnitTests
  tests/EnterpriseAI.IntegrationTests
  docker
  docs
  samples
)

required_files=(
  .editorconfig
  .gitignore
  Directory.Build.props
  EnterpriseAI.sln
  README.md
  docker-compose.yml
  global.json
)

for path in "${required_directories[@]}"; do
  [[ -d "$path" ]] || { echo "Missing directory: $path" >&2; exit 1; }
done

for path in "${required_files[@]}"; do
  [[ -f "$path" ]] || { echo "Missing file: $path" >&2; exit 1; }
done

grep -q '^Microsoft Visual Studio Solution File, Format Version 12.00$' EnterpriseAI.sln
grep -q '<TargetFramework>net8.0</TargetFramework>' Directory.Build.props
grep -q '"version": "8.0.100"' global.json

git rev-parse --is-inside-work-tree >/dev/null
test "$(git branch --show-current)" = "main"

echo "EAK-001 repository structure is valid."
