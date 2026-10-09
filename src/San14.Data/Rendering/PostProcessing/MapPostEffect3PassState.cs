using System.Buffers.Binary;

namespace San14.Data;

public enum MapPostEffect3AutomaticExecution { ComputeDispatch, StockScreenQuad, SpecificFactoryGraphics }
public enum MapPostEffect3PassOperation {
    SelectPass,BeforeHook,BindProgramsAndResources,ExecuteHook,AutomaticExecuteAndRelease,AfterHook,ResetPass,FinalizeHook
}
public enum MapPostEffect3FunctorOperation { ResetHookFlags,PrepareHook,InitializeHook,ExecutePass,FinishHook,ClearHookFlags }
public sealed record MapPostEffect3FactoryState(int Index,uint NameHash,string NativeName,
    MapPostEffect3PassWrapper PassWrapper,
    MapPostEffect3Hook PrepareHook,MapPostEffect3Hook InitializeHook,MapPostEffect3Hook FinishHook,
    MapPostEffect3Hook BeforeHook,MapPostEffect3Hook ExecuteHook,MapPostEffect3Hook AfterHook,MapPostEffect3Hook FinalizeHook)
{
    public string Locale{get;init;}="TC";
}
public sealed record MapPostEffect3PassState(MapPostEffect3Pass Pass,
    MapPostEffect3FactoryState Factory,byte NativeFlags,
    MapPostEffect3AutomaticExecution AutomaticExecution)
{
    public bool UsesStockScreenShaderBinding=>(NativeFlags&1)!=0;
    public bool RequiresSpecificFactoryState=>true;
    public bool IsCompleteForExecution=>false;
}
public sealed record MapPostEffect3FunctorStep(MapPostEffect3FunctorOperation Operation,
    int FactoryIndex,int? SourcePassIndex,MapPostEffect3Hook? SourceHook);
public sealed record MapPostEffect3HistorySlot(MapPostEffect3BindingTable Table,int ParameterIndex);

/// <summary>Ordered module and factory callback protocol.
/// Named hooks identify the installed behavior; this does not
/// evaluate their controller predicates, scalar writes or custom draw commands.</summary>
public static class MapPostEffect3PassStateReader
{
    public static IReadOnlyList<MapPostEffect3FactoryState> Factories{get;}=Array.AsReadOnly(new[] {
        new MapPostEffect3FactoryState(0,0xD93C0DCE,"plTemporalAA",MapPostEffect3PassWrapper.Standard,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.BeforeTemporalAA,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue),
        new MapPostEffect3FactoryState(1,0x2413E650,"plLightShaft",MapPostEffect3PassWrapper.Standard,MapPostEffect3Hook.PrepareMiddle,MapPostEffect3Hook.InitializeLightShaft,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.BeforeMiddle,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue),
        new MapPostEffect3FactoryState(2,0x625AD5EC,"plDepthOfField",MapPostEffect3PassWrapper.Standard,MapPostEffect3Hook.PrepareMiddle,MapPostEffect3Hook.InitializeDepthOfFieldLegacy,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.BeforeMiddle,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue),
        new MapPostEffect3FactoryState(3,0xC49F7F09,"plMotionBlur",MapPostEffect3PassWrapper.Standard,MapPostEffect3Hook.PrepareMiddle,MapPostEffect3Hook.InitializeMotionBlur,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.BeforeMiddle,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue),
        new MapPostEffect3FactoryState(4,0x6007F69C,"plLensFlare",MapPostEffect3PassWrapper.Standard,MapPostEffect3Hook.PrepareMiddle,MapPostEffect3Hook.InitializeLensLegacy,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.BeforeLensLegacy,MapPostEffect3Hook.ExecuteLensLegacy,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue),
        new MapPostEffect3FactoryState(5,0x3D2D6725,"plComposite",MapPostEffect3PassWrapper.Standard,MapPostEffect3Hook.PrepareMiddle,MapPostEffect3Hook.InitializeCompositeLegacy,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue),
        new MapPostEffect3FactoryState(6,0xF59F3BCE,"plLensFlare2",MapPostEffect3PassWrapper.Standard,MapPostEffect3Hook.PrepareLens2,MapPostEffect3Hook.InitializeLens2,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.BeforeLens2,MapPostEffect3Hook.ExecuteLens2,MapPostEffect3Hook.AfterLens2,MapPostEffect3Hook.AlwaysTrue),
        new MapPostEffect3FactoryState(7,0x70D82B7A,"plDepthOfField2",MapPostEffect3PassWrapper.Standard,MapPostEffect3Hook.PrepareDepthOfField2,MapPostEffect3Hook.InitializeDepthOfField2,MapPostEffect3Hook.FinishDepthOfField2,MapPostEffect3Hook.BeforeDepthOfField2,MapPostEffect3Hook.ExecuteDepthOfField2,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue),
        new MapPostEffect3FactoryState(8,0x80E2C49A,"plEyeAdaptation2",MapPostEffect3PassWrapper.EyeRepeat,MapPostEffect3Hook.PrepareEye,MapPostEffect3Hook.InitializeEye,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.BeforeEye,MapPostEffect3Hook.ExecuteEye,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue),
        new MapPostEffect3FactoryState(9,0xD2C4AC57,"plComposite2",MapPostEffect3PassWrapper.Standard,MapPostEffect3Hook.PrepareComposite2,MapPostEffect3Hook.InitializeComposite2,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.BeforeComposite2,MapPostEffect3Hook.ExecuteComposite2,MapPostEffect3Hook.AlwaysTrue,MapPostEffect3Hook.AlwaysTrue)
    });
    private static readonly IReadOnlyList<MapPostEffect3FactoryState> ScFactories=
        Array.AsReadOnly(Factories.Select(f=>f with{Locale="SC"}).ToArray());
    public static IReadOnlyList<MapPostEffect3FactoryState> FactoriesForLocale(string locale)=>locale switch{
        "TC"=>Factories,"SC"=>ScFactories,_=>throw new ArgumentOutOfRangeException(nameof(locale))};

