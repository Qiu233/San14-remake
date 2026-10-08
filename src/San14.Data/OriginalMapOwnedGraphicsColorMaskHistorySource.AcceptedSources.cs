namespace San14.Data;

/// <summary>Canonical declared-hook source semantics captured from this
/// owner's private immutable byte copies. It is neither a live mutable asset
/// validation waiver nor a GPU receipt. Exact pending prefix and source
/// revision remain current; edited live assets reject at capture/commit/new
/// attempt boundaries, and explicit unknown writers invalidate immediately.</summary>
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
    public string SourceProvenance=>owner.SourceProvenance+";accepted-private-immutable-declared-source-snapshot/revision="+
        SourceRevision+";frame="+FrameNumber+";live-source-validation-at-capture/commit/new-attempt;not-GPU-completion";
    public void RequireCurrent()=>owner.RequireAcceptedSourceSnapshot(this);
    /// <summary>Use at a complete source acceptance boundary, including a
    /// separately closed Functor1 route. This retains the live mutable asset
    /// edit rejection while draw-time proof uses the private captured bytes.</summary>
    public void RevalidateMutableSourceBoundary()
    {RequireCurrent();owner.RevalidateAcceptedMutableSourceBoundary(this);}
}

public sealed partial class OriginalMapOwnedGraphicsColorMaskHistorySource
{
    private OriginalMapOwnedGraphicsMaskAcceptedSourceSnapshot? acceptedSources;
    private long acceptedSourceRevision=1;
    private long fullStaticSourceValidationCount;
    /// <summary>Diagnostic number of complete live-byte comparison passes.
    /// ReadFresh also verifies every canonical hash/configuration once.</summary>
    public long FullStaticSourceValidationCount=>Interlocked.Read(ref fullStaticSourceValidationCount);

    /// <summary>Explicit acceptance boundary. The private source copies were
    /// hash/configuration verified by ReadFresh and are never exposed. Check
    /// the mutable original once before accepting this exact active prefix.
    /// The Game owner must retain all actual dynamic World/stock checks.</summary>
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
        Interlocked.Increment(ref fullStaticSourceValidationCount);
        foreach(var asset in assets)
            if(!asset.Source.Bytes.AsSpan().SequenceEqual(asset.Bytes))
                throw new InvalidDataException("Owned mask history contains an edited live source at its capture/commit/attempt boundary.");
    }
    private void ReleaseAcceptedSourceSnapshot()
    {acceptedSources=null;acceptedSourceRevision=checked(acceptedSourceRevision+1);}
}
