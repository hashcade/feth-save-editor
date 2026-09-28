using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using SaveEditor;
using SaveEditor.Structs;

namespace FethCli
{
    internal static class Program
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 100 };

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || args[0] == "help" || args[0] == "--help")
                {
                    Console.WriteLine("FETH_Cli.exe inspect|get|apply|catalog|export-character|import-character --input <slot00> [options]");
                    return 0;
                }

                string command = args[0].ToLowerInvariant();
                Dictionary<string, string> options = ParseOptions(args.Skip(1).ToArray());
                enmLanguage language = enmLanguage.en_u;
                if (options.TryGetValue("language", out string lang))
                    language = (enmLanguage)Enum.Parse(typeof(enmLanguage), lang, true);
                Database.Init(language);

                if (command == "catalog")
                {
                    Print(Catalog(Required(options, "type")));
                    return 0;
                }

                string input = Required(options, "input");
                SaveBuffer save = SaveBuffer.Open(input);

                switch (command)
                {
                    case "inspect":
                        Print(Inspect(save, options.TryGetValue("section", out string section) ? section : "all"));
                        break;
                    case "get":
                        string path = Required(options, "path");
                        Print(new { path, value = save.Get(path), sha256 = save.Sha256 });
                        break;
                    case "apply":
                        Apply(save, input, options);
                        break;
                    case "export-character":
                        int exportSlot = int.Parse(Required(options, "slot"), CultureInfo.InvariantCulture);
                        string exportPath = Required(options, "output");
                        if (File.Exists(exportPath)) throw new IOException("Output already exists: " + exportPath);
                        File.WriteAllBytes(exportPath, save.ExportCharacter(exportSlot));
                        Print(new { output = Path.GetFullPath(exportPath), slot = exportSlot, bytes = Character_V23.SIZE });
                        break;
                    case "import-character":
                        var importPatch = new Dictionary<string, object>
                        {
                            { "expectedSha256", save.Sha256 },
                            { "operations", new object[] { new Dictionary<string, object>
                                {
                                    { "op", "importCharacter" },
                                    { "slot", int.Parse(Required(options, "slot"), CultureInfo.InvariantCulture) },
                                    { "file", Required(options, "character") }
                                } } }
                        };
                        ApplyOperations(save, importPatch);
                        Finish(save, input, options);
                        break;
                    default:
                        throw new ArgumentException("Unknown command: " + command);
                }
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(Json.Serialize(new { error = error.Message, type = error.GetType().Name }));
                return 1;
            }
        }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("Expected an --option, got " + args[i]);
                string key = args[i].Substring(2);
                if (key == "in-place" || key == "dry-run")
                    result[key] = "true";
                else
                {
                    if (++i == args.Length) throw new ArgumentException("Missing value for --" + key);
                    result[key] = args[i];
                }
            }
            return result;
        }

        private static string Required(Dictionary<string, string> options, string key)
        {
            if (!options.TryGetValue(key, out string value) || string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Missing --" + key);
            return value;
        }

        private static void Apply(SaveBuffer save, string input, Dictionary<string, string> options)
        {
            var patch = Json.DeserializeObject(File.ReadAllText(Required(options, "patch"))) as Dictionary<string, object>;
            if (patch == null) throw new InvalidDataException("Patch must be a JSON object.");
            ApplyOperations(save, patch);
            Finish(save, input, options);
        }

        private static void ApplyOperations(SaveBuffer save, Dictionary<string, object> patch)
        {
            if (patch.TryGetValue("expectedSha256", out object expected)
                && !string.Equals(Convert.ToString(expected), save.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Input SHA-256 differs from patch expectation.");
            if (!patch.TryGetValue("operations", out object operations) || !(operations is object[] list))
                throw new InvalidDataException("Patch needs an operations array.");
            foreach (object operation in list)
            {
                var item = operation as Dictionary<string, object>;
                if (item == null) throw new InvalidDataException("Every operation must be an object.");
                string op = Value<string>(item, "op");
                switch (op)
                {
                    case "set":
                        save.Set(Value<string>(item, "path"), Value<long>(item, "value"));
                        break;
                    case "setName":
                        save.SetName(Value<string>(item, "value"));
                        break;
                    case "setBit":
                        save.SetBit(Value<string>(item, "path"), Value<int>(item, "index"), Value<bool>(item, "value"));
                        break;
                    case "sortItems":
                        save.SortItems();
                        break;
                    case "sortBattalions":
                        save.SortBattalions();
                        break;
                    case "importCharacter":
                        save.ImportCharacter(Value<int>(item, "slot"), File.ReadAllBytes(Value<string>(item, "file")));
                        break;
                    default:
                        throw new ArgumentException("Unknown patch operation: " + op);
                }
            }
        }

        private static T Value<T>(Dictionary<string, object> objectValue, string key)
        {
            if (!objectValue.TryGetValue(key, out object value)) throw new ArgumentException("Missing " + key);
            if (typeof(T) == typeof(bool))
            {
                if (!(value is bool)) throw new ArgumentException(key + " must be a boolean.");
                return (T)value;
            }
            if (typeof(T) == typeof(string))
            {
                if (!(value is string)) throw new ArgumentException(key + " must be a string.");
                return (T)value;
            }
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }

        private static void Finish(SaveBuffer save, string input, Dictionary<string, string> options)
        {
            bool inPlace = options.ContainsKey("in-place");
            bool dryRun = options.ContainsKey("dry-run");
            if (inPlace && options.ContainsKey("output"))
                throw new ArgumentException("Use either --in-place or --output, not both.");
            if (!inPlace && !dryRun && !options.ContainsKey("output"))
                throw new ArgumentException("Provide --output or --in-place.");

            byte[] result = save.FinishedBytes();
            string resultHash = SaveBuffer.Digest(result);
            if (dryRun)
            {
                Print(new { dryRun = true, inputSha256 = save.Sha256, resultSha256 = resultHash,
                    changedBytes = save.ChangedBytes, differences = save.Differences() });
                return;
            }
            if (save.ChangedBytes == 0)
                throw new InvalidOperationException("Patch changed no data; output was not written.");

            string destination = Path.GetFullPath(inPlace ? input : Required(options, "output"));
            if (!inPlace && Path.GetFullPath(input).Equals(destination, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Use --in-place to modify the input file.");
            if (!inPlace && File.Exists(destination))
                throw new IOException("Output already exists: " + destination);
            string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            string backup = null;
            try
            {
                File.WriteAllBytes(temporary, result);
                SaveBuffer verified = SaveBuffer.Open(temporary);
                if (verified.Sha256 != resultHash)
                    throw new InvalidDataException("Written save failed verification.");
                if (inPlace)
                {
                    backup = destination + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
                    File.Replace(temporary, destination, backup);
                }
                else
                    File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            Print(new { inputSha256 = save.Sha256, resultSha256 = resultHash,
                changedBytes = save.ChangedBytes, output = destination, backup });
        }

        private static object Inspect(SaveBuffer save, string section)
        {
            SaveData_V23 data = save.Data;
            var result = new Dictionary<string, object>
            {
                ["format"] = "FE3H 1.1.0-1.2.0 / save version 23",
                ["sha256"] = save.Sha256,
                ["note"] = "Current-run fields only. NG+ journal inheritance fields are not mapped."
            };
            if (section == "all" || section == "summary")
                result["summary"] = new
                {
                    playerName = Util.DecodeString(data.PlayerName),
                    data.Player.Playtime, data.Player.Money, data.Player.Chapter,
                    data.Player.Difficulty, data.Player.Gamestyle, data.Player.Route, data.Player.MapID,
                    renown = data.Activities.Reputation, professorExp = data.Activities.InstructExp,
                    data.ItemCount
                };
            if (section == "all" || section == "characters")
                result["characters"] = data.Characters.Select((character, slot) => new
                {
                    slot, id = character.data.Id,
                    name = SafeName(() => Database.GetUnitName(character.data.Id)),
                    character.data.Level, character.data.Exp, character.data.Class,
                    className = SafeName(() => Database.GetClassName(character.data.Class)),
                    character.data.CurrentClassExp, character.data.CurrentClassLevel,
                    character.data.HP, character.data.Strength, character.data.Magic,
                    character.data.Dexterity, character.data.Speed, character.data.Luck,
                    character.data.Defense, character.data.Resistance,
                    character.data.Movement, character.data.Charm, character.data.Motivation,
                    character.data.RNG_VALUE, character.data.AdjutantId,
                    character.data.SkillExp, character.data.SkillLevel,
                    character.data.ClassExp, character.data.ClassLevel,
                    character.data.CombatArts, character.data.Abilities,
                    character.data.EquippedAbilities, character.data.EquippedCombatArts,
                    character.data.Flags, character.data.ClassUnlockFlags, character.data.ClassFlags,
                    items = character.data.Items.Select((item, index) => ItemInfo(item, index)).ToArray()
                }).ToArray();
            if (section == "all" || section == "inventory")
                result["inventory"] = new
                {
                    items = data.Items.Select((item, slot) => ItemInfo(item, slot)).ToArray(),
                    misc = data.Player.MiscItems.Select((value, id) => new { id, name = SafeName(() => Database.GetMiscItemName(id)), value }).ToArray(),
                    gifts = Enumerable.Range(0, Player_V23.COUNT_GIFT_ITEMS)
                        .Select(id => new { id, name = SafeName(() => Database.GetGiftItemName(id)), value = data.Player.GetGiftItem(id) }).ToArray()
                };
            if (section == "all" || section == "battalions")
                result["battalions"] = data.Player.Battalions.Select((battalion, slot) => new
                {
                    slot, battalion.CharacterId, battalion.Exp, battalion.Stamina, battalion.Type, battalion.Skill,
                    name = SafeName(() => Database.GetBattalionName(battalion.Type))
                }).ToArray();
            if (section == "all" || section == "activities")
                result["activities"] = new
                {
                    data.Activities.Reputation, data.Activities.InstructExp,
                    data.Activities.ActivityExplore, data.Activities.ActivityLesson, data.Activities.ActivityBattle,
                    data.Activities.Statue1, data.Activities.Statue2, data.Activities.Statue3, data.Activities.Statue4,
                    playLog = new { data.Activities.PlayLog_Wark, data.Activities.PlayLog_Lecture,
                        data.Activities.PlayLog_ToBtl, data.Activities.PlayLog_Rest, data.Activities.PlayLog_Trnmnt,
                        data.Activities.PlayLog_Sing, data.Activities.PlayLog_Lunch, data.Activities.PlayLog_Cooking,
                        data.Activities.PlayLog_Drill, data.Activities.PlayLog_Teaparty, data.Activities.PlayLog_SCOUT },
                    quests = data.Activities.QuestStateList.Select((value, id) => new { id,
                        name = SafeName(() => Database.GetQuestName(id)), value }).ToArray()
                };
            if (section == "all" || section == "supports")
                result["supports"] = data.Player.CharacterSupportValues.Select((value, id) => new
                { id, name = SafeName(() => Database.GetSupportTalkName(id)), value }).ToArray();
            if (result.Count == 3) throw new ArgumentException("Unknown section: " + section);
            return result;
        }

        private static object ItemInfo(Item item, int slot) => new
        {
            slot, item.Id, name = SafeName(() => Database.GetItemName(item.Id)), item.Durability, item.Amount
        };

        private static string SafeName(Func<string> getName)
        {
            try { return getName(); }
            catch { return Database.STR_UNKNOWN; }
        }

        private static object Catalog(string type)
        {
            switch (type.ToLowerInvariant())
            {
                case "items": return Database.ItemList.Select(entry => new { id = entry.Key, name = entry.Value }).ToArray();
                case "characters": return Database.UnitList.Select(entry => new { id = entry.Key, name = entry.Value }).ToArray();
                case "classes": return Database.ClassList.Select(entry => new { id = entry.Key, name = entry.Value }).ToArray();
                case "battalions": return Database.BattalionList.Select(entry => new { id = entry.Key, name = entry.Value }).ToArray();
                case "abilities": return Database.AbilityList.Select(entry => new { id = entry.Key, name = entry.Value }).ToArray();
                case "arts": return Database.CombatArtList.Select(entry => new { id = entry.Key, name = entry.Value }).ToArray();
                case "quests": return Enumerable.Range(0, 150).Select(id => new { id, name = SafeName(() => Database.GetQuestName(id)) }).ToArray();
                case "supports": return Enumerable.Range(0, Player_V23.COUNT_SUPPORT).Select(id => new { id, name = SafeName(() => Database.GetSupportTalkName(id)) }).ToArray();
                default: throw new ArgumentException("Unknown catalog: " + type);
            }
        }

        private static void Print(object value) => Console.WriteLine(Json.Serialize(value));
    }
}
