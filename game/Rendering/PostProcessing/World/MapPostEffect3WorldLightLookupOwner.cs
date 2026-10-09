using San14.Data;

/// <summary>Current Main0 lookup capability over this source-created World's
/// complete five Directional records and their privately retained payloads.
/// The owned prefix has only kind1; Field1BB8 is a separate publication, and
/// every other publisher is excluded by the current source-frame contract.
/// Single-bit distinct masks make its selected result invariant under equal
/// packed-key ordering. Ambiguous masks/extra owners require another producer;
/// neither the primary light nor Sky selection is substituted.</summary>
public sealed class MapPostEffect3WorldLightLookupOwner
{
    private readonly MapSourceCreatedWorldPlayableHost host;
    private readonly SceneViewNativeMapWorldF103SourceOwner sourceOwner;
    private readonly SceneViewNativeMapWorldF103SourceOwner.CurrentFrame world;
    private readonly NativeMapWorldDeferredSubmittedFrame submitted;
    private readonly MapPostEffect3OwnedDirectCaller caller;
    private readonly IReadOnlyList<MapPostEffect3LightLookupPrimitive> prefix;
    public NativeMapFrameContext Context{get;}
    public MapPostEffect3LightLookupResult Result{get;}
    public string SourceEvidence=>"main-light-lookup;actual-owned-complete-kind1-prefix;"+
        world.CountedQueue.QueueOwnerIdentity+";single-bit-unique-selection/order-invariant;"+submitted.SourceEvidence+";"+caller.SourceEvidence;

    private MapPostEffect3WorldLightLookupOwner(MapSourceCreatedWorldPlayableHost actualHost,
        SceneViewNativeMapWorldF103SourceOwner actualSource,NativeMapFrameContext context,
        MapPostEffect3OwnedDirectCaller actualCaller)
    {
        host=actualHost;sourceOwner=actualSource;Context=context;caller=actualCaller;
        world=actualSource.RequireCurrent(context);
        submitted=host.Scene.LastWorldDeferredSubmittedFrame ??
            throw new InvalidDataException("Current native World late submission is absent.");
        RequireCurrentScope();
        var records=world.CountedQueue.Records;
        if(records.Count!=5 || world.Queue.Count!=records.Count ||
            world.World.CompleteRecordPayloadCount!=records.Count ||
            records.Any(r=>r.Mask78==0 || (r.Mask78&(r.Mask78-1))!=0) ||
            records.Select(r=>r.Mask78).Distinct().Count()!=records.Count)
            throw new NotSupportedException("Owned lookup requires the actual complete five distinct single-bit Directional records.");
        var inputs=new List<MapPostEffect3LightLookupPrimitive>();
        for(int i=0;i<records.Count;i++)
        {
            var r=records[i];var item=world.Queue[i];
            if(r.CurrentQueueIndex!=i || item.CurrentItemIndex!=i || item.CurrentItemTagWord>>28!=1 ||
                item.CurrentItemPlus8AndWorldPQWord78Retained!=true || item.CurrentWorldPQOwnerIdentity!=r.RecordOwnerIdentity ||
                item.CurrentQueueOwnerIdentity!=world.CountedQueue.QueueOwnerIdentity ||
                item.CurrentWorldPQWord78!=r.Mask78 || item.FrameNumber!=context.FrameNumber ||
                item.ViewIdentity!=host.Session.Binding.ViewIdentity)
                throw new InvalidDataException("Owned D586 prefix is not every actual current packed kind1 record in its complete queue.");
            inputs.Add(new(item.CurrentItemTagWord,true,r.Mask78,world.World.CopyAcceptedRecordLighting(r)));
        }
        prefix=Array.AsReadOnly(inputs.ToArray());
        if(prefix.Count(p=>(p.RecordMask78&caller.LightLookupExclusionMask)==0)>1)
            throw new NotSupportedException("Current D586 selection depends on an unclosed equal-key native sort order.");
        // This owned adapter's complete published positive count is its actual
        // prefix length, not an observation of original View19D4/19D0 memory.
        Result=MapPostEffect3LightLookup.ReadSortedPrefix(prefix,prefix.Count,prefix.Count,caller.LightLookupExclusionMask);
    }

    public static MapPostEffect3WorldLightLookupOwner CaptureCurrent(
        MapSourceCreatedWorldPlayableHost actualHost,
        SceneViewNativeMapWorldF103SourceOwner actualSource,
        NativeMapFrameContext actualContext,MapPostEffect3OwnedDirectCaller actualCaller)
    {
        ArgumentNullException.ThrowIfNull(actualHost);ArgumentNullException.ThrowIfNull(actualSource);
        ArgumentNullException.ThrowIfNull(actualContext);ArgumentNullException.ThrowIfNull(actualCaller);
        return new(actualHost,actualSource,actualContext,actualCaller);
    }

    private void RequireCurrentScope()
    {
        submitted.RequireCurrent(Context);host.Session.RequireSubmittedWorld(submitted);
        caller.ValidateSameCamera(world.First.Lighting.CapturePreparedCameraFromSameAcceptedMain(world.First.Camera));
        if(!ReferenceEquals(sourceOwner.RequireCurrent(Context),world) || !ReferenceEquals(host.Session.CurrentWorld,world) ||
            !ReferenceEquals(submitted.Context,Context) || caller.FrameNumber!=Context.FrameNumber ||
            caller.ActualViewIndexArgument!=0 || caller.ContextOwnerIdentity!=world.First.Common.CommonTableOwnerIdentity ||
            !ReferenceEquals(world.World.View.CurrentWorldQueue,world.CountedQueue) ||
            world.Publication.OtherOrdinaryPublishersAbsent!=true || world.Publication.OtherCurrentOrdinaryPrimitiveCounts is not null ||
            world.Publication.CurrentViewPublication.OtherAdditionalOwners1B80_1BB0_1B98Absent!=true ||
            world.Publication.CurrentViewPublication.ShadowPrimitiveCount1BFC!=0 ||
            world.Publication.CurrentViewPublication.CollectionPrimitiveCounts2To5.Any(n=>n!=0))
            throw new NotSupportedException("D586 lookup lacks this exact owned Main0 World context and complete publisher scope.");
    }

    public void RequireCurrent()
    {
        RequireCurrentScope();
        for(int i=0;i<prefix.Count;i++)
            if(!prefix[i].Payload8!.LocalLightData.Span.SequenceEqual(
                world.World.CopyAcceptedRecordLighting(world.CountedQueue.Records[i]).LocalLightData.Span))
                throw new InvalidDataException("Accepted D586 record payload changed or has another current producer.");
    }

    public MapPostEffect3ApplyUncontrolledPresentLightInputs? CreatePresentLightInputs()
    {
        RequireCurrent();
        return Result.Payload8 is { } selected ?
            MapPostEffect3ApplyUncontrolledPresentLightInputs.CaptureSelectedLookup(selected,
                world.CountedQueue.QueueOwnerIdentity+"/main-light-lookup",SourceEvidence,caller.ActualLightShaftArgument):null;
    }
    public MapPostEffect3ApplyUncontrolledAbsentLightInputs? CreateAbsentLightInputs()
    {
        RequireCurrent();
        return Result.Payload8 is null ? new(Context.FrameNumber,world.CountedQueue.QueueOwnerIdentity+"/main-light-lookup",
            caller.ActualLightShaftArgument,SourceEvidence):null;
    }
}
