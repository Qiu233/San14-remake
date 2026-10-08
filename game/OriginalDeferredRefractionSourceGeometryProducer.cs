using San14.Data;

/// <summary>Unforgeable outside this assembly: completion of an actual
/// RenderType0/state16 source-null execution on the accepted geometry roster.
/// It is separate from node97's real pixel write.</summary>
public sealed class OriginalDeferredRefractionSourceNullExecution
{
    private readonly OriginalDeferredAuxiliarySourcePixelStages stages;
    private readonly OriginalNativeMapModelPopulationAcceptedFrame population;
    private readonly OriginalNativeMapArmyPopulationAcceptedFrame army;
    private readonly OriginalDeferredSourceViewFamilyCurrentPublication publication;
    private readonly OriginalDeferredRefractionSourceNullPlan plan;
    internal OriginalDeferredRefractionSourceNullExecution(OriginalDeferredAuxiliarySourcePixelStages stages,
        OriginalNativeMapModelPopulationAcceptedFrame population,OriginalNativeMapArmyPopulationAcceptedFrame army,
        OriginalDeferredSourceViewFamilyCurrentPublication publication,OriginalDeferredRefractionSourceNullPlan plan,int node)
        =>(this.stages,this.population,this.army,this.publication,this.plan,WriterNodeIndex)=
            (stages,population,army,publication,plan,node);
    public int WriterNodeIndex { get; }
    public int OrdinaryCandidateCount=>plan.OrdinaryCandidateCount;
    public string SourceProvenance=>plan.SourceProvenance;
    public void ValidateCurrent(OriginalMapSourceCreatedNativeSessionPreparedFrame accepted,
        OriginalDeferredAuxiliaryTargetDevice target,int writerNode)
    {
        if(!ReferenceEquals(stages.AcceptedFrame,accepted) || !ReferenceEquals(stages.RefractionCopy.Target,target) ||
            WriterNodeIndex!=writerNode || writerNode is not(139 or 140))
            throw new InvalidDataException("Source-null refraction execution belongs to another frame, target or writer.");
        publication.ValidateCurrent(stages,population,army);
        plan.ValidateCurrent(accepted.PreparedSourceFrame!,publication.SourceFamilies);
    }
}

/// <summary>Executes the proved source-null mode16 branches without replacing
/// their existing depth with a clear. Non-null/unknown final interfaces reject;
/// they need their actual original geometry program instead.</summary>
public static class OriginalDeferredRefractionSourceGeometryProducer
{
    public static async Task CompleteSourceNullAsync(OriginalDeferredAuxiliarySourcePixelStages stages,
        OriginalNativeMapModelPopulationAcceptedFrame population,OriginalNativeMapArmyPopulationAcceptedFrame army,
        OriginalDeferredSourceViewFamilyCurrentPublication publication)
    {
        ArgumentNullException.ThrowIfNull(stages);ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(population);ArgumentNullException.ThrowIfNull(army);
        publication.ValidateCurrent(stages,population,army);
        var accepted=stages.AcceptedFrame;
        var source=accepted.PreparedSourceFrame??throw new InvalidDataException("Current refraction source frame is unavailable.");
        var plan=OriginalDeferredRefractionSourceNullPlan.Prepare(source,publication.SourceFamilies);
        var target=stages.RefractionCopy.Target;
        OriginalMainMapCameraRefractionBindingSource.ReadSource(accepted.Selection)
            .ValidateTarget(target.Construction,accepted.Context.FrameNumber);
        var graph=accepted.Selection.SourcePlan.Graph;
        var prior=stages.RefractionCopy.CopyDepthReceipt;
        if(!ReferenceEquals(stages.RefractionCopy.AcceptedSelection,accepted.Selection) ||
            prior.FrameNumber!=accepted.Context.FrameNumber || prior.WriterNodeIndex!=97 ||
            prior.ProducerProvenance!=accepted.Selection.Current.CurrentProducerProvenance)
            throw new InvalidDataException("Source-null geometry requires the actual same-frame node97 copy result.");
        foreach(int node in new[]{139,140})
        {
            var proof=new OriginalDeferredRefractionSourceNullExecution(stages,population,army,publication,plan,node);
            var receipt=OriginalDeferredAuxiliaryProductionReceipt.FromOriginalWriter(graph,node,
                accepted.Context.FrameNumber,accepted.Selection.Current.CurrentProducerProvenance,
                proof.SourceProvenance+";acceptedTerrainPatches="+publication.AcceptedTerrainPatchCount+
                ";source-null-execution-not-a-depth-write",plan.TargetObjectId,[0])
                with{SourceNullRefractionExecution=proof};
            await target.CompleteSourceMode16NoOpAsync(receipt,prior,
                ()=>proof.ValidateCurrent(accepted,target,node));
            prior=receipt;
        }
    }
}
