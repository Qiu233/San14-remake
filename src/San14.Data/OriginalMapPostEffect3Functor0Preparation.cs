using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMapPostEffect3ImplicitConstantBufferRequirement(uint StageCode,
    OriginalMapPostEffect3Program Program,DxbcRdefBinding Binding,DxbcRdefBuffer Buffer);

/// <summary>Exact source prerequisites, not an evaluated hook result or an
/// executable GPU packet. Factory hooks and implicit backend state stay visible.</summary>
public sealed record OriginalMapPostEffect3Functor0PassPreparationPlan(
    OriginalMapPostEffect3PassState State,OriginalMapPostEffect3PassAllocationLinks Allocation,
    IReadOnlyList<OriginalMapPostEffect3ParameterBindingRequirement> ConstantBuffers,
    IReadOnlyList<OriginalMapPostEffect3Binding> ExternalResourceBindings,
    IReadOnlyList<OriginalMapPostEffect3ImplicitConstantBufferRequirement> ImplicitConstantBuffers);

public sealed record OriginalMapPostEffect3Functor0PreparationPlan(string SourceSha256,string OwnerIdentity,
    IReadOnlyList<OriginalMapPostEffect3FunctorStep> Protocol,
    IReadOnlyList<OriginalMapPostEffect3Functor0PassPreparationPlan> Passes);

public enum OriginalMapPostEffect3TemporalAAFirstPassDisposition
{
    EnabledSourceConstantBufferPrepared,
    DisabledNativeTextureAliasRequiresProducer
}

/// <summary>Recovered11505A0 first-pass output. Neither disposition establishes
/// texture readiness, implicit stock CB/sampler state, draw completion or a
/// functor-history flip. NextHistory is the source before-hook matrix update;
/// it must not be relabelled as a successful full-frame GPU commit.</summary>
public sealed record OriginalMapPostEffect3TemporalAAFirstPassPreparation(long FrameNumber,string OwnerIdentity,
    OriginalMapPostEffect3Functor0PassPreparationPlan Plan,
    OriginalMapPostEffect3TemporalAAFirstPassDisposition Disposition,
    OriginalMapPostEffect3ParameterTemporalAAUpdate BeforeHook,
    IReadOnlyList<OriginalMapPostEffect3ParameterConstantBufferBinding> OrderedConstantBufferBindings,
    OriginalMapPostEffect3ConstantBufferUpload? SelectedPassUpload);

/// <summary>Bounded actual Functor0 prerequisite producer. Reuses the counted
/// reader, allocation and recovered TemporalAA writer; no alternate shader,
/// generic hook no-op or invented external resource is admitted.</summary>
public static class OriginalMapPostEffect3Functor0Preparation
{
    private static readonly uint[] Stages=[5,1,0,2,3,4];

