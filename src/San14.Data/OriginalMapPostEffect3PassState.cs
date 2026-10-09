using System.Buffers.Binary;

namespace San14.Data;

public enum OriginalMapPostEffect3AutomaticExecution { ComputeDispatch, StockScreenQuad, SpecificFactoryGraphics }
public enum OriginalMapPostEffect3PassOperation {
    SelectPass,BeforeHook,BindProgramsAndResources,ExecuteHook,AutomaticExecuteAndRelease,AfterHook,ResetPass,FinalizeHook
}
public enum OriginalMapPostEffect3FunctorOperation { ResetHookFlags,PrepareHook,InitializeHook,ExecutePass,FinishHook,ClearHookFlags }
public sealed record OriginalMapPostEffect3FactoryState(int Index,uint NameHash,string NativeName,
    OriginalMapPostEffect3PassWrapper PassWrapper,
    OriginalMapPostEffect3Hook PrepareHook,OriginalMapPostEffect3Hook InitializeHook,OriginalMapPostEffect3Hook FinishHook,
    OriginalMapPostEffect3Hook BeforeHook,OriginalMapPostEffect3Hook ExecuteHook,OriginalMapPostEffect3Hook AfterHook,OriginalMapPostEffect3Hook FinalizeHook)
{
    public string Locale{get;init;}="TC";
}
public sealed record OriginalMapPostEffect3PassState(OriginalMapPostEffect3Pass Pass,
    OriginalMapPostEffect3FactoryState Factory,byte NativeFlags,
    OriginalMapPostEffect3AutomaticExecution AutomaticExecution)
{
    public bool UsesStockScreenShaderBinding=>(NativeFlags&1)!=0;
    public bool RequiresSpecificFactoryState=>true;
    public bool IsCompleteForExecution=>false;
}
public sealed record OriginalMapPostEffect3FunctorStep(OriginalMapPostEffect3FunctorOperation Operation,
    int FactoryIndex,int? SourcePassIndex,OriginalMapPostEffect3Hook? SourceHook);
public sealed record OriginalMapPostEffect3HistorySlot(OriginalMapPostEffect3BindingTable Table,int ParameterIndex);

