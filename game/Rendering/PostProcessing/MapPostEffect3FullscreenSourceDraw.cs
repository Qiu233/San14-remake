using System.Buffers.Binary;
using System.Security.Cryptography;
using Godot;
using San14.Data;

/// <summary>One current native procedural fullscreen command, admitted only
/// by an actual prepared factory. It does not grant target or input contents.</summary>
public sealed class MapPostEffect3FullscreenSourceDraw
{
    private readonly Action validate;
    private MapPostEffect3FullscreenSourceDraw(int pass,long frame,string owner,string sha,
        string evidence,Action validate,MapPostEffect3EyeBeforeBindings? eye=null,
        MapPostEffect3DepthOfField2BeforeBindings? dof=null)
        =>(SourcePassIndex,FrameNumber,ContextOwnerIdentity,SourceSha256,SourceEvidence,this.validate,EyeBefore,DofBefore)=
          (pass,frame,owner,sha,evidence,validate,eye,dof);
    public int SourcePassIndex{get;}
    public long FrameNumber{get;}
    public string ContextOwnerIdentity{get;}
    public string SourceSha256{get;}
    public string ExecutableLocale{get;private init;}="TC";
    public string SourceEvidence{get;}
    public uint VertexCount=>3;
    public uint StartVertex=>0;
    public uint InstanceCount=>1;
    public RenderingDevice.RenderPrimitive DevicePrimitive=>RenderingDevice.RenderPrimitive.Triangles;
    public MapPostEffect3EyeBeforeBindings? EyeBefore{get;}
    public MapPostEffect3EyeAdaptation2Frame? EyeFrame{get;private init;}
    public MapPostEffect3DepthOfField2BeforeBindings? DofBefore{get;}
    public MapPostEffect3Composite2GraphicsRecipe? Composite2Recipe{get;private init;}
    public bool UsesInheritedCallerTarget{get;private init;}
    public IReadOnlyDictionary<int,string> RequiredConstantBufferBytesSha256{get;private init;}=
        new Dictionary<int,string>();
    public ReadOnlyMemory<byte> NativeDrawCommand=>Convert.FromHexString("2D0003000000000003000000");
    public void RequireCurrent()=>validate();

