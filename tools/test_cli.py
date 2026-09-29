#!/usr/bin/env python3
"""Exercise the CLI against a synthetic v23 save without publishing user data."""

from __future__ import annotations

import argparse
import json
import shutil
import struct
import subprocess
import tempfile
from pathlib import Path


SAVE_SIZE = 0x25B20 + 12


def checksum(data: bytes) -> int:
    return sum(data[12:]) & 0xFFFFFFFF


def run(cli: Path, *args: str, success: bool = True) -> dict:
    command = [shutil.which("dotnet") or "dotnet", str(cli)] if cli.suffix == ".dll" else [str(cli)]
    result = subprocess.run([*command, *args], capture_output=True, text=True)
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
        for item_slot in range(6):
            struct.pack_into("<h", raw, 12 + 0x644 + item_slot * 4, -1)
        sentinel = 12 + 0x644 + 0x2A
        raw[sentinel] = 0xA5
        player = 12 + 0x231D9
        struct.pack_into("<H", raw, player + 0x1576, 1001)
        raw[player + 0x17CE] = 9
        raw[player + 0x17D8] = 11
        class_start = player + 0x17D8 + 45 * 11
        raw[class_start + 42 // 8] |= 1 << (42 % 8)
        raw[class_start + 38 * 13 + 42 // 8] |= 1 << (42 % 8)
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
        assert run(cli, "catalog", "--type", "classes", "--id", "0")["details"]
        assert run(cli, "catalog", "--type", "items", "--id", "22")["details"]
        assert run(cli, "catalog", "--type", "battalion-skills")
        battalion_catalog = run(cli, "catalog", "--type", "obtainable-battalions")
        assert len(battalion_catalog) == 128
        assert len({entry["id"] for entry in battalion_catalog}) == 128
        assert battalion_catalog[0] == {
            "id": 0, "name": "Church of Seiros Soldiers",
            "experience": 400, "stamina": 30, "skill": 4,
        }
        assert run(cli, "catalog", "--type", "support-ranks")[-1] == {"rank": "S", "points": 1001}
        support_catalog = run(cli, "catalog", "--type", "supports")
        assert support_catalog[1]["maximumRank"] == "S"
        assert support_catalog[65]["ranks"] == ["None", "C", "B"]

        battalion_raw = bytearray(raw)
        battalion_start = player + 0xA30
        for slot in range(200):
            struct.pack_into("<hHHBB", battalion_raw, battalion_start + slot * 8,
                             -1, 0, 0, 200, 0)
        existing_battalion = (-1, 77, 43, 20, 1)
        struct.pack_into("<hHHBB", battalion_raw, battalion_start, *existing_battalion)
        character_start = 12 + 0x644
        battalion_raw[character_start + 0x4A] = 1
        struct.pack_into("<hHHBB", battalion_raw, character_start + 0x18,
                         -1, 11, 55, 10, 3)
        struct.pack_into("<I", battalion_raw, 0, checksum(battalion_raw))
        battalion_source = directory / "battalion-source"
        battalion_source.write_bytes(battalion_raw)
        battalion_patch = directory / "battalion-patch.json"
        battalion_patch.write_text(json.dumps({"operations": [
            {"op": "fillMissingBattalions"},
        ]}), encoding="utf-8")
        battalion_target = directory / "all-battalions"
        run(cli, "apply", "--input", str(battalion_source), "--patch", str(battalion_patch),
            "--output", str(battalion_target))
        battalion_data = battalion_target.read_bytes()
        entries = [struct.unpack_from("<hHHBB", battalion_data, battalion_start + slot * 8)
                   for slot in range(200)]
        assert next(entry for entry in entries if entry[3] == 20) == existing_battalion
        owned_types = {entry[3] for entry in entries if entry[3] < 200}
        assert len(owned_types) == 127
        assert 10 not in owned_types  # Equipped by a character, not duplicated in barracks.
        assert owned_types | {10} == {entry["id"] for entry in battalion_catalog}
        assert next(entry for entry in entries if entry[3] == 0) == (-1, 400, 30, 0, 4)
        assert battalion_source.read_bytes() == battalion_raw
        assert run(cli, "apply", "--input", str(battalion_target),
                   "--patch", str(battalion_patch), "--dry-run")["changedBytes"] == 0

        full_battalion_raw = bytearray(battalion_raw)
        for slot in range(200):
            struct.pack_into("<hHHBB", full_battalion_raw, battalion_start + slot * 8,
                             *existing_battalion)
        struct.pack_into("<I", full_battalion_raw, 0, checksum(full_battalion_raw))
        full_battalion_source = directory / "full-battalion-source"
        full_battalion_source.write_bytes(full_battalion_raw)
        full_battalion_target = directory / "full-battalion-target"
        error = run(cli, "apply", "--input", str(full_battalion_source),
                    "--patch", str(battalion_patch), "--output", str(full_battalion_target),
                    success=False)
        assert "free battalion slots" in error["error"]
        assert not full_battalion_target.exists()
        assert full_battalion_source.read_bytes() == full_battalion_raw

        rank_patch = directory / "rank.json"
        rank_patch.write_text(json.dumps({"operations": [
            {"op": "setProfessorRank", "rank": 9},
            {"op": "setSupportRank", "index": 1, "rank": "A"},
            {"op": "setNgPlusSupportRank", "index": 2, "rank": "S"},
        ]}), encoding="utf-8")
        rank_target = directory / "rank-slot00"
        run(cli, "apply", "--input", str(source), "--patch", str(rank_patch),
            "--output", str(rank_target))
        assert run(cli, "get", "--input", str(rank_target),
                   "--path", "Activities.InstructExp")["value"] == 44500
        assert run(cli, "inspect", "--input", str(rank_target),
                   "--section", "supports")["supports"][1]["rank"] == "A"
        assert run(cli, "inspect", "--input", str(rank_target),
                   "--section", "inheritance")["inheritance"]["supports"][2]["maxPoints"] == 1001

        invalid_rank_patch = directory / "invalid-support-rank.json"
        invalid_rank_patch.write_text(json.dumps({"operations": [
            {"op": "setSupportRank", "index": 1, "rank": "A+"},
        ]}), encoding="utf-8")
        invalid_rank_target = directory / "invalid-support-rank-slot00"
        run(cli, "apply", "--input", str(source), "--patch", str(invalid_rank_patch),
            "--output", str(invalid_rank_target), success=False)
        assert not invalid_rank_target.exists()

        max_rank_patch = directory / "max-support-ranks.json"
        max_rank_patch.write_text(json.dumps({"operations": [
            {"op": "maxSupportRank", "index": 65},
            {"op": "maxNgPlusSupportRank", "index": 65},
            {"op": "maxNgPlusSupports"},
        ]}), encoding="utf-8")
        max_rank_target = directory / "max-support-ranks-slot00"
        run(cli, "apply", "--input", str(source), "--patch", str(max_rank_patch),
            "--output", str(max_rank_target))
        assert run(cli, "inspect", "--input", str(max_rank_target),
                   "--section", "supports")["supports"][65]["rank"] == "B"
        max_history = run(cli, "inspect", "--input", str(max_rank_target),
                          "--section", "inheritance")["inheritance"]["supports"]
        assert max_history[0]["maxPoints"] == 1001
        assert max_history[35]["maxPoints"] == 0
        assert max_history[62]["maxPoints"] == 801
        assert max_history[65]["maxPoints"] == 301

        item_patch = directory / "character-item.json"
        item_patch.write_text(json.dumps({"operations": [
            {"op": "setCharacterItem", "slot": 0, "itemSlot": 0, "id": 22},
        ]}), encoding="utf-8")
        item_target = directory / "character-item-slot00"
        run(cli, "apply", "--input", str(source), "--patch", str(item_patch),
            "--output", str(item_target))
        item_bytes = item_target.read_bytes()
        assert struct.unpack_from("<h", item_bytes, 12 + 0x644)[0] == 22
        assert item_bytes[12 + 0x644 + 0x87] == 1
        assert item_bytes[12 + 0x644 + 2] > 0
        assert run(cli, "inspect", "--input", str(item_target),
                   "--section", "characters")["characters"][0]["ItemCount"] == 1
        invalid_item = directory / "invalid-character-item.json"
        invalid_item.write_text(json.dumps({"operations": [
            {"op": "setCharacterItem", "slot": 0, "itemSlot": 2, "id": 22},
        ]}), encoding="utf-8")
        run(cli, "apply", "--input", str(source), "--patch", str(invalid_item),
            "--output", str(directory / "invalid-character-item"), success=False)
        invalid_stat = directory / "invalid-stat.json"
        invalid_stat.write_text(json.dumps({"operations": [
            {"op": "set", "path": "Characters[0].data.Movement", "value": 250},
        ]}), encoding="utf-8")
        stat_error = run(cli, "apply", "--input", str(source), "--patch", str(invalid_stat),
                         "--output", str(directory / "invalid-stat"), success=False)
        assert "maximum" in stat_error["error"]

        system_raw = bytearray(0x1204)
        struct.pack_into("<II", system_raw, 4, 7, len(system_raw))
        struct.pack_into("<I", system_raw, 0, checksum(system_raw))
        system_source = directory / "system"
        system_source.write_bytes(system_raw)
        system_info = run(cli, "inspect-system", "--input", str(system_source),
                          "--section", "flags")
        assert system_info["checksumValid"] is True
        assert len(system_info["flags"]) == 2464
        assert system_info["flags"][8]["enabled"] is False
        system_patch = directory / "system-patch.json"
        system_patch.write_text(json.dumps({
            "expectedSha256": system_info["sha256"],
            "operations": [{"op": "setSystemFlag", "index": 8, "value": True}],
        }), encoding="utf-8")
        system_target = directory / "edited-system"
        system_dry = run(cli, "apply-system", "--input", str(system_source),
                         "--patch", str(system_patch), "--dry-run")
        assert system_dry["changedFlags"] == [8]
        assert not system_target.exists()
        run(cli, "apply-system", "--input", str(system_source), "--patch", str(system_patch),
            "--output", str(system_target))
        assert system_source.read_bytes() == system_raw
        assert run(cli, "inspect-system", "--input", str(system_target),
                   "--section", "flags")["flags"][8]["enabled"] is True
        assert struct.unpack_from("<I", system_target.read_bytes(), 0)[0] == checksum(system_target.read_bytes())
        inplace_system = directory / "inplace-system"
        inplace_system.write_bytes(system_raw)
        written = run(cli, "apply-system", "--input", str(inplace_system),
                      "--patch", str(system_patch), "--in-place")
        assert Path(written["backup"]).read_bytes() == system_raw

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
        assert not ng_edited[class_start + 42 // 8] & (1 << (42 % 8))
        assert ng_edited[class_start + 34 * 13 + 99 // 8] & (1 << (99 % 8))
        assert not ng_edited[class_start + 38 * 13 + 42 // 8] & (1 << (42 % 8))
        assert ng_edited[class_start + 44 * 13 + 99 // 8] & 0x08
        changed_offsets = {0, 1, 2, 3, player + 0x17CE, player + 0x1576,
                           player + 0x1577, player + 0x17D8,
                           class_start + 42 // 8, class_start + 34 * 13 + 99 // 8,
                           class_start + 38 * 13 + 42 // 8, class_start + 44 * 13 + 99 // 8}
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

        print("CLI slot/system inspection and editing, NG+ history, character items, backups, and rejection checks passed.")


if __name__ == "__main__":
    main()
