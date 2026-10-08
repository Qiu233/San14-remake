using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Snapshot of the actual selected owned texture's native +28 metadata
/// chain. Null is an explicit null-pointer observation, not a missing API value.
/// Presence is derived from ID16, never supplied as a boolean. No GPU claim.</summary>
public sealed class OriginalMapPostEffect3ControlledToneMetadata
{
    private readonly OriginalMapPostEffect3ParameterState state;
    private readonly OriginalMapPostEffect3AllocationPlan allocation;
    private readonly byte[] neutral0,neutral1;
    private OriginalMapPostEffect3ControlledToneMetadata(OriginalMapPostEffect3ParameterState state,
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3ApplyView view,
        string textureIdentity,string textureSourceIdentity,string callerEvidence,bool nullPointer,
        byte[]? chain,int? offset,int type,int gamut,byte[] p0,byte[] p1,string evidence)
    {
        this.state=state;this.allocation=allocation;FrameNumber=view.FrameNumber;
        ContextOwnerIdentity=view.OwnerIdentity;OwnedTextureIdentity=textureIdentity;
        TextureSourceIdentity=textureSourceIdentity;CallerTextureSourceEvidence=callerEvidence;
        SourceMetadataPointerWasNull=nullPointer;Metadata16ValueByteOffset=offset;
        MetadataChainSha256=chain is null?null:Convert.ToHexString(SHA256.HashData(chain));
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
    public string? MetadataChainSha256{get;}
    public int TonemapType{get;}
    public int ColorGamut{get;}
    public ReadOnlyMemory<byte> NeutralTonemapParams0=>neutral0.ToArray();
    public ReadOnlyMemory<byte> NeutralTonemapParams1=>neutral1.ToArray();
    public string SourceEvidence{get;}
    public bool EstablishesGpuCompletion=>false;
    internal static OriginalMapPostEffect3ControlledToneMetadata CaptureValidated(
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3ApplyView view,string textureIdentity,string textureSourceIdentity,
        string callerEvidence,bool nullPointer,byte[]? chain,int? offset,int type,int gamut,
        byte[] p0,byte[] p1,string evidence)
        =>new(state,allocation,view,textureIdentity,textureSourceIdentity,callerEvidence,nullPointer,
            chain,offset,type,gamut,p0,p1,evidence);
    public void ValidateCurrent(OriginalMapPostEffect3ParameterState actualState,
        OriginalMapPostEffect3AllocationPlan actualAllocation)
    {
        if(!ReferenceEquals(state,actualState)||!ReferenceEquals(allocation,actualAllocation)||
            FrameNumber!=actualState.FrameNumber||ContextOwnerIdentity!=actualState.OwnerIdentity||
            ContextOwnerIdentity!=actualAllocation.ContextOwnerIdentity||SourceSha256!=actualState.SourceSha256)
            throw new InvalidDataException("Controlled Tone metadata is not from this owned scalar frame and source allocation.");
        OriginalMapPostEffect3ParameterState.ValidateSource(actualAllocation.Source);
    }
}

/// <summary>Pinned 1147F60 / 96C650 / E25A00. The caller supplies the actual
/// selected texture snapshot, rather than inferring metadata absence from G1T
/// readers or an unrelated resource. Does not select HDR textures or execute.</summary>
public sealed class OriginalMapPostEffect3ControlledToneSource
{
    private readonly int defaultType,defaultGamut;
    private readonly byte[] default0,default1;
    internal OriginalMapPostEffect3ControlledToneSource(string locale,int type,int gamut,
        byte[] p0,byte[] p1,string evidence)
    {Locale=locale;defaultType=type;defaultGamut=gamut;default0=p0;default1=p1;SourceEvidence=evidence;}
    public string Locale{get;}
    public string SourceEvidence{get;}
    public static OriginalMapPostEffect3ControlledToneSource Load(OriginalGameRepository repository,string locale)
        =>OriginalDeferredSamplerSources.ReadControlledToneSource(repository,locale);

