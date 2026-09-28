using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SaveEditor
{
    public static class MemoryHelper
    {
        public enum GameVersion
        {
            v100 = 0,
            v101 = 1,
            v102 = 2,
            v110 = 3,
            v111 = 4,
            v120 = 5
        }

        private static List<ulong> SaveDataPtr = new List<ulong>()
        {
            0x19CF6E0, //v1.0.0
            0x19D76F0, //v1.0.1
            0x0, //v1.0.2
            0x0, //v1.1.0
            0x0, //v1.1.1
            0x1B12190, //v1.2.0
        };

        private static List<ulong> BattlePtr = new List<ulong>()
        {
            0x0, //v1.0.0
            0x1988EF0, //v1.0.1
            0x0, //v1.0.2
            0x0, //v1.1.0
            0x0, //v1.1.1
            0x0, //v1.2.0
        };

        public static void GetMemoryAddresses(ulong MainAddress, GameVersion version)
        {
            string result = "";

            ulong savePtr = SaveDataPtr[(int)version], battlePtr = BattlePtr[(int)version];
            ulong saveDataPtr = MainAddress + savePtr + 0x10;

            result += $"SavePointer: {MainAddress + savePtr:X}\r\n";
            result += $"BattlePointer: {MainAddress + battlePtr:X}\r\n";
            result += $"TeachingMotivationCost: {MainAddress + 0x18EDAE0:X}\r\n";
            result += $"TeachingActivityPointCost: {MainAddress + 0x18ED8C8:X}\r\n";
            result += $"InitMotivation: {MainAddress + 0x18EDAEC:X}\r\n";

            result += $"Exp Address: {MainAddress + 0x3BC418:X}\r\n";
            result += $"Skill Exp Address: {MainAddress + 0x3BC63C:X},{MainAddress + 0x38D434:X}\r\n";
            result += $"Stats Address1: {MainAddress + 0x38CE08:X},{MainAddress + 0x38CF94:X}\r\n";
            result += $"Stats Address1: {MainAddress + 0x38C1F0:X},{MainAddress + 0x38C3F0:X}\r\n";

            result += GetCharacterAddresses(saveDataPtr, version);

            Clipboard.SetText(result);
        }

        private static string GetCharacterAddresses(ulong MainPointer, GameVersion version)
        {
            ulong baseAddress = MainPointer + 0x640;

            int CharacterSize = 0;

            switch(version)
            {
                case GameVersion.v100:
                case GameVersion.v101:
                case GameVersion.v102:
                    CharacterSize = SaveEditor.Structs.Character_V13.SIZE;
                    break;

                case GameVersion.v110:
                case GameVersion.v111:
                case GameVersion.v120:
                    CharacterSize = SaveEditor.Structs.Character_V23.SIZE;
                    break;
            }

            string result = "";

            for (int i = 0; i < 35; i++)
            {
                result += $"{baseAddress + (uint) (i * CharacterSize):X} - {Database.GetUnitName(i + 1)}";
                result += $", EXP: {baseAddress + (uint) (i * CharacterSize) + 0x30:X}";
                result += $", Motivation: {baseAddress + (uint) (i * CharacterSize) + 0xC8:X}\r\n";
            }

            return result;
        }


    }
}
