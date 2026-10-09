using Godot;
using San14.Data;

public sealed partial class MapPostEffect3Composite2GraphicsOwner
{
    private void RequireBound(MapPostEffect3Composite2BeforeBindings before,MapPostEffect3Composite2BoundGraphics bound)
    {
        RequireCurrent();before.RequireCurrent();
        if(!ReferenceEquals(bound.Before,before)||bound.ActualNativeTargetSlots0To3.Count!=4||string.IsNullOrWhiteSpace(bound.SourceEvidence))
            throw new InvalidDataException("Composite2 Bind requires its exact Before and complete native target cache0..3.");
        RequireLuts(bound.ActualModuleLutsAfterBefore,before.ActualModuleLuts);
        var current=invocation.CaptureCurrentModuleLuts();RequireLuts(current,bound.ActualModuleLutsAfterBefore);
        ushort expected=before.NativeHdrSampler48AfterWrite??before.ActualModuleLuts.ActualHdrSampler48;
        if(bound.ActualModuleLutsAfterBefore.ActualHdrSampler48!=expected||current.ActualHdrSampler48!=expected)
            throw new InvalidDataException("Composite2 Before omitted its original current HDR sampler48 mutation/restore.");
        if(before.ClearsNativeTarget0&&bound.ActualNativeTargetSlots0To3[0] is not null)
            throw new InvalidDataException("Composite2 Before did not clear native RT0 after the directory Select.");
        var state=bound.ActualPostBindingNativeState;var context=bound.ActualPostBindingDrawContext;
        context.RequireCurrentForPass(before.SourcePassIndex);
        var vp=state.ActualViewport;
        if(state.FrameNumber!=Frame.FrameNumber||state.ContextOwnerIdentity!=Frame.OwnerIdentity||
            state.OwnedFrontendIdentity!=context.OwnedFrontendIdentity||state.ViewIdentity!=context.ViewIdentity||
            vp.Left!=context.ActualViewport.Position.X||vp.Top!=context.ActualViewport.Position.Y||
            vp.Width!=context.ActualViewport.Size.X||vp.Height!=context.ActualViewport.Size.Y||
            vp.MinimumDepth!=context.ActualViewportMinimumDepth||vp.MaximumDepth!=context.ActualViewportMaximumDepth||
            state.ActualScissor!=(context.ActualScissor is{} sc?new FinalEffectScissor(sc.Position.X,sc.Position.Y,sc.Size.X,sc.Size.Y):(FinalEffectScissor?)null)||
            string.IsNullOrWhiteSpace(state.SourceEvidence))
            throw new InvalidDataException("Composite2 source native post-Bind snapshot differs from its full current frontend capability.");
    }

    private static IReadOnlyList<MapPostEffect3FullscreenTargetBinding> ResolveActualTargets(
        MapPostEffect3Composite2BoundGraphics bound)
    {
        var slots=bound.ActualNativeTargetSlots0To3;
        var selected=slots.Select((target,index)=>(target,index)).Where(p=>p.target is not null).ToArray();
        if(selected.Length==0)
            return [new(0,bound.ActualInheritedCallerTarget??throw new InvalidDataException("An empty native target cache needs its actual inherited caller target."))];
        if(selected.Length!=1||selected[0].index!=0)
            throw new NotSupportedException("Composite2 current nonempty RT1..3 topology requires its original MRT adapter; clearing RT0 cannot imply caller inheritance.");
        return [new(0,selected[0].target!)];
    }

