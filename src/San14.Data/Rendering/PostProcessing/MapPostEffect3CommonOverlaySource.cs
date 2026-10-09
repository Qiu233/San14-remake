namespace San14.Data;

/// <summary>Immutable fields of the selected Common overlay consumed by
/// controlled Apply. Unread fields of the native68B record remain unproved.</summary>
public sealed class MapPostEffect3CommonOverlayPacket
{
    private readonly MapPostEffect3OwnedCommonOverlay owner;
    internal MapPostEffect3CommonOverlayPacket(MapPostEffect3OwnedCommonOverlay owner,long frame)
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
    public void ValidateCurrent(MapPostEffect3OwnedCommonOverlay actualOwner,long actualFrame)
    {
        if(!ReferenceEquals(owner,actualOwner)||actualFrame!=FrameNumber||
            actualOwner.CurrentFrame!=actualFrame||actualOwner.Revision!=Revision||
            actualOwner.Selected!=Selected||actualOwner.ConsumedLowBits!=ConsumedLowBits||
            actualOwner.ConsumedHighBits!=ConsumedHighBits)
            throw new InvalidDataException("Common overlay packet differs from its actual owned context/frame/revision.");
    }
    /// <summary>'s controlledFlags8 writes. The nonnull overlay branch
    /// does not write these four other consumed Aux offsets.</summary>
    public MapPostEffect3ControlledAuxFields ApplyControlledFields(
        MapPostEffect3ControlledAuxFields actualSourceAux)
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
            (Selected?"-selected-overlay-Flags8-writers":"-null-overlay-no-rewrite"));
    }
    private static uint WriteBit(uint destination,int at,uint source,int from)
        =>(destination&~(1u<<at))|(((source>>from)&1u)<<at);
    private static readonly (int Source,int Target)[] LowBindings=
        [(0,0),(1,1),(2,11),(3,2),(8,3),(9,4),(10,10),(12,5),(16,17),(17,9)];
    private static readonly (int Source,int Target)[] HighBindings=[(0,25),(1,26),(5,12),(6,13)];
}

/// <summary>Independent remake Common state with the exact known Host callback
/// producer. It is not an original observation or a complete native overlay.</summary>
public sealed class MapPostEffect3OwnedCommonOverlay
{
    private readonly string baseEvidence;
    private string flagsWriterEvidence="Common-constructor-consumed-fields";
    public const uint ControlledLowReadMask=0x0003170F;
    public const uint ControlledHighReadMask=0x00000063;
    internal MapPostEffect3OwnedCommonOverlay(string common,string view,string host,string evidence)
    {
        CommonContextOwnerIdentity=common;SelectedViewOwnerIdentity=view;HostOwnerIdentity=host;
        baseEvidence=evidence+";owned-Common="+common+";owned-Host="+host;
        SourceEvidence=baseEvidence+
            ";3324=0;34B0=01FFFFFF;only-consumed-fields-before-callback";
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
    public MapPostEffect3CommonOverlayPacket CaptureBeforeCallback(long frame)
    {AcceptFrame(frame);return new(this,frame);}
    /// <summary>Original153EA0 selects the existing packet or its local native
    /// defaults. Its unknown configuration/device/rate stores affect no bit
    /// read by the controlledFlags8 mapper; relevant writer is highword OR20.</summary>
    public MapPostEffect3CommonOverlayPacket PublishKnownHostCallback(long frame)
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
            "-publish;3324.bit2=1;host-lowword19/22-and-rate-not-consumed;revision="+Revision;
        return new(this,frame);
    }
    /// <summary>Explicit owned configuration write. Both words are actual
    /// caller inputs; this never turns an original observation into a default.</summary>
    public MapPostEffect3CommonOverlayPacket PublishOwnedConsumedFlags(long frame,
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
        SourceEvidence=baseEvidence+"-clear-3324.bit2"+actualOwnedWriterEvidence+";revision="+Revision;
    }
    private void AcceptFrame(long frame)
    {
        if(frame<=0||frame<CurrentFrame)throw new InvalidDataException("Common overlay needs a current positive owned frame.");
        CurrentFrame=frame;
    }
}

public sealed class LoadedMapPostEffect3CommonOverlaySource
{
    internal LoadedMapPostEffect3CommonOverlaySource(LoadedDeferredSamplerNativeSource native,string evidence)=>(Native,SourceEvidence)=(native,evidence);
    public LoadedDeferredSamplerNativeSource Native{get;}
    public string Locale=>Native.Locale;
    public string SourceEvidence{get;}
    public MapPostEffect3OwnedCommonOverlay CreateOwnedKnownHostCallback(
        string actualOwnedCommonContext,string actualSelectedView,string actualOwnedHost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwnedCommonContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualSelectedView);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwnedHost);
        return new(actualOwnedCommonContext,actualSelectedView,actualOwnedHost,SourceEvidence);
    }
}

public static class MapPostEffect3CommonOverlaySource
{
    public static LoadedMapPostEffect3CommonOverlaySource Load(GameResourceRepository repository,string locale="TC")
        =>RenderSamplerSourceAdapters.ReadPostEffect3CommonOverlaySource(
            RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3CommonOverlaySource ReadPostEffect3CommonOverlaySource(
        LoadedDeferredSamplerNativeSource native)
    {

        return new(native,native.RenderRuleIdentity+
            "TC:Host-VT/v30=153EA0;registered-app-owner->CommonSettingF0"+
            "secondary-CommonC+8/VT;v10=;v8="+
            "153EA0-owned-known-Host-callback;controlled-read-masks3170F/63;"+
            "-flags8-only;not-complete68B/not-original-observation/no-GPU-claim");
    }
}
