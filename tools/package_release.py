#!/usr/bin/env python3
"""Package self-contained GUI and CLI builds for a GitHub release."""

import argparse
import os
import plistlib
import shutil
import subprocess
import tempfile
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
APP_NAME = "FETH Save Editor.app"


def make_macos_app(gui: Path, destination: Path, version: str) -> None:
    contents = destination / "Contents"
    executable_dir = contents / "MacOS"
    resources = contents / "Resources"
    executable_dir.mkdir(parents=True)
    gui_binary = executable_dir / "FethEditor.Gui"
    shutil.copy2(gui / "FethEditor.Gui", gui_binary)
    os.chmod(gui_binary, gui_binary.stat().st_mode | 0o111)
    resources.mkdir()

    iconset = contents / "Sothis.iconset"
    iconset.mkdir()
    portrait = ROOT / "Gui/Assets/sothis-portrait.png"
    for name, pixels in (
        ("icon_16x16", 16),
        ("icon_16x16@2x", 32),
        ("icon_32x32", 32),
        ("icon_32x32@2x", 64),
        ("icon_128x128", 128),
        ("icon_128x128@2x", 256),
        ("icon_256x256", 256),
        ("icon_256x256@2x", 512),
        ("icon_512x512", 512),
        ("icon_512x512@2x", 1024),
    ):
        subprocess.run(
            ["sips", "-z", str(pixels), str(pixels), str(portrait),
             "--out", str(iconset / f"{name}.png")],
            check=True,
            capture_output=True,
        )
    subprocess.run(
        ["iconutil", "-c", "icns", str(iconset),
         "-o", str(resources / "Sothis.icns")],
        check=True,
    )
    shutil.rmtree(iconset)

    with (contents / "Info.plist").open("wb") as output:
        plistlib.dump(
            {
                "CFBundleDevelopmentRegion": "en",
                "CFBundleDisplayName": "FETH Save Editor",
                "CFBundleExecutable": "FethEditor.Gui",
                "CFBundleIconFile": "Sothis.icns",
                "CFBundleIdentifier": "com.jinghaihan.feth-save-editor",
                "CFBundleName": "FETH Save Editor",
                "CFBundlePackageType": "APPL",
                "CFBundleShortVersionString": version,
                "CFBundleVersion": version,
                "NSHighResolutionCapable": True,
            },
            output,
        )
    subprocess.run(
        ["codesign", "--force", "--deep", "--sign", "-", "--timestamp=none",
         str(destination)],
        check=True,
    )
    subprocess.run(
        ["codesign", "--verify", "--deep", "--strict", str(destination)],
        check=True,
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("rid", choices=("win-x64", "osx-arm64", "osx-x64", "linux-x64"))
    parser.add_argument("--gui", type=Path)
    parser.add_argument("--cli", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    gui = args.gui or ROOT / "dist/gui" / args.rid
    cli = args.cli or ROOT / "dist/cli" / args.rid
    output = args.output or ROOT / "dist" / f"feth-save-editor-v{version}-{args.rid}.zip"
    suffix = ".exe" if args.rid == "win-x64" else ""
    for executable in (gui / f"FethEditor.Gui{suffix}", cli / f"FethEditor.Cli{suffix}"):
        if not executable.is_file():
            parser.error(f"Missing published executable: {executable}")

    with tempfile.TemporaryDirectory(prefix="feth-release-") as temporary:
        staging = Path(temporary)
        if args.rid.startswith("osx-"):
            make_macos_app(gui, staging / APP_NAME, version)
            gui_binary = staging / APP_NAME / "Contents/MacOS/FethEditor.Gui"
        else:
            (staging / "Gui").mkdir()
            gui_binary = staging / "Gui" / f"FethEditor.Gui{suffix}"
            shutil.copy2(gui / gui_binary.name, gui_binary)
        (staging / "Cli").mkdir()
        cli_binary = staging / "Cli" / f"FethEditor.Cli{suffix}"
        shutil.copy2(cli / cli_binary.name, cli_binary)
        if args.rid == "linux-x64":
            os.chmod(gui_binary, gui_binary.stat().st_mode | 0o111)
        if args.rid != "win-x64":
            os.chmod(cli_binary, cli_binary.stat().st_mode | 0o111)

        output.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
            for path in sorted(staging.rglob("*")):
                archive.write(path, path.relative_to(staging))
        with zipfile.ZipFile(output) as archive:
            expected = [
                gui_binary.relative_to(staging).as_posix(),
                cli_binary.relative_to(staging).as_posix(),
            ]
            for name in expected:
                info = archive.getinfo(name)
                if args.rid != "win-x64" and not (info.external_attr >> 16) & 0o111:
                    raise RuntimeError(f"Executable permission missing from archive: {name}")
        print(output)


if __name__ == "__main__":
    main()
