using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SaveEditor;
using SaveEditor.Structs;

namespace FethCli
{
    internal sealed class SaveBuffer
    {
        private const int HeaderSize = 12;
        private static readonly Regex SegmentPattern = new Regex(@"^([A-Za-z_][A-Za-z_0-9]*)(?:\[(\d+)\])?$", RegexOptions.Compiled);
        private static readonly Regex[] WritablePaths =
        {
            Pattern(@"^items\[\d+\]\.(id|durability|amount)$"),
            Pattern(@"^characters\[\d+\]\.data\.(id|rng_value|exp|level|hp|strength|magic|dexterity|speed|luck|defense|resistance|movement|charm|motivation|currentclassexp|currentclasslevel|adjutantid|flags|classflags)$"),
            Pattern(@"^characters\[\d+\]\.data\.(skillexp|classexp|classlevel|equippedabilities|equippedcombatarts|combatarts|abilities|classunlockflags)\[\d+\]$"),
            Pattern(@"^characters\[\d+\]\.data\.items\[\d+\]\.(id|durability|amount)$"),
            Pattern(@"^player\.(playtime|money|chapter|difficulty|gamestyle|route|mapid)$"),
            Pattern(@"^player\.(charactersupportvalues|miscitems|giftitems|giftitems2)\[\d+\]$"),
            Pattern(@"^player\.battalions\[\d+\]\.(characterid|exp|stamina|type|skill)$"),
            Pattern(@"^activities\.(reputation|instructexp|activityexplore|activitylesson|activitybattle|statue[1-4]|playlog_[a-z]+)$"),
            Pattern(@"^activities\.queststatelist\[\d+\]$")
        };
        private static readonly Regex[] WritableBits =
        {
            Pattern(@"^characters\[\d+\]\.data\.(combatarts|abilities|classunlockflags|flags|classflags)$")
        };

        private readonly byte[] bytes;
        private readonly byte[] original;

        private SaveBuffer(byte[] file)
        {
            bytes = file;
            original = (byte[])file.Clone();
        }

        public static SaveBuffer Open(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            if (file.Length != Save.SIZE_SAVE_V23)
                throw new InvalidDataException($"Expected a 1.2.0 slot/auto file of {Save.SIZE_SAVE_V23} bytes; got {file.Length}. Suspend and system files cannot be edited here.");
            if (BitConverter.ToUInt32(file, 4) != Save.CURRENT_VERSION || BitConverter.ToUInt32(file, 8) != file.Length)
                throw new InvalidDataException("Unsupported save version or declared size.");
            uint checksum = BitConverter.ToUInt32(file, 0);
            if (checksum != Checksum(file))
                throw new InvalidDataException("Save checksum is invalid. No changes were made.");
            return new SaveBuffer(file);
        }

        public SaveData_V23 Data => Util.ReadStructure<SaveData_V23>(bytes.Skip(HeaderSize).ToArray());
        public string Sha256 => Digest(original);
        public int ChangedBytes => Enumerable.Range(HeaderSize, bytes.Length - HeaderSize).Count(i => bytes[i] != original[i]);

        public object Get(string path)
        {
            if (path.Equals("playerName", StringComparison.OrdinalIgnoreCase))
                return Util.DecodeString(Data.PlayerName);
            if (TryGift(path, out int gift))
                return gift < Player_V23.COUNT_GIFT_ITEMS1
                    ? Get($"player.GiftItems[{gift}]")
                    : Get($"player.GiftItems2[{gift - Player_V23.COUNT_GIFT_ITEMS1}]");
            Location loc = Resolve(path);
            return ReadNumber(loc);
        }

        public void Set(string path, long value)
        {
            if (TryGift(path, out int gift))
            {
                Set(gift < Player_V23.COUNT_GIFT_ITEMS1
                    ? $"player.GiftItems[{gift}]"
                    : $"player.GiftItems2[{gift - Player_V23.COUNT_GIFT_ITEMS1}]", value);
                return;
            }
            if (!WritablePaths.Any(pattern => pattern.IsMatch(path)))
                throw new ArgumentException($"Field is not writable: {path}");
            Location loc = Resolve(path);
            Validate(path, value);
            WriteNumber(loc, value);

            Match skill = Regex.Match(path, @"^(characters\[\d+\]\.data\.)skillexp(\[\d+\])$", RegexOptions.IgnoreCase);
            if (skill.Success)
                WriteNumber(Resolve(skill.Groups[1].Value + "SkillExp2" + skill.Groups[2].Value), value);

            Match current = Regex.Match(path, @"^(characters\[\d+\]\.data\.)currentclass(exp|level)$", RegexOptions.IgnoreCase);
            if (current.Success)
            {
                int classId = checked((int)(long)Get(current.Groups[1].Value + "Class"));
                string field = current.Groups[2].Value.Equals("exp", StringComparison.OrdinalIgnoreCase) ? "ClassExp" : "ClassLevel";
                WriteNumber(Resolve(current.Groups[1].Value + field + "[" + classId + "]"), value);
            }

            Match classValue = Regex.Match(path, @"^(characters\[\d+\]\.data\.)class(exp|level)\[(\d+)\]$", RegexOptions.IgnoreCase);
            if (classValue.Success)
            {
                int classId = checked((int)(long)Get(classValue.Groups[1].Value + "Class"));
                if (classId == int.Parse(classValue.Groups[3].Value))
                    WriteNumber(Resolve(classValue.Groups[1].Value + "CurrentClass" + classValue.Groups[2].Value), value);
            }
            if (Regex.IsMatch(path, @"^items\[\d+\]\.id$", RegexOptions.IgnoreCase))
                RecountItems();
        }