    public static MapPostEffect3FullscreenSourceDraw CaptureComposite2(
        MapPostEffect3Composite2GraphicsRecipe actual)
    {
        ArgumentNullException.ThrowIfNull(actual);var before=actual.Before;var frame=before.Frame;
        void Validate()
        {
            actual.RequireCurrent();before.RequireCurrent();
            if(before.IsCompute||!frame.Prepared.PassSelection.OrderedFactory9Passes.Contains(before.SourcePassIndex)||
                !actual.NativeDrawCommand.Span.SequenceEqual(Convert.FromHexString("2D0003000000000003000000")))
                throw new InvalidDataException("Composite2 graphics needs its actual selected lifecycle command and post-Bind state.");
        }
        Validate();return new(before.SourcePassIndex,frame.FrameNumber,frame.OwnerIdentity,frame.SourceSha256,
            before.SourceEvidence+";composite-graphics-execute/restore;start0-count3",Validate)
        {ExecutableLocale=frame.Locale,Composite2Recipe=actual,UsesInheritedCallerTarget=actual.ActualEffectiveTargetCount==0,
            RequiredConstantBufferBytesSha256=new System.Collections.ObjectModel.ReadOnlyDictionary<int,string>(
                new Dictionary<int,string>{{22,Convert.ToHexString(SHA256.HashData(frame.Prepared.ConstantBuffer.Bytes.Span))}})};
    }
    public static MapPostEffect3FullscreenSourceDraw CaptureEye(
        MapPostEffect3EyeAdaptation2Frame actual,MapPostEffect3EyeBeforeBindings before)
    {
        ArgumentNullException.ThrowIfNull(actual);ArgumentNullException.ThrowIfNull(before);
        void Validate()
        {
            actual.RequireCurrent();
            if(!actual.OrderedIterations.Contains(before.Iteration)||before.Iteration.IsCompute||
                !before.NativeDrawCommand.Span.SequenceEqual(Convert.FromHexString("2D0003000000000003000000")))
                throw new InvalidDataException("Eye source iteration does not admit its actual fullscreen command.");
        }
        Validate();return new(before.Iteration.SourcePassIndex,actual.FrameNumber,actual.OwnerIdentity,
            actual.State.SourceSha256,before.SourceEvidence+";eye-graphics-draw;start0-count3",Validate,before)
            {ExecutableLocale=actual.Locale,EyeFrame=actual,RequiredConstantBufferBytesSha256=CaptureEyeConstants(actual,before)};
    }
    public static MapPostEffect3FullscreenSourceDraw CaptureLensFlare2(
        MapPostEffect3LensFlare2PassRecipe actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        void Validate()
        {
            actual.RequireCurrent();
            if(actual.SourcePassIndex is not(45 or 50)||
                !actual.NativeDrawCommand.Span.SequenceEqual(Convert.FromHexString("2D0003000000000003000000")))
                throw new NotSupportedException("LensFlare2 fullscreen route requires its original three-vertex command.");
        }
        Validate();return new(actual.SourcePassIndex,actual.FrameNumber,actual.ContextOwnerIdentity,
            actual.SourceSha256,actual.SourceEvidence+";lens-graphics-draw;start0-count3",Validate)
        {ExecutableLocale=actual.ConstantBuffer.Locale,RequiredConstantBufferBytesSha256=new System.Collections.ObjectModel.ReadOnlyDictionary<int,string>(
            new Dictionary<int,string>{{17,Convert.ToHexString(SHA256.HashData(actual.ConstantBuffer.Bytes.Span))}})};
    }
    public static MapPostEffect3FullscreenSourceDraw CaptureLensFlare2(
        MapPostEffect3LensFlare2GpuPassSource actual)
    {
        ArgumentNullException.ThrowIfNull(actual);actual.RequireCurrent();var recipe=actual.Recipe;
        var draw=CaptureLensFlare2(recipe);
        void Validate(){actual.RequireCurrent();draw.RequireCurrent();}
        return new(actual.SourcePassIndex,actual.FrameNumber,actual.ContextOwnerIdentity,actual.SourceSha256,
            draw.SourceEvidence,Validate)
        {ExecutableLocale=draw.ExecutableLocale,RequiredConstantBufferBytesSha256=new System.Collections.ObjectModel.ReadOnlyDictionary<int,string>(
            new Dictionary<int,string>{{17,actual.ConstantBufferBytesSha256}})};
    }
    public static MapPostEffect3FullscreenSourceDraw CaptureDepthOfField2(
        MapPostEffect3ParameterDepthOfField2Update actual,MapPostEffect3DepthOfField2BeforeBindings before)
    {
        ArgumentNullException.ThrowIfNull(actual);ArgumentNullException.ThrowIfNull(before);
        void Validate()
        {
            actual.RequireCurrent();before.RequireCurrent();
            if(before.FrameNumber!=actual.FrameNumber||before.ContextOwnerIdentity!=actual.ConstantBuffer.OwnerIdentity||
                !actual.BeforeHookAllows(before.SourcePassIndex)||before.SourcePassIndex is >=57 and <=65)
                throw new NotSupportedException("DOF2 fullscreen route requires a selected ordinary procedural draw.");
        }
        Validate();return new(before.SourcePassIndex,actual.FrameNumber,before.ContextOwnerIdentity,
            actual.ConstantBuffer.SourceSha256,before.SourceEvidence+";dof-selected-graphics-draw;start0-count3",Validate,null,before)
        {
            ExecutableLocale=actual.ConstantBuffer.Locale,
            RequiredConstantBufferBytesSha256=new System.Collections.ObjectModel.ReadOnlyDictionary<int,string>(
                new Dictionary<int,string>{{18,Convert.ToHexString(SHA256.HashData(actual.ConstantBuffer.Bytes.Span))}})
        };
    }
    private static IReadOnlyDictionary<int,string> CaptureEyeConstants(MapPostEffect3EyeAdaptation2Frame frame,
        MapPostEffect3EyeBeforeBindings before)
    {
        var packets=new List<MapPostEffect3ParameterConstantBuffer>{frame.BloomMainConstants};
        if(before.BloomLoopConstants is{} loop)packets.Add(loop);
        var active=MapPostEffect3ParameterBindingRequirements.Read(frame.Allocation,before.Iteration.SourcePassIndex)
            .Where(r=>r.StageCode is 0 or 1&&r.ShaderDeclaredByteCount is not null).Select(r=>r.ParameterIndex).ToHashSet();
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int,string>(packets.Where(p=>active.Contains(p.Declaration.Index))
            .ToDictionary(p=>p.Declaration.Index,p=>Convert.ToHexString(SHA256.HashData(p.Bytes.Span))));
    }
}

