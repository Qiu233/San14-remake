using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

public enum OriginalCeAreaDevelopmentField
{
    Commerce = 0x3C,
    Agriculture = 0x3E,
    Barracks = 0x40
}

/// <summary>Current queries used by the three native progress setters. Null is
/// unresolved input; a reference with no physical slot is a known null pointer.</summary>
public interface IOriginalCeAreaDevelopmentWriteContext
{
    ushort? ReadAreaProgress(OriginalAreaReference area, OriginalCeAreaDevelopmentField field);
    void WriteAreaProgress(OriginalAreaReference area, OriginalCeAreaDevelopmentField field, ushort value);
    ushort? ReadAreaRepresentativeHexIndex(OriginalAreaReference area);
    OriginalHexReference? ReadHexTableReference(int physicalSlot);
    byte? ReadHexForceSlot(OriginalHexReference hex);
    OriginalForceReference? ReadForceTableReference(int physicalSlot);
    bool? ReadForceNativeValidity(OriginalForceReference force);
    int? ReadForcePhysicalIdentity(OriginalForceReference force);
    short? ReadForceDevelopmentWord1BE(OriginalForceReference force);
    int? ReadCurrentDevelopmentLevelChangeCoefficient();
    void WriteForceDevelopmentWord1BE(OriginalForceReference force, short value);
}

public sealed record OriginalCeAreaDevelopmentWriteResult(ushort Progress, int? PreviousLevel,
    int? CurrentLevel, int? ChangedForceSlot, short? ChangedForceWord1BE);

/// <summary>The current SC/TC setters for area3C/3E/40, including their optional
/// level-change side effect. This callable writer does not certify startup/event
/// completion or choose a new-game input value.</summary>
public sealed class OriginalCeInitialAreaDevelopment
{
    public const string ProfileId = "san14:ce-area-development-writers-20261002";
    public const string VerifiedScDecodedTextSha256 =
        "D7F3831C369A76148D87370D91EF0A8A43487D5E13A383886C68A6D50591E8F6";
    public const string VerifiedTcDecodedTextSha256 =
        "FFBD504FC3DEA6A218EF4E8530A65761296C17191F7D675AB3C49DF64D950766";
    private readonly int[] _thresholds;

    public string Locale { get; }
    public string SourceExecutableSha256 { get; }
    public string DecodedTextSha256 => Locale == "SC" ? VerifiedScDecodedTextSha256 : VerifiedTcDecodedTextSha256;
    public uint AreaResetRva => Locale == "SC" ? 0x226BC0u : 0x226950u;
    public uint StrategyWorkerRva => Locale == "SC" ? 0x4C6F50u : 0x4C6F20u;
    public uint LevelChangeCoefficientRva => Locale == "SC" ? 0x1A4A874u : 0x1A49844u;
    /// <summary>Image initial value only. A write consumes the context's current
    /// value, because the parameter may have changed after image loading.</summary>
    public int ImageInitialLevelChangeCoefficient { get; }
    public IReadOnlyList<int> LevelThresholds { get; }

    private OriginalCeInitialAreaDevelopment(string locale, string sha, int[] thresholds, int coefficient)
    {
        Locale = locale; SourceExecutableSha256 = sha; _thresholds = thresholds;
        ImageInitialLevelChangeCoefficient = coefficient;
        LevelThresholds = Array.AsReadOnly(_thresholds);
    }

