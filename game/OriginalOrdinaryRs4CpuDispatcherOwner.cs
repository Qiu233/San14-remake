using San14.Data;

public sealed record OriginalOrdinaryRs4RecordCpuVisit(string RecordOwnerIdentity,uint RecordMask78,
    bool Selected,int CompleteFirstBankCount,int VisitedTableGroups,int NullShaderPrimitives,
    OriginalModelOrdinaryDispatcherStencilPacket Stencil);

/// <summary>Successful CPU execution of one exact earlier queue. It reports
/// no renderer draw or initialized pixels. A false native draw return is not
/// a skipped CPU node.</summary>
public sealed record OriginalOrdinaryRs4CpuDispatchCompleted(int SourceNode,byte ShaderState,
    bool HasCurrentView,bool EnteredRecordDispatcher,int VisitedPrimitives,
    IReadOnlyList<OriginalOrdinaryRs4RecordCpuVisit> Records,
    OriginalOrdinaryRs4DepthProjection ExitDepth,bool? ExitAlphaEnabled)
{
    public bool SourceDrawReturn=>false;
    public bool ProvesGpuDrawSubmitted=>false;
    public bool ProvesPixelContentsInitialized=>false;
}

/// <summary>Actual43-bound ordinary44/45/46 CPU dispatcher. It consumes
/// the complete current first bank and final primitive18 table, before the
/// later Army/static GPU queues. The current finite source has null13/22;
/// another source renderer remains a required real consumer.</summary>
public sealed class OriginalOrdinaryRs4CpuDispatcherOwner
{
    private readonly OriginalOrdinaryRs4CpuSource source;
    private readonly LoadedOriginalModelFrontendColorSource colors;
    private readonly OriginalWorldGeometryTargetBinding binding;
    private readonly OriginalOrdinaryRs4CurrentPopulation population;
    private readonly OriginalMapSourceCreatedWorldF103Frames world;
    private readonly OriginalWorldPrimitiveOrderSource order;
    private readonly OriginalOrdinaryRs4CpuParameterSourceOwner parameters;
    private readonly Func<OriginalWorldGeometryLateCurrentContext> current;
    private readonly List<OriginalOrdinaryRs4CpuDispatchCompleted> completed=[];
    private int phase;

    public OriginalOrdinaryRs4CpuDispatcherOwner(OriginalOrdinaryRs4CpuSource actualSource,
        LoadedOriginalModelFrontendColorSource loadedColorSource,OriginalWorldGeometryTargetBinding actual43,
        OriginalOrdinaryRs4CurrentPopulation actualPopulation,OriginalMapSourceCreatedWorldF103Frames actualGeometryWorld,
        OriginalWorldPrimitiveOrderSource actualViewOrder,OriginalOrdinaryRs4CpuFrontendState actualFrontend,
        OriginalOrdinaryRs4CpuParameterSourceOwner actualParameters,
        Func<OriginalWorldGeometryLateCurrentContext> readActualCurrent)
    {
        source=actualSource??throw new ArgumentNullException(nameof(actualSource));
        colors=loadedColorSource??throw new ArgumentNullException(nameof(loadedColorSource));
        binding=actual43??throw new ArgumentNullException(nameof(actual43));
        population=actualPopulation??throw new ArgumentNullException(nameof(actualPopulation));
        world=actualGeometryWorld??throw new ArgumentNullException(nameof(actualGeometryWorld));
        order=actualViewOrder??throw new ArgumentNullException(nameof(actualViewOrder));
        State=actualFrontend??throw new ArgumentNullException(nameof(actualFrontend));
        parameters=actualParameters??throw new ArgumentNullException(nameof(actualParameters));
        current=readActualCurrent??throw new ArgumentNullException(nameof(readActualCurrent));
        if(colors.Locale!=source.Source.Native.Locale)
            throw new InvalidDataException("Earlier RS4 colors use a different rendering layout.");
        RequireCurrent();
    }
    public OriginalOrdinaryRs4CpuFrontendState State{get;}
    public IReadOnlyList<OriginalOrdinaryRs4CpuDispatchCompleted> CompletedQueues=>completed.AsReadOnly();
    public void RequireCompletedBeforePop47()
    {
        RequireCurrent();
        if(phase!=3)throw new InvalidOperationException("Actual47 pop requires completed CPU44/45/46 in source order.");
    }
    private void RequireCurrent()
    {
        population.RequireCurrent();parameters.RequireCurrent(binding,world);
        if(binding.SourceNode!=43||binding.Frame.ColorViews.Count!=7 ||
            population.FrameNumber!=binding.Accepted.Context.FrameNumber||
            population.ViewIdentity!=world.View.Binding.ViewIdentity ||
            binding.Accepted.MainCamera.ExecutableLocale!=source.Source.Native.Locale)
            throw new InvalidDataException("Earlier RS4 dispatcher needs the actual43/View/current complete primitive publication.");
    }