/// <summary>Ordered module and factory callback protocol.
/// Named hooks identify the installed behavior; this does not
/// evaluate their controller predicates, scalar writes or custom draw commands.</summary>
public static class OriginalMapPostEffect3PassStateReader
{
    public static IReadOnlyList<OriginalMapPostEffect3FactoryState> Factories{get;}=Array.AsReadOnly(new[] {
        new OriginalMapPostEffect3FactoryState(0,0xD93C0DCE,"plTemporalAA",OriginalMapPostEffect3PassWrapper.Standard,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.BeforeTemporalAA,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue),
        new OriginalMapPostEffect3FactoryState(1,0x2413E650,"plLightShaft",OriginalMapPostEffect3PassWrapper.Standard,OriginalMapPostEffect3Hook.PrepareMiddle,OriginalMapPostEffect3Hook.InitializeLightShaft,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.BeforeMiddle,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue),
        new OriginalMapPostEffect3FactoryState(2,0x625AD5EC,"plDepthOfField",OriginalMapPostEffect3PassWrapper.Standard,OriginalMapPostEffect3Hook.PrepareMiddle,OriginalMapPostEffect3Hook.InitializeDepthOfFieldLegacy,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.BeforeMiddle,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue),
        new OriginalMapPostEffect3FactoryState(3,0xC49F7F09,"plMotionBlur",OriginalMapPostEffect3PassWrapper.Standard,OriginalMapPostEffect3Hook.PrepareMiddle,OriginalMapPostEffect3Hook.InitializeMotionBlur,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.BeforeMiddle,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue),
        new OriginalMapPostEffect3FactoryState(4,0x6007F69C,"plLensFlare",OriginalMapPostEffect3PassWrapper.Standard,OriginalMapPostEffect3Hook.PrepareMiddle,OriginalMapPostEffect3Hook.InitializeLensLegacy,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.BeforeLensLegacy,OriginalMapPostEffect3Hook.ExecuteLensLegacy,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue),
        new OriginalMapPostEffect3FactoryState(5,0x3D2D6725,"plComposite",OriginalMapPostEffect3PassWrapper.Standard,OriginalMapPostEffect3Hook.PrepareMiddle,OriginalMapPostEffect3Hook.InitializeCompositeLegacy,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue),
        new OriginalMapPostEffect3FactoryState(6,0xF59F3BCE,"plLensFlare2",OriginalMapPostEffect3PassWrapper.Standard,OriginalMapPostEffect3Hook.PrepareLens2,OriginalMapPostEffect3Hook.InitializeLens2,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.BeforeLens2,OriginalMapPostEffect3Hook.ExecuteLens2,OriginalMapPostEffect3Hook.AfterLens2,OriginalMapPostEffect3Hook.AlwaysTrue),
        new OriginalMapPostEffect3FactoryState(7,0x70D82B7A,"plDepthOfField2",OriginalMapPostEffect3PassWrapper.Standard,OriginalMapPostEffect3Hook.PrepareDepthOfField2,OriginalMapPostEffect3Hook.InitializeDepthOfField2,OriginalMapPostEffect3Hook.FinishDepthOfField2,OriginalMapPostEffect3Hook.BeforeDepthOfField2,OriginalMapPostEffect3Hook.ExecuteDepthOfField2,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue),
        new OriginalMapPostEffect3FactoryState(8,0x80E2C49A,"plEyeAdaptation2",OriginalMapPostEffect3PassWrapper.EyeRepeat,OriginalMapPostEffect3Hook.PrepareEye,OriginalMapPostEffect3Hook.InitializeEye,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.BeforeEye,OriginalMapPostEffect3Hook.ExecuteEye,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue),
        new OriginalMapPostEffect3FactoryState(9,0xD2C4AC57,"plComposite2",OriginalMapPostEffect3PassWrapper.Standard,OriginalMapPostEffect3Hook.PrepareComposite2,OriginalMapPostEffect3Hook.InitializeComposite2,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.BeforeComposite2,OriginalMapPostEffect3Hook.ExecuteComposite2,OriginalMapPostEffect3Hook.AlwaysTrue,OriginalMapPostEffect3Hook.AlwaysTrue)
    });
    private static readonly IReadOnlyList<OriginalMapPostEffect3FactoryState> ScFactories=
        Array.AsReadOnly(Factories.Select(f=>f with{Locale="SC"}).ToArray());
    public static IReadOnlyList<OriginalMapPostEffect3FactoryState> FactoriesForLocale(string locale)=>locale switch{
        "TC"=>Factories,"SC"=>ScFactories,_=>throw new ArgumentOutOfRangeException(nameof(locale))};

    public static IReadOnlyList<OriginalMapPostEffect3PassState> Read(OriginalMapPostEffect3Container source,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(source);
        var factories=FactoriesForLocale(locale);
        var rows=source.Tables.Single(t=>t.ChunkKind==7).Rows;
        if(rows.Count!=factories.Count)throw new NotSupportedException("3SPK factory population differs from the positively joined installed native implementations.");
        for(int i=0;i<rows.Count;i++)if(BinaryPrimitives.ReadUInt32LittleEndian(rows[i].Span.Slice(4,4))!=factories[i].NameHash)
            throw new NotSupportedException("3SPK factory key has no source-supported installed class join.");
        var result=new List<OriginalMapPostEffect3PassState>();
        foreach(var pass in source.Passes)
        {
            uint index=BinaryPrimitives.ReadUInt32LittleEndian(pass.RawBytes.Span.Slice(8,4));
            if(index>=factories.Count)throw new InvalidDataException("3SPK pass factory index exceeds its original counted type7 table.");
            byte flags=pass.RawBytes.Span[0x38];
            if((flags&~3)!=0)throw new NotSupportedException("3SPK pass flags are outside the source-supported installed subset.");
            bool compute=pass.ProgramReferences.Any(p=>p.StageCode==5&&p.StageIndex is not null);
            bool pixel=pass.ProgramReferences.Any(p=>p.StageCode==0&&p.StageIndex is not null);
            if(!compute&&!pixel)throw new NotSupportedException("3SPK pass has no native automatic execution stage.");
            result.Add(new(pass,factories[(int)index],flags,compute?OriginalMapPostEffect3AutomaticExecution.ComputeDispatch:
                (flags&2)!=0?OriginalMapPostEffect3AutomaticExecution.StockScreenQuad:OriginalMapPostEffect3AutomaticExecution.SpecificFactoryGraphics));
        }
        return result.AsReadOnly();
    }

