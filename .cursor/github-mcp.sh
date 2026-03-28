#!/usr/bin/env bash
# Resolves GITHUB_PERSONAL_ACCESS_TOKEN for @modelcontextprotocol/server-github.
# Cursor MCP often does not inherit devcontainer remoteEnv; this wrapper covers:
#   1) GITHUB_PERSONAL_ACCESS_TOKEN / GH_TOKEN (if present in the MCP process env)
#   2) gh auth token (after postCreate gh auth login or interactive gh auth login)
#   3) .devcontainer/local-secrets/github_token (one line, gitignored)

set -euo pipefail

if [[ -z "${GITHUB_PERSONAL_ACCESS_TOKEN:-}" && -n "${GH_TOKEN:-}" ]]; then
  export GITHUB_PERSONAL_ACCESS_TOKEN="$GH_TOKEN"
fi

if [[ -z "${GITHUB_PERSONAL_ACCESS_TOKEN:-}" ]] && command -v gh >/dev/null 2>&1; then
  if t="$(gh auth token 2>/dev/null)" && [[ -n "$t" ]]; then
    export GITHUB_PERSONAL_ACCESS_TOKEN="$t"
  fi
fi

if [[ -z "${GITHUB_PERSONAL_ACCESS_TOKEN:-}" ]]; then
  _here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  _secret="${GITHUB_TOKEN_FILE:-${_here}/../.devcontainer/local-secrets/github_token}"
  if [[ -f "$_secret" ]]; then
    export GITHUB_PERSONAL_ACCESS_TOKEN="$(tr -d '\r\n' < "$_secret" | head -c 8192)"
  fi
fi

if [[ -z "${GITHUB_PERSONAL_ACCESS_TOKEN:-}" ]]; then
  echo "github-mcp.sh: No GitHub token. Options:" >&2
  echo "  - Rebuild the dev container with GITHUB_PERSONAL_ACCESS_TOKEN or GH_TOKEN set on the host (terminal that launches Cursor, or same user env)." >&2
  echo "  - In the container: gh auth login (then restart the GitHub MCP server)." >&2
  echo "  - Create .devcontainer/local-secrets/github_token with one line (PAT); file is gitignored." >&2
  exit 1
fi

exec npx -y @modelcontextprotocol/server-github
