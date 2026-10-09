using Godot;
using San14.Data;

public sealed partial class MapPostEffect3FullscreenGraphicsExecutor
{
    private static object?[] TextureKey(IMapPostEffect3ComputeTextureLease t)=>
        [t.FrameNumber,t.ContextOwnerIdentity,t.ParameterIndex,t.InstanceIndex,t.ShaderViewRid,t.StorageRid,t.NativeViewFormat,
         t.DeviceFormat,t.Extent,t.SourceEvidence,(t as IMapPostEffect3ComputeExternalTextureLease)?.ProducerOwnerIdentity,
         (t as IMapPostEffect3ComputeSelectedMipTextureLease)?.IsSourceSelectedMipView,
         (t as IMapPostEffect3ComputeSelectedMipTextureLease)?.NativeFirstMipLevel,
         (t as IMapPostEffect3ComputeSelectedMipTextureLease)?.NativeMipLevelCount,
         (t as IMapPostEffect3ComputeSelectedMipTextureLease)?.NativeMipSelectionEvidence];
    private static object?[] TargetKey(IMapPostEffect3StockGraphicsTargetLease t)=>
        [t.FrameNumber,t.ContextOwnerIdentity,t.OwnedFrontendIdentity,t.ViewIdentity,t.ParameterIndex,t.InstanceIndex,
         t.ProducerOwnerIdentity,t.StorageRid,t.TargetViewRid,t.NativeViewFormat,t.DeviceFormat,t.Extent,t.SourceEvidence,
         (t as IMapPostEffect3FullscreenSelectedMipTargetLease)?.NativeFirstMipLevel,
         (t as IMapPostEffect3FullscreenSelectedMipTargetLease)?.NativeMipLevelCount,
         (t as IMapPostEffect3FullscreenSelectedMipTargetLease)?.NativeMipSelectionEvidence];
    private static object?[] SamplerKey(IMapPostEffect3ComputeSamplerLease s)=>
        [s.FrameNumber,s.ContextOwnerIdentity,s.NativeStageOrdinal,s.SourceRegister,s.SamplerRid,s.SourceEvidence];
    private static object?[] BufferKey(IMapPostEffect3ComputeBufferLease b)=>
        [b.FrameNumber,b.ContextOwnerIdentity,b.ParameterIndex,b.InstanceIndex,b.StorageRid,b.ShaderReadRid,b.DeviceTexelFormat,
         b.ByteCount,b.UploadRevision,b.SourceEvidence];
    private static bool SameRaster(ModelRasterInputs a,ModelRasterInputs b)=>
        a.Cull==b.Cull&&a.FrontFace==b.FrontFace&&a.DepthTest==b.DepthTest&&a.DepthWrite==b.DepthWrite&&
        a.DepthCompare==b.DepthCompare&&a.NativeStencil==b.NativeStencil&&a.Attachments.SequenceEqual(b.Attachments);

    private static void RequireBeforeOverrides(MapPostEffect3FullscreenSourceDraw draw,
        IReadOnlyList<MapPostEffect3FullscreenTextureOverride> setters)
    {
        if(draw.EyeBefore is{} eye)
        {
            if(eye.RequiresDynamicPixelTextureBinding)
            {
                if(setters is not [{SourceSetter.RegisterIndex:0}]||setters[0].Setter!=eye.TextureSetter)
                    throw new InvalidDataException("Actual Eye iteration requires exactly its typed Before t0 setter.");
            }
            else if(setters.Count!=0)throw new InvalidDataException("Eye iteration has no source dynamic t0 setter.");
        }
        else if(draw.DofBefore is{} dof)
        {
            if(!dof.OrderedPixelTextureSetters.GroupBy(s=>s.NativeRegisterIndex).Select(g=>g.Last())
                .Select(s=>(s.NativeRegisterIndex,s.ActualTexture.ParameterIndex))
                .SequenceEqual(setters.Select(s=>((int)s.SourceSetter.RegisterIndex,s.SourceSetter.ParameterIndex))))
                throw new InvalidDataException("DOF2 fullscreen Before setters differ from the actual ordered factory receipt.");
        }
        else if(setters.Count!=0)throw new InvalidDataException("Selected source factory has no admitted dynamic texture setter.");
    }

