# Cursor configuration for Shortboxerr

## MCP (Model Context Protocol)

### GitHub MCP server

The project configures the [GitHub MCP server](https://github.com/modelcontextprotocol/servers/tree/main/src/github) in `.cursor/mcp.json` for the **shortboxerr/shortboxerr** repo (shortboxerr org).

**Run Cursor in the dev container:** The GitHub MCP server is started with `npx`; that only works when Cursor is running inside the development container (where Node/npx is installed). Open the project in the container: **Command Palette** (Ctrl+Shift+P / Cmd+Shift+P) → **Dev Containers: Reopen in Container**. After the container builds and the window reloads, MCP and other tools use the container’s environment.

**Setup:** Set a GitHub Personal Access Token so the MCP can access the repo:

1. Create a token at [GitHub → Settings → Developer settings → Personal access tokens](https://github.com/settings/tokens) with `repo` scope (or fine-grained with repository access to `shortboxerr/shortboxerr`).
2. In Cursor: **Settings → Tools & MCP** (Cmd+Shift+J / Ctrl+Shift+J) → find **github** → **Edit**. The editor opens the MCP JSON config. Add an `env` object to the `github` server with your token, for example:

   ```json
   "github": {
     "command": "npx",
     "args": ["-y", "@modelcontextprotocol/server-github"],
     "env": {
       "GITHUB_PERSONAL_ACCESS_TOKEN": "your-token-here"
     }
   }
   ```

   **Important:** The project’s `.cursor/mcp.json` does not (and must not) contain the token. Put the token only in your **global** config so it is never committed: edit `~/.cursor/mcp.json` and add the `env` block there, or add a `github` entry with `env` in that file. Cursor merges project and global config; the token in global will be used when the server runs.

Alternatively, set `GITHUB_PERSONAL_ACCESS_TOKEN` in your shell environment and start Cursor from that shell so the MCP server inherits it (no need to put the token in any JSON file).

**Security:** Do not commit the token. If a token was ever committed, revoke it at [GitHub → Settings → Developer settings → Personal access tokens](https://github.com/settings/tokens) and create a new one.

### Troubleshooting GitHub MCP

- **Restart Cursor** after changing `mcp.json` or env; MCP config is loaded at startup.
- **`spawn npx ENOENT`**: Cursor is not running in the dev container, so `npx` isn’t on the PATH. Use **Dev Containers: Reopen in Container** so the workspace (and MCP) runs inside the container where Node/npx is available.
- **Project config not loaded**: Some Cursor versions don’t pick up `.cursor/mcp.json`. Add the same `github` server to **global** config: `~/.cursor/mcp.json` (merge into existing `mcpServers` if needed). You can put `GITHUB_PERSONAL_ACCESS_TOKEN` in that file’s `env` only if you do **not** commit it (keep `~/.cursor/mcp.json` local).
- **Logs**: **View → Output**, then choose **MCP Logs** to see why the server might be failing.
- **Toggle**: In **Settings → Tools & MCP**, ensure the **github** server is enabled (toggle on).
