using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>D3A850's actual package from one selected owned View+1B20/+8
/// LightData. This is the same selected complete packet, not another HDRI
/// allocation or Main's separate weather72 writer.</summary>
public sealed class OriginalSkySelectedLightAtmosphereOwner
{
    private readonly LoadedOriginalSkyAtmosphereNativeSource native;
    private readonly string source;
    private OriginalSkySelectedLightAtmosphereFrame? current;
    private long revision;
    private OriginalSkySelectedLightAtmosphereOwner(LoadedOriginalSkyAtmosphereNativeSource native,string owner,string source)
    {this.native=native;OwnerIdentity=owner;this.source=source;}
    public string OwnerIdentity{get;}
    public static OriginalSkySelectedLightAtmosphereOwner CreateFresh(OriginalGameRepository repository,string locale,
        string ownedAtmosphereIdentity,OriginalNativeLogDispatch explicitLogDispatch)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentException.ThrowIfNullOrWhiteSpace(ownedAtmosphereIdentity);
        var shader=repository.LoadShaderContainer("system",0x340AC335);
        if(shader.Source.OverrideSource is not null||shader.Source.FileId!=0x340AC335)
            throw new InvalidDataException("Selected Sky atmosphere requires the exact stock340AC335 source shader.");
        var row=shader.ReadG1Bindings().Features[0].Feature;
        if(row.Words[6]!=4||row.Words[7]!=1)
            throw new InvalidDataException("Actual selected stock Sky row0 renderer/profile changed.");
        var init=OriginalSkySceneSourceDefaults.SharedContextInitializer();
        if(!init.FogEnabled120||init.ScatteringPending121||init.Selector122!=1||init.RendererFogMode12C!=0||init.Profile130!=1)
            throw new InvalidDataException("Fresh Generic source context controls changed.");
        var n=OriginalSkyAtmosphereNativeSource.Load(repository,locale,explicitLogDispatch);
        string recipe=OriginalDeferredSamplerSources.ReadSelectedSkyPackageNativeRecipe(repository.SourceDirectory,locale);
        return new(n,ownedAtmosphereIdentity,recipe+";"+n.SourceProvenance+
            $";sourceShader={shader.Source.ResourceUri}@{shader.Source.DecodedSha256};selected-row0=renderer4/profile1;freshGeneric120=true/122=1");
    }
    public OriginalSkySelectedLightAtmosphereFrame PrepareCurrent(OriginalSkySceneSelectedViewLightPacket selectedPacket,
        OriginalMapSourceCreatedLightingPacket acceptedLighting)
    {
        ArgumentNullException.ThrowIfNull(selectedPacket);ArgumentNullException.ThrowIfNull(acceptedLighting);
        if(acceptedLighting.SourceProvenance.Contains("backend-owned-retained210-normalization",StringComparison.Ordinal)||
            !acceptedLighting.SourceProvenance.Contains("owned-current-allocation=",StringComparison.Ordinal))
            throw new InvalidDataException("Selected atmosphere requires a complete explicit owned retained allocation; normalized backend holes cannot supply package Fog96.");
        var id=selectedPacket.Identity;
        if((acceptedLighting.AcceptedInputs.PresentSeasonCamera is { } present&&present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale!=native.Locale)||id.FrameNumber!=acceptedLighting.FrameNumber||
            id.SelectedViewOwner!=acceptedLighting.Binding.ViewIdentity||id.ProviderPacketOwner!=acceptedLighting.Binding.LightOwnerIdentity)
            throw new InvalidDataException("Sky atmosphere requires the same accepted selected View record and complete current LightData.");
        ReadOnlySpan<byte> constructed=acceptedLighting.LocalLightData.Span;
        if(acceptedLighting.CameraContextApplied)
        {
            if(constructed[0xF1E]!=2)throw new InvalidDataException("Current mode2 source lighting writer changed.");
        }
        if(constructed[0x10B0]!=2||(constructed[0xFDC]&1)!=0)
            throw new NotSupportedException("Selected atmosphere closes the explicit source-created mode2 packet; additional current writers require their source continuation.");
        // D3AED4..D3AF45: package count1; row0 exact first32B; no
        // hemispheres; zero ambient float4; copy all96B including retained76.
        byte[] fog96=selectedPacket.CompleteLightData.Span.Slice(0x150,96).ToArray();
        uint mode=BinaryPrimitives.ReadUInt32LittleEndian(fog96.AsSpan(0x10,4));
        if(mode!=0)throw new NotSupportedException("Selected stock profile1 active Fog needs the separate original finalFog branch.");
        // Start0 directional commit marks121 even with count0 (97CBB9).
        // Actual count1 supplies row0. Inactive profile1 zeroFog preserves
        // pending; pre-submit976890 dispatch4 executes97CF70 then clears121.
        var fog=new OriginalSkyFogPacket(4,1,true,mode,Vector4.Zero,new byte[16],false,null);
        var b=selectedPacket.CompleteLightData.Span;
        var scat=OriginalSkyScatteringMode4.Generate(fog96,new(new(Read(b,0x10),Read(b,0x14),Read(b,0x18)),1,Vector4.Zero,null),native);
        revision=checked(revision+1);
        string provenance=source+";"+acceptedLighting.SourceProvenance+";"+selectedPacket.SourceProvenance+";owner="+OwnerIdentity+
            ";D3A850-package-copy->97C1F0-profile1-zeroFog->pending97CF70-Scat->121clear;explicit-owned-retained-template-not-original-currentMain-or-restart-default";
        current=new(this,revision,selectedPacket,acceptedLighting,fog96,new(provenance,fog,Array.AsReadOnly(scat.Values.ToArray())),provenance);
        return current;
    }
    internal void Validate(OriginalSkySelectedLightAtmosphereFrame frame,long capturedRevision)
    {if(current!=frame||revision!=capturedRevision)throw new InvalidDataException("Selected Sky atmosphere receipt is stale or superseded.");}
    private static float Read(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadSingleLittleEndian(b.Slice(at,4));
}

