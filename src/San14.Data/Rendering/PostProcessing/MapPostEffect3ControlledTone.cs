using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Snapshot of the actual selected owned texture's native +28 metadata
/// chain. Null is an explicit null-pointer observation, not a missing API value.
/// Presence is derived from ID16, never supplied as a boolean. No GPU claim.</summary>
public sealed class MapPostEffect3ControlledToneMetadata
{
    private readonly MapPostEffect3ParameterState state;
    private readonly MapPostEffect3AllocationPlan allocation;
    private readonly byte[] neutral0,neutral1;
    private MapPostEffect3ControlledToneMetadata(MapPostEffect3ParameterState state,
        MapPostEffect3AllocationPlan allocation,MapPostEffect3ApplyView view,
        string textureIdentity,string textureSourceIdentity,string callerEvidence,bool nullPointer,
        int? offset,int type,int gamut,byte[] p0,byte[] p1,string evidence)
    {
        this.state=state;this.allocation=allocation;FrameNumber=view.FrameNumber;
        ContextOwnerIdentity=view.OwnerIdentity;OwnedTextureIdentity=textureIdentity;
        TextureSourceIdentity=textureSourceIdentity;CallerTextureSourceEvidence=callerEvidence;
        SourceMetadataPointerWasNull=nullPointer;Metadata16ValueByteOffset=offset;
        TonemapType=type;ColorGamut=gamut;neutral0=p0;neutral1=p1;SourceEvidence=evidence;
    }
    public long FrameNumber{get;}
    public string ContextOwnerIdentity{get;}
    public string SourceSha256=>allocation.Source.Sha256;
    public string OwnedTextureIdentity{get;}
    public string TextureSourceIdentity{get;}
    public string CallerTextureSourceEvidence{get;}
    public bool SourceMetadataPointerWasNull{get;}
    public int? Metadata16ValueByteOffset{get;}
    public bool NativeReaderReturnedTrue=>Metadata16ValueByteOffset is not null;
    public int TonemapType{get;}
    public int ColorGamut{get;}
    public ReadOnlyMemory<byte> NeutralTonemapParams0=>neutral0.ToArray();
    public ReadOnlyMemory<byte> NeutralTonemapParams1=>neutral1.ToArray();
    public string SourceEvidence{get;}
    public bool EstablishesGpuCompletion=>false;
    internal static MapPostEffect3ControlledToneMetadata CaptureValidated(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3ApplyView view,string textureIdentity,string textureSourceIdentity,
        string callerEvidence,bool nullPointer,int? offset,int type,int gamut,
        byte[] p0,byte[] p1,string evidence)
        =>new(state,allocation,view,textureIdentity,textureSourceIdentity,callerEvidence,nullPointer,
            offset,type,gamut,p0,p1,evidence);
    public void ValidateCurrent(MapPostEffect3ParameterState actualState,
        MapPostEffect3AllocationPlan actualAllocation)
    {
        if(!ReferenceEquals(state,actualState)||!ReferenceEquals(allocation,actualAllocation)||
            FrameNumber!=actualState.FrameNumber||ContextOwnerIdentity!=actualState.OwnerIdentity||
            ContextOwnerIdentity!=actualAllocation.ContextOwnerIdentity||SourceSha256!=actualState.SourceSha256)
            throw new InvalidDataException("Controlled Tone metadata is not from this owned scalar frame and source allocation.");
        MapPostEffect3ParameterState.ValidateSource(actualAllocation.Source);
    }
}

/// <summary>Owned tone metadata rules. The caller supplies the actual
/// selected texture snapshot, rather than inferring metadata absence from G1T
/// readers or an unrelated resource. Does not select HDR textures or execute.</summary>
public sealed class MapPostEffect3ControlledToneSource
{
    private readonly int defaultType,defaultGamut;
    private readonly byte[] default0,default1;
    internal MapPostEffect3ControlledToneSource(string locale,int type,int gamut,
        byte[] p0,byte[] p1,string evidence)
    {Locale=locale;defaultType=type;defaultGamut=gamut;default0=p0;default1=p1;SourceEvidence=evidence;}
    public string Locale{get;}
    public string SourceEvidence{get;}
    public static MapPostEffect3ControlledToneSource Load(GameResourceRepository repository,string locale)
        =>RenderSamplerSourceAdapters.ReadControlledToneSource(repository,locale);

