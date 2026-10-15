using Godot;
using San14.Data;

/// <summary>Retained original shape wrappers must supply these actual native
/// metadata fields. A declaration or an arbitrary square GPU image is insufficient.</summary>
public interface IMapPostEffect3DepthOfField2ShapeTextureLease:IMapPostEffect3ComputeExternalTextureLease,
    IMapPostEffect3ComputeMipTextureLease
{
    uint ActualNativeFlags46{get;}
    uint ActualNativeMipCount45{get;}
}

public interface IMapPostEffect3DepthOfField2RetainedShapeInvocation
{
    Task<IMapPostEffect3DepthOfField2ShapeTextureLease> CaptureCurrentShapeAsync(
        MapPostEffect3DepthOfField2ShapeRequirement actualRequirement,
        MapPostEffect3ResolvedTextureParameter actualPointer);
}

public sealed partial class MapPostEffect3DepthOfField2ExecutionOwner
{
    private MapPostEffect3DepthOfField2ModuleResources.ShapeStorage? ownedShape;
    private MapPostEffect3ResolvedTextureParameter? shapePointer;
    private IMapPostEffect3DepthOfField2ShapeTextureLease? retainedShape;

    private async Task PrepareShapeAsync()
    {
        RequireCurrent();var requirement=Prepared.ShapeRequirement;
        if(requirement.ActualShapeTextureParameterIndex!=58||requirement.RequiredMipCount!=1||
            requirement.RequiredNativeFormat!=3||requirement.RequestedSide==0||requirement.RequestedSide>32767)
            throw new NotSupportedException("DOF2 source shape requires its exact positive bounded native-format3/single-mip square requirement.");
        if(ModuleResources.Shape is { } previous)
        {
            var pointer=TextureParameters.ReadKnownNullablePointer(58) is not null?TextureParameters.Require(58):null;
            if(pointer is null||!ReferenceEquals(pointer.Reference,previous.Reference)||previous.Side!=requirement.RequestedSide||
                !previous.Rid.IsValid||previous.Reference.InstanceIndex!=0||
                previous.Reference.PhysicalAllocationParameterIndex is not null)
                throw new NotSupportedException("DOF2 retained shape requires the same actual module texture RID, pointer identity and source side; source replacement needs its explicit adapter.");
            if(!requirement.SourceShapeChanged&&(!previous.Complete||previous.WriterFrameNumber>=Prepared.FrameNumber||previous.Revision<=0))
                throw new InvalidDataException("DOF2 cached shape has no retained actual original53 pixels and writer revision.");
            ownedShape=previous;shapePointer=pointer;previous.LastTable=TextureParameters;return;
        }
        if(TextureParameters.ReadKnownNullablePointer(58) is not null)
        {
            if(ModuleResources.CpuModule is not null)
                throw new InvalidDataException("DOF2 retained module cannot adopt an external shape pointer as its original module-owned GPU contents.");
            if(invocation is not IMapPostEffect3DepthOfField2RetainedShapeInvocation retained)
                throw new NotSupportedException("DOF2 retained shape pointer needs its actual native wrapper flags/mip/format producer; it cannot be replaced from allocation metadata.");
            var pointer=TextureParameters.Require(58);
            retainedShape=await retained.CaptureCurrentShapeAsync(requirement,pointer);
            retainedShape.RequireCurrent();TextureParameters.ValidateCurrent(pointer);
            if(retainedShape.FrameNumber!=Prepared.FrameNumber||retainedShape.ContextOwnerIdentity!=State.OwnerIdentity||
                retainedShape.ParameterIndex!=58||retainedShape.InstanceIndex!=pointer.Reference.InstanceIndex||
                retainedShape.ProducerOwnerIdentity!=pointer.Reference.ProducerOwnerIdentity||retainedShape.NativeViewFormat!=10||
                retainedShape.DeviceFormat!=RenderingDevice.DataFormat.R16G16B16A16Sfloat||
                retainedShape.Extent!=new Vector3I((int)requirement.RequestedSide,(int)requirement.RequestedSide,1)||
                retainedShape.ActualNativeMipCount45!=1||
                (retainedShape.ActualNativeFlags46&requirement.RequiredFlags46Mask)!=requirement.RequiredFlags46Value||
                (retainedShape.ActualNativeFlags46&127)!=requirement.RequiredFlags46Low7)
                throw new NotSupportedException("DOF2 retained shape does not satisfy the required shape wrapper; its real replacement owner is required.");
            return;
        }
        if(!requirement.SourceShapeChanged)
            throw new InvalidDataException("DOF2 source did not run the shape writer and has no retained actual shape pointer.");
        var resources=new RenderDeviceResources("Device rejected the actual DOF2 source-created shape allocation.");
        try
        {
            Rid image=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                var usage=RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.ColorAttachmentBit;
                if(!rd.TextureIsFormatSupportedForUsage(RenderingDevice.DataFormat.R16G16B16A16Sfloat,usage))
                    throw new NotSupportedException("DOF2 original native-format3/DXGI10 shape target is unsupported by this device.");
                using var format=new RDTextureFormat{Format=RenderingDevice.DataFormat.R16G16B16A16Sfloat,
                    TextureType=RenderingDevice.TextureType.Type2D,Width=requirement.RequestedSide,Height=requirement.RequestedSide,
                    Depth=1,ArrayLayers=1,Mipmaps=1,Samples=RenderingDevice.TextureSamples.Samples1,UsageBits=usage};
                using var view=new RDTextureView();return resources.Own(rd.TextureCreate(format,view));
            });
            TextureParameters.BindExternal(58,new(Prepared.FrameNumber,State.OwnerIdentity+":DOF2-source-shape/"+image),
                Prepared.SourceEvidence+";create-required-square-M1-format3/copy-shape");
            shapePointer=TextureParameters.Require(58);
            ownedShape=new(image,requirement.RequestedSide,shapePointer.Reference,TextureParameters,resources);
            ModuleResources.AdoptShape(this,ownedShape);
        }
        catch(Exception failure)
        {
            try{if(resources.Count!=0)await resources.DisposeAsync();}
            catch(Exception cleanup)
            {
                var partial=NativeMapBorrowerRetirementPackage.CreatePending("DOF2-shape-construction",[resources])!;
                partial.RetainFailure();throw new NativeMapBorrowerConstructionException(failure,cleanup,partial);
            }
            throw;
        }
    }

    private MapPostEffect3DepthOfField2TargetView ShapeView(int register,MapPostEffect3ResolvedTextureParameter pointer)
    {
        var shape=ownedShape??throw new InvalidDataException("DOF2 source-created shape owner is absent.");
        RequireCurrent();TextureParameters.ValidateCurrent(shapePointer!);
        if(register!=0||pointer!=shapePointer||!ReferenceEquals(pointer.Reference,shape.Reference))throw new InvalidDataException("DOF2 shape target differs from original53 RT0/58.");
        return new(register,pointer,shape.Rid,shape.Rid,10,RenderingDevice.DataFormat.R16G16B16A16Sfloat,
            new((int)shape.Side,(int)shape.Side),Prepared.SourceEvidence+";source-created-shape-nativeFormat3/DXGI10/M1;allocation-not-pixel-initialization");
    }
    private IMapPostEffect3ComputeTextureLease CaptureShapeRead(MapPostEffect3ResolvedTextureParameter pointer)
    {
        var shape=ownedShape??throw new InvalidDataException("DOF2 shape owner is absent.");
        if(pointer!=shapePointer||!ReferenceEquals(pointer.Reference,shape.Reference)||!shape.Complete)
            throw new InvalidDataException("DOF2 actual shape has no complete original53 writer pixels.");
        return new ShapeRead(this,shape,pointer,shape.Revision,shape.WriterFrameNumber);
    }
    private void PublishShapeCoverage(MapPostEffect3DepthOfField2GraphicsCoverageReceipt receipt)
    {
        receipt.RequireCurrentSubmission();var shape=ownedShape??throw new InvalidDataException("DOF2 shape writer owner is absent.");
        var write=receipt.Completed.Writes.Single();
        if(receipt.Source.SourcePassIndex!=53||!receipt.CompleteTargetPixelDomains[0]||write.ParameterIndex!=58||
            write.StorageRid!=shape.Rid||write.TargetViewRid!=shape.Rid||write.NativeViewFormat!=10||
            write.WriterRevision!=shape.Revision||write.Extent!=new Vector2I((int)shape.Side,(int)shape.Side)||shape.Pass!=53)
            throw new InvalidDataException("DOF2 complete shape pixels require the actual original53 target and latest writer revision.");
        shape.Complete=true;shape.WriterFrameNumber=Prepared.FrameNumber;
        shape.VertexProgramSha256=receipt.Completed.VertexProgramSha256;shape.PixelProgramSha256=receipt.Completed.PixelProgramSha256;
    }
    private void ReleaseFrameShape()
    {
        ownedShape=null;shapePointer=null;retainedShape=null;
    }
    private sealed record ShapeTarget(MapPostEffect3DepthOfField2ExecutionOwner Owner,
        MapPostEffect3DepthOfField2BeforeBindings Before,MapPostEffect3DepthOfField2TargetView View,
        IMapPostEffect3FullscreenDrawContextLease Context):IMapPostEffect3FullscreenExternalWritableTargetLease
    {
        public long FrameNumber=>Before.FrameNumber;public string ContextOwnerIdentity=>Before.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>Context.OwnedFrontendIdentity;public string ViewIdentity=>Context.ViewIdentity;
        public int? ParameterIndex=>58;public int InstanceIndex=>0;public string ProducerOwnerIdentity=>View.ActualPointer.Reference.ProducerOwnerIdentity;
        public Rid StorageRid=>View.StorageRid;public Rid TargetViewRid=>View.TargetViewRid;
        public uint NativeViewFormat=>10;public RenderingDevice.DataFormat DeviceFormat=>View.DeviceFormat;
        public Vector2I Extent=>View.Extent;public string SourceEvidence=>View.SourceEvidence+";"+Context.SourceEvidence;
        public void RequireCurrentForPass(int pass)
        {Owner.RequireCurrent();Before.RequireCurrent();Context.RequireCurrentForPass(pass);Owner.TextureParameters.ValidateCurrent(View.ActualPointer);
         if(pass!=53||Before.SourcePassIndex!=53||Owner.ownedShape?.Rid!=StorageRid)
             throw new InvalidDataException("DOF2 actual shape target belongs to another source writer.");}
        public long InvalidateContentsBeforePixelWrite(int pass)
        {RequireCurrentForPass(pass);var shape=Owner.ownedShape!;shape.Complete=false;shape.Pass=pass;
         shape.VertexProgramSha256=null;shape.PixelProgramSha256=null;
         shape.WriterFrameNumber=FrameNumber;return shape.Revision=checked(shape.Revision+1);}
    }
    private sealed record ShapeRead(MapPostEffect3DepthOfField2ExecutionOwner Owner,
        MapPostEffect3DepthOfField2ModuleResources.ShapeStorage Shape,
        MapPostEffect3ResolvedTextureParameter CurrentPointer,long Revision,long WriterFrame)
        :IMapPostEffect3DepthOfField2ShapeTextureLease
    {
        public long FrameNumber=>Owner.Prepared.FrameNumber;public string ContextOwnerIdentity=>Owner.State.OwnerIdentity;
        public int ParameterIndex=>58;public int InstanceIndex=>0;public string ProducerOwnerIdentity=>Shape.Reference.ProducerOwnerIdentity;
        public Rid StorageRid=>Shape.Rid;public Rid ShaderViewRid=>Shape.Rid;public uint NativeViewFormat=>10;
        public RenderingDevice.DataFormat DeviceFormat=>RenderingDevice.DataFormat.R16G16B16A16Sfloat;
        public Vector3I Extent=>new((int)Shape.Side,(int)Shape.Side,1);public uint ActualNativeFlags46=>0x403;
        public uint ActualNativeMipCount45=>1;
        public string SourceEvidence=>Owner.Prepared.SourceEvidence+$";actual-original53-complete-shape-writerFrame={WriterFrame}/revision={Revision};source-nativeFlags46=403/M1/DXGI10;current-frame-consumer-only";
        public void RequireCurrent()
        {Owner.RequireCurrent();Owner.TextureParameters.ValidateCurrent(CurrentPointer);
         if(!ReferenceEquals(Owner.ownedShape,Shape)||!ReferenceEquals(Owner.ModuleResources.Shape,Shape)||
            !ReferenceEquals(CurrentPointer.Reference,Shape.Reference)||!Shape.Complete||Shape.Revision!=Revision||Shape.WriterFrameNumber!=WriterFrame)
             throw new InvalidDataException("DOF2 original shape pixels were replaced or invalidated.");}
        public void RequireReadableMipRange(uint first,uint count)
        {RequireCurrent();if(first!=0||count!=1)throw new InvalidDataException("DOF2 source shape covers only actual mip0.");}
    }
}
