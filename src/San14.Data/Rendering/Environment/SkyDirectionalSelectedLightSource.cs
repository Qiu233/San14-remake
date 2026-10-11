using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual D7 global setter fields and D3A850's ordinary row0 package.
/// The two fourth color words are retained transport words, not RGB/max or an
/// invented alpha. Fog is copied from the current prepared Camera by CBC770.</summary>
public sealed record SkyDirectionalSelectedLightFields(Vector4 SunPosition, Vector3 SunRgb,
    uint SunFourthWord, Vector4 MoonPosition, Vector3 MoonRgb, uint MoonFourthWord,
    Vector4 PackagePosition, Vector3 PackageDirectionalRgb, uint PackageColorFourthWord);

/// <summary>A same-frame Directional mode6/B2=8 selected record, validated by
/// its own constructor and current Camera writer. Constructor-only retained
/// Fog96 and mode2/HDRI allocation receipts cannot enter this source path.</summary>
public sealed class SkyDirectionalSelectedLightSource
{
    private readonly byte[] fog96;
    private SkySceneLights? preparedLights;
    private SkySceneSourceFramePacket? preparedAppliedFrame;
    private long preparedSetterRevision;
    private long colorSetterRevision;
    private SkyDirectionalSetterColorReceipt? preparedColorReceipt;
    private SkyDirectionalSelectedLightSource(SkySceneSelectedViewLightPacket selected,
        MapSourceCreatedLightingPacket lighting, SkyDirectionalSelectedLightFields fields)
    {
        SelectedPacket = selected; AcceptedLighting = lighting; Fields = fields;
        fog96 = selected.CompleteLightData.Span.Slice(0x150, 96).ToArray();
        SourceProvenance = lighting.SourceProvenance + ";" + selected.SourceProvenance +
            ";Directional-mode6/current-CBC770;F80/F90=Camera2B8/2FC;FA0/FB0=Camera2C4/308;" +
            "Fog150/96B=Camera340/96B;D7CF4E/5D-SunMoon-setters;D3AED4-package-one-row0/no-hemi/zero-ambient";
    }
    public SkySceneSelectedViewLightPacket SelectedPacket { get; }
    public MapSourceCreatedLightingPacket AcceptedLighting { get; }
    public SkyDirectionalSelectedLightFields Fields { get; }
    public string SourceProvenance { get; }
    public ReadOnlyMemory<byte> PackageFog96 => fog96.ToArray();
    public bool HasExactZeroMoonRgb => SelectedPacket.CompleteLightData.Span.Slice(0xFB0, 12).SequenceEqual(new byte[12]);
    public SkyDirectionalSetterColorReceipt? CurrentColorSetterReceipt=>preparedColorReceipt;

    public static SkyDirectionalSelectedLightSource ReadCurrent(
        SkySceneSelectedViewLightPacket selected, MapSourceCreatedLightingPacket lighting)
    {
        Validate(selected, lighting);
        return new(selected, lighting, ReadNamedFields(selected.CompleteLightData.Span));
    }

    public void ValidateCurrent() => Validate(SelectedPacket, AcceptedLighting);

    private static void Validate(SkySceneSelectedViewLightPacket selected, MapSourceCreatedLightingPacket lighting)
    {
        ArgumentNullException.ThrowIfNull(selected); ArgumentNullException.ThrowIfNull(lighting);
        var identity = selected.Identity; var b = lighting.LocalLightData.Span;
        if (lighting.SourceLightClassId != DirectionalLightSource.SourceClass ||
            !lighting.CameraContextApplied || !lighting.CameraLightingApplied || b[0x10B0] != 6 || b[0x10B2] != 8 ||
            lighting.SourceIdentity.DeclaredGroupMask != 0x80 || identity.FrameNumber != lighting.FrameNumber ||
            identity.SelectedViewOwner != lighting.Binding.ViewIdentity || identity.ProviderPacketOwner != lighting.Binding.LightOwnerIdentity)
            throw new InvalidDataException("Directional Sky requires the actual selected group80 mode6 record and its complete same-frame Camera writes.");
    }

