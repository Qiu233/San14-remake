using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual default370 WorldPQ constructor/loader fields retained
/// before a later World mask or eligibility setter. This is independent of
/// ordinary visibility, a selected shader state and a GPU draw receipt.</summary>
public sealed class LoadedOriginalDeferredEarlyShadowWorldCallerSource
{
    internal LoadedOriginalDeferredEarlyShadowWorldCallerSource(uint definition, uint model,
        uint exclusion, int caster, string identity)
        => (DefinitionObjectId, ModelObjectId, Exclusion78, WorldCasterFlags10C, SourceIdentity) =
            (definition, model, exclusion, caster, identity);
    public uint DefinitionObjectId { get; }
    public uint ModelObjectId { get; }
    public uint Exclusion78 { get; }
    public int WorldCasterFlags10C { get; }
    public uint FreshMask328 => uint.MaxValue;
    public uint FreshMask32C => uint.MaxValue;
    public uint FreshMask330 => uint.MaxValue;
    public int FreshEligibilitySelector334 => -1;
    public string SourceIdentity { get; }
    public string SourceEvidence => SourceIdentity + ";TC:D9EC00/D9EC0B:328/32C/330/334;" +
        "DA6E64/DA6FA0/DA9147:property31->10C;DA6D40/DA7E48/D9E928:property9->78;DB2ED0";

    /// <summary>Use the actual retained fresh source receiver. A current
    /// overridden receiver supplies its complete fields to ReadCurrentMasks.</summary>
    public OriginalDeferredEarlyShadowCurrentWorldCallerFields CaptureRetainedFresh()
        => new(FreshMask328, FreshMask32C, FreshMask330, true, Exclusion78, WorldCasterFlags10C, SourceEvidence);
}

public readonly record struct OriginalDeferredEarlyShadowCurrentWorldCallerFields(
    uint Mask328, uint Mask32C, uint Mask330, bool ActualEligibilityV408,
    uint Exclusion78, int WorldCasterFlags10C, string CurrentWriterEvidence);

public readonly record struct OriginalDeferredEarlyShadowCurrentCallerMasks(
    uint FirstOrdinary, uint SecondOrdinary, uint Shadow,
    bool FirstOrdinarySlotEligible, bool SecondOrdinarySlotEligible, bool ShadowSlotEligible);

/// <summary>Three currently reached C394F0 tail fields. They come from the
/// first nonexcluded, nonnull kind1 LightData in the complete actual plain
/// queue, which need not be View1BD0's selected record.</summary>
public sealed record OriginalDeferredEarlyShadowCurrentCasterLightRecord(
    OriginalMapWorldKind1CurrentRecord Record, byte F1C, byte F1F, byte F20,
    bool RecordShadowWrapper0Present, string CurrentSourceEvidence);

public sealed class OriginalDeferredEarlyShadowCurrentCasterLightRows
{
    internal OriginalDeferredEarlyShadowCurrentCasterLightRows(OriginalMapWorldKind1CurrentQueue queue,
        OriginalDeferredEarlyShadowCurrentCasterLightRecord[] rows)
        => (Queue, Rows) = (queue, Array.AsReadOnly(rows));
    public OriginalMapWorldKind1CurrentQueue Queue { get; }
    public IReadOnlyList<OriginalDeferredEarlyShadowCurrentCasterLightRecord> Rows { get; }

    public OriginalDeferredEarlyShadowCurrentCasterLightRecord? SelectFirstCurrentKind1(uint actualExclusion78And404)
        => Rows.FirstOrDefault(row => (row.Record.Mask78 & actualExclusion78And404) == 0);
}

public readonly record struct OriginalDeferredEarlyShadowFirstCasterAdmission(
    OriginalDeferredEarlyShadowCurrentCallerMasks Masks,
    OriginalDeferredEarlyShadowCurrentCasterLightRecord? FirstReachedLight,
    uint FirstFamilyAllowedPassMask, uint SecondFamilyDeclaredPassMask);

public static class OriginalDeferredEarlyShadowCasterAdmissionSource
{
    private static readonly ConditionalWeakTable<OriginalMapStaticModelAsset, LoadedOriginalDeferredEarlyShadowWorldCallerSource> statics = new();
    private static readonly ConditionalWeakTable<OriginalArmyPrimitivePublicationSource, LoadedOriginalDeferredEarlyShadowWorldCallerSource> armies = new();

