using Godot;
using San14.Data;

public sealed partial class MapPostEffect3DepthOfField2ExecutionOwner
{
    private MapPostEffect3DepthOfField2TargetView CaptureTargetView(
        MapPostEffect3DepthOfField2BeforeBindings before,int register,MapPostEffect3ResolvedTextureParameter pointer)
    {
        var proof=MapPostEffect3DepthOfField2GraphicsSource.Prepare(State,Bank.Source,TextureParameters,Prepared,before);
        if(pointer.Reference.PhysicalAllocationParameterIndex is not null)
            return Bank.ReadDepthOfField2TargetView(TextureParameters,proof,register,pointer);
        if(pointer.ParameterIndex==58&&ownedShape is not null)return ShapeView(register,pointer);
        var view=invocation.CaptureCurrentExternalTargetView(before,register,pointer);
        if(view.SourceTargetRegister!=register||view.ActualPointer!=pointer||!view.StorageRid.IsValid||!view.TargetViewRid.IsValid||
            view.Extent.X<=0||view.Extent.Y<=0||string.IsNullOrWhiteSpace(view.SourceEvidence))
            throw new InvalidDataException("DOF2 external target metadata differs from its actual current source pointer/register.");
        return view;
    }

    private IMapPostEffect3StockGraphicsTargetLease CaptureTarget(
        MapPostEffect3DepthOfField2BeforeBindings before,MapPostEffect3DepthOfField2TargetView view,
        IMapPostEffect3FullscreenDrawContextLease context)
    {
        var proof=MapPostEffect3DepthOfField2GraphicsSource.Prepare(State,Bank.Source,TextureParameters,Prepared,before);
        if(view.ActualPointer.Reference.PhysicalAllocationParameterIndex is not null)
            return Bank.CaptureDepthOfField2Target(TextureParameters,proof,view,context);
        if(view.ActualPointer.ParameterIndex==58&&ownedShape is not null)return new ShapeTarget(this,before,view,context);
        var actual=invocation.CaptureCurrentExternalTarget(before,view,context);
        actual.RequireCurrentForPass(before.SourcePassIndex);
        if(actual is not IMapPostEffect3FullscreenExternalWritableTargetLease||
            actual.FrameNumber!=Prepared.FrameNumber||actual.ContextOwnerIdentity!=State.OwnerIdentity||
            actual.OwnedFrontendIdentity!=context.OwnedFrontendIdentity||actual.ViewIdentity!=context.ViewIdentity||
            actual.ParameterIndex!=view.ActualPointer.ParameterIndex||actual.InstanceIndex!=view.ActualPointer.Reference.InstanceIndex||
            actual.ProducerOwnerIdentity!=view.ActualPointer.Reference.ProducerOwnerIdentity||
            actual.StorageRid!=view.StorageRid||actual.TargetViewRid!=view.TargetViewRid||actual.NativeViewFormat!=view.NativeViewFormat||
            actual.DeviceFormat!=view.DeviceFormat||actual.Extent!=view.Extent)
            throw new InvalidDataException("DOF2 external target lacks its actual writer invalidation or post-Bind current view join.");
        return actual;
    }