    /// <summary>Nonnull setters consume the actual current global fields.
    /// Exact zero Moon enters its separately pinned PS10/4 or PS3/4 capability;
    /// other packets use the ordinary finite setter preparation.</summary>
    public SkySceneLights PrepareLights(LoadedShaderContainer skyShader, Sky2TextureBindingSnapshot textureBindings)
    {
        textureBindings.RequireCurrent();
        ValidateCurrent();
        preparedColorReceipt=null;colorSetterRevision=checked(colorSetterRevision+1);
        if (HasExactZeroMoonRgb)
        {
            var zero = Sky2SourceZeroMoon.CreateDirectionalSelected(skyShader, textureBindings.RequireNull(SkySamplingRole.Moon), this);
            preparedLights = SkySceneSelectedViewLightSource.PrepareSourceZeroMoonLights(SelectedPacket,
                Fields.PackagePosition, SelectedPacket.Identity.ProviderRecordOwner + "/D3A850-package-row0", zero);
        }
        else preparedLights = SkySceneSelectedViewLightSource.PrepareSkyLights(SelectedPacket,
            Fields.PackagePosition, SelectedPacket.Identity.ProviderRecordOwner + "/D3A850-package-row0");
        preparedAppliedFrame = null;
        preparedSetterRevision = checked(preparedSetterRevision + 1);
        return preparedLights;
    }

    /// <summary>D3A850's actual global baseline→same Main HS/temperature
    /// continuation before nonnull Sun/Moon setters. Legacy two-argument D7
    /// preparation remains a distinct direct-baseline scope.</summary>
    public SkySceneLights PrepareD3PackageLights(LoadedShaderContainer skyShader,Sky2TextureBindingSnapshot textureBindings,
        LoadedSkyDirectionalSetterColorNativeSource native)
    {
        textureBindings.RequireCurrent();
        ValidateCurrent();long next=checked(colorSetterRevision+1);
        var receipt=new SkyDirectionalSetterColorReceipt(this,native,next);
        SkySceneLights lights;
        if(HasExactZeroMoonRgb)
        {
            var zero=Sky2SourceZeroMoon.CreateDirectionalSelected(skyShader,textureBindings.RequireNull(SkySamplingRole.Moon),this);
            lights=SkySceneSelectedViewLightSource.PrepareSourceZeroMoonLights(SelectedPacket,Fields.PackagePosition,
                SelectedPacket.Identity.ProviderRecordOwner+"/D3A850-package-row0",zero);
            if(receipt.MoonRgb!=Vector3.Zero)throw new InvalidDataException("Source-zero Moon capability cannot cover a changed D3 color.");
        }
        else lights=new(receipt.SourceEvidence,true,Fields.SunPosition,Sky2CameraLightPreparation.PrepareRgbCache(receipt.SunRgb),
            true,Fields.MoonPosition,receipt.MoonRgb,Sky2CameraLightPreparation.PrepareRgbCache(receipt.MoonRgb).Maximum,Fields.PackagePosition);
        lights=lights with{SunRgbCache1460=Sky2CameraLightPreparation.PrepareRgbCache(receipt.SunRgb),
            SourceProvenance=lights.SourceProvenance+";"+receipt.SourceEvidence};
        preparedLights=lights;preparedAppliedFrame=null;colorSetterRevision=next;preparedColorReceipt=receipt;
        preparedSetterRevision=checked(preparedSetterRevision+1);receipt.ValidateLights(lights,this);return lights;
    }
    internal void ValidateColorReceiptOwner(SkyDirectionalSetterColorReceipt receipt,long revision)
    {if(!ReferenceEquals(receipt,preparedColorReceipt)||revision!=colorSetterRevision)throw new InvalidDataException("D3 color setter receipt is stale or superseded.");}
    internal void ValidateActualColorPreparedLights(SkySceneLights lights)
    {if(!ReferenceEquals(preparedLights,lights))throw new InvalidDataException("D3 color receipt needs its actual current setter instance.");}

    public void ValidatePreparedLights(SkySceneLights lights)
    {
        ValidateCurrent();
        if (!ReferenceEquals(preparedLights, lights) || !lights.SunPresent || !lights.MoonPresent)
            throw new InvalidDataException("Directional Sky frame must consume this source's actual nonnull Sun/Moon setter receipt.");
        lights.SourceZeroMoon?.ValidateLighting(AcceptedLighting);
        preparedColorReceipt?.ValidateLights(lights,this);
    }

    /// <summary>Project the desired native words from this source's actual
    /// nonnull setter receipts. The complete renderer feature is an explicit
    /// owned caller input, independent of cached original renderer indices.
    /// Texture absence and zero RGB never clear the Moon setter word.</summary>
    public SkyDirectionalDesiredSelectionReceipt CaptureDesiredSelection(
        LoadedShaderContainer skyShader, SkySceneLights actualPreparedLights,
        ReadOnlySpan<byte> explicitOwnedRendererFeature48, string rendererFeatureWriterEvidence)
    {
        ValidatePreparedLights(actualPreparedLights);
        ValidateCurrentRendererRequest(explicitOwnedRendererFeature48);
        return new(this, skyShader, actualPreparedLights, null, preparedSetterRevision,
            explicitOwnedRendererFeature48, rendererFeatureWriterEvidence);
    }

