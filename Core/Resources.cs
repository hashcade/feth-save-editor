using System;
using System.IO;

namespace SaveEditor.Properties
{
    internal static class Resources
    {
        internal static byte[] fixed_persondata_bin => Read("fixed_persondata.bin.gz");
        internal static byte[] fixed_classdata_bin => Read("fixed_classdata.bin.gz");
        internal static byte[] fixed_data_bin => Read("fixed_data.bin.gz");
        internal static byte[] msgdata_bin => Read("msgdata.bin.gz");

        private static byte[] Read(string name)
        {
            using Stream stream = typeof(Resources).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidDataException("Missing embedded database: " + name);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }
}
