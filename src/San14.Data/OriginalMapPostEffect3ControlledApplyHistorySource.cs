namespace San14.Data;

/// <summary>The EB5D50 post-return bit23 writer and its next E22860 argument.
/// Only this known bit is modeled; the other Apply1A0 bits are not inferred.</summary>
public sealed class LoadedOriginalMapPostEffect3ControlledApplyHistorySource
{
    internal LoadedOriginalMapPostEffect3ControlledApplyHistorySource(LoadedOriginalDeferredSamplerNativeSource native,string evidence)
        =>(Native,SourceEvidence)=(native,evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public bool ProvesGpuCompletion=>false;
    public OriginalMapPostEffect3ControlledApplyHistoryState ConstructFreshOwned(
        OriginalMapPostEffect3Functor1ModuleState module)=>new(this,module);
    /// <summary>Pure source candidate for independent branch/oracle checks.
    /// A returning AL alone grants no actual module or pixel authority.</summary>
    public bool ReadBit23Candidate(bool previousBit23,byte actualControllerReturnAl,
        int actualCallIndex,int actualCallCount,int actualGatherSizeTypeC0)
    {
        // The source compares AL against zero, then index == count-1. It
        // preserves bit23 on failure or an intermediate Apply call.
        if(actualControllerReturnAl==0||actualCallIndex!=unchecked(actualCallCount-1))return previousBit23;
        return actualGatherSizeTypeC0!=0;
    }
}

/// <summary>Module-owned narrow Apply continuation. CPU source receipt only:
/// completing this bit does not publish a texture or grant GPU completion.</summary>
public sealed class OriginalMapPostEffect3ControlledApplyHistoryState
{
    private readonly LoadedOriginalMapPostEffect3ControlledApplyHistorySource source;
    private readonly OriginalMapPostEffect3Functor1ModuleState module;
    internal OriginalMapPostEffect3ControlledApplyHistoryState(
        LoadedOriginalMapPostEffect3ControlledApplyHistorySource source,OriginalMapPostEffect3Functor1ModuleState module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if(module.HasUnreturnedFailure||module.DepthOfFieldController.FrameNumber!=0)
            throw new InvalidDataException("Apply bit23 construction requires the same fresh owned source module.");
        this.source=source;this.module=module;
    }
    public bool Bit23{get;private set;}
    public long LastReturnedFrameNumber{get;private set;}
    public bool ProvesGpuCompletion=>false;
    public bool ProvesTexturePixelsReady=>false;
    public string SourceEvidence=>source.SourceEvidence+
        ";module-owned-narrow-Apply1A0.bit23;fresh-source-bit0;other-1A0-bits-unclaimed";
    public byte ReadNextControllerArgument5(OriginalMapPostEffect3Functor1ModuleState actualModule,long actualPreviousFrame)
    {
        if(!ReferenceEquals(module,actualModule)||module.HasUnreturnedFailure||
            actualPreviousFrame!=LastReturnedFrameNumber||actualPreviousFrame<=0)
            throw new InvalidDataException("Next Execute arg5 requires this exact module's preceding source Apply post-return writer.");
        return Bit23?(byte)1:(byte)0;
    }
    public void ApplyReturnedSingleMainCall(OriginalMapPostEffect3Functor1SourceReceipt actualControllerReturn,
        OriginalMapPostEffect3OwnedControlledCameraFrame actualCamera)
    {
        ArgumentNullException.ThrowIfNull(actualControllerReturn);ArgumentNullException.ThrowIfNull(actualCamera);
        var invocation=actualControllerReturn.Invocation;
        invocation.RequireOwnedModule(module);actualControllerReturn.RequireCurrent();
        if(actualControllerReturn.Operation!=OriginalMapPostEffect3Functor1SourceOperation.ControllerReturned||
            !invocation.IsControllerReturned||actualCamera.Invocation.NativeControllerReturn!=true||
            !ReferenceEquals(invocation.State,actualCamera.State)||!ReferenceEquals(actualCamera.Allocation,module.Allocation)||
            actualCamera.FrameNumber!=invocation.FrameNumber||actualCamera.FrameNumber!=LastReturnedFrameNumber+1||
            actualCamera.CaptureAcceptedCamera().ExecutableLocale!=source.Native.Locale)
            throw new InvalidDataException("Apply history postwriter must follow the exact returned current Camera/controller/module source protocol.");
        // E9E6B0's actual node193 source caller passes index0/count1. The
        // controlled Main profile admits this one invocation and View0 only.
        int c0=actualCamera.Invocation.Snapshot.EnableDofA4;
        Bit23=source.ReadBit23Candidate(Bit23,1,0,1,c0);
        LastReturnedFrameNumber=actualCamera.FrameNumber;
    }
}

public static class OriginalMapPostEffect3ControlledApplyHistorySource
{
    public static LoadedOriginalMapPostEffect3ControlledApplyHistorySource Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadControlledApplyHistorySource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3ControlledApplyHistorySource ReadControlledApplyHistorySource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {

        return new(native,native.RenderRuleIdentity+
            $";{native.Locale}:paired-Main-Apply-history-profile;TC-reference-role:E9E6B0-node193-callIndex0/count1;EB5F0A-Camera1320&7-localC0;EB5F29/30/33/38-prior-bit23->arg5;"+
            "EB7F79-E22860/EB7F7E-returnAL;EB7FFE-success/EB8004..12-lastcall;EB801C-C0nonzero-bit23set/EB8035-C0zero-bit23clear;"+
            "failure-and-intermediate-call-preserve;CPU-source-only/no-GPU-or-pixel-readiness");
    }
}