    public static OriginalCeInitialAreaDevelopment LoadCurrent(string sourceDirectory, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        (string file, string expected, uint thresholds, uint coefficient) = locale switch
        {
            "SC" => ("SAN14CE_SC.exe", OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256, 0x13A8DACu, 0x1A4A874u),
            "TC" => ("SAN14CE_TC.exe", OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256, 0x13A7DACu, 0x1A49844u),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), "Area development writers are verified only for current CE SC/TC.")
        };
        byte[] image = File.ReadAllBytes(Path.Combine(sourceDirectory, file));
        string actual = Convert.ToHexString(SHA256.HashData(image));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Installed {file} SHA-256 changed: {actual}; {ProfileId} requires its verified executable.");
        int pe = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x3C, 4));
        if (BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(pe, 4)) != 0x4550 ||
            BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(pe + 4, 2)) != 0x8664 ||
            BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(pe + 24, 2)) != 0x20B ||
            BinaryPrimitives.ReadUInt64LittleEndian(image.AsSpan(pe + 48, 8)) != 0x140000000)
            throw new InvalidDataException("Area development source is not the verified AMD64 PE32+ image.");
        int[] limits = Enumerable.Range(0, 4).Select(index => ReadInt32(thresholds + (uint)(index * 4))).ToArray();
        if (!limits.SequenceEqual(new[] { 100, 300, 600, 1000 }))
            throw new InvalidDataException("Current area development source threshold table differs from its verified bytes.");
        return new(locale, actual, limits, ReadInt32(coefficient));

        int ReadInt32(uint rva)
        {
            int optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(pe + 20, 2));
            int count = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(pe + 6, 2));
            for (int index = 0; index < count; index++)
            {
                int at = pe + 24 + optionalSize + index * 40;
                uint start = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(at + 12, 4));
                uint rawSize = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(at + 16, 4));
                if (rva < start || (ulong)rva + 4 > (ulong)start + rawSize) continue;
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(at + 20, 4));
                return BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(checked((int)(offset + rva - start)), 4));
            }
            throw new InvalidDataException($"Area development source RVA {rva:X} has no raw PE section.");
        }
    }

    public uint SetterRva(OriginalCeAreaDevelopmentField field) => (Locale, field) switch
    {
        ("SC", OriginalCeAreaDevelopmentField.Commerce) => 0x240EA0,
        ("SC", OriginalCeAreaDevelopmentField.Agriculture) => 0x241440,
        ("SC", OriginalCeAreaDevelopmentField.Barracks) => 0x241E20,
        ("TC", OriginalCeAreaDevelopmentField.Commerce) => 0x240C20,
        ("TC", OriginalCeAreaDevelopmentField.Agriculture) => 0x2411C0,
        ("TC", OriginalCeAreaDevelopmentField.Barracks) => 0x241BA0,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    public int ReadLevel(ushort progress)
    {
        for (int index = 0; index < _thresholds.Length; index++)
            if (progress < _thresholds[index]) return index + 1;
        return 5;
    }

    public OriginalCeAreaDevelopmentWriteResult WriteCurrentProgress(OriginalAreaReference area,
        OriginalCeAreaDevelopmentField field, int requestedValue, int notificationFlag,
        IOriginalCeAreaDevelopmentWriteContext context)
    {
        ArgumentNullException.ThrowIfNull(area); ArgumentNullException.ThrowIfNull(context);
        _ = SetterRva(field);
        if (!area.PhysicalSlot.HasValue)
            throw new InvalidDataException("Native area progress setter dereferences a null receiver.");
        //Flag0 does not read the previous word, Hex or force. Every nonzero
        //flag, including a negative value, takes the level-change branch.
        int? oldLevel = notificationFlag != 0 ? ReadLevel(Known(context.ReadAreaProgress(area, field), "previous progress")) : null;
        ushort progress = (ushort)Math.Clamp(requestedValue, 0, 1000);
        context.WriteAreaProgress(area, field, progress);
        if (notificationFlag == 0) return new(progress, null, null, null, null);
        int newLevel = ReadLevel(progress);
        if (newLevel == oldLevel) return new(progress, oldLevel, newLevel, null, null);

        ushort hexId = Known(context.ReadAreaRepresentativeHexIndex(area), "area3A");
        OriginalHexReference hex = Pointer(context.ReadHexTableReference(hexId <= 48399 ? hexId : 0), "Hex lookup");
        if (!hex.PhysicalSlot.HasValue)
            throw new InvalidDataException("Native area level-change branch dereferences a null Hex.");
        byte forceId = Known(context.ReadHexForceSlot(hex), "Hex14");
        OriginalForceReference force = Pointer(context.ReadForceTableReference(forceId <= 51 ? forceId : 0), "force lookup");
        if (!Known(context.ReadForceNativeValidity(force), "force validity"))
            return new(progress, oldLevel, newLevel, null, null);
        if (Known(context.ReadForcePhysicalIdentity(force), "force slot12") == 0)
            return new(progress, oldLevel, newLevel, null, null);
        //The exclusion helper invokes slot12 again; changing queries may not
        //reuse the identity consumed by the preceding zero guard.
        int freshIdentity = Known(context.ReadForcePhysicalIdentity(force), "fresh force slot12");
        if (freshIdentity == 51 || unchecked((uint)(freshIdentity - 46)) <= 4u)
            return new(progress, oldLevel, newLevel, null, null);
        short current = Known(context.ReadForceDevelopmentWord1BE(force), "force1BE");
        int coefficient = Known(context.ReadCurrentDevelopmentLevelChangeCoefficient(), "current level-change coefficient");
        int updated = unchecked(current + (newLevel - oldLevel!.Value) * coefficient);
        short bounded = (short)Math.Clamp(updated, -1000, 1000);
        context.WriteForceDevelopmentWord1BE(force, bounded);
        return new(progress, oldLevel, newLevel, force.PhysicalSlot, bounded);
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current area development {field} is unknown.");
    private static T Pointer<T>(T? value, string field) where T : class => value
        ?? throw new InvalidDataException($"Current area development {field} is unknown.");
}
