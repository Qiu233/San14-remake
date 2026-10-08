namespace San14.Data;

/// <summary>Immutable fields of the selected Common overlay consumed by
/// controlled Apply. Unread fields of the native68B record remain unproved.</summary>
public sealed class OriginalMapPostEffect3CommonOverlayPacket
{
    private readonly OriginalMapPostEffect3OwnedCommonOverlay owner;
    internal OriginalMapPostEffect3CommonOverlayPacket(OriginalMapPostEffect3OwnedCommonOverlay owner,long frame)
    {
        this.owner=owner;FrameNumber=frame;Revision=owner.Revision;Selected=owner.Selected;
        ConsumedLowBits=owner.ConsumedLowBits;ConsumedHighBits=owner.ConsumedHighBits;
        SelectedViewOwnerIdentity=owner.SelectedViewOwnerIdentity;
        SourceEvidence=owner.SourceEvidence;
    }
    public long FrameNumber{get;}
    public long Revision{get;}
    public bool Selected{get;}
    public uint ConsumedLowBits{get;}
    public uint ConsumedHighBits{get;}
    public string SelectedViewOwnerIdentity{get;}
    public string SourceEvidence{get;}
    public bool ProvesCompleteNative68ByteRecord=>false;
    public void ValidateCurrent(OriginalMapPostEffect3OwnedCommonOverlay actualOwner,long actualFrame)
    {
        if(!ReferenceEquals(owner,actualOwner)||actualFrame!=FrameNumber||
            actualOwner.CurrentFrame!=actualFrame||actualOwner.Revision!=Revision||
            actualOwner.Selected!=Selected||actualOwner.ConsumedLowBits!=ConsumedLowBits||
            actualOwner.ConsumedHighBits!=ConsumedHighBits)
            throw new InvalidDataException("Common overlay packet differs from its actual owned context/frame/revision.");
    }
    /// <summary>D1E410's controlledFlags8 writes. The nonnull overlay branch
    /// does not write these four other consumed Aux offsets.</summary>
    public OriginalMapPostEffect3ControlledAuxFields ApplyControlledFields(
        OriginalMapPostEffect3ControlledAuxFields actualSourceAux)
    {
        ArgumentNullException.ThrowIfNull(actualSourceAux);ValidateCurrent(owner,FrameNumber);
        actualSourceAux.ValidateFrame(FrameNumber);
        if(actualSourceAux.SelectedViewOwnerIdentity!=SelectedViewOwnerIdentity)
            throw new InvalidDataException("Common overlay and source Aux belong to different selected Views.");
        uint flags=actualSourceAux.Flags8;
        if(Selected)
        {
            foreach(var pair in LowBindings)flags=WriteBit(flags,pair.Target,ConsumedLowBits,pair.Source);
            foreach(var pair in HighBindings)flags=WriteBit(flags,pair.Target,ConsumedHighBits,pair.Source);
        }
        return new(FrameNumber,SelectedViewOwnerIdentity,flags,actualSourceAux.Mode3C0,
            actualSourceAux.Fxaa3A8,actualSourceAux.SeparateHdrMode440,actualSourceAux.BloomColorFix448,
            actualSourceAux.CustomPixelShader108Absent,
            actualSourceAux.SourceEvidence+";"+SourceEvidence+
            (Selected?";D1E8D3..D1EA1C-selected-overlay-Flags8-writers":";D1E8CD-null-overlay-no-rewrite"));
    }
    private static uint WriteBit(uint destination,int at,uint source,int from)
        =>(destination&~(1u<<at))|(((source>>from)&1u)<<at);
    private static readonly (int Source,int Target)[] LowBindings=
        [(0,0),(1,1),(2,11),(3,2),(8,3),(9,4),(10,10),(12,5),(16,17),(17,9)];
    private static readonly (int Source,int Target)[] HighBindings=[(0,25),(1,26),(5,12),(6,13)];
}