        public void SetName(string name)
        {
            byte[] encoded = Util.MakeName(name, 0x28);
            int offset = HeaderSize + checked((int)Marshal.OffsetOf(typeof(SaveData_V23), "PlayerName"));
            Buffer.BlockCopy(encoded, 0, bytes, offset, encoded.Length);
        }

        public void SetBit(string path, int index, bool enabled)
        {
            if (!WritableBits.Any(pattern => pattern.IsMatch(path)))
                throw new ArgumentException($"Flag group is not writable: {path}");
            Location loc = Resolve(path);
            int byteCount;
            if (loc.Type.IsArray)
                byteCount = loc.ArrayLength * ElementSize(loc.Type.GetElementType());
            else
                byteCount = ElementSize(loc.Type);
            if (index < 0 || index >= byteCount * 8)
                throw new ArgumentOutOfRangeException(nameof(index));
            int offset = loc.Offset + index / 8;
            int mask = 1 << (index % 8);
            bytes[offset] = enabled ? (byte)(bytes[offset] | mask) : (byte)(bytes[offset] & ~mask);
        }

        public byte[] ExportCharacter(int slot)
        {
            Location loc = Resolve($"Characters[{slot}]");
            return bytes.Skip(loc.Offset).Take(Character_V23.SIZE).ToArray();
        }

        public void ImportCharacter(int slot, byte[] character)
        {
            if (character.Length != Character_V23.SIZE)
                throw new InvalidDataException($"Character export must be {Character_V23.SIZE} bytes.");
            Location loc = Resolve($"Characters[{slot}]");
            Buffer.BlockCopy(character, 0, bytes, loc.Offset, character.Length);
        }

        public void RecountItems()
        {
            int count = 0;
            for (int i = 0; i < SaveData_V23.ITEM_COUNT; i++)
                if ((long)Get($"Items[{i}].Id") != -1) count++;
            WriteNumber(Resolve("ItemCount"), count);
        }

        public void SortItems()
        {
            Location baseLoc = Resolve("Items[0]");
            var entries = Enumerable.Range(0, SaveData_V23.ITEM_COUNT)
                .Select(i => bytes.Skip(baseLoc.Offset + 4 * i).Take(4).ToArray())
                .OrderBy(entry => BitConverter.ToInt16(entry, 0) == -1 ? 1 : 0)
                .ThenBy(entry => BitConverter.ToInt16(entry, 0))
                .ThenBy(entry => entry[2]).ToArray();
            for (int i = 0; i < entries.Length; i++)
                Buffer.BlockCopy(entries[i], 0, bytes, baseLoc.Offset + 4 * i, 4);
            RecountItems();
        }

        public void SortBattalions()
        {
            Location baseLoc = Resolve("Player.Battalions[0]");
            var entries = Enumerable.Range(0, Player_V23.COUNT_BATTALION)
                .Select(i => bytes.Skip(baseLoc.Offset + 8 * i).Take(8).ToArray())
                .OrderBy(entry => entry[6] == Database.BATTALION_COUNT ? 1 : 0)
                .ThenBy(entry => entry[6])
                .ThenBy(entry => BitConverter.ToInt16(entry, 0)).ToArray();
            for (int i = 0; i < entries.Length; i++)
                Buffer.BlockCopy(entries[i], 0, bytes, baseLoc.Offset + 8 * i, 8);
        }

        public IReadOnlyList<object> Differences()
        {
            var result = new List<object>();
            for (int i = HeaderSize; i < bytes.Length; i++)
                if (bytes[i] != original[i])
                    result.Add(new { offset = i, before = original[i], after = bytes[i] });
            return result;
        }

        public byte[] FinishedBytes()
        {
            byte[] result = (byte[])bytes.Clone();
            byte[] sum = BitConverter.GetBytes(Checksum(result));
            Buffer.BlockCopy(sum, 0, result, 0, 4);
            return result;
        }