/// <summary>Actual current frontend raster, viewport and scissor. The scissor
/// is independent: context binding and frontend1D9 do not change the viewport.
/// A null scissor denotes positively proved disabled source scissor.</summary>
public interface IMapPostEffect3FullscreenDrawContextLease
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    string OwnedFrontendIdentity{get;}
    string ViewIdentity{get;}
    Rect2I ActualViewport{get;}
    float ActualViewportMinimumDepth{get;}
    float ActualViewportMaximumDepth{get;}
    Rect2I? ActualScissor{get;}
    ModelRasterInputs ActualRaster{get;}
    string SourceEvidence{get;}
    void RequireCurrentForPass(int sourcePassIndex);
}

/// <summary>Current native wrapper with one selected render-target mip.
/// Storage allocation and another mip view cannot establish this capability.</summary>
public interface IMapPostEffect3FullscreenSelectedMipTargetLease:IMapPostEffect3StockGraphicsTargetLease
{
    uint NativeFirstMipLevel{get;}
    uint NativeMipLevelCount{get;}
    string NativeMipSelectionEvidence{get;}
    void RequireSourceSelectedTarget(int sourcePassIndex,int sourceTargetRegister);
}

public sealed record MapPostEffect3FullscreenTargetBinding(int SourceTargetRegister,
    IMapPostEffect3StockGraphicsTargetLease Target);

/// <summary>The real external target owner invalidates prior readiness at the
/// actual submission point. A revision describes writes, never pixel coverage.</summary>
public interface IMapPostEffect3FullscreenExternalWritableTargetLease:IMapPostEffect3StockGraphicsTargetLease
{
    long InvalidateContentsBeforePixelWrite(int sourcePassIndex);
}

/// <summary>Exact native Before texture setter, retaining its table revision
/// and producer. Constructors require the actual typed factory receipt.</summary>
public sealed class MapPostEffect3FullscreenTextureOverride
{
    private readonly MapPostEffect3TextureParameterState table;
    private readonly MapPostEffect3ResolvedTextureParameter reference;
    private readonly Action validate;
    private MapPostEffect3FullscreenTextureOverride(MapPostEffect3TextureParameterState table,
        int pass,int register,IMapPostEffect3ComputeTextureLease texture,MapPostEffect3ResourceSetter setter,string evidence,Action validate)
    {
        this.table=table;reference=table.Require(texture.ParameterIndex);this.validate=validate;
        SourcePassIndex=pass;Texture=texture;Setter=setter;SourceEvidence=evidence;
        SourceSetter=new(-1,MapPostEffect3BindingTable.TextureParameters,texture.ParameterIndex,1,
            checked((short)register),-1,-1,4,MapPostEffect3BindingRole.ShaderResourceRead,0,ReadOnlyMemory<byte>.Empty);
    }
    public int SourcePassIndex{get;}
    public long FrameNumber=>table.FrameNumber;
    public string ContextOwnerIdentity=>table.ContextOwnerIdentity;
    public string SourceSha256=>table.SourceSha256;
    public MapPostEffect3ResourceSetter Setter{get;}
    public MapPostEffect3Binding SourceSetter{get;}
    public IMapPostEffect3ComputeTextureLease Texture{get;}
    public string SourceEvidence{get;}
    public void RequireCurrent(){validate();table.ValidateCurrent(reference);Texture.RequireCurrent();}
    public static MapPostEffect3FullscreenTextureOverride CaptureEye(
        MapPostEffect3TextureParameterState table,MapPostEffect3EyeAdaptation2Frame frame,
        MapPostEffect3EyeBeforeBindings before,IMapPostEffect3ComputeSelectedMipTextureLease texture)
    {
        ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(before);ArgumentNullException.ThrowIfNull(texture);
        var reference=table.Require(texture.ParameterIndex);
        void Validate()
        {
            frame.RequireCurrent();table.ValidateCurrent(reference);texture.RequireCurrent();
            if(table.FrameNumber!=frame.FrameNumber||table.ContextOwnerIdentity!=frame.OwnerIdentity||
                table.SourceSha256!=frame.State.SourceSha256||!frame.OrderedIterations.Contains(before.Iteration)||
                !before.RequiresDynamicPixelTextureBinding||before.SourceTextureParameterIndex is not{} physical||
                reference.Reference.PhysicalAllocationParameterIndex!=physical||reference.Reference.InstanceIndex!=texture.InstanceIndex||
                !texture.IsSourceSelectedMipView||before.SourceMipLevel is not{} mip||
                texture.NativeFirstMipLevel!=mip||texture.NativeMipLevelCount!=1)
                throw new InvalidDataException("Eye t0 override differs from its actual current source single-mip wrapper.");
            texture.RequireSourceSelectedView(before.Iteration.SourcePassIndex,0);
        }
        Validate();return new(table,before.Iteration.SourcePassIndex,0,texture,before.TextureSetter,before.SourceEvidence,Validate);
    }
    public static MapPostEffect3FullscreenTextureOverride CaptureDepthOfField2(
        MapPostEffect3TextureParameterState table,MapPostEffect3DepthOfField2BeforeBindings before,
        MapPostEffect3DepthOfField2PixelTextureSetter setter,IMapPostEffect3ComputeTextureLease texture)
    {
        ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(setter);ArgumentNullException.ThrowIfNull(texture);
        void Validate()
        {
            before.RequireCurrent();texture.RequireCurrent();table.ValidateCurrent(setter.ActualTexture);
            if(table.FrameNumber!=before.FrameNumber||table.ContextOwnerIdentity!=before.ContextOwnerIdentity||
                !before.OrderedPixelTextureSetters.Contains(setter)||
                before.OrderedPixelTextureSetters.Last(s=>s.NativeRegisterIndex==setter.NativeRegisterIndex)!=setter||
                texture.ParameterIndex!=setter.ActualTexture.ParameterIndex||
                texture.InstanceIndex!=setter.ActualTexture.Reference.InstanceIndex)
                throw new InvalidDataException("DOF2 Before override differs from its actual source texture setter.");
        }
        Validate();return new(table,before.SourcePassIndex,setter.NativeRegisterIndex,texture,setter.TextureSetter,
            before.SourceEvidence,Validate);
    }
}

