using System.Buffers.Binary;
using System.Security.Cryptography;
using Godot;
using San14.Data;

/// <summary>One current native procedural fullscreen command, admitted only
/// by an actual prepared factory. It does not grant target or input contents.</summary>
public sealed class OriginalMapPostEffect3FullscreenSourceDraw
{
    private readonly Action validate;
    private OriginalMapPostEffect3FullscreenSourceDraw(int pass,long frame,string owner,string sha,
        string evidence,Action validate,OriginalMapPostEffect3EyeBeforeBindings? eye=null,
        OriginalMapPostEffect3DepthOfField2BeforeBindings? dof=null)
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
    public OriginalMapPostEffect3EyeBeforeBindings? EyeBefore{get;}
    public OriginalMapPostEffect3EyeAdaptation2Frame? EyeFrame{get;private init;}
    public OriginalMapPostEffect3DepthOfField2BeforeBindings? DofBefore{get;}
    public OriginalMapPostEffect3Composite2GraphicsRecipe? Composite2Recipe{get;private init;}
    public bool UsesInheritedCallerTarget{get;private init;}
    public IReadOnlyDictionary<int,string> RequiredConstantBufferBytesSha256{get;private init;}=
        new Dictionary<int,string>();
    public ReadOnlyMemory<byte> NativeDrawCommand=>Convert.FromHexString("2D0003000000000003000000");
    public void RequireCurrent()=>validate();

    public static OriginalMapPostEffect3FullscreenSourceDraw CaptureComposite2(
        OriginalMapPostEffect3Composite2GraphicsRecipe actual)
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
            before.SourceEvidence+";TC115B750-actual-graphics-Execute/restore;opcode3002D-start0-count3",Validate)
        {ExecutableLocale=frame.Locale,Composite2Recipe=actual,UsesInheritedCallerTarget=actual.ActualEffectiveTargetCount==0,
            RequiredConstantBufferBytesSha256=new System.Collections.ObjectModel.ReadOnlyDictionary<int,string>(
                new Dictionary<int,string>{{22,Convert.ToHexString(SHA256.HashData(frame.Prepared.ConstantBuffer.Bytes.Span))}})};
    }
    public static OriginalMapPostEffect3FullscreenSourceDraw CaptureEye(
        OriginalMapPostEffect3EyeAdaptation2Frame actual,OriginalMapPostEffect3EyeBeforeBindings before)
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
            actual.State.SourceSha256,before.SourceEvidence+";TC1159004/115908D-opcode3002D-start0-count3",Validate,before)
            {ExecutableLocale=actual.Locale,EyeFrame=actual,RequiredConstantBufferBytesSha256=CaptureEyeConstants(actual,before)};
    }
    public static OriginalMapPostEffect3FullscreenSourceDraw CaptureLensFlare2(
        OriginalMapPostEffect3LensFlare2PassRecipe actual)
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
            actual.SourceSha256,actual.SourceEvidence+";TC114C458-opcode3002D-start0-count3",Validate)
        {ExecutableLocale=actual.ConstantBuffer.Locale,RequiredConstantBufferBytesSha256=new System.Collections.ObjectModel.ReadOnlyDictionary<int,string>(
            new Dictionary<int,string>{{17,Convert.ToHexString(SHA256.HashData(actual.ConstantBuffer.Bytes.Span))}})};
    }
    public static OriginalMapPostEffect3FullscreenSourceDraw CaptureLensFlare2(
        OriginalMapPostEffect3LensFlare2GpuPassSource actual)
    {
        ArgumentNullException.ThrowIfNull(actual);actual.RequireCurrent();var recipe=actual.Recipe;
        var draw=CaptureLensFlare2(recipe);
        void Validate(){actual.RequireCurrent();draw.RequireCurrent();}
        return new(actual.SourcePassIndex,actual.FrameNumber,actual.ContextOwnerIdentity,actual.SourceSha256,
            draw.SourceEvidence,Validate)
        {ExecutableLocale=draw.ExecutableLocale,RequiredConstantBufferBytesSha256=new System.Collections.ObjectModel.ReadOnlyDictionary<int,string>(
            new Dictionary<int,string>{{17,actual.ConstantBufferBytesSha256}})};
    }
    public static OriginalMapPostEffect3FullscreenSourceDraw CaptureDepthOfField2(
        OriginalMapPostEffect3ParameterDepthOfField2Update actual,OriginalMapPostEffect3DepthOfField2BeforeBindings before)
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
            actual.ConstantBuffer.SourceSha256,before.SourceEvidence+";TC1155CF0-selected-opcode3002D-start0-count3",Validate,null,before)
        {
            ExecutableLocale=actual.ConstantBuffer.Locale,
            RequiredConstantBufferBytesSha256=new System.Collections.ObjectModel.ReadOnlyDictionary<int,string>(
                new Dictionary<int,string>{{18,Convert.ToHexString(SHA256.HashData(actual.ConstantBuffer.Bytes.Span))}})
        };
    }
    private static IReadOnlyDictionary<int,string> CaptureEyeConstants(OriginalMapPostEffect3EyeAdaptation2Frame frame,
        OriginalMapPostEffect3EyeBeforeBindings before)
    {
        var packets=new List<OriginalMapPostEffect3ParameterConstantBuffer>{frame.BloomMainConstants};
        if(before.BloomLoopConstants is{} loop)packets.Add(loop);
        var active=OriginalMapPostEffect3ParameterBindingRequirements.Read(frame.Allocation,before.Iteration.SourcePassIndex)
            .Where(r=>r.StageCode is 0 or 1&&r.ShaderDeclaredByteCount is not null).Select(r=>r.ParameterIndex).ToHashSet();
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int,string>(packets.Where(p=>active.Contains(p.Declaration.Index))
            .ToDictionary(p=>p.Declaration.Index,p=>Convert.ToHexString(SHA256.HashData(p.Bytes.Span))));
    }
}