    private void RequireTextureOwner(IMapPostEffect3ComputeTextureLease lease,
        MapPostEffect3ResolvedTextureParameter actual)
    {
        var reference=actual.Reference;
        if(reference.InstanceIndex!=lease.InstanceIndex)throw new InvalidDataException("Fullscreen source texture points at another physical side.");
        if(reference.PhysicalAllocationParameterIndex is{} physical)
        {
            var owned=Bank.RequireTexture(physical,lease.InstanceIndex);
            if(owned.StorageRid!=lease.StorageRid||!owned.NativeFormatViews.TryGetValue(lease.NativeViewFormat,out var view)||
                view!=lease.ShaderViewRid&&lease is not IMapPostEffect3ComputeSelectedMipTextureLease{IsSourceSelectedMipView:true})
                throw new InvalidDataException("Fullscreen source texture is not its actual bank allocation and native view.");
            if(reference.HasSourceSelectedMipView&&
                (lease is not IMapPostEffect3ComputeSelectedMipTextureLease{IsSourceSelectedMipView:true} selected||
                 selected.NativeFirstMipLevel!=reference.NativeFirstMipLevel||selected.NativeMipLevelCount!=reference.NativeMipLevelCount))
                throw new InvalidDataException("Fullscreen current pointer's selected native mip is not preserved.");
        }
        else if(lease is not IMapPostEffect3ComputeExternalTextureLease external||
            external.ProducerOwnerIdentity!=reference.ProducerOwnerIdentity)
            throw new InvalidDataException("Fullscreen external texture differs from its exact current native wrapper producer.");
    }