public sealed record MapPostEffect3FullscreenFrameInputs(MapPostEffect3FullscreenSourceDraw SourceDraw,
    MapPostEffect3TextureParameterState TextureParameters,
    IReadOnlyList<IMapPostEffect3ComputeTextureLease> CurrentTextures,
    IReadOnlyList<IMapPostEffect3ComputeSamplerLease> CurrentSamplers,
    IReadOnlyDictionary<int,int> ConstantBufferInstances,
    IReadOnlyList<MapPostEffect3FullscreenTargetBinding> CurrentTargets,
    IMapPostEffect3FullscreenDrawContextLease CurrentDrawContext)
{
    public IReadOnlyList<MapPostEffect3FullscreenTextureOverride> TextureOverrides{get;init;}=[];
    public IReadOnlyList<IMapPostEffect3ComputeBufferLease> CurrentBuffers{get;init;}=[];
}

public sealed record MapPostEffect3FullscreenWriteReceipt(int SourceTargetRegister,int? ParameterIndex,
    int? PhysicalAllocationParameterIndex,int InstanceIndex,Rid StorageRid,Rid TargetViewRid,
    uint NativeViewFormat,Vector2I Extent,uint NativeMipLevel,long? WriterRevision)
{
    public bool PriorDestinationPixelsWereValidated{get;init;}
}

public sealed record MapPostEffect3FullscreenTextureReadReceipt(uint SourceStageCode,uint SourceRegister,
    int ParameterIndex,int? PhysicalAllocationParameterIndex,int InstanceIndex,Rid StorageRid,Rid ShaderViewRid,
    uint NativeViewFormat,RenderingDevice.DataFormat DeviceFormat,Vector3I Extent,
    uint NativeFirstMipLevel,uint NativeMipLevelCount,bool HasSourceReadFootprintProof,string SourceEvidence);

public sealed record MapPostEffect3FullscreenCompletedPass(long FrameNumber,string ContextOwnerIdentity,
    int SourcePassIndex,string VertexProgramSha256,string PixelProgramSha256,
    IReadOnlyList<MapPostEffect3Binding> OrderedEffectiveBindings,
    IReadOnlyList<MapPostEffect3FullscreenWriteReceipt> Writes,
    IReadOnlyList<MapPostEffect3ComputeConstantBufferReadReceipt> ConstantBufferReads,
    Rect2I ActualViewport,Rect2I? ActualScissor,ModelRasterInputs ActualRaster,string SourceEvidence)
{
    public bool EstablishesCompleteResourceContents=>false;
    public bool CommitsFunctorHistory=>false;
    public bool WasPixelDrawSubmitted{get;init;}=true;
    public IReadOnlyList<MapPostEffect3FullscreenTextureReadReceipt> TextureReads{get;init;}=[];
    public IReadOnlyList<MapPostEffect3NullShaderResource> SourceNullShaderResources{get;init;}=[];
}
