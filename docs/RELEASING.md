# Building and releasing

`VERSION` is this fork's release number. The supported game version is 1.2.0.

Every push to `main`, pull request, or manual run of the [build workflow](../.github/workflows/build.yml) builds and tests the GUI and CLI on Windows, macOS, and Linux. It then packages and tests a ZIP for each platform, with one shared, self-contained .NET runtime for both programs. The `feth-gui-*` and `feth-cli-*` build artifacts are retained for 14 days.

To publish from a clean, synchronized `main` branch, run `python3 tools/release.py` and choose patch, minor, or major. You can also pass the type directly, for example `python3 tools/release.py patch`. The script builds and tests locally, updates `VERSION`, creates a `chore: release vX.Y.Z` commit, pushes `main`, then creates and pushes an annotated tag. Use `--check` to preview without publishing.

For the already committed `0.2.1` version only, `python3 tools/release.py --current` tags the prepared commit after its build workflow succeeds. Do not use `--current` for subsequent releases.

The [release workflow](../.github/workflows/release.yml) builds and tests the tagged source on all four targets, publishes the platform ZIPs, then downloads and checks their SHA-256 hashes. The workflow creates the GitHub release. The `--current` migration path requires GitHub CLI authentication.
