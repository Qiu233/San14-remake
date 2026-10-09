using Godot;
using San14.Data;

/// <summary>Selected original graph auxiliary view owner. It allocates no
/// image and certifies no target contents. Each read borrows this accepted
/// frame's actual source target views, including after target replacement.</summary>
public sealed class MapSelectedSourceImageOwner : IAsyncDisposable
{
    public MapSelectedSourceImagePlan Source { get; }
    private readonly object gate = new();
    private bool retiring;
    private MapSelectedSourceImageOwner(MapSelectedSourceImagePlan source) => Source=source;
    public static MapSelectedSourceImageOwner FromSource(MapSelectedSourceImagePlan source)
    {ArgumentNullException.ThrowIfNull(source);source.Validate();return new(source);}

    public static void ValidateCompatible(MapSelectedSourceImagePlan initial,MapSelectedSourceImagePlan current)
    {
        initial.Validate(); current.Validate();
        var a=initial.Selection;var b=current.Selection;
        if(a.Combination.Index!=b.Combination.Index||a.PixelProgramIndex!=b.PixelProgramIndex||a.PixelProgramSha256!=b.PixelProgramSha256||
            a.FeatureMatch.RowIndex!=b.FeatureMatch.RowIndex||!a.RequestedVariant96.Span.SequenceEqual(b.RequestedVariant96.Span)||
            a.SourcePlan.Graph.Source.DecodedSha256!=b.SourcePlan.Graph.Source.DecodedSha256||
            a.SourcePlan.Graph.Source.ResourceUri!=b.SourcePlan.Graph.Source.ResourceUri||
            a.Camera.Source.Camera.ObjectId!=b.Camera.Source.Camera.ObjectId||a.Camera.ExecutableLocale!=b.Camera.ExecutableLocale||
            a.Camera.Source.Resource.Source.DecodedSha256!=b.Camera.Source.Resource.Source.DecodedSha256||
            a.Camera.Source.Resource.Source.ResourceUri!=b.Camera.Source.Resource.Source.ResourceUri)
            throw new InvalidDataException("Current original image/sampler selection differs from the compiled owner generation; rebuild its owner.");
    }

