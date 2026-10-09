using San14.Data;

/// <summary>Executes the selected post-processing directory and preserves module history between frames.</summary>
public static partial class MapPostEffect3WorldControlledExecution
{
    /// <summary>Executes the selected directory and returns its completed module publication.</summary>
    public static async Task<MapPostEffect3WorldControlledPortReturnReceipt> ExecuteAsync(
        GameResourceRepository repository,MapPostEffect3WorldControlledFactoryFrame frame,
        PostProcessResourceBank bank,MapPostEffect3ProgramLibrary programs,
        Vkd3dShaderCompiler compiler,MapPostEffect3StaticLutTextureOwner hdr,Action<string>? checkpoint=null)
    {
        try
        {
            await ExecuteSelectedDirectoryAsync(repository,frame,bank,programs,compiler,hdr,checkpoint);
            //All using owners have drained actual lists and retired temporary
            //bindings on return from the inner method. No live executor bool
            //is used as an End/whole-directory completion receipt.
            var directory=frame.DirectoryCursor!.CompleteDirectoryReturnedAndJoinHistory();
            var returned=frame.ModuleLifetime!.CompletePortLifecycle(frame,directory);
            frame.RequirePortReturned(directory);
            checkpoint?.Invoke("controlled-Functor1-managed-Begin-Finish-End-outer-registry-controller-return-completed");
            return returned;
        }
        catch
        {
            frame.ModuleLifetime?.MarkFailure();
            throw;
        }
    }

    private static async Task ExecuteSelectedDirectoryAsync(GameResourceRepository repository,
        MapPostEffect3WorldControlledFactoryFrame frame,PostProcessResourceBank bank,
        MapPostEffect3ProgramLibrary programs,Vkd3dShaderCompiler compiler,
        MapPostEffect3StaticLutTextureOwner hdr,Action<string>? checkpoint)
    {
        frame.RequireCurrent();
        if(!ReferenceEquals(frame.World.Bank,bank))
            throw new InvalidDataException("Actual controlled execution belongs to another current World resource bank.");
        var sources=frame.ModuleLifetime!.StaticSources;
        sources.RequireRepository(repository);
        var submitted=new List<int>();
        await using var lens=await MapPostEffect3LensFlare2ExecutionOwner.BuildForCurrentControlledFrameAsync(repository,frame,programs,compiler);
        var lensResult=await lens.ExecuteInitializationAsync(frame.Lens,frame.Textures,
            frame.AllocationSides.ConstantBufferInstances[17],new MapPostEffect3WorldLensFlare2Invocation(frame,lens));
        submitted.AddRange(lensResult.OrderedSelectedPasses);checkpoint?.Invoke("controlled-Lens45..49-and-source-After-mips-completed");
        var dofResources=frame.AcquireDepthOfFieldModuleResources(programs,bank);
        long previousStagingRevision=dofResources.CapturePhysicalRetention().StagingCopies
            .SingleOrDefault(c=>c.SourceSide28==frame.DepthOfField.NextController.SpriteSide28)?.ActualStagingWriteRevision??0;
        await using var dof=MapPostEffect3DepthOfField2ExecutionOwner.CreateBorrowingModuleResources(
            dofResources,frame.CpuInvocation,frame.Camera.State,
            frame.DepthOfField,frame.Textures,new MapPostEffect3WorldDepthOfField2GraphicsInvocation(frame,checkpoint,
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
        checkpoint?.Invoke("controlled-DOF2-physical-module-resources-retained-after-invocation-disposal");
        await using var eyeGraphics=await MapPostEffect3EyeBloomExecutionOwner.BuildForCurrentControlledFrameAsync(repository,frame,programs);
        await using var compute=MapPostEffect3ComputeExecutor.Create(programs,bank);
        await bank.UploadCompleteConstantBufferAsync(frame.Exposure,frame.AllocationSides.ConstantBufferInstances[24]);
        var execution=sources.EyeCompute;
        var exposureInitialization=sources.ExposureInitialization;
        IMapPostEffect3ComputeBufferLease? histogram=null;
        var computeReceipts=new List<MapPostEffect3ComputeCompletedPass>();
        MapPostEffect3WorldComposite2GraphicsInvocation? invocation=null;
        MapPostEffect3Composite2GraphicsOwner? composite=null;
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
                    invocation=MapPostEffect3WorldComposite2GraphicsInvocation.CaptureActualDirectoryPrefix(
                        frame,hdr,eyeGraphics,compute,computeReceipts,exposureInitialization);
                    composite=MapPostEffect3Composite2GraphicsOwner.Create(programs,bank,
                        sources.CompositeExecution,frame.Composite,frame.Textures,invocation,
                        frame.AllocationSides.ConstantBufferInstances[22]);
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
            var coverage=MapPostEffect3EyeComputeWriteCoverage.Prepare(bank.Source,recipe);
            MapPostEffect3FreshUndefinedEyeExposureRead? firstRead=null;
            MapPostEffect3RetainedControlledExposureRead? retainedExposure=null;
            if(coverage.RequiresPriorExposureTexelContents)
            {
                if(frame.Begin.Counter>1)
                    retainedExposure=(MapPostEffect3RetainedControlledExposureRead)frame.ModuleLifetime!.CapturePriorExposure(frame,104,coverage);
                else
                {
                    var initial=exposureInitialization.Prepare(bank.Source,coverage,frame.Textures,frame.Begin,frame.ApplyViewSlot);
                    firstRead=bank.CaptureFreshUndefinedEyeExposureRead(initial);
                }
            }
            var input=new MapPostEffect3ComputeFrameInputs(frame.Camera.FrameNumber,
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
        }
        finally{if(composite is not null)await composite.DisposeAsync();}
    }
}
