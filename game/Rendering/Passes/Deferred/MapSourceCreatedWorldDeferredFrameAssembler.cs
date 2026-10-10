using System.Buffers.Binary;
using San14.Data;

/// <summary>Same-frame model/material/graph resource assembly for actual
/// seasonal World F103 passes. Sky's selected wrapper is supplied by its
/// independent current device owner; no static HDRI cube is substituted.</summary>
public sealed class MapSourceCreatedWorldDeferredFrameAssembler : IAsyncDisposable
{
    public MapSourceCreatedDirectionalDeferredProvider Lighting { get; }
    public NativeDeferredResources Resources { get; }
    public MapSelectedSourceSamplerOwner Samplers { get; }
    public MapSelectedSourceImageOwner Images { get; }
    public MapMaterialCachePrepared Materials { get; }
    public NativeMapModelPopulationRenderer Population { get; }
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly Dictionary<long,List<MapSelectedSourceImageBindings>> leases=new();
    private readonly object lifetimeGate=new();
    private bool retiring;
    private Task? disposal;
    private long begunAttempt;

    public MapSourceCreatedWorldDeferredFrameAssembler(MapSourceCreatedDirectionalDeferredProvider lighting,
        NativeDeferredResources resources,MapSelectedSourceSamplerOwner samplers,
        MapSelectedSourceImageOwner images,MapMaterialCachePrepared materials,
        NativeMapModelPopulationRenderer population)
    {
        ArgumentNullException.ThrowIfNull(lighting);ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(samplers);ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(materials);ArgumentNullException.ThrowIfNull(population);
        if(!ReferenceEquals(resources.Program,lighting.Program)||materials.SelectedSource is null||
            materials.Cache.Branch!=MapMaterialCacheBranch.FreshExternalSource||materials.Table.EntryCount!=2)
            throw new InvalidDataException("World Deferred requires its actual selected program, native material source and owned population.");
        (Lighting,Resources,Samplers,Images,Materials,Population)=(lighting,resources,samplers,images,materials,population);
    }

