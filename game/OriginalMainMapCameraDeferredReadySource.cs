using Godot;
using San14.Data;

/// <summary>The exact source Main Camera module publication owns a current
/// B6169FDB descriptor lease. CompleteRead ends its ordered read; Dispose also
/// releases the view lifetime after any cached consumer has retired.</summary>
public sealed class OriginalMainMapCameraDeferredReadyFrame : IDisposable
{
    private OriginalDeferredAuxiliaryTargetBorrow? depth;
    private readonly OriginalMainMapCameraDeferredSourcePacket camera;
    private readonly OriginalMapSourceCreatedNativeSessionPreparedFrame accepted;
    private readonly OriginalMainMapViewportDepthPacket viewport;

    internal OriginalMainMapCameraDeferredReadyFrame(
        OriginalMapSourceCreatedNativeSessionPreparedFrame accepted,
        OriginalMainMapViewportDepthPacket viewport,
        OriginalMainMapCameraRefractionBindingSource source,
        OriginalDeferredAuxiliaryTargetBorrow depth,
        OriginalMainMapCameraDeferredSourcePacket camera)
    { this.accepted=accepted;this.viewport=viewport;Source=source;this.depth=depth;this.camera=camera; }

    public OriginalMainMapCameraRefractionBindingSource Source { get; }
    public long FrameNumber=>camera.FrameNumber;
    public bool RefractionTexturePresent { get { RequireLive();return true; } }
    public Rid RefractionShaderViewRid { get { return RequireLive().ShaderViewRid; } }
    public Rid RefractionStorageRid { get { return RequireLive().StorageRid; } }
    public OriginalMainMapCameraDeferredSourcePacket Camera
    {
        get
        {
            RequireLive();
            viewport.ValidateFrame(accepted.Selection,accepted.Feature.Binding.ViewIdentity);
            return camera;
        }
    }
    public OriginalMapSourceCreatedNativeSessionGpuInputs CreateSessionInputs(
        IReadOnlyList<OriginalMapMaterialTerrainConsumer> terrainMaterials,
        IReadOnlyList<OriginalDeferredAuxiliaryTargetDevice> producedAuxiliaryDevices,
        OriginalDeferredRasterState raster)
        =>OriginalMapSourceCreatedNativeSessionGpuInputs.FromAcceptedCamera(accepted,Camera,
            terrainMaterials,producedAuxiliaryDevices,raster) with { SharedCameraReady=this };

    /// <summary>Independent World modifier passes consume this one actual
    /// Camera/Aux generation. Authorizing a reader does not rename its shader
    /// feature provenance or manufacture a target completion.</summary>
    public string RequireSharedWorldConsumer(OriginalMapSourceCreatedNativeSessionPreparedFrame consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        _=Camera;
        accepted.Feature.ValidateFrame(accepted.MainCamera,accepted.Lighting);
        consumer.Feature.ValidateFrame(consumer.MainCamera,consumer.Lighting);
        if(!ReferenceEquals(consumer.Context,accepted.Context)||consumer.Context.FrameNumber!=FrameNumber||
            consumer.PreparedSourceFrame is not { } source||accepted.PreparedSourceFrame is not { } producer||
            !ReferenceEquals(source.ViewFields,producer.ViewFields)||
            !ReferenceEquals(consumer.CpuPopulation,accepted.CpuPopulation)||
            consumer.Lighting.Binding!=accepted.Lighting.Binding||
            consumer.Selection.Combination.Index!=510||consumer.Selection.PixelProgramIndex!=383||
            !consumer.MainCamera.Prefix.Span.SequenceEqual(accepted.MainCamera.Prefix.Span)||
            !consumer.Lighting.LocalLightData.Span.SequenceEqual(accepted.Lighting.LocalLightData.Span))
            throw new InvalidDataException("World auxiliary reader does not share this actual accepted Camera/View/outer-light generation.");
        return accepted.Selection.Current.CurrentProducerProvenance;
    }

    /// <summary>Only the actual node139/140 source-null executions preserve
    /// every sampler. A completed arbitrary refraction geometry writer does
    /// not establish that absence and is not admitted by this bounded scope.</summary>
    internal void RequireSourceNullRefractionSamplerPreservation(OriginalMapSourceCreatedNativeSessionPreparedFrame frame,
        OriginalDeferredAuxiliaryTargetDevice target)
    {
        if(!ReferenceEquals(frame,accepted))throw new InvalidDataException("Refraction sampler proof belongs to another accepted source frame.");
        var current=RequireLive();
        foreach(int writer in new[]{139,140})
        {
            var receipt=current.Receipts.LastOrDefault(r=>r.WriterNodeIndex==writer)??
                throw new InvalidDataException("Source-null refraction sampler proof lacks its actual writer receipt.");
            var execution=receipt.SourceNullRefractionExecution??
                throw new NotSupportedException("Non-null refraction geometry needs its actual sampler writer continuation.");
            execution.ValidateCurrent(accepted,target,writer);
        }
    }

    private OriginalDeferredAuxiliaryTargetBorrow RequireLive()
        =>depth??throw new ObjectDisposedException(nameof(OriginalMainMapCameraDeferredReadyFrame));
    public void CompleteRead()=>RequireLive().CompleteRead();
    public void Dispose()=>Interlocked.Exchange(ref depth,null)?.Dispose();
}