    public Task<IReadOnlyList<DeferredDeviceBinding>> ReadTargetBindingsAsync(
        MapSelectedSourceImagePlan currentImagePlan, DeferredTransparencyImageSelection transparency,
        IReadOnlyList<RenderTargetFrame> producedTargets, bool explicitRefractionEnabled=false)
    {
        ArgumentNullException.ThrowIfNull(currentImagePlan);ArgumentNullException.ThrowIfNull(producedTargets);
        lock(gate)ObjectDisposedException.ThrowIf(retiring,this);
        ValidateCompatible(Source,currentImagePlan);
        var desired=new List<DeferredTargetImageSource>();
        if(currentImagePlan.Images.Any(i=>i.Role=="TranslucentSpecular"))desired.Add(currentImagePlan.SelectSpecular(transparency));
        if(currentImagePlan.GraphTargets.SingleOrDefault(t=>t.Role=="BlurredDepth") is {} blurred)desired.Add(blurred);
        if(explicitRefractionEnabled&&currentImagePlan.GraphTargets.SingleOrDefault(t=>t.Role=="RefractionDepth") is {} refraction)
            desired.Insert(0,refraction);
        var frames=producedTargets.ToArray();
        if(frames.Any(f=>f is null))throw new InvalidDataException("Selected Deferred graph source frame is null.");
        var selected=new List<(DeferredTargetImageSource Source,RenderTargetDeviceTexture Device)>();
        foreach(var source in desired)
        {
            var matches=frames.SelectMany(f=>f.Textures.Values).Where(t=>t.Source.Object.ObjectId==source.Texture.Object.ObjectId&&
                t.Source.Source.Source.ResourceUri==source.Texture.Source.Source.ResourceUri&&
                t.Source.Source.Source.DecodedSha256==source.Texture.Source.Source.DecodedSha256).DistinctBy(t=>t.StorageRid).ToArray();
            if(matches.Length!=1)throw new InvalidDataException("Selected Deferred image requires one actual produced graph view: "+source.Role+".");
            var texture=matches[0];
            var canonical=RenderTextureResources.Read(texture.Source.Source,texture.Source.Object.ObjectId);
            if(canonical.Kind!=source.Texture.Kind||canonical.Width!=source.Texture.Width||canonical.Height!=source.Texture.Height||
                RenderTextureFormatMath.Resolve(canonical)!=RenderTextureFormatMath.Resolve(source.Texture)||
                canonical.LevelNum!=source.Texture.LevelNum||canonical.MinMipLevel!=source.Texture.MinMipLevel||canonical.MultiSample!=source.Texture.MultiSample)
                throw new InvalidDataException("Selected auxiliary view differs from its original target declaration.");
            selected.Add((source,texture));
        }
        return TerrainDeviceTexture.OnRenderThreadAsync<IReadOnlyList<DeferredDeviceBinding>>(rd=>
        {
            lock(gate)ObjectDisposedException.ThrowIf(retiring,this);
            foreach(var pair in selected)
            {
                var device=pair.Device;
                if(!device.StorageRid.IsValid||!device.ShaderViewRid.IsValid||!rd.TextureIsValid(device.StorageRid)||!rd.TextureIsValid(device.ShaderViewRid))
                    throw new InvalidDataException("Selected source graph view has retired.");
                using var actual=rd.TextureGetFormat(device.ShaderViewRid);
                if(actual.TextureType!=RenderingDevice.TextureType.Type2D||actual.ArrayLayers!=1||
                    actual.Mipmaps!=(uint)pair.Source.Texture.LevelNum||
                    actual.Width!=(uint)device.ExplicitExtent.X||actual.Height!=(uint)device.ExplicitExtent.Y||actual.Format!=device.ShaderFormat||
                    !actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                    throw new InvalidDataException("Selected source graph view differs from its actual float2D descriptor.");
            }
            return Array.AsReadOnly(selected.Select(p=>new DeferredDeviceBinding("Texture",p.Source.PixelBinding.BindPoint,
                p.Device.ShaderViewRid,p.Device.StorageRid,p.Source.Texture.Source.Source.ResourceUri+"#"+
                p.Source.Texture.Object.ObjectId.ToString("X8")+";source-field="+p.Source.SourceArgument.NameHash.ToString("X8")+
                ";source-sha="+p.Source.Texture.Source.Source.DecodedSha256)).ToArray());
        });
    }
    public ValueTask DisposeAsync(){lock(gate)retiring=true;return ValueTask.CompletedTask;}

    /// <summary>Borrow complete actual auxiliary producers for this accepted
    /// frame. The returned batch owns leases across the consumer's cached draw.</summary>
    public async Task<MapSelectedSourceImageBindings> BorrowTargetBindingsAsync(
        MapSelectedSourceImagePlan currentImagePlan, DeferredTransparencyImageSelection transparency,
        IReadOnlyList<DeferredAuxiliaryTargetDevice> producedTargets, bool explicitRefractionEnabled=false)
        =>await BorrowTargetBindingsCoreAsync(currentImagePlan,transparency,producedTargets,
            explicitRefractionEnabled,currentImagePlan.CurrentProducerProvenance);

    public Task<MapSelectedSourceImageBindings> BorrowWorldTargetBindingsAsync(
        MapSelectedSourceImagePlan currentImagePlan, DeferredTransparencyImageSelection transparency,
        IReadOnlyList<DeferredAuxiliaryTargetDevice> producedTargets,
        MainMapCameraDeferredReadyFrame sharedCamera,
        MapSourceCreatedNativeSessionPreparedFrame consumer)
    {
        ArgumentNullException.ThrowIfNull(sharedCamera); ArgumentNullException.ThrowIfNull(consumer);
        if(!ReferenceEquals(currentImagePlan.Selection,consumer.Selection))
            throw new InvalidDataException("World image reader has another source selection.");
        string producer=sharedCamera.RequireSharedWorldConsumer(consumer);
        return BorrowTargetBindingsCoreAsync(currentImagePlan,transparency,producedTargets,true,producer);
    }

    private async Task<MapSelectedSourceImageBindings> BorrowTargetBindingsCoreAsync(
        MapSelectedSourceImagePlan currentImagePlan, DeferredTransparencyImageSelection transparency,
        IReadOnlyList<DeferredAuxiliaryTargetDevice> producedTargets, bool explicitRefractionEnabled,
        string actualProducerProvenance)
    {
        ArgumentNullException.ThrowIfNull(currentImagePlan);ArgumentNullException.ThrowIfNull(producedTargets);
        lock(gate)ObjectDisposedException.ThrowIf(retiring,this);
        ValidateCompatible(Source,currentImagePlan);
        var desired=new List<DeferredTargetImageSource>();
        if(currentImagePlan.Images.Any(i=>i.Role=="TranslucentSpecular"))desired.Add(currentImagePlan.SelectSpecular(transparency));
        if(currentImagePlan.GraphTargets.SingleOrDefault(t=>t.Role=="BlurredDepth") is {} blurred)desired.Add(blurred);
        if(explicitRefractionEnabled&&currentImagePlan.GraphTargets.SingleOrDefault(t=>t.Role=="RefractionDepth") is {} refraction)
            desired.Insert(0,refraction);
        var candidates=producedTargets.ToArray();
        if(candidates.Any(device=>device is null))throw new InvalidDataException("Selected auxiliary producer is null.");
        var leases=new List<DeferredAuxiliaryTargetBorrow>();
        var bindings=new List<DeferredDeviceBinding>();
        try
        {
            foreach(var target in desired)
            {
                var matches=candidates.Where(device=>device.Source.Object.ObjectId==target.Texture.Object.ObjectId&&
                    device.Source.Source.Source.ResourceUri==target.Texture.Source.Source.ResourceUri&&
                    device.Source.Source.Source.DecodedSha256==target.Texture.Source.Source.DecodedSha256).Distinct().ToArray();
                if(matches.Length!=1)throw new InvalidDataException("Selected image requires one actual auxiliary producer: "+target.Role+".");
                var device=matches[0];DeferredAuxiliaryTargetSources.Validate(device.Construction);
                if(device.Source with{Source=target.Texture.Source,Object=target.Texture.Object}!=target.Texture||
                    device.MipCount!=target.Texture.LevelNum||device.MostDetailedMip!=target.Texture.MinMipLevel||
                    device.ViewMipCount!=device.MipCount-device.MostDetailedMip)
                    throw new InvalidDataException("Selected image differs from its actual auxiliary source declaration.");
                var lease=await device.BorrowAsync(currentImagePlan.FrameNumber,actualProducerProvenance);
                leases.Add(lease);
                var graph=currentImagePlan.TexturePlan.Graph;
                if(lease.FrameNumber!=currentImagePlan.FrameNumber||lease.ProducerProvenance!=actualProducerProvenance||
                    lease.Receipts.Count==0||lease.Receipts.Any(receipt=>receipt.FrameNumber!=lease.FrameNumber||
                        receipt.ProducerProvenance!=lease.ProducerProvenance||receipt.TextureObjectId!=target.Texture.Object.ObjectId||
                        receipt.GraphResourceUri!=graph.Source.ResourceUri||receipt.GraphSha256!=graph.Source.DecodedSha256||
                        (uint)receipt.WriterNodeIndex>=graph.Document.Nodes.Count||
                        graph.Document.Nodes[receipt.WriterNodeIndex].Call.FactoryClassId!=receipt.WriterFactoryClassId))
                    throw new InvalidDataException("Selected image lacks the actual current source graph writer receipts.");
                bindings.Add(new("Texture",target.PixelBinding.BindPoint,lease.ShaderViewRid,lease.StorageRid,
                    target.Texture.Source.Source.ResourceUri+"#"+target.Texture.Object.ObjectId.ToString("X8")+
                    ";source-field="+target.SourceArgument.NameHash.ToString("X8")+
                    ";source-sha="+target.Texture.Source.Source.DecodedSha256));
            }
            lock(gate)ObjectDisposedException.ThrowIf(retiring,this);
            return new(currentImagePlan,bindings.AsReadOnly(),leases.AsReadOnly());
        }
        catch
        {
            for(int index=leases.Count-1;index>=0;index--)leases[index].Dispose();
            throw;
        }
    }
}