    public MapPostEffect3ControlledToneMetadata CaptureOwnedTextureMetadata(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3ApplyView actualView,string actualOwnedTextureIdentity,
        string actualTextureSourceIdentity,ReadOnlyMemory<byte>? actualNativeTexture28MetadataChain,
        string actualTextureSourceEvidence)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(actualView);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwnedTextureIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualTextureSourceIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualTextureSourceEvidence);
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(state.SourceSha256!=allocation.Source.Sha256||state.OwnerIdentity!=allocation.ContextOwnerIdentity||
            state.FrameNumber!=actualView.FrameNumber||state.OwnerIdentity!=actualView.OwnerIdentity)
            throw new InvalidDataException("Controlled Tone selected texture and scalar state need the same source owner/frame.");
        // This internal owner check also requires the exact module object, not
        // just matching hashes, and rejects binding retained state elsewhere.
        state.BindOwnedAllocation(allocation);
        byte[]? chain=actualNativeTexture28MetadataChain?.ToArray();int? found=null;
        if(chain is not null)
        {
            int cursor=0;
            while(true)
            {
                if(chain.Length-cursor<4)throw new InvalidDataException("Native texture metadata chain has no complete final header.");
                ushort id=BinaryPrimitives.ReadUInt16LittleEndian(chain.AsSpan(cursor));
                int bytes=chain[cursor+2]*4,next=checked(cursor+4+bytes);
                if(next>chain.Length)throw new InvalidDataException("Native texture metadata record extends outside its actual snapshot.");
                if(id==16&&found is null)
                {
                    if(bytes<40)throw new InvalidDataException("Native metadata16 does not contain the forty bytes read by");
                    found=cursor+4;
                }
                if(chain[cursor+3]!=0)
                {
                    if(next!=chain.Length)throw new InvalidDataException("Native metadata snapshot contains bytes after its last record.");
                    break;
                }
                cursor=next;
            }
        }
        int type=defaultType,gamut=defaultGamut;byte[] p0=default0.ToArray(),p1=default1.ToArray();
        if(found is int o)
        {
            type=ConvertNativeFloatTypeWord(BinaryPrimitives.ReadUInt32LittleEndian(chain!.AsSpan(o)));
            gamut=ConvertNativeFloatTypeWord(BinaryPrimitives.ReadUInt32LittleEndian(chain.AsSpan(o+4)));
            p0=chain.AsSpan(o+8,16).ToArray();p1=chain.AsSpan(o+24,16).ToArray();
        }
        return MapPostEffect3ControlledToneMetadata.CaptureValidated(state,allocation,actualView,actualOwnedTextureIdentity,actualTextureSourceIdentity,
            actualTextureSourceEvidence,chain is null,found,type,gamut,p0,p1,SourceEvidence+
            (found is null?";actual-texture28-ID16-absent-native-fallback":";actual-texture28-first-ID16-present")+
            ";caller-selected-owned-texture-snapshot;no-texture-selection-or-GPU-completion");
    }

    /// <summary>CVTTSS2SI RAX followed by MOV [out],EAX. Invalid64 conversion
    /// gives 8000000000000000 whose low32 is zero. Finite in-range values may
    /// wrap low32; this is deliberately not CVTTSS2SI EAX or a clamp.</summary>
    public static int ConvertNativeFloatTypeWord(uint rawFloat)
    {
        double value=BitConverter.UInt32BitsToSingle(rawFloat);
        if(double.IsNaN(value)||value>=9223372036854775808d||value< -9223372036854775808d)return 0;
        return unchecked((int)(long)Math.Truncate(value));
    }

    public void Apply(MapPostEffect3ParameterState actualState,
        MapPostEffect3AllocationPlan actualAllocation,MapPostEffect3ControlledToneMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);metadata.ValidateCurrent(actualState,actualAllocation);
        if(!metadata.SourceEvidence.StartsWith(SourceEvidence,StringComparison.Ordinal))
            throw new InvalidDataException("Controlled Tone reader and writer use different tone rules.");
        (string name,uint hash,uint type,int bytes)[] targets=
        [("TonemapType",0x87D299B0,6,4),("ColorGamut",0x0F58CE72,6,4),
         ("NeutralTonemapParams0",0xB6001D47,4,16),("NeutralTonemapParams1",0x3C85D7E6,4,16)];
        foreach(var t in targets)
        {
            int index=actualState.FindScalarIndex(t.hash)??throw new InvalidDataException("Controlled Tone primary declaration absent: "+t.name);
            var d=actualState.Declarations[index];
            if(d.NativeValueType!=t.type||d.ElementCount!=1||d.ValueByteCount!=t.bytes)
                throw new InvalidDataException("Controlled Tone source scalar layout differs: "+t.name);
        }
        byte[] typeBytes=new byte[4],gamutBytes=new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(typeBytes,metadata.TonemapType);
        BinaryPrimitives.WriteInt32LittleEndian(gamutBytes,metadata.ColorGamut);
        ReadOnlyMemory<byte>[] values=[typeBytes,gamutBytes,metadata.NeutralTonemapParams0,metadata.NeutralTonemapParams1];
        for(int i=0;i<targets.Length;i++)
            if(!actualState.SetTypedByHash(targets[i].hash,targets[i].type,1,values[i].Span,
                metadata.SourceEvidence+"-four-primary-current-controlled-bank-writes"))
                throw new InvalidDataException("Controlled Tone source scalar target changed.");
    }
}

public static partial class RenderSamplerSourceAdapters
{
    internal static MapPostEffect3ControlledToneSource ReadControlledToneSource(
        GameResourceRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");
        // Defaults are rules of the owned tone implementation. ID16, when
        // present on the selected texture, still replaces all four values.
        const int defaultTonemapType=2,defaultColorGamut=1;
        byte[] Pack(float x,float y,float z,float w)
        {
            byte[] bytes=new byte[16];
            BinaryPrimitives.WriteSingleLittleEndian(bytes,x);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4),y);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(8),z);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12),w);
            return bytes;
        }
        return new(locale,defaultTonemapType,defaultColorGamut,
            Pack(.2f,.37f,.24f,.2f),Pack(.02f,.3f,5.3f,10),
            RenderRuleIdentity.For("post-effect3-controlled-tone",locale)+
            ";selected-texture-ID16-or-neutral-defaults;float-type-truncate64-low32");
    }
}
