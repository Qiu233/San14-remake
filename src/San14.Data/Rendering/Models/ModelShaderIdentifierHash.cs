using System.Text;

namespace San14.Data;

/// <summary>Shader binding column zero uses native ABD700's first-byte seed.
/// Object and resource names use the separate zero-seeded OriginalResourceNameHash.</summary>
public static class ModelShaderIdentifierHash
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public static uint Compute(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        if (identifier.Length == 0 || identifier.IndexOf('\0') >= 0)
            throw new InvalidDataException("Original model shader identifier must be nonempty and have no NUL.");
        byte[] bytes;
        try { bytes = StrictUtf8.GetBytes(identifier); }
        catch (EncoderFallbackException error)
        { throw new InvalidDataException("Original model shader identifier is not valid UTF-8.", error); }
        return unchecked(ResourceNameHash.Compute(identifier) + (uint)(sbyte)bytes[0]);
    }
}
