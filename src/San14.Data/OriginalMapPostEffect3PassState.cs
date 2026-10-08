using System.Buffers.Binary;

namespace San14.Data;

public enum OriginalMapPostEffect3AutomaticExecution { ComputeDispatch, StockScreenQuad, SpecificFactoryGraphics }
public enum OriginalMapPostEffect3PassOperation {
    SelectPass,BeforeHook,BindProgramsAndResources,ExecuteHook,AutomaticExecuteAndRelease,AfterHook,ResetPass,FinalizeHook
}
public enum OriginalMapPostEffect3FunctorOperation { ResetHookFlags,PrepareHook,InitializeHook,ExecutePass,FinishHook,ClearHookFlags }
public sealed record OriginalMapPostEffect3FactoryState(int Index,uint NameHash,string NativeName,
    uint RegistrationCreator,uint PlacementCreator,uint Constructor,uint Vtable,
    uint PassWrapper,uint PrepareHook,uint InitializeHook,uint FinishHook,
    uint BeforeHook,uint ExecuteHook,uint AfterHook,uint FinalizeHook)
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
    int FactoryIndex,int? SourcePassIndex,uint? SourceHook);
public sealed record OriginalMapPostEffect3HistorySlot(OriginalMapPostEffect3BindingTable Table,int ParameterIndex);

/// <summary>Source-only protocol for 11466F0 -> 1164140 -> factory.v8.
/// Hook addresses identify the actual installed implementations; this does not
/// evaluate their controller predicates, scalar writes or custom draw commands.</summary>
public static class OriginalMapPostEffect3PassStateReader
{
    private const uint True=0xBF9280,Standard=0x114CD40;
    // 11444C0 passes the exact names to each creator. Primary114CC30 is stored
    // at factory+28; 1145CF0 compares it to type7 row+4. Placement v20 creates
    // the concrete CPU object below. Do not confuse these with Apply functors0/1.
    public static IReadOnlyList<OriginalMapPostEffect3FactoryState> Factories{get;}=Array.AsReadOnly(new[] {
        new OriginalMapPostEffect3FactoryState(0,0xD93C0DCE,"plTemporalAA",0x11458C0,0x1144EF0,0x11504A0,0x173AEA8,Standard,True,True,True,0x11505A0,True,True,True),
        new OriginalMapPostEffect3FactoryState(1,0x2413E650,"plLightShaft",0x11455A0,0x1144CC0,0x1153F20,0x173B458,Standard,0x1153DF0,0x1153FA0,True,0x1153F90,True,True,True),
        new OriginalMapPostEffect3FactoryState(2,0x625AD5EC,"plDepthOfField",0x1145280,0x1144A90,0x1150D60,0x173AFD0,Standard,0x1153DF0,0x1150D90,True,0x1153F90,True,True,True),
        new OriginalMapPostEffect3FactoryState(3,0xC49F7F09,"plMotionBlur",0x11456E0,0x1144DA0,0x11512F0,0x173B070,Standard,0x1153DF0,0x11513C0,True,0x1153F90,True,True,True),
        new OriginalMapPostEffect3FactoryState(4,0x6007F69C,"plLensFlare",0x1145460,0x1144BE0,0x1151900,0x173B160,Standard,0x1153DF0,0x1151FF0,True,0x1151B90,0x1153120,True,True),
        new OriginalMapPostEffect3FactoryState(5,0x3D2D6725,"plComposite",0x1145000,0x1144910,0x1153320,0x173B3E0,Standard,0x1153DF0,0x1153390,True,0x1153340,True,True,True),
        new OriginalMapPostEffect3FactoryState(6,0xF59F3BCE,"plLensFlare2",0x11453C0,0x1144B70,0x114B1D0,0x173A590,Standard,0x114C640,0x114B500,True,0x114B450,0x114C350,0x114BEA0,True),
        new OriginalMapPostEffect3FactoryState(7,0x70D82B7A,"plDepthOfField2",0x1145140,0x11449B0,0x1154C00,0x173B608,Standard,0x11564F0,0x1155140,0x1155280,0x1154DC0,0x1155CF0,True,True),
        new OriginalMapPostEffect3FactoryState(8,0x80E2C49A,"plEyeAdaptation2",0x1145320,0x1144B00,0x1157810,0x173BB68,0x1157A20,0x11592A0,0x1158040,True,0x1157CB0,0x1158DC0,True,True),
        new OriginalMapPostEffect3FactoryState(9,0xD2C4AC57,"plComposite2",0x1144F60,0x11448A0,0x1159CC0,0x173BD98,Standard,0x115BBA0,0x115A020,True,0x1159DD0,0x115B750,True,True)
    });

    private static readonly Lazy<IReadOnlyList<OriginalMapPostEffect3FactoryState>> ScFactories=new(
        ()=>Array.AsReadOnly(Factories.Select(f=>f with {
            Locale="SC",
            RegistrationCreator=Code(f.RegistrationCreator),PlacementCreator=Code(f.PlacementCreator),
            Constructor=Code(f.Constructor),
            Vtable=checked((uint)OriginalMapPostEffect3NativeSourceProfile.DataRva("SC","OriginalMapPostEffect3PassState.cs",checked((int)f.Vtable))),
            PassWrapper=Code(f.PassWrapper),PrepareHook=Code(f.PrepareHook),InitializeHook=Code(f.InitializeHook),
            FinishHook=Code(f.FinishHook),BeforeHook=Code(f.BeforeHook),ExecuteHook=Code(f.ExecuteHook),
            AfterHook=Code(f.AfterHook),FinalizeHook=Code(f.FinalizeHook)
        }).ToArray()));
    private static uint Code(uint tcRva)=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(
        "SC","OriginalMapPostEffect3PassState.cs",checked((int)tcRva)));
    public static IReadOnlyList<OriginalMapPostEffect3FactoryState> FactoriesForLocale(string locale)=>locale switch {
        "TC"=>Factories,"SC"=>ScFactories.Value,_=>throw new ArgumentOutOfRangeException(nameof(locale))};

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
        foreach(var p in passes)result.Add(new(OriginalMapPostEffect3FunctorOperation.ExecutePass,p.Factory.Index,p.Pass.Index,p.Factory.PassWrapper));
        var finished=new HashSet<int>();
        foreach(var p in passes){if(finished.Add(p.Factory.Index))result.Add(new(OriginalMapPostEffect3FunctorOperation.FinishHook,p.Factory.Index,p.Pass.Index,p.Factory.FinishHook));
            result.Add(new(OriginalMapPostEffect3FunctorOperation.ClearHookFlags,p.Factory.Index,p.Pass.Index,null));}
        return result.AsReadOnly();
    }

    /// <summary>114CD40 branches only. The bind return is deliberately ignored
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

    /// <summary>114AB40 flips every runtime pair after 11466F0's successful
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
