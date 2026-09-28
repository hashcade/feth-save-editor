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
        player = 12 + 0x231D9
        struct.pack_into("<H", raw, player + 0x1576, 1001)
        raw[player + 0x17CE] = 9
        raw[player + 0x17D8] = 11
        raw[player + 0x19CC + 42 // 8] |= 1 << (42 % 8)
        # Records 35-44 use a packed 100-bit class set, unlike the first 35.
        extra_class_bit = (38 - 35) * 100 + 42
        raw[player + 0x1B93 + extra_class_bit // 8] |= 1 << (extra_class_bit % 8)
        struct.pack_into("<I", raw, 0, checksum(raw))
        source.write_bytes(raw)

        summary = run(cli, "inspect", "--input", str(source), "--section", "summary")
        assert summary["summary"]["Money"] == 0
        history = run(cli, "inspect", "--input", str(source), "--section", "inheritance")["inheritance"]
        assert history["professorRank"] == 9
        assert history["supports"][0]["maxPoints"] == 1001
        assert history["characters"][0]["skillRanks"][0] == 11
        assert 42 in history["characters"][0]["masteredClassIds"]
        assert 42 in history["characters"][38]["masteredClassIds"]
        assert history["characters"][30]["name"] == "Gilbert"
        assert [history["characters"][index]["name"] for index in range(38, 45)] == [
            "Yuri", "Balthus", "Constance", "Hapi", "Aelfric", "Jeritza", "Anna"]
        assert history["supports"][0]["name"]
        assert history["characters"][0]["name"]
        assert run(cli, "get", "--input", str(source), "--path", "Items[0].Id")["value"] == -1
        catalog = run(cli, "catalog", "--type", "classes")
        assert len(catalog) == 101  # 100 classes plus the GUI's "none" sentinel.

        patch_file.write_text(json.dumps({
            "expectedSha256": summary["sha256"],
            "operations": [
                {"op": "set", "path": "Player.Money", "value": 12345},
                {"op": "set", "path": "Activities.Reputation", "value": 321},
                {"op": "set", "path": "Characters[0].data.SkillExp[0]", "value": 39},
                {"op": "setSkillRank", "slot": 0, "skill": 1, "rank": 11, "experience": 0},
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
        assert struct.unpack_from("<H", edited, 12 + 0x644 + 0x32)[0] == 39
        assert struct.unpack_from("<H", edited, 12 + 0x644 + 0xFC)[0] == 39
        assert edited[12 + 0x644 + 0x88 + 1] == 11
        assert edited[12 + 0x644 + 0x1DC + 1] == 11
        assert edited[12 + 0x644 + 0x61] & 8
        assert struct.unpack_from("<I", edited, 12 + 0x231D9 + 0x1074)[0] == 12345
        assert struct.unpack_from("<I", edited, 12 + 0x250A1 + 0xC)[0] == 321

        class_flags_patch = directory / "class-flags.json"
        class_flags_patch.write_text(json.dumps({"operations": [
            {"op": "setBit", "path": "Characters[0].data.ClassUnlockFlags", "index": 17, "value": True},
            {"op": "setBit", "path": "Characters[0].data.ClassFlags", "index": 1, "value": True},
        ]}), encoding="utf-8")
        class_flags_target = directory / "class-flags-slot00"
        run(cli, "apply", "--input", str(source), "--patch", str(class_flags_patch),
            "--output", str(class_flags_target))
        class_flags_bytes = class_flags_target.read_bytes()
        unlock_offset = 12 + 0x644 + 0xD3 + 17 // 8
        class_flag_offset = 12 + 0x644 + 0xDF
        assert class_flags_bytes[unlock_offset] == raw[unlock_offset] | (1 << (17 % 8))
        assert class_flags_bytes[class_flag_offset] == raw[class_flag_offset] | (1 << 1)
        assert all(before == after for index, (before, after) in enumerate(zip(raw, class_flags_bytes))
                   if index not in {0, 1, 2, 3, unlock_offset, class_flag_offset})
        assert struct.unpack_from("<I", class_flags_bytes, 0)[0] == checksum(class_flags_bytes)

        invalid_checksum = bytearray(raw)
        invalid_checksum[12 + 0x231D9 + 0x1074] ^= 1
        invalid_source = directory / "checksum-mismatch-slot00"
        invalid_source.write_bytes(invalid_checksum)
        invalid_summary = run(cli, "inspect", "--input", str(invalid_source), "--section", "summary")
        assert invalid_summary["checksumValid"] is False
        repair_patch = directory / "repair-checksum.json"
        repair_patch.write_text(json.dumps({"operations": [
            {"op": "set", "path": "Player.Money", "value": 54321},
        ]}), encoding="utf-8")
        repaired_path = directory / "repaired-slot00"
        run(cli, "apply", "--input", str(invalid_source), "--patch", str(repair_patch),
            "--output", str(repaired_path))
        repaired = repaired_path.read_bytes()
        assert checksum(repaired) == struct.unpack_from("<I", repaired, 0)[0]
        assert invalid_source.read_bytes() == invalid_checksum

        ng_patch = directory / "ng-plus.json"
        ng_patch.write_text(json.dumps({"expectedSha256": summary["sha256"], "operations": [
            {"op": "setNgPlusProfessorRank", "rank": 8},
            {"op": "setNgPlusSupport", "index": 0, "points": 1200},
            {"op": "setNgPlusSkillRank", "recordIndex": 0, "skill": 0, "rank": 10},
            {"op": "setNgPlusClassMastery", "recordIndex": 0, "classId": 42, "mastered": False},
            {"op": "setNgPlusClassMastery", "recordIndex": 34, "classId": 99, "mastered": True},
            {"op": "setNgPlusClassMastery", "recordIndex": 38, "classId": 42, "mastered": False},
            {"op": "setNgPlusClassMastery", "recordIndex": 44, "classId": 99, "mastered": True},
        ]}), encoding="utf-8")
        ng_target = directory / "ng-plus-slot00"
        run(cli, "apply", "--input", str(source), "--patch", str(ng_patch),
            "--output", str(ng_target))
        ng_edited = ng_target.read_bytes()
        assert source.read_bytes() == raw
        assert ng_edited[player + 0x17CE] == 8
        assert struct.unpack_from("<H", ng_edited, player + 0x1576)[0] == 1200
        assert ng_edited[player + 0x17D8] == 10
        assert not ng_edited[player + 0x19CC + 42 // 8] & (1 << (42 % 8))
        assert ng_edited[player + 0x19CC + 34 * 13 + 99 // 8] & (1 << (99 % 8))
        assert not ng_edited[player + 0x1B93 + extra_class_bit // 8] & (1 << (extra_class_bit % 8))
        assert ng_edited[player + 0x1B93 + 124] & 0x80
        changed_offsets = {0, 1, 2, 3, player + 0x17CE, player + 0x1576,
                           player + 0x1577, player + 0x17D8,
                           player + 0x19CC + 42 // 8, player + 0x19CC + 34 * 13 + 99 // 8,
                           player + 0x1B93 + extra_class_bit // 8, player + 0x1B93 + 124}
        assert all(before == after for index, (before, after) in enumerate(zip(raw, ng_edited))
                   if index not in changed_offsets)
        assert struct.unpack_from("<I", ng_edited, 0)[0] == checksum(ng_edited)
        ng_history = run(cli, "inspect", "--input", str(ng_target),
                         "--section", "inheritance")["inheritance"]
        assert ng_history["professorRank"] == 8
        assert ng_history["supports"][0]["maxPoints"] == 1200
        assert ng_history["characters"][0]["skillRanks"][0] == 10
        assert 42 not in ng_history["characters"][0]["masteredClassIds"]
        assert 99 in ng_history["characters"][34]["masteredClassIds"]
        assert 42 not in ng_history["characters"][38]["masteredClassIds"]
        assert 99 in ng_history["characters"][44]["masteredClassIds"]

        invalid_ng_patch = directory / "invalid-ng-plus.json"
        invalid_ng_patch.write_text(json.dumps({"operations": [
            {"op": "setNgPlusClassMastery", "recordIndex": 45, "classId": 42, "mastered": True},
        ]}), encoding="utf-8")
        run(cli, "apply", "--input", str(source), "--patch", str(invalid_ng_patch),
            "--output", str(directory / "invalid-ng-plus"), success=False)
        assert not (directory / "invalid-ng-plus").exists()

        # Agent edits must not bypass the GUI's rank-specific skill limit.
        out_of_range = directory / "out-of-range.json"
        out_of_range.write_text(json.dumps({"operations": [
            {"op": "set", "path": "Characters[0].data.SkillExp[0]", "value": 40},
        ]}), encoding="utf-8")
        error = run(cli, "apply", "--input", str(source), "--patch", str(out_of_range),
                    "--output", str(directory / "invalid-skill"), success=False)
        assert "rank limit" in error["error"]

        # In-place mode must retain the exact original save as a backup.
        inplace = directory / "inplace-slot00"
        inplace.write_bytes(raw)
        applied_inplace = run(cli, "apply", "--input", str(inplace), "--patch", str(patch_file), "--in-place")
        assert Path(applied_inplace["backup"]).read_bytes() == raw
        assert inplace.read_bytes() == edited

        # Raw character exchange preserves the record, without rewriting the rest of the save.
        character = directory / "character.bin"
        run(cli, "export-character", "--input", str(target), "--slot", "0", "--output", str(character))
        imported = directory / "imported-slot00"
        run(cli, "import-character", "--input", str(source), "--slot", "0",
            "--character", str(character), "--output", str(imported))
        assert imported.read_bytes()[12 + 0x644:12 + 0x644 + len(character.read_bytes())] == character.read_bytes()
        assert imported.read_bytes()[sentinel] == 0xA5

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

        print("CLI inspection, NG+ editing, backups, character import, byte preservation, and rejection checks passed.")


if __name__ == "__main__":
    main()
