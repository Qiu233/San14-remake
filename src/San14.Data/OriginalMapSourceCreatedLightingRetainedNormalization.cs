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
public sealed class OriginalMapSourceCreatedLightingGenericFogScope
{
    internal OriginalMapSourceCreatedLightingGenericFogScope(string identity) => OwnerIdentity = identity;
    public string OwnerIdentity { get; }
    /// <summary>This backend projection's state only; never native current121.</summary>
    public byte Pending121 => 0;
    public bool HasActiveFogHistory => false;
    public string SourceProvenance => "backend-named-projection/no-native-97CAE0-or-delayed-Scat-cache;" +
        "backend-pending=0/native-current-pending-unresolved;owner=" + OwnerIdentity;
}

/// <summary>Explicit current Sky coefficients from a separate owned producer.
/// Independence is a required current-owner scope, not inferred from numeric
/// zeros, sourceMode0 or a fresh native Generic constructor.</summary>
public sealed record OriginalMapSourceCreatedLightingIndependentSkyAtmosphereInputs(long FrameNumber,
    string ViewIdentity, string AtmosphereOwnerIdentity, OriginalSkySceneSharedAtmosphere Atmosphere,
    bool? IndependentOfNormalizedLightData, string CurrentProducerProvenance);

public sealed class OriginalMapSourceCreatedLightingIndependentSkyAtmosphere
{
    internal OriginalMapSourceCreatedLightingIndependentSkyAtmosphere(
        OriginalMapSourceCreatedLightingRetainedConsumerReceipt receipt,
        OriginalMapSourceCreatedLightingIndependentSkyAtmosphereInputs input)
    {
        Receipt=receipt; FrameNumber=input.FrameNumber; ViewIdentity=input.ViewIdentity;
        AtmosphereOwnerIdentity=input.AtmosphereOwnerIdentity; CurrentProducerProvenance=input.CurrentProducerProvenance;
        var value=input.Atmosphere;
        captured=value with { Fog=value.Fog with {Bytes=value.Fog.Bytes.ToArray(),
            ScatteringOverride=value.Fog.ScatteringOverride is { } scattering ? scattering with {
                Bytes=scattering.Bytes.ToArray(),Values=Array.AsReadOnly(scattering.Values.ToArray())} : null},
            Scattering=Array.AsReadOnly(value.Scattering.ToArray()) };
    }
    private readonly OriginalSkySceneSharedAtmosphere captured;
    internal OriginalMapSourceCreatedLightingRetainedConsumerReceipt Receipt { get; }
    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public string AtmosphereOwnerIdentity { get; }
    public string CurrentProducerProvenance { get; }
    public OriginalSkySceneSharedAtmosphere Atmosphere => captured with {
        Fog=captured.Fog with {Bytes=captured.Fog.Bytes.ToArray(),
            ScatteringOverride=captured.Fog.ScatteringOverride is { } scattering ? scattering with {
                Bytes=scattering.Bytes.ToArray(),Values=Array.AsReadOnly(scattering.Values.ToArray())} : null},
        Scattering=Array.AsReadOnly(captured.Scattering.ToArray()) };
    internal void Validate(OriginalSkySceneSharedAtmosphere actual)
    {
        if(actual.SourceProvenance!=captured.SourceProvenance || actual.Fog.RendererFogMode!=captured.Fog.RendererFogMode ||
            actual.Fog.Profile!=captured.Fog.Profile || actual.Fog.FogEnabled!=captured.Fog.FogEnabled ||
            actual.Fog.SourceFogMode!=captured.Fog.SourceFogMode || actual.Fog.Value!=captured.Fog.Value ||
            actual.Fog.RequiresMode7NeutralScattering!=captured.Fog.RequiresMode7NeutralScattering ||
            !actual.Fog.Bytes.AsSpan().SequenceEqual(captured.Fog.Bytes) ||
            !actual.Scattering.SequenceEqual(captured.Scattering) ||
            (actual.Fog.ScatteringOverride is null)!=(captured.Fog.ScatteringOverride is null) ||
            (actual.Fog.ScatteringOverride is { } a && captured.Fog.ScatteringOverride is { } b &&
                (a.RendererFogMode!=b.RendererFogMode || !a.Bytes.AsSpan().SequenceEqual(b.Bytes) || !a.Values.SequenceEqual(b.Values))))
            throw new InvalidDataException("Actual Sky atmosphere differs from its independent current owner.");
    }
}

