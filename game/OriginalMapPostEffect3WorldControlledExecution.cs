using San14.Data;

/// <summary>Executes the original selected fresh Main0 graph in directory
/// order, including the selected original Eye bloom iterations. Other source profiles
/// require their selected producers; they are never silently omitted.</summary>
public static partial class OriginalMapPostEffect3WorldControlledExecution
{
    /// <summary>Ordinary runtime entry. Source state selects the directory;
    /// the return is the actual module completion, without diagnostic JSON.</summary>
    public static async Task<OriginalMapPostEffect3WorldControlledPortReturnReceipt> ExecuteAsync(
        OriginalGameRepository repository,OriginalMapPostEffect3WorldControlledFactoryFrame frame,
        OriginalMapPostEffect3ResourceBank bank,OriginalMapPostEffect3ProgramLibrary programs,
        Vkd3dShaderCompiler compiler,OriginalMapPostEffect3StaticLutTextureOwner hdr,Action<string>? checkpoint=null)
    {
        var completed=await ExecuteAndReturnAsync(repository,frame,bank,programs,compiler,hdr,
            checkpoint,false,null,null,false);
        return completed.PortReturn??throw new InvalidDataException("Normal selected directory has not returned.");
    }

    public static async Task<object> ExecuteFreshAsync(OriginalGameRepository repository,
        OriginalMapPostEffect3WorldControlledFactoryFrame frame,OriginalMapPostEffect3ResourceBank bank,
        OriginalMapPostEffect3ProgramLibrary programs,Vkd3dShaderCompiler compiler,
        OriginalMapPostEffect3StaticLutTextureOwner hdr,Action<string>? checkpoint=null,bool stopAfterLens=false,
        Action<IOriginalMapPostEffect3ComputeMipTextureLease>? captureCompletedExposure=null,
        Func<OriginalMapPostEffect3Composite2GraphicsCoverageReceipt,Task>? completedCompositeDiagnostic=null,
        bool captureNumericDiagnostics=true)
    {
        var completed=await ExecuteAndReturnAsync(repository,frame,bank,programs,compiler,hdr,checkpoint,
            stopAfterLens,captureCompletedExposure,completedCompositeDiagnostic,captureNumericDiagnostics);
        if(completed.PortReturn is null)return completed.Diagnostics;
        var exported=System.Text.Json.JsonSerializer.SerializeToNode(completed.Diagnostics)!.AsObject();
        exported["portLifecycle"]=System.Text.Json.JsonSerializer.SerializeToNode(completed.PortReturn.CaptureEvidence());
        return exported;
    }

    private sealed record ExecutionReturn(object Diagnostics,OriginalMapPostEffect3WorldControlledPortReturnReceipt? PortReturn);
    private static async Task<ExecutionReturn> ExecuteAndReturnAsync(OriginalGameRepository repository,
        OriginalMapPostEffect3WorldControlledFactoryFrame frame,OriginalMapPostEffect3ResourceBank bank,
        OriginalMapPostEffect3ProgramLibrary programs,Vkd3dShaderCompiler compiler,
        OriginalMapPostEffect3StaticLutTextureOwner hdr,Action<string>? checkpoint,bool stopAfterLens,
        Action<IOriginalMapPostEffect3ComputeMipTextureLease>? captureCompletedExposure,
        Func<OriginalMapPostEffect3Composite2GraphicsCoverageReceipt,Task>? completedCompositeDiagnostic,
        bool captureNumericDiagnostics)
    {
        try
        {
            var result=await ExecuteSelectedDirectoryAsync(repository,frame,bank,programs,compiler,hdr,checkpoint,
                stopAfterLens,captureCompletedExposure,completedCompositeDiagnostic,captureNumericDiagnostics);
            if(stopAfterLens)return new(result,null);
            //All using owners have drained actual lists and retired temporary
            //bindings on return from the inner method. No live executor bool
            //is used as an End/whole-directory completion receipt.
            var directory=frame.DirectoryCursor!.CompleteDirectoryReturnedAndJoinHistory();
            var returned=frame.ModuleLifetime!.CompletePortLifecycle(frame,directory);
            frame.RequirePortReturned(directory);
            checkpoint?.Invoke("controlled-Functor1-managed-Begin-Finish-End-outer-registry-controller-return-completed");
            return new(result,returned);
        }
        catch
        {
            frame.ModuleLifetime?.MarkFailure();
            throw;
        }
    }