    private static void RequireSamplers(MapPostEffect3Composite2BeforeBindings before,
        MapPostEffect3Composite2ModuleLuts actualLuts,IReadOnlyList<MapPostEffect3Composite2SamplerRegister> registers,
        IReadOnlyList<IMapPostEffect3ComputeSamplerLease> leases)
    {
        if(!registers.Select(r=>(r.NativeStageOrdinal,r.SourceRegister)).Order().SequenceEqual(
            leases.Select(l=>(l.NativeStageOrdinal,l.SourceRegister)).Order()))
            throw new InvalidDataException("Composite2 actual sampler leases differ from its original shader declarations.");
        foreach(var lease in leases)
        {
            lease.RequireCurrent();
            if(lease.FrameNumber!=before.Frame.FrameNumber||lease.ContextOwnerIdentity!=before.Frame.OwnerIdentity)
                throw new InvalidDataException("Composite2 sampler belongs to another actual invocation.");
            if(lease.NativeStageOrdinal!=2||lease.SourceRegister is not(7 or 8))continue;
            if(lease is not IMapPostEffect3Composite2ModuleLutSamplerLease module)
                throw new InvalidDataException("Composite2 PS7/8 requires its actual module LUT Before setter capability.");
            module.RequireSelectedBeforeSetter(before);
            var wrapper=lease.SourceRegister==7?actualLuts.ActualHdrWrapper30:actualLuts.ActualLdrWrapper38;
            if(module.ActualModuleWrapper!=wrapper||lease.SourceRegister==7&&module.ActualWrapperSampler48!=actualLuts.ActualHdrSampler48||
                wrapper is null&&module.ActualWrapperSampler48 is not null)
                throw new InvalidDataException("Composite2 sampler was inferred from a counted LUT texture slot instead of its actual module wrapper.");
        }
    }

    private async Task<IReadOnlyList<IMapPostEffect3ComputeTextureLease>> CaptureTextureReadsAsync(
        MapPostEffect3Composite2BeforeBindings before,MapPostEffect3CompiledPass compiled)
    {
        MapPostEffect3Composite2ReadFootprint? proof=before.SourcePassIndex<=108?
            MapPostEffect3Composite2ReadFootprint.Prepare(Bank.Source,Frame.Prepared,before.SourcePassIndex):null;
        var leases=new List<IMapPostEffect3ComputeTextureLease>();
        foreach(var stage in compiled.Stages)
        foreach(var declared in stage.Bindings.Bindings.Where(b=>b.DescriptorKind=="Texture"))
        {
            if(stage.Source.StageCode!=0)throw new NotSupportedException("Composite2 selected graphics has an unclosed vertex texture read.");
            var binding=Bank.Source.Passes[before.SourcePassIndex].ActiveBindings.Last(b=>
                b.Table==MapPostEffect3BindingTable.TextureParameters&&b.Role==MapPostEffect3BindingRole.ShaderResourceRead&&
                (b.StageMask&4)!=0&&b.RegisterIndex==declared.Source.BindPoint);
            var pointer=TextureParameters.Require(binding.ParameterIndex);
            var footprint=proof?.ShaderReads.Single(r=>r.SourceRegister==declared.Source.BindPoint);
            if(proof is null&&(before.SourcePassIndex is <109 or >116||binding.ParameterIndex!=84||binding.RegisterIndex!=0||
                declared.Source.Dimension!=4))
                throw new NotSupportedException("Composite2 FXAA read needs its exact original84/t0 Texture2D producer.");
            IMapPostEffect3ComputeTextureLease actual;
            if(binding.ParameterIndex==61&&proof is not null&&
                invocation is IMapPostEffect3Composite2RetainedExposureInvocationLease retained&&retained.RequiresRetainedExposure(before))
                actual=retained.CaptureRetainedExposure(before,proof);
            else if(binding.ParameterIndex==61&&footprint is{RequiresInitializedContents:true}&&
                invocation is IMapPostEffect3Composite2FreshExposureInvocationLease fresh&&fresh.RequiresFreshUndefinedExposure(before))
                actual=fresh.CaptureFreshUndefinedExposure(before,proof!);
            else if(pointer.Reference.PhysicalAllocationParameterIndex is not null)
                actual=footprint is{RequiresInitializedContents:false}?
                    Bank.CaptureComposite2IndependentTextureRead(TextureParameters,before,proof!,footprint):
                    await Bank.CaptureComposite2TextureReadLeaseAsync(TextureParameters,before,binding);
            else actual=await invocation.CaptureCurrentExternalTextureReadAsync(before,binding);
            actual.RequireCurrent();
            if(actual.ParameterIndex!=binding.ParameterIndex||actual.FrameNumber!=Frame.FrameNumber||
                actual.ContextOwnerIdentity!=Frame.OwnerIdentity||actual.InstanceIndex!=pointer.Reference.InstanceIndex)
                throw new InvalidDataException("Composite2 shader read differs from its actual copied wrapper/side.");
            leases.Add(new ReadCapability(this,before,actual,pointer,stage.Source.Sha256,proof,footprint));
        }
        return Array.AsReadOnly(leases.ToArray());
    }

