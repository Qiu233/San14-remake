namespace San14.Data;

public enum MapPostEffect3Functor1BeforeReturnRule
{
    NotInvokedForEmptyRepeat,MaskRejectsWithAl0,SelectedSourceReturnsAl1,
    SelectedEyeScalarAndLoopConstantWriterResults
}
public enum MapPostEffect3Functor1SourceSkip
{ None,BeforeMaskRejected,EmptyNativeRepeat }
public enum MapPostEffect3Functor1DeviceReceiptKind
{ FactoryExecuteAndAutomaticRelease,LensAfterMipChain }
public sealed record MapPostEffect3Functor1MaskSnapshot(uint LensMaskF4,uint DepthOfFieldMaskA8,
    uint EyeMask1E8,int EyeLevelCount1B0,uint CompositeMask64);

/// <summary>One original directory wrapper. The final function AL comes from
/// v58 , independently of callback AL and device work. Selection still
/// requires native CS/PS wrapper availability; declarations are no live proof.</summary>
public sealed class MapPostEffect3Functor1WrapperRecipe
{
    internal MapPostEffect3Functor1WrapperRecipe(MapPostEffect3PassState state,int ordinal,uint mask,
        int bound,object? prepared,string evidence)
    {Pass=state;NativeCachedPassOrdinal=ordinal;ActualSourceMask=mask;NativeWrapperLoopBound=bound;
        SourcePreparedIdentity=prepared;SourceEvidence=evidence;}
    public MapPostEffect3PassState Pass{get;}
    public int SourcePassIndex=>Pass.Pass.Index;
    public int SourceDirectoryOrdinal=>SourcePassIndex-45;
    public int FactoryIndex=>Pass.Factory.Index;
    public int NativeCachedPassOrdinal{get;}
    public uint ActualSourceMask{get;}
    public int NativeWrapperLoopBound{get;}
    public bool MaskAdmitsBefore=>(ActualSourceMask&(1u<<NativeCachedPassOrdinal))!=0;
    public int ExpectedSuccessfulDeviceIterationCount=>MaskAdmitsBefore?NativeWrapperLoopBound:0;
    public MapPostEffect3Functor1SourceSkip SourceSkip=>NativeWrapperLoopBound==0?
        MapPostEffect3Functor1SourceSkip.EmptyNativeRepeat:MaskAdmitsBefore?
        MapPostEffect3Functor1SourceSkip.None:MapPostEffect3Functor1SourceSkip.BeforeMaskRejected;
    public MapPostEffect3Functor1BeforeReturnRule BeforeReturnRule=>NativeWrapperLoopBound==0?
        MapPostEffect3Functor1BeforeReturnRule.NotInvokedForEmptyRepeat:!MaskAdmitsBefore?
        MapPostEffect3Functor1BeforeReturnRule.MaskRejectsWithAl0:FactoryIndex==8?
        MapPostEffect3Functor1BeforeReturnRule.SelectedEyeScalarAndLoopConstantWriterResults:
        MapPostEffect3Functor1BeforeReturnRule.SelectedSourceReturnsAl1;
    public byte? UnconditionalSourceBeforeReturnAl=>BeforeReturnRule switch{
        MapPostEffect3Functor1BeforeReturnRule.MaskRejectsWithAl0=>0,
        MapPostEffect3Functor1BeforeReturnRule.SelectedSourceReturnsAl1=>1,_=>null};
    public byte SourceExecuteReturnAlAfterReturning=>1;
    public byte SourceAfterReturnAlAfterReturning=>1;
    public byte SourceFinalizeReturnAl=>1;
    public string Locale=>Pass.Factory.Locale;
    public MapPostEffect3Hook FinalizeHook=>Pass.Factory.FinalizeHook;
    public bool RequiresNativeCsOrPsWrapperForSelection=>true;
    public int SourceSelectionContextFirstPass1BF4=>45;
    public int SourceSelectionContextDirectoryOrdinal1BF8=>SourceDirectoryOrdinal;
    public int SourceDeclaredProgramMode1C04=>Pass.AutomaticExecution==MapPostEffect3AutomaticExecution.ComputeDispatch?0:2;
    public bool MaskRejectedStillSelectsAndResets=>true;
    public bool SelectionModeChangeMayClearTargets=>true;
    public bool ProvesActualFrontendSelectionOrReset=>false;
    public bool ProvesActualNativeSelectionSucceeded=>false;
    public bool BindReturnAlIsIgnored=>true;
    public bool AfterReturnAlControlsRepeat=>FactoryIndex==8;
    public bool StandardAfterReturnAlIsIgnored=>FactoryIndex!=8;
    public bool RequiresLensAfterDeviceJoin=>ExpectedSuccessfulDeviceIterationCount>0&&SourcePassIndex is 45 or 49;
    public object? SourcePreparedIdentity{get;}
    public string SourceEvidence{get;}
    public bool ProvesGpuSubmission=>false;
    public bool ProvesNativeProcessCallbackObservation=>false;
    public bool CommitsFunctorHistory=>false;
    public IReadOnlyList<MapPostEffect3PassOperation> ExpectedSuccessfulSourceOperations
    {
        get
        {
            var operations=new List<MapPostEffect3PassOperation>{MapPostEffect3PassOperation.SelectPass};
            if(NativeWrapperLoopBound>0)
            {
                for(int i=0;i<(MaskAdmitsBefore?NativeWrapperLoopBound:1);i++)
                {
                    operations.Add(MapPostEffect3PassOperation.BeforeHook);
                    if(!MaskAdmitsBefore)break;
                    operations.Add(MapPostEffect3PassOperation.BindProgramsAndResources);
                    operations.Add(MapPostEffect3PassOperation.ExecuteHook);
                    operations.Add(MapPostEffect3PassOperation.AutomaticExecuteAndRelease);
                    operations.Add(MapPostEffect3PassOperation.AfterHook);
                }
            }
            operations.Add(MapPostEffect3PassOperation.ResetPass);
            operations.Add(MapPostEffect3PassOperation.FinalizeHook);return operations.AsReadOnly();
        }
    }
}

