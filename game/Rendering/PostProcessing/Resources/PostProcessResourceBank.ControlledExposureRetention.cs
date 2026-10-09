using Godot;
using San14.Data;

internal sealed class MapPostEffect3RetainedControlledExposureSnapshot(
    PostProcessResourceBank bank,MapPostEffect3DeviceTexture texture,
    long writerFrame,long writerRevision,IMapPostEffect3ComputeMipTextureLease contents)
{
    internal readonly PostProcessResourceBank Bank=bank;
    internal readonly MapPostEffect3DeviceTexture Texture=texture;
    internal readonly long WriterFrame=writerFrame,WriterRevision=writerRevision;
    internal readonly IMapPostEffect3ComputeMipTextureLease Contents=contents;
}

/// <summary>A later invocation's explicit read of the preceding actual104
/// contents. WriterFrame/Revision stay unchanged. It becomes stale when104
/// begins its next physical write, irrespective of table/frame migration.</summary>
public sealed class MapPostEffect3RetainedControlledExposureRead:IMapPostEffect3ComputeMipTextureLease,
    IMapPostEffect3SourceTextureReadFootprintLease
{
    internal MapPostEffect3RetainedControlledExposureRead(MapPostEffect3WorldControlledFactoryFrame frame,
        MapPostEffect3RetainedControlledExposureSnapshot snapshot,int pass,object consumer,Action require,
        string shader,IReadOnlyDictionary<int,string> cbs,uint register)
        =>(Frame,Snapshot,Pass,Consumer,RequireConsumer,ProgramSha,Cbs,Register)=(frame,snapshot,pass,consumer,require,shader,cbs,register);
    internal readonly MapPostEffect3WorldControlledFactoryFrame Frame;
    internal readonly MapPostEffect3RetainedControlledExposureSnapshot Snapshot;
    internal readonly int Pass;internal readonly object Consumer;internal readonly Action RequireConsumer;
    internal readonly string ProgramSha;internal readonly IReadOnlyDictionary<int,string> Cbs;internal readonly uint Register;
    public long FrameNumber=>Frame.Camera.FrameNumber;
    public long WriterFrameNumber=>Snapshot.WriterFrame;
    public long WriterRevision=>Snapshot.WriterRevision;
    public int ParameterIndex=>61;public int InstanceIndex=>0;
    public string ContextOwnerIdentity=>Frame.Camera.State.OwnerIdentity;
    public Rid StorageRid=>Snapshot.Texture.StorageRid;public Rid ShaderViewRid=>Snapshot.Texture.NativeFormatViews[41];
    public uint NativeViewFormat=>41;public RenderingDevice.DataFormat DeviceFormat=>RenderingDevice.DataFormat.R32Sfloat;
    public Vector3I Extent=>new(2,1,1);
    public string SourceEvidence=>Snapshot.Contents.SourceEvidence+$";exact-retained104-writerFrame={WriterFrameNumber}/revision={WriterRevision};"+
        "current-consumer="+Pass+";no-old-texture-ready-frame-relabel";
    public void RequireCurrent()
    {
        Frame.RequireCurrent();RequireConsumer();Snapshot.Bank.RequireControlledExposureSnapshot(Snapshot);
        var pointer=Frame.Textures.Require(61);
        if(Frame.Begin.Counter<=1||WriterFrameNumber!=FrameNumber-1||pointer.Reference.PhysicalAllocationParameterIndex!=61||
            pointer.Reference.InstanceIndex!=0||pointer.Reference.HasSourceSelectedMipView)
            throw new InvalidDataException("Retained exposure read is not the same owned61 in its exact next invocation.");
    }
    public void RequireReadableMipRange(uint first,uint count)
    {RequireCurrent();if(first!=0||count!=1)throw new InvalidDataException("Exposure history contains only its real2x1 mip0.");Snapshot.Contents.RequireReadableMipRange(0,1);}
    public void RequireReadableSourceFootprint(int pass,uint register,string shader,IReadOnlyList<MapPostEffect3ComputeConstantBufferReadReceipt> cbs)
    {
        RequireReadableMipRange(0,1);
        if(pass!=Pass||register!=Register||shader!=ProgramSha||Cbs.Any(expected=>!cbs.Any(c=>c.ParameterIndex==expected.Key&&
            c.HasCompleteWriterContents&&c.CompleteBytesSha256==expected.Value)))
            throw new InvalidDataException("Retained61 consumer differs from the actual source shader/CB/literal texel contract.");
    }
}

