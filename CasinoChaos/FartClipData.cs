using System;
using System.IO;
using System.Text;

namespace GWYF_CasinoChaos
{
    internal static class FartClipData
    {
        internal const int NormalCount = 6;
        internal const byte DryPuff = 6;
        // Wire indices are stable: the host chooses once and every client loads
        // the same embedded clip. The original downloads remain untouched.
        internal static readonly string[] Names = {
            "apebble-fart-4-228244", "freesound_community-fart-83471",
            "apebble-fart-6-228246", "freesound_community-wet-fart-6139",
            "beanfrog-proud-fart-288263", "apebble-fart-5-228245",
            "freesound_community-dry-puff-39175"
        };
        internal static Stream Open(int index) => typeof(FartClipData).Assembly
            .GetManifestResourceStream("CasinoChaos.Farts." + Names[index] + ".wav");

        internal static float[] Decode(Stream stream, out int rate)
        {
            // Packaged assets are mono PCM16 WAVs; no runtime codec dependency.
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen:true))
            {
                if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not RIFF audio");
                reader.ReadUInt32();
                if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not WAV audio");
                rate = 0;
                while (stream.Position + 8 <= stream.Length)
                {
                    string id = new string(reader.ReadChars(4));
                    uint length = reader.ReadUInt32();
                    long end = stream.Position + length;
                    if (end > stream.Length) throw new InvalidDataException("Truncated WAV chunk");
                    if (id == "fmt ")
                    {
                        if (length < 16 || reader.ReadUInt16() != 1 || reader.ReadUInt16() != 1)
                            throw new InvalidDataException("Expected mono PCM audio");
                        rate = reader.ReadInt32();
                        reader.ReadUInt32();
                        if (reader.ReadUInt16() != 2 || reader.ReadUInt16() != 16 || rate <= 0)
                            throw new InvalidDataException("Expected PCM16 audio");
                    }
                    else if (id == "data")
                    {
                        if (rate == 0 || length == 0 || length % 2 != 0) throw new InvalidDataException("Invalid PCM data");
                        var samples = new float[length / 2];
                        for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32768f;
                        return samples;
                    }
                    stream.Position = end + (length & 1);
                }
                throw new InvalidDataException("No WAV samples");
            }
        }
    }
}
