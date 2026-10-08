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
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"BeginFunctor executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"BeginFunctor decoded text");
        void Check(int rva,int size,string hash)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3BeginFunctorCounterSource.cs",rva,size,hash);
            VerifyHash(text.AsSpan(actual.Rva-start,size),actual.Sha,"BeginFunctor source window");
        }
        Check(0x1145D90,0xE0,"597B8224C2A09CF53C7A8AD11B400B57640B8B0F3B632BD722D73490E01B8342");
        Check(0x1147378,0x1A2,"8EA6625A4CABBED62614A2CD861256BC4D3246C67D094490A1A93955EF80C25C");
        Check(0x11466F0,0x11C,"C3F836385B4CEC37786D224DA89984870ED33D84409BA7A3144E75C65C9CAA0D");
        Check(0x114680C,0x33,"AB6227DAA7B6028290279BD6E647446AC9DA42E40610A4983277CD33EF923B3B");
        Check(0x1161C00,0x1E,"51B2BA133887DD4FEAED03CB6A147871E610F82DB24281365A0859CFCC02088E");
        return new(native.Locale,native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+
            ";TC:1147378-allocate/114739D-moduleCtor1145D90;1145DAA-xorECX;1145DD1-qwordCPU50-zero-coversCPU54;"+
            "11474D8-dwordCPU50-count-preservesCPU54;11466F0-module-BeginFunctor;114681D-INC-dwordCPU54;"+
            "1146820-EDX=CPU54/1146823-call1161C00;1161C16-context1BF0=EDX;"+
            "1146828-frontend-failure-test-after-counter;fresh-first-Begin=1;uint32-wrap;not-QCounter/not-FrameNumber");
    }
}
