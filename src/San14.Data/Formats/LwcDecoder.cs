using System.Buffers.Binary;

namespace San14.Data.Formats;

/// <summary>Decoder for the LWC stream present inside the installed .s14 files.</summary>
public static class LwcDecoder
{
    private const int HeaderLength = 12 + 256;
    private const int MaximumDecodedLength = 128 * 1024 * 1024;

    public static byte[] Decode(ReadOnlySpan<byte> input)
    {
        if (input.Length < HeaderLength || !input[..4].SequenceEqual("LWC\x1a"u8))
            throw new InvalidDataException("LWC header is missing or truncated.");

        uint decodedSize = BinaryPrimitives.ReadUInt32LittleEndian(input[4..8]);
        uint streamSize = BinaryPrimitives.ReadUInt32LittleEndian(input[8..12]);
        if (decodedSize > MaximumDecodedLength || streamSize != input.Length - HeaderLength)
            throw new InvalidDataException("LWC size fields do not match the source file.");

        ReadOnlySpan<byte> symbols = input.Slice(12, 256);
        var bits = new BitInput(input[HeaderLength..]);
        var output = new byte[(int)decodedSize];
        int written = 0;

        while (written < output.Length)
        {
            int token = bits.ReadCode();
            if (token < 256)
            {
                output[written++] = symbols[token];
                continue;
            }

            int distance = token - 256;
            int length = checked(bits.ReadCode() + 3);
            if (distance < 1 || distance > written || length > output.Length - written)
                throw new InvalidDataException($"Invalid LWC back reference at output offset {written}.");

            for (int i = 0; i < length; i++)
            {
                output[written] = output[written - distance];
                written++;
            }
        }

        return output;
    }

    private ref struct BitInput(ReadOnlySpan<byte> bytes)
    {
        private readonly ReadOnlySpan<byte> _bytes = bytes;
        private int _position;

        private int ReadBit()
        {
            if (_position >= _bytes.Length * 8)
                throw new InvalidDataException("LWC bitstream ended before decoded output was complete.");
            int result = (_bytes[_position / 8] >> (7 - _position % 8)) & 1;
            _position++;
            return result;
        }

        public int ReadCode()
        {
            int prefix = 0;
            int width = 0;
            int bit;
            do
            {
                if (++width > 30)
                    throw new InvalidDataException("LWC token width exceeds the supported range.");
                bit = ReadBit();
                prefix = (prefix << 1) | bit;
            } while (bit != 0);

            int tail = 0;
            for (int i = 0; i < width; i++)
                tail = (tail << 1) | ReadBit();
            return checked(prefix + tail);
        }
    }
}
