using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A fresh backend named projection. It executes no native Generic
/// directional handler or delayed Scat/cache submission. The native ctor's
/// pending0 is not a proof of the native current pending bit:97D430 calls
/// 97CAE0(start0), which sets it to1 even with sourceMode0. This independent
/// backend owner is never a capture or simulation of that complete lifecycle.</summary>
public sealed class MapSourceCreatedLightingGenericFogScope
{
    internal MapSourceCreatedLightingGenericFogScope(string identity) => OwnerIdentity = identity;
    public string OwnerIdentity { get; }
    /// <summary>This backend projection's state only; never native current121.</summary>
    public byte Pending121 => 0;
    public bool HasActiveFogHistory => false;
    public string SourceProvenance => "backend-named-projection/no-native-97CAE0-or-delayed-Scat-cache;" +
        "backend-pending=0/native-current-pending-unresolved;owner=" + OwnerIdentity;
}

/// <summary>Validation of one actual owned frame. Early parameters and Sky
/// have separate typed checks at their preparation boundaries. A receipt does
/// not prove descriptor readiness, image contents, a GPU draw, native retained
/// bytes, or equivalence of complete LightData/Generic-owner byte images.</summary>
public sealed class MapSourceCreatedLightingRetainedConsumerReceipt
{
    internal MapSourceCreatedLightingRetainedConsumerReceipt(
        MapSourceCreatedLightingRetainedNormalization owner,
        MapSourceCreatedLightingPacket lighting,
        DeferredF103SelectedSource late,
        DeferredEarlyF103SelectedSource? early)
    {
        Owner = owner; Lighting = lighting; Late = late; Early = early;
        FrameNumber = lighting.FrameNumber;
    }
    internal MapSourceCreatedLightingRetainedNormalization Owner { get; }
    internal MapSourceCreatedLightingPacket Lighting { get; }
    internal DeferredF103SelectedSource Late { get; }
    internal DeferredEarlyF103SelectedSource? Early { get; }
    public long FrameNumber { get; }
    public bool NativeByteIdentityPreserved => false;
    public bool BackendNamedOnly => true;
    public string SourceProvenance => Owner.SourceProvenance + ";frame=" + FrameNumber +
        ";raw-SharedFog-reader-skipped;Exposure-mode2/FDC-bit0-clear/Camera17C;Final-Camera-only";
}

/// <summary>Chosen backend representation of the twelve native retained
/// extents, limited to the exact source consumer projections below. Source
/// code does not establish their native values. Construction may bootstrap
/// before shader selection; every actual frame must subsequently validate its
/// consumers before it can use this representation. Other consumers require
/// explicit current retained bytes through RetainedAllocation instead.</summary>
public sealed class MapSourceCreatedLightingRetainedNormalization
{
    private readonly LoadedShaderContainer lateShader, skyShader;
    private readonly LoadedShaderContainer? earlyShader;
    private MainLightConstructionPacket? construction;
    private string? createdHash, constructedOwnerHash;
    private long lastAcceptedFrame;
    private static readonly (int Offset, int Length)[] NativeLayout =
    [
        (0x164,76),(0x2BC,4),(0xAD5,3),(0xB6F,1),(0xB95,3),(0xDC9,3),
        (0xDD5,3),(0xDE1,3),(0xE01,3),(0xF21,71),(0x1090,32),(0x10B8,8)
    ];
    private MapSourceCreatedLightingRetainedNormalization(LoadedShaderContainer late,
        LoadedShaderContainer sky, LoadedShaderContainer? early, string identity)
    {
        lateShader=late; skyShader=sky; earlyShader=early; BackendOwnerIdentity=identity;
        GenericFogOwner=new(identity + ":fresh-Generic-inactive-projection");
    }
    public string BackendOwnerIdentity { get; }
    public MapSourceCreatedLightingGenericFogScope GenericFogOwner { get; }
    public bool IncludesOrdinaryEarly126 => earlyShader is not null;
    public bool BackendNamedOnly => true;
    public int BackendNormalizedRetainedBytes => 210;
    public bool NativeByteIdentityPreserved => false;
    public string SourceProvenance => "backend-owned-retained210-normalization;owner=" + BackendOwnerIdentity +
        ";native-retained-values-unresolved;native-byte-identity=false;" +
        "source-consumers=kind1/F103-9D8E811F-1022-PS767/Sky340AC335-PS3-PS4/Exposure-mode2/Final-Camera-only" +
        (IncludesOrdinaryEarly126 ? "/early-B82C3BBF-126-PS95-typed-source-Caustics" : "") +
        ";" + GenericFogOwner.SourceProvenance;