    private static async Task<object> ExecuteSelectedDirectoryAsync(OriginalGameRepository repository,
        OriginalMapPostEffect3WorldControlledFactoryFrame frame,OriginalMapPostEffect3ResourceBank bank,
        OriginalMapPostEffect3ProgramLibrary programs,Vkd3dShaderCompiler compiler,
        OriginalMapPostEffect3StaticLutTextureOwner hdr,Action<string>? checkpoint=null,bool stopAfterLens=false,
        Action<IOriginalMapPostEffect3ComputeMipTextureLease>? captureCompletedExposure=null,
        Func<OriginalMapPostEffect3Composite2GraphicsCoverageReceipt,Task>? completedCompositeDiagnostic=null,
        bool captureNumericDiagnostics=true)
    {
        frame.RequireCurrent();
        if(!ReferenceEquals(frame.World.Bank,bank))
            throw new InvalidDataException("Actual controlled execution belongs to another current World resource bank.");
        var sources=frame.ModuleLifetime!.StaticSources;
        sources.RequireRepository(repository);
        var submitted=new List<int>();
        await using var lens=await OriginalMapPostEffect3LensFlare2ExecutionOwner.BuildForCurrentControlledFrameAsync(repository,frame,programs,compiler);
        var lensResult=await lens.ExecuteInitializationAsync(frame.Lens,frame.Textures,
            frame.AllocationSides.ConstantBufferInstances[17],new OriginalMapPostEffect3WorldLensFlare2Invocation(frame,lens));
        submitted.AddRange(lensResult.OrderedSelectedPasses);checkpoint?.Invoke("controlled-Lens45..49-and-source-After-mips-completed");
        if(stopAfterLens)
        {
            frame.World.RevalidateStaticMaskSourceAcceptanceBoundary();
            return new{actualWorldFrame=frame.Camera.FrameNumber,sourceFunctor=1,actualBeginCounter=frame.Begin.Counter,
                actualLensSourcePrefixCompleted=true,submittedPasses=submitted,lensAfterMipDraws=lensResult.AfterMips.Count,
                selectedFunctorCompleted=false,correctFinalAccepted=false,images=0};
        }
        var dofResources=frame.AcquireDepthOfFieldModuleResources(programs,bank);
        long previousStagingRevision=dofResources.CapturePhysicalRetention().StagingCopies
            .SingleOrDefault(c=>c.SourceSide28==frame.DepthOfField.NextController.SpriteSide28)?.ActualStagingWriteRevision??0;
        await using var dof=OriginalMapPostEffect3DepthOfField2ExecutionOwner.CreateBorrowingModuleResources(
            dofResources,frame.CpuInvocation,frame.Camera.State,
            frame.DepthOfField,frame.Textures,new OriginalMapPostEffect3WorldDepthOfField2GraphicsInvocation(frame,checkpoint,
                sources.ExposureInitialization),
            frame.AllocationSides.ConstantBufferInstances);
        var dofResult=await dof.ExecuteAsync();
        if(!dofResult.ProvesAllSelectedPassesWereSubmitted)
            throw new InvalidDataException("Actual DOF2 completion omitted a selected source pass.");
        submitted.AddRange(dofResult.OrderedSubmittedSourcePassIndices);checkpoint?.Invoke("controlled-DOF2-selected-sprite-graph-completed");
        await dof.DisposeAsync();
        var dofRetention=dofResources.CapturePhysicalRetention();
        dofRetention.RequirePhysicalCurrent();
        var retainedCopy=dofRetention.StagingCopies.SingleOrDefault(c=>c.FrameNumber==frame.Camera.FrameNumber&&
            c.SourcePassIndex==63&&c.SourceSide28==frame.DepthOfField.NextController.SpriteSide28);
        if(dofRetention.LastCompletedComponentFrameNumber!=frame.Camera.FrameNumber||
            dofRetention.ShapeWriterFrameNumber<=0||dofRetention.ShapeWriterFrameNumber>frame.Camera.FrameNumber||
            frame.DepthOfField.PassSelection.OrderedFactory7Passes.Contains(53)&&dofRetention.ShapeWriterFrameNumber!=frame.Camera.FrameNumber||
            !dofRetention.ShapeHasActualCompletePixels||
            !dofRetention.ShapeStorageRid.IsValid||retainedCopy is null||
            retainedCopy.FrameNumber!=frame.Camera.FrameNumber||retainedCopy.SourcePassIndex!=63||
            retainedCopy.SourceSide28!=frame.DepthOfField.NextController.SpriteSide28||
            retainedCopy.ActualStagingWriteRevision!=checked(previousStagingRevision+
                dofResult.OrderedDraws.Count(d=>d.SourcePassIndex is 62 or 63))||retainedCopy.ByteCount!=32)
            throw new InvalidDataException("Actual DOF module storage did not survive disposal of its completed invocation.");
        var retainedArguments=dofResources.CaptureRetainedArgumentContents(frame.AllocationSides.BufferInstances[5]);
        string? retainedArgumentsSha=captureNumericDiagnostics?await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            dofRetention.RequirePhysicalCurrent();retainedArguments.RequireReadRange(0,32);
            if(!rd.TextureIsValid(dofRetention.ShapeStorageRid))
                throw new InvalidDataException("Actual retained DOF shape was freed by invocation disposal.");
            byte[] actual=rd.BufferGetData(retainedArguments.StorageRid,0,32);
            byte[] copied=rd.BufferGetData(retainedCopy.ActualStagingStorageRid,0,32);
            if(actual.Length!=32||!actual.AsSpan().SequenceEqual(copied))
                throw new InvalidDataException("Actual retained DOF staging contents differ from the last source argument copy.");
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(copied));
        }):null;
        checkpoint?.Invoke("controlled-DOF2-physical-module-resources-retained-after-invocation-disposal");
        await using var eyeGraphics=await OriginalMapPostEffect3EyeBloomExecutionOwner.BuildForCurrentControlledFrameAsync(repository,frame,programs);
        await using var compute=OriginalMapPostEffect3ComputeExecutor.Create(programs,bank);
        await bank.UploadCompleteConstantBufferAsync(frame.Exposure,frame.AllocationSides.ConstantBufferInstances[24]);
        var execution=sources.EyeCompute;
        var exposureInitialization=sources.ExposureInitialization;
        IOriginalMapPostEffect3ComputeBufferLease? histogram=null;
        var computeReceipts=new List<OriginalMapPostEffect3ComputeCompletedPass>();
        OriginalMapPostEffect3WorldComposite2GraphicsInvocation? invocation=null;
        OriginalMapPostEffect3Composite2GraphicsOwner? composite=null;
        int eyeIterationIndex=0;
        try
        {
        foreach(int selectedPass in frame.OrderedSelectedPasses.Skip(submitted.Count))
        {
            frame.RequireCurrent();
            if(frame.Composite.Prepared.PassSelection.OrderedFactory9Passes.Contains(selectedPass))
            {
                if(invocation is null)
                {
                    invocation=OriginalMapPostEffect3WorldComposite2GraphicsInvocation.CaptureActualDirectoryPrefix(
                        frame,hdr,eyeGraphics,compute,computeReceipts,exposureInitialization);
                    composite=OriginalMapPostEffect3Composite2GraphicsOwner.Create(programs,bank,
                        sources.CompositeExecution,frame.Composite,frame.Textures,invocation,
                        frame.AllocationSides.ConstantBufferInstances[22]);
                    composite.CompletedDrawDiagnostic=completedCompositeDiagnostic;
                }
                var drawn=await invocation.ExecuteControlledPassAsync(composite!,selectedPass);
                submitted.Add(drawn.SourcePassIndex);
                checkpoint?.Invoke("controlled-Composite-source"+selectedPass+"-in-original-directory-order-completed");
                continue;
            }
            if(eyeIterationIndex>=frame.Eye.OrderedIterations.Count||frame.Eye.OrderedIterations[eyeIterationIndex].SourcePassIndex!=selectedPass)
                throw new InvalidDataException("Remaining source directory command is not the next actual Eye iteration or selected Composite wrapper.");
            var iteration=frame.Eye.OrderedIterations[eyeIterationIndex++];
            var before=frame.EyeOwner.PrepareBefore(frame.Eye,iteration,frame.EyeInputs);
            if(!iteration.IsCompute)
            {
                await ExecuteEyeGraphicsIterationAsync(frame,bank,eyeGraphics,before,exposureInitialization);
                submitted.Add(iteration.SourcePassIndex);
                checkpoint?.Invoke("controlled-Eye-source"+iteration.SourcePassIndex+"-loop"+iteration.NativeLoopIndex+"-graphics-completed");
                continue;
            }
            var recipe=execution.Prepare(bank.Source,frame.Eye,before,frame.Exposure);
            var coverage=OriginalMapPostEffect3EyeComputeWriteCoverage.Prepare(bank.Source,recipe);
            OriginalMapPostEffect3FreshUndefinedEyeExposureRead? firstRead=null;
            OriginalMapPostEffect3RetainedControlledExposureRead? retainedExposure=null;
            if(coverage.RequiresPriorExposureTexelContents)
            {
                if(frame.Begin.Counter>1)
                    retainedExposure=(OriginalMapPostEffect3RetainedControlledExposureRead)frame.ModuleLifetime!.CapturePriorExposure(frame,104,coverage);
                else
                {
                    var initial=exposureInitialization.Prepare(bank.Source,coverage,frame.Textures,frame.Begin,frame.ApplyViewSlot);
                    firstRead=bank.CaptureFreshUndefinedEyeExposureRead(initial);
                }
            }
            var input=new OriginalMapPostEffect3ComputeFrameInputs(frame.Camera.FrameNumber,
                iteration.SourcePassIndex==103?[frame.SceneColor.CaptureCurrentRead(79)]:[],[],
                iteration.SourcePassIndex==102?new Dictionary<int,int>():new Dictionary<int,int>{{24,frame.AllocationSides.ConstantBufferInstances[24]}},
                iteration.SourcePassIndex==104?new Dictionary<int,int>{{61,frame.AllocationSides.TextureInstances[61]}}:new Dictionary<int,int>())
            {
                TextureParameters=frame.Textures,FreshUndefinedEyeExposureRead=firstRead,RetainedControlledExposureRead=retainedExposure,
                CurrentBuffers=iteration.SourcePassIndex==102?[]:[histogram??throw new InvalidDataException("Eye source preceding histogram is unavailable.")],
                OutputBufferInstances=iteration.SourcePassIndex==104?new Dictionary<int,int>():
                    new Dictionary<int,int>{{8,frame.AllocationSides.BufferInstances[8]}}
            };
            frame.Frontend.BindEyeCompute(frame.Eye,before);
            var published=await bank.ExecuteAndPublishEyeComputeAsync(coverage,compute,input);
            frame.Frontend.AcceptEyeCompute(frame.Eye,before,compute,published.Completed);
            if(published.Histogram is{} currentHistogram)histogram=currentHistogram;
            if(published.Exposure is{} completedExposure)
            {
                completedExposure.RequireReadableMipRange(0,1);
                captureCompletedExposure?.Invoke(completedExposure);
            }
            computeReceipts.Add(published.Completed);submitted.Add(iteration.SourcePassIndex);
            checkpoint?.Invoke("controlled-Eye-source"+iteration.SourcePassIndex+"-completed");
        }
        var result=composite?.LastCompleted??throw new InvalidDataException("Actual interleaved Composite selected directory did not return.");
        if(!result.ProvesAllSelectedGraphicsWereSubmitted||eyeIterationIndex!=frame.Eye.OrderedIterations.Count)
            throw new InvalidDataException("Actual Composite2 completion omitted selected graphics.");
        var expectedCommands=frame.WrapperDirectory.Wrappers.SelectMany(recipe=>
            Enumerable.Repeat(recipe.SourcePassIndex,recipe.ExpectedSuccessfulDeviceIterationCount)).ToArray();
        if(!submitted.SequenceEqual(expectedCommands)||!expectedCommands.SequenceEqual(frame.OrderedSelectedPasses))
            throw new InvalidDataException("Actual current World submissions differ from its original selected directory order.");
        frame.World.RevalidateStaticMaskSourceAcceptanceBoundary();frame.RequireCurrent();
        checkpoint?.Invoke("controlled-actual-selected-Functor1-graphics-and-compute-completed");
        if(!captureNumericDiagnostics)return new{actualWorldFrame=frame.Camera.FrameNumber,
            actualSelectedGraphSubmitted=true,finalGraphicsPass=result.FinalOutputDraw.SourcePassIndex};
        return new{actualWorldFrame=frame.Camera.FrameNumber,sourceFunctor=1,actualBeginCounter=frame.Begin.Counter,
            selectedPasses=frame.OrderedSelectedPasses,submittedPasses=submitted,
            expectedSourceCommandPasses=expectedCommands,
            selectedEyeIterations=frame.Eye.OrderedIterations.Select(i=>new{i.SourcePassIndex,i.NativeCachedPassOrdinal,
                i.NativeLoopIndex,i.NativeLoopCount,i.IsCompute}).ToArray(),
            actualEyeGraphics=eyeGraphics.CompletedDraws.Select(d=>new{d.SourcePassIndex,d.VertexProgramSha256,d.PixelProgramSha256,d.Writes}).ToArray(),
            actualEyeCompute=computeReceipts.Select(d=>new{d.SourcePassIndex,d.ProgramSha256,d.Dispatch}).ToArray(),
            lensAfterMipDraws=lensResult.AfterMips.Count,dofSpriteCounts=dofResult.ActualGpuSpriteCounts,
            dofSourceController=new{beforeCount10=frame.DepthOfField.PreviousController.Count10,
                preparedCount10=frame.DepthOfField.NextController.Count10,finishedCount10=dofResult.FinishedController.Count10,
                frame.DepthOfField.RequiresSpriteCountReadback,frame.DepthOfField.NextController.RearScatterEnabled14,
                frame.DepthOfField.NextController.ForeScatterEnabled15,
                actualBefore57StagingMap=dof.ActualSpriteCountReadback is{} mapped?
                    new{mapped.ActualCopy,mapped.SourceReadback}:null},
            dofRetainedAfterInvocationDisposal=new{shapeRid=dofRetention.ShapeStorageRid.Id,
                dofRetention.ShapeWriterFrameNumber,dofRetention.ShapeContentsRevision,
                dofRetention.ShapeNativeFlags46,dofRetention.ShapeNativeMipCount45,dofRetention.ShapeNativeViewFormat,
                dofRetention.StagingCopies,dofRetention.LastCompletedComponentFrameNumber,
                retainedArgumentsSha,
                dofRetention.CommitsFunctorHistory,dofRetention.ProvesNextFramePixelsReady},
            actualSelectedGraphSubmitted=true,finalGraphicsPass=result.FinalOutputDraw.SourcePassIndex,
            nativeControllerReturnAndRetainedHistoryCommitted=false,originalComparisonAccepted=false,images=0};
        }
        finally{if(composite is not null)await composite.DisposeAsync();}
    }
}
