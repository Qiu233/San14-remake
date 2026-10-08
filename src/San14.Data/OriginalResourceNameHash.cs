using System.Text;

namespace San14.Data;

/// <summary>The original resource-name hash: signed UTF-8 bytes weighted by successive powers of 31.</summary>
public static class OriginalResourceNameHash
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static uint Compute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.IndexOf('\0') >= 0)
            throw new InvalidDataException("Original resource names cannot contain a NUL terminator.");
        byte[] bytes;
        try { bytes = StrictUtf8.GetBytes(name); }
        catch (EncoderFallbackException error)
        { throw new InvalidDataException("Original resource name is not valid UTF-8.", error); }
        uint weight = 1, result = 0;
        unchecked
        {
            foreach (byte value in bytes)
            {
                weight *= 31;
                result += (uint)(sbyte)value * weight;
            }
        }
        return result;
    }
}