public sealed class OriginalSkySelectedLightAtmosphereFrame
{
    private readonly OriginalSkySelectedLightAtmosphereOwner owner;
    private readonly long revision;
    private readonly byte[] fog96;
    private readonly OriginalSkySceneSharedAtmosphere atmosphere;
    internal OriginalSkySelectedLightAtmosphereFrame(OriginalSkySelectedLightAtmosphereOwner owner,long revision,
        OriginalSkySceneSelectedViewLightPacket selected,OriginalMapSourceCreatedLightingPacket lighting,byte[] fog96,
        OriginalSkySceneSharedAtmosphere atmosphere,string provenance)
    {this.owner=owner;this.revision=revision;SelectedPacket=selected;AcceptedLighting=lighting;this.fog96=fog96;this.atmosphere=atmosphere;SourceProvenance=provenance;}
    public OriginalSkySceneSelectedViewLightPacket SelectedPacket{get;}
    public OriginalMapSourceCreatedLightingPacket AcceptedLighting{get;}
    public long FrameNumber=>SelectedPacket.Identity.FrameNumber;
    public string ViewIdentity=>SelectedPacket.Identity.SelectedViewOwner;
    public string SourceProvenance{get;}
    public ReadOnlyMemory<byte> CopiedPackageFog96=>fog96.ToArray();
    public OriginalSkySceneSharedAtmosphere Atmosphere=>atmosphere with{Fog=atmosphere.Fog with{Bytes=atmosphere.Fog.Bytes.ToArray()},Scattering=Array.AsReadOnly(atmosphere.Scattering.ToArray())};
    public void ValidateCurrent(OriginalSkySelectedLightAtmosphereOwner actualOwner,
        OriginalSkySceneSelectedViewLightPacket actualSelected,OriginalMapSourceCreatedLightingPacket actualLighting)
    {
        if(!ReferenceEquals(owner,actualOwner)||!ReferenceEquals(SelectedPacket,actualSelected)||!ReferenceEquals(AcceptedLighting,actualLighting))
            throw new InvalidDataException("Selected Sky atmosphere current owner/View/lighting mismatch.");
        owner.Validate(this,revision);
    }
}