    public static LoadedOriginalDeferredEarlyShadowWorldCallerSource LoadStatic(OriginalMapStaticModelAsset actualAsset)
    {
        ArgumentNullException.ThrowIfNull(actualAsset);
        return statics.GetValue(actualAsset, static asset =>
        {
            var catalog = asset.TextureResources.Catalog ?? throw new InvalidDataException("Caster WorldPQ catalog is absent.");
            var definition = asset.TextureResources.CatalogDefinition ?? throw new InvalidDataException("Caster WorldPQ definition is absent.");
            return ReadImported(catalog, definition.ObjectId, asset.Reference.ModelObject.ObjectId);
        });
    }

    public static LoadedOriginalDeferredEarlyShadowWorldCallerSource LoadArmy(OriginalArmyPrimitivePublicationSource actualSource)
    {
        ArgumentNullException.ThrowIfNull(actualSource);
        return armies.GetValue(actualSource, static source => ReadImported(source.Catalog.Catalog,
            source.Category.DefinitionId, source.Category.ModelObjectId));
    }

    /// <summary>Load/reload boundary only. Later requests retain these fields;
    /// this helper never rereads RDB/EXE or hashes an internal request.</summary>
    public static LoadedOriginalDeferredEarlyShadowWorldCallerSource ReadImported(LoadedObjects actualCatalog,
        uint actualDefinitionObjectId, uint actualModelObjectId)
    {
        ArgumentNullException.ThrowIfNull(actualCatalog);
        if (Convert.ToHexString(SHA256.HashData(actualCatalog.Source.Bytes)) != actualCatalog.Source.DecodedSha256)
            throw new InvalidDataException("Caster catalog differs at import.");
        var world = DokReader.Read(actualCatalog.Source.Bytes).Objects.Single(o =>
            o.ObjectId == actualDefinitionObjectId && o.ClassId == OriginalScenePlacements.ModelWorldPqClass && o.Kind == DokObjectKind.Inline);
        uint Word(int ordinal, uint hash)
        {
            var p = world.Properties[ordinal];
            if (p.Index != ordinal || p.NameHash != hash || p.TypeCode != 5 || p.ElementCount != 1 || p.RawValue.Length != 4)
                throw new InvalidDataException("Caster WorldPQ source does not retain its exact scalar loader binding.");
            return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
        }
        // Property26 may register an additional source dependency; it is not
        // the default370 creator selector and does not write these mask fields.
        // Actual current GeometryInput remains the separate complete inventory.
        if (Word(0, 0x0B6E1578) != actualModelObjectId || Word(3, 0xECEC336A) != 0)
            throw new NotSupportedException("Caster WorldPQ requires the source default370 creator and actual selected Model.");
        return new(world.ObjectId, actualModelObjectId, Word(9, OriginalMapSourceCreatedViewFields.ModelExclusion78PropertyHash),
            OriginalDeferredEarlyShadowCasterSource.ReadLoadedWorldCasterFlags10C(world),
            actualCatalog.Source.ResourceUri + "@" + actualCatalog.Source.DecodedSha256 + "/" + world.ObjectId.ToString("X8"));
    }

    /// <summary>Fresh same-generation D65620 kind1 records: literal kind1 at
    /// D65764 makes CFD4D9/4EC skip point/spot wrapper creation, so D067B8
    /// publishes record0 null. View1BD0 itself can still select this record.
    /// A later record0 setter requires CaptureCurrentRows with its actual input.</summary>
    public static OriginalDeferredEarlyShadowCurrentCasterLightRows CaptureFreshSourceRetainedKind1Rows(
        OriginalMapWorldKind1CurrentQueue actualQueue,
        Func<OriginalMapWorldKind1CurrentRecord, OriginalMapSourceCreatedLightingPacket> readActualCurrentLighting)
        => CaptureCurrentRows(actualQueue, readActualCurrentLighting, _ => false,
            "TC:D65764:kind1;CFD4D9/CFD4EC/CFD842:wrapper0null;D067B8");

