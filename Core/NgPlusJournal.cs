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
        private const int ClassOffset = SkillOffset + CharacterCount * SkillCount;
        private const int ClassBytesPerCharacter = 13;

        private readonly byte[] file;
        private readonly int player;

        public NgPlusJournal(byte[] file, int playerOffset)
        {
            this.file = file ?? throw new ArgumentNullException(nameof(file));
            player = playerOffset;
            if (player < 0 || player + ClassOffset + CharacterCount * ClassBytesPerCharacter > file.Length)
                throw new InvalidDataException("The NG+ journal extends beyond the save file.");
        }

        public int ProfessorRank => file[player + ProfessorOffset];

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
            int offset = player + ClassOffset + recordIndex * ClassBytesPerCharacter + classId / 8;
            return (file[offset] & (1 << (classId % 8))) != 0;
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
            CheckIndex(recordIndex, CharacterCount, nameof(recordIndex));
            CheckIndex(classId, ClassCount, nameof(classId));
            int offset = player + ClassOffset + recordIndex * ClassBytesPerCharacter + classId / 8;
            byte mask = (byte)(1 << (classId % 8));
            file[offset] = mastered ? (byte)(file[offset] | mask) : (byte)(file[offset] & ~mask);
        }

        public object Snapshot()
        {
            var supports = Enumerable.Range(0, Player_V23.COUNT_SUPPORT)
                .Select(index => new
                {
                    index,
                    name = SupportName(index),
                    maxPoints = GetSupportPoints(index)
                }).ToArray();
            var characters = Enumerable.Range(0, CharacterCount)
                .Select(index => new
                {
                    recordIndex = index,
                    name = CharacterName(index),
                    skillRanks = Enumerable.Range(0, SkillCount).Select(skill => GetSkillRank(index, skill)).ToArray(),
                    masteredClassIds = Enumerable.Range(0, ClassCount)
                        .Where(classId => IsClassMastered(index, classId)).ToArray()
                }).ToArray();
            return new
            {
                professorRank = ProfessorRank,
                supports,
                characters,
                note = "NG+ history is separate from current-run progress. Names follow the game's database order; verify changes in-game."
            };
        }

        private static string CharacterName(int index)
        {
            if (Database.BinaryDatabase == null || index >= Database.BinaryDatabase.CharacterEntries.Count)
                return "Record " + index;
            var entry = Database.BinaryDatabase.CharacterEntries[index];
            if (entry.MainCharacterId < 0) return "Record " + index;
            return entry.UnitName;
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