/// <summary>Independent remake Common state with the exact known Host callback
/// producer. It is not an original observation or a complete native overlay.</summary>
public sealed class OriginalMapPostEffect3OwnedCommonOverlay
{
    private readonly string baseEvidence;
    private string flagsWriterEvidence="Common-constructor-consumed-fields";
    public const uint ControlledLowReadMask=0x0003170F;
    public const uint ControlledHighReadMask=0x00000063;
    internal OriginalMapPostEffect3OwnedCommonOverlay(string common,string view,string host,string evidence)
    {
        CommonContextOwnerIdentity=common;SelectedViewOwnerIdentity=view;HostOwnerIdentity=host;
        baseEvidence=evidence+";owned-Common="+common+";owned-Host="+host;
        SourceEvidence=baseEvidence+
            ";D01739-3324=0;D01794-34B0=01FFFFFF;only-consumed-fields-before-callback";
    }
    public string CommonContextOwnerIdentity{get;}
    public string SelectedViewOwnerIdentity{get;}
    public string HostOwnerIdentity{get;}
    public long CurrentFrame{get;private set;}
    public long Revision{get;private set;}
    public bool Selected{get;private set;}
    public uint ConsumedLowBits{get;private set;}=ControlledLowReadMask;
    public uint ConsumedHighBits{get;private set;}
    public string SourceEvidence{get;private set;}
    public OriginalMapPostEffect3CommonOverlayPacket CaptureBeforeCallback(long frame)
    {AcceptFrame(frame);return new(this,frame);}
    /// <summary>Original153EA0 selects the existing packet or its local native
    /// defaults. Its unknown configuration/device/rate stores affect no bit
    /// read by the controlledFlags8 mapper; relevant writer is highword OR20.</summary>
    public OriginalMapPostEffect3CommonOverlayPacket PublishKnownHostCallback(long frame)
    {
        AcceptFrame(frame);
        if(!Selected)
        {
            ConsumedLowBits=ControlledLowReadMask;ConsumedHighBits=0;
            flagsWriterEvidence="153EE6-native-local-default-consumed-fields";
        }
        ConsumedHighBits|=0x20;Selected=true;Revision=checked(Revision+1);
        SourceEvidence=baseEvidence+";"+flagsWriterEvidence+
            ";153F1B-existing-overlay-or-native-default;153FC4-highword-OR20;"+
            "D6D070-publish;3324.bit2=1;host-lowword19/22-and-rate-not-consumed;revision="+Revision;
        return new(this,frame);
    }
    /// <summary>Explicit owned configuration write. Both words are actual
    /// caller inputs; this never turns an original observation into a default.</summary>
    public OriginalMapPostEffect3CommonOverlayPacket PublishOwnedConsumedFlags(long frame,
        uint actualLowWord,uint actualHighWord,string actualOwnedWriterEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwnedWriterEvidence);AcceptFrame(frame);
        ConsumedLowBits=actualLowWord&ControlledLowReadMask;ConsumedHighBits=actualHighWord&ControlledHighReadMask;
        Selected=true;Revision=checked(Revision+1);
        flagsWriterEvidence=actualOwnedWriterEvidence;
        SourceEvidence=baseEvidence+";explicit-owned-overlay-fields;"+actualOwnedWriterEvidence+";revision="+Revision;
        return new(this,frame);
    }
    public void ClearOwnedSelection(long frame,string actualOwnedWriterEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwnedWriterEvidence);AcceptFrame(frame);
        if(Selected){Selected=false;Revision=checked(Revision+1);}
        SourceEvidence=baseEvidence+";D1A3B4-clear-3324.bit2;"+actualOwnedWriterEvidence+";revision="+Revision;
    }
    private void AcceptFrame(long frame)
    {
        if(frame<=0||frame<CurrentFrame)throw new InvalidDataException("Common overlay needs a current positive owned frame.");
        CurrentFrame=frame;
    }
}

public sealed class LoadedOriginalMapPostEffect3CommonOverlaySource
{
    internal LoadedOriginalMapPostEffect3CommonOverlaySource(LoadedOriginalDeferredSamplerNativeSource native,string evidence)=>(Native,SourceEvidence)=(native,evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string Locale=>Native.Locale;
    public string SourceEvidence{get;}
    public OriginalMapPostEffect3OwnedCommonOverlay CreateOwnedKnownHostCallback(
        string actualOwnedCommonContext,string actualSelectedView,string actualOwnedHost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwnedCommonContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualSelectedView);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwnedHost);
        return new(actualOwnedCommonContext,actualSelectedView,actualOwnedHost,SourceEvidence);
    }
}

public static class OriginalMapPostEffect3CommonOverlaySource
{
    public static LoadedOriginalMapPostEffect3CommonOverlaySource Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadPostEffect3CommonOverlaySource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3CommonOverlaySource ReadPostEffect3CommonOverlaySource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Common overlay executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"Common overlay decoded text");
        foreach(var(at,length,hash)in new(int,int,string)[]{
            (0x153EA0,336,"F2F21296DE70A33399ACC43BE7AD4EAE2DFA6FC7016119A3C7E989B9F8C7ED3D"),
            (0xD01794,107,"1DA03F40ECBFA1EC0F09D6C4DFD2C98E5C396BBA3912F069D26C43FAB72C6DB9"),
            (0xD5BAF0,28,"627EBEC618D96D0B74A613F9F71D0DDCE981357AFE67A035FCBBC9019E42774B"),
            (0xD6D070,60,"28E8BB5365191AAEDBDA508DCA9DA03B8F921F5C50FED76927B8ED9DF92390CB"),
            (0xD1E8D3,333,"2CE64B436FA983E100B6B1F0EA87B0EEEE4A474D6CC5D401B3DD9F3EBBF2C147")})
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3CommonOverlaySource.cs",at,length,hash);
            VerifyHash(text.AsSpan(actual.Rva-start,length),actual.Sha,"Common overlay producer/consumer window");
        }
        return new(native,native.ResourceUri+"@"+native.ExecutableSha256+
            ";TC:Host-VT1388718/v30=153EA0;registered-app-owner->CommonSettingF0;"+
            "secondary-CommonC+8/VT16C75E0;v10=D5BAF0;v8=D6D070;"+
            "153EA0-owned-known-Host-callback;controlled-read-masks3170F/63;"+
            "D1E8D3..D1EA1C-flags8-only;not-complete68B/not-original-observation/no-GPU-claim");
    }
}
