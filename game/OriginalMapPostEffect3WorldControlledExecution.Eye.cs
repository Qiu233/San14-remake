using San14.Data;

public static partial class OriginalMapPostEffect3WorldControlledExecution
{
    private static async Task ExecuteEyeGraphicsIterationAsync(OriginalMapPostEffect3WorldControlledFactoryFrame frame,
        OriginalMapPostEffect3ResourceBank bank,OriginalMapPostEffect3EyeBloomExecutionOwner actualEye,
        OriginalMapPostEffect3EyeBeforeBindings before,LoadedOriginalMapPostEffect3EyeExposureInitializationSource initialization)
    {
        var cpu=OriginalRenderThreadSchedulingDiagnostics.BeginCpuTrace("EyeIteration",before.Iteration.SourcePassIndex);
        frame.RequireCurrent();
        if(before.Iteration.IsCompute||!ReferenceEquals(actualEye.Bank,bank)||!ReferenceEquals(frame.World.Bank,bank))
            throw new InvalidDataException("Eye graphics iteration requires this actual selected factory frame and bank.");
        cpu?.Mark("01-frame-current");
        var context=await frame.Frontend.BindEyeGraphicsAsync(frame.Eye,before);
        cpu?.Mark("02-frontend-bind");
        var reads=new List<IOriginalMapPostEffect3ComputeTextureLease>();
        if(before.Iteration.NativeCachedPassOrdinal==0)
        {
            var coverage=OriginalMapPostEffect3EyeGraphicsWriteCoverage.Prepare(bank.Source,frame.Eye,before);
            foreach(var footprint in coverage.RequiredShaderReads)
            {
                IOriginalMapPostEffect3ComputeTextureLease read;
                if(footprint.SourceStageCode==0&&footprint.SourceRegister==0&&
                    footprint.NativeBinding.ParameterIndex==68&&footprint.Kind==OriginalMapPostEffect3ShaderReadFootprintKind.EntireSelectedMip)
                    read=frame.SceneColor.CaptureCurrentRead(68);
                else if(footprint.SourceStageCode==0&&footprint.SourceRegister==1&&footprint.NativeBinding.ParameterIndex==61)
                    read=frame.Begin.Counter>1?frame.ModuleLifetime!.CapturePriorExposure(frame,84,before):
                        bank.CaptureEyePrefilterExposureRead(frame.Textures,OriginalMapPostEffect3EyePrefilterExposureSource.Prepare(
                        initialization,frame.Eye,before,frame.Textures,frame.Begin,frame.ApplyViewSlot));
                else throw new NotSupportedException("Actual Eye prefilter needs an additional exact source texture producer.");
                read.RequireCurrent();reads.Add(read);
            }
        }
        cpu?.Mark("03-prefilter-read-footprints");
        var completed=await actualEye.DrawIterationAsync(frame.Eye,frame.Textures,before,context,reads,
            frame.AllocationSides.ConstantBufferInstances);
        cpu?.Mark("04-eye-draw");
        if(!completed.WasPixelDrawSubmitted||actualEye.LastPublishedMip is not{} written)
            throw new InvalidDataException("Selected Eye graphics did not publish its actual complete source target mip.");
        written.RequireReadableMipRange(checked((uint)before.TargetMipLevel!.Value),1);
        cpu?.Mark("05-published-mip-validation");
        frame.Frontend.AcceptEyeGraphics(actualEye,completed);
        cpu?.Mark("06-frontend-history-release");
    }
}
