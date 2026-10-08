namespace San14.Data;

public sealed partial class OriginalMapPostEffect3TextureParameterState
{
    /// <summary>Functor1 CPU pointer/selector migration only. Copied old
    /// aliases retain their old pointer. No pixel or GPU lease is transferred.</summary>
    public static OriginalMapPostEffect3TextureParameterState CreateRetainedForFunctor1Invocation(
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3Functor1ModuleInvocation actualInvocation,
        OriginalMapPostEffect3TextureParameterState previousActualTable)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(actualInvocation);
        ArgumentNullException.ThrowIfNull(previousActualTable);
        actualInvocation.ValidateRetainedTextureTable(allocation,previousActualTable);
        if(!ReferenceEquals(previousActualTable.allocation,allocation)||previousActualTable.exposureSwap is not null)
            throw new InvalidDataException("Functor1 retention requires the same table allocation and restored exposure swap.");
        var state=new OriginalMapPostEffect3TextureParameterState(allocation,actualInvocation.FrameNumber,
            OriginalMapPostEffect3ParameterState.CreateUninitialized(allocation.Source,actualInvocation.FrameNumber,allocation.ContextOwnerIdentity));
        Array.Copy(previousActualTable.references,state.references,state.references.Length);
        Array.Copy(previousActualTable.known,state.known,state.known.Length);
        Array.Copy(previousActualTable.revisions,state.revisions,state.revisions.Length);
        foreach(var a in allocation.TextureAllocations)
        {
            int index=a.Parameter.Index;var old=previousActualTable.references[index];
            if(!previousActualTable.known[index]||old is null||old.PhysicalAllocationParameterIndex!=index)
                throw new NotSupportedException("Functor1 owned wrapper replacement requires its particular retained source writer.");
            int side=actualInvocation.TextureInstances[index];
            if((uint)side>=a.InstanceCount)throw new InvalidDataException("Functor1 retained side exceeds the original allocation.");
            if(side!=old.InstanceIndex)state.Assign(index,new(index,side,allocation.ContextOwnerIdentity+$"/texture{index}/side{side}",
                "TC:114AB40 Functor1 CPU selector transition;"+actualInvocation.Begin.SourceEvidence));
        }
        return state;
    }
}