    private async Task<IReadOnlyList<IMapPostEffect3ComputeTextureLease>> CaptureReadsAsync(
        MapPostEffect3DepthOfField2GraphicsSource proof,MapPostEffect3CompiledPass compiled)
    {
        var reads=new List<IMapPostEffect3ComputeTextureLease>();
        foreach(var stage in compiled.Stages)
        foreach(var descriptor in stage.Bindings.Bindings.Where(d=>d.DescriptorKind=="Texture"))
        {
            if(stage.Source.StageCode!=0||descriptor.Source.Dimension!=4)
                throw new NotSupportedException("DOF2 ordinary proof requires its actual pixel Texture2D source reads.");
            var row=proof.EffectivePixelTextureReads.Single(r=>r.RegisterIndex==descriptor.Source.BindPoint);
            var pointer=TextureParameters.Require(row.ParameterIndex);bool allocatedOnly=false,independentExposure=false;
            IMapPostEffect3ComputeTextureLease actual;
            if(pointer.Reference.PhysicalAllocationParameterIndex is not null)
            {
                if(proof.AllowsAllocatedOnlyUnusedHistorySource)
                {actual=Bank.CaptureDepthOfField2UnusedHistoryRead(TextureParameters,proof,row);allocatedOnly=true;}
                else if(row.ParameterIndex==61&&row.RegisterIndex==2&&proof.AllowsUninitializedExposureLiteralRead)
                {actual=Bank.CaptureDepthOfField2IndependentExposureRead(TextureParameters,proof,row);independentExposure=true;}
                else if(row.ParameterIndex==61&&row.RegisterIndex==2&&proof.ExposureTexelValueAffectsOutput&&
                    invocation is IMapPostEffect3DepthOfField2RetainedExposureInvocationLease {HasRetainedExposure:true} retainedExposure)
                    actual=retainedExposure.CaptureRetainedExposure(proof);
                else if(row.ParameterIndex==61&&row.RegisterIndex==2&&proof.ExposureTexelValueAffectsOutput&&
                    invocation is IMapPostEffect3DepthOfField2FreshExposureInvocationLease firstExposure)
                    actual=Bank.CaptureDepthOfField2FreshUndefinedExposureRead(TextureParameters,
                        firstExposure.CaptureFreshUndefinedExposureSource(proof));
                else actual=Bank.CaptureSourceWrittenTextureAliasLease(TextureParameters,row.ParameterIndex,Prepared.FrameNumber,0,1);
            }
            else if(row.ParameterIndex==58&&ownedShape is not null)actual=CaptureShapeRead(pointer);
            else if(row.ParameterIndex==58&&retainedShape is not null)actual=retainedShape;
            else actual=await invocation.CaptureCurrentExternalTextureReadAsync(proof.Before,row);
            actual.RequireCurrent();TextureParameters.ValidateCurrent(pointer);
            if(actual.ParameterIndex!=row.ParameterIndex||actual.InstanceIndex!=pointer.Reference.InstanceIndex||
                actual.FrameNumber!=Prepared.FrameNumber||actual.ContextOwnerIdentity!=State.OwnerIdentity||
                pointer.Reference.HasSourceSelectedMipView)
                throw new InvalidDataException("DOF2 read differs from its actual current logical wrapper/side/full native mip.");
            reads.Add(new ReadCapability(this,proof,Array.AsReadOnly(new[]{row}),actual,pointer,allocatedOnly,independentExposure));
        }
        // The executor has one physical capability per logical parameter.
        // Actual67 reads current60 at both t0 and final t1: retain both exact
        // register proofs on that capability rather than submit duplicate IDs.
        return Array.AsReadOnly(reads.OfType<ReadCapability>().GroupBy(r=>r.ParameterIndex).Select(group=>
        {
            var same=group.ToArray();var first=same[0];
            if(same.Length>1&&(proof.SourcePassIndex!=67||first.ParameterIndex!=60||same.Length!=2||
                same.Any(r=>r.InstanceIndex!=first.InstanceIndex||r.StorageRid!=first.StorageRid||r.ShaderViewRid!=first.ShaderViewRid||
                    r.NativeViewFormat!=first.NativeViewFormat||r.DeviceFormat!=first.DeviceFormat||r.Extent!=first.Extent||r.Pointer!=first.Pointer)||
                !same.SelectMany(r=>r.Rows).Select(r=>(int)r.RegisterIndex).Order().SequenceEqual([0,1])))
                throw new InvalidDataException("DOF2 multiple registers do not have the exact original67/current60 resource identity.");
            return (IMapPostEffect3ComputeTextureLease)(first with{Rows=Array.AsReadOnly(same.SelectMany(r=>r.Rows).ToArray())});
        }).ToArray());
    }