    private sealed record ExecuteContext(IMapPostEffect3FullscreenDrawContextLease Actual,
        MapPostEffect3Composite2GraphicsRecipe Recipe):IMapPostEffect3FullscreenDrawContextLease
    {
        public long FrameNumber=>Actual.FrameNumber;public string ContextOwnerIdentity=>Actual.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>Actual.OwnedFrontendIdentity;public string ViewIdentity=>Actual.ViewIdentity;
        public Rect2I ActualViewport=>Actual.ActualViewport;public float ActualViewportMinimumDepth=>Actual.ActualViewportMinimumDepth;
        public float ActualViewportMaximumDepth=>Actual.ActualViewportMaximumDepth;
        public Rect2I? ActualScissor=>Recipe.DrawScissor is{} sc?new(sc.Left,sc.Top,sc.Width,sc.Height):null;
        public ModelRasterInputs ActualRaster
        {
            get
            {
                var snapshot=MapFinalOutputRasterSnapshot.Capture(Actual.ActualRaster);
                if(!Recipe.Before.ChangesBlend)return snapshot;
                if(Recipe.DrawNativeColorStateF4!=0x111100u||Recipe.DrawNativeRt0BlendEnable1C0!=1)
                    throw new NotSupportedException("Composite2 temporary blend needs its original111100/RT0-enable1 recipe.");
                return snapshot with{Attachments=Array.AsReadOnly(snapshot.Attachments.Select((a,index)=>a with{
                    EnableBlend=index==0||a.EnableBlend,SourceColor=RenderingDevice.BlendFactor.One,
                    DestinationColor=RenderingDevice.BlendFactor.One,ColorOperation=RenderingDevice.BlendOperation.Add,
                    SourceAlpha=RenderingDevice.BlendFactor.One,DestinationAlpha=RenderingDevice.BlendFactor.One,
                    AlphaOperation=RenderingDevice.BlendOperation.Add}).ToArray())};
            }
        }
        public string SourceEvidence=>Actual.SourceEvidence+";"+Recipe.Before.SourceEvidence+
            ";actual-Execute-scissor-delta;full-raster-preserved;local-draw-state-restores-actual-prior-snapshot";
        public void RequireCurrentForPass(int pass)
        {Actual.RequireCurrentForPass(pass);Recipe.RequireCurrent();if(pass!=Recipe.Before.SourcePassIndex)
            throw new InvalidDataException("Composite2 Execute context belongs to another actual selected native pass.");}
    }

