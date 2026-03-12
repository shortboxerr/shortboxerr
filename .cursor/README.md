# Cursor configuration for Shortboxerr

## MCP (Model Context Protocol)

### GitHub MCP server

The project configures the [GitHub MCP server](https://github.com/modelcontextprotocol/servers/tree/main/src/github) in `.cursor/mcp.json` for the **shortboxerr/shortboxerr** repo (shortboxerr org).

**Setup:** Set a GitHub Personal Access Token so the MCP can access the repo:

1. Create a token at [GitHub → Settings → Developer settings → Personal access tokens](https://github.com/settings/tokens) with `repo` scope (or fine-grained with repository access to `shortboxerr/shortboxerr`).
2. In Cursor: **Settings → MCP → github → Edit → Environment** and add:
   - `GITHUB_PERSONAL_ACCESS_TOKEN` = your token

Alternatively, set `GITHUB_PERSONAL_ACCESS_TOKEN` in your shell environment before starting Cursor.

**Security:** Do not commit the token to the repo. Use Cursor’s MCP env or your local environment only.
