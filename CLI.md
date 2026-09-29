# Command-line save editor

`FethEditor.Cli` runs on Windows, macOS, and Linux. Download the matching self-contained CI artifact or build from source with `dotnet build Cli/Cli.csproj -c Release`. It accepts version-23 `slotXX` and `auto` saves from Fire Emblem: Three Houses v1.2.0. The separate `inspect-system` and `apply-system` commands accept version-5 or version-7 `system` saves. `suspend` files are not supported.

On macOS or Linux, replace `FethEditor.Cli.exe` in the examples below with `./FethEditor.Cli`. The source build requires .NET 10; the self-contained downloads include the runtime. An unsigned macOS download may need local approval in macOS security settings.

The CLI patches only named, mapped fields. Unknown bytes are preserved. `inspect` distinguishes current-run data from NG+ journal history. An edited save still needs an in-game test.

## Read

```powershell
.\FethEditor.Cli.exe inspect --input C:\saves\slot00 --section summary
.\FethEditor.Cli.exe inspect --input C:\saves\slot00 --section characters
.\FethEditor.Cli.exe get --input C:\saves\slot00 --path Characters[0].data.Level
.\FethEditor.Cli.exe inspect --input C:\saves\slot00 --section inheritance
.\FethEditor.Cli.exe catalog --type classes
.\FethEditor.Cli.exe catalog --type characters --id 0
.\FethEditor.Cli.exe inspect-system --input C:\saves\system --section flags
```

`inspect --section` accepts `summary`, `characters`, `inventory`, `battalions`, `activities`, `supports`, `inheritance`, or `all`. `inspect-system --section` accepts `slots`, `flags`, or `all`. `catalog --type` accepts `items`, `characters`, `classes`, `battalions`, `battalion-skills`, `abilities`, `arts`, `quests`, `supports`, or `support-ranks`. `catalog --type characters|classes|items --id N` returns the database entry's details. Output and errors are JSON. Optional `--language` selects a game database language enum such as `en_u`.

`inheritance` reads the separate NG+ journal history: professor rank, 270 maximum support-point values, 45 records of 11 skill ranks, and 45 class-mastery bitsets. These are not the current-run values. Character names follow the game's character database order; records without a confirmed playable character keep their numeric `recordIndex`. Support names come from the game's support table.

NG+ history can be edited with `setNgPlusProfessorRank` (`rank`: 0–9), `setNgPlusSupport` (`index`, `points`: 0–65535), `setNgPlusSkillRank` (`recordIndex`: 0–44, `skill`: 0–10, `rank`: 0–11), and `setNgPlusClassMastery` (`recordIndex`, `classId`: 0–99, `mastered`: boolean). `unlockNgPlusClasses` (`recordIndex`) sets only the character's eligible class-mastery bits, including DLC classes; existing bits for other classes are left untouched. For example, `{ "op": "setNgPlusSkillRank", "recordIndex": 0, "skill": 0, "rank": 11 }` changes the first historical character's sword rank. These operations edit historical unlocks, not the current-run character. The storage range for support points is known, but not every numeric value is meaningful to the game. Test one targeted change on a disposable save before broader edits.

For the GUI's rank controls, `setNgPlusSupportRank` and `setSupportRank` take a support `index` and `rank` (`None`, `C`, `C+`, `B`, `B+`, `A`, `A+`, or `S`) and map it to support points. `setProfessorRank` takes a rank index from 0 (E) to 9 (A+) and writes the matching current-run instruction-experience threshold. Unlike `setNgPlusProfessorRank`, it does not edit inherited progress.

## Edit

Save this as `patch.json`:

```json
{
  "expectedSha256": "<sha256 from inspect>",
  "operations": [
    { "op": "set", "path": "Player.Money", "value": 50000 },
    { "op": "set", "path": "Activities.Reputation", "value": 1000 },
    { "op": "set", "path": "Characters[0].data.Level", "value": 10 },
    { "op": "setBit", "path": "Characters[0].data.Abilities", "index": 3, "value": true }
  ]
}
```

```powershell
.\FethEditor.Cli.exe apply --input C:\saves\slot00 --patch patch.json --dry-run
.\FethEditor.Cli.exe apply --input C:\saves\slot00 --patch patch.json --output C:\saves\edited-slot00
```

The output path must not already exist. `--in-place` is available instead of `--output`; it creates a timestamped backup next to the source before replacement. Prefer a new output file and keep the original. `expectedSha256` prevents applying a patch to a different save. `--dry-run` reports byte-level differences without writing.

Other operations: `setName` (`value`), `sortItems`, `sortBattalions`, `inventoryDurability` (`mode`: `normal`, `unlimited`, or `weapons-unlimited`), `characterItemDurability` (`slot`, restores each carried item's normal durability maximum), `setCharacterItem` (`slot`, `itemSlot`, `id`, optional `durability`; default is that item's normal maximum), `maxSkillExp` (`slot`), `maxClassExp` (`slot`), `unlockAll` (`slot`, `kind`: `abilities` or `combat-arts`), `fillItems` (`kind`: `misc` or `gifts`, `amount`), and `addEssentialItems`. `maxClassExp` changes experience only for classes available to that character; it does not set mastery flags or grant rewards. Single-field edits remain available for unusual save data. Bulk unlocking or filling may produce game-invalid combinations; test those changes separately.

To change system flags, use a separate patch file with operations such as `{ "op": "setSystemFlag", "index": 8, "value": true }`, then run `apply-system --input C:\saves\system --patch patch.json --output C:\saves\edited-system`. `--dry-run` and `--in-place` work here too. An unchanged patch does not write a file. A written version-5 input is upgraded to version 7.

`setSkillRank` takes `slot`, zero-based `skill`, `rank`, and `experience`. It updates both the primary and mirrored skill rank/experience fields together. For example, Byleth's sword rank S+ is `{"op":"setSkillRank","slot":0,"skill":0,"rank":11,"experience":0}`. This operation goes beyond the GUI's experience-only controls; use it only with a valid in-game rank and test the result.

`set` supports the GUI's mapped inventory, character, battalion, player, activity, support, gift, and quest-state fields. Use `get` for a numeric field and `inspect` for the structure; `gifts[0]` through `gifts[244]` are convenient aliases. `setBit` works on a character's `Abilities`, `CombatArts`, `ClassUnlockFlags`, `Flags`, or `ClassFlags` by zero-based bit index. Class mastery and skill experience edits follow the GUI's value limits and mirror fields where required.

For full character records, use `export-character --input ... --slot 0 --output character.bin`, then `import-character --input ... --slot 0 --character character.bin --output ...`. Importing a record from another save is riskier than targeted edits; it replaces all bytes in that record, including fields not understood by the editor.

The CLI checks file size and version before reading, reports checksum validity in `inspect`, and verifies written output. A mismatched source checksum is repaired when the save is written. It does not make gameplay semantics safe automatically. Close the game before replacing a save, retain an untouched backup, and test on a disposable copy first.
