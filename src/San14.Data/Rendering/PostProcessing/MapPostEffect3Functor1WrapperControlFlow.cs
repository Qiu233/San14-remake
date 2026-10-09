namespace San14.Data;

/// <summary>Caller-observed CPU callback bytes, or explicit inert oracle
/// fixtures. Nullable returned values mean that callback did not return.
/// Bind AL is recorded although the native wrapper does not test it.</summary>
public sealed record MapPostEffect3Functor1CpuCallbackAttempt(byte BeforeReturnAl,
    byte? BindReturnAl,byte? ExecuteReturnAl,byte? AfterReturnAl);
public sealed record MapPostEffect3Functor1CpuControlFlowObservation(
    byte SelectionReturnAl,IReadOnlyList<MapPostEffect3Functor1CpuCallbackAttempt> CallbackAttempts,string SourceEvidence);

public sealed class MapPostEffect3Functor1CpuWrapperTrace
{
    internal MapPostEffect3Functor1CpuWrapperTrace(MapPostEffect3Functor1WrapperRecipe recipe,
        IReadOnlyList<MapPostEffect3PassOperation> operations,bool returned,int completed,string evidence)
        =>(Recipe,Operations,FunctionReturned,CompletedCallbackIterations,SourceEvidence)=(recipe,operations,returned,completed,evidence);
    public MapPostEffect3Functor1WrapperRecipe Recipe{get;}
    public IReadOnlyList<MapPostEffect3PassOperation> Operations{get;}
    public bool FunctionReturned{get;}
    public int CompletedCallbackIterations{get;}
    public byte? SourceFunctionReturnAl=>FunctionReturned?(byte)1:null;
    public bool ProvesGpuSubmission=>false;
    public bool CommitsFunctorHistory=>false;
    public string SourceEvidence{get;}
}

public static class MapPostEffect3Functor1WrapperControlFlow
{
    /// <summary>Replay the original wrapper branches from explicit CPU
    /// observations. This does not assert those bytes were observed in the
    /// original process, or upgrade a callback AL into a device submission.</summary>
    public static MapPostEffect3Functor1CpuWrapperTrace Read(
        MapPostEffect3Functor1WrapperRecipe recipe,MapPostEffect3Functor1CpuControlFlowObservation observations)
    {
        ArgumentNullException.ThrowIfNull(recipe);ArgumentNullException.ThrowIfNull(observations);
        ArgumentException.ThrowIfNullOrWhiteSpace(observations.SourceEvidence);
        var ops=new List<MapPostEffect3PassOperation>{MapPostEffect3PassOperation.SelectPass};
        int used=0,completed=0;bool returned=true;
        if(observations.SelectionReturnAl!=0)
        {
            for(int loop=0;loop<recipe.NativeWrapperLoopBound;loop++)
            {
                if(used>=observations.CallbackAttempts.Count)throw new InvalidDataException("CPU callback trace ends without a returned or unreturned source branch.");
                var a=observations.CallbackAttempts[used++];ops.Add(MapPostEffect3PassOperation.BeforeHook);
                if(!recipe.MaskAdmitsBefore&&a.BeforeReturnAl!=0)
                    throw new InvalidDataException("Native mask-rejected Before cannot observe nonzero AL.");
                if(a.BeforeReturnAl==0)
                {
                    if(a.BindReturnAl is not null||a.ExecuteReturnAl is not null||a.AfterReturnAl is not null)
                        throw new InvalidDataException("Before AL0 cannot reach Bind/Execute/After.");
                    break;
                }
                ops.Add(MapPostEffect3PassOperation.BindProgramsAndResources);
                if(a.BindReturnAl is null){returned=false;break;}
                ops.Add(MapPostEffect3PassOperation.ExecuteHook);
                if(a.ExecuteReturnAl is null){returned=false;break;}
                if(a.ExecuteReturnAl==0)
                {if(a.AfterReturnAl is not null)throw new InvalidDataException("Execute AL0 cannot reach After.");break;}
                ops.Add(MapPostEffect3PassOperation.AutomaticExecuteAndRelease);
                ops.Add(MapPostEffect3PassOperation.AfterHook);
                if(a.AfterReturnAl is null){returned=false;break;}
                completed++;
                if(recipe.FactoryIndex!=8||a.AfterReturnAl==0)break;
            }
            if(returned)ops.Add(MapPostEffect3PassOperation.ResetPass);
        }
        if(used!=observations.CallbackAttempts.Count)
            throw new InvalidDataException("CPU wrapper trace supplies callbacks beyond its source stop/repeat bound.");
        if(returned)ops.Add(MapPostEffect3PassOperation.FinalizeHook);
        return new(recipe,ops.AsReadOnly(),returned,completed,recipe.SourceEvidence+";"+observations.SourceEvidence+
            ";original-CPU-control-flow;no-device-completion-from-callback-AL");
    }
}
