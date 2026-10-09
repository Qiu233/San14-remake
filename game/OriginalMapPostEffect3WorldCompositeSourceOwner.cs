using System.Buffers.Binary;
using Godot;
using San14.Data;

/// <summary>The remake frontend's actual source-format parent allocation and
/// typed target-stack entry. This is an owned producer, not identification of
/// an original-process pointer. Node192's matched Host mode1 preserves it;
/// node193 and Composite44 inherit that same live allocation.</summary>
public sealed partial class OriginalMapPostEffect3WorldCompositeSourceOwner:IAsyncDisposable
{
    private readonly OriginalMapSourceCreatedWorldPlayableHost host;
    private OriginalRenderTargetDeviceTexture target;
    private OriginalRenderOutputPresentation presentation;
    private readonly bool allowReadback;
    private readonly object lifetime=new();
    private readonly Stack<OriginalRenderTargetDeviceTexture> targetStack=new();
    private ParentBinding? current;
    private long revision;
    private bool retiring;
    private int accepted;
    private TaskCompletionSource? drained;
    private Task? disposal;
    private OriginalMapPostEffect3WorldCompositeSourceOwner(OriginalMapSourceCreatedWorldPlayableHost owner,
        OriginalRenderTargetDeviceTexture allocation,OriginalRenderOutputPresentation wrapper,bool readback)
        =>(host,target,presentation,allowReadback)=(owner,allocation,wrapper,readback);
    public Vector2I Extent=>target.ExplicitExtent;
    public OriginalRenderTextureRuntimeDescriptor EffectiveSource=>target.EffectiveSource;
    public ParentBinding? CurrentBinding{get{lock(lifetime)return current;}}
    public OriginalRenderOutputPresentation? CompletedPresentation{get;private set;}
    public string SourceEvidence=>"owned-remake-frontend-parent;"+target.Source.Source.Source.ResourceUri+
        ";original-Output13BF771D-descriptor;not-original-process-target;node192-Host-mode1-matched-parent-retained;node193-current-target-inheritance";