    public OriginalMapPostEffect3ControlledToneMetadata CaptureOwnedTextureMetadata(
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3ApplyView actualView,string actualOwnedTextureIdentity,
        string actualTextureSourceIdentity,ReadOnlyMemory<byte>? actualNativeTexture28MetadataChain,
        string actualTextureSourceEvidence)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(actualView);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwnedTextureIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualTextureSourceIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualTextureSourceEvidence);
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
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
                    if(bytes<40)throw new InvalidDataException("Native metadata16 does not contain the forty bytes read by 1147F60.");
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
        return OriginalMapPostEffect3ControlledToneMetadata.CaptureValidated(state,allocation,actualView,actualOwnedTextureIdentity,actualTextureSourceIdentity,
            actualTextureSourceEvidence,chain is null,chain,found,type,gamut,p0,p1,SourceEvidence+
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

    public void Apply(OriginalMapPostEffect3ParameterState actualState,
        OriginalMapPostEffect3AllocationPlan actualAllocation,OriginalMapPostEffect3ControlledToneMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);metadata.ValidateCurrent(actualState,actualAllocation);
        if(!metadata.SourceEvidence.StartsWith(SourceEvidence,StringComparison.Ordinal))
            throw new InvalidDataException("Controlled Tone reader and writer use different pinned source profiles.");
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
                metadata.SourceEvidence+";E25A00-four-primary-current-controlled-bank-writes"))
                throw new InvalidDataException("Controlled Tone source scalar target changed.");
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static OriginalMapPostEffect3ControlledToneSource ReadControlledToneSource(
        OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);var profile=Profile(locale);
        var executable=OriginalNativeCameraTangentSource.Load(repository,locale);
        byte[] exe=File.ReadAllBytes(executable.PhysicalPath);VerifyHash(exe,profile.ExeHash,"Controlled Tone executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int textRva);
        VerifyHash(code,profile.TextHash,"Controlled Tone decoded text");bool tc=locale=="TC";
        int reader=tc?0x1147F60:0x1148FE0,lookup=tc?0x96C650:0x96D7E0,writer=tc?0xE25A00:0xE26980;
        var raw=code.AsSpan(reader-textRva,0xF1);
        VerifyHash(raw,tc?"3B3D0F8F516120AFA0576FC5E631BDD8114F9C4A80FE505C93794CEED08A17AC":
            "731BC3F13D9BA0BB67A6E29CFB53DFEF42E2190FF882D3D457746A3FAAF4D7A0","Controlled Tone complete reader");
        VerifyHash(code.AsSpan(lookup-textRva,0x36),"C67B21A2F7BEDD892915F5B540DF49D079E33460C2E6A86584A0732F85BB1A64","Controlled Tone complete metadata lookup");
        VerifyHash(code.AsSpan(writer-textRva,0x9A),tc?"8FB72F0371607D1EA80154CB961D88A5FD8A25D97E5206698A2D855ABAD8FE45":
            "092423E64F68F5A1A35BEA466F8B78ABEBE898C826151EEE3443807E08D81745","Controlled Tone complete four writes");
        byte[] p0=new byte[16],p1=new byte[16];int[] o0=[0x3F,0x46,0x4D,0x54],o1=[0x5F,0x66,0x6D,0x74];
        for(int i=0;i<4;i++){raw.Slice(o0[i],4).CopyTo(p0.AsSpan(i*4));raw.Slice(o1[i],4).CopyTo(p1.AsSpan(i*4));}
        return new(locale,BinaryPrimitives.ReadInt32LittleEndian(raw[0x33..]),
            BinaryPrimitives.ReadInt32LittleEndian(raw[0x39..]),p0,p1,executable.ResourceUri+
            ";paired-complete-TC1147F60/SC1148FE0;metadataID16-96C650;E25A00-four-writes;CVTTSS2SI-RAX-low32");
    }
}
