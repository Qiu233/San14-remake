using San14.Data;

/// <summary>Complete same-family Model inputs from their actual current
/// owners. Main visibility does not filter this inventory. Cross-family
/// collection order, independent culling, tuples and draws remain separate.</summary>
public sealed class OriginalSceneViewNativeMapShadowPopulationSourceOwner
{
    private readonly OriginalSceneViewNativeMapCpuSourceOwner staticSource;
    public OriginalSceneViewNativeMapShadowPopulationSourceOwner(OriginalSceneViewNativeMapCpuSourceOwner actualStaticSource)
        =>staticSource=actualStaticSource;

    public OriginalDeferredEarlyShadowCompleteModelCandidates PrepareStatic(
        OriginalMapStaticModelCurrentOwnedFrame actualPopulation,string viewIdentity)
    {
        var inputs=new Dictionary<string,OriginalDeferredEarlyShadowCurrentStaticModelInputs>(StringComparer.Ordinal);
        foreach(var instance in actualPopulation.Population.Instances)
        {
            var node=staticSource.BorrowCurrentSourceNode(actualPopulation,instance);
            var provider=OriginalModelWorldGeometryFallbackSource.LoadStatic(instance.Asset);
            inputs.Add(instance.InstanceKey,new(provider.CaptureRetainedCurrentModelCpu(instance.CurrentSourceIdentity!),
                node.Words,provider.World.CaptureRetainedFresh(),node.BoundsPublication));
        }
        return OriginalDeferredEarlyShadowCompleteModelCandidates.PrepareCurrentStatic(actualPopulation,viewIdentity,0,inputs,
            actualPopulation.SourceRevision+"/complete-shadow-static-models/"+actualPopulation.FrameNumber);
    }

    public OriginalDeferredEarlyShadowCompleteModelCandidates PrepareArmy(OriginalArmyWorldPrimitivePublication actualArmy)
    {
        var inputs=new Dictionary<string,OriginalDeferredEarlyShadowCurrentArmyModelInputs>(StringComparer.Ordinal);
        foreach(var actor in actualArmy.Actors)
        {
            var provider=OriginalModelWorldGeometryFallbackSource.LoadArmy(actor.Source);
            inputs.Add(actor.ActorIdentity,new(provider.CaptureRetainedCurrentModelCpu(actor.ActorIdentity),
                provider.World.CaptureRetainedFresh()));
        }
        return OriginalDeferredEarlyShadowCompleteModelCandidates.PrepareCurrentArmy(actualArmy,inputs,
            actualArmy.Counts.Queue.QueueOwnerIdentity+"/complete-shadow-Army-models");
    }

    /// <summary>Read the real current Common/View/Light caller for every
    /// complete Model. This stage does not run the order-sensitive culler or
    /// allocate tuples; the actual collection owner supplies that next order.</summary>
    public static OriginalSceneViewNativeMapShadowModelAdmissions PrepareAdmissions(
        OriginalDeferredEarlyShadowCompleteModelCandidates models,
        OriginalDeferredEarlyShadowCurrentViewMaskFields masks,
        OriginalDeferredEarlyShadowCurrentCasterLightRows lights,
        OriginalMapFreshCommonResourceSnapshot common,bool selectedShadowRecordPresent)
    {
        if(models.FrameNumber!=masks.FrameNumber||models.ViewIdentity!=masks.ViewIdentity||
            common.FrameNumber!=models.FrameNumber||common.SelectedViewOwnerIdentity!=models.ViewIdentity)
            throw new InvalidDataException("Shadow Model callers require the same current View and Common.");
        uint exclusion=common.CurrentAuxiliaryExclusion404??
            throw new NotSupportedException("Shadow Model caller requires the current Common404 writer.");
        var rows=models.Models.Select(model=>new OriginalSceneViewNativeMapShadowModelAdmission(model,
            OriginalDeferredEarlyShadowCasterAdmissionSource.PrepareCurrentFirstCasterAdmission(
                masks,model.WorldCaller,lights,common.CommonViewResource240Present,common.CommonViewFlags8,
                exclusion,model.PerViewOpacityB0,selectedShadowRecordPresent))).ToArray();
        return new(models,common,Array.AsReadOnly(rows));
    }
}

public sealed record OriginalSceneViewNativeMapShadowModelAdmission(
    OriginalDeferredEarlyShadowCurrentModelCandidate Model,
    OriginalDeferredEarlyShadowFirstCasterAdmission Caller);

public sealed record OriginalSceneViewNativeMapShadowModelAdmissions(
    OriginalDeferredEarlyShadowCompleteModelCandidates Models,
    OriginalMapFreshCommonResourceSnapshot Common,
    IReadOnlyList<OriginalSceneViewNativeMapShadowModelAdmission> Rows);
