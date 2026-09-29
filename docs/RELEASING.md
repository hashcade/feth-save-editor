# Building and releasing

`VERSION` is this fork's release number. The supported game version is 1.2.0.

Every push to `main`, pull request, or manual run of the [build workflow](../.github/workflows/build.yml) builds and tests the GUI and CLI on Windows, macOS, and Linux. It then packages and tests a ZIP for each platform, with one shared, self-contained .NET runtime for both programs. The `feth-gui-*` and `feth-cli-*` build artifacts are retained for 14 days.

To publish after reviewing a successful build on all platforms:

1. Update `VERSION` in a Conventional Commit and push `main`.
2. Wait for the build workflow to pass for that commit.
3. Run `python3 tools/release.py --current --check` to check readiness.
4. Run `python3 tools/release.py --current` to create and push an annotated version tag.

The [release workflow](../.github/workflows/release.yml) builds and tests the tagged source on all four targets, publishes the platform ZIPs, then downloads and checks their SHA-256 hashes. The release script creates and pushes the version tag; the workflow creates the GitHub release. The script requires GitHub CLI authentication.