/// <summary>Implemented by the real device adapter around its closed actual
/// submission. This Data interface neither manufactures a GPU receipt nor
/// treats a caller AL as proof of a submission. The adapter must validate the
/// exact programs, source bindings, CBs, target writes and native auxiliary work.</summary>
public interface IMapPostEffect3Functor1ActualDeviceReceipt
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    string ModuleSourceSha256{get;}
    int SourcePassIndex{get;}
    int NativeLoopIndex{get;}
    MapPostEffect3Functor1DeviceReceiptKind Kind{get;}
    object SourcePreparedIdentity{get;}
    object ActualSubmissionIdentity{get;}
    string SourceEvidence{get;}
    void RequireActualForSource(MapPostEffect3Functor1WrapperRecipe sourceRecipe);
}

public sealed class LoadedMapPostEffect3Functor1WrapperSource
{
    internal LoadedMapPostEffect3Functor1WrapperSource(string evidence,string locale="TC")=>(SourceEvidence,Locale)=(evidence,locale);
    public string Locale{get;}
    public string SourceEvidence{get;}
    public bool ProvesGpuCompletion=>false;
    internal static readonly int[][] NativeCachePasses=[
        [45,46,47,48,49,50,51,52],
        [53,54,55,56,57,58,59,62,63,64,65,66,67,68,70,72,73,74,75,76,77,69,71,78,79,80,81,82,83,60,61],
        [84,85,86,88,90,102,103,104,105,87,89,91],
        [98,100,108,109,110,111,112,92,94,96,106,107,99,101,93,95,97,113,114,115,116]];
    public IReadOnlyList<MapPostEffect3Functor1WrapperRecipe> ReadSourceRecipes(
        MapPostEffect3Container module,MapPostEffect3Functor1MaskSnapshot sourceFields)
        =>ReadRecipes(module,sourceFields,null);
    internal IReadOnlyList<MapPostEffect3Functor1WrapperRecipe> ReadRecipes(
        MapPostEffect3Container module,MapPostEffect3Functor1MaskSnapshot fields,IReadOnlyList<object>? frames)
    {
        MapPostEffect3ParameterState.ValidateSource(module);
        if(module.Functors[1].FirstPass!=45||module.Functors[1].PassCount!=72||fields.EyeLevelCount1B0 is <1 or >16)
            throw new InvalidDataException("Functor1 wrapper source requires its actual72 directory and bounded native Eye layer count.");
        uint[] masks=[fields.LensMaskF4,fields.DepthOfFieldMaskA8,fields.EyeMask1E8,fields.CompositeMask64];
        var states=MapPostEffect3PassStateReader.Read(module,Locale);var recipes=new List<MapPostEffect3Functor1WrapperRecipe>();
        foreach(var p in module.ReadFunctorPasses(1))
        {
            var state=states[p.Index];int family=state.Factory.Index-6;
            if((uint)family>=4||state.Factory.FinalizeHook!=MapPostEffect3Hook.AlwaysTrue||
                state.Factory.PassWrapper!=(family==2?MapPostEffect3PassWrapper.EyeRepeat:MapPostEffect3PassWrapper.Standard))
                throw new InvalidDataException("Functor1 wrapper/finalize native vtable identity differs.");
            int ordinal=Array.IndexOf(NativeCachePasses[family],p.Index);
            if(ordinal<0)throw new InvalidDataException("Functor1 counted pass has no native Before cache entry.");
            int bound=family==2&&ordinal is 3 or 10?Math.Max(0,fields.EyeLevelCount1B0-2):
                family==2&&ordinal is 4 or 11?Math.Max(0,fields.EyeLevelCount1B0-1):1;
            recipes.Add(new(state,ordinal,masks[family],bound,frames?[family],SourceEvidence+
                $";directory={p.Index};factory={state.Factory.Index};cachedOrdinal={ordinal};mask={masks[family]:X8};wrapperBound={bound};v58-literal1-independent-of-device"));
        }
        if(recipes.Count!=72||!recipes.Select(r=>r.SourcePassIndex).SequenceEqual(Enumerable.Range(45,72)))
            throw new InvalidDataException("Functor1 native cache map does not cover all72 counted wrappers.");
        return recipes.AsReadOnly();
    }
    public MapPostEffect3Functor1WrapperDirectory CapturePreparedDirectory(
        MapPostEffect3Functor1ModuleInvocation actualInvocation,MapPostEffect3TextureParameterState actualTable,
        MapPostEffect3ParameterLensFlare2Update lens,MapPostEffect3ParameterDepthOfField2Update dof,
        MapPostEffect3EyeAdaptation2Frame eye,MapPostEffect3Composite2ExecutionFrame composite)
        =>new(this,actualInvocation,actualTable,lens,dof,eye,composite);
}

public static class MapPostEffect3Functor1WrapperSource
{
    public static LoadedMapPostEffect3Functor1WrapperSource Load(GameResourceRepository repository,string locale="TC")
        =>RenderSamplerSourceAdapters.ReadPostEffect3Functor1WrapperSource(RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
}
public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3Functor1WrapperSource ReadPostEffect3Functor1WrapperSource(LoadedDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Functor1 wrapper source requires a verified SC/TC profile.");

        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;"+";render-rules="+native.RenderRuleIdentity+
            "-source-Before-masks/repeats/Execute/After/Reset;v58=-literal1;no-native-live-program-or-device-observation",native.Locale);
    }
}