    /// <summary>All hooks in source order for one explicitly selected Apply
    /// functor. Factory instances are shared across passes. Prepare/init/finish
    /// happen once per instance, while v8 receives every original pass index.
    /// Hook false results do not remove later passes from this outer schedule.</summary>
    public static IReadOnlyList<OriginalMapPostEffect3FunctorStep> ReadFunctorProtocol(OriginalMapPostEffect3Container source,int explicitFunctorIndex,string locale="TC")
    {
        var states=Read(source,locale);var passes=source.ReadFunctorPasses(explicitFunctorIndex).Select(p=>states[p.Index]).ToArray();
        var result=new List<OriginalMapPostEffect3FunctorStep>();
        foreach(var p in passes)result.Add(new(OriginalMapPostEffect3FunctorOperation.ResetHookFlags,p.Factory.Index,p.Pass.Index,null));
        var unique=passes.Select(p=>p.Factory).DistinctBy(f=>f.Index).ToArray();
        foreach(var f in unique){result.Add(new(OriginalMapPostEffect3FunctorOperation.PrepareHook,f.Index,null,f.PrepareHook));
            result.Add(new(OriginalMapPostEffect3FunctorOperation.InitializeHook,f.Index,null,f.InitializeHook));}
        foreach(var p in passes)result.Add(new(OriginalMapPostEffect3FunctorOperation.ExecutePass,p.Factory.Index,p.Pass.Index,null));
        var finished=new HashSet<int>();
        foreach(var p in passes){if(finished.Add(p.Factory.Index))result.Add(new(OriginalMapPostEffect3FunctorOperation.FinishHook,p.Factory.Index,p.Pass.Index,p.Factory.FinishHook));
            result.Add(new(OriginalMapPostEffect3FunctorOperation.ClearHookFlags,p.Factory.Index,p.Pass.Index,null));}
        return result.AsReadOnly();
    }

    /// <summary> branches only. The bind return is deliberately ignored
    /// by the original. EyeAdaptation2 has a separate repeat wrapper and must
    /// not consume this single-pass trace.</summary>
    public static IReadOnlyList<OriginalMapPostEffect3PassOperation> ReadStandardProtocol(
        OriginalMapPostEffect3PassState state,bool selectionSucceeded,bool beforeSucceeded,bool executeSucceeded)
    {
        ArgumentNullException.ThrowIfNull(state);
        if(state.Factory.PassWrapper!=FactoriesForLocale(state.Factory.Locale)[0].PassWrapper)throw new NotSupportedException("This native factory has a distinct repeat-pass wrapper.");
        var result=new List<OriginalMapPostEffect3PassOperation>{OriginalMapPostEffect3PassOperation.SelectPass};
        if(selectionSucceeded){result.Add(OriginalMapPostEffect3PassOperation.BeforeHook);
            if(beforeSucceeded){result.Add(OriginalMapPostEffect3PassOperation.BindProgramsAndResources);result.Add(OriginalMapPostEffect3PassOperation.ExecuteHook);
                if(executeSucceeded){result.Add(OriginalMapPostEffect3PassOperation.AutomaticExecuteAndRelease);result.Add(OriginalMapPostEffect3PassOperation.AfterHook);}}
            result.Add(OriginalMapPostEffect3PassOperation.ResetPass);}
        result.Add(OriginalMapPostEffect3PassOperation.FinalizeHook);return result.AsReadOnly();
    }

    /// <summary> flips every runtime pair after 's successful
    /// BeginFunctor, regardless of individual hook/pass results. No flip occurs
    /// when BeginFunctor fails. This is not a per-pass ping-pong.</summary>
    public static IReadOnlyList<OriginalMapPostEffect3HistorySlot> ReadHistoryFlip(OriginalMapPostEffect3AllocationPlan allocation,bool beginFunctorSucceeded)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        if(!beginFunctorSucceeded)return Array.Empty<OriginalMapPostEffect3HistorySlot>();
        return Array.AsReadOnly(allocation.TextureParameters.Where(p=>p.HasTwoRuntimeInstances).Select(p=>new OriginalMapPostEffect3HistorySlot(OriginalMapPostEffect3BindingTable.TextureParameters,p.Index))
            .Concat(allocation.BufferParameters.Where(p=>p.HasTwoRuntimeInstances).Select(p=>new OriginalMapPostEffect3HistorySlot(OriginalMapPostEffect3BindingTable.BufferParameters,p.Index)))
            .Concat(allocation.ConstantBufferParameters.Where(p=>p.HasTwoRuntimeInstances).Select(p=>new OriginalMapPostEffect3HistorySlot(OriginalMapPostEffect3BindingTable.ConstantBufferParameters,p.Index))).ToArray());
    }
}