/// <summary>Actual current frontend raster, viewport and scissor. The scissor
/// is independent: 95BC90/963D30 and frontend1D9 do not change the viewport.
/// A null scissor denotes positively proved disabled source scissor.</summary>
public interface IOriginalMapPostEffect3FullscreenDrawContextLease
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    string OwnedFrontendIdentity{get;}
    string ViewIdentity{get;}
    Rect2I ActualViewport{get;}
    float ActualViewportMinimumDepth{get;}
    float ActualViewportMaximumDepth{get;}
    Rect2I? ActualScissor{get;}
    OriginalModelRasterInputs ActualRaster{get;}
    string SourceEvidence{get;}
    void RequireCurrentForPass(int sourcePassIndex);
}

/// <summary>Current native wrapper with one selected render-target mip.
/// Storage allocation and another mip view cannot establish this capability.</summary>
public interface IOriginalMapPostEffect3FullscreenSelectedMipTargetLease:IOriginalMapPostEffect3StockGraphicsTargetLease
{
    uint NativeFirstMipLevel{get;}
    uint NativeMipLevelCount{get;}
    string NativeMipSelectionEvidence{get;}
    void RequireSourceSelectedTarget(int sourcePassIndex,int sourceTargetRegister);
}

public sealed record OriginalMapPostEffect3FullscreenTargetBinding(int SourceTargetRegister,
    IOriginalMapPostEffect3StockGraphicsTargetLease Target);

/// <summary>The real external target owner invalidates prior readiness at the
/// actual submission point. A revision describes writes, never pixel coverage.</summary>
public interface IOriginalMapPostEffect3FullscreenExternalWritableTargetLease:IOriginalMapPostEffect3StockGraphicsTargetLease
{
    long InvalidateContentsBeforePixelWrite(int sourcePassIndex);
}