    public static OriginalDeferredEarlyShadowCurrentCasterLightRows CaptureCurrentRows(
        OriginalMapWorldKind1CurrentQueue actualQueue,
        Func<OriginalMapWorldKind1CurrentRecord, OriginalMapSourceCreatedLightingPacket> readActualCurrentLighting,
        Func<OriginalMapWorldKind1CurrentRecord, bool> readActualRecordShadowWrapper0Present,
        string actualRecordWrapperWriterEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualQueue); ArgumentNullException.ThrowIfNull(readActualCurrentLighting);
        ArgumentNullException.ThrowIfNull(readActualRecordShadowWrapper0Present);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualRecordWrapperWriterEvidence);
        var rows = new List<OriginalDeferredEarlyShadowCurrentCasterLightRecord>(actualQueue.Records.Count);
        foreach (var record in actualQueue.Records)
        {
            var lighting = readActualCurrentLighting(record) ?? throw new InvalidDataException("Current caster LightData owner is absent.");
            if (lighting.FrameNumber != actualQueue.FrameNumber || lighting.Binding.ViewIdentity != actualQueue.Binding.ViewIdentity ||
                lighting.Binding.CameraOwnerIdentity != actualQueue.Binding.CameraOwnerIdentity || lighting.LocalLightData.Length <= 0xF20)
                throw new InvalidDataException("Caster LightData is not from the same current View/Camera/queue.");
            // Verifies the actual registered Directional root and selected ready
            // provider. It does not replace its runtime F1C with source metadata.
            _ = lighting.CaptureRetainedDirectionalRecordNumericFields(record);
            var bytes = lighting.LocalLightData.Span;
            if (bytes[0xF20] != record.LightDataF20)
                throw new InvalidDataException("Caster record and actual LightData classification differ.");
            rows.Add(new(record, bytes[0xF1C], bytes[0xF1F], bytes[0xF20],
                readActualRecordShadowWrapper0Present(record), lighting.SourceProvenance + ";" + actualRecordWrapperWriterEvidence));
        }
        return new(actualQueue, rows.ToArray());
    }

    /// <summary>DB5A60 -> DB866B -> C3937F..39B. Actual per-view opacity
    /// precedes those calls (DB7A18); Main frustum rejection is deliberately
    /// absent from this independent caster gate.</summary>
    public static OriginalDeferredEarlyShadowCurrentCallerMasks ReadCurrentMasks(
        OriginalDeferredEarlyShadowCurrentViewMaskFields actualView,
        OriginalDeferredEarlyShadowCurrentWorldCallerFields actualWorld,
        bool actualCommon240Present, uint actualCommonFlags8, float actualPerViewOpacityB0)
    {
        ArgumentNullException.ThrowIfNull(actualView);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualWorld.CurrentWriterEvidence);
        uint first = actualWorld.ActualEligibilityV408 ? actualView.CallerFirstMask & actualWorld.Mask328 : 0;
        uint second = actualWorld.ActualEligibilityV408 ? actualView.CallerSecondMask & actualWorld.Mask32C : 0;
        uint shadow = actualWorld.ActualEligibilityV408 && actualWorld.WorldCasterFlags10C >= 0
            ? actualView.CallerShadowMask & actualWorld.Mask330 : 0;
        if (actualCommon240Present && (actualCommonFlags8 & 0x800) == 0) shadow = 0;
        uint active = actualView.SuccessfulViewMask3340;
        // COMISS(0,B0)/JB also accepts unordered; retain the source flags.
        bool opacity = !(actualPerViewOpacityB0 <= 0);
        return new(first, second, shadow, opacity && (first & active) != 0,
            opacity && (second & active) != 0, opacity && (shadow & active) != 0);
    }

    public static OriginalDeferredEarlyShadowFirstCasterAdmission PrepareCurrentFirstCasterAdmission(
        OriginalDeferredEarlyShadowCurrentViewMaskFields actualView,
        OriginalDeferredEarlyShadowCurrentWorldCallerFields actualWorld,
        OriginalDeferredEarlyShadowCurrentCasterLightRows actualLights,
        bool actualCommon240Present, uint actualCommonFlags8, uint actualCommonExclusion404,
        float actualPerViewOpacityB0, bool actualSelectedShadowRecord1BD0Present)
    {
        ArgumentNullException.ThrowIfNull(actualLights);
        if (actualView.FrameNumber != actualLights.Queue.FrameNumber || actualView.ViewIdentity != actualLights.Queue.Binding.ViewIdentity)
            throw new InvalidDataException("Caster masks and complete current LightData queue belong to different Views.");
        var masks = ReadCurrentMasks(actualView, actualWorld, actualCommon240Present, actualCommonFlags8, actualPerViewOpacityB0);
        var light = masks.ShadowSlotEligible && actualSelectedShadowRecord1BD0Present
            ? actualLights.SelectFirstCurrentKind1(actualWorld.Exclusion78 | actualCommonExclusion404) : null;
        uint first = light is not null && OriginalDeferredEarlyShadowCasterSource.ReadFirstCasterGate(light.F1C,
            (actualWorld.WorldCasterFlags10C & 1) != 0) ? 15u : 0;
        // The second family is a separate C3A393..A88A consumer. Expose its
        // current declaration rather than silently treating it as first-family.
        return new(masks, light, first, light is null ? 0u : (uint)(light.F1F & 15));
    }
}