/// <summary>Production source Main38D/View0/Class868/Color60 camera gate. Its
/// two depths come from the owned native backend writer, and its refraction
/// presence comes from an actual complete current B6169FDB shader view. Invoke
/// after source auxiliary production; allocation alone cannot publish this
/// frame. Later edited modules use the separate current-cache boundary.</summary>
public static class OriginalMainMapCameraDeferredReadySource
{
    public static async Task<OriginalMainMapCameraDeferredReadyFrame> PrepareAsync(
        OriginalMapSourceCreatedNativeSessionPreparedFrame accepted,
        OriginalMainMapViewportDepthPacket viewport,
        OriginalDeferredPixelProgram borrowedProgram,
        OriginalDeferredAuxiliaryTargetDevice actualRefractionDepth)
    {
        ArgumentNullException.ThrowIfNull(accepted);ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(borrowedProgram);ArgumentNullException.ThrowIfNull(actualRefractionDepth);
        accepted.Selection.Validate();accepted.Feature.ValidateFrame(accepted.MainCamera,accepted.Lighting);
        viewport.ValidateFrame(accepted.Selection,accepted.Feature.Binding.ViewIdentity);
        borrowedProgram.ValidateCompatibleCurrentSelection(accepted.Selection);
        if(!borrowedProgram.ShaderRid.IsValid||
            !ReferenceEquals(accepted.Selection.OwnedFeatureSource,accepted.Feature)||
            accepted.Context.FrameNumber!=accepted.MainCamera.FrameNumber||
            accepted.Context.FrameNumber!=accepted.Lighting.FrameNumber||
            accepted.Context.PreparedCamera is null||
            accepted.Context.PreparedCamera!=OriginalPreparedCameraSceneSource.ReadCompletePrefix(accepted.MainCamera.Prefix.Span))
            throw new InvalidDataException("Ready Main Camera requires its actual accepted Main/View/light/program frame.");
        var source=OriginalMainMapCameraRefractionBindingSource.ReadSource(accepted.Selection);
        source.ValidateTarget(actualRefractionDepth.Construction,accepted.Context.FrameNumber);
        string producer=accepted.Selection.Current.CurrentProducerProvenance;
        var depth=await actualRefractionDepth.BorrowAsync(accepted.Context.FrameNumber,producer);
        try
        {
            var graph=accepted.Selection.SourcePlan.Graph;
            if(depth.FrameNumber!=accepted.Context.FrameNumber||depth.ProducerProvenance!=producer||
                !depth.StorageRid.IsValid||!depth.ShaderViewRid.IsValid||depth.MostDetailedMip!=0||depth.ViewMipCount!=1||
                depth.Receipts.Count==0||depth.Receipts.Any(receipt=>receipt.FrameNumber!=depth.FrameNumber||
                    receipt.ProducerProvenance!=producer||receipt.TextureObjectId!=0xB6169FDB||
                    receipt.GraphResourceUri!=graph.Source.ResourceUri||receipt.GraphSha256!=graph.Source.DecodedSha256||
                    (uint)receipt.WriterNodeIndex>=graph.Document.Nodes.Count||
                    graph.Document.Nodes[receipt.WriterNodeIndex].Call.FactoryClassId!=receipt.WriterFactoryClassId))
                throw new InvalidDataException("Main refraction descriptor lacks this accepted source graph's actual current writer completion.");
            // Node97 copies depth before the source138 attachment and both
            // mode16 geometry writers. A copy-only receipt cannot publish a
            // complete Refraction pass, including for source-owned empty
            // queues: those two owners must publish their actual no-op or
            // completed geometry executions in order.
            int priorReceipt=-1;
            foreach(int writer in new[]{97,139,140})
            {
                uint factory=writer==97?0xFD1176C6u:0xFC64DCB6u;
                if(graph.Document.Nodes[writer].Call.FactoryClassId!=factory)
                    throw new InvalidDataException("Main refraction source writer chain differs from node97/139/140.");
                int next=-1;
                for(int index=priorReceipt+1;index<depth.Receipts.Count;index++)
                    if(depth.Receipts[index].WriterNodeIndex==writer&&
                        depth.Receipts[index].WriterFactoryClassId==factory&&
                        depth.Receipts[index].SurfaceIds.SequenceEqual(new uint[]{0}))
                    {next=index;break;}
                if(next<0)
                    throw new InvalidDataException("Main refraction pass requires actual ordered node97/139/140 production; copy-only or unproved empty geometry is unavailable.");
                depth.Receipts[next].SourceNullRefractionExecution?.ValidateCurrent(accepted,actualRefractionDepth,writer);
                priorReceipt=next;
            }
            // BorrowAsync validated the live whole-range2D shader view on the
            // render thread and protects it across this source publication.
            viewport.ValidateFrame(accepted.Selection,accepted.Feature.Binding.ViewIdentity);
            var camera=OriginalMainMapCameraDeferredSource.PrepareAfterSuccessfulFreshMainViewInitialization(
                accepted.Selection,new(viewport.ViewportMinimumDepth,viewport.ViewportMaximumDepth,true,
                    accepted.Context.FrameNumber,viewport.SourceProvenance+";"+source.SourceProvenance+
                    ";actual-ready-B6169FDB/V28-descriptor="+depth.ShaderViewRid.Id+
                    ";encoded23-publication;source-created-owned-graphics;not-original-process-state"));
            return new(accepted,viewport,source,depth,camera);
        }
        catch{depth.Dispose();throw;}
    }
}