    private void RequireTargets(int pass,Accepted actual)
    {
        var input=actual.Input;var context=input.CurrentDrawContext;var source=input.SourceDraw;var targets=input.CurrentTargets;
        if(targets.Count==0||targets.Count>4||!targets.Select(t=>t.SourceTargetRegister).SequenceEqual(Enumerable.Range(0,targets.Count))||
            actual.Raster.Attachments.Count!=targets.Count||actual.Viewport.Size.X<=0||actual.Viewport.Size.Y<=0||
            actual.Viewport.Position.X<0||actual.Viewport.Position.Y<0||
            context.ActualViewportMinimumDepth!=0f||context.ActualViewportMaximumDepth!=1f)
            throw new NotSupportedException("Fullscreen requires its actual contiguous color targets and admitted native viewport/depth range.");
        var native=actual.EffectiveBindings.Where(b=>b.Role==MapPostEffect3BindingRole.RenderTargetWrite)
            .GroupBy(b=>b.RegisterIndex).Select(g=>g.Last()).ToArray();
        if(source.Composite2Recipe is{} composite)
        {
            composite.RequireCurrent();
            if(composite.Before.SourcePassIndex!=pass||composite.ActualEffectiveTargetCount!=(source.UsesInheritedCallerTarget?0:targets.Count))
                throw new InvalidDataException("Composite2 source command lost its complete effective target topology.");
            var vp=composite.DrawViewport;
            if(actual.Viewport!=new Rect2I(checked((int)vp.Left),checked((int)vp.Top),checked((int)vp.Width),checked((int)vp.Height))||
                context.ActualViewportMinimumDepth!=vp.MinimumDepth||context.ActualViewportMaximumDepth!=vp.MaximumDepth||
                actual.Scissor!=(composite.DrawScissor is{} sc?new Rect2I(sc.Left,sc.Top,sc.Width,sc.Height):(Rect2I?)null))
                throw new InvalidDataException("Composite2 current draw omitted its actual native viewport/scissor Execute recipe.");
            if(composite.Before.ChangesBlend)
            {
                var a=actual.Raster.Attachments[0];
                if(composite.DrawNativeColorStateF4!=0x111100u||composite.DrawNativeRt0BlendEnable1C0!=1||!a.EnableBlend||
                    a.SourceColor!=RenderingDevice.BlendFactor.One||a.DestinationColor!=RenderingDevice.BlendFactor.One||
                    a.ColorOperation!=RenderingDevice.BlendOperation.Add||a.SourceAlpha!=RenderingDevice.BlendFactor.One||
                    a.DestinationAlpha!=RenderingDevice.BlendFactor.One||a.AlphaOperation!=RenderingDevice.BlendOperation.Add)
                    throw new InvalidDataException("Composite2 AddBloom requires its exact original111100 additive RGB/alpha blend formula.");
            }
        }
        if(source.EyeBefore is{RequiresTemporaryBlendOverride:true})
        {
            var a=actual.Raster.Attachments[0];
            // The packed111100 blend state expands to:
            // D3D RGB/alpha ONE+ONE and operation ADD, not SRC_ALPHA.
            if(!a.EnableBlend||a.SourceColor!=RenderingDevice.BlendFactor.One||a.DestinationColor!=RenderingDevice.BlendFactor.One||
                a.ColorOperation!=RenderingDevice.BlendOperation.Add||a.SourceAlpha!=RenderingDevice.BlendFactor.One||
                a.DestinationAlpha!=RenderingDevice.BlendFactor.One||a.AlphaOperation!=RenderingDevice.BlendOperation.Add)
                throw new InvalidDataException("Eye upsample requires its exact original111100/RT0-enable temporary blend state.");
        }
        bool dynamicEye=source.EyeBefore?.TargetTextureParameterIndex is not null;
        bool dynamicDof=source.DofBefore?.ActualTarget0 is not null;
        if(!source.UsesInheritedCallerTarget&&!dynamicEye&&!dynamicDof&&
            (native.Length!=targets.Count||native.Any(b=>!targets.Any(t=>t.SourceTargetRegister==b.RegisterIndex&&t.Target.ParameterIndex==b.ParameterIndex))))
            throw new InvalidDataException("Fullscreen current targets differ from the counted source setters.");
        if(source.UsesInheritedCallerTarget&&targets.Count!=1)
            throw new NotSupportedException("Current final/FXAA caller target inheritance requires one proved source color target.");
        foreach(var binding in targets)
        {
            var target=binding.Target;
            if(target.FrameNumber!=source.FrameNumber||target.ContextOwnerIdentity!=source.ContextOwnerIdentity||
                target.OwnedFrontendIdentity!=context.OwnedFrontendIdentity||target.ViewIdentity!=context.ViewIdentity||
                string.IsNullOrWhiteSpace(target.ProducerOwnerIdentity)||string.IsNullOrWhiteSpace(target.SourceEvidence)||
                !target.StorageRid.IsValid||!target.TargetViewRid.IsValid||target.DeviceFormat!=ViewFormat(target.NativeViewFormat)||
                target.Extent.X<=0||target.Extent.Y<=0||target.Extent!=targets[0].Target.Extent||
                actual.Viewport.End.X>target.Extent.X||actual.Viewport.End.Y>target.Extent.Y)
                throw new InvalidDataException("Fullscreen target identity, native format, extent or viewport differs from its current source owner.");
            if(target.ParameterIndex is not{} parameter)continue;
            var reference=actual.References[parameter].Reference;
            if(reference.InstanceIndex!=target.InstanceIndex)throw new InvalidDataException("Fullscreen native target points at another side.");
            if(reference.PhysicalAllocationParameterIndex is{} physical)
            {
                var owner=Bank.RequireTexture(physical,target.InstanceIndex);
                if(owner.StorageRid!=target.StorageRid||!owner.Shape.HasRenderTargetPath||
                    target.NativeViewFormat!=owner.Shape.Formats.RenderTargetFormat0&&target.NativeViewFormat!=owner.Shape.Formats.RenderTargetFormat1||
                    !owner.NativeFormatViews.TryGetValue(target.NativeViewFormat,out var view))
                    throw new InvalidDataException("Fullscreen target is not its exact physical native bank RT allocation.");
                if(target is IMapPostEffect3FullscreenSelectedMipTargetLease selected)
                {
                    selected.RequireSourceSelectedTarget(pass,binding.SourceTargetRegister);
                    if(selected.NativeMipLevelCount!=1||selected.NativeFirstMipLevel>=owner.Shape.MipLevels||
                        string.IsNullOrWhiteSpace(selected.NativeMipSelectionEvidence)||
                        reference.HasSourceSelectedMipView&&(reference.NativeFirstMipLevel!=selected.NativeFirstMipLevel||reference.NativeMipLevelCount!=1))
                        throw new InvalidDataException("Fullscreen target lacks its actual current single-mip wrapper proof.");
                }
                else if(view!=target.TargetViewRid||owner.Shape.MipLevels!=1||reference.HasSourceSelectedMipView)
                    throw new NotSupportedException("Fullscreen multi-mip target requires the source-selected target capability.");
                if(dynamicEye)
                {
                    var before=source.EyeBefore!;
                    if(binding.SourceTargetRegister!=0||targets.Count!=1||before.TargetTextureParameterIndex!=physical||
                        target is not IMapPostEffect3FullscreenSelectedMipTargetLease chosen||chosen.NativeFirstMipLevel!=before.TargetMipLevel)
                        throw new InvalidDataException("Eye current target differs from the actual Before physical texture/mip setter.");
                }
            }
            else if(reference.ProducerOwnerIdentity!=target.ProducerOwnerIdentity)
                throw new InvalidDataException("Fullscreen external target differs from its current copied wrapper producer.");
            if(dynamicDof&&(binding.SourceTargetRegister!=0||source.DofBefore!.ActualTarget0!.ParameterIndex!=parameter))
                throw new InvalidDataException("DOF2 target differs from the exact Before target setter.");
        }
    }

