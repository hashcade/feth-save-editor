#!/usr/bin/env python3
"""Exercise the CLI against a synthetic v23 save without publishing user data."""

from __future__ import annotations

import argparse
import json
import struct
import subprocess
import tempfile
from pathlib import Path


SAVE_SIZE = 0x25B20 + 12


def checksum(data: bytes) -> int:
    return sum(data[12:]) & 0xFFFFFFFF


def run(cli: Path, *args: str, success: bool = True) -> dict:
    result = subprocess.run([str(cli), *args], capture_output=True, text=True)
    if (result.returncode == 0) != success:
        raise AssertionError((result.returncode, result.stdout, result.stderr))
    return json.loads(result.stdout if success else result.stderr)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--cli", type=Path, required=True)
    cli = parser.parse_args().cli.resolve()

    with tempfile.TemporaryDirectory() as temporary:
        directory = Path(temporary)
        source = directory / "slot00"
        target = directory / "edited-slot00"
        patch_file = directory / "patch.json"
        raw = bytearray(SAVE_SIZE)
        struct.pack_into("<II", raw, 4, 23, SAVE_SIZE)
        for slot in range(400):
            struct.pack_into("<h", raw, 12 + slot * 4, -1)
        sentinel = 12 + 0x644 + 0x2A
        raw[sentinel] = 0xA5
        struct.pack_into("<I", raw, 0, checksum(raw))
        source.write_bytes(raw)

        summary = run(cli, "inspect", "--input", str(source), "--section", "summary")
        assert summary["summary"]["Money"] == 0
        assert run(cli, "get", "--input", str(source), "--path", "Items[0].Id")["value"] == -1
        catalog = run(cli, "catalog", "--type", "classes")
        assert len(catalog) == 100

        patch_file.write_text(json.dumps({
            "expectedSha256": summary["sha256"],
            "operations": [
                {"op": "set", "path": "Player.Money", "value": 12345},
                {"op": "set", "path": "Activities.Reputation", "value": 321},
                {"op": "set", "path": "Characters[0].data.SkillExp[0]", "value": 40},
                {"op": "setBit", "path": "Characters[0].data.Abilities", "index": 3, "value": True},
                {"op": "set", "path": "Items[0].Id", "value": 65},
            ],
        }), encoding="utf-8")
        dry = run(cli, "apply", "--input", str(source), "--patch", str(patch_file), "--dry-run")
        assert dry["changedBytes"] > 0
        assert not target.exists()

        applied = run(cli, "apply", "--input", str(source), "--patch", str(patch_file), "--output", str(target))
        assert applied["changedBytes"] > 0
        edited = target.read_bytes()
        assert source.read_bytes() == raw
        assert edited[sentinel] == 0xA5
        assert struct.unpack_from("<I", edited, 0)[0] == checksum(edited)
        assert struct.unpack_from("<I", edited, 12 + 0x640)[0] == 1
        assert struct.unpack_from("<h", edited, 12)[0] == 65
        assert struct.unpack_from("<H", edited, 12 + 0x644 + 0x32)[0] == 40
        assert struct.unpack_from("<H", edited, 12 + 0x644 + 0xFC)[0] == 40
        assert edited[12 + 0x644 + 0x61] & 8
        assert struct.unpack_from("<I", edited, 12 + 0x231D9 + 0x1074)[0] == 12345
        assert struct.unpack_from("<I", edited, 12 + 0x250A1 + 0xC)[0] == 321

        bad_patch = directory / "bad.json"
        bad_patch.write_text(json.dumps({"operations": [
            {"op": "set", "path": "Player.field_17D8[0]", "value": 99},
        ]}), encoding="utf-8")
        error = run(cli, "apply", "--input", str(source), "--patch", str(bad_patch),
                    "--output", str(directory / "should-not-exist"), success=False)
        assert "not writable" in error["error"]
        assert not (directory / "should-not-exist").exists()

        suspend = directory / "suspend"
        suspend.write_bytes(raw + b"battle-state")
        error = run(cli, "inspect", "--input", str(suspend), success=False)
        assert "Suspend" in error["error"]

        print("CLI save inspection, patching, byte preservation, and rejection checks passed.")


if __name__ == "__main__":
    main()
