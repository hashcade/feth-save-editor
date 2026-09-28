# Building and releasing

`VERSION` is this fork's release number. The supported game version is 1.2.0.

Every push to `main`, pull request, or manual run of the [build workflow](../.github/workflows/build.yml) builds the new GUI and CLI for Windows, macOS, and Linux, plus the legacy .NET Framework 4.7.2 projects on Windows. The `feth-gui-*` and `feth-cli-*` artifacts contain standalone builds. The `fe3h-editor-windows` artifact contains the original save editor, a ZIP with both legacy tools and their config files, and SHA-256 checksums. Artifacts are retained for 14 days.

To publish after reviewing a successful Windows build:

1. Update `VERSION` in a Conventional Commit and push `main`.
2. Wait for the build workflow to pass for that commit.
3. Run `python3 tools/release.py --current --check` to check readiness.
4. Run `python3 tools/release.py --current` to create and push an annotated version tag.

The [release workflow](../.github/workflows/release.yml) builds the tagged source on Windows, packages the same files, publishes a prerelease, then downloads and checks the published files. The release script does not create a release until the version tag is pushed. It requires GitHub CLI authentication.
