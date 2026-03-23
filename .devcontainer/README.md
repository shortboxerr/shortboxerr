# Development container

## GitHub CLI (`gh`)

The container installs the GitHub CLI. To use it for pushes and PRs without interactive login:

1. Create a [Personal Access Token](https://github.com/settings/tokens) with `repo` scope (or equivalent).
2. **Before opening or rebuilding the dev container**, set the token in your **host** environment so the container can use it:
   - **One-off:** In the terminal where you start Cursor / open the folder, run:
     ```bash
     export GH_TOKEN=your_token_here
     ```
   - **Persistent:** Add `export GH_TOKEN=your_token_here` to your shell profile (e.g. `~/.bashrc`). Do not commit this file if it’s in the repo.
3. Reopen in the container (**Dev Containers: Reopen in Container**). The `postCreateCommand` will run `gh auth login --with-token` when `GH_TOKEN` is set.

**Security:** Never commit the token or put it in any file under the repo. Use host environment or a local, uncommitted config only.
