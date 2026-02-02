# Persistent Repo Memory

- App name: Shortboxerr.
- Stack must be Sonarr/Radarr-like (.NET backend, Arr-style workflows).
- UI must adapt *arr UI patterns heavily.
- Behavior parity with Mylar3 is required, especially:
  - release selection logic (DecisionEngine)
  - media management (renaming, post-processing, failures)
- Collected editions (TPB/HC/Omnibus/etc.) are first-class.
- Variants are not a priority.
- Defaults should match Mylar3; if exact values unknown, choose reasonable defaults and mark TODO in docs/ASSUMPTIONS.md and tests.
- Use git and commit at logical breakpoints (granular history).
- Do all work inside the Dev Container.
