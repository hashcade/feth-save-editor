using System;
using System.IO;
using System.Linq;
using SaveEditor;
using SaveEditor.Structs;

namespace FethEditor.Core
{
    public sealed class SystemBuffer
    {
        private const int HeaderSize = SystemSave.SIZE_SAVE_HEADER;
        private const int FlagsOffset = HeaderSize + SystemSaveData_V7.COUNT_SAVES * SaveFileInfo.SIZE;
        private readonly byte[] _bytes;

        private SystemBuffer(byte[] bytes) => _bytes = bytes;

        public static SystemBuffer Open(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length != SystemSave.SIZE_SAVE_V7 ||
                BitConverter.ToUInt32(bytes, 4) != SystemSave.CURRENT_VERSION ||
                BitConverter.ToUInt32(bytes, 8) != bytes.Length)
                throw new InvalidDataException("Expected a version-7 system save from game 1.1.0–1.2.0.");
            if (BitConverter.ToUInt32(bytes, 0) != Util.CalcChecksum32(bytes.Skip(HeaderSize).ToArray()))
                throw new InvalidDataException("System save checksum is invalid.");
            return new SystemBuffer(bytes);
        }

        public SystemSaveData_V7 Data => Util.ReadStructure<SystemSaveData_V7>(
            _bytes.Skip(HeaderSize).ToArray());

        public bool GetFlag(int index)
        {
            CheckFlagIndex(index);
            return (_bytes[FlagsOffset + index / 8] & (1 << (index % 8))) != 0;
        }

        public void SetFlag(int index, bool enabled)
        {
            CheckFlagIndex(index);
            int offset = FlagsOffset + index / 8;
            int mask = 1 << (index % 8);
            _bytes[offset] = enabled ? (byte)(_bytes[offset] | mask) : (byte)(_bytes[offset] & ~mask);
        }

        public byte[] FinishedBytes()
        {
            byte[] result = (byte[])_bytes.Clone();
            BitConverter.GetBytes(Util.CalcChecksum32(result.Skip(HeaderSize).ToArray()))
                .CopyTo(result, 0);
            return result;
        }

        private static void CheckFlagIndex(int index)
        {
            if (index < 0 || index >= SystemSaveData_V7.COUNT_FLAGS)
                throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