    public static OriginalMapPostEffect3Functor0PreparationPlan Read(
        OriginalMapPostEffect3AllocationPlan allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        var states=OriginalMapPostEffect3PassStateReader.Read(allocation.Source);
        var order=allocation.ReadSourceFunctorOrder(0);
        if(order.Count!=45||order.Where((p,i)=>p.Pass.Index!=i).Any())
            throw new InvalidDataException("Actual Functor0 must retain its source pass0..44 order.");
        var passes=new List<OriginalMapPostEffect3Functor0PassPreparationPlan>();
        foreach(var pass in order)
        {
            var requirements=OriginalMapPostEffect3ParameterBindingRequirements.Read(allocation,pass.Pass.Index);
            var implicitCbs=new List<OriginalMapPostEffect3ImplicitConstantBufferRequirement>();
            foreach(var selected in pass.Pass.ProgramReferences.Where(p=>p.StageIndex is not null))
            {
                var program=allocation.Source.GetProgram(selected.StageCode,selected.StageIndex!.Value);
                var defs=DxbcResourceDefinitionReader.Read(program.Bytes.Span);
                int stageBit=Array.IndexOf(Stages,selected.StageCode);
                foreach(var cb in defs.Bindings.Where(b=>b.InputType==0))
                {
                    bool explicitBinding=pass.ActiveBindings.Any(b=>
                        b.Role==OriginalMapPostEffect3BindingRole.ConstantBufferRead&&
                        b.RegisterIndex==cb.BindPoint&&(b.StageMask&(1<<stageBit))!=0);
                    if(!explicitBinding)
                        implicitCbs.Add(new(selected.StageCode,program,cb,
                            defs.Buffers.Single(b=>b.Name.Text==cb.Name.Text)));
                }
            }
            bool IsExternal(OriginalMapPostEffect3Binding b)=>b.Table switch
            {
                OriginalMapPostEffect3BindingTable.TextureParameters=>
                    allocation.TextureParameters[b.ParameterIndex].Ownership==OriginalMapPostEffect3AllocationOwnership.ExternalParameterSlot,
                OriginalMapPostEffect3BindingTable.BufferParameters=>
                    allocation.BufferParameters[b.ParameterIndex].Ownership==OriginalMapPostEffect3AllocationOwnership.ExternalParameterSlot,
                OriginalMapPostEffect3BindingTable.ConstantBufferParameters=>
                    allocation.ConstantBufferParameters[b.ParameterIndex].Ownership==OriginalMapPostEffect3AllocationOwnership.ExternalParameterSlot,
                _=>throw new NotSupportedException("Functor0 binding has an unsupported source parameter table.")
            };
            passes.Add(new(states[pass.Pass.Index],pass,requirements,
                Array.AsReadOnly(pass.ActiveBindings.Where(IsExternal).ToArray()),implicitCbs.AsReadOnly()));
        }
        return new(allocation.Source.Sha256,allocation.ContextOwnerIdentity,
            OriginalMapPostEffect3PassStateReader.ReadFunctorProtocol(allocation.Source,0),passes.AsReadOnly());
    }

    /// <summary>Native first pass preparation from an actual scalar bank and
    /// same-owner retained history. Unknown present scalar values reject in the
    /// source writer. Disabled texture aliases are returned as an unresolved
    /// producer requirement, never a substitute CB or a successful skipped draw.</summary>
    public static OriginalMapPostEffect3TemporalAAFirstPassPreparation PrepareFirstPass(
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3ParameterTemporalAAHistory history,int actualDescriptorWidth40)
    {
        ArgumentNullException.ThrowIfNull(state);
        var plan=Read(allocation).Passes[0];
        if(plan.State.Factory.Index!=0||plan.State.Factory.NativeName!="plTemporalAA"||
            plan.State.Factory.BeforeHook!=0x11505A0||
            plan.State.AutomaticExecution!=OriginalMapPostEffect3AutomaticExecution.StockScreenQuad||
            plan.State.Pass.ProgramReferences.Where(r=>r.StageIndex is not null).ToArray() is not
                [{StageCode:0,StageIndex:0}])
            throw new InvalidDataException("Functor0 first pass differs from actual TemporalAA/PS0 stock-screen source.");
        var update=OriginalMapPostEffect3ParameterTemporalAA.Prepare(state,allocation,history,actualDescriptorWidth40);
        if(update.ConstantBuffer is not { } packet)
            return new(state.FrameNumber,state.OwnerIdentity,plan,
                OriginalMapPostEffect3TemporalAAFirstPassDisposition.DisabledNativeTextureAliasRequiresProducer,
                update,Array.Empty<OriginalMapPostEffect3ParameterConstantBufferBinding>(),null);
        var bindings=OriginalMapPostEffect3ParameterConstantBuffers.PreparePassBindings(allocation,0,state.FrameNumber,[packet]);
        if(bindings.Count!=2||bindings.Any(b=>b.StageCode!=0||b.RegisterIndex!=3||b.Program.StageIndex!=0||
                b.ConstantBuffer.Declaration.Index!=0))
            throw new InvalidDataException("TemporalAA first pass must preserve both actual CB0 to PS0 CB3 source rows.");
        var image=new OriginalMapPostEffect3ConstantBufferImage(allocation,packet.Declaration.Index,state.FrameNumber);
        image.Write(0,packet.Bytes.Span,packet.WriterRva,"actual11505A0 complete TemporalAA before-hook output");
        var upload=image.PrepareForPass(allocation,0,state.FrameNumber);
        return new(state.FrameNumber,state.OwnerIdentity,plan,
            OriginalMapPostEffect3TemporalAAFirstPassDisposition.EnabledSourceConstantBufferPrepared,
            update,bindings,upload);
    }
}