    public static IReadOnlyList<MapPostEffect3PassState> Read(MapPostEffect3Container source,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(source);
        var factories=FactoriesForLocale(locale);
        var rows=source.Tables.Single(t=>t.ChunkKind==7).Rows;
        if(rows.Count!=factories.Count)throw new NotSupportedException("3SPK factory population differs from the positively joined installed native implementations.");
        for(int i=0;i<rows.Count;i++)if(BinaryPrimitives.ReadUInt32LittleEndian(rows[i].Span.Slice(4,4))!=factories[i].NameHash)
            throw new NotSupportedException("3SPK factory key has no source-supported installed class join.");
        var result=new List<MapPostEffect3PassState>();
        foreach(var pass in source.Passes)
        {
            uint index=BinaryPrimitives.ReadUInt32LittleEndian(pass.RawBytes.Span.Slice(8,4));
            if(index>=factories.Count)throw new InvalidDataException("3SPK pass factory index exceeds its original counted type7 table.");
            byte flags=pass.RawBytes.Span[0x38];
            if((flags&~3)!=0)throw new NotSupportedException("3SPK pass flags are outside the source-supported installed subset.");
            bool compute=pass.ProgramReferences.Any(p=>p.StageCode==5&&p.StageIndex is not null);
            bool pixel=pass.ProgramReferences.Any(p=>p.StageCode==0&&p.StageIndex is not null);
            if(!compute&&!pixel)throw new NotSupportedException("3SPK pass has no native automatic execution stage.");
            result.Add(new(pass,factories[(int)index],flags,compute?MapPostEffect3AutomaticExecution.ComputeDispatch:
                (flags&2)!=0?MapPostEffect3AutomaticExecution.StockScreenQuad:MapPostEffect3AutomaticExecution.SpecificFactoryGraphics));
        }
        return result.AsReadOnly();
    }