/// <summary>Validation of one actual owned frame. Early parameters and Sky
/// have separate typed checks at their preparation boundaries. A receipt does
/// not prove descriptor readiness, image contents, a GPU draw, native retained
/// bytes, or equivalence of complete LightData/Generic-owner byte images.</summary>
public sealed class OriginalMapSourceCreatedLightingRetainedConsumerReceipt
{
    internal OriginalMapSourceCreatedLightingRetainedConsumerReceipt(
        OriginalMapSourceCreatedLightingRetainedNormalization owner,
        OriginalMapSourceCreatedLightingPacket lighting,
        OriginalDeferredF103SelectedSource late,
        OriginalDeferredEarlyF103SelectedSource? early)
    {
        Owner = owner; Lighting = lighting.CopyAcceptedSnapshot(); Late = late; Early = early;
        FrameNumber = lighting.FrameNumber; LocalLightDataSha256 = Hash(lighting.LocalLightData.Span);
    }
    internal OriginalMapSourceCreatedLightingRetainedNormalization Owner { get; }
    internal OriginalMapSourceCreatedLightingPacket Lighting { get; }
    internal OriginalDeferredF103SelectedSource Late { get; }
    internal OriginalDeferredEarlyF103SelectedSource? Early { get; }
    public long FrameNumber { get; }
    public string LocalLightDataSha256 { get; }
    public bool Late767Validated => true;
    public bool Early126ParametersValidated { get; internal set; }
    public bool SkyLightVectorsValidated { get; internal set; }
    public bool SkyAtmosphereValidated { get; internal set; }
    internal int SkyNamedProgramMask { get; set; }
    public bool Sky3And4Validated => SkyAtmosphereValidated && SkyNamedProgramMask==3;
    public bool AllDeclaredF103ConsumersValidated => Early is null || Early126ParametersValidated;
    public bool AllDeclaredConsumersValidated => AllDeclaredF103ConsumersValidated && Sky3And4Validated;
    public bool NativeByteIdentityPreserved => false;
    public bool BackendNamedOnly => true;
    public string SourceProvenance => Owner.SourceProvenance + ";frame=" + FrameNumber +
        ";accepted-light=" + LocalLightDataSha256 + ";raw-SharedFog-reader-skipped;" +
        "Exposure-mode2/FDC-bit0-clear/Camera17C;Final-Camera-only;" +
        "early126-parameters=" + Early126ParametersValidated + ";Sky3/4=" + Sky3And4Validated;
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

/// <summary>Chosen backend representation of the twelve native retained
/// extents, limited to the exact source consumer projections below. Source
/// code does not establish their native values. Construction may bootstrap
/// before shader selection; every actual frame must subsequently validate its
/// consumers before it can use this representation. Other consumers require
/// explicit current retained bytes through RetainedAllocation instead.</summary>
public sealed class OriginalMapSourceCreatedLightingRetainedNormalization
{
    private readonly LoadedShaderContainer lateShader, skyShader;
    private readonly LoadedShaderContainer? earlyShader;
    private OriginalMainLightConstructionPacket? construction;
    private string? createdHash, constructedOwnerHash;
    private long lastAcceptedFrame;
    private static readonly (int Offset, int Length)[] NativeLayout =
    [
        (0x164,76),(0x2BC,4),(0xAD5,3),(0xB6F,1),(0xB95,3),(0xDC9,3),
        (0xDD5,3),(0xDE1,3),(0xE01,3),(0xF21,71),(0x1090,32),(0x10B8,8)
    ];
    private OriginalMapSourceCreatedLightingRetainedNormalization(LoadedShaderContainer late,
        LoadedShaderContainer sky, LoadedShaderContainer? early, string identity)
    {
        lateShader=late; skyShader=sky; earlyShader=early; BackendOwnerIdentity=identity;
        GenericFogOwner=new(identity + ":fresh-Generic-inactive-projection");
    }
    public string BackendOwnerIdentity { get; }
    public OriginalMapSourceCreatedLightingGenericFogScope GenericFogOwner { get; }
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
    public static OriginalMapSourceCreatedLightingRetainedNormalization LoadKnownConsumers(
        OriginalGameRepository repository, string backendOwnerIdentity, bool includeOrdinaryEarly126)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentException.ThrowIfNullOrWhiteSpace(backendOwnerIdentity);
        var late=repository.LoadShaderContainer("system",0x9D8E811F);
        RequireShader(late,0x9D8E811F,OriginalMapSourceCreatedShaderSelection.ShaderSha256);
        RequireProgram(late,767,OriginalDeferredSelected767Constants.PixelProgramSha256);
        RequireProgram(late,0,OriginalDeferredEarlyF103SourceSelection.FullscreenVertexSha256);
        var sky=repository.LoadShaderContainer("system",0x340AC335);
        RequireShader(sky,0x340AC335,"CE4CEC6271BDE59E40D5B97746A7A1877188E133550A0C608D41C353A4999424");
        RequireProgram(sky,0,"E0E2FEB809484A59D79E28A58BBE384BAAE01B67914296C37CF1BBC26ED2AB36");
        RequireProgram(sky,3,OriginalSky2SourceZeroMoon.AtmosphereProgramSha256);
        RequireProgram(sky,4,OriginalSky2SourceZeroMoon.ContributionProgramSha256);
        RequireInactiveProfile(sky,0,4);
        LoadedShaderContainer? early=null;
        if(includeOrdinaryEarly126)
        {
            early=repository.LoadShaderContainer("system",0xB82C3BBF);
            RequireShader(early,0xB82C3BBF,OriginalDeferredEarlyCausticsOwner.ShaderSourceSha256);
            RequireProgram(early,95,OriginalDeferredEarlyCausticsOwner.PixelProgramSha256);
            RequireProgram(early,0,OriginalDeferredEarlyF103SourceSelection.FullscreenVertexSha256);
        }
        return new(late,sky,early,backendOwnerIdentity);
    }