    private static void RequireSamplers(MapPostEffect3DepthOfField2BeforeBindings before,
        IReadOnlyList<MapPostEffect3DepthOfField2SamplerRegister> registers,
        IReadOnlyList<IMapPostEffect3ComputeSamplerLease> actual)
    {
        if(!registers.Select(r=>(r.NativeStageOrdinal,r.SourceRegister)).Order().SequenceEqual(
            actual.Select(r=>(r.NativeStageOrdinal,r.SourceRegister)).Order()))
            throw new InvalidDataException("DOF2 current samplers differ from its actual original shader declarations.");
        foreach(var sampler in actual)
        {
            sampler.RequireCurrent();
            if(sampler.FrameNumber!=before.FrameNumber||sampler.ContextOwnerIdentity!=before.ContextOwnerIdentity||
                sampler is not IMapPostEffect3DepthOfField2ManagerSamplerLease manager||
                sampler.NativeStageOrdinal!=2||!before.PixelSamplerRegisters.Contains(checked((int)sampler.SourceRegister))||
                manager.ManagerKind!=(sampler.SourceRegister==7?MapPostEffect3ManagerSamplerKind.Linear:MapPostEffect3ManagerSamplerKind.Point))
                throw new InvalidDataException("DOF2 sampler needs its actual source Before manager-carrier PS7/8 selection.");
            manager.RequireSelectedBeforeSetter(before);
        }
    }

    private sealed class ExecuteContext:IMapPostEffect3FullscreenDrawContextLease
    {
        private readonly MapPostEffect3DepthOfField2ExecutionOwner owner;
        private readonly MapPostEffect3DepthOfField2GraphicsSource proof;
        private readonly IMapPostEffect3FullscreenDrawContextLease actual;
        private readonly ModelRasterInputs raster;
        private readonly Rect2I? scissor;
        internal ExecuteContext(MapPostEffect3DepthOfField2ExecutionOwner owner,MapPostEffect3DepthOfField2GraphicsSource proof,
            IMapPostEffect3FullscreenDrawContextLease actual,Vector2I target0Extent)
        {
            this.owner=owner;this.proof=proof;this.actual=actual;
            actual.RequireCurrentForPass(proof.SourcePassIndex);
            var previous=actual.ActualScissor is{} old?new FinalEffectScissor(old.Position.X,old.Position.Y,old.Size.X,old.Size.Y):(FinalEffectScissor?)null;
            var native=proof.ReadExecuteScissor(target0Extent.X,target0Extent.Y,previous,owner.State);
            scissor=native is{} s?new Rect2I(s.Left,s.Top,s.Width,s.Height):null;
            var snapshot=MapFinalOutputRasterSnapshot.Capture(actual.ActualRaster);
            if(proof.Before.RequiresTemporaryBlendOverride)
            {
                if(proof.Before.TemporaryNativeColorStateF4!=0x441100u||proof.Before.TemporaryNativeRt0BlendEnable1C0!=1)
                    throw new NotSupportedException("DOF2 ordinary composite requires its exact native441100/RT0 blend-enable1 writer.");
                snapshot=snapshot with{Attachments=Array.AsReadOnly(snapshot.Attachments.Select((a,index)=>a with{
                    EnableBlend=index==0||a.EnableBlend,SourceColor=RenderingDevice.BlendFactor.One,
                    DestinationColor=RenderingDevice.BlendFactor.SrcAlpha,ColorOperation=RenderingDevice.BlendOperation.Add,
                    SourceAlpha=RenderingDevice.BlendFactor.One,DestinationAlpha=RenderingDevice.BlendFactor.SrcAlpha,
                    AlphaOperation=RenderingDevice.BlendOperation.Add}).ToArray())};
            }
            raster=snapshot;
        }
        public long FrameNumber=>actual.FrameNumber;public string ContextOwnerIdentity=>actual.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>actual.OwnedFrontendIdentity;public string ViewIdentity=>actual.ViewIdentity;
        public Rect2I ActualViewport=>actual.ActualViewport;public float ActualViewportMinimumDepth=>actual.ActualViewportMinimumDepth;
        public float ActualViewportMaximumDepth=>actual.ActualViewportMaximumDepth;
        public Rect2I? ActualScissor=>scissor;public ModelRasterInputs ActualRaster=>raster;
        public string SourceEvidence=>actual.SourceEvidence+";"+proof.SourceEvidence+";dof-scissor-and-blend-delta;full-prior-raster-preserved/local-restoration";
        public void RequireCurrentForPass(int pass)
        {owner.RequireCurrent();proof.RequireCurrent();actual.RequireCurrentForPass(pass);
         if(pass!=proof.SourcePassIndex||FrameNumber!=proof.FrameNumber||ContextOwnerIdentity!=proof.ContextOwnerIdentity)
             throw new InvalidDataException("DOF2 Execute requires the exact actual post-Bind current draw context.");}
    }

