namespace San14.Core;

/// <summary>Encoding of the selected office leadership bonus vector.</summary>
public enum OfficeBonusEncoding
{
    RotateByteXorOffice = 1,
}

public static class OfficeBonusCodec
{
    public static byte? Decode(OfficeBonusEncoding? encoding, byte encoded, int rotation, int officeId)
    {
        if (encoding != OfficeBonusEncoding.RotateByteXorOffice) return null;
        uint value = encoded;
        uint decoded = ((value >> ((8 - rotation) & 31)) | (value << (rotation & 31))) ^ (byte)officeId;
        // The bonus consumer takes the low byte of the wide rotation result.
        return unchecked((byte)decoded);
    }
}
