namespace San14.Data;

/// <summary>The fresh native module constructor clears CPU50..57. The upper
/// dword, CPU54, is incremented by BeginFunctor and copied to context1BF0.
/// This sequence belongs to the module, independently of Camera or frame clocks.</summary>
public sealed class LoadedOriginalMapPostEffect3BeginFunctorCounterSource
{
    internal LoadedOriginalMapPostEffect3BeginFunctorCounterSource(string locale,string evidence)
        =>(Locale,SourceEvidence)=(locale,evidence);
    public string Locale{get;}
    public string SourceEvidence{get;}
    public uint NativeFreshInitialCounter=>0;
    public bool IsFrameNumber=>false;
    public bool InitializesGpuContents=>false;

    /// <summary>Construct only together with a new owned module generation.
    /// Loading a retained module bank is not a constructor and cannot reset it.</summary>
    public OriginalMapPostEffect3OwnedModuleFunctorCounter ConstructFreshOwnedModule(
        OriginalMapPostEffect3Container module,string ownerIdentity)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerIdentity);
        OriginalMapPostEffect3ParameterState.ValidateSource(module);
        var snapshot=OriginalMapPostEffect3ContainerReader.Read(module.Bytes.Span);
        if(Locale is not ("TC" or "SC")||snapshot.Functors.Count!=2||snapshot.Functors[0].Index!=0||snapshot.Functors[1].Index!=1)
            throw new NotSupportedException("Fresh BeginFunctor counter requires the verified SC/TC source module generation.");
        return new(snapshot,ownerIdentity,SourceEvidence);
    }
}

/// <summary>Module lifetime, not frame lifetime. A Begin consumes the original
/// uint32 INC before frontend Begin can fail. Failure must not roll it back.</summary>
public sealed class OriginalMapPostEffect3OwnedModuleFunctorCounter:IDisposable
{
    private readonly object lifetime=new();
    private readonly int functorCount;
    private readonly string sourceEvidence;
    private uint counter;
    private OriginalMapPostEffect3BeginFunctorCounterReceipt? current;
    private bool disposed;
    internal OriginalMapPostEffect3OwnedModuleFunctorCounter(OriginalMapPostEffect3Container module,
        string ownerIdentity,string evidence)
        =>(ModuleSha256,OwnerIdentity,functorCount,sourceEvidence)=(module.Sha256,ownerIdentity,module.Functors.Count,evidence);
    public string ModuleSha256{get;}
    public string OwnerIdentity{get;}

    /// <summary>Invoke at actual module Begin, before factory preparation.
    /// This receipt proves source counter publication, not GPU execution or success.</summary>
    public OriginalMapPostEffect3BeginFunctorCounterReceipt Begin(int selectedFunctorIndex)
    {
        lock(lifetime)
        {
            if(disposed)throw new ObjectDisposedException(nameof(OriginalMapPostEffect3OwnedModuleFunctorCounter));
            if((uint)selectedFunctorIndex>=(uint)functorCount)
                throw new ArgumentOutOfRangeException(nameof(selectedFunctorIndex));
            counter=unchecked(counter+1u);
            current=new(this,counter,selectedFunctorIndex,sourceEvidence+
                ";owned-module="+OwnerIdentity+";CPU54-uint32-INC="+counter+
                ";context1BF0-published-before-frontend-Begin-result;no-frame-clock-input");
            return current;
        }
    }

    internal void RequireCurrent(OriginalMapPostEffect3BeginFunctorCounterReceipt receipt)
    {
        lock(lifetime)
        {
            if(disposed)throw new ObjectDisposedException(nameof(OriginalMapPostEffect3OwnedModuleFunctorCounter));
            if(!ReferenceEquals(current,receipt)||receipt.Counter!=counter)
                throw new InvalidDataException("BeginFunctor receipt belongs to an earlier module invocation.");
        }
    }
    public void Dispose(){lock(lifetime){disposed=true;current=null;}}
}

public sealed class OriginalMapPostEffect3BeginFunctorCounterReceipt
{
    private readonly OriginalMapPostEffect3OwnedModuleFunctorCounter owner;
    internal OriginalMapPostEffect3BeginFunctorCounterReceipt(OriginalMapPostEffect3OwnedModuleFunctorCounter source,
        uint counter,int selectedFunctorIndex,string evidence)
        =>(owner,Counter,SelectedFunctorIndex,SourceEvidence)=(source,counter,selectedFunctorIndex,evidence);
    public uint Counter{get;}
    public int SelectedFunctorIndex{get;}
    public string ModuleOwnerIdentity=>owner.OwnerIdentity;
    public string ModuleSha256=>owner.ModuleSha256;
    public string SourceEvidence{get;}
    public bool ProvesGpuExecution=>false;
    public void RequireCurrent()=>owner.RequireCurrent(this);
}

public static class OriginalMapPostEffect3BeginFunctorCounterSource
{
    public static LoadedOriginalMapPostEffect3BeginFunctorCounterSource Load(
        OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("BeginFunctor module counter source windows require a verified SC/TC profile.");
        return OriginalDeferredSamplerSources.ReadPostEffect3BeginFunctorCounterSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3BeginFunctorCounterSource ReadPostEffect3BeginFunctorCounterSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("BeginFunctor source requires its verified SC/TC module creator.");

        return new(native.Locale,native.ResourceUri+$";{native.Locale}:paired-source-profile;"+
            ";allocate/-moduleCtor;-xorECX;-qwordCPU50-zero-coversCPU54"+
            "-dwordCPU50-count-preservesCPU54;-module-BeginFunctor;-INC-dwordCPU54"+
            "-EDX=CPU54/-call;-context1BF0=EDX"+
            "-frontend-failure-test-after-counter;fresh-first-Begin=1;uint32-wrap;not-QCounter/not-FrameNumber");
    }
}
