using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The Field scene's actual Landscape exclusion. This is a View1BB8
/// record, not a WorldPQ Model primitive or a kind1 primitive counter.</summary>
public sealed class TerrainViewPublicationSource
{
    private TerrainViewPublicationSource(LoadedTerrainParameterResources parameters, uint shaderFileId, uint exclusion)
    { Parameters = parameters; LandscapeObjectId = parameters.WrapperRoot.ObjectId; ShaderFileId = shaderFileId;
      Exclusion2B8 = exclusion; SourceIdentity = Wrapper.Source.ResourceUri + "@" + Wrapper.Source.DecodedSha256 + "/" + LandscapeObjectId.ToString("X8"); }
    public LoadedTerrainParameterResources Parameters { get; }
    public LoadedObjects Wrapper => Parameters.Wrapper;
    public uint LandscapeObjectId { get; }
    public uint ShaderFileId { get; }
    public uint Exclusion2B8 { get; }
    public string SourceIdentity { get; }
    public static TerrainViewPublicationSource ReadFieldScene(LoadedTerrainParameterResources parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var landscape = parameters.WrapperRoot;
        if (landscape.ClassId != 0x8E1ECB72 || landscape.Kind != DokObjectKind.Inline ||
            Scalar(landscape, 0x445D803E) != parameters.TerrainRoot.ObjectId)
            throw new InvalidDataException("Terrain View publication requires the imported Landscape targeting its terrain.");
        uint shader = Scalar(landscape, TerrainShaderResources.GBufferShaderPropertyHash);
        if (shader == 0 || Scalar(landscape, 0xC87D92E0) != 0)
            throw new NotSupportedException("Terrain View source requires an ordinary shader and source-null separate shadow-map role.");
        return new(parameters, shader, Scalar(landscape, 0xC0C5A5B5));
    }
    private static uint Scalar(DokObject source, uint hash)
    {
        var matches = source.Properties.Where(p => p.NameHash == hash).ToArray();
        if (matches.Length != 1 || matches[0].TypeCode != 5 || matches[0].ElementCount != 1 || matches[0].RawValue.Length != 4)
            throw new InvalidDataException("Terrain View source scalar layout changed.");
        return BinaryPrimitives.ReadUInt32LittleEndian(matches[0].RawValue);
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
        selected.ValidateFor(lighting);
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
