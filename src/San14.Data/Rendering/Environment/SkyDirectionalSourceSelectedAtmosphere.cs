using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual D3AE2D 12-DWORD request and D3AEC8 package copy.
/// These pure operations issue no owner, selected-frame or commit receipt.</summary>
public static class SkyDirectionalRendererFeatureWriter
{
    public static byte[] WriteRequest(ReadOnlySpan<byte> completeSelectedLightData)
    {
        if(completeSelectedLightData.Length!=CameraDirectionalLightData.LightDataBytes)
            throw new InvalidDataException("Sky renderer request needs the complete actual selected LightData copy.");
        uint mode=BinaryPrimitives.ReadUInt32LittleEndian(completeSelectedLightData.Slice(0x160,4));
        var result=new byte[48];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4),1);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(20),1);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(24),mode);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(28),mode==7?2u:1u);
        return result;
    }

    public static byte[] CopyPackageFog96(ReadOnlySpan<byte> selectedFog96,float actualMainCamera1EC)
    {
        if(selectedFog96.Length!=96||!float.IsFinite(actualMainCamera1EC))
            throw new InvalidDataException("Sky package copy needs the complete selected Fog96 and finite actual Main1EC.");
        byte[] result=selectedFog96.ToArray();
        if(BinaryPrimitives.ReadUInt32LittleEndian(selectedFog96.Slice(0x10,4))==7&&actualMainCamera1EC>0)
        {
            float k=BinaryPrimitives.ReadSingleLittleEndian(selectedFog96.Slice(0x44,4));
            if(!float.IsFinite(k))throw new InvalidDataException("Actual package Fog44 is outside the finite continuation domain.");
            float changed=NativeFloat.Add(NativeFloat.Multiply(
                NativeFloat.Subtract(1,actualMainCamera1EC),k),actualMainCamera1EC);
            if(!float.IsFinite(changed))throw new InvalidDataException("Original Main1EC package Fog44 continuation overflowed.");
            BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(0x44,4),changed);
        }
        return result;
    }
}

/// <summary>One current owned WORK/preparation generation freezes the actual
/// selected L and same Main1EC, writes the full request48, and retains the
/// separately matched source row. It never replaces the request with a row.</summary>
public sealed class SkyDirectionalRendererFeatureReceipt
{
    private readonly SkyDirectionalSelectedLightSource source;
    private readonly byte[] selectedLightData,mainPrefix,request48,matched48,lightFog96,packageFog96;
    private readonly Sky2Selection selection;

