using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record PcmWave(int SampleRate, int Channels, byte[] Pcm16, uint? ChannelMask)
{
    public int SampleFrames => Pcm16.Length / checked(Channels * 2);
}

/// <summary>Strict RIFF/WAVE PCM16 reader for the selected-stream decoder result.
/// Other sample encodings and RF64 are explicit errors; metadata chunks are retained
/// by the decoder provenance hash but do not alter samples.</summary>
public static class PcmWaveReader
{
    public const int MaximumWaveBytes = 256 * 1024 * 1024;
    private static ReadOnlySpan<byte> PcmSubFormat =>
        [1, 0, 0, 0, 0, 0, 0x10, 0, 0x80, 0, 0, 0xAA, 0, 0x38, 0x9B, 0x71];

    public static PcmWave Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 12 or > MaximumWaveBytes || !bytes[..4].SequenceEqual("RIFF"u8) ||
            !bytes.Slice(8, 4).SequenceEqual("WAVE"u8) || U32(bytes, 4) != bytes.Length - 8)
            throw new InvalidDataException("Decoded audio must be a complete bounded RIFF/WAVE file.");
        int rate = 0, channels = 0, blockAlign = 0;
        uint? channelMask = null;
        byte[]? samples = null;
        bool formatSeen = false;
        int cursor = 12;
        while (cursor < bytes.Length)
        {
            if (bytes.Length - cursor < 8) throw new InvalidDataException("WAVE chunk header is truncated.");
            ReadOnlySpan<byte> id = bytes.Slice(cursor, 4);
            uint count = U32(bytes, cursor + 4);
            cursor += 8;
            if (count > bytes.Length - cursor) throw new InvalidDataException("WAVE chunk exceeds its RIFF container.");
            ReadOnlySpan<byte> chunk = bytes.Slice(cursor, checked((int)count));
            if (id.SequenceEqual("fmt "u8))
            {
                if (formatSeen || chunk.Length < 16) throw new InvalidDataException("WAVE has a missing or duplicate PCM format.");
                ushort encoding = U16(chunk, 0);
                channels = U16(chunk, 2);
                rate = checked((int)U32(chunk, 4));
                uint averageBytes = U32(chunk, 8);
                blockAlign = U16(chunk, 12);
                if (channels is < 1 or > 8 || rate is < 1 or > 192000 ||
                    U16(chunk, 14) != 16 || blockAlign != channels * 2 ||
                    averageBytes != (long)rate * blockAlign)
                    throw new NotSupportedException("Decoded WAVE must preserve 1–8 channels of PCM16 at the source sample rate.");
                if (encoding == 0xFFFE)
                {
                    if (chunk.Length < 40 || U16(chunk, 16) < 22 || U16(chunk, 18) != 16 ||
                        !chunk.Slice(24, 16).SequenceEqual(PcmSubFormat))
                        throw new NotSupportedException("WAVE extensible audio is not PCM16.");
                    channelMask = U32(chunk, 20);
                }
                else if (encoding != 1) throw new NotSupportedException($"WAVE encoding {encoding} is unsupported.");
                formatSeen = true;
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (samples is not null) throw new InvalidDataException("WAVE has multiple sample data chunks.");
                samples = chunk.ToArray();
            }
            cursor = checked(cursor + (int)count);
            if ((count & 1) != 0)
            {
                if (cursor == bytes.Length) throw new InvalidDataException("WAVE chunk lacks its alignment byte.");
                cursor++;
            }
        }
        if (!formatSeen || samples is null || samples.Length == 0 || samples.Length % blockAlign != 0)
            throw new InvalidDataException("WAVE sample data is missing or not aligned to its PCM channel frames.");
        return new(rate, channels, samples, channelMask);
    }

    private static uint U32(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(at, 4));
    private static ushort U16(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(at, 2));
}
