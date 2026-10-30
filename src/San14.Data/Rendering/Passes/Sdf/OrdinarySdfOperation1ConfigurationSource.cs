using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The selected Host's signed slot overrides. The normal startup
/// passes an actual override block containing zero; the bare Host constructor
/// initially writes -1. They are different source lifecycles.</summary>
public readonly record struct OrdinarySdfHostSlotOverrides(
    int SlotCapacity748, int OtherSlotCapacity74C);

/// <summary>A skipped D1B0A0 preserves the existing Frame3D8 pool. It does not
/// publish an empty bank, zero operation count, dependency readiness or pixels.</summary>
public sealed record OrdinarySdfOperation1PoolPreparation(
    bool ConfigurePool, OrdinarySdfOperation1PoolFields? Fields,
    uint SourceMode, uint WidthDivisor, uint HeightDivisor, string? SkippedReason);

/// <summary>One import of the selected View/Common declarations. PrepareCurrent
/// only applies their actual source gates and Host inputs; it does not reparse
/// resources or allocate a provider on a frame/query.</summary>
public sealed class OrdinarySdfOperation1ConfigurationSource
{
    private OrdinarySdfOperation1ConfigurationSource(LoadedDefaultView0Source view,
        LoadedObjects common, uint mode, uint slots, uint records, uint volumeGate)
        => (ViewSource, CommonSource, SourceMode, SettingsSlotCapacity,
            SettingsRecordCapacity, VolumeGate) = (view, common, mode, slots, records, volumeGate);

    public LoadedDefaultView0Source ViewSource { get; }
    public LoadedObjects CommonSource { get; }
    public uint SourceMode { get; }
    public uint SettingsSlotCapacity { get; }
    public uint SettingsRecordCapacity { get; }
    public uint VolumeGate { get; }

    public static OrdinarySdfOperation1ConfigurationSource Load(
        LoadedDefaultView0Source actualView0, LoadedObjects actualCommon)
    {
        ArgumentNullException.ThrowIfNull(actualView0);
        ArgumentNullException.ThrowIfNull(actualCommon);
        var view = actualView0.ViewDefinitions.Document.Objects.Single(
            value => value.ObjectId == actualView0.ViewObjectId);
        var common = actualCommon.Document.Objects.Single(
            value => value.ObjectId == CommonRenderResources.SourceObjectId);
        if (view.Kind != DokObjectKind.Inline || view.ClassId != 0x97AF26FD ||
            view.Properties.Count != 52 || common.Kind != DokObjectKind.Inline ||
            common.ClassId != 0xEF3D849D || common.Properties.Count != 102)
            throw new InvalidDataException("SDF pool configuration requires the selected View0 and CommonViewSetting declarations.");
        uint mode = Word(view, 25, 0x739F83E1, 5);
        uint slots = Word(view, 42, 0x4E19B11B, 5);
        uint volumeGate = Word(view, 43, 0xAE5607AF, 5);
        // D20D50 derives this BEFORE applying the independent Common overrides.
        uint records = unchecked(slots * 8u);
        int commonSlots = unchecked((int)Word(common, 66, 0x5CEEF06B, 4));
        int commonRecords = unchecked((int)Word(common, 67, 0xCF4FB686, 4));
        // D223C0 normalizes every negative signed setting to -1. Both mean
        // preserve the View value at D20D50; zero is a real overriding value.
        if (commonSlots >= 0) slots = (uint)commonSlots;
        if (commonRecords >= 0) records = (uint)commonRecords;
        return new(actualView0, actualCommon, mode, slots, records, volumeGate);
    }

    public OrdinarySdfOperation1PoolPreparation PrepareCurrent(
        uint actualWidth, uint actualHeight, uint actualHostFlags3E4,
        int actualHostSlot748)
    {
        // D20D50 computes divisors from Settings slots BEFORE Host748 wins.
        uint divisor = SourceMode == 1 && (actualHostFlags3E4 & 3) == 1 &&
            SettingsSlotCapacity != 0 ? 64u : 0u;
        uint slots = actualHostSlot748 >= 0 ? (uint)actualHostSlot748 : SettingsSlotCapacity;
        if (divisor == 0 || VolumeGate == 0)
            return new(false, null, SourceMode, divisor, divisor,
                "source.operation1.mode_host_or_volume_gate_skipped");
        if (slots == 0)
            return new(false, null, SourceMode, divisor, divisor,
                "source.operation1.slot_capacity_zero_preserves_pool");
        uint width = unchecked((uint)(long)MathF.Ceiling((float)actualWidth / divisor));
        uint height = unchecked((uint)(long)MathF.Ceiling((float)actualHeight / divisor));
        return new(true, new OrdinarySdfOperation1PoolFields(slots, SettingsRecordCapacity, width, height, 16),
            SourceMode, divisor, divisor, null);
    }

    /// <summary>13D6D0/13D470 pass a present 24B override block (-1,0,0)
    /// in qwords. 12EDC0/12EB60 copy its final qword to Host748/74C.</summary>
    public static OrdinarySdfHostSlotOverrides ReadFreshNormalHostSlotOverrides(
        MainMapContextOwnerSource actualFreshHost)
    {
        ArgumentNullException.ThrowIfNull(actualFreshHost);
        _ = RenderRuleIdentity.For("ordinary-sdf-host-pool", actualFreshHost.Locale);
        return new(0, 0);
    }

    private static uint Word(DokObject owner, int ordinal, uint hash, uint type)
    {
        var field = owner.Properties[ordinal];
        if (field.Index != ordinal || field.NameHash != hash || field.TypeCode != type ||
            field.ElementCount != 1 || field.RawValue.Length != 4)
            throw new InvalidDataException("SDF pool source field differs from its actual loader ordinal/type.");
        return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
    }
}