    internal SkyDirectionalRendererFeatureReceipt(SkyDirectionalSelectedLightSource source,
        LoadedShaderContainer shader,LoadedSkyDirectionalAtmosphereNativeSource native,
        SkyAppliedSourceStageFrame actualStages)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(native);
        ArgumentNullException.ThrowIfNull(actualStages);source.ValidateCurrent();
        this.source=source;ActualStages=actualStages;
        if(source.AcceptedLighting.AcceptedInputs.PresentSeasonCamera is not { } present||
            !ReferenceEquals(present,actualStages.AcceptedPresent)||!ReferenceEquals(source.AcceptedLighting,actualStages.AcceptedLighting)||
            present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale!=native.ScatteringSource.Locale)
            throw new InvalidDataException("Sky renderer feature needs the actual same selected lighting, owned WORK, Main and native locale.");
        actualStages.ValidateCurrent(source.AcceptedLighting,present.AcceptedSeasonCamera.Inputs.Source.Selection,present.CurrentSkyCamera);
        var main=present.AcceptedSeasonCamera.Inputs.MainFrame.Camera;
        // Source binding is ordinal928/hash273D81CE/type8/1 ->CPU98;
        // native TC C40B86/SC C42236 copy ->embedded1330+1EC.
        uint sourceWord=BitConverter.SingleToUInt32Bits(main.Source.Declaration.FogLightingBlend);
        mainPrefix=present.PreparedCameraPrefix.ToArray();
        if(BinaryPrimitives.ReadUInt32LittleEndian(main.Prefix.Span.Slice(0x1EC,4))!=sourceWord||
            BinaryPrimitives.ReadUInt32LittleEndian(mainPrefix.AsSpan(0x1EC,4))!=sourceWord||
            !source.AcceptedLighting.PreparedCameraPrefix.Span.SequenceEqual(mainPrefix)||
            !present.CurrentSkyCamera.PreparedCameraPrefix.Span.SequenceEqual(mainPrefix))
            throw new InvalidDataException("Current Main1EC lacks the actual audited source CPU98 copy; another current edit needs its own writer.");
        MainCamera1ECBits=sourceWord;MainCamera1EC=BitConverter.UInt32BitsToSingle(sourceWord);
        if(!float.IsFinite(MainCamera1EC))throw new InvalidDataException("Current Main1EC must be the finite audited source word.");
        selectedLightData=source.SelectedPacket.CompleteLightData.ToArray();
        lightFog96=selectedLightData.AsSpan(0x150,96).ToArray();
        if(!lightFog96.AsSpan().SequenceEqual(mainPrefix.AsSpan(0x340,96)))
            throw new InvalidDataException("Selected L96 and this actual Main prepared Fog96 have different writers.");
        packageFog96=SkyDirectionalRendererFeatureWriter.CopyPackageFog96(lightFog96,MainCamera1EC);
        request48=SkyDirectionalRendererFeatureWriter.WriteRequest(selectedLightData);
        selection=Sky2Selection.Read(shader);
        // Renderer matching is independent of desired/committed binding
        // variant words. Only the renderer match of this projection is kept.
        var projected=selection.SelectDesired(request48,true,false,selectedLightData[0x10B0],selectedLightData[0xFDC],true);
        matched48=projected.RendererFeature.Feature.RawBytes.ToArray();
        RendererFeatureIndex=projected.RendererFeatureMatch.RowIndex;
        RendererFogMode=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(matched48.AsSpan(24,4)));
        Profile=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(matched48.AsSpan(28,4)));
        if((RendererFogMode,Profile) is not ((4,1) or (7,2)))
            throw new NotSupportedException("Current Sky source-selected atmosphere supports the exact original4/1 or7/2 renderer rows.");
        SourceEvidence=native.SourceSelectedEvidence+";"+source.SourceProvenance+
            ";Main928/273D81CE/type8->CPU98->embedded1330+1EC;"+
            $"WORK={WorkIdentity};frame={FrameNumber};L={Hash(selectedLightData)};L96={Hash(lightFog96)};M96={Hash(packageFog96)};"+
            "D3AE2D-full-source-request48;980850-renderer-match/not-binding-commit;D3AF58-Main1EC-package-Fog44";
    }
    public SkyAppliedSourceStageFrame ActualStages{get;}
    public long FrameNumber=>ActualStages.FrameNumber;
    public string WorkIdentity=>ActualStages.OwnedWorkIdentity;
    public string MainIdentity=>ActualStages.OwnedMainIdentity;
    public SkySceneSelectedViewLightIdentity SelectedIdentity=>source.SelectedPacket.Identity;
    public uint SourceFogMode=>BinaryPrimitives.ReadUInt32LittleEndian(lightFog96.AsSpan(0x10,4));
    public int RendererFeatureIndex{get;}
    public int RendererFogMode{get;}
    public int Profile{get;}
    public float MainCamera1EC{get;}
    public uint MainCamera1ECBits{get;}
    public bool PackageFog44Written=>SourceFogMode==7&&MainCamera1EC>0;
    public string SourceEvidence{get;}
    public ReadOnlyMemory<byte> RequestFeature48=>request48.ToArray();
    public ReadOnlyMemory<byte> MatchedFeature48=>matched48.ToArray();
    public ReadOnlyMemory<byte> SelectedLightFog96=>lightFog96.ToArray();
    public ReadOnlyMemory<byte> PackageFog96=>packageFog96.ToArray();

    public void ValidateCurrent(SkyDirectionalSelectedLightSource actualSource)
    {
        if(!ReferenceEquals(source,actualSource))throw new InvalidDataException("Sky source renderer feature belongs to another selected source.");
        source.ValidateCurrent();
        var present=ActualStages.AcceptedPresent;
        ActualStages.ValidateCurrent(source.AcceptedLighting,present.AcceptedSeasonCamera.Inputs.Source.Selection,present.CurrentSkyCamera);
        if(!source.SelectedPacket.CompleteLightData.Span.SequenceEqual(selectedLightData)||
            !present.PreparedCameraPrefix.Span.SequenceEqual(mainPrefix))
            throw new InvalidDataException("Sky source renderer feature's actual selected L/Main generation changed.");
    }
    public void ValidateStage(SkyAppliedSourceStageFrame actualStages)
    {ValidateCurrent(source);if(!ReferenceEquals(actualStages,ActualStages))throw new InvalidDataException("Sky renderer feature belongs to this exact owned WORK/preparation generation.");}
    internal Sky2SelectionResult ProjectDesired(bool moonPresent,bool sunArgumentNull,bool atmosphere)
    {ValidateCurrent(source);return selection.SelectDesired(request48,moonPresent,sunArgumentNull,selectedLightData[0x10B0],selectedLightData[0xFDC],atmosphere);}
    private static string Hash(ReadOnlySpan<byte> value)=>Convert.ToHexString(SHA256.HashData(value));
}

