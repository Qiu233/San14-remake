namespace San14.Data;

public enum OriginalMapPostEffect3Functor1BeforeReturnRule
{
    NotInvokedForEmptyRepeat,MaskRejectsWithAl0,SelectedSourceReturnsAl1,
    SelectedEyeScalarAndLoopConstantWriterResults
}
public enum OriginalMapPostEffect3Functor1SourceSkip
{ None,BeforeMaskRejected,EmptyNativeRepeat }
public enum OriginalMapPostEffect3Functor1DeviceReceiptKind
{ FactoryExecuteAndAutomaticRelease,LensAfterMipChain }
public sealed record OriginalMapPostEffect3Functor1MaskSnapshot(uint LensMaskF4,uint DepthOfFieldMaskA8,
    uint EyeMask1E8,int EyeLevelCount1B0,uint CompositeMask64);

/// <summary>One original directory wrapper. The final function AL comes from
/// v58 BF9280, independently of callback AL and device work. Selection still
/// requires native CS/PS wrapper availability; declarations are no live proof.</summary>
public sealed class OriginalMapPostEffect3Functor1WrapperRecipe
{
    internal OriginalMapPostEffect3Functor1WrapperRecipe(OriginalMapPostEffect3PassState state,int ordinal,uint mask,
        int bound,object? prepared,string evidence)
    {Pass=state;NativeCachedPassOrdinal=ordinal;ActualSourceMask=mask;NativeWrapperLoopBound=bound;
        SourcePreparedIdentity=prepared;SourceEvidence=evidence;}
    public OriginalMapPostEffect3PassState Pass{get;}
    public int SourcePassIndex=>Pass.Pass.Index;
    public int SourceDirectoryOrdinal=>SourcePassIndex-45;
    public int FactoryIndex=>Pass.Factory.Index;
    public int NativeCachedPassOrdinal{get;}
    public uint ActualSourceMask{get;}
    public int NativeWrapperLoopBound{get;}
    public bool MaskAdmitsBefore=>(ActualSourceMask&(1u<<NativeCachedPassOrdinal))!=0;
    public int ExpectedSuccessfulDeviceIterationCount=>MaskAdmitsBefore?NativeWrapperLoopBound:0;
    public OriginalMapPostEffect3Functor1SourceSkip SourceSkip=>NativeWrapperLoopBound==0?
        OriginalMapPostEffect3Functor1SourceSkip.EmptyNativeRepeat:MaskAdmitsBefore?
        OriginalMapPostEffect3Functor1SourceSkip.None:OriginalMapPostEffect3Functor1SourceSkip.BeforeMaskRejected;
    public OriginalMapPostEffect3Functor1BeforeReturnRule BeforeReturnRule=>NativeWrapperLoopBound==0?
        OriginalMapPostEffect3Functor1BeforeReturnRule.NotInvokedForEmptyRepeat:!MaskAdmitsBefore?
        OriginalMapPostEffect3Functor1BeforeReturnRule.MaskRejectsWithAl0:FactoryIndex==8?
        OriginalMapPostEffect3Functor1BeforeReturnRule.SelectedEyeScalarAndLoopConstantWriterResults:
        OriginalMapPostEffect3Functor1BeforeReturnRule.SelectedSourceReturnsAl1;
    public byte? UnconditionalSourceBeforeReturnAl=>BeforeReturnRule switch{
        OriginalMapPostEffect3Functor1BeforeReturnRule.MaskRejectsWithAl0=>0,
        OriginalMapPostEffect3Functor1BeforeReturnRule.SelectedSourceReturnsAl1=>1,_=>null};
    public byte SourceExecuteReturnAlAfterReturning=>1;
    public byte SourceAfterReturnAlAfterReturning=>1;
    public byte SourceFinalizeReturnAl=>1;
    public string Locale=>Pass.Factory.Locale;
    public uint SourceFinalizeRva=>Pass.Factory.FinalizeHook;
    public bool RequiresNativeCsOrPsWrapperForSelection=>true;
    public uint SourceSelectionRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3Functor1WrapperSource.cs",0x1161D40));
    public int SourceSelectionContextFirstPass1BF4=>45;
    public int SourceSelectionContextDirectoryOrdinal1BF8=>SourceDirectoryOrdinal;
    public int SourceDeclaredProgramMode1C04=>Pass.AutomaticExecution==OriginalMapPostEffect3AutomaticExecution.ComputeDispatch?0:2;
    public uint SourceResetRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3Functor1WrapperSource.cs",0x1163080));
    public uint SourceResetTargetSlotsRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3Functor1WrapperSource.cs",0x1163460));
    public uint SourceResetCurrentPassDescriptorRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3Functor1WrapperSource.cs",0x1161B60));
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
    public IReadOnlyList<OriginalMapPostEffect3PassOperation> ExpectedSuccessfulSourceOperations
    {
        get
        {
            var operations=new List<OriginalMapPostEffect3PassOperation>{OriginalMapPostEffect3PassOperation.SelectPass};
            if(NativeWrapperLoopBound>0)
            {
                for(int i=0;i<(MaskAdmitsBefore?NativeWrapperLoopBound:1);i++)
                {
                    operations.Add(OriginalMapPostEffect3PassOperation.BeforeHook);
                    if(!MaskAdmitsBefore)break;
                    operations.Add(OriginalMapPostEffect3PassOperation.BindProgramsAndResources);
                    operations.Add(OriginalMapPostEffect3PassOperation.ExecuteHook);
                    operations.Add(OriginalMapPostEffect3PassOperation.AutomaticExecuteAndRelease);
                    operations.Add(OriginalMapPostEffect3PassOperation.AfterHook);
                }
            }
            operations.Add(OriginalMapPostEffect3PassOperation.ResetPass);
            operations.Add(OriginalMapPostEffect3PassOperation.FinalizeHook);return operations.AsReadOnly();
        }
    }
}