    /// <summary>The same desired projection after the actual 11775C0 position
    /// continuation. A caller-created frame record cannot acquire the receipt.</summary>
    public SkyDirectionalDesiredSelectionReceipt CaptureDesiredSelection(
        LoadedShaderContainer skyShader, SkySceneSourceFramePacket actualAppliedFrame,
        ReadOnlySpan<byte> explicitOwnedRendererFeature48, string rendererFeatureWriterEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualAppliedFrame);
        ValidateSelectionReceipt(actualAppliedFrame.SourceFrame.Lights, actualAppliedFrame, preparedSetterRevision);
        ValidateCurrentRendererRequest(explicitOwnedRendererFeature48);
        return new(this, skyShader, actualAppliedFrame.SourceFrame.Lights, actualAppliedFrame,
            preparedSetterRevision, explicitOwnedRendererFeature48, rendererFeatureWriterEvidence);
    }

    internal void ValidateSelectionReceipt(SkySceneLights actualLights,
        SkySceneSourceFramePacket? actualFrame, long setterRevision)
    {
        if (setterRevision != preparedSetterRevision ||
            (actualFrame is not null && !ReferenceEquals(preparedAppliedFrame, actualFrame)))
            throw new InvalidDataException("Directional desired selection belongs to the current actual setter/position continuation.");
        ValidatePreparedLights(actualLights);
        if(preparedAppliedFrame?.SourceFrame.SourceSelectedAtmosphere is { } atmosphere)
            atmosphere.ValidateFrame(preparedAppliedFrame,this);
    }

    private void ValidateCurrentRendererRequest(ReadOnlySpan<byte> explicitFeature)
    {
        if(preparedAppliedFrame?.SourceFrame.SourceSelectedAtmosphere is not { } atmosphere)return;
        atmosphere.ValidateFrame(preparedAppliedFrame,this);
        if(!explicitFeature.SequenceEqual(atmosphere.RendererFeature.RequestFeature48.Span))
            throw new InvalidDataException("Source-selected current Sky cannot replace its actual request48 with a caller-selected row or unrelated feature.");
    }

    internal void ValidateActualAppliedFrame(SkySceneSourceFramePacket actualFrame)
    {
        if(!ReferenceEquals(preparedAppliedFrame,actualFrame))
            throw new InvalidDataException("Directional source requires its actual current applied frame, not a matching clone.");
        ValidatePreparedLights(actualFrame.SourceFrame.Lights);
    }
    internal SkySceneSourceFramePacket RequireActualAppliedSourceFrame(SkySceneSourceFrame actualFrame)
    {
        if(preparedAppliedFrame is not { } frame||!ReferenceEquals(frame.SourceFrame,actualFrame))
            throw new InvalidDataException("Directional source frame has no actual current applied owner.");
        ValidateActualAppliedFrame(frame);return frame;
    }

    public SkyDirectionalRendererFeatureReceipt CaptureSourceRendererFeature(
        LoadedShaderContainer skyShader,LoadedSkyDirectionalAtmosphereNativeSource native,
        SkyAppliedSourceStageFrame actualStages)
        =>new(this,skyShader,native,actualStages);

    public SkyDirectionalSourceSelectedAtmosphereReceipt PrepareSourceSelectedAtmosphere(
        SkyDirectionalRendererFeatureReceipt rendererFeature,LoadedSkyDirectionalAtmosphereNativeSource native)
        =>new(this,rendererFeature,native);

    /// <summary>Current D3A850 source renderer request, actual package Fog96
    /// continuation and pending Scat writer. Legacy mode4 preparation remains
    /// a separate explicit entry. The current receipt does not commit native
    /// desired variant state or execute an atmosphere pass.</summary>
    public SkySceneSourceFramePacket PrepareSourceSelectedSeasonFrame(
        CubeRenderTextureDefinition target,LoadedSkyDirectionalAtmosphereNativeSource native,
        int nativeClipProfile,LoadedShaderContainer skyShader,SkyAppliedSourceStageFrame actualStages,
        Sky2TextureBindingSnapshot textureBindings)
    {
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(native);ValidateCurrent();
        if(preparedLights is null||AcceptedLighting.AcceptedInputs.PresentSeasonCamera is not { } present)
            throw new InvalidDataException("Source-selected season frame needs its actual nonnull Sun/Moon setter receipts and Main.");
        PrepareD3PackageLights(skyShader,textureBindings,native.ColorSetterContinuationSource);
        var feature=CaptureSourceRendererFeature(skyShader,native,actualStages);
        var atmosphere=PrepareSourceSelectedAtmosphere(feature,native);
        var camera=present.CurrentSkyCamera;
        var controller=PrepareActualMainController(present);
        var frame=SkySceneSourceFrameProvider.PrepareAppliedSeason(
            present.AcceptedSeasonCamera.Inputs.Source.Selection,camera,AcceptedLighting,target,
            preparedLights,atmosphere.Atmosphere,controller,nativeClipProfile,textureBindings);
        frame=frame with{SourceFrame=frame.SourceFrame with{SourceSelectedAtmosphere=atmosphere},SourceProvenance=frame.SourceProvenance+";"+feature.SourceEvidence};
        preparedLights=frame.SourceFrame.Lights;preparedAppliedFrame=frame;
        preparedSetterRevision=checked(preparedSetterRevision+1);
        atmosphere.ValidateFrame(frame,this);return frame;
    }

    public SkyDirectionalDesiredSelectionReceipt CaptureDesiredSelection(
        LoadedShaderContainer skyShader,SkySceneSourceFramePacket actualAppliedFrame)
    {
        var atmosphere=actualAppliedFrame.SourceFrame.SourceSelectedAtmosphere??
            throw new InvalidDataException("Current source-selected desired projection needs the actual renderer request and shared atmosphere receipt.");
        atmosphere.ValidateFrame(actualAppliedFrame,this);
        return CaptureDesiredSelection(skyShader,actualAppliedFrame,atmosphere.RendererFeature.RequestFeature48.Span,
            atmosphere.RendererFeature.SourceEvidence);
    }

    /// <summary>D7's actual selected season/Camera controller continuation:
    /// 11775C0 updates the attached Sun/Moon positions after their RGB setters.
    /// The accepted current owner supplies all inputs; no caller-created light
    /// clone or unrelated seasonal Camera can acquire this receipt.</summary>
    public SkySceneSourceFramePacket PrepareAppliedSeasonFrame(
        CubeRenderTextureDefinition target, LoadedSkyDirectionalAtmosphereNativeSource native,
        int nativeClipProfile, Sky2TextureBindingSnapshot textureBindings)
    {
        ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(native);
        ValidateCurrent();
        if (preparedLights is null || AcceptedLighting.AcceptedInputs.PresentSeasonCamera is not { } present)
            throw new InvalidDataException("Directional applied season frame needs its actual prepared RGB setter receipt and current Camera.");
        var camera = present.CurrentSkyCamera;
        var controller = PrepareActualMainController(present);
        var frame = SkySceneSourceFrameProvider.PrepareAppliedSeason(
            present.AcceptedSeasonCamera.Inputs.Source.Selection, camera, AcceptedLighting, target,
            preparedLights, PrepareAtmosphere(native), controller, nativeClipProfile, textureBindings);
        preparedLights = frame.SourceFrame.Lights;
        preparedAppliedFrame = frame;
        preparedSetterRevision = checked(preparedSetterRevision + 1);
        ValidatePreparedLights(preparedLights);
        return frame;
    }

    private SkyScenePreparedController PrepareActualMainController(MapSourceCreatedSeasonLightingFrame present)
    {
        var camera=present.CurrentSkyCamera;
        if(camera.FrameNumber!=AcceptedLighting.FrameNumber||
            !camera.PreparedCameraPrefix.Span.SequenceEqual(AcceptedLighting.PreparedCameraPrefix.Span))
            throw new InvalidDataException("Directional D3 controller needs its actual same-frame selected Main Camera prefix.");
        // D3AB9F..D3ABB2 reads the prepared Main278 word and publishes
        // controller1F0 after multiplying by100. The declaration-only Main
        // profile is not the current externally applied seasonal Camera.
        var current=SkySceneSourceDefaults.ApplyPreparedMainControllerInputs(
            camera.Controller,AcceptedLighting.PreparedCameraPrefix.Span);
        if(BitConverter.SingleToUInt32Bits(current.Scalar1F0)!=BitConverter.SingleToUInt32Bits(camera.Controller.Scalar1F0))
            throw new InvalidDataException("Directional D3 controller lacks its same-Main prepared278-to-1F0 writer.");
        return SkySceneSourceDefaults.PrepareController(camera.Controller,camera.Parameters,true,true);
    }

    /// <summary>Actual stock feature0/profile1, Fog mode0 D3A850 package path.
    /// This preserves the current96B transport and applies the existing original
    /// four-row97CF70 arithmetic; active Fog needs its separate finalFog writer.</summary>
    public SkySceneSharedAtmosphere PrepareInactiveFogAtmosphere(LoadedSkyAtmosphereNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native); ValidateCurrent();
        if (AcceptedLighting.AcceptedInputs.PresentSeasonCamera is not { } present ||
            present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale != native.Locale)
            throw new InvalidDataException("Directional Sky atmosphere requires the same original locale and seasonal Camera producer.");
        uint mode = BinaryPrimitives.ReadUInt32LittleEndian(fog96.AsSpan(0x10));
        if (mode != 0) throw new NotSupportedException("Directional active Fog must use its source-backed finalFog writer.");
        var init = SkySceneSourceDefaults.SharedContextInitializer();
        if (!init.FogEnabled120 || init.Selector122 != 1 || init.RendererFogMode12C != 0 || init.Profile130 != 1)
            throw new InvalidDataException("Directional Sky requires the original stock Generic atmosphere controls.");
        var fog = new SkyFogPacket(4, 1, true, mode, Vector4.Zero, new byte[16], false, null);
        var scat = SkyScatteringMode4.Generate(fog96, new(Fields.PackageDirectionalRgb, 1, Vector4.Zero, null), native);
        return new(SourceProvenance + ";" + native.SourceProvenance +
            ";D3A850-one-row0->97C1F0-profile1-zeroFog/pending97CF70-Scat->121clear",
            fog, Array.AsReadOnly(scat.Values.ToArray()));
    }

    /// <summary>The actual stock mode4/profile1 finalFog branch for the
    /// current Main Weather Fog96, including its active source-mode7 record.</summary>
    public SkySceneSharedAtmosphere PrepareAtmosphere(LoadedSkyDirectionalAtmosphereNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native); ValidateCurrent();
        if (AcceptedLighting.AcceptedInputs.PresentSeasonCamera is not { } present ||
            present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale != native.ScatteringSource.Locale)
            throw new InvalidDataException("Directional atmosphere source locale differs from its current Camera.");
        var init = SkySceneSourceDefaults.SharedContextInitializer();
        if (!init.FogEnabled120 || init.Selector122 != 1 || init.Profile130 != 1)
            throw new InvalidDataException("Directional atmosphere needs the actual stock Generic controls.");
        var fog = SkyDirectionalMode4FogReceipt.Generate(this, native);
        var scat = SkyScatteringMode4.Generate(fog96, new(Fields.PackageDirectionalRgb, 1, Vector4.Zero, null), native.ScatteringSource);
        return new(SourceProvenance + ";" + native.FogSourceProvenance + ";" + native.ScatteringSource.SourceProvenance,
            fog, Array.AsReadOnly(scat.Values.ToArray()));
    }

    internal static SkyDirectionalSelectedLightFields ReadNamedFields(ReadOnlySpan<byte> b)
    {
        if (b.Length != CameraDirectionalLightData.LightDataBytes || b[0x10B0] != 6 || b[0x10B2] != 8)
            throw new InvalidDataException("Directional named Sky fields require the full mode6/B2=8 current payload.");
        return new(Vector(b, 0xF80), Rgb(b, 0xFA0), Word(b, 0xFAC), Vector(b, 0xF90), Rgb(b, 0xFB0), Word(b, 0xFBC),
            Vector(b, 0), Rgb(b, 0x10), Word(b, 0x1C));
    }
    private static uint Word(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(at, 4));
    private static Vector3 Rgb(ReadOnlySpan<byte> b, int at) => new(Read(b, at), Read(b, at + 4), Read(b, at + 8));
    private static Vector4 Vector(ReadOnlySpan<byte> b, int at) => new(Read(b, at), Read(b, at + 4), Read(b, at + 8), Read(b, at + 12));
    private static float Read(ReadOnlySpan<byte> b, int at)
    { float value = BinaryPrimitives.ReadSingleLittleEndian(b.Slice(at, 4)); return float.IsFinite(value) ? value : throw new InvalidDataException("Directional Sky named float is nonfinite."); }
}