    /// <summary>All hooks in source order for one explicitly selected Apply
    /// functor. Factory instances are shared across passes. Prepare/init/finish
    /// happen once per instance, while v8 receives every original pass index.
    /// Hook false results do not remove later passes from this outer schedule.</summary>
    public static IReadOnlyList<MapPostEffect3FunctorStep> ReadFunctorProtocol(MapPostEffect3Container source,int explicitFunctorIndex,string locale="TC")
    {
        var states=Read(source,locale);var passes=source.ReadFunctorPasses(explicitFunctorIndex).Select(p=>states[p.Index]).ToArray();
        var result=new List<MapPostEffect3FunctorStep>();
        foreach(var p in passes)result.Add(new(MapPostEffect3FunctorOperation.ResetHookFlags,p.Factory.Index,p.Pass.Index,null));
        var unique=passes.Select(p=>p.Factory).DistinctBy(f=>f.Index).ToArray();
        foreach(var f in unique){result.Add(new(MapPostEffect3FunctorOperation.PrepareHook,f.Index,null,f.PrepareHook));
            result.Add(new(MapPostEffect3FunctorOperation.InitializeHook,f.Index,null,f.InitializeHook));}
        foreach(var p in passes)result.Add(new(MapPostEffect3FunctorOperation.ExecutePass,p.Factory.Index,p.Pass.Index,null));
        var finished=new HashSet<int>();
        foreach(var p in passes){if(finished.Add(p.Factory.Index))result.Add(new(MapPostEffect3FunctorOperation.FinishHook,p.Factory.Index,p.Pass.Index,p.Factory.FinishHook));
            result.Add(new(MapPostEffect3FunctorOperation.ClearHookFlags,p.Factory.Index,p.Pass.Index,null));}
        return result.AsReadOnly();
    }

    /// <summary> branches only. The bind return is deliberately ignored
    /// by the original. EyeAdaptation2 has a separate repeat wrapper and must
    /// not consume this single-pass trace.</summary>
    public static IReadOnlyList<MapPostEffect3PassOperation> ReadStandardProtocol(
        MapPostEffect3PassState state,bool selectionSucceeded,bool beforeSucceeded,bool executeSucceeded)
    {
        ArgumentNullException.ThrowIfNull(state);
        if(state.Factory.PassWrapper!=FactoriesForLocale(state.Factory.Locale)[0].PassWrapper)throw new NotSupportedException("This native factory has a distinct repeat-pass wrapper.");
        var result=new List<MapPostEffect3PassOperation>{MapPostEffect3PassOperation.SelectPass};
        if(selectionSucceeded){result.Add(MapPostEffect3PassOperation.BeforeHook);
            if(beforeSucceeded){result.Add(MapPostEffect3PassOperation.BindProgramsAndResources);result.Add(MapPostEffect3PassOperation.ExecuteHook);
                if(executeSucceeded){result.Add(MapPostEffect3PassOperation.AutomaticExecuteAndRelease);result.Add(MapPostEffect3PassOperation.AfterHook);}}
            result.Add(MapPostEffect3PassOperation.ResetPass);}
        result.Add(MapPostEffect3PassOperation.FinalizeHook);return result.AsReadOnly();
    }

    /// <summary> flips every runtime pair after 's successful
    /// BeginFunctor, regardless of individual hook/pass results. No flip occurs
    /// when BeginFunctor fails. This is not a per-pass ping-pong.</summary>
    public static IReadOnlyList<MapPostEffect3HistorySlot> ReadHistoryFlip(MapPostEffect3AllocationPlan allocation,bool beginFunctorSucceeded)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        if(!beginFunctorSucceeded)return Array.Empty<MapPostEffect3HistorySlot>();
        return Array.AsReadOnly(allocation.TextureParameters.Where(p=>p.HasTwoRuntimeInstances).Select(p=>new MapPostEffect3HistorySlot(MapPostEffect3BindingTable.TextureParameters,p.Index))
            .Concat(allocation.BufferParameters.Where(p=>p.HasTwoRuntimeInstances).Select(p=>new MapPostEffect3HistorySlot(MapPostEffect3BindingTable.BufferParameters,p.Index)))
            .Concat(allocation.ConstantBufferParameters.Where(p=>p.HasTwoRuntimeInstances).Select(p=>new MapPostEffect3HistorySlot(MapPostEffect3BindingTable.ConstantBufferParameters,p.Index))).ToArray());
    }
}
