namespace San14.Data;

/// <summary>Source context110 camera selection used by EC3360. The slot inputs
/// are actual ApplyCPU1A3 and caller record3F8; this helper never infers them from
/// a View name, archive Camera id or difficulty/game settings.</summary>
public sealed class OriginalMapPostEffect3ApplyFallbackCameraSelection
{
    private OriginalMapPostEffect3ApplyFallbackCameraSelection(long frame,string owner,uint slot,
        OriginalMapCameraConstructionPacket? camera,string evidence)
    {FrameNumber=frame;OwnerIdentity=owner;Slot=slot;Camera=camera;SourceEvidence=evidence;}
    public long FrameNumber{get;}
    public string OwnerIdentity{get;}
    public uint Slot{get;}
    public bool Context110CameraPresent=>Camera is not null;
    public OriginalMapCameraConstructionPacket? Camera{get;}
    public bool UsesNativeDefaultCamera=>Camera is null;
    public string SourceEvidence{get;}
    public static OriginalMapPostEffect3ApplyFallbackCameraSelection Select(
        long frame,string contextOwner,byte actualApply1A3,uint actualRecord3F8,
        IReadOnlyDictionary<uint,OriginalMapCameraConstructionPacket> actualContext110Cameras,string lookupEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contextOwner);ArgumentException.ThrowIfNullOrWhiteSpace(lookupEvidence);
        ArgumentNullException.ThrowIfNull(actualContext110Cameras);
        if(frame<=0)throw new InvalidDataException("Fallback Camera lookup requires an owned frame.");
        uint slot=unchecked((uint)(actualApply1A3&0x7F)+(actualRecord3F8&0x7FFFFFFF));
        OriginalMapCameraConstructionPacket? selected=null;
        if(slot<32&&actualContext110Cameras.TryGetValue(slot,out var candidate))
        {
            if(candidate is null||candidate.FrameNumber!=frame)
                throw new InvalidDataException("Fallback Camera context entry belongs to another frame.");
            selected=candidate.CopyAcceptedSnapshot();
        }
        return new(frame,contextOwner,slot,selected,"TC:EC33AF/EC33B5-record3F8-clearbit31;EC33BD/EC33CC-Apply1A3&7F;"+
            "EC33CF-uint32-add;EC340D-slot<32;EC3412-context110;EC3423-B992A0-present-copy;"+
            "EC33D1-AB5990-absent-default;"+lookupEvidence);
    }
}