    private sealed record ReadCapability(MapPostEffect3Composite2GraphicsOwner Owner,
        MapPostEffect3Composite2BeforeBindings Before,IMapPostEffect3ComputeTextureLease Actual,
        MapPostEffect3ResolvedTextureParameter Pointer,string PixelSha,
        MapPostEffect3Composite2ReadFootprint? Proof,MapPostEffect3ShaderReadFootprint? Footprint)
        :IMapPostEffect3SourceTextureReadFootprintLease,IMapPostEffect3ComputeExternalTextureLease,
        IMapPostEffect3ComputeSelectedMipTextureLease,IMapPostEffect3SharedSampledViewLease
    {
        public long FrameNumber=>Actual.FrameNumber;public string ContextOwnerIdentity=>Actual.ContextOwnerIdentity;
        public int ParameterIndex=>Actual.ParameterIndex;public int InstanceIndex=>Actual.InstanceIndex;
        public Rid StorageRid=>Actual.StorageRid;public Rid ShaderViewRid=>Actual.ShaderViewRid;
        public uint NativeViewFormat=>Actual.NativeViewFormat;public RenderingDevice.DataFormat DeviceFormat=>Actual.DeviceFormat;
        public Vector3I Extent=>Actual.Extent;
        public string ProducerOwnerIdentity=>(Actual as IMapPostEffect3ComputeExternalTextureLease)?.ProducerOwnerIdentity??Pointer.Reference.ProducerOwnerIdentity;
        public bool IsSourceSelectedMipView=>Actual is IMapPostEffect3ComputeSelectedMipTextureLease{IsSourceSelectedMipView:true};
        public uint NativeFirstMipLevel=>(Actual as IMapPostEffect3ComputeSelectedMipTextureLease)?.NativeFirstMipLevel??0;
        public uint NativeMipLevelCount=>(Actual as IMapPostEffect3ComputeSelectedMipTextureLease)?.NativeMipLevelCount??1;
        public string NativeMipSelectionEvidence=>(Actual as IMapPostEffect3ComputeSelectedMipTextureLease)?.NativeMipSelectionEvidence??"actual full native wrapper";
        public string SourceEvidence=>Actual.SourceEvidence+";"+(Proof?.SourceEvidence??"original-FXAA84/t0-conservative-entire-single-native-mip")+
            ";same-frame-CB22-and-current-source-setter-join;no-additional-pixel-initialization-claim";
        public void RequireCurrent(){Owner.RequireCurrent();Before.RequireCurrent();Owner.TextureParameters.ValidateCurrent(Pointer);Actual.RequireCurrent();Proof?.RequireCurrent();}
        public void RequireSourceSelectedView(int pass,uint register)
        {RequireCurrent();if(Actual is IMapPostEffect3ComputeSelectedMipTextureLease selected)selected.RequireSourceSelectedView(pass,register);}
        public void RequireReadableMipRange(uint first,uint count)
        {RequireCurrent();if(Actual is IMapPostEffect3ComputeMipTextureLease mip)mip.RequireReadableMipRange(first,count);
         else if(first!=0||count!=1)throw new InvalidDataException("Composite2 actual non-mip producer cannot authorize another physical mip range.");}
        public MapPostEffect3SharedSampledViewProof RequireSharedSampledViewProof()=>
            (Actual as IMapPostEffect3SharedSampledViewLease??throw new InvalidDataException("Composite2 read has no original shared native view proof.")).RequireSharedSampledViewProof();
        public void RequireReadableSourceFootprint(int pass,uint register,string shader,
            IReadOnlyList<MapPostEffect3ComputeConstantBufferReadReceipt> cbs)
        {
            RequireCurrent();
            if(pass!=Before.SourcePassIndex||shader!=PixelSha||register!=(Footprint?.SourceRegister??0)||
                cbs.Single(r=>r.ParameterIndex==22).CompleteBytesSha256!=Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(Owner.Frame.Prepared.ConstantBuffer.Bytes.Span)))
                throw new InvalidDataException("Composite2 footprint differs from its exact submitted original shader/CB22/setter.");
            if(Footprint is{RequiresInitializedContents:false})return;
            if(Actual is IMapPostEffect3SourceTextureReadFootprintLease exact)
            {exact.RequireReadableSourceFootprint(pass,register,shader,cbs);return;}
            uint mip=IsSourceSelectedMipView?NativeFirstMipLevel:0;
            if(Actual is IMapPostEffect3ComputeMipTextureLease readable)readable.RequireReadableMipRange(mip,1);
            // Ordinary imported/external producers' RequireCurrent contract
            // proves their completed contents. No allocated-only lease can
            // enter this branch through this owner's bank-independent gate.
        }
    }
}