    /// <summary>Pin source programs without selecting a synthetic current row.
    /// The actual matched feature row is checked later against the current
    /// Camera/light/View producer. This step allocates no renderer resources.</summary>
    public static MapSourceCreatedLightingRetainedNormalization LoadKnownConsumers(
        GameResourceRepository repository, string backendOwnerIdentity, bool includeOrdinaryEarly126)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentException.ThrowIfNullOrWhiteSpace(backendOwnerIdentity);
        var late=repository.LoadShaderContainer("system",0x9D8E811F);
        RequireShader(late,0x9D8E811F,MapSourceCreatedShaderSelection.ShaderSha256);
        RequireProgram(late,767,DeferredSelected767Constants.PixelProgramSha256);
        RequireProgram(late,0,DeferredEarlyF103SourceSelection.FullscreenVertexSha256);
        var sky=repository.LoadShaderContainer("system",0x340AC335);
        RequireShader(sky,0x340AC335,"CE4CEC6271BDE59E40D5B97746A7A1877188E133550A0C608D41C353A4999424");
        RequireProgram(sky,0,"E0E2FEB809484A59D79E28A58BBE384BAAE01B67914296C37CF1BBC26ED2AB36");
        RequireProgram(sky,3,Sky2SourceZeroMoon.AtmosphereProgramSha256);
        RequireProgram(sky,4,Sky2SourceZeroMoon.ContributionProgramSha256);
        RequireInactiveProfile(sky,0,4);
        LoadedShaderContainer? early=null;
        if(includeOrdinaryEarly126)
        {
            early=repository.LoadShaderContainer("system",0xB82C3BBF);
            RequireShader(early,0xB82C3BBF,DeferredEarlyCausticsOwner.ShaderSourceSha256);
            RequireProgram(early,95,DeferredEarlyCausticsOwner.PixelProgramSha256);
            RequireProgram(early,0,DeferredEarlyF103SourceSelection.FullscreenVertexSha256);
        }
        return new(late,sky,early,backendOwnerIdentity);
    }

    /// <summary>Apply all4078 source writes and choose only the210 backend
    /// retained bytes. Actual resource handles/provenance are passed unchanged;
    /// this method never creates readiness receipts or source-null bindings.</summary>
    public MainLightConstructionPacket ApplyConstructor(LoadedMainLightBindingSource source,
        uint contextFlags3E4, IReadOnlyList<MainLightConstructionResourceBinding> actualResourceBindings,
        byte backendFill=0)
    {
        if(construction is not null)
            throw new InvalidOperationException("This retained normalization owner already has its construction generation.");
        var layout=MapSourceCreatedLightingRetainedSource.ReadGameDefaultLayout(source,contextFlags3E4);
        if(layout.Count!=NativeLayout.Length || layout.Where((e,i)=>e.Offset!=NativeLayout[i].Offset||e.Length!=NativeLayout[i].Length).Any())
            throw new InvalidDataException("Retained normalization requires the exact twelve source extents/210 bytes.");
        var ranges=NativeLayout.Select(e=>new MapSourceCreatedLightingRetainedRange(e.Offset,
            Enumerable.Repeat(backendFill,e.Length).ToArray())).ToArray();
        var template=MapSourceCreatedLightingRetainedSource.BuildGameDefaultTemplate(source,ranges,contextFlags3E4);
        var result=MainLightConstruction.Apply(source,template,
            new(contextFlags3E4,SourceProvenance + ";backend-fill=" + backendFill.ToString("X2"),actualResourceBindings));
        RequireLightControls(result.Bytes.Span);
        construction=result; createdHash=Hash(result.Bytes.Span);
        constructedOwnerHash=Hash(Encoding.UTF8.GetBytes(result.SourceProvenance));
        return result;
    }

    /// <summary>Validate actual producers after current-frame preparation.
    /// False SharedFog still reads retained fog coefficients and is rejected.
    /// The complete native Generic fog cache is outside this named projection.
    /// The declared ordinary early consumer must occur exactly once when this
    /// recipe includes it, and needs a later typed Caustics/parameter check.
    /// The active list is the caller's actual captured execution plan.</summary>
    public MapSourceCreatedLightingRetainedConsumerReceipt RequireCurrentConsumers(
        MapSourceCreatedLightingFrameInputs actualLightingInput,
        MapSourceCreatedLightingPacket lighting, MapSourceCreatedF103FeaturePacket feature,
        DeferredF103SelectedSource actualLateSelected,
        IReadOnlyList<DeferredEarlyF103SelectedSource> actualActiveEarlySelected,
        MapSourceCreatedLightingGenericFogScope currentGenericFogScope)
    {
        ArgumentNullException.ThrowIfNull(actualLightingInput); ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(actualLateSelected); ArgumentNullException.ThrowIfNull(actualActiveEarlySelected);
        ValidateLighting(lighting);
        actualLightingInput.PresentSeasonCamera?.ValidateInputs(actualLightingInput);
        if(!ReferenceEquals(currentGenericFogScope,GenericFogOwner) || actualLightingInput.FrameNumber!=lighting.FrameNumber ||
            actualLightingInput.Binding!=lighting.Binding || actualLightingInput.ContextFlags3E4!=lighting.ContextFlags3E4 ||
            actualLightingInput.RuntimeTable!=lighting.RuntimeTable ||
            actualLightingInput.CameraScope!=MapLightingCameraScope.LightingPrefix ||
            actualLightingInput.SelectedSharedFogOwnerEnabled is not null || actualLightingInput.AdditionalSceneInputs.Fog is not null ||
            lighting.SceneInputs.Fog is not null || lighting.FrameNumber<lastAcceptedFrame)
            throw new InvalidDataException("Retained normalization requires the same actual input, skipped raw SharedFog and this fresh inactive Generic owner.");
        feature.ValidateFrame(actualLateSelected.Camera,lighting);
        if(actualLateSelected.OwnedFeatureSource is not { } selectedFeature ||
            selectedFeature.EffectiveLightOwnerIdentity!=feature.EffectiveLightOwnerIdentity ||
            !selectedFeature.CurrentFeature48.Span.SequenceEqual(feature.CurrentFeature48.Span) ||
            actualLateSelected.SourcePlan.Shader.Source.DecodedSha256!=lateShader.Source.DecodedSha256 ||
            actualLateSelected.SourcePlan.Node.Index!=166 || actualLateSelected.Combination.Index!=1022 ||
            actualLateSelected.PixelProgramIndex!=767 || actualLateSelected.PixelProgramSha256!=DeferredSelected767Constants.PixelProgramSha256 ||
            feature.Scratch.Mode50!=0)
            throw new NotSupportedException("Retained normalization admits only the actual source-owned kind1/1022/PS767 inactive light projection.");
        RequireInactiveProfile(lateShader,actualLateSelected.FeatureMatch.RowIndex,0);
        DeferredEarlyF103SelectedSource? early=null;
        if((earlyShader is not null && actualActiveEarlySelected.Count!=1) ||
            (earlyShader is null && actualActiveEarlySelected.Count!=0))
            throw new NotSupportedException("Actual active early execution differs from this recipe's declared consumer set.");
        if(actualActiveEarlySelected.Count!=0)
        {
            if(earlyShader is null || actualActiveEarlySelected.Count!=1)
                throw new NotSupportedException("An unproved additional early source consumer requires explicit retained allocation bytes.");
            early=actualActiveEarlySelected[0]; DeferredEarlyCausticsOwner.ValidateSelection(early);
            early.ValidateParameterFrame(early.Camera,lighting);
            if(early.SourcePlan.Shader.Source.DecodedSha256!=earlyShader.Source.DecodedSha256 ||
                early.OwnedFeatureSource?.EffectiveLightOwnerIdentity!=feature.EffectiveLightOwnerIdentity ||
                !early.CurrentFeature48.Span.SequenceEqual(feature.CurrentFeature48.Span))
                throw new InvalidDataException("Early normalization consumer differs from the same owned light/feature frame.");
            RequireInactiveProfile(earlyShader,early.FeatureMatch.RowIndex,0);
        }
        lastAcceptedFrame=lighting.FrameNumber;
        return new(this,lighting,actualLateSelected,early);
    }

    /// <summary>Use the immutable input retained by the actual lighting
    /// preparation when its caller no longer retains the input object.</summary>
    public MapSourceCreatedLightingRetainedConsumerReceipt RequireCurrentConsumers(
        MapSourceCreatedLightingPacket lighting, MapSourceCreatedF103FeaturePacket feature,
        DeferredF103SelectedSource actualLateSelected,
        IReadOnlyList<DeferredEarlyF103SelectedSource> actualActiveEarlySelected,
        MapSourceCreatedLightingGenericFogScope currentGenericFogScope) =>
        RequireCurrentConsumers(lighting.AcceptedInputs,lighting,feature,actualLateSelected,
            actualActiveEarlySelected,currentGenericFogScope);

    public void RequireCurrentEarlyConsumer(MapSourceCreatedLightingRetainedConsumerReceipt receipt,
        DeferredEarlyF103SelectedSource selected)
    {
        ArgumentNullException.ThrowIfNull(receipt); ArgumentNullException.ThrowIfNull(selected);
        if(!ReferenceEquals(receipt.Owner,this) || !ReferenceEquals(receipt.Early,selected))
            throw new InvalidDataException("Early retained lighting requires this owner's selected current consumer.");
    }

    private void ValidateLighting(MapSourceCreatedLightingPacket lighting)
    {
        ArgumentNullException.ThrowIfNull(lighting);
        if(construction is null || lighting.SourceIdentity.CreatedLightDataSha256!=createdHash ||
            lighting.SourceIdentity.ConstructedOwnerSha256!=constructedOwnerHash ||
            lighting.SourceIdentity.MainLightSha256!=construction.Source.Resource.Source.DecodedSha256 ||
            lighting.ContextFlags3E4!=construction.ContextFlags3E4 ||
            lighting.Binding.BasePath!=MapLightingBasePath.UnmodifiedOrdinary)
            throw new InvalidDataException("Lighting differs from this source construction/current owned normalization generation.");
        if(lighting.AcceptedInputs.PresentSeasonCamera is { } season)
            season.ValidatePreparedLighting(lighting);
        else if(lighting.RuntimeTable!=MapLightingRuntimeTable.Absent)
            throw new InvalidDataException("Retained consumers cannot infer a table branch from a declaration key or unresolved state.");
        RequireLightControls(lighting.LocalLightData.Span);
        if(lighting.CameraContextApplied && lighting.LocalLightData.Span[0xF1E]!=2)
            throw new InvalidDataException("Current source Camera context writer differs.");
    }
    private static void RequireInactiveProfile(LoadedShaderContainer shader,int rowIndex,uint rendererMode)
    {
        var rows=shader.ReadG1Bindings().Features;
        if(rowIndex<0 || rowIndex>=rows.Count || rows[rowIndex].Feature.Words[6]!=rendererMode || rows[rowIndex].Feature.Words[7]!=1)
            throw new NotSupportedException("Selected source renderer/profile lacks the inactive profile1 retained-byte proof.");
    }
    private static void RequireLightControls(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length!=0x10C0 || BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0x160,4))!=0 ||
            bytes[0x10B0]!=2 || (bytes[0xFDC]&1)!=0)
            throw new NotSupportedException("Retained normalization requires written source L160=0/mode2/FDC.bit0clear.");
    }
    private static void RequireShader(LoadedShaderContainer shader,uint file,string sha)
    {
        if(shader.Source.OverrideSource is not null || shader.Source.FileId!=file || shader.Source.DecodedSha256!=sha || Hash(shader.Source.Bytes)!=sha)
            throw new InvalidDataException("Retained consumer shader differs from its pinned original source.");
    }
    private static void RequireProgram(LoadedShaderContainer shader,int program,string sha)
    {
        if(shader.Source.OverrideSource is not null || Hash(shader.Container.GetProgramBytes(shader.Source.Bytes,program))!=sha)
            throw new InvalidDataException("Retained consumer program differs from its exact unchanged source.");
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
