using Godot;
using San14.Data;

public sealed partial class MapPostEffect3WorldNativeFullscreenFrontendOwner
{
    private EyeComputeBinding? actualEyeCompute;
    private MapPostEffect3EyeAdaptation2Frame? actualEyeComputeFrame;
    private readonly List<MapPostEffect3ComputeCompletedPass> completedEyeCompute=[];
    public IReadOnlyList<MapPostEffect3ComputeCompletedPass> CompletedEyeCompute=>completedEyeCompute.AsReadOnly();

    /// <summary>Original Eye5/6/7 Before, CS Select/mode0 and resource Bind.
    /// The selected native mask always executes102..104; AutoExposure does
    /// not waive a command. No target row is pushed and no contents are granted.</summary>
    public void BindEyeCompute(MapPostEffect3EyeAdaptation2Frame frame,
        MapPostEffect3EyeBeforeBindings before)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(before);
        RequireCurrent();RequireReleasedBeforeNextBind();frame.RequireCurrent();
        int pass=before.Iteration.SourcePassIndex;
        var selected=MapPostEffect3PassStateReader.Read(World.Bank.Source.Source,Source.Locale)[pass];
        if(!ReferenceEquals(frame.Allocation,World.Bank.Source)||frame.FrameNumber!=World.Context.FrameNumber||
            frame.OwnerIdentity!=World.Bank.Source.ContextOwnerIdentity||!frame.OrderedIterations.Contains(before.Iteration)||
            pass is <102 or >104||before.Iteration.NativeCachedPassOrdinal!=pass-97||!before.RequiresGenericComputeDispatch||
            before.Iteration.NativeLoopIndex!=0||before.Iteration.NativeLoopCount!=1||before.NativeDrawCommand.Length!=0||
            before.SourceTextureParameterIndex is not null||before.TargetTextureParameterIndex is not null||before.BloomLoopConstants is not null||
            selected.Factory.Index!=8||selected.AutomaticExecution!=MapPostEffect3AutomaticExecution.ComputeDispatch||
            World.Bank.Source.Source.Passes[pass].ProgramReferences.Where(r=>r.StageIndex is not null).ToArray() is not [{StageCode:5}]||
            (frame.NativeMask1E8&(1u<<before.Iteration.NativeCachedPassOrdinal))==0||
            !frame.OrderedIterations.Where(i=>i.IsCompute).Select(i=>i.SourcePassIndex).SequenceEqual(new[]{102,103,104})||
            pass!=102+completedEyeCompute.Count||actualEyeComputeFrame is not null&&!ReferenceEquals(actualEyeComputeFrame,frame))
            throw new InvalidDataException("Eye compute Bind requires this actual next102/103/104 iteration and its released preceding native row.");
        // Select CS/mode0 and reset bindings when switching from graphics.
        // The previous standard wrapper also reset all four targets;
        // compute inherits no color RT.
        directoryCursor?.EnterSelectedBefore(pass,before.Iteration.NativeLoopIndex);
        directoryCursor?.RecordBeforeReturned(before,before);directoryCursor?.ApplyEyeBefore(before);
        actualEyeComputeFrame=frame;actualEyeCompute=new(frame,before);revision=checked(revision+1);
        directoryCursor?.RecordBindReturned(before,actualEyeCompute);
    }

    public void AcceptEyeCompute(MapPostEffect3EyeAdaptation2Frame frame,
        MapPostEffect3EyeBeforeBindings before,MapPostEffect3ComputeExecutor actualExecutor,
        MapPostEffect3ComputeCompletedPass completed)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(actualExecutor);ArgumentNullException.ThrowIfNull(completed);
        RequireCurrent();frame.RequireCurrent();var binding=actualEyeCompute??
            throw new InvalidOperationException("Eye compute Release needs its actual selected source resource binding first.");
        int pass=before.Iteration.SourcePassIndex;
        var reference=World.Bank.Source.Source.Passes[pass].ProgramReferences.Single(r=>r.StageCode==5);
        var shader=World.Bank.Source.Source.GetProgram(5,reference.StageIndex!.Value);
        var expectedBindings=World.Bank.Source.Passes[pass].ActiveBindings.Where(b=>(b.StageMask&1)!=0).ToArray();
        Vector3I local=pass switch{102=>new(64,1,1),103=>new(8,8,1),104=>new(1,1,1),
            _=>throw new InvalidDataException("Eye compute Release is outside its source102..104 sequence.")};
        if(!ReferenceEquals(binding.Frame,frame)||!ReferenceEquals(binding.Before,before)||
            !ReferenceEquals(actualExecutor.Bank,World.Bank)||!ReferenceEquals(actualExecutor.LastCompletedPass,completed)||
            completed.SourcePassIndex!=pass||completed.FrameNumber!=frame.FrameNumber||completed.ContextOwnerIdentity!=frame.OwnerIdentity||
            completed.ProgramSha256!=shader.Sha256||completed.Dispatch!=World.Bank.Source.Passes[pass].DispatchExtent||
            completed.ActualShaderLocalSize!=local||
            completed.OrderedEffectiveBindings.Count!=expectedBindings.Length||
            !completed.OrderedEffectiveBindings.Zip(expectedBindings).All(pair=>
                (pair.First with{RawBytes=pair.Second.RawBytes})==pair.Second&&
                pair.First.RawBytes.Span.SequenceEqual(pair.Second.RawBytes.Span)))
            throw new InvalidDataException("Eye compute Release requires its actual selected original shader/dispatch/ordered binding receipt.");
        // Issue the automatic CS command after native custom Execute AL1;
        // the standard wrapper resets every cached resource/target.
        //Pixel/buffer publication is separately proved by the real bank
        //coverage helper, never by binding or this lifecycle acceptance.
        var history=submissionLedger?.CaptureEyeCompute(this,frame,before,actualExecutor,completed);
        completedEyeCompute.Add(completed);actualEyeCompute=null;
        if(history is not null)submissionLedger!.AcceptAfterActualRelease(history);
    }
    private sealed record EyeComputeBinding(MapPostEffect3EyeAdaptation2Frame Frame,
        MapPostEffect3EyeBeforeBindings Before);
}
