using Godot;
using San14.Data;

public sealed record MapPostEffect3EyeComputePublishedPass(
    MapPostEffect3ComputeCompletedPass Completed,
    IMapPostEffect3ComputeBufferLease? Histogram,
    IMapPostEffect3ComputeMipTextureLease? Exposure)
{
    public bool CommitsFunctorHistory=>false;
}

public sealed partial class PostProcessResourceBank
{
    private sealed record EyeBufferInputSnapshot(int Parameter,int Side,Rid Storage,uint Bytes,long Revision,string? Hash);
    private sealed record EyeCbInputSnapshot(int Parameter,int Side,Rid Storage,long Revision,string Hash);
    private sealed record EyeAcceptedInputs(MapPostEffect3ComputeFrameInputs Submitted,
        IReadOnlyList<EyeBufferInputSnapshot> Buffers,IReadOnlyList<EyeCbInputSnapshot> ConstantBuffers,
        MapPostEffect3ResolvedTextureParameter? ColorPointer,
        IMapPostEffect3ComputeTextureLease? Color);

    /// <summary>Publishes only this method's actual accepted dispatch and input
    /// transaction. No caller-supplied completion receipt or evidence string can
    /// substitute for the actual source color/prior-buffer consumed here.</summary>
    public async Task<MapPostEffect3EyeComputePublishedPass> ExecuteAndPublishEyeComputeAsync(
        MapPostEffect3EyeComputeWriteCoverage coverage,
        MapPostEffect3ComputeExecutor actualExecutor,
        MapPostEffect3ComputeFrameInputs actualInputs)
    {
        ArgumentNullException.ThrowIfNull(coverage);ArgumentNullException.ThrowIfNull(actualExecutor);
        ArgumentNullException.ThrowIfNull(actualInputs);
        coverage.RequireCurrent();
        if(!ReferenceEquals(actualExecutor.Bank,this)||coverage.SourceSha256!=Source.Source.Sha256||
            coverage.ContextOwnerIdentity!=Source.ContextOwnerIdentity||coverage.FrameNumber!=actualInputs.FrameNumber)
            throw new InvalidDataException("Eye compute transaction belongs to another actual bank/executor/frame.");
        if(coverage.RequiresPriorExposureTexelContents&&actualInputs.FreshUndefinedEyeExposureRead is null&&actualInputs.RetainedControlledExposureRead is null)
            throw new NotSupportedException("Eye CS52 positive adaptation needs its exact first source-undefined exposure admission or real prior history adapter.");
        if(coverage.SourcePassIndex==104)
        {
            // Original buffer8 is a typed uint SRV; the translated readonly
            // interface needs the existing exact-byte backend bridge after
            // CS51 invalidated its preceding view. This keeps all256 GPU bytes.
            var histogram=actualInputs.CurrentBuffers.SingleOrDefault(b=>b.ParameterIndex==8)
                ??throw new InvalidDataException("Eye exposure lacks its actual completed histogram producer.");
            histogram.RequireCurrent();histogram.RequireReadRange(0,256);
            var readable=await PrepareBufferShaderReadViewAsync(8,histogram.InstanceIndex,coverage.FrameNumber);
            if(readable.StorageRid!=histogram.StorageRid||readable.UploadRevision!=histogram.UploadRevision)
                throw new InvalidDataException("Eye readonly histogram bridge changed the selected completed source storage.");
            actualInputs=actualInputs with{CurrentBuffers=Array.AsReadOnly(new[]{readable})};
        }
        EyeAcceptedInputs acceptedInputs;
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            coverage.RequireCurrent();
            if(!ReferenceEquals(actualExecutor.Bank,this)||coverage.SourceSha256!=Source.Source.Sha256||
                coverage.ContextOwnerIdentity!=Source.ContextOwnerIdentity||coverage.FrameNumber!=actualInputs.FrameNumber)
                throw new InvalidDataException("Eye compute transaction belongs to another actual bank/executor/frame.");
            // A fresh original allocation supplies unspecified old pixels.
            // A retained exposure needs a separate real history producer.
            if(coverage.RequiresPriorExposureTexelContents&&actualInputs.FreshUndefinedEyeExposureRead is null&&actualInputs.RetainedControlledExposureRead is null)
                throw new NotSupportedException("Eye CS52 positive adaptation needs its exact first source-undefined exposure admission or real prior history adapter.");
            acceptedInputs=CaptureEyeInputs(coverage,actualInputs);
            accepted++;
        }
        try
        {
            var completed=await actualExecutor.DispatchAsync(coverage.SourcePassIndex,acceptedInputs.Submitted);
            lock(lifetime)
            {
                ObjectDisposedException.ThrowIf(retiring,this);
                RequireEyeCompletedTransaction(coverage,actualExecutor,completed,acceptedInputs);
                if(coverage.Table==MapPostEffect3BindingTable.BufferParameters)
                    return new(completed,PublishEyeHistogramTransaction(coverage,completed,acceptedInputs),null);
                if(acceptedInputs.Submitted.FreshUndefinedEyeExposureRead is{} initialExposure)
                    RequireFreshUndefinedEyeExposureCompleted(initialExposure,completed);
                if(acceptedInputs.Submitted.RetainedControlledExposureRead is{} retainedExposure)
                    RequireRetainedControlledExposureCompleted(retainedExposure,coverage,completed);
                var exposure=PublishSourceTextureCoverage(actualExecutor,completed,coverage.FrameNumber,
                    coverage.ContextOwnerIdentity,coverage.SourceSha256,coverage.SourcePassIndex,coverage.ProgramSha256,
                    coverage.ParameterIndex,0,coverage.TextureWidth,coverage.TextureHeight,coverage.ActualDispatch,coverage.LocalSize,
                    coverage.SourceEvidence+";actual-Eye-input-dispatch-publication-transaction;"+
                        (acceptedInputs.Submitted.FreshUndefinedEyeExposureRead is{} first?
                            first.SourceEvidence:acceptedInputs.Submitted.RetainedControlledExposureRead is{} retained?
                                retained.SourceEvidence:"source-no-prior-u0-load"),
                    coverage.RequiredConstantBufferBytesSha256,[],[8],[],[],[coverage.RequiredInputBufferBinding!]);
                return new(completed,null,exposure);
            }
        }
        finally
        {
            lock(lifetime)if(--accepted==0&&retiring)drained?.TrySetResult();
        }
    }

    private EyeAcceptedInputs CaptureEyeInputs(MapPostEffect3EyeComputeWriteCoverage coverage,
        MapPostEffect3ComputeFrameInputs actual)
    {
        int pass=coverage.SourcePassIndex;
        if(pass is not(102 or 103 or 104)||actual.LensExecution is not null||actual.LensFlare2Execution is not null||
            actual.DepthOfField2Prepared is not null||actual.DepthOfField2Before is not null||actual.ToneInfoSourceValueInfluence is not null||
            actual.CurrentSamplers.Count!=0||actual.NativeRegisterResetSource is not null)
            throw new InvalidDataException("Eye transaction cannot borrow another factory execution/old-value proof or implicit sampler.");
        var table=actual.TextureParameters??throw new InvalidDataException("Eye compute requires its actual source texture-parameter table.");
        if(!ReferenceEquals(table.Allocation,Source)||table.FrameNumber!=coverage.FrameNumber||
            table.ContextOwnerIdentity!=coverage.ContextOwnerIdentity||table.SourceSha256!=coverage.SourceSha256)
            throw new InvalidDataException("Eye compute current pointer table has another source allocation/frame.");
        int[] expectedTextures=pass==103?[79]:[];
        int[] expectedBuffers=pass==102?[]:[8];
        if(!actual.CurrentTextures.Select(t=>t.ParameterIndex).Order().SequenceEqual(expectedTextures)||
            !actual.CurrentBuffers.Select(b=>b.ParameterIndex).Order().SequenceEqual(expectedBuffers)||
            !actual.OutputBufferInstances.Keys.Order().SequenceEqual(pass==104?[]:new[]{8})||
            !actual.OutputTextureInstances.Keys.Order().SequenceEqual(pass==104?new[]{61}:[]))
            throw new InvalidDataException("Eye compute actual inputs/outputs differ from its exact original resources.");
        var cbs=new List<EyeCbInputSnapshot>();
        foreach(var requirement in coverage.RequiredConstantBufferBytesSha256)
        {
            if(!actual.ConstantBufferInstances.TryGetValue(requirement.Key,out int side))
                throw new InvalidDataException("Eye compute source CB24 instance is absent.");
            var cb=RequireCurrentConstantBuffer(requirement.Key,side,coverage.FrameNumber);
            if(cb.CompleteBytesSha256!=requirement.Value||cb.Source.Parameter.ByteCount!=64)
                throw new InvalidDataException("Eye compute actual CB24 upload differs from its complete original writer.");
            cbs.Add(new(requirement.Key,side,cb.StorageRid,cb.UploadRevision,requirement.Value));
        }
        var buffers=new List<EyeBufferInputSnapshot>();
        foreach(var input in actual.CurrentBuffers)
        {
            input.RequireCurrent();input.RequireReadRange(0,256);
            var native=RequireCurrentBuffer(8,input.InstanceIndex,coverage.FrameNumber);
            int expectedPrior=pass==103?102:103;
            if(input.FrameNumber!=coverage.FrameNumber||input.ContextOwnerIdentity!=coverage.ContextOwnerIdentity||
                input.StorageRid!=native.StorageRid||input.ByteCount!=256||input.UploadRevision!=native.UploadRevision||
                native.Shape.ViewFormat!=42||native.LastShaderWritePassIndex!=expectedPrior||
                native.LastShaderWriteFrameNumber!=coverage.FrameNumber||native.Writer!=MapPostEffect3BufferWriter.Compute)
                throw new InvalidDataException("Eye histogram input is not this ordered actual preceding native producer.");
            buffers.Add(new(8,input.InstanceIndex,input.StorageRid,input.ByteCount,input.UploadRevision,native.CompleteBytesSha256));
        }
        MapPostEffect3ResolvedTextureParameter? colorPointer=null;
        IMapPostEffect3ComputeTextureLease? color=null;
        if(pass==103)
        {
            color=actual.CurrentTextures.Single();color.RequireCurrent();
            if(color.FrameNumber!=coverage.FrameNumber||color.ContextOwnerIdentity!=coverage.ContextOwnerIdentity||
                !color.StorageRid.IsValid||!color.ShaderViewRid.IsValid||color.Extent.Z!=1||
                coverage.RequiredInputTexturePixelRegion is not{} region||region.X!=0||region.Y!=0||
                region.Width>color.Extent.X||region.Height>color.Extent.Y)
                throw new InvalidDataException("Eye CS51 lacks its actual current source image pixel range.");
            if(color is not IMapPostEffect3ComputeMipTextureLease mip)
                throw new NotSupportedException("Eye histogram source image must prove its actual initialized selected mip0.");
            mip.RequireReadableMipRange(0,1);
            colorPointer=table.Require(79);table.ValidateCurrent(colorPointer);
        }
        if(pass==104)
        {
            var output=table.Require(61);
            if(output.Reference.PhysicalAllocationParameterIndex!=61||output.Reference.InstanceIndex!=actual.OutputTextureInstances[61]||
                output.Reference.HasSourceSelectedMipView)
                throw new NotSupportedException("Eye owned exposure publication requires its actual unchanged physical texture61 mip0; an external target needs its own owner adapter.");
            if(actual.FreshUndefinedEyeExposureRead is{} first)
            {
                first.RequireCurrent();first.Source.RequireCurrentTextureTable(table);
                if(!ReferenceEquals(first.Bank,this)||!ReferenceEquals(first.Source.WriteCoverage,coverage)||
                    !coverage.PriorExposureTexelLoadExecutes||coverage.AllowsFreshUninitializedExposureStorage)
                    throw new InvalidDataException("Eye original undefined exposure load needs the exact same coverage and physical bank.");
            }
            else if(actual.RetainedControlledExposureRead is{} retained)
            {
                retained.RequireReadableMipRange(0,1);
                if(!ReferenceEquals(retained.Snapshot.Bank,this)||!ReferenceEquals(retained.Consumer,coverage)||
                    !coverage.RequiresPriorExposureTexelContents||actual.FreshUndefinedEyeExposureRead is not null)
                    throw new InvalidDataException("Eye retained prior load needs this exact coverage and same actual physical bank.");
            }
            else if(!coverage.AllowsFreshUninitializedExposureStorage||coverage.PriorExposureTexelLoadExecutes)
                throw new InvalidDataException("Eye CS52 uninitialized target has no exact source no-prior-load proof.");
        }
        else if(actual.FreshUndefinedEyeExposureRead is not null||actual.RetainedControlledExposureRead is not null)
            throw new InvalidDataException("Undefined exposure source admission applies only to exact CS52.");
        // Freeze collection structure before giving these exact inputs to the
        // executor, which additionally freezes and revalidates each producer.
        var submitted=actual with
        {
            CurrentTextures=Array.AsReadOnly(actual.CurrentTextures.ToArray()),
            CurrentSamplers=Array.AsReadOnly(actual.CurrentSamplers.ToArray()),
            CurrentBuffers=Array.AsReadOnly(actual.CurrentBuffers.ToArray()),
            ConstantBufferInstances=new Dictionary<int,int>(actual.ConstantBufferInstances),
            OutputTextureInstances=new Dictionary<int,int>(actual.OutputTextureInstances),
            OutputBufferInstances=new Dictionary<int,int>(actual.OutputBufferInstances),
            EyeExposureSourceValueInfluence=pass==104&&actual.FreshUndefinedEyeExposureRead is null&&actual.RetainedControlledExposureRead is null?coverage:null
        };
        return new(submitted,buffers.AsReadOnly(),cbs.AsReadOnly(),colorPointer,color);
    }

    private void RequireEyeCompletedTransaction(MapPostEffect3EyeComputeWriteCoverage coverage,
        MapPostEffect3ComputeExecutor executor,MapPostEffect3ComputeCompletedPass completed,EyeAcceptedInputs inputs)
    {
        coverage.RequireCurrent();
        if(!ReferenceEquals(executor.Bank,this)||!ReferenceEquals(executor.LastCompletedPass,completed)||
            completed.FrameNumber!=coverage.FrameNumber||completed.ContextOwnerIdentity!=coverage.ContextOwnerIdentity||
            completed.SourcePassIndex!=coverage.SourcePassIndex||completed.ProgramSha256!=coverage.ProgramSha256||
            completed.Dispatch!=coverage.ActualDispatch||completed.ActualShaderLocalSize!=new Vector3I((int)coverage.LocalSize.X,(int)coverage.LocalSize.Y,(int)coverage.LocalSize.Z))
            throw new InvalidDataException("Eye publication is not this exact actual submitted executor transaction.");
        foreach(var cb in inputs.ConstantBuffers)
        {
            var read=completed.ConstantBufferReads.SingleOrDefault(r=>r.ParameterIndex==cb.Parameter);
            var current=RequireCurrentConstantBuffer(cb.Parameter,cb.Side,coverage.FrameNumber);
            if(read is null||read.InstanceIndex!=cb.Side||read.StorageRid!=cb.Storage||read.UploadRevision!=cb.Revision||
                !read.HasCompleteWriterContents||read.CompleteBytesSha256!=cb.Hash||current.StorageRid!=cb.Storage||
                current.UploadRevision!=cb.Revision||current.CompleteBytesSha256!=cb.Hash)
                throw new InvalidDataException("Eye actual submitted CB24 input/owner/revision changed.");
        }
        foreach(var input in inputs.Buffers)
        {
            var read=completed.BufferReads.SingleOrDefault(r=>r.ParameterIndex==input.Parameter);
            if(read is null||read.InstanceIndex!=input.Side||read.StorageRid!=input.Storage||read.ByteCount!=input.Bytes||
                read.UploadRevision!=input.Revision||read.CompleteBytesSha256!=input.Hash)
                throw new InvalidDataException("Eye actual submitted prior/current histogram input differs from the accepted transaction.");
            if(coverage.SourcePassIndex==104)
            {
                var current=RequireCurrentBuffer(input.Parameter,input.Side,coverage.FrameNumber);
                if(current.StorageRid!=input.Storage||current.UploadRevision!=input.Revision)
                    throw new InvalidDataException("Eye CS52 histogram source was modified during exposure computation.");
            }
        }
        if(inputs.Color is{} color)
        {color.RequireCurrent();inputs.Submitted.TextureParameters!.ValidateCurrent(inputs.ColorPointer!);}
        foreach(var required in new[]{coverage.OutputBinding,coverage.RequiredInputTextureBinding,coverage.RequiredInputBufferBinding}.Where(b=>b is not null))
        {
            var actual=completed.OrderedEffectiveBindings.LastOrDefault(b=>b.Table==required!.Table&&b.Role==required.Role&&b.RegisterIndex==required.RegisterIndex&&(b.StageMask&1)!=0);
            if(actual is null||actual.ParameterIndex!=required!.ParameterIndex||actual.SourceRowIndex!=required.SourceRowIndex||
                actual.NativeViewKind!=required.NativeViewKind||actual.MipSelector!=required.MipSelector||actual.SliceSelector!=required.SliceSelector||
                actual.StageMask!=required.StageMask||actual.RawFlags0!=required.RawFlags0)
                throw new InvalidDataException("Eye actual submitted native last-effective resource setter differs from source coverage.");
        }
    }

    private IMapPostEffect3ComputeBufferLease PublishEyeHistogramTransaction(
        MapPostEffect3EyeComputeWriteCoverage coverage,MapPostEffect3ComputeCompletedPass completed,EyeAcceptedInputs inputs)
    {
        if(coverage.ParameterIndex!=8||coverage.ByteOffset!=0||coverage.ByteCount!=256||coverage.SourcePassIndex is not(102 or 103)||
            completed.Writes.Count!=0||completed.BufferWrites.Count!=1)
            throw new InvalidDataException("Eye histogram publication has another store domain or extra output.");
        var write=completed.BufferWrites.Single();
        var target=Require(Buffers,8,write.InstanceIndex,"covered original Eye histogram");
        if(write.ParameterIndex!=8||!write.SelectedShaderDeclaresWrite||write.ByteCount!=256||write.StorageRid!=target.StorageRid||
            write.WriterRevision!=target.UploadRevision||target.LastShaderWriteFrameNumber!=coverage.FrameNumber||
            target.LastShaderWritePassIndex!=coverage.SourcePassIndex||target.Shape.Dimension!=MapPostEffect3BufferDimension.Typed||
            target.Shape.ViewFormat!=42||target.Shape.ViewElementCount!=64)
            throw new InvalidDataException("Eye histogram source coverage belongs to another physical storage/side/current writer.");
        if(coverage.RequiresPriorHistogramBufferContents)
        {
            var prior=inputs.Buffers.Single();
            if(prior.Storage!=write.StorageRid||prior.Side!=write.InstanceIndex||write.WriterRevision!=checked(prior.Revision+1))
                throw new InvalidDataException("Eye atomic accumulation did not consume and replace the accepted prior histogram storage exactly once.");
        }
        target.HasCompleteWriterContents=true;target.CompleteFrameNumber=coverage.FrameNumber;target.Writer=MapPostEffect3BufferWriter.Compute;
        target.CompleteBytesSha256=coverage.SourceKnownCompleteBufferBytesSha256;
        target.CompleteSourceEvidence=coverage.SourceEvidence+";"+completed.SourceEvidence+
            ";actual-accepted-Eye-input-dispatch-publication-transaction;physical256B/current-writer-revision";
        return CaptureCurrentBufferLease(8,write.InstanceIndex,coverage.FrameNumber);
    }
}