        public static string Digest(byte[] data)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        private static Regex Pattern(string value) => new Regex(value, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static bool TryGift(string path, out int index)
        {
            Match match = Regex.Match(path, @"^gifts\[(\d+)\]$", RegexOptions.IgnoreCase);
            index = match.Success ? int.Parse(match.Groups[1].Value) : -1;
            if (match.Success && index >= Player_V23.COUNT_GIFT_ITEMS)
                throw new ArgumentOutOfRangeException(nameof(path));
            return match.Success;
        }

        private static uint Checksum(byte[] file)
        {
            uint sum = 0;
            for (int i = HeaderSize; i < file.Length; i++)
                unchecked { sum += file[i]; }
            return sum;
        }

        private static int ElementSize(Type type) => type.IsPrimitive ? Marshal.SizeOf(type) : Marshal.SizeOf(type);

        private Location Resolve(string path)
        {
            Type type = typeof(SaveData_V23);
            int offset = HeaderSize;
            foreach (string part in path.Split('.'))
            {
                Match segment = SegmentPattern.Match(part);
                if (!segment.Success)
                    throw new ArgumentException($"Invalid field path: {path}");
                FieldInfo field = type.GetField(segment.Groups[1].Value, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field == null)
                    throw new ArgumentException($"Unknown field: {path}");
                offset += checked((int)Marshal.OffsetOf(type, field.Name));
                type = field.FieldType;
                if (segment.Groups[2].Success)
                {
                    if (!type.IsArray)
                        throw new ArgumentException($"Field is not an array: {path}");
                    int length = field.GetCustomAttribute<MarshalAsAttribute>().SizeConst;
                    int index = int.Parse(segment.Groups[2].Value);
                    if (index < 0 || index >= length)
                        throw new ArgumentOutOfRangeException(nameof(path), $"Index {index} is outside {field.Name}[{length}].");
                    type = type.GetElementType();
                    offset += checked(index * ElementSize(type));
                }
            }
            int arrayLength = 0;
            if (type.IsArray)
            {
                // The last segment's MarshalAs length is required for bit operations.
                string last = path.Split('.').Last();
                string parent = path.Substring(0, path.Length - last.Length).TrimEnd('.');
                Type parentType = parent.Length == 0 ? typeof(SaveData_V23) : Resolve(parent).Type;
                FieldInfo field = parentType.GetField(last, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                arrayLength = field.GetCustomAttribute<MarshalAsAttribute>().SizeConst;
            }
            return new Location(offset, type, arrayLength);
        }

        private long ReadNumber(Location loc)
        {
            if (loc.Type == typeof(byte)) return bytes[loc.Offset];
            if (loc.Type == typeof(short)) return BitConverter.ToInt16(bytes, loc.Offset);
            if (loc.Type == typeof(ushort)) return BitConverter.ToUInt16(bytes, loc.Offset);
            if (loc.Type == typeof(int)) return BitConverter.ToInt32(bytes, loc.Offset);
            if (loc.Type == typeof(uint)) return BitConverter.ToUInt32(bytes, loc.Offset);
            throw new ArgumentException("The path must point to a numeric field.");
        }

        private void WriteNumber(Location loc, long value)
        {
            byte[] encoded;
            if (loc.Type == typeof(byte)) encoded = new[] { checked((byte)value) };
            else if (loc.Type == typeof(short)) encoded = BitConverter.GetBytes(checked((short)value));
            else if (loc.Type == typeof(ushort)) encoded = BitConverter.GetBytes(checked((ushort)value));
            else if (loc.Type == typeof(int)) encoded = BitConverter.GetBytes(checked((int)value));
            else if (loc.Type == typeof(uint)) encoded = BitConverter.GetBytes(checked((uint)value));
            else throw new ArgumentException("The path must point to a numeric field.");
            Buffer.BlockCopy(encoded, 0, bytes, loc.Offset, encoded.Length);
        }

        private static void Validate(string path, long value)
        {
            if (Regex.IsMatch(path, @"^player\.money$", RegexOptions.IgnoreCase) && (value < 0 || value > Database.MAX_MONEY))
                throw new ArgumentOutOfRangeException(nameof(value), "Money exceeds the GUI limit.");
            if (Regex.IsMatch(path, @"^activities\.reputation$", RegexOptions.IgnoreCase) && (value < 0 || value > Database.MAX_REPUTATION))
                throw new ArgumentOutOfRangeException(nameof(value), "Renown exceeds the GUI limit.");
            if (Regex.IsMatch(path, @"^activities\.instructexp$", RegexOptions.IgnoreCase) && (value < 0 || value > Database.MAX_INSTRUCT_EXP))
                throw new ArgumentOutOfRangeException(nameof(value), "Professor experience exceeds the GUI limit.");
            if (Regex.IsMatch(path, @"\.classlevel\[\d+\]$|\.currentclasslevel$", RegexOptions.IgnoreCase) && (value < 0 || value > Database.MAX_CLASS_LEVEL))
                throw new ArgumentOutOfRangeException(nameof(value), "Class mastery must be 0 or 1.");
            if (Regex.IsMatch(path, @"^activities\.queststatelist\[\d+\]$", RegexOptions.IgnoreCase) && (value < 0 || value > 3))
                throw new ArgumentOutOfRangeException(nameof(value), "Quest state must be 0 through 3.");
        }

        private sealed class Location
        {
            public Location(int offset, Type type, int arrayLength)
            {
                Offset = offset;
                Type = type;
                ArrayLength = arrayLength;
            }
            public int Offset { get; }
            public Type Type { get; }
            public int ArrayLength { get; }
        }
    }
}