/// <summary>Exact native Before texture setter, retaining its table revision
/// and producer. Constructors require the actual typed factory receipt.</summary>
public sealed class OriginalMapPostEffect3FullscreenTextureOverride
{
    private readonly OriginalMapPostEffect3TextureParameterState table;
    private readonly OriginalMapPostEffect3ResolvedTextureParameter reference;
    private readonly Action validate;
    private OriginalMapPostEffect3FullscreenTextureOverride(OriginalMapPostEffect3TextureParameterState table,
        int pass,int register,IOriginalMapPostEffect3ComputeTextureLease texture,uint setter,string evidence,Action validate)
    {
        this.table=table;reference=table.Require(texture.ParameterIndex);this.validate=validate;
        SourcePassIndex=pass;Texture=texture;SourceSetterRva=setter;SourceEvidence=evidence;
        SourceSetter=new(-1,OriginalMapPostEffect3BindingTable.TextureParameters,texture.ParameterIndex,1,
            checked((short)register),-1,-1,4,OriginalMapPostEffect3BindingRole.ShaderResourceRead,0,ReadOnlyMemory<byte>.Empty);
    }
    public int SourcePassIndex{get;}
    public long FrameNumber=>table.FrameNumber;
    public string ContextOwnerIdentity=>table.ContextOwnerIdentity;
    public string SourceSha256=>table.SourceSha256;
    public uint SourceSetterRva{get;}
    public OriginalMapPostEffect3Binding SourceSetter{get;}
    public IOriginalMapPostEffect3ComputeTextureLease Texture{get;}
    public string SourceEvidence{get;}
    public void RequireCurrent(){validate();table.ValidateCurrent(reference);Texture.RequireCurrent();}
    public static OriginalMapPostEffect3FullscreenTextureOverride CaptureEye(
        OriginalMapPostEffect3TextureParameterState table,OriginalMapPostEffect3EyeAdaptation2Frame frame,
        OriginalMapPostEffect3EyeBeforeBindings before,IOriginalMapPostEffect3ComputeSelectedMipTextureLease texture)
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
        Validate();return new(table,before.Iteration.SourcePassIndex,0,texture,before.NativePixelTextureSetterRva,before.SourceEvidence,Validate);
    }
    public static OriginalMapPostEffect3FullscreenTextureOverride CaptureDepthOfField2(
        OriginalMapPostEffect3TextureParameterState table,OriginalMapPostEffect3DepthOfField2BeforeBindings before,
        OriginalMapPostEffect3DepthOfField2PixelTextureSetter setter,IOriginalMapPostEffect3ComputeTextureLease texture)
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
        Validate();return new(table,before.SourcePassIndex,setter.NativeRegisterIndex,texture,setter.NativeSetterRva,
            before.SourceEvidence,Validate);
    }
}

public sealed record OriginalMapPostEffect3FullscreenFrameInputs(OriginalMapPostEffect3FullscreenSourceDraw SourceDraw,
    OriginalMapPostEffect3TextureParameterState TextureParameters,
    IReadOnlyList<IOriginalMapPostEffect3ComputeTextureLease> CurrentTextures,
    IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> CurrentSamplers,
    IReadOnlyDictionary<int,int> ConstantBufferInstances,
    IReadOnlyList<OriginalMapPostEffect3FullscreenTargetBinding> CurrentTargets,
    IOriginalMapPostEffect3FullscreenDrawContextLease CurrentDrawContext)
{
    public IReadOnlyList<OriginalMapPostEffect3FullscreenTextureOverride> TextureOverrides{get;init;}=[];
    public IReadOnlyList<IOriginalMapPostEffect3ComputeBufferLease> CurrentBuffers{get;init;}=[];
}

public sealed record OriginalMapPostEffect3FullscreenWriteReceipt(int SourceTargetRegister,int? ParameterIndex,
    int? PhysicalAllocationParameterIndex,int InstanceIndex,Rid StorageRid,Rid TargetViewRid,
    uint NativeViewFormat,Vector2I Extent,uint NativeMipLevel,long? WriterRevision)
{
    public bool PriorDestinationPixelsWereValidated{get;init;}
}

public sealed record OriginalMapPostEffect3FullscreenTextureReadReceipt(uint SourceStageCode,uint SourceRegister,
    int ParameterIndex,int? PhysicalAllocationParameterIndex,int InstanceIndex,Rid StorageRid,Rid ShaderViewRid,
    uint NativeViewFormat,RenderingDevice.DataFormat DeviceFormat,Vector3I Extent,
    uint NativeFirstMipLevel,uint NativeMipLevelCount,bool HasSourceReadFootprintProof,string SourceEvidence);

public sealed record OriginalMapPostEffect3FullscreenCompletedPass(long FrameNumber,string ContextOwnerIdentity,
    int SourcePassIndex,string VertexProgramSha256,string PixelProgramSha256,
    IReadOnlyList<OriginalMapPostEffect3Binding> OrderedEffectiveBindings,
    IReadOnlyList<OriginalMapPostEffect3FullscreenWriteReceipt> Writes,
    IReadOnlyList<OriginalMapPostEffect3ComputeConstantBufferReadReceipt> ConstantBufferReads,
    Rect2I ActualViewport,Rect2I? ActualScissor,OriginalModelRasterInputs ActualRaster,string SourceEvidence)
{
    public bool EstablishesCompleteResourceContents=>false;
    public bool CommitsFunctorHistory=>false;
    public bool WasPixelDrawSubmitted{get;init;}=true;
    public IReadOnlyList<OriginalMapPostEffect3FullscreenTextureReadReceipt> TextureReads{get;init;}=[];
    public IReadOnlyList<OriginalMapPostEffect3NullShaderResource> SourceNullShaderResources{get;init;}=[];
}