    /// <summary>Call after Host construction and before its first World draw.
    /// The actual frontend extent comes from the live owned Scene generation,
    /// while format/sampler/declaration identity come from original Output.</summary>
    public static async Task<OriginalMapPostEffect3WorldCompositeSourceOwner> BuildAsync(
        OriginalMapSourceCreatedWorldPlayableHost actualHost,bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(actualHost);
        var source=actualHost.Final.Chain.Source;
        var declaration=source.OutputTexture;
        var scene=actualHost.Scene.OutputFrame.Textures[0x064B746B];
        var formats=OriginalRenderTextureFormatMath.Resolve(declaration);
        if(declaration.Object.ObjectId!=0x13BF771D||declaration.Width!=0||declaration.Height!=0||
            declaration.Kind!=OriginalRenderTextureKind.Texture2D||declaration.LevelNum!=1||declaration.MultiSample!=0||
            formats.ShaderViewFormat!=91||formats.TargetViewFormat!=91||
            source.Graph.Source.DecodedSha256!=actualHost.Scene.OutputFrame.Source.Graph.Source.DecodedSha256)
            throw new NotSupportedException("World frontend parent needs the exact dynamic source Output BGRA8 sRGB declaration and same graph.");
        string owner=actualHost.Session.Binding.ViewIdentity+"/frontend-source-Output13BF/"+Guid.NewGuid().ToString("N");
        var effective=OriginalRenderTextureRuntimeDescriptor.ConstructDeclared(declaration,owner);
        var allocation=await AllocateAsync(declaration,scene.ExplicitExtent,effective,allowReadback);
        try{return new(actualHost,allocation,await OriginalRenderOutputPresentation.BuildAsync(allocation),allowReadback);}
        catch{await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(allocation.StorageRid);return true;});throw;}
    }

    private static Task<OriginalRenderTargetDeviceTexture> AllocateAsync(OriginalRenderTextureDefinition declaration,
        Vector2I extent,OriginalRenderTextureRuntimeDescriptor effective,bool allowReadback)
        =>OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            using var format=new RDTextureFormat{Format=RenderingDevice.DataFormat.B8G8R8A8Srgb,
                Width=(uint)extent.X,Height=(uint)extent.Y,Depth=1,ArrayLayers=1,Mipmaps=1,
                TextureType=RenderingDevice.TextureType.Type2D,UsageBits=RenderingDevice.TextureUsageBits.SamplingBit|
                    RenderingDevice.TextureUsageBits.ColorAttachmentBit|RenderingDevice.TextureUsageBits.CanCopyFromBit|
                    RenderingDevice.TextureUsageBits.CanCopyToBit|(allowReadback?RenderingDevice.TextureUsageBits.CanUpdateBit:0)};
            using var view=new RDTextureView();Rid image=rd.TextureCreate(format,view);
            if(!image.IsValid)throw new InvalidDataException("Owned frontend source Output allocation failed.");
            return new OriginalRenderTargetDeviceTexture(declaration,extent,image,image,
                format.Format,format.Format,format.Format){EffectiveSource=effective};
        });

    /// <summary>Replace only the actual frontend target generation after prior
    /// commands returned. The independent Surface keeps its accepted canvas.</summary>
    public async Task RefreshOwnedExtentAsync(OriginalNativeMapFrameContext context,Func<Task>? retireDisplayBorrower=null)
    {
        var extent=context.OutputFrame.Textures[0x064B746B].ExplicitExtent;
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(extent==Extent)return;
            if(accepted!=0)throw new InvalidOperationException("Cannot resize the frontend while Composite consumes it.");
        }
        var declaration=target.Source;
        var effective=OriginalRenderTextureRuntimeDescriptor.ConstructDeclared(declaration,
            target.EffectiveSource.OwnerIdentity+"/resized");
        var next=await AllocateAsync(declaration,extent,effective,allowReadback);
        OriginalRenderOutputPresentation nextPresentation;
        try{nextPresentation=await OriginalRenderOutputPresentation.BuildAsync(next);}
        catch{await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(next.StorageRid);return true;});throw;}
        var previousTarget=target;var previousPresentation=presentation;
        try{if(retireDisplayBorrower is not null)await retireDisplayBorrower();}
        catch
        {
            await nextPresentation.DisposeAsync();
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(next.StorageRid);return true;});
            throw;
        }
        target=next;presentation=nextPresentation;current=null;targetStack.Clear();CompletedPresentation=null;
        await previousPresentation.DisposeAsync();
        await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(previousTarget.StorageRid);return true;});
    }

    public OriginalWorldMainDisplaySurfaceSet CaptureActualDisplaySurfaces()
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        var actualTarget=target;var actualWrapper=presentation;
        void RequireCurrent()
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(target,actualTarget)||!ReferenceEquals(presentation,actualWrapper))
                throw new InvalidOperationException("The actual display surface generation has been replaced.");
        }
        return new(new(actualTarget,actualWrapper,actualTarget.StorageRid,actualTarget.ShaderViewRid,
            actualTarget.ExplicitExtent,91,RequireCurrent,
            "actual-owned-backend-display-surface;BGRA8-sRGB;separate-from-RGK-working-Scene;native-Windows-device-query-is-a-port-boundary"),null);
    }

    /// <summary>The frontend actually pushes its owned parent. Original node192
    /// mode1/Host virtual80=false then tests effective ratio1 and target extent,
    /// performs no push, and node193 inherits the unchanged top. Actual late
    /// framebuffer completion is required, but its Scene is never substituted
    /// for this separately owned frontend target.</summary>
    public ParentBinding BindNode192And193Current(OriginalNativeMapFrameContext actualContext,
        OriginalMapCameraConstructionPacket actualPostSeasonCamera)
    {
        ArgumentNullException.ThrowIfNull(actualContext);ArgumentNullException.ThrowIfNull(actualPostSeasonCamera);
        var prepared=host.Session.CurrentFrame??throw new InvalidDataException("World preparation is absent.");
        var late=host.Scene.LastWorldDeferredSubmittedFrame??throw new InvalidDataException("World late framebuffer has not submitted.");
        host.Session.RequireSubmittedWorld(late);late.RequireCurrent(actualContext);
        var world=prepared.PreparedSourceFrame??throw new InvalidDataException("World source frame is absent.");
        var camera=world.Lighting.CapturePreparedCameraFromSameAcceptedMain(world.Camera);
        if(!ReferenceEquals(prepared.Context,actualContext)||camera.FrameNumber!=actualPostSeasonCamera.FrameNumber||
            camera.Source.Camera.ObjectId!=actualPostSeasonCamera.Source.Camera.ObjectId||
            camera.Source.Resource.Source.ResourceUri!=actualPostSeasonCamera.Source.Resource.Source.ResourceUri||
            camera.Source.Resource.Source.DecodedSha256!=actualPostSeasonCamera.Source.Resource.Source.DecodedSha256||
            !camera.Prefix.Span.SequenceEqual(actualPostSeasonCamera.Prefix.Span)||
            world.Context.Source.Host.Locale!=camera.ExecutableLocale||
            world.Context.Source.Host.RuleVersion!=RenderRuleIdentity.Version||
            actualContext.OutputFrame.Textures[0x064B746B].ExplicitExtent!=Extent)
            throw new InvalidDataException("Node192/193 target inheritance requires this actual World/post-season Camera/Q/extent.");
        var ratio=camera.Source.Camera.Properties[996];
        if(ratio.Index!=996||ratio.NameHash!=0x95C18937||ratio.TypeCode!=8||ratio.ElementCount!=1||
            ratio.RawValue.Length!=4||BinaryPrimitives.ReadUInt32LittleEndian(ratio.RawValue)!=0)
            throw new NotSupportedException("This source node192 owner admits the original fresh RenderResolutionRatio0->effective1 branch only.");
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(accepted!=0)throw new InvalidOperationException("Cannot rebind frontend target during an accepted Composite draw.");
            targetStack.Clear();targetStack.Push(target);
            // The matched ratio1/context-size branch returns without
            // target push. This is execution of that finite owned branch.
            if(targetStack.Count!=1||!ReferenceEquals(targetStack.Peek(),target))
                throw new InvalidDataException("Node192 changed its owned inherited target.");
            CompletedPresentation=null;
            return current=new(this,actualContext,++revision,world.Context.Source.OwnedHostIdentity,
                SourceEvidence+";frontend-context-disabled;sourceRatio0-effective1;"+
                "matched-ratio-and-context-size-no-push;actual-frontend-stack-depth1;"+late.SourceEvidence);
        }
    }

    public sealed class ParentBinding
    {
        internal ParentBinding(OriginalMapPostEffect3WorldCompositeSourceOwner owner,OriginalNativeMapFrameContext context,
            long generation,string q,string evidence)
            =>(Owner,Context,Revision,ContextOwnerIdentity,SourceEvidence)=(owner,context,generation,q,evidence);
        internal OriginalMapPostEffect3WorldCompositeSourceOwner Owner{get;}
        internal long Revision{get;}
        public OriginalNativeMapFrameContext Context{get;}
        public long FrameNumber=>Context.FrameNumber;
        public string ContextOwnerIdentity{get;}
        public OriginalRenderTextureRuntimeDescriptor EffectiveSource=>Owner.target.EffectiveSource;
        public string SourceEvidence{get;}
        public void RequireCurrent()=>Owner.RequireCurrent(this);
        public IOriginalMapPostEffect3StockGraphicsTargetLease LeaseTarget44(
            OriginalMapPostEffect3WorldStockGraphicsSourceOwner actualWorldGraphics)
        {
            ArgumentNullException.ThrowIfNull(actualWorldGraphics);RequireCurrent();actualWorldGraphics.RequireCurrent();
            if(!ReferenceEquals(actualWorldGraphics.Context,Context))
                throw new InvalidDataException("Composite target lease belongs to another World attempt.");
            return new CompositeTargetLease(this,actualWorldGraphics);
        }
    }
    private void RequireCurrent(ParentBinding binding)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring&&accepted==0,this);
            if(!ReferenceEquals(current,binding)||binding.Revision!=revision||targetStack.Count!=1||
                !ReferenceEquals(targetStack.Peek(),target)||!ReferenceEquals(host.Session.CurrentFrame?.Context,binding.Context)||
                binding.Context.OutputFrame.Textures[0x064B746B].ExplicitExtent!=Extent)
                throw new InvalidDataException("Frontend parent target stack was replaced, resized or belongs to another frame.");
        }
    }
    private sealed record CompositeTargetLease(ParentBinding Binding,OriginalMapPostEffect3WorldStockGraphicsSourceOwner World)
        :IOriginalMapPostEffect3StockGraphicsTargetLease
    {
        public long FrameNumber=>Binding.FrameNumber;
        public string ContextOwnerIdentity=>World.Bank.Source.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>World.CurrentStock.OwnedFrontendIdentity;
        public string ViewIdentity=>World.CurrentStock.ViewIdentity;
        public int? ParameterIndex=>null;
        public int InstanceIndex=>0;
        public string ProducerOwnerIdentity=>Binding.EffectiveSource.OwnerIdentity;
        public Rid StorageRid=>Binding.Owner.target.StorageRid;
        public Rid TargetViewRid=>Binding.Owner.target.ShaderViewRid;
        public uint NativeViewFormat=>91;
        public RenderingDevice.DataFormat DeviceFormat=>Binding.Owner.target.TargetFormat;
        public Vector2I Extent=>Binding.Owner.Extent;
        public string SourceEvidence=>Binding.SourceEvidence+";Composite44-current-target-no-RT-setter;"+World.SourceEvidence;
        public void RequireCurrentForPass(int sourcePassIndex)
        {
            Binding.RequireCurrent();World.RequireCurrent();
            if(sourcePassIndex!=44||World.Bank.Source.Passes[44].ActiveBindings.Any(r=>r.Role==OriginalMapPostEffect3BindingRole.RenderTargetWrite))
                throw new NotSupportedException("Inherited frontend target admits the actual Composite44 no-RT-setter only.");
        }
    }

    /// <summary>Actual supplied texture/sampler/CB-side capabilities are checked
    /// against all counted pass44 rows. Manager7/8 are produced by the existing
    /// Composite Before owner; sampled-wrapper samplers remain explicit.</summary>
    public OriginalMapPostEffect3StockGraphicsFrameInputs PrepareStock44Inputs(ParentBinding binding,
        OriginalMapPostEffect3WorldStockGraphicsSourceOwner world,OriginalMapPostEffect3OwnedFreshCameraFrame parameters,
        IReadOnlyList<IOriginalMapPostEffect3ComputeTextureLease> actualTextures,
        IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> actualSampledWrapperSamplers,
        OriginalMapPostEffect3SourceSamplerOwner actualManagerSamplers,IReadOnlyDictionary<int,int> actualConstantBufferSides,
        float actualContext1C48,OriginalModelRasterInputs actualSourceRaster,string actualDrawStateEvidence)
    {
        ArgumentNullException.ThrowIfNull(parameters);ArgumentException.ThrowIfNullOrWhiteSpace(actualDrawStateEvidence);
        binding.RequireCurrent();world.RequireCurrent();parameters.RequireCurrent();
        if(!ReferenceEquals(parameters.Allocation,world.Bank.Source)||parameters.FrameNumber!=binding.FrameNumber||
            !float.IsFinite(actualContext1C48)||actualSourceRaster.Attachments.Count!=1||
            actualSourceRaster.Attachments.Any(a=>a.EnableBlend)||actualSourceRaster.DepthTest||actualSourceRaster.DepthWrite||
            actualSourceRaster.NativeStencil is not null)
            throw new NotSupportedException("Composite44 requires actual same-frame initialized bank and proved opaque single-color source raster.");
        var rows=world.Bank.Source.Passes[44].ActiveBindings;
        var textureIndices=rows.Where(r=>r.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&(r.StageMask&4)!=0)
            .Select(r=>r.ParameterIndex).Distinct().Where(i=>world.TextureParameters.ReadKnownNullablePointer(i) is not null).Order().ToArray();
        var cbIndices=rows.Where(r=>r.Role==OriginalMapPostEffect3BindingRole.ConstantBufferRead)
            .Select(r=>r.ParameterIndex).Distinct().Order().ToArray();
        if(!actualTextures.Select(t=>t.ParameterIndex).Order().SequenceEqual(textureIndices)||
            !actualConstantBufferSides.Keys.Order().SequenceEqual(cbIndices))
            throw new InvalidDataException("Composite44 texture capabilities or actual CB sides do not cover its counted source bindings.");
        foreach(var texture in actualTextures)texture.RequireCurrent();
        foreach(var sampler in actualSampledWrapperSamplers)sampler.RequireCurrent();
        var selected=world.SelectCompositeBeforeSamplers(parameters).Select(s=>actualManagerSamplers.LeaseSelected(s,2)).ToArray();
        var samplers=actualSampledWrapperSamplers.Concat(selected).ToArray();
        if(samplers.Select(s=>s.SourceRegister).Distinct().Count()!=samplers.Length)
            throw new InvalidDataException("Composite manager7/8 cannot overwrite duplicate sampled-wrapper sampler capabilities.");
        var targetLease=binding.LeaseTarget44(world);targetLease.RequireCurrentForPass(44);
        return new(binding.FrameNumber,world.CurrentStock,world.TextureParameters,Array.AsReadOnly(actualTextures.ToArray()),
            Array.AsReadOnly(samplers),new Dictionary<int,int>(actualConstantBufferSides),targetLease,
            new Rect2I(Vector2I.Zero,Extent),actualContext1C48,actualSourceRaster,
            binding.SourceEvidence+";"+actualDrawStateEvidence+";actual-Composite44-stock-inputs;no-implicit-resource-contents-or-samplers");
    }

    public async Task<OriginalMapPostEffect3StockGraphicsCompletedPass> DrawComposite44Async(ParentBinding binding,
        OriginalMapPostEffect3StockGraphicsExecutor actualExecutor,OriginalMapPostEffect3StockGraphicsFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(actualExecutor);ArgumentNullException.ThrowIfNull(input);binding.RequireCurrent();
        if(input.CurrentTarget is not CompositeTargetLease targetLease||!ReferenceEquals(targetLease.Binding,binding)||
            !ReferenceEquals(actualExecutor.Bank,targetLease.World.Bank))
            throw new InvalidDataException("Composite draw must use this exact inherited frontend target and actual bank executor.");
        lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);accepted++;}
        try
        {
            var completed=await actualExecutor.DrawAsync(44,input);binding.RequireCurrent();
            if(!ReferenceEquals(actualExecutor.LastCompletedPass,completed)||completed.FrameNumber!=binding.FrameNumber||
                completed.TargetStorageRid!=target.StorageRid||completed.TargetViewRid!=target.ShaderViewRid||completed.TargetParameterIndex is not null)
                throw new InvalidDataException("Composite44 completed a different target/frame.");
            CompletedPresentation=presentation;return completed;
        }
        finally{lock(lifetime){accepted--;if(retiring&&accepted==0)drained?.TrySetResult();}}
    }
    public ValueTask DisposeAsync()
    {
        lock(lifetime)
        {
            if(disposal is not null)return new(disposal);retiring=true;CompletedPresentation=null;
            Task wait=accepted==0?Task.CompletedTask:(drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            disposal=RetireAsync(wait);return new(disposal);
        }
    }
    private async Task RetireAsync(Task wait)
    {
        await wait;await presentation.DisposeAsync();
        await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(target.StorageRid);return true;});
    }
}
