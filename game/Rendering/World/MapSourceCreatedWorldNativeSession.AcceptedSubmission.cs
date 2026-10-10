using San14.Data;

/// <summary>Private copies of source Camera-applied CB results that the real
/// Scene already submitted. Current roster/Camera/native bytes and owning
/// session revision remain checked. Generic mutable session validation is
/// preserved; this receipt is confined to the captured submitted generation.</summary>
public sealed class MapPostEffect3WorldSubmittedImmutableSourceReceipt
{
    private readonly MapSourceCreatedWorldNativeSession owner;
    private readonly NativeMapWorldDeferredSubmittedFrame submitted;
    private readonly long revision;
    private readonly MapSourceCreatedNativeSessionPreparedFrame[] frames;
    private readonly SceneViewNativeMapWorldF103SourceOwner.CurrentFrame world;
    private readonly NativeDeferredPacket[] prepared;
    private readonly PacketSnapshot[] packetSnapshots;
    private readonly FrameSnapshot[] frameSnapshots;
    private readonly NativeDeferredPacket[] acceptedApplied;
    internal MapPostEffect3WorldSubmittedImmutableSourceReceipt(MapSourceCreatedWorldNativeSession owner,
        NativeMapWorldDeferredSubmittedFrame submitted,long revision,
        MapSourceCreatedNativeSessionPreparedFrame[] frames,
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame world,NativeDeferredPacket[] prepared,
        NativeDeferredPacket[] applied)
    {
        this.owner=owner;this.submitted=submitted;this.revision=revision;this.frames=frames;this.world=world;this.prepared=prepared;
        packetSnapshots=prepared.Select(p=>new PacketSnapshot(p)).ToArray();frameSnapshots=frames.Select(f=>new FrameSnapshot(f)).ToArray();
        acceptedApplied=applied.Select(p=>p with{Constants=new System.Collections.ObjectModel.ReadOnlyDictionary<uint,ReadOnlyMemory<byte>>(
            p.Constants.ToDictionary(v=>v.Key,v=>(ReadOnlyMemory<byte>)v.Value.ToArray())),Inputs=Array.AsReadOnly(p.Inputs.ToArray())}).ToArray();
    }
    public long FrameNumber=>submitted.Context.FrameNumber;
    public int SubmittedPassCount=>frames.Length;
    public string SourceEvidence=>submitted.SourceEvidence+";accepted-private-Camera-applied-CB/source-roster-snapshot;session-revision="+revision;
    public void RequireCurrent()=>owner.RequireAcceptedSubmittedWorld(submitted,revision,world,frames,prepared,
        ()=>{foreach(var packet in packetSnapshots)packet.RequireCurrent();foreach(var frame in frameSnapshots)frame.RequireCurrent();});
    public void RevalidateMutableSourceBoundary()
    {
        RequireCurrent();owner.RequireSubmittedWorld(submitted);
        submitted.RequireExactOrderedPackets(owner.Program,acceptedApplied);
    }
    private sealed class PacketSnapshot
    {
        private readonly NativeDeferredPacket packet;
        private readonly Dictionary<uint,byte[]> constants;
        private readonly DeferredDeviceBinding[] inputs;
        private readonly DeferredRasterState raster;
        private readonly DeferredTranslucencyViewportInputs? viewport;
        internal PacketSnapshot(NativeDeferredPacket packet)
        {this.packet=packet;constants=packet.Constants.ToDictionary(v=>v.Key,v=>v.Value.ToArray());inputs=packet.Inputs.ToArray();raster=packet.Raster;viewport=packet.TemporaryTranslucencyViewport;}
        internal void RequireCurrent()
        {
            if(packet.Constants.Count!=constants.Count||packet.Raster!=raster||packet.TemporaryTranslucencyViewport!=viewport||!packet.Inputs.SequenceEqual(inputs)||
                constants.Any(c=>!packet.Constants.TryGetValue(c.Key,out var actual)||!actual.Span.SequenceEqual(c.Value)))
                throw new InvalidDataException("Accepted submitted World changed its native CB template/resources/raster/viewport.");
        }
    }
    private sealed class FrameSnapshot
    {
        private readonly MapSourceCreatedNativeSessionPreparedFrame frame;
        private readonly byte[] camera,lightingCamera,light,feature,selectionFeature,variant,row,binding;
        internal FrameSnapshot(MapSourceCreatedNativeSessionPreparedFrame frame)
        {
            this.frame=frame;camera=frame.MainCamera.Prefix.ToArray();lightingCamera=frame.Lighting.PreparedCameraPrefix.ToArray();
            light=frame.Lighting.LocalLightData.ToArray();feature=frame.Feature.CurrentFeature48.ToArray();selectionFeature=frame.Selection.CurrentFeature48.ToArray();
            variant=frame.Selection.RequestedVariant96.ToArray();row=frame.Selection.Combination.RawRow.ToArray();
            binding=frame.Selection.BindingVariant.RawBytes.ToArray();
        }
        internal void RequireCurrent()
        {
            if(frame.Context.PreparedCamera!=frame.Lighting.SceneInputs.Camera||frame.MainCamera.FrameNumber!=frame.Context.FrameNumber||
                !frame.MainCamera.Prefix.Span.SequenceEqual(camera)||!frame.Lighting.PreparedCameraPrefix.Span.SequenceEqual(lightingCamera)||
                !frame.Lighting.LocalLightData.Span.SequenceEqual(light)||!frame.Feature.CurrentFeature48.Span.SequenceEqual(feature)||
                !frame.Selection.CurrentFeature48.Span.SequenceEqual(selectionFeature)||!frame.Selection.RequestedVariant96.Span.SequenceEqual(variant)||
                !frame.Selection.Combination.RawRow.AsSpan().SequenceEqual(row)||!frame.Selection.BindingVariant.RawBytes.AsSpan().SequenceEqual(binding))
                throw new InvalidDataException("Accepted submitted World changed its current native Camera/lighting/feature/variant packet.");
        }
    }
}

public sealed partial class MapSourceCreatedWorldNativeSession
{
    private long acceptedSubmittedWorldRevision;
    private void AdvanceAcceptedSubmittedWorldRevision()
    {
        acceptedLateSubmission=null;appliedLatePackets=null;
        Interlocked.Increment(ref acceptedSubmittedWorldRevision);
    }
    public MapPostEffect3WorldSubmittedImmutableSourceReceipt CaptureAcceptedSubmittedWorld(
        NativeMapWorldDeferredSubmittedFrame submission)
    {
        var applied=RequireSubmittedWorldAndReadAppliedPackets(submission);
        return new(this,submission,Interlocked.Read(ref acceptedSubmittedWorldRevision),CurrentPasses.ToArray(),
            CurrentWorld!,preparedLatePackets!.ToArray(),applied);
    }
    internal void RequireAcceptedSubmittedWorld(NativeMapWorldDeferredSubmittedFrame submission,long revision,
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame expectedWorld,
        MapSourceCreatedNativeSessionPreparedFrame[] expectedFrames,NativeDeferredPacket[] expectedPackets,Action validatePrivateNativePackets)
    {
        lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
        submission.RequireCurrent(submission.Context);
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
        validatePrivateNativePackets();
    }
}