    /// <summary>Apply all4078 source writes and choose only the210 backend
    /// retained bytes. Actual resource handles/provenance are passed unchanged;
    /// this method never creates readiness receipts or source-null bindings.</summary>
    public OriginalMainLightConstructionPacket ApplyConstructor(LoadedOriginalMainLightBindingSource source,
        uint contextFlags3E4, IReadOnlyList<OriginalMainLightConstructionResourceBinding> actualResourceBindings,
        byte backendFill=0)
    {
        if(construction is not null)
            throw new InvalidOperationException("This retained normalization owner already has its construction generation.");
        var layout=OriginalMapSourceCreatedLightingRetainedSource.ReadGameDefaultLayout(source,contextFlags3E4);
        if(layout.Count!=NativeLayout.Length || layout.Where((e,i)=>e.Offset!=NativeLayout[i].Offset||e.Length!=NativeLayout[i].Length).Any())
            throw new InvalidDataException("Retained normalization requires the exact twelve source extents/210 bytes.");
        var ranges=NativeLayout.Select(e=>new OriginalMapSourceCreatedLightingRetainedRange(e.Offset,
            Enumerable.Repeat(backendFill,e.Length).ToArray())).ToArray();
        var template=OriginalMapSourceCreatedLightingRetainedSource.BuildGameDefaultTemplate(source,ranges,contextFlags3E4);
        var result=OriginalMainLightConstruction.Apply(source,template,
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
    public OriginalMapSourceCreatedLightingRetainedConsumerReceipt ValidateCurrentConsumers(
        OriginalMapSourceCreatedLightingFrameInputs actualLightingInput,
        OriginalMapSourceCreatedLightingPacket lighting, OriginalMapSourceCreatedF103FeaturePacket feature,
        OriginalDeferredF103SelectedSource actualLateSelected,
        IReadOnlyList<OriginalDeferredEarlyF103SelectedSource> actualActiveEarlySelected,
        OriginalMapSourceCreatedLightingGenericFogScope currentGenericFogScope)
    {
        ArgumentNullException.ThrowIfNull(actualLightingInput); ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(actualLateSelected); ArgumentNullException.ThrowIfNull(actualActiveEarlySelected);
        ValidateLighting(lighting);
        var acceptedInput=lighting.AcceptedInputs;
        actualLightingInput.PresentSeasonCamera?.ValidateInputs(actualLightingInput);
        if(!ReferenceEquals(currentGenericFogScope,GenericFogOwner) || actualLightingInput.FrameNumber!=lighting.FrameNumber ||
            actualLightingInput.Binding!=lighting.Binding || actualLightingInput.ContextFlags3E4!=lighting.ContextFlags3E4 ||
            actualLightingInput.SelectedSharedFogOwnerEnabled!=acceptedInput.SelectedSharedFogOwnerEnabled ||
            actualLightingInput.SelectedBuiltinAmbientCameraRate!=acceptedInput.SelectedBuiltinAmbientCameraRate ||
            !actualLightingInput.PriorPreparedCameraPrefix.Span.SequenceEqual(acceptedInput.PriorPreparedCameraPrefix.Span) ||
            actualLightingInput.LightingSkylightParameters!=acceptedInput.LightingSkylightParameters ||
            BitConverter.SingleToUInt32Bits(actualLightingInput.FrameDelta)!=BitConverter.SingleToUInt32Bits(acceptedInput.FrameDelta) ||
            actualLightingInput.RuntimeTable!=lighting.RuntimeTable ||
            !ReferenceEquals(actualLightingInput.PresentSeasonCamera,acceptedInput.PresentSeasonCamera) ||
            actualLightingInput.CameraScope!=OriginalMapLightingCameraScope.LightingPrefix ||
            actualLightingInput.SelectedSharedFogOwnerEnabled is not null || actualLightingInput.AdditionalSceneInputs.Fog is not null ||
            lighting.SceneInputs.Fog is not null || lighting.FrameNumber<lastAcceptedFrame)
            throw new InvalidDataException("Retained normalization requires the same actual input, skipped raw SharedFog and this fresh inactive Generic owner.");
        feature.ValidateFrame(actualLateSelected.Camera,lighting); actualLateSelected.Validate();
        if(actualLateSelected.OwnedFeatureSource is not { } selectedFeature ||
            selectedFeature.EffectiveLightDataSha256!=feature.EffectiveLightDataSha256 ||
            !selectedFeature.CurrentFeature48.Span.SequenceEqual(feature.CurrentFeature48.Span) ||
            actualLateSelected.SourcePlan.Shader.Source.DecodedSha256!=lateShader.Source.DecodedSha256 ||
            actualLateSelected.SourcePlan.Node.Index!=166 || actualLateSelected.Combination.Index!=1022 ||
            actualLateSelected.PixelProgramIndex!=767 || actualLateSelected.PixelProgramSha256!=OriginalDeferredSelected767Constants.PixelProgramSha256 ||
            feature.Scratch.Mode50!=0)
            throw new NotSupportedException("Retained normalization admits only the actual source-owned kind1/1022/PS767 inactive light projection.");
        RequireInactiveProfile(lateShader,actualLateSelected.FeatureMatch.RowIndex,0);
        OriginalDeferredEarlyF103SelectedSource? early=null;
        if((earlyShader is not null && actualActiveEarlySelected.Count!=1) ||
            (earlyShader is null && actualActiveEarlySelected.Count!=0))
            throw new NotSupportedException("Actual active early execution differs from this recipe's declared consumer set.");
        if(actualActiveEarlySelected.Count!=0)
        {
            if(earlyShader is null || actualActiveEarlySelected.Count!=1)
                throw new NotSupportedException("An unproved additional early source consumer requires explicit retained allocation bytes.");
            early=actualActiveEarlySelected[0]; OriginalDeferredEarlyCausticsOwner.ValidateSelection(early);
            early.ValidateParameterFrame(early.Camera,lighting);
            if(early.SourcePlan.Shader.Source.DecodedSha256!=earlyShader.Source.DecodedSha256 ||
                early.OwnedFeatureSource?.EffectiveLightDataSha256!=feature.EffectiveLightDataSha256 ||
                !early.CurrentFeature48.Span.SequenceEqual(feature.CurrentFeature48.Span))
                throw new InvalidDataException("Early normalization consumer differs from the same owned light/feature frame.");
            RequireInactiveProfile(earlyShader,early.FeatureMatch.RowIndex,0);
        }
        lastAcceptedFrame=lighting.FrameNumber;
        return new(this,lighting,actualLateSelected,early);
    }

    /// <summary>Use the immutable input retained by the actual lighting
    /// preparation when its caller no longer retains the input object.</summary>
    public OriginalMapSourceCreatedLightingRetainedConsumerReceipt ValidateCurrentConsumers(
        OriginalMapSourceCreatedLightingPacket lighting, OriginalMapSourceCreatedF103FeaturePacket feature,
        OriginalDeferredF103SelectedSource actualLateSelected,
        IReadOnlyList<OriginalDeferredEarlyF103SelectedSource> actualActiveEarlySelected,
        OriginalMapSourceCreatedLightingGenericFogScope currentGenericFogScope) =>
        ValidateCurrentConsumers(lighting.AcceptedInputs,lighting,feature,actualLateSelected,
            actualActiveEarlySelected,currentGenericFogScope);

    /// <summary>Complete the node78 check using the same-frame private typed
    /// Caustics source authority and complete actual named cb0 packet. Its
    /// source size gate skips every LightData-dependent Caustics calculation.</summary>
    public void ValidateCurrentEarlyConsumer(OriginalMapSourceCreatedLightingRetainedConsumerReceipt receipt,
        OriginalDeferredEarlyF103SelectedSource selected, OriginalDeferredEarlyCausticsPacket actualCaustics,
        OriginalDeferredEarlyConstantPacket actualNamedConstants)
    {
        ValidateReceipt(receipt); ArgumentNullException.ThrowIfNull(actualCaustics); ArgumentNullException.ThrowIfNull(actualNamedConstants);
        if(!ReferenceEquals(receipt.Early,selected))
            throw new InvalidDataException("Early retained validation requires the actual declared current selection.");
        actualCaustics.SourceNull.Validate(selected);
        if(actualNamedConstants.Bytes.Length!=480 || actualNamedConstants.RetainedVariables.Count!=0 ||
            actualNamedConstants.CommonWrites.Count!=13 || actualNamedConstants.AdditionalWrites.Count!=3)
            throw new InvalidDataException("Retained normalization requires all16 current ordinary early named writers.");
        foreach(var write in actualCaustics.NativeWrites)
        {
            var actual=actualNamedConstants.AdditionalWrites.SingleOrDefault(w=>w.Name==write.Name);
            var target=selected.PixelDefinitions.Buffers.Single(b=>b.Name.Text=="$Globals").Variables.Single(v=>v.Name.Text==write.Name);
            if(actual is null || actual.CurrentProducerProvenance!=write.CurrentProducerProvenance ||
                !actual.Bytes.Span.SequenceEqual(write.Bytes.Span) ||
                !actualNamedConstants.Bytes.Span.Slice(target.StartOffset,target.ByteSize).SequenceEqual(write.Bytes.Span))
                throw new InvalidDataException("Early cb0 differs from its exact typed current source Caustics writer.");
        }
        var globals=selected.PixelDefinitions.Buffers.Single(b=>b.Name.Text=="$Globals");
        var occurrence=globals.Variables.Single(v=>v.Name.Text=="vOccRate");
        var fade=globals.Variables.Single(v=>v.Name.Text=="fSdmFade");
        var bytes=actualNamedConstants.Bytes.Span;
        var reprojected=OriginalDeferredEarlySceneConstants.CreateOrdinary126(selected,lateShader,receipt.Lighting,
            new(new(BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(occurrence.StartOffset,4)),
                BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(occurrence.StartOffset+4,4)))),
            new(BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(fade.StartOffset,4))),
            actualCaustics.NativeWrites,actualNamedConstants.CurrentProducerProvenance);
        if(!reprojected.Bytes.Span.SequenceEqual(bytes))
            throw new InvalidDataException("Early cb0 does not match the exact current Camera/light/ambient named projection.");
        receipt.Early126ParametersValidated=true;
    }

    /// <summary>Check the independently prepared actual source Sky packet
    /// before its default PS3/PS4 consumers. Source mode2 reads only F80..FBF,
    /// all written by the complete loader; row0 supplies renderer4/profile1.</summary>
    public void ValidateCurrentSkyConsumer(OriginalMapSourceCreatedLightingRetainedConsumerReceipt receipt,
        OriginalMapSourceCreatedLightingPacket lighting, OriginalSky2SourceZeroMoon actualSourceSky,
        OriginalSkySceneSelectedViewLightPacket actualSelectedPacket)
    {
        ValidateReceipt(receipt); ValidateLighting(lighting); ArgumentNullException.ThrowIfNull(actualSourceSky);
        if(lighting.FrameNumber!=receipt.FrameNumber || Hash(lighting.LocalLightData.Span)!=receipt.LocalLightDataSha256)
            throw new InvalidDataException("Sky retained validation requires the exact accepted current lighting generation.");
        RequireInactiveProfile(skyShader,0,4);
        actualSourceSky.ValidateLighting(lighting); actualSourceSky.ValidatePacket(actualSelectedPacket);
        // This validates the selected Sun/Moon reader only. Sky VS consumes
        // fog.xy and Sky PS consumes Scat, which need their actual owner below.
        receipt.SkyLightVectorsValidated=true;
    }

    /// <summary>Capture actual independent atmosphere values without supplying
    /// any defaults. A native delayed Scat calculation from LightData cannot
    /// acquire this authority, irrespective of sourceMode or enable flags.</summary>
    public OriginalMapSourceCreatedLightingIndependentSkyAtmosphere CaptureCurrentIndependentSkyAtmosphere(
        OriginalMapSourceCreatedLightingRetainedConsumerReceipt receipt,
        OriginalMapSourceCreatedLightingIndependentSkyAtmosphereInputs input)
    {
        ValidateReceipt(receipt); ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Atmosphere);
        if(input.FrameNumber!=receipt.FrameNumber || input.ViewIdentity!=receipt.Lighting.Binding.ViewIdentity ||
            input.IndependentOfNormalizedLightData!=true || string.IsNullOrWhiteSpace(input.AtmosphereOwnerIdentity) ||
            string.IsNullOrWhiteSpace(input.CurrentProducerProvenance) || string.IsNullOrWhiteSpace(input.Atmosphere.SourceProvenance) ||
            input.Atmosphere.Fog is null || input.Atmosphere.Scattering is null || input.Atmosphere.Scattering.Count!=4 ||
            input.Atmosphere.Fog.Bytes.Length!=16)
            throw new InvalidDataException("Sky requires complete current independent atmosphere owner/values; native delayed Scat scope is unresolved.");
        RequireVectorPacket(input.Atmosphere.Fog.Value,input.Atmosphere.Fog.Bytes);
        foreach(var vector in input.Atmosphere.Scattering) RequireFinite(vector);
        if(input.Atmosphere.Fog.ScatteringOverride is { } scattering)
        {
            if(scattering.Values.Count!=4 || scattering.Bytes.Length!=64)
                throw new InvalidDataException("Independent current scattering override requires its complete four-vector packet.");
            for(int index=0;index<4;index++) RequireVectorPacket(scattering.Values[index],scattering.Bytes.AsSpan(index*16,16));
        }
        return new(receipt,input);
    }

    /// <summary>Complete Sky coverage against the actual frame consumed by
    /// its renderer, including the separately owned Fog/Scat fields. The
    /// selected-light capability alone cannot certify these semantic groups.</summary>
    public void ValidateCurrentSkyConsumer(OriginalMapSourceCreatedLightingRetainedConsumerReceipt receipt,
        OriginalMapSourceCreatedLightingPacket lighting, OriginalSky2SourceZeroMoon actualSourceSky,
        OriginalSkySceneSelectedViewLightPacket actualSelectedPacket,
        OriginalSkySceneSourceFramePacket actualSkyFrame,
        OriginalMapSourceCreatedLightingIndependentSkyAtmosphere actualAtmosphere)
    {
        ValidateCurrentSkyConsumer(receipt,lighting,actualSourceSky,actualSelectedPacket);
        ArgumentNullException.ThrowIfNull(actualSkyFrame); ArgumentNullException.ThrowIfNull(actualAtmosphere);
        if(!ReferenceEquals(actualAtmosphere.Receipt,receipt) || actualSkyFrame.FrameNumber!=receipt.FrameNumber ||
            actualSkyFrame.Binding!=lighting.Binding || !ReferenceEquals(actualSkyFrame.SourceFrame.Lights.SourceZeroMoon,actualSourceSky))
            throw new InvalidDataException("Sky retained validation requires the exact same-frame independent atmosphere and light capabilities.");
        actualAtmosphere.Validate(actualSkyFrame.SourceFrame.Atmosphere);
        var actualLights=actualSkyFrame.SourceFrame.Lights;
        var originalLights=OriginalSkySceneSelectedViewLightSource.PrepareSourceZeroMoonLights(actualSelectedPacket,
            actualLights.PackageLightPosition,"validation-only-current-package-position",actualSourceSky);
        // Controller11775C0 may replace Sun/Moon positions after the RGB
        // setters. Their position owner is the source frame/controller, while
        // these exact source RGB caches must remain the selected light's.
        if(!actualLights.SunPresent || !actualLights.MoonPresent ||
            actualLights.SunRgbCache1460!=originalLights.SunRgbCache1460 ||
            actualLights.RawMoonRgb!=originalLights.RawMoonRgb ||
            BitConverter.SingleToUInt32Bits(actualLights.MoonMaximum147C)!=0 ||
            actualLights.SourceMoonRgbCache1470!=originalLights.SourceMoonRgbCache1470)
            throw new InvalidDataException("Actual Sky RGB caches differ from the selected source light/Moon authority.");
        receipt.SkyAtmosphereValidated=true;
    }

    /// <summary>Validate the actual Sky named buffers before their draws. Both
    /// declared default PS3/PS4 packets must be checked for full coverage. Their
    /// active VS fog and PS3 Scat come from the captured independent atmosphere;
    /// no raw Generic cache or native delayed97CF70 result is admitted.</summary>
    public void ValidateCurrentSkyNamedConstants(OriginalMapSourceCreatedLightingRetainedConsumerReceipt receipt,
        OriginalMapSourceCreatedLightingIndependentSkyAtmosphere actualAtmosphere,
        OriginalSky2DrawInputs actualDraw, int actualPixelProgram,
        ReadOnlyMemory<byte> actualVertexCb0, ReadOnlyMemory<byte> actualPixelCb0)
    {
        ValidateReceipt(receipt); ArgumentNullException.ThrowIfNull(actualAtmosphere); ArgumentNullException.ThrowIfNull(actualDraw);
        if(!ReferenceEquals(actualAtmosphere.Receipt,receipt) || !receipt.SkyAtmosphereValidated ||
            actualPixelProgram is not (3 or 4) || actualVertexCb0.Length!=160 || actualPixelCb0.Length!=256 ||
            actualDraw.Pixel.SourceZeroMoon is not { } sourceSky)
            throw new InvalidDataException("Sky named validation requires the actual declared source programs and complete independent-atmosphere frame.");
        sourceSky.ValidateLighting(receipt.Lighting);
        var atmosphere=actualAtmosphere.Atmosphere;
        var scattering=atmosphere.Fog.ScatteringOverride?.Values??atmosphere.Scattering;
        if(actualDraw.Vertex.Fog!=atmosphere.Fog.Value || actualDraw.Pixel.Fog!=atmosphere.Fog.Value ||
            !actualDraw.Pixel.Scattering.SequenceEqual(scattering))
            throw new InvalidDataException("Actual Sky named Fog/Scat inputs differ from the independent current atmosphere.");
        var vertex=OriginalSky2Constants.Vertex(skyShader.ReadResourceDefinitions(0),actualVertexCb0.Span,actualDraw.Vertex);
        var pixel=OriginalSky2Constants.Pixel(skyShader.ReadResourceDefinitions(actualPixelProgram),actualPixelCb0.Span,actualDraw.Pixel);
        if(!vertex.Bytes.AsSpan().SequenceEqual(actualVertexCb0.Span) || !pixel.Bytes.AsSpan().SequenceEqual(actualPixelCb0.Span))
            throw new InvalidDataException("Actual Sky cb0 differs from its complete unchanged source named setters.");
        receipt.SkyNamedProgramMask |= actualPixelProgram==3?1:2;
    }

    private void ValidateLighting(OriginalMapSourceCreatedLightingPacket lighting)
    {
        ArgumentNullException.ThrowIfNull(lighting);
        if(construction is null || lighting.SourceIdentity.CreatedLightDataSha256!=createdHash ||
            lighting.SourceIdentity.ConstructedOwnerSha256!=constructedOwnerHash ||
            lighting.SourceIdentity.MainLightSha256!=construction.Source.Resource.Source.DecodedSha256 ||
            lighting.ContextFlags3E4!=construction.ContextFlags3E4 ||
            lighting.Binding.BasePath!=OriginalMapLightingBasePath.UnmodifiedOrdinary)
            throw new InvalidDataException("Lighting differs from this source construction/current owned normalization generation.");
        if(lighting.RuntimeTable==OriginalMapLightingRuntimeTable.Present)
        {
            if(lighting.AcceptedInputs.PresentSeasonCamera is not { } season)
                throw new InvalidDataException("Present retained consumers require the actual source season table/weather capability.");
            season.ValidatePreparedLighting(lighting);
        }
        else if(lighting.RuntimeTable!=OriginalMapLightingRuntimeTable.Absent || lighting.AcceptedInputs.PresentSeasonCamera is not null)
            throw new InvalidDataException("Retained consumers cannot infer a table branch from a declaration key or unresolved state.");
        byte[] restored=lighting.LocalLightData.ToArray(); RequireLightControls(restored);
        if(lighting.CameraContextApplied)
        {
            if(restored[0xF1E]!=2) throw new InvalidDataException("Current source Camera context writer differs.");
            restored[0xF1E]=1;
        }
        if(Hash(restored)!=createdHash)
            throw new InvalidDataException("An additional current LightData writer requires a new retained consumer proof.");
    }
    private void ValidateReceipt(OriginalMapSourceCreatedLightingRetainedConsumerReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if(!ReferenceEquals(receipt.Owner,this)) throw new InvalidDataException("Retained consumer receipt belongs to another backend owner.");
        ValidateLighting(receipt.Lighting); receipt.Late.Validate();
    }
    private static void RequireInactiveProfile(LoadedShaderContainer shader,int rowIndex,uint rendererMode)
    {
        // Reparse source bytes; mutable caller feature tables cannot certify a gate.
        var canonical=new LoadedShaderContainer(G2ShaderContainerReader.Read(shader.Source.Bytes),shader.Source);
        var rows=canonical.ReadG1Bindings().Features;
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
    private static void RequireFinite(System.Numerics.Vector4 vector)
    {
        if(!float.IsFinite(vector.X) || !float.IsFinite(vector.Y) || !float.IsFinite(vector.Z) || !float.IsFinite(vector.W))
            throw new InvalidDataException("Independent Sky atmosphere contains a nonfinite active vector.");
    }
    private static void RequireVectorPacket(System.Numerics.Vector4 vector,ReadOnlySpan<byte> bytes)
    {
        RequireFinite(vector); float[] values=[vector.X,vector.Y,vector.Z,vector.W];
        for(int index=0;index<4;index++)
            if(BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(index*4,4))!=BitConverter.SingleToUInt32Bits(values[index]))
                throw new InvalidDataException("Independent Sky atmosphere vector differs from its current byte packet.");
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