/// <summary>Actual source-selected shared Generic Fog/Scat CPU output for
/// the same selected L, Main1EC package continuation and owned WORK generation.
/// Pending is consumed by the appropriate original writer before Cloud can
/// inherit Scat. No atmosphere draw or native binding commit is declared.</summary>
public sealed class SkyDirectionalSourceSelectedAtmosphereReceipt
{
    private readonly SkyDirectionalSelectedLightSource source;
    private readonly byte[] fog16,scat64;
    private readonly Vector4[] scattering;
    private readonly SkySceneSharedAtmosphere atmosphere;
    private readonly bool requiresNeutralScattering;
    private readonly SkyDirectionalMode4FogReceipt? legacyFogProof;
    internal SkyDirectionalSourceSelectedAtmosphereReceipt(SkyDirectionalSelectedLightSource source,
        SkyDirectionalRendererFeatureReceipt feature,LoadedSkyDirectionalAtmosphereNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(feature);ArgumentNullException.ThrowIfNull(native);
        feature.ValidateCurrent(source);this.source=source;RendererFeature=feature;
        var init=SkySceneSourceDefaults.SharedContextInitializer();
        if(!init.FogEnabled120||init.ScatteringPending121||init.Selector122!=1||init.RendererFogMode12C!=0||init.Profile130!=1)
            throw new InvalidDataException("Source-selected Sky requires the actual fresh Generic initializer before renderer matching.");
        if(native.ScatteringSource.Locale!=feature.ActualStages.AcceptedPresent.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale)
            throw new InvalidDataException("Source-selected atmosphere native locale differs from its actual Main.");
        SkyFogPacket fog;SkyScatteringPacket scat;
        // The actual one-row package light registration sets pending121.
        ScatteringPendingBeforeFog=true;
        SourceActive=init.FogEnabled120&&feature.SourceFogMode!=0;
        if(feature.RendererFogMode==7)
        {
            fog=SkyFogMath.Generate(7,feature.Profile,init.FogEnabled120,feature.PackageFog96.Span);
            if(fog.RequiresMode7NeutralScattering)
            {scat=fog.ScatteringOverride!;ScatteringPendingAfterFog=false;ScatteringWriter="97C64E-neutral64/121clear";}
            else
            {
                ScatteringPendingAfterFog=SourceActive||ScatteringPendingBeforeFog;
                if(!ScatteringPendingAfterFog)throw new InvalidDataException("Source-selected Sky7 has no current pending Scat producer.");
                scat=SkyScatteringMath.Generate(7,feature.PackageFog96.Span,source.Fields.PackageDirectionalRgb);
                ScatteringWriter="976909->97CCE0-active-pending/97690E-clear121";
            }
        }
        else
        {
            // A source request mode0 matches row4/1, but remains sourceMode0.
            // Its original package copy has no mode7 Main1EC continuation.
            var legacy=source.PrepareAtmosphere(native);fog=legacy.Fog;
            scat=new(4,Array.AsReadOnly(legacy.Scattering.ToArray()),Pack(legacy.Scattering));
            ScatteringPendingAfterFog=true;ScatteringWriter="9768F2->97CF70-pending/97690E-clear121";
        }
        fog16=fog.Bytes.ToArray();scat64=scat.Bytes.ToArray();scattering=scat.Values.ToArray();
        requiresNeutralScattering=fog.RequiresMode7NeutralScattering;legacyFogProof=fog.SourceMode4Proof;
        if(scattering.Length!=4||scat64.Length!=64||scattering.SelectMany(Words).Any(v=>!float.IsFinite(v)))
            throw new InvalidDataException("Current source-selected Scat is outside the finite original writer domain.");
        ScatteringPendingAfterPreparation=false;
        atmosphere=new(feature.SourceEvidence+";"+ScatteringWriter+";same-source-selected-Fog16/Scat64/no-atmosphere-draw",
            fog with{Bytes=fog16.ToArray()},Array.AsReadOnly(scattering.ToArray()));
    }
    public SkyDirectionalRendererFeatureReceipt RendererFeature{get;}
    public SkyAppliedSourceStageFrame ActualStages=>RendererFeature.ActualStages;
    public SkySceneSharedAtmosphere Atmosphere=>atmosphere;
    public bool SourceActive{get;}
    public bool ScatteringPendingBeforeFog{get;}
    public bool ScatteringPendingAfterFog{get;}
    public bool ScatteringPendingAfterPreparation{get;}
    public string ScatteringWriter{get;}
    public int RendererFogMode=>RendererFeature.RendererFogMode;
    public int Profile=>RendererFeature.Profile;
    public long FrameNumber=>RendererFeature.FrameNumber;
    public string WorkIdentity=>RendererFeature.WorkIdentity;
    public ReadOnlyMemory<byte> Fog16=>fog16.ToArray();
    public ReadOnlyMemory<byte> Scat64=>scat64.ToArray();
    public void ValidateCurrent(SkyDirectionalSelectedLightSource actualSource)
    {RendererFeature.ValidateCurrent(actualSource);ValidateAtmosphere(atmosphere);}
    public void ValidateStage(SkyAppliedSourceStageFrame actualStages)=>RendererFeature.ValidateStage(actualStages);
    public void ValidateAtmosphere(SkySceneSharedAtmosphere actual)
    {
        var fog=actual.Fog;
        if(fog.RendererFogMode!=RendererFogMode||fog.Profile!=Profile||fog.SourceFogMode!=RendererFeature.SourceFogMode||
            !fog.FogEnabled||!fog.Bytes.AsSpan().SequenceEqual(fog16)||!Pack([fog.Value]).AsSpan().SequenceEqual(fog16)||
            !actual.Scattering.SequenceEqual(scattering)||!Pack(actual.Scattering).AsSpan().SequenceEqual(scat64)||
            fog.RequiresMode7NeutralScattering!=requiresNeutralScattering||
            !ReferenceEquals(fog.SourceMode4Proof,legacyFogProof)||
            (requiresNeutralScattering!=(fog.ScatteringOverride is not null))||
            (fog.ScatteringOverride is { } overridePacket&&!overridePacket.Bytes.AsSpan().SequenceEqual(scat64))||
            ScatteringPendingAfterPreparation)
            throw new InvalidDataException("Source-selected Sky shared atmosphere differs from its actual same-source Fog/Scat writer receipt.");
    }
    public void ValidateFrame(SkySceneSourceFramePacket actualFrame)
        =>ValidateFrame(actualFrame,source);
    public void ValidateFrame(SkySceneSourceFramePacket actualFrame,SkyDirectionalSelectedLightSource actualSource)
    {
        ValidateCurrent(actualSource);source.ValidateActualAppliedFrame(actualFrame);
        if(!ReferenceEquals(actualFrame.SourceFrame.SourceSelectedAtmosphere,this)||actualFrame.FrameNumber!=FrameNumber||
            actualFrame.Binding!=source.AcceptedLighting.Binding)
            throw new InvalidDataException("Source-selected atmosphere belongs to its actual current applied Sky frame.");
        ValidateAtmosphere(actualFrame.SourceFrame.Atmosphere);
    }
    internal void ValidateSourceFrame(SkySceneSourceFrame actualFrame,SkyDirectionalSelectedLightSource actualSource)
    {ValidateFrame(actualSource.RequireActualAppliedSourceFrame(actualFrame),actualSource);}
    private static float[] Words(Vector4 v)=>[v.X,v.Y,v.Z,v.W];
    private static byte[] Pack(IReadOnlyList<Vector4> values)
    {var result=new byte[values.Count*16];for(int row=0;row<values.Count;row++){float[] words=Words(values[row]);for(int lane=0;lane<4;lane++)BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(row*16+lane*4),words[lane]);}return result;}
}
