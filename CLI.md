# Command-line save editor

`FethEditor.Cli` runs on Windows, macOS, and Linux. Download the matching self-contained CI artifact or build from source with `dotnet build Cli/Cli.csproj -c Release`. It accepts version-23 `slotXX` and `auto` saves from Fire Emblem: Three Houses v1.2.0 and rejects `system` and `suspend` files.

On macOS or Linux, replace `FethEditor.Cli.exe` in the examples below with `./FethEditor.Cli`. The source build requires .NET 10; the self-contained downloads include the runtime. An unsigned macOS download may need local approval in macOS security settings.

The CLI patches only named, mapped fields. Unknown bytes are preserved. `inspect` distinguishes current-run data from read-only NG+ journal history. An edited save still needs an in-game test.

## Read

```powershell
.\FethEditor.Cli.exe inspect --input C:\saves\slot00 --section summary
.\FethEditor.Cli.exe inspect --input C:\saves\slot00 --section characters
.\FethEditor.Cli.exe get --input C:\saves\slot00 --path Characters[0].data.Level
.\FethEditor.Cli.exe inspect --input C:\saves\slot00 --section inheritance
.\FethEditor.Cli.exe catalog --type classes
```

`inspect --section` accepts `summary`, `characters`, `inventory`, `battalions`, `activities`, `supports`, `inheritance`, or `all`. `catalog --type` accepts `items`, `characters`, `classes`, `battalions`, `abilities`, `arts`, `quests`, and `supports`. Output and errors are JSON. Optional `--language` selects a GUI language enum such as `en_u`.

`inheritance` reads the separate NG+ journal history: professor rank, 270 maximum support-point values, 45 records of 11 skill ranks, and 45 class-mastery bitsets. These are not the current-run values. Character names follow the game's character database order; records without a confirmed playable character keep their numeric `recordIndex`. Support names come from the game's support table.

NG+ history can be edited with `setNgPlusProfessorRank` (`rank`: 0–9), `setNgPlusSupport` (`index`, `points`: 0–65535), `setNgPlusSkillRank` (`recordIndex`: 0–44, `skill`: 0–10, `rank`: 0–11), and `setNgPlusClassMastery` (`recordIndex`, `classId`: 0–99, `mastered`: boolean). For example, `{ "op": "setNgPlusSkillRank", "recordIndex": 0, "skill": 0, "rank": 11 }` changes the first historical character's sword rank. These operations edit historical unlocks, not the current-run character. The storage range for support points is known, but not every numeric value is meaningful to the game. Test one targeted change on a disposable save before broader edits.

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

Other operations: `setName` (`value`), `sortItems`, `sortBattalions`, `inventoryDurability` (`mode`: `normal`, `unlimited`, or `weapons-unlimited`), `characterItemDurability` (`slot`), `maxSkillExp` (`slot`), `maxClassExp` (`slot`), `unlockAll` (`slot`, `kind`: `abilities` or `combat-arts`), `fillItems` (`kind`: `misc` or `gifts`, `amount`), and `addEssentialItems`. Bulk unlocking or filling may produce game-invalid combinations; test those changes separately.

`setSkillRank` takes `slot`, zero-based `skill`, `rank`, and `experience`. It updates both the primary and mirrored skill rank/experience fields together. For example, Byleth's sword rank S+ is `{"op":"setSkillRank","slot":0,"skill":0,"rank":11,"experience":0}`. This operation goes beyond the GUI's experience-only controls; use it only with a valid in-game rank and test the result.

`set` supports the GUI's mapped inventory, character, battalion, player, activity, support, gift, and quest-state fields. Use `get` for a numeric field and `inspect` for the structure; `gifts[0]` through `gifts[244]` are convenient aliases. `setBit` works on a character's `Abilities`, `CombatArts`, `ClassUnlockFlags`, `Flags`, or `ClassFlags` by zero-based bit index. Class mastery and skill experience edits follow the GUI's value limits and mirror fields where required.

For full character records, use `export-character --input ... --slot 0 --output character.bin`, then `import-character --input ... --slot 0 --character character.bin --output ...`. Importing a record from another save is riskier than targeted edits; it replaces all bytes in that record, including fields not understood by the editor.

The CLI checks file size, version, and checksum before reading and verifies written output. It does not make gameplay semantics safe automatically. Close the game before replacing a save, retain an untouched backup, and test on a disposable copy first.
