#!/usr/bin/env python3
"""Extract a platform ZIP and test its GUI entry point and CLI save editing."""

import argparse
import json
import os
import plistlib
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("rid", choices=("win-x64", "osx-arm64", "osx-x64", "linux-x64"))
    parser.add_argument("archive", nargs="?", type=Path)
    args = parser.parse_args()
    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    archive = args.archive or ROOT / "dist" / f"feth-save-editor-v{version}-{args.rid}.zip"

    with zipfile.ZipFile(archive) as package:
        if package.testzip() is not None:
            raise ValueError("Release ZIP is corrupt")
        names = package.namelist()
        if any(name.endswith(".pdb") for name in names):
            raise ValueError("Release ZIP contains debug symbols")
        with tempfile.TemporaryDirectory(prefix="feth-package-test-") as temporary:
            extracted = Path(temporary)
            if args.rid.startswith("osx-"):
                subprocess.run(["ditto", "-x", "-k", str(archive), str(extracted)], check=True)
                app = extracted / "FETH Save Editor.app"
                gui = app / "Contents/MacOS/FethEditor.Gui"
                cli = extracted / "Cli/FethEditor.Cli"
                with (app / "Contents/Info.plist").open("rb") as source:
                    info = plistlib.load(source)
                if info["CFBundleExecutable"] != gui.name:
                    raise ValueError("macOS app does not point to the GUI")
                if not (app / "Contents/Resources/Sothis.icns").is_file():
                    raise ValueError("macOS app icon is missing")
                subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
            else:
                package.extractall(extracted)
                if args.rid == "win-x64":
                    gui = extracted / "FETH Save Editor.exe"
                    cli = extracted / "FethEditor.Cli.exe"
                    if gui.read_bytes()[:2] != b"MZ":
                        raise ValueError("Windows GUI is not a PE executable")
                else:
                    gui = extracted / "FethEditor.Gui"
                    cli = extracted / "FethEditor.Cli"
                    for executable in (gui, cli):
                        mode = package.getinfo(executable.name).external_attr >> 16
                        if not mode & 0o111:
                            raise ValueError(f"ZIP lost executable permission: {executable.name}")
                        os.chmod(executable, mode)
            for config in extracted.rglob("*.runtimeconfig.json"):
                properties = json.loads(config.read_text(encoding="utf-8"))["runtimeOptions"]["configProperties"]
                if properties.get("System.Runtime.InteropServices.BuiltInComInterop.IsSupported", True):
                    raise ValueError(f"Shared trimmed runtime requires COM interop to be disabled: {config.name}")
            for executable in (gui, cli):
                if not executable.is_file():
                    raise ValueError(f"Release entry point is missing: {executable}")
            subprocess.run(
                [sys.executable, str(ROOT / "tools/test_cli.py"), "--cli", str(cli)],
                check=True,
            )
    print(f"Release package passed: {args.rid}")


if __name__ == "__main__":
    main()