    private sealed record ReadCapability(MapPostEffect3DepthOfField2ExecutionOwner Owner,
        MapPostEffect3DepthOfField2GraphicsSource Proof,IReadOnlyList<MapPostEffect3Binding> Rows,
        IMapPostEffect3ComputeTextureLease Actual,MapPostEffect3ResolvedTextureParameter Pointer,
        bool AllocatedOnlyUnusedHistory,bool IndependentExposure):IMapPostEffect3SourceTextureReadFootprintLease,
        IMapPostEffect3ComputeExternalTextureLease,IMapPostEffect3SharedSampledViewLease
    {
        public long FrameNumber=>Actual.FrameNumber;public string ContextOwnerIdentity=>Actual.ContextOwnerIdentity;
        public int ParameterIndex=>Actual.ParameterIndex;public int InstanceIndex=>Actual.InstanceIndex;
        public Rid StorageRid=>Actual.StorageRid;public Rid ShaderViewRid=>Actual.ShaderViewRid;
        public uint NativeViewFormat=>Actual.NativeViewFormat;public RenderingDevice.DataFormat DeviceFormat=>Actual.DeviceFormat;
        public Vector3I Extent=>Actual.Extent;
        public string ProducerOwnerIdentity=>(Actual as IMapPostEffect3ComputeExternalTextureLease)?.ProducerOwnerIdentity??Pointer.Reference.ProducerOwnerIdentity;
        public string SourceEvidence=>Actual.SourceEvidence+";"+Proof.SourceEvidence+
            (AllocatedOnlyUnusedHistory?";original-read-retained/no-selected-color-consumer/no-complete-history-claim":
             IndependentExposure?";original-literal-exposure-load-retained/actual-CB18-independent-branch/no-exposure-readiness-claim":
             Actual is MapPostEffect3DepthOfField2FreshUndefinedExposureRead?";original-auto-texel0-value-dependency/source-undefined-first-use/no-exposure-readiness-claim":
             ";conservative-entire-actual-single-native-mip-readable");
        public void RequireCurrent(){Owner.RequireCurrent();Proof.RequireCurrent();Owner.TextureParameters.ValidateCurrent(Pointer);Actual.RequireCurrent();}
        public MapPostEffect3SharedSampledViewProof RequireSharedSampledViewProof()=>
            (Actual as IMapPostEffect3SharedSampledViewLease??throw new InvalidDataException("DOF2 source has no shared native sampled view proof.")).RequireSharedSampledViewProof();
        public void RequireReadableSourceFootprint(int pass,uint register,string shader,IReadOnlyList<MapPostEffect3ComputeConstantBufferReadReceipt> cbs)
        {
            RequireCurrent();
            if(pass!=Proof.SourcePassIndex||shader!=Proof.PixelProgramSha256||!Rows.Any(row=>row.RegisterIndex==register)||
                Proof.RequiredConstantBufferBytesSha256.Any(expected=>!cbs.Any(c=>c.ParameterIndex==expected.Key&&
                    c.HasCompleteWriterContents&&c.CompleteBytesSha256==expected.Value)))
                throw new InvalidDataException("DOF2 read footprint differs from its actual original shader, setter or CB18 writer.");
            if(AllocatedOnlyUnusedHistory)
            {if(!Proof.AllowsAllocatedOnlyUnusedHistorySource)throw new InvalidDataException("DOF2 unused-history source became an actual color consumer.");return;}
            if(IndependentExposure)
            {if(!Proof.AllowsUninitializedExposureLiteralRead)throw new InvalidDataException("DOF2 exposure load became a value dependency.");return;}
            if(Actual is MapPostEffect3DepthOfField2FreshUndefinedExposureRead fresh)
            {fresh.RequireConsumer(Proof,pass,register,shader,cbs);return;}
            if(Actual is IMapPostEffect3ComputeMipTextureLease mip)mip.RequireReadableMipRange(0,1);
            // External ordinary producers' RequireCurrent proves their pixels.
            // The only allocated-only bank lease has the separate branch above.
        }
    }
}
