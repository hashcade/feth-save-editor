#!/usr/bin/env python3
"""Package the Windows Release build for CI downloads and GitHub Releases."""

from __future__ import annotations

import argparse
import hashlib
import re
import shutil
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
FILES = {
    "FETH_SaveEditor.exe": ROOT / "SaveEditor/bin/Release/FETH_SaveEditor.exe",
    "FETH_SaveEditor.exe.config": ROOT / "SaveEditor/bin/Release/FETH_SaveEditor.exe.config",
    "FETH_Cli.exe": ROOT / "FethCli/bin/Release/net472/FETH_Cli.exe",
    "DataUnpacker.exe": ROOT / "data-unpacker/bin/Release/DataUnpacker.exe",
    "DataUnpacker.exe.config": ROOT / "data-unpacker/bin/Release/DataUnpacker.exe.config",
    "README.md": ROOT / "README.md",
    "CLI.md": ROOT / "CLI.md",
}


def version() -> str:
    value = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?", value):
        raise ValueError(f"invalid VERSION: {value}")
    return value


def digest(path: Path) -> str:
    sha256 = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            sha256.update(chunk)
    return sha256.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "dist")
    args = parser.parse_args()

    for name, source in FILES.items():
        if not source.is_file() or source.stat().st_size == 0:
            raise FileNotFoundError(f"missing build file: {name} ({source})")

    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    editor = output / "FETH_SaveEditor.exe"
    package = output / f"fe3h-editor-v{version()}.zip"
    checksums = output / "SHA256SUMS"

    shutil.copy2(FILES["FETH_SaveEditor.exe"], editor)
    with zipfile.ZipFile(package, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, source in FILES.items():
            archive.write(source, name)

    with zipfile.ZipFile(package) as archive:
        if archive.testzip() is not None or set(archive.namelist()) != set(FILES):
            raise ValueError("release ZIP failed verification")

    checksums.write_text(
        "".join(f"{digest(path)}  {path.name}\n" for path in (editor, package)),
        encoding="utf-8",
    )
    for path in (editor, package, checksums):
        print(path)


if __name__ == "__main__":
    main()