    private static void RequireDeviceTarget(RenderingDevice rd,IMapPostEffect3StockGraphicsTargetLease target,
        int pass,int register)
    {
        if(!rd.TextureIsValid(target.StorageRid)||!rd.TextureIsValid(target.TargetViewRid))
            throw new InvalidDataException("Fullscreen native target retired before submission.");
        using var shape=rd.TextureGetFormat(target.TargetViewRid);
        if(shape.TextureType!=RenderingDevice.TextureType.Type2D||shape.ArrayLayers!=1||shape.Depth!=1||shape.Mipmaps!=1||
            shape.Width!=target.Extent.X||shape.Height!=target.Extent.Y||shape.Format!=target.DeviceFormat||
            !shape.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.ColorAttachmentBit))
            throw new InvalidDataException("Fullscreen actual RT view does not expose the source-selected single 2D mip.");
        if(target is IMapPostEffect3FullscreenSelectedMipTargetLease selected)
        {
            selected.RequireSourceSelectedTarget(pass,register);using var storage=rd.TextureGetFormat(target.StorageRid);
            uint mip=selected.NativeFirstMipLevel;
            if(mip>=storage.Mipmaps||mip>=32||shape.Width!=Math.Max(1u,storage.Width>>checked((int)mip))||
                shape.Height!=Math.Max(1u,storage.Height>>checked((int)mip)))
                throw new InvalidDataException("Fullscreen selected target does not match its actual physical mip extent.");
        }
    }

    private static void RequireDeviceTexture(RenderingDevice rd,IMapPostEffect3ComputeTextureLease lease,
        TranslatedShaderBinding declaration,int pass,IReadOnlyList<MapPostEffect3FullscreenTargetBinding> targets)
    {
        if(!rd.TextureIsValid(lease.StorageRid)||!rd.TextureIsValid(lease.ShaderViewRid))
            throw new InvalidDataException("Fullscreen current native sampled view retired.");
        using var shape=rd.TextureGetFormat(lease.ShaderViewRid);
        bool matches=shape.Format==lease.DeviceFormat;
        if(!matches&&lease is IMapPostEffect3SharedSampledViewLease shared)
        {shared.RequireSharedSampledViewProof().RequireCurrentNativeView(lease,rd,shape);matches=true;}
        bool volume=declaration.Source.Dimension==8;
        //DXGI21 is R32_FLOAT_X8X24_TYPELESS: its SRV exposes the float
        //depth aspect. The shared Vulkan D32_SFLOAT_S8_UINT storage name
        //also includes an unsigned stencil aspect, which is not sampled by
        //this original depth SRV. Classify the proved native view, rather
        //than a substring of the combined backend storage format.
        uint returnType=lease.NativeViewFormat switch{30 or 42 or 50=>4u,_=>5u};
        if(declaration.Source.Dimension is not(4 or 8)||declaration.Source.ReturnType!=returnType||
            shape.TextureType!=(volume?RenderingDevice.TextureType.Type3D:RenderingDevice.TextureType.Type2D)||
            shape.ArrayLayers!=1||shape.Width!=lease.Extent.X||shape.Height!=lease.Extent.Y||shape.Depth!=lease.Extent.Z||
            !matches||lease.DeviceFormat!=ViewFormat(lease.NativeViewFormat)||shape.Mipmaps==0||
            !shape.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
            throw new InvalidDataException("Fullscreen sampled texture differs from its exact native shader interface and GPU view.");
        uint first=0,count=shape.Mipmaps;
        if(lease is IMapPostEffect3ComputeSelectedMipTextureLease{IsSourceSelectedMipView:true} selected)
        {
            selected.RequireSourceSelectedView(pass,declaration.Source.BindPoint);selected.RequireReadableMipRange(selected.NativeFirstMipLevel,selected.NativeMipLevelCount);
            using var storage=rd.TextureGetFormat(lease.StorageRid);first=selected.NativeFirstMipLevel;count=selected.NativeMipLevelCount;
            if(volume||first>=storage.Mipmaps||count==0||count>storage.Mipmaps-first||first>=32||shape.Mipmaps!=count||
                shape.Width!=Math.Max(1u,storage.Width>>checked((int)first))||shape.Height!=Math.Max(1u,storage.Height>>checked((int)first)))
                throw new InvalidDataException("Fullscreen sampled slice view does not preserve its actual source wrapper mip range.");
        }
        else if(shape.Mipmaps>1&&lease is not IMapPostEffect3SourceTextureReadFootprintLease)
        {
            if(lease is not IMapPostEffect3ComputeMipTextureLease readable)
                throw new InvalidDataException("Fullscreen sampled mip range lacks completed source contents.");
            readable.RequireReadableMipRange(first,count);
        }
        foreach(var binding in targets.Where(t=>t.Target.StorageRid==lease.StorageRid))
        {
            uint written=(binding.Target as IMapPostEffect3FullscreenSelectedMipTargetLease)?.NativeFirstMipLevel??0;
            if(written>=first&&written-first<count)
                throw new NotSupportedException("Fullscreen sampled and target views expose the same physical mip.");
        }
    }
    private static RenderingDevice.DataFormat ViewFormat(uint native)=>native switch
    {
        2=>RenderingDevice.DataFormat.R32G32B32A32Sfloat,10=>RenderingDevice.DataFormat.R16G16B16A16Sfloat,
        26=>RenderingDevice.DataFormat.B10G11R11UfloatPack32,28=>RenderingDevice.DataFormat.R8G8B8A8Unorm,
        29=>RenderingDevice.DataFormat.R8G8B8A8Srgb,30=>RenderingDevice.DataFormat.R8G8B8A8Uint,
        34=>RenderingDevice.DataFormat.R16G16Sfloat,40=>RenderingDevice.DataFormat.D32Sfloat,
        41=>RenderingDevice.DataFormat.R32Sfloat,42=>RenderingDevice.DataFormat.R32Uint,
        49=>RenderingDevice.DataFormat.R8G8Unorm,50=>RenderingDevice.DataFormat.R8G8Uint,
        54=>RenderingDevice.DataFormat.R16Sfloat,61=>RenderingDevice.DataFormat.R8Unorm,
        87=>RenderingDevice.DataFormat.B8G8R8A8Unorm,91=>RenderingDevice.DataFormat.B8G8R8A8Srgb,
        20 or 21=>RenderingDevice.DataFormat.D32SfloatS8Uint,
        _=>throw new NotSupportedException("Fullscreen native format has no verified device image representation.")
    };
}