    public OriginalOrdinaryRs4CpuDispatchCompleted ExecuteQueue(int sourceNode,float actualCommonTime254,
        int actualView19D4Stored,int? actualView19D0Capacity,object? actualView1C18)
    {
        RequireCurrent();
        if(phase<0||sourceNode!=44+phase)throw new InvalidOperationException("Earlier RS4 queues execute44,45,46 once in source order.");
        var queue=source.Source.Queues[sourceNode];var ctx=current();var common=world.Passes[0].Frame.Common;
        var entry=source.PrepareCurrentEntry(sourceNode,ctx,common.CommonViewResource240Present,
            common.CommonViewFlags8,actualView19D4Stored,actualView19D0Capacity,actualView1C18);
        if(!float.IsFinite(actualCommonTime254))throw new InvalidDataException("Earlier RS4 requires its actual current Common254 clock.");
        var rows=world.View.CurrentWorldQueue?.Records ??throw new InvalidDataException("RS4 has no current complete View record queue.");
        if(entry.CurrentRecordCount is int count&&rows.Count!=count)
            throw new InvalidDataException("ActualRS4 View19D4 differs from its complete current World record roster.");
        var visits=new List<OriginalOrdinaryRs4RecordCpuVisit>();int visitedPrimitives=0;
        try
        {
            if(entry.HasSelectedView)
            {
                parameters.Begin(sourceNode);bool saved=false;float savedTime=0;
                uint? savedFill=State.ActualSourceFillCode;
                try
                {
                    savedTime=parameters.ReadTime();saved=true;parameters.SetTime(actualCommonTime254);
                    if(entry.EntersRecordDispatcher)
                    {
                        State.Depth=OriginalOrdinaryRs4DepthProjection.FromActual(source.PrepareEntryDepth(sourceNode,ctx.ContextFlags3E4));State.SetStencilEnabled(false);
                        ulong queueRaster=source.ReadSourceQueueRasterWord(sourceNode);
                        State.ActualSourceFillCode=(uint)queueRaster&15;
                        State.ActualSourceCullCode=(uint)(queueRaster>>4)&15;
                        State.DepthBiasEnabled=false; // D71788..B7, actual type10=23 high words0.
                        var activeColors=source.PrepareActiveColorEntry(sourceNode,binding.Frame.ColorViews.Count);
                        State.SetActiveColors(activeColors);
                        var aux=colors.ReadCurrentAuxiliaryMasks(common);
                        parameters.EnterGeneric();State.SetStencilEnabled(true);
                        State.Stencil=State.Stencil with{Fail=0,DepthFail=0,Pass=2};
                        if(common.CommonViewResource240Present)State.ClipWord1F0=source.PrepareCurrentClipWord(common);
                        foreach(var record in rows)
                        {
                            RequireCurrent();
                            var bank=ReadActualSortedFirstBank(record);
                            var nativeRecord=new OriginalModelOrdinaryDispatcherRecord(population.FrameNumber,population.ViewIdentity,
                                record.Mask78,true,bank.Count,true,record.ProducerProvenance+";actual43/firstbank/currentfinalPrimitive18");
                            var stencil=source.PrepareRecord(sourceNode,nativeRecord,common,ctx.ContextFlags3E4);
                            int groups=0,nullPrimitives=0;
                            if(stencil.RecordSelected)
                            {
                                State.Stencil=State.Stencil with{Compare=stencil.NativeCompareCode,Reference=stencil.Reference,
                                    ReadMask=stencil.ReadMask,WriteMask=stencil.WriteMask};
                                var color=colors.PrepareLateRecordActiveColor(activeColors,record.Mask78,aux);
                                if(color.HasRecordMaskWriter)State.SetActiveColors(color.DrawState);
                                if(bank.Count!=0)
                                {
                                    parameters.PrepareRecord(sourceNode,record);
                                    try
                                    {
                                        State.AlphaEnabled=queue.BackendAlphaEnabled;
                                        OriginalNativeWorldShaderTableState? previous=null;
                                        foreach(var primitive in bank)
                                        {
                                            if(!ReferenceEquals(previous,primitive.ActualFinalTable))groups++;
                                            previous=primitive.ActualFinalTable;
                                            var selected=primitive.QueueInterfaces[sourceNode];
                                            if(selected.Boundary==OriginalOrdinaryRs4InterfaceBoundary.RequiresRenderer)
                                                throw new NotSupportedException($"ActualRS4 node{sourceNode}/state{queue.ShaderStateOffset}/"+
                                                    $"primitive={primitive.PacketIdentity}/finalTable={selected.FinalTable.Object.ObjectId:X8} requires its real renderer consumer.");
                                            visitedPrimitives++;nullPrimitives++;
                                        }
                                    }
                                    finally{parameters.CleanupRecord(record);}
                                }
                                if(color.HasRecordMaskWriter)State.SetActiveColors(color.ExitState);
                            }
                            visits.Add(new(record.RecordOwnerIdentity,record.Mask78,stencil.RecordSelected,bank.Count,groups,nullPrimitives,stencil));
                        }
                        State.SetStencilEnabled(false);
                        if(common.CommonViewResource240Present)State.ClipWord1F0=0;
                        parameters.ClearSixResources();
                    }
                }
                finally
                {
                    try{if(saved)parameters.SetTime(savedTime);}
                    finally
                    {
                        try
                        {
                            if(saved)
                            {
                                // EA8C51 restores EC (Fill), while E8 Cull
                                // remains the actual D06390/D71710 writer.
                                State.ActualSourceFillCode=savedFill;
                                if((source.Parameters(sourceNode).Packed728&(1UL<<41))!=0)
                                    State.Depth=State.Depth with{Write=true,Compare=(ctx.ContextFlags3E4&0x20000000)!=0?6u:3u};
                            }
                        }
                        finally{parameters.Exit(sourceNode);}
                    }
                }
            }
        }
        catch{phase=-1;completed.Clear();throw;}
        var result=new OriginalOrdinaryRs4CpuDispatchCompleted(sourceNode,queue.ShaderStateOffset,entry.HasSelectedView,
            entry.EntersRecordDispatcher,visitedPrimitives,visits.AsReadOnly(),State.Depth,State.AlphaEnabled);
        completed.Add(result);phase++;return result;
    }

