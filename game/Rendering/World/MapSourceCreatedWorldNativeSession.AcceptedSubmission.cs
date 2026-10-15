using San14.Data;

/// <summary>The submitted source generation retained for postprocessing.
/// Actual packet/roster ownership, Scene completion and session revision
/// establish currentness; no second native byte snapshot is maintained.</summary>
public sealed class MapPostEffect3WorldSubmittedImmutableSourceReceipt
{
    private readonly MapSourceCreatedWorldNativeSession owner;
    private readonly NativeMapWorldDeferredSubmittedFrame submitted;
    private readonly long revision;
    private readonly MapSourceCreatedNativeSessionPreparedFrame[] frames;
    private readonly SceneViewNativeMapWorldF103SourceOwner.CurrentFrame world;
    private readonly NativeDeferredPacket[] prepared;
    internal MapPostEffect3WorldSubmittedImmutableSourceReceipt(MapSourceCreatedWorldNativeSession owner,
        NativeMapWorldDeferredSubmittedFrame submitted,long revision,
        MapSourceCreatedNativeSessionPreparedFrame[] frames,
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame world,NativeDeferredPacket[] prepared)
        =>(this.owner,this.submitted,this.revision,this.frames,this.world,this.prepared)=
            (owner,submitted,revision,frames,world,prepared);

    public long FrameNumber=>submitted.Context.FrameNumber;
    public int SubmittedPassCount=>frames.Length;
    public string SourceEvidence=>submitted.SourceEvidence+";accepted-World-packet-roster;session-revision="+revision;
    public void RequireCurrent()=>owner.RequireAcceptedSubmittedWorld(submitted,revision,world,frames,prepared);
    public void RevalidateMutableSourceBoundary()=>RequireCurrent();
}

public sealed partial class MapSourceCreatedWorldNativeSession
{
    private long acceptedSubmittedWorldRevision;
    private void AdvanceAcceptedSubmittedWorldRevision()
    {
        Interlocked.Increment(ref acceptedSubmittedWorldRevision);
    }
    public MapPostEffect3WorldSubmittedImmutableSourceReceipt CaptureAcceptedSubmittedWorld(
        NativeMapWorldDeferredSubmittedFrame submission)
    {
        RequireSubmittedWorld(submission);
        return new(this,submission,Interlocked.Read(ref acceptedSubmittedWorldRevision),CurrentPasses.ToArray(),
            CurrentWorld!,preparedLatePackets!.ToArray());
    }
    internal void RequireAcceptedSubmittedWorld(NativeMapWorldDeferredSubmittedFrame submission,long revision,
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame expectedWorld,
        MapSourceCreatedNativeSessionPreparedFrame[] expectedFrames,NativeDeferredPacket[] expectedPackets)
    {
        lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
        var currentPackets=preparedLatePackets;
        var actualWorld=CurrentWorld;
        if(revision!=Interlocked.Read(ref acceptedSubmittedWorldRevision)||actualWorld is null||!ReferenceEquals(actualWorld,expectedWorld)||
            !ReferenceEquals(CurrentFrame,expectedFrames.FirstOrDefault())||CurrentPasses.Count!=expectedFrames.Length||
            currentPackets is null||currentPackets.Count!=expectedPackets.Length||expectedFrames.Length!=submission.SubmittedPassCount||
            !ReferenceEquals(submission.Program,Program)||actualWorld.World.Passes.Count!=expectedFrames.Length||
            !CurrentPasses.Zip(expectedFrames).All(p=>ReferenceEquals(p.First,p.Second))||
            !CurrentPasses.Zip(actualWorld.World.Passes).All(p=>ReferenceEquals(p.First.PreparedSourceFrame,p.Second.Frame))||
            !currentPackets.Zip(expectedPackets).All(p=>ReferenceEquals(p.First,p.Second))||
            expectedFrames.Any(f=>!ReferenceEquals(f.Context,submission.Context)))
            throw new InvalidDataException("Accepted submitted World was retired/replaced or has another current source roster/session revision.");
        submission.RequireOrderedPackets(Program,currentPackets);
    }
}
