namespace San14.Data.Formats;

internal enum DistrictIdentitySourceField { Force, Number, LeaderLow, LeaderHigh }

/// <summary>Byte addresses belong to the source record adapter, not district queries.</summary>
internal static class DistrictIdentitySourceProfile
{
    public const int RecordCount = 52;
    public const int WindowStart = 0x10;
    public const int IdentityWidth = 4;
    public const int ExtensionStart = WindowStart + IdentityWidth;
    public const int ExtensionWidth = 24;
    public const int WindowWidth = IdentityWidth + ExtensionWidth;

    public static DistrictIdentitySourceField IdentityField(int offset) =>
        (DistrictIdentitySourceField)(offset - WindowStart);
}