    private IReadOnlyList<OriginalOrdinaryRs4CurrentPrimitive> ReadActualSortedFirstBank(OriginalMapWorldKind1CurrentRecord record)
    {
        var statics=population.StaticPrimitives.Where(p=>p.AdmittedRecordOwners.Contains(record.RecordOwnerIdentity,StringComparer.Ordinal)).ToArray();
        var armies=population.ArmyPrimitives.Where(p=>p.AdmittedRecordOwners.Contains(record.RecordOwnerIdentity,StringComparer.Ordinal)).ToArray();
        if(statics.Length!=0&&armies.Length!=0)
            throw new NotSupportedException("ActualRS4 overlapping static/Army firstbank needs its real shared insertion publication order.");
        var actual=statics.Length!=0?statics:armies;
        int count=world.View.Kind1Records.Single(r=>r.LightOwnerIdentity==record.RecordOwnerIdentity).CurrentCpuPrimitiveCount;
        if(count!=actual.Length || actual.Length>record.FirstCounterCapacity)
            throw new InvalidDataException("ActualRS4 complete firstbank differs from its record-owner current counter/capacity.");
        return Array.AsReadOnly(order.Sort(OriginalWorldPrimitiveSortFamily.Opaque,actual,(tuple,field)=>field switch
        {3=>tuple.ActualFinalTable.OrderedAllocationIdentity,14=>tuple.ActualFirstCounterKey,
            _=>throw new NotSupportedException("ActualRS4 tuple has no producer for selected field "+field)}));
    }
}