public sealed partial class PostProcessResourceBank
{
    private void RequireRetainedControlledExposureCompleted(MapPostEffect3RetainedControlledExposureRead prior,
        MapPostEffect3EyeComputeWriteCoverage coverage,MapPostEffect3ComputeCompletedPass completed)
    {
        var write=completed.Writes.SingleOrDefault(w=>w.ParameterIndex==61);
        if(!ReferenceEquals(prior.Snapshot.Bank,this)||!ReferenceEquals(prior.Consumer,coverage)||prior.Pass!=104||
            completed.FrameNumber!=prior.FrameNumber||completed.SourcePassIndex!=104||completed.ProgramSha256!=prior.ProgramSha||
            write is null||write.StorageRid!=prior.StorageRid||write.InstanceIndex!=0||write.MipLevel!=0||write.MipLevelCount!=1||
            write.NativeViewFormat!=41||write.WriterRevision!=checked(prior.WriterRevision+1)||
            !textureContents.TryGetValue((61,0,0),out var current)||current.Frame!=prior.FrameNumber||current.Pass!=104||
            current.Revision!=write.WriterRevision||current.Complete)
            throw new InvalidDataException("CS52 must replace the exact accepted prior104 revision once, before its current coverage publication.");
    }
    internal MapPostEffect3RetainedControlledExposureSnapshot CaptureRetainedControlledExposure(
        MapPostEffect3WorldControlledFactoryFrame frame,MapPostEffect3Functor1DirectoryReturnedReceipt directory)
    {
        frame.RequireActualDirectoryReturned(directory);
        var submission=directory.SelectedDeviceJoin.ActualReceipts.Single(r=>r.SourcePassIndex==104);
        submission.RequireActualForSource(frame.WrapperDirectory.Wrappers[104-45]);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!textureContents.TryGetValue((61,0,0),out var current)||!current.Complete||current.Pass!=104||current.Frame!=frame.Camera.FrameNumber)
                throw new InvalidDataException("Module history requires its actual complete current104 writer, not a CPU exit or allocation.");
            var texture=Require(Textures,61,0,"actual controlled exposure history");
            var contents=CaptureSourceWrittenTextureLease(61,0,current.Frame,0,1);
            return new(this,texture,current.Frame,current.Revision,contents);
        }
    }
    internal void RequireControlledExposureSnapshot(MapPostEffect3RetainedControlledExposureSnapshot snapshot)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(snapshot.Bank,this)||!ReferenceEquals(Require(Textures,61,0,"retained exposure"),snapshot.Texture)||
                !textureContents.TryGetValue((61,0,0),out var current)||!current.Complete||current.Pass!=104||
                current.Frame!=snapshot.WriterFrame||current.Revision!=snapshot.WriterRevision)
                throw new InvalidDataException("Actual prior exposure storage/revision was replaced or rewritten.");
            snapshot.Contents.RequireReadableMipRange(0,1);
        }
    }
    internal MapPostEffect3RetainedControlledExposureRead CaptureRetainedControlledExposureRead(
        MapPostEffect3WorldControlledFactoryFrame frame,MapPostEffect3RetainedControlledExposureSnapshot snapshot,
        int pass,object consumer,MapPostEffect3ShaderReadFootprint? footprint)
    {
        frame.RequireCurrent();RequireControlledExposureSnapshot(snapshot);
        string shader;IReadOnlyDictionary<int,string> cbs;uint register;Action require;
        if(pass is 55 or 56&&consumer is MapPostEffect3DepthOfField2GraphicsSource dof)
        {
            dof.Before.RequirePrepared(frame.DepthOfField);
            if(!dof.ExposureTexelValueAffectsOutput||dof.SourcePassIndex!=pass)
                throw new InvalidDataException("Retained DOF exposure needs its exact auto texel0 dependency.");
            shader=dof.PixelProgramSha256;cbs=dof.RequiredConstantBufferBytesSha256;register=2;require=dof.RequireCurrent;
        }
        else if(pass==84&&consumer is MapPostEffect3EyeBeforeBindings eye)
        {
            if(!frame.Eye.OrderedIterations.Contains(eye.Iteration)||eye.Iteration.SourcePassIndex!=84)
                throw new InvalidDataException("Retained prefilter exposure is not the actual prepared84 Before.");
            var proof=MapPostEffect3EyeGraphicsWriteCoverage.Prepare(Source,frame.Eye,eye);
            var read=proof.RequiredShaderReads.Single(r=>r.NativeBinding.ParameterIndex==61&&r.SourceRegister==1);
            if(read.Kind!=MapPostEffect3ShaderReadFootprintKind.LiteralTexel||read.LiteralX!=0)
                throw new InvalidDataException("Retained prefilter exposure changed its original texel0.");
            shader=proof.PixelProgramSha256;cbs=proof.RequiredConstantBufferBytesSha256;register=1;require=frame.Eye.RequireCurrent;
        }
        else if(pass is 100 or 101&&consumer is MapPostEffect3Composite2ReadFootprint composite)
        {
            composite.RequirePrepared(frame.Composite.Prepared);
            if(composite.SourcePassIndex!=pass||footprint is null||!composite.ShaderReads.Contains(footprint)||
                footprint.NativeBinding.ParameterIndex!=61||footprint.SourceRegister!=2||
                footprint.Kind!=MapPostEffect3ShaderReadFootprintKind.LiteralTexel||footprint.LiteralX!=1||
                footprint.LiteralY!=0||footprint.LiteralZ!=0)
                throw new InvalidDataException("Retained AddBloom exposure needs its original exact texel1 consumer.");
            shader=composite.PixelProgramSha256;cbs=composite.RequiredConstantBufferBytesSha256;register=2;require=composite.RequireCurrent;
        }
        else if(pass==104&&consumer is MapPostEffect3EyeComputeWriteCoverage coverage)
        {
            coverage.RequirePreparedFrame(frame.Eye);
            if(coverage.FrameNumber!=frame.Camera.FrameNumber||coverage.SourcePassIndex!=104||!coverage.RequiresPriorExposureTexelContents||
                coverage.ProgramSha256!=Source.Source.GetProgram(5,52).Sha256||coverage.ParameterIndex!=61)
                throw new InvalidDataException("Retained exposure UAV prior load needs the exact current sourceCS52 coverage.");
            shader=coverage.ProgramSha256;cbs=coverage.RequiredConstantBufferBytesSha256;register=0;require=coverage.RequireCurrent;
        }
        else throw new NotSupportedException("This retained exposure authority does not admit another source consumer.");
        var result=new MapPostEffect3RetainedControlledExposureRead(frame,snapshot,pass,consumer,require,shader,cbs,register);
        result.RequireCurrent();return result;
    }
}
