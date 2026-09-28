using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SaveEditor;
using SaveEditor.Structs;

namespace FethEditor.Core
{
    public sealed class NgPlusJournal
    {
        public const int CharacterCount = 45;
        public const int SkillCount = 11;
        public const int ClassCount = 100;
        private const int ProfessorOffset = 0x17CE;
        private const int SupportOffset = 0x1576;
        private const int SkillOffset = 0x17D8;
        private const int ClassOffset = 0x19CC;
        private const int ClassBytesPerCharacter = 13;
        private const int BaseCharacterCount = 35;
        private const int AdditionalClassOffset = 0x1B93;
        private const int AdditionalClassBits = 100;

        private readonly byte[] file;
        private readonly int player;

        public NgPlusJournal(byte[] file, int playerOffset)
        {
            this.file = file ?? throw new ArgumentNullException(nameof(file));
            player = playerOffset;
            if (player < 0 || player + AdditionalClassOffset +
                (CharacterCount - BaseCharacterCount) * AdditionalClassBits / 8 > file.Length)
                throw new InvalidDataException("The NG+ journal extends beyond the save file.");
        }

        public int ProfessorRank => file[player + ProfessorOffset];

        public string GetCharacterName(int recordIndex)
        {
            CheckIndex(recordIndex, CharacterCount, nameof(recordIndex));
            return CharacterName(recordIndex);
        }

        public string GetSupportName(int index)
        {
            CheckIndex(index, Player_V23.COUNT_SUPPORT, nameof(index));
            return SupportName(index);
        }

        public int GetSupportPoints(int index)
        {
            CheckIndex(index, Player_V23.COUNT_SUPPORT, nameof(index));
            int offset = player + SupportOffset + index * 2;
            return file[offset] | file[offset + 1] << 8;
        }

        public int GetSkillRank(int recordIndex, int skillIndex)
        {
            CheckIndex(recordIndex, CharacterCount, nameof(recordIndex));
            CheckIndex(skillIndex, SkillCount, nameof(skillIndex));
            return file[player + SkillOffset + recordIndex * SkillCount + skillIndex];
        }

        public bool IsClassMastered(int recordIndex, int classId)
        {
            CheckIndex(recordIndex, CharacterCount, nameof(recordIndex));
            CheckIndex(classId, ClassCount, nameof(classId));
            var (offset, mask) = ClassBit(recordIndex, classId);
            return (file[offset] & mask) != 0;
        }

        public void SetProfessorRank(int rank)
        {
            if (rank < 0 || rank > 9) throw new ArgumentOutOfRangeException(nameof(rank));
            file[player + ProfessorOffset] = (byte)rank;
        }

        public void SetSupportPoints(int index, int points)
        {
            CheckIndex(index, Player_V23.COUNT_SUPPORT, nameof(index));
            if (points < 0 || points > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(points));
            int offset = player + SupportOffset + index * 2;
            file[offset] = (byte)points;
            file[offset + 1] = (byte)(points >> 8);
        }

        public void SetSkillRank(int recordIndex, int skillIndex, int rank)
        {
            CheckIndex(recordIndex, CharacterCount, nameof(recordIndex));
            CheckIndex(skillIndex, SkillCount, nameof(skillIndex));
            if (rank < 0 || rank > 11) throw new ArgumentOutOfRangeException(nameof(rank));
            file[player + SkillOffset + recordIndex * SkillCount + skillIndex] = (byte)rank;
        }

        public void SetClassMastered(int recordIndex, int classId, bool mastered)
        {
            throw new NotSupportedException("NG+ class flag semantics are not verified; writing them is disabled.");
        }

        private (int offset, byte mask) ClassBit(int recordIndex, int classId)
        {
            CheckIndex(recordIndex, CharacterCount, nameof(recordIndex));
            CheckIndex(classId, ClassCount, nameof(classId));
            if (recordIndex < BaseCharacterCount)
                return (player + ClassOffset + recordIndex * ClassBytesPerCharacter + classId / 8,
                    (byte)(1 << (classId % 8)));

            int bit = (recordIndex - BaseCharacterCount) * AdditionalClassBits + classId;
            return (player + AdditionalClassOffset + bit / 8, (byte)(1 << (bit % 8)));
        }

        public object Snapshot()
        {
            var supports = Enumerable.Range(0, Player_V23.COUNT_SUPPORT)
                .Select(index => new
                {
                    index,
                    name = GetSupportName(index),
                    maxPoints = GetSupportPoints(index)
                }).ToArray();
            var characters = Enumerable.Range(0, CharacterCount)
                .Select(index => new
                {
                    recordIndex = index,
                    name = GetCharacterName(index),
                    skillRanks = Enumerable.Range(0, SkillCount).Select(skill => GetSkillRank(index, skill)).ToArray(),
                    rawClassFlagIds = Enumerable.Range(0, ClassCount)
                        .Where(classId => IsClassMastered(index, classId)).ToArray()
                }).ToArray();
            return new
            {
                professorRank = ProfessorRank,
                supports,
                characters,
                note = "NG+ history is separate from current-run progress. Class flags are raw, unverified data and cannot be edited."
            };
        }

        private static string CharacterName(int index)
        {
            if (Database.BinaryDatabase == null)
                return "Record " + index;
            var entries = Database.BinaryDatabase.CharacterEntries;
            string name = Enumerable.Range(0, entries.Count)
                .Where(id => entries[id].MainCharacterId == index)
                .OrderByDescending(id => id >= 1000)
                .Select(id => Database.GetUnitName(id))
                .FirstOrDefault();
            return string.IsNullOrWhiteSpace(name) ? "Record " + index : name;
        }

        private static string SupportName(int index)
        {
            if (Database.BinaryDatabase == null || index >= Database.BinaryDatabase.SupportTalkEntries.Count)
                return "Support " + index;
            return Database.GetSupportTalkName(index);
        }

        private static void CheckIndex(int index, int count, string name)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(name);
        }
    }
}
