using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The Field scene's actual Landscape exclusion. This is a View1BB8
/// record, not a WorldPQ Model primitive or a kind1 primitive counter.</summary>
public sealed class TerrainViewPublicationSource
{
    private readonly byte[] wrapperBytes;
    private TerrainViewPublicationSource(LoadedObjects wrapper, uint objectId, uint exclusion)
    { Wrapper = wrapper; LandscapeObjectId = objectId; Exclusion2B8 = exclusion; wrapperBytes = wrapper.Source.Bytes.ToArray(); }
    public LoadedObjects Wrapper { get; }
    public uint LandscapeObjectId { get; }
    public uint Exclusion2B8 { get; }
    public string SourceIdentity => Wrapper.Source.ResourceUri + "@" + Wrapper.Source.DecodedSha256 + "/" + LandscapeObjectId.ToString("X8");
    public static TerrainViewPublicationSource ReadFieldScene(LoadedTerrainParameterResources parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var wrapper = parameters.Wrapper;
        if (wrapper.Source.ResourceUri != GameResourceId.Rdb("KIDSSystemResource", 0xB6DCC68E).ToString() ||
            wrapper.Source.DecodedSha256 != "AF2F9FB1E78C59DB43BE2DBF9515E61CAEBD6E3892A6AF508391875136557491" ||
            Convert.ToHexString(SHA256.HashData(wrapper.Source.Bytes)) != wrapper.Source.DecodedSha256 ||
            parameters.WrapperRoot.ObjectId != 0xFF30C1FE)
            throw new InvalidDataException("Terrain View publication requires the verified original Field Landscape source.");
        var landscape = DokReader.Read(wrapper.Source.Bytes).Objects.Single(o =>
            o.ObjectId == parameters.WrapperRoot.ObjectId && o.ClassId == 0x8E1ECB72 && o.Kind == DokObjectKind.Inline);
        uint target = Scalar(landscape, 1, 0x445D803E);
        if (target != parameters.TerrainRoot.ObjectId || Scalar(landscape, 7, 0x3AF82377) != 0xDC52FD1C ||
            Scalar(landscape, 18, 0xC87D92E0) != 0)
            throw new NotSupportedException("Terrain View source needs its original ordinary shader and source-null separate shadow-map role.");
        return new(wrapper, landscape.ObjectId, Scalar(landscape, 0, 0xC0C5A5B5));
    }
    public void ValidateSourceUnchanged()
    {
        if (!wrapperBytes.AsSpan().SequenceEqual(Wrapper.Source.Bytes))
            throw new InvalidDataException("Terrain View source changed after its accepted construction.");
    }
    private static uint Scalar(DokObject source, int ordinal, uint hash)
    {
        var p = source.Properties[ordinal];
        if (p.Index != ordinal || p.NameHash != hash || p.TypeCode != 5 || p.ElementCount != 1 || p.RawValue.Length != 4)
            throw new InvalidDataException("Terrain View source scalar layout changed.");
        return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
    }
}