/// <summary>Implemented by the real device adapter around its closed actual
/// submission. This Data interface neither manufactures a GPU receipt nor
/// treats a caller AL as proof of a submission. The adapter must validate the
/// exact programs, source bindings, CBs, target writes and native auxiliary work.</summary>
public interface IOriginalMapPostEffect3Functor1ActualDeviceReceipt
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    string ModuleSourceSha256{get;}
    int SourcePassIndex{get;}
    int NativeLoopIndex{get;}
    OriginalMapPostEffect3Functor1DeviceReceiptKind Kind{get;}
    object SourcePreparedIdentity{get;}
    object ActualSubmissionIdentity{get;}
    string SourceEvidence{get;}
    void RequireActualForSource(OriginalMapPostEffect3Functor1WrapperRecipe sourceRecipe);
}

public sealed class LoadedOriginalMapPostEffect3Functor1WrapperSource
{
    internal LoadedOriginalMapPostEffect3Functor1WrapperSource(string evidence,string locale="TC")=>(SourceEvidence,Locale)=(evidence,locale);
    public string Locale{get;}
    public string SourceEvidence{get;}
    public bool ProvesGpuCompletion=>false;
    internal static readonly int[][] NativeCachePasses=[
        [45,46,47,48,49,50,51,52],
        [53,54,55,56,57,58,59,62,63,64,65,66,67,68,70,72,73,74,75,76,77,69,71,78,79,80,81,82,83,60,61],
        [84,85,86,88,90,102,103,104,105,87,89,91],
        [98,100,108,109,110,111,112,92,94,96,106,107,99,101,93,95,97,113,114,115,116]];
    public IReadOnlyList<OriginalMapPostEffect3Functor1WrapperRecipe> ReadSourceRecipes(
        OriginalMapPostEffect3Container module,OriginalMapPostEffect3Functor1MaskSnapshot sourceFields)
        =>ReadRecipes(module,sourceFields,null);
    internal IReadOnlyList<OriginalMapPostEffect3Functor1WrapperRecipe> ReadRecipes(
        OriginalMapPostEffect3Container module,OriginalMapPostEffect3Functor1MaskSnapshot fields,IReadOnlyList<object>? frames)
    {
        OriginalMapPostEffect3ParameterState.ValidateSource(module);
        if(module.Functors[1].FirstPass!=45||module.Functors[1].PassCount!=72||fields.EyeLevelCount1B0 is <1 or >16)
            throw new InvalidDataException("Functor1 wrapper source requires its actual72 directory and bounded native Eye layer count.");
        uint[] masks=[fields.LensMaskF4,fields.DepthOfFieldMaskA8,fields.EyeMask1E8,fields.CompositeMask64];
        var states=OriginalMapPostEffect3PassStateReader.Read(module,Locale);var recipes=new List<OriginalMapPostEffect3Functor1WrapperRecipe>();
        foreach(var p in module.ReadFunctorPasses(1))
        {
            var state=states[p.Index];int family=state.Factory.Index-6;
            if((uint)family>=4||state.Factory.FinalizeHook!=checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3Functor1WrapperSource.cs",0xBF9280))||
                state.Factory.PassWrapper!=checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3Functor1WrapperSource.cs",family==2?0x1157A20:0x114CD40)))
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
    public OriginalMapPostEffect3Functor1WrapperDirectory CapturePreparedDirectory(
        OriginalMapPostEffect3Functor1ModuleInvocation actualInvocation,OriginalMapPostEffect3TextureParameterState actualTable,
        OriginalMapPostEffect3ParameterLensFlare2Update lens,OriginalMapPostEffect3ParameterDepthOfField2Update dof,
        OriginalMapPostEffect3EyeAdaptation2Frame eye,OriginalMapPostEffect3Composite2ExecutionFrame composite)
        =>new(this,actualInvocation,actualTable,lens,dof,eye,composite);
}

public static class OriginalMapPostEffect3Functor1WrapperSource
{
    public static LoadedOriginalMapPostEffect3Functor1WrapperSource Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadPostEffect3Functor1WrapperSource(OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
}
public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3Functor1WrapperSource ReadPostEffect3Functor1WrapperSource(LoadedOriginalDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Functor1 wrapper source requires a verified SC/TC profile.");
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Functor1 wrapper executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int start);VerifyHash(code,native.DecodedTextSha256,"Functor1 wrapper decoded text");
        foreach(var w in new (int Rva,int Count,string Sha)[]{
            (0x114CD40,152,"15A81E37172F430508849084B0C0F18A32F61EF5AD8A2ED2939464BC68AF20A9"),
            (0x1157A20,326,"48A3E39E4134C3912C9377A266FD51C5394BAC6F98D6BB50E4992A5B008CA807"),
            (0x1161D40,1773,"57B3E131B5FEAA2CC5DFC7318AD910B987B26478F475619E5ADB679F020B770A"),
            (0x1163170,751,"521950BFFD2C49366ACAF604363A74BDB91126F20BC422F31370E925794C5DA3"),
            (0x1163080,28,"98DED7630F7B1079FB54BAD0024AC6EF05F24ADC41481D36BEF7135CED74AC80"),
            (0x114B450,170,"214B18CAFFAB679177C23D8DA4A88816DE78B7A2908B8C00E019366ED8693863"),
            (0x114C350,586,"BFD8C2D94C30825F8771F3F6E1AE82FC1ED1623F540454C89C973A2ABAA29484"),
            (0x114BEA0,1194,"BFDC89EDCA1363CC6755EB01070A1E7ED3D09C49E68171436B8FFFC48E1BEC2A"),
            (0x1154DC0,883,"B7D2C93150508BEADA08F35D346DA55113204A7966C559C495DD8F793FCB324B"),
            (0x1155CF0,1943,"C967847CFF3FE30B1F599E6D596E0690D5E2729929B17CB8B8871FEF5B03F30A"),
            (0x1157CB0,909,"1C6187E908374993AA3E7624A283557CD43EB7FD8727B1E1CA5F2967C89E4009"),
            (0x1158DC0,956,"975632AA78E57E1DA37C17BECDF3C5A914D89794359C58D8EE800D7D847AF859"),
            (0x1159DD0,588,"52A867136CF0A14353EDDCF8425FBE61F0AB1AA72D425553DE3BD7BE39F28888"),
            (0x115B750,1101,"8D49711CA686E9B60C7D6C8C677104193720893902AFA0535404E3551EA95EA3"),
            (0x1164140,367,"42AAAE035916B91AF03ADF1C3A99CB1596F9A566E6466C864D7C12E40803538E"),
            (0xBF9280,3,"E18AACCE29AFFCDDDAA5F641AC1EC12552E0FE9D066E6408941B1792818B8619")})
            { var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3Functor1WrapperSource.cs",w.Rva,w.Count,w.Sha);
              VerifyHash(code.AsSpan(actual.Rva-start,w.Count),actual.Sha,"Functor1 wrapper/callback source"+w.Rva.ToString("X")); }
        foreach(var f in OriginalMapPostEffect3PassStateReader.FactoriesForLocale(native.Locale).Skip(6))
        foreach(var slot in new (int Offset,uint Rva)[]{(8,f.PassWrapper),(0x40,f.BeforeHook),(0x48,f.ExecuteHook),(0x50,f.AfterHook),(0x58,f.FinalizeHook)})
            if(System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(image.Raw(checked((int)f.Vtable+slot.Offset),8))!=0x140000000UL+slot.Rva)
                throw new InvalidDataException("Functor1 wrapper callback vtable join changed.");
        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+"@"+native.ExecutableSha256+
            ";114CD40/1157A20-source-Before-masks/repeats/Execute/After/Reset;v58=BF9280-literal1;no-native-live-program-or-device-observation",native.Locale);
    }
}
