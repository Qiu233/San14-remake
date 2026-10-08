using Godot;
using San14.Data;

public sealed partial class OriginalMapFirstWorldNativeSamplerState
{
    /// <summary>Evaluate the actual source setters on this privately accepted
    /// fresh World prefix. The original process's frontend fields are irrelevant
    /// to this independently owned source context.</summary>
    public OwnedDrawContext CaptureOwnedDrawContext(OriginalMapPostEffect3FirstWorldPreparedPrefix prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return OwnedDrawContext.Capture(this,prefix);
    }

    public sealed class OwnedDrawContext:IOriginalMapPostEffect3ShaftPass7DrawCapability
    {
        private readonly OriginalMapFirstWorldNativeSamplerState owner;
        private readonly Prefix accepted;
        private readonly OriginalMapPostEffect3FirstWorldPreparedPrefix prefix;
        private readonly OriginalDeferredDeviceBinding lastPixelSampler7;
        private readonly OriginalDeferredRasterState lastRaster;
        private readonly OriginalModelRasterInputs raster;
        private readonly IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> pixelSamplers;
        public OriginalMapPostEffect3FreshStockDrawSource Source{get;}
        private OwnedDrawContext(OriginalMapFirstWorldNativeSamplerState owner,Prefix accepted,
            OriginalMapPostEffect3FirstWorldPreparedPrefix prefix,
            OriginalMapPostEffect3FreshStockDrawSource source,OriginalDeferredDeviceBinding sampler,
            OriginalDeferredRasterState lateRaster)
        {
            (this.owner,this.accepted,this.prefix,Source,lastPixelSampler7,lastRaster)=
                (owner,accepted,prefix,source,sampler,lateRaster);
            // Blend factors and front winding cannot affect an opaque CullNONE
            // draw. Their neutral backend representation does not infer native
            // packed blend/winding fields; only effective source state is used.
            raster=new(RenderingDevice.PolygonCullMode.Disabled,
                RenderingDevice.PolygonFrontFace.CounterClockwise,false,false,
                RenderingDevice.CompareOperator.Always,Array.AsReadOnly(new[]{
                    new OriginalModelBlendAttachment(false,RenderingDevice.BlendFactor.One,
                        RenderingDevice.BlendFactor.Zero,RenderingDevice.BlendOperation.Add,
                        RenderingDevice.BlendFactor.One,RenderingDevice.BlendFactor.Zero,
                        RenderingDevice.BlendOperation.Add,true,true,true,true)}));
            pixelSamplers=Array.AsReadOnly<IOriginalMapPostEffect3ComputeSamplerLease>([new RetainedPixelSampler(this)]);
        }
        internal static OwnedDrawContext Capture(OriginalMapFirstWorldNativeSamplerState owner,
            OriginalMapPostEffect3FirstWorldPreparedPrefix prefix)
        {
            lock(owner.lifetime)
            {
                ObjectDisposedException.ThrowIf(owner.retiring,owner);
                prefix.RequireCurrent();
                if(owner.current is null&&!prefix.Result.Shaft.BeforeHookAllowsPasses)
                    owner.CaptureFirstWorldPrefix(owner.host.Scene.LastWorldDeferredSubmittedFrame??
                        throw new InvalidDataException("Source-skipped Shaft has no actual late submission."),
                        owner.host.Final.Chain.Exposure.LastPacket??throw new InvalidDataException("Source-skipped Shaft has no actual Exposure completion."));
                var accepted=owner.current??throw new InvalidDataException("Draw context needs the actual private World/Exposure prefix.");
                if(!ReferenceEquals(prefix.WorldStockScope.Bank,owner.Bank)||
                    !ReferenceEquals(prefix.Context,accepted.Submitted.Context)||prefix.Context.FrameNumber!=accepted.Submitted.Context.FrameNumber||
                    !ReferenceEquals(owner.RequireGraphicsPrefix(accepted.Submitted,owner.sky,
                        accepted.Exposure,accepted.ExposureDrawCount),accepted.SkyPrefix))
                    throw new InvalidDataException("Draw state belongs to another actual first World/Exposure/module owner.");
                var source=OriginalDeferredSamplerSources.ReadFreshPostEffect3StockDrawSource(owner.Source.Native);
                // PS383 remaps its ambient sampler to3 and never writes7.
                // The actual final source writer is the Terrain sampler7;
                // Host retains its submitted draw and proves that the following
                // Early/Aux/Late/Exposure/LightScene prefix cannot overwrite it.
                var sampler=owner.RequirePixelSampler7(accepted.Submitted,accepted.Exposure);
                var lateRaster=accepted.Submitted.RequireLastSubmittedRaster(owner.host.Session);
                if(lateRaster.Cull!=RenderingDevice.PolygonCullMode.Disabled||lateRaster.EnableBlend||
                    owner.host.Final.Chain.Exposure.PixelTranslation.SourceProgramSha256!=OriginalMapExposureSource.PixelSha256)
                    throw new NotSupportedException("Fresh source draw evaluator requires its actual CullNONE opaque F103 and stock Exposure PS.");
                var result=new OwnedDrawContext(owner,accepted,prefix,source,sampler,lateRaster);
                result.RequireCurrent();return result;
            }
        }
        public long FrameNumber=>prefix.Context.FrameNumber;
        public string ContextOwnerIdentity=>owner.Bank.Source.ContextOwnerIdentity;
        public float ActualContext1C48=>Source.FreshContext1C48;
        public OriginalModelRasterInputs ActualRaster=>raster;
        public OriginalModelRasterInputs StockRaster=>raster;
        public OriginalModelRasterInputs GhostEffectiveRaster=>raster;
        public RenderingDevice.RenderPrimitive GhostSourcePrimitive=>RenderingDevice.RenderPrimitive.TriangleStrips;
        public string SourceEvidence=>Source.SourceEvidence+";"+accepted.Submitted.SourceEvidence+
            ";actual-node1-Exposure-draw="+accepted.ExposureDrawCount+
            (prefix.InputLightSceneSource is{} light?";"+light.SourceEvidence+";actual-stock1-LightScene-copy-only-PSslot0":";actual-LightScene-source-fallback-no-dedicated-draw")+
            ";retained-actual-prefix-PSsampler7="+lastPixelSampler7.SourceResourceUri;
        public Rect2I ActualViewport
        {
            get{RequireCurrent();var target=prefix.WorldStockScope.RequirePass7Target();
                return new(Vector2I.Zero,target.Extent);}
        }
        public IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> ActualPixelSamplers=>pixelSamplers;
        public void RequireCurrent()
        {
            lock(owner.lifetime)
            {
                ObjectDisposedException.ThrowIf(owner.retiring,owner);
                prefix.RequireCurrent();prefix.WorldStockScope.RequireCurrent();
                prefix.InputLightSceneSource?.RequireCurrent();
                if(!ReferenceEquals(owner.current,accepted)||
                    !ReferenceEquals(owner.RequireGraphicsPrefix(accepted.Submitted,owner.sky,
                        accepted.Exposure,accepted.ExposureDrawCount),accepted.SkyPrefix)||
                    owner.RequirePixelSampler7(accepted.Submitted,accepted.Exposure)!=lastPixelSampler7||
                    accepted.Submitted.RequireLastSubmittedRaster(owner.host.Session)!=lastRaster)
                    throw new InvalidDataException("Owned draw source prefix or actual last late sampler/raster was replaced.");
            }
        }
        public void RequireCurrentForPass7(OriginalMapPostEffect3FirstWorldPreparedPrefix actualPrefix)
        {
            RequireCurrent();
            if(!ReferenceEquals(actualPrefix,prefix))throw new InvalidDataException("Shaft7 draw capability belongs to another exact source prefix.");
            prefix.WorldStockScope.RequirePass7Target().RequireCurrentForPass(7);
        }
        /// <summary>Composite Before creates its real manager7/8 selections.
        /// The shared source evaluator supplies only raster/z and the actual
        /// owned frontend target; textures and wrapper samplers remain typed.</summary>
        public OriginalMapPostEffect3StockGraphicsFrameInputs PrepareComposite44Inputs(
            IReadOnlyList<IOriginalMapPostEffect3ComputeTextureLease> actualTextures,
            IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> actualWrapperSamplers,
            OriginalMapPostEffect3SourceSamplerOwner actualManagerSamplers,
            IReadOnlyDictionary<int,int> actualConstantBufferSides)
        {
            RequireCurrent();var binding=prefix.FrontendParentBinding;
            return binding.Owner.PrepareStock44Inputs(binding,prefix.WorldStockScope,prefix.Parameters,
                actualTextures,actualWrapperSamplers,actualManagerSamplers,actualConstantBufferSides,
                ActualContext1C48,StockRaster,SourceEvidence+";actual-pass44-ScreenBegin-source-setters");
        }
        public OriginalMapPostEffect3LensGhostFrameInputs PrepareGhostDrawInputs(
            OriginalMapPostEffect3LensExecutionRecipe recipe,IOriginalMapPostEffect3ComputeBufferLease actualSprite,
            IOriginalMapPostEffect3ComputeTextureLease actualStarBurst,
            IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> actualBeforePixelSamplers,
            IReadOnlyList<OriginalMapPostEffect3LensDescriptorTextureInput> actualBeforeDescriptorTextures,
            int actualConstantBufferInstance,IOriginalMapPostEffect3StockGraphicsReadableTargetLease actualTarget)
        {
            RequireCurrent();actualTarget.RequireCurrentForPass(34);actualTarget.RequireReadableForPass(34);
            if(recipe.SourcePassIndex!=34||!recipe.ExecutesPass||recipe.GhostDraw is not{
                    NativePrimitiveCode:4,NativeD3DPrimitiveTopology:5,TemporaryDepthTestEnabled:false}||
                recipe.FrameNumber!=FrameNumber||recipe.ContextOwnerIdentity!=ContextOwnerIdentity||
                recipe.SourceSha256!=owner.Bank.Source.Source.Sha256||actualTarget.ParameterIndex!=41||
                actualTarget.FrameNumber!=FrameNumber||actualTarget.ContextOwnerIdentity!=ContextOwnerIdentity)
                throw new InvalidDataException("Ghost draw state needs the actual source pass34 recipe and current complete target41.");
            foreach(var sampler in actualBeforePixelSamplers)sampler.RequireCurrent();
            foreach(var texture in actualBeforeDescriptorTextures)texture.Texture.RequireCurrent();
            var target=prefix.Textures.Require(41);
            if(target.Reference.PhysicalAllocationParameterIndex!=41||target.Reference.InstanceIndex!=actualTarget.InstanceIndex||
                owner.Bank.RequireTexture(41,actualTarget.InstanceIndex).StorageRid!=actualTarget.StorageRid)
                throw new InvalidDataException("Ghost source target setter differs from the actual bank/table allocation.");
            _=owner.Bank.RequireConstantBufferForPass(6,actualConstantBufferInstance,34,FrameNumber);
            return new(prefix.Textures,actualSprite,actualStarBurst,
                Array.AsReadOnly(actualBeforePixelSamplers.ToArray()),Array.AsReadOnly(actualBeforeDescriptorTextures.ToArray()),
                actualConstantBufferInstance,actualTarget,new Rect2I(Vector2I.Zero,actualTarget.Extent),GhostSourcePrimitive,
                GhostEffectiveRaster,SourceEvidence+";actual-counted-Ghost-RT41-color-only-full-viewport;"+
                    "factory1153120-temporary-depthoff;source-opcode40031-TriangleStrip;actual-Before-manager-PS7/8");
        }
        private sealed record RetainedPixelSampler(OwnedDrawContext Owner):IOriginalMapPostEffect3ComputeSamplerLease
        {
            public long FrameNumber=>Owner.FrameNumber;
            public string ContextOwnerIdentity=>Owner.ContextOwnerIdentity;
            public int NativeStageOrdinal=>2;
            public uint SourceRegister=>7;
            public Rid SamplerRid=>Owner.lastPixelSampler7.Resource;
            public string SourceEvidence=>Owner.SourceEvidence+";actual-Terrain-PS7-submission;following-Early-Aux-PS383-no-source-PS7-write;"+
                "actual-Exposure-LightScene-only-slot0;Shaft-CS1..6-compute-stage-only;PS7-retained-until-source-Shaft7";
            public void RequireCurrent()=>Owner.RequireCurrent();
        }
    }
}