/// <summary>Nonempty current Landscape1BB8 publication in either the original
/// no-selected branch or the typed current Aux-disabled selected-kind1 branch.
/// Complete patches are kept even when the
/// current kind1 record masks do not include Landscape's channel8.</summary>
public sealed class TerrainCurrentViewPublication
{
    private readonly TerrainPatchInput[] patches;
    private TerrainSelectedKind1Record? selectedShadow;
    private MapCommonRenderState? shadowCommon;
    private TerrainCurrentViewPublication(TerrainViewPublicationSource source,
        MapSourceCreatedLightingPacket lighting, TerrainPatchInput[] patches, string provenance)
    { Source = source; FrameNumber = lighting.FrameNumber; ViewIdentity = lighting.Binding.ViewIdentity;
      CameraOwnerIdentity = lighting.Binding.CameraOwnerIdentity; this.patches = patches; SourceProvenance = provenance; }
    public TerrainViewPublicationSource Source { get; }
    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public string CameraOwnerIdentity { get; }
    public int CurrentLandscapeRecordCount => 1;
    public IReadOnlyList<TerrainPatchInput> CompleteAcceptedPatches => Array.AsReadOnly(patches.ToArray());
    public string SourceProvenance { get; }
    public TerrainSelectedKind1Record? SelectedShadowRecord1BD0 => selectedShadow;
    public bool ShadowEligibility2C6 => false;
    public string ShadowEligibilityScope => selectedShadow is null ? "selected-wrapper-absent" :
        "selected-kind1-present;actual-current-Aux240-absent-or-2000-clear";
    public static TerrainCurrentViewPublication PrepareWithSelectedKind1Record(
        TerrainViewPublicationSource source, MapSourceCreatedLightingPacket lighting,
        IReadOnlyList<TerrainPatchInput> completeAcceptedPatches, TerrainSelectedKind1Record selected,
        MapCommonRenderState currentCommon, string currentCpuProvenance)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(completeAcceptedPatches); ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(currentCommon); ArgumentException.ThrowIfNullOrWhiteSpace(currentCpuProvenance);
        source.ValidateSourceUnchanged(); selected.ValidateFor(lighting);
        currentCommon.ValidateFrame(lighting.FrameNumber, currentCommon.CommonTableOwnerIdentity, lighting.Binding.ViewIdentity);
        if (lighting.FrameNumber <= 0 || completeAcceptedPatches.Count == 0 || completeAcceptedPatches.Any(p => p is null))
            throw new InvalidDataException("Present selected-shadow Terrain publication needs the complete nonempty actual patch inventory.");
        if (currentCommon.AuxiliaryPresent && (currentCommon.CommonFlags & 0x2000) != 0)
            throw new NotSupportedException("Current Aux240 permits Terrain shadows; this branch requires its actual record20 CPU128 and shadow-geometry producer before 2C6 can be published.");
        var result = new TerrainCurrentViewPublication(source, lighting, completeAcceptedPatches.ToArray(),
            currentCpuProvenance + ";" + source.SourceIdentity + ";" + selected.SourceProvenance + ";" + currentCommon.ProducerProvenance +
            ";D64610/D08910-actual-Landscape1BB8;D5CDF0/D5D1F6-clear2C6;" +
            (currentCommon.AuxiliaryPresent ? "D5CE2D/D5D284-Aux240+8-bit13-clear" : "D5CE2D/D5D262-current-Aux240-absent") +
            ";D61730-no-shadow-contribution;selected1BD0-remains-present");
        result.selectedShadow = selected; result.shadowCommon = currentCommon;
        return result;
    }
    public static TerrainCurrentViewPublication PrepareWithoutSelectedShadowWrapper(
        TerrainViewPublicationSource source, MapSourceCreatedLightingPacket lighting,
        IReadOnlyList<TerrainPatchInput> completeAcceptedPatches, string currentCpuProvenance)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(completeAcceptedPatches); ArgumentException.ThrowIfNullOrWhiteSpace(currentCpuProvenance);
        source.ValidateSourceUnchanged();
        if (lighting.FrameNumber <= 0 || completeAcceptedPatches.Count == 0 || completeAcceptedPatches.Any(p => p is null))
            throw new InvalidDataException("Terrain current View publication requires a complete nonempty accepted patch inventory.");
        return new(source, lighting, completeAcceptedPatches.ToArray(), currentCpuProvenance + ";" + source.SourceIdentity +
            ";TC:BA5360/BA556A/B988D0/BA9880/D5D108;Landscape358-to-record2B8;" +
            "TC:D64610/D08910;View1BB8-one-Landscape-record;TC:D61580;" +
            "TC:D5CDF0-selected1BD0-null-keeps2C6-zero;TC:D61730-no-shadow-contribution");
    }
    internal uint ValidateAndGetFlags(MapSourceCreatedLightingPacket lighting,
        MapSourceCreatedViewPublicationState current, IReadOnlyList<uint> sourceKind1Masks)
    {
        Source.ValidateSourceUnchanged();
        if (FrameNumber != lighting.FrameNumber || FrameNumber != current.FrameNumber ||
            ViewIdentity != lighting.Binding.ViewIdentity || CameraOwnerIdentity != lighting.Binding.CameraOwnerIdentity ||
            current.AdditionalOwners1B80_1BB0_1BB8_1B98Absent != false ||
            current.OtherAdditionalOwners1B80_1BB0_1B98Absent != true)
            throw new InvalidDataException("Terrain View publication requires the exact current frame and explicit bounded no-shadow/other-owner branch.");
        if (selectedShadow is null)
        {
            if (current.SelectedShadowWrapper1BD0Present != false || current.SelectedShadowRecord1BD0 is not null)
                throw new InvalidDataException("The original no-selected-wrapper Terrain branch cannot accept a present or unknown record.");
        }
        else
        {
            selectedShadow.ValidateFor(lighting);
            if (current.SelectedShadowWrapper1BD0Present != true || !ReferenceEquals(current.SelectedShadowRecord1BD0, selectedShadow) ||
                shadowCommon is null || !sourceKind1Masks.Contains(selectedShadow.RecordWord78))
                throw new InvalidDataException("Present selected Terrain record must retain the actual same-frame kind1 owner and current Aux branch.");
            shadowCommon.ValidateFrame(FrameNumber, shadowCommon.CommonTableOwnerIdentity, ViewIdentity);
            if (shadowCommon.AuxiliaryPresent && (shadowCommon.CommonFlags & 0x2000) != 0)
                throw new InvalidDataException("Current Aux240 no longer supplies the accepted finite Terrain shadow branch.");
        }
        uint flags = 0;
        foreach (uint mask in sourceKind1Masks)
            if ((Source.Exclusion2B8 & mask) == 0) flags |= mask;
        return flags;
    }
}
