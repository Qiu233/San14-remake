namespace San14.Data;

/// <summary>The retained declared-hook scope for one actual frontend attempt.
/// The pending prefix and owner generation remain current; explicit unknown
/// writers and failed attempts invalidate it. This is not a GPU receipt.</summary>
public sealed class OriginalMapOwnedGraphicsMaskAcceptedSourceSnapshot
{
    private readonly OriginalMapOwnedGraphicsColorMaskHistorySource owner;
    internal OriginalMapOwnedGraphicsMaskAcceptedSourceSnapshot(OriginalMapOwnedGraphicsColorMaskHistorySource owner,
        OriginalMapOwnedGraphicsMaskPrefix prefix,long revision,int assets,long bytes)
        =>(this.owner,Prefix,SourceRevision,AssetCount,CapturedByteCount)=(owner,prefix,revision,assets,bytes);
    internal OriginalMapOwnedGraphicsMaskPrefix Prefix{get;}
    public long SourceRevision{get;}
    public long FrameNumber=>Prefix.FrameNumber;
    public string ViewIdentity=>Prefix.ViewIdentity;
    public string OwnedFrontendIdentity=>owner.OwnedFrontendIdentity;
    public int AssetCount{get;}
    public long CapturedByteCount{get;}
    public string SourceProvenance=>owner.SourceProvenance+";accepted-declared-source-scope/generation="+
        SourceRevision+";frame="+FrameNumber+";not-GPU-completion";
    public void RequireCurrent()=>owner.RequireAcceptedSourceSnapshot(this);
    /// <summary>Validate the live attempt and explicit failure/writer state.</summary>
    public void RevalidateMutableSourceBoundary()
    {RequireCurrent();owner.RevalidateAcceptedMutableSourceBoundary(this);}
}

public sealed partial class OriginalMapOwnedGraphicsColorMaskHistorySource
{
    private OriginalMapOwnedGraphicsMaskAcceptedSourceSnapshot? acceptedSources;
    private long acceptedSourceRevision=1;

    /// <summary>Accept the exact active prefix. Resource declarations were
    /// parsed when loading; only actual attempt lifetime is checked here.</summary>
    public OriginalMapOwnedGraphicsMaskAcceptedSourceSnapshot CaptureAcceptedImmutableSourceSnapshot(
        OriginalMapOwnedGraphicsMaskPrefix prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if(acceptedSources is{} existing)
        {
            existing.RequireCurrent();
            if(!ReferenceEquals(existing.Prefix,prefix))throw new InvalidDataException("Accepted mask source belongs to another actual prefix.");
            return existing;
        }
        RequirePrefix(prefix,true);
        return acceptedSources=new(this,prefix,acceptedSourceRevision,assets.Length,assets.Sum(a=>(long)a.Bytes.Length));
    }
    internal void RequireAcceptedSourceSnapshot(OriginalMapOwnedGraphicsMaskAcceptedSourceSnapshot snapshot)
    {
        if(!valid||!started||!ReferenceEquals(acceptedSources,snapshot)||snapshot.SourceRevision!=acceptedSourceRevision||
            !ReferenceEquals(pending,snapshot.Prefix)||!ReferenceEquals(snapshot.Prefix.Source,this)||
            snapshot.FrameNumber<=CompletedSourceFrameNumber||snapshot.ViewIdentity!=OwnedViewIdentity)
            throw new InvalidDataException("Accepted immutable mask source was failed, released or replaced by another prefix/frontend attempt.");
    }
    internal void RevalidateAcceptedMutableSourceBoundary(OriginalMapOwnedGraphicsMaskAcceptedSourceSnapshot snapshot)
    {RequireAcceptedSourceSnapshot(snapshot);RequireMutableSourceBoundary();}
    private void RequireMutableSourceBoundary()
    {
        if(!valid)throw new InvalidDataException("Owned mask history contains a failed attempt or an unknown writer/hook.");
    }
    private void ReleaseAcceptedSourceSnapshot()
    {acceptedSources=null;acceptedSourceRevision=checked(acceptedSourceRevision+1);}
}