    public async Task<MapSourceCreatedDeferredFramePacket> PrepareAsync(NativeMapFrameContext context,
        MapSourceCreatedLightingPacket lighting,MapSourceCreatedSelectedDeferredFrameInputs input,
        SkyDirectionalAmbientDeviceBinding ambient,DeferredSelected383AdditionalInputs additional,
        MapSourceCreatedNativeSessionPreparedFrame accepted,MainMapCameraDeferredReadyFrame sharedCamera,
        DeferredAuxiliaryCameraFilterSource cameraFilters,MapCommonViewFilterCurrent commonFilters)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(ambient);
        MapSelectedSourceImageBindings? pending=null;
        await gate.WaitAsync();
        try
        {
            lock(lifetimeGate)ObjectDisposedException.ThrowIf(retiring,this);
            input.CurrentSamplers.Validate();Lighting.Program.ValidateCompatibleCurrentSelection(input.CurrentSelection);
            input.CurrentSelection.OwnedFeatureSource!.ValidateFrame(input.CurrentSelection.Camera,lighting);
            var geometry=input.Geometry;var population=Population.LastAcceptedFrame;
            if(population is null||!ReferenceEquals(population.Context,context)||!ReferenceEquals(population.Frame,geometry.Models)||
                !ReferenceEquals(geometry.ModelAcceptedContext,context)||!geometry.Models.Complete||
                !ReferenceEquals(input.CurrentSamplers.Selection,input.CurrentSelection)||lighting.FrameNumber!=context.FrameNumber||
                context.PreparedCamera is null||lighting.SceneInputs.Camera!=context.PreparedCamera||
                input.CurrentSelection.Camera.SceneInputs!=context.PreparedCamera)
                throw new InvalidDataException("World Deferred requires its actual successful geometry and same accepted View/Main frame.");
            if(!ReferenceEquals(accepted.Context,context)||!ReferenceEquals(accepted.Selection,input.CurrentSelection)||
                !ReferenceEquals(accepted.Lighting,lighting)||accepted.PreparedSourceFrame is not { } sourceFrame)
                throw new InvalidDataException("World image assembly has another actual source pass.");
            var main=lighting.AcceptedInputs.PresentSeasonCamera?.AcceptedSeasonCamera.Inputs.MainFrame
                ??throw new InvalidDataException("World transparency needs its current actual Main/season source.");
            cameraFilters.RequireSameMain(main);
            commonFilters.ValidateFrame(context.FrameNumber,sourceFrame.Common.CommonTableOwnerIdentity,lighting.Binding.ViewIdentity);
            var expectedTransparency=cameraFilters.ReadTransparencySelection(
                sourceFrame.Common.CommonViewResource240Present?commonFilters.TransparencyResolution8C:null);
            if(input.CurrentSamplers.EffectiveBindings.Interface.Sha256!=Lighting.Program.Translation.SpirvSha256||
                geometry.TransparencySelection!=expectedTransparency)
                throw new InvalidDataException("Current World sampler interface or transparency differs from its exact selected source.");
            var plan=input.CurrentSamplers.ImagePlan;
            uint ambientRegister=plan.RequireRole("AmbientSpecular").Declaration.BindPoint;
            ambient.State.ValidateCurrent(ambient.State.OwnedSkyIdentity,context.FrameNumber);
            if(!ambient.HasCompleteGeneratedContents||ambient.Texture.SourceBindPoint!=ambientRegister||
                ambient.Texture.Resource!=ambient.Cube.TextureRid||ambient.Texture.OwnerTexture!=ambient.Cube.TextureRid||
                !ambient.Cube.TextureRid.IsValid||ambient.State.SelectedSpecularIndex>=4)
                throw new InvalidOperationException("Actual native-selected Sky wrapper needs completed contents before this backend presents it.");
            var known=plan.GraphTargets.Select(t=>t.PixelBinding.BindPoint).Append(ambientRegister).ToHashSet();
            var remaining=Resources.RequiredExplicitInputs.Where(d=>d.DescriptorKind=="Texture"&&!known.Contains(d.Source.BindPoint))
                .Select(d=>d.Source.BindPoint).Order().ToArray();
            if(!remaining.SequenceEqual(geometry.RemainingSourceImages.Select(i=>i.SourceBindPoint).Order()))
                throw new InvalidDataException("World Deferred is missing an actual remaining source image owner.");
            var devices=input.ProducedAuxiliaryDevices??throw new InvalidDataException("World Deferred needs actual current auxiliary devices and read leases.");
            pending=await Images.BorrowWorldTargetBindingsAsync(plan,geometry.TransparencySelection,devices,sharedCamera,accepted);
            var samplerBindings=Samplers.GetBindings(input.CurrentSamplers);
            var neededSamplers=Resources.RequiredExplicitInputs.Where(d=>d.DescriptorKind=="Sampler").Select(d=>d.Source.BindPoint).Order();
            if(!neededSamplers.SequenceEqual(samplerBindings.Select(b=>b.SourceBindPoint).Order())||
                Resources.RequiredExplicitInputs.Any(d=>d.DescriptorKind=="TextureBuffer"))
                throw new InvalidDataException("World Deferred requires every source sampler and any separately owned active typed buffer.");
            var combined=pending.Bindings.Append(ambient.Texture).Concat(samplerBindings).Concat(geometry.RemainingSourceImages).ToArray();
            if(combined.Select(b=>(b.DescriptorKind,b.SourceBindPoint)).Distinct().Count()!=combined.Length)
                throw new InvalidDataException("World resource producers overlap a source binding role.");
            var material=MapMaterialCacheProvider.Prepare(Materials.Cache,geometry.Models.Packets,geometry.TerrainMaterials,
                geometry.ExplicitCurrentModelMaterial) with {SelectedSource=Materials.SelectedSource};
            var template=await Resources.AssemblePacketAsync(context.TerrainFrame,geometry.CompleteConstantBuffers,combined,geometry.Raster);
            var packet=await Lighting.PrepareDeferredAsync(context,template,lighting,input.CurrentSelection,additional);
            lock(lifetimeGate)
            {
                if(!leases.TryGetValue(context.FrameNumber,out var current))leases.Add(context.FrameNumber,current=new());
                current.Add(pending);pending=null;
            }
            return new(context,lighting,material,packet);
        }
        finally{if(pending is not null)await pending.DisposeAsync();gate.Release();}
    }

    public void CompletePreviousReads(NativeMapFrameContext context)
    {
        lock(lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(context.FrameNumber<=0||context.FrameNumber<begunAttempt)throw new InvalidDataException("World source attempts must be ordered.");
            begunAttempt=context.FrameNumber;
            foreach(var lease in leases.Where(p=>p.Key<context.FrameNumber).SelectMany(p=>p.Value))lease.CompleteRead();
        }
    }
    public async Task CompleteDeferredReadsAsync(NativeMapFrameContext context)
    {
        await gate.WaitAsync();
        try
        {
            List<MapSelectedSourceImageBindings> stale=new();
            lock(lifetimeGate)
            {
                if(leases.TryGetValue(context.FrameNumber,out var current))foreach(var lease in current)lease.CompleteRead();
                foreach(long key in leases.Keys.Where(k=>k<context.FrameNumber).ToArray()){stale.AddRange(leases[key]);leases.Remove(key);}
            }
            foreach(var lease in stale)await lease.DisposeAsync();
        }
        finally{gate.Release();}
    }
    public async Task RetireTargetDependenciesAsync()
    {
        await gate.WaitAsync();
        try
        {
            MapSelectedSourceImageBindings[] retired;
            lock(lifetimeGate){retired=leases.Values.SelectMany(p=>p).ToArray();leases.Clear();}
            foreach(var lease in retired)await lease.DisposeAsync();
        }
        finally{gate.Release();}
    }
    public ValueTask DisposeAsync(){lock(lifetimeGate){retiring=true;return new(disposal??=RetireAsync());}}
    private async Task RetireAsync(){await RetireTargetDependenciesAsync();Lighting.Dispose();}
}
