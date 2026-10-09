using San14.Data;

public sealed record NativeArmyCurrentEligibility(NativeMapFrameContext Context,
    IReadOnlyDictionary<string, bool> CompleteCurrentActorEligibility, string CurrentSourceProvenance);

/// <summary>Detached source CPU inventory borrowed from the actual BeforeFrame
/// Army owner. Actor identities preserve both ordinary and flag populations.</summary>
public sealed class NativeArmyCpuPrimitivePopulation
{
    internal NativeArmyCpuPrimitivePopulation(NativeMapArmyPreparedFrame prepared,
        IReadOnlyList<(string Family, string Actor, ArmyPrimitivePublicationSource Source)> actors)
        => (Prepared, Actors) = (prepared, actors);
    public NativeMapArmyPreparedFrame Prepared { get; }
    public NativeMapFrameContext Context => Prepared.Context;
    public IReadOnlyList<(string Family, string Actor, ArmyPrimitivePublicationSource Source)> Actors { get; }
    public int CompleteSourcePrimitiveCount => Actors.Sum(a => a.Source.OrdinaryModelCpuCount);
}

/// <summary>A source-created Army modifier owner. Import, unknown overrides or
/// changes to native plugin/stencil state invalidate its finite source recipe.</summary>
public sealed class NativeArmyPrimitiveAdmissionOwner
{
    private readonly Dictionary<string, ArmyPrimitivePublicationSource> models = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ModelOrdinaryNativeStencilSource> stencilSources = new(StringComparer.Ordinal);
    private bool retained = true;
    public string OwnerIdentity { get; } = "owned-army-native-primitive:" + Guid.NewGuid().ToString("N");
    public static NativeArmyPrimitiveAdmissionOwner CreateFreshNoExternalPrimitiveAssignments() => new();
    public void MarkImportedEditedOrUnresolved(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter); retained = false; models.Clear(); stencilSources.Clear(); }

    public NativeArmyCpuPrimitivePopulation Capture(NativeMapArmyPreparedFrame actualBeforeFrame)
    {
        ArgumentNullException.ThrowIfNull(actualBeforeFrame); RequireRetained();
        if (actualBeforeFrame.Unresolved.Count != 0)
            throw new InvalidDataException("The Army CPU primitive inventory contains unresolved source actors.");
        var output = new List<(string, string, ArmyPrimitivePublicationSource)>();
        foreach (var frame in actualBeforeFrame.Ordinary.Values)
        foreach (var actor in frame.Publications.Values)
        {
            var model = actualBeforeFrame.Models[actor.Category];
            output.Add(("ArmyOrdinary", $"remake-army/{frame.UnitId}/{actor.ComponentId}",
                Source(model.Catalog, actor.Category, model.Transforms.Source)));
        }
        foreach (var frame in actualBeforeFrame.Flags.Values)
        foreach (var actor in frame.Actors)
            output.Add(("ArmyFlag", actor.InstanceKey, Source(actor.Source.Catalog, actor.Category, actor.Source.Model.Source)));
        if (output.Select(a => a.Item2).Distinct(StringComparer.Ordinal).Count() != output.Count)
            throw new InvalidDataException("Current Army CPU actors have duplicate source identities.");
        return new(actualBeforeFrame, output.AsReadOnly());
    }

    public ArmyCurrentPrimitivePublication PrepareCurrent(NativeArmyCpuPrimitivePopulation population,
        MapSourceCreatedLightingPacket lighting, MapFreshCommonResourceSnapshot common,
        NativeArmyCurrentEligibility actualEligibility)
    {
        ArgumentNullException.ThrowIfNull(population); ArgumentNullException.ThrowIfNull(actualEligibility); RequireRetained();
        if (!ReferenceEquals(actualEligibility.Context, population.Context) ||
            population.Context.FrameNumber != lighting.FrameNumber || common.CurrentViewAux3F2 is not byte selector ||
            string.IsNullOrWhiteSpace(actualEligibility.CurrentSourceProvenance) ||
            !population.Actors.Select(a => a.Actor).Order(StringComparer.Ordinal)
                .SequenceEqual(actualEligibility.CompleteCurrentActorEligibility.Keys.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Army eligibility must cover every actor in the exact accepted BeforeFrame context.");
        var actors = population.Actors.Select(a => new ArmyCurrentPrimitiveActor(a.Actor, a.Source,
            actualEligibility.CompleteCurrentActorEligibility[a.Actor], true, a.Source.Exclusion78,
            OwnerIdentity + "/retained-owned-WorldPQ-prefix;" + actualEligibility.CurrentSourceProvenance)).ToArray();
        return ArmyCurrentPrimitivePublication.Prepare(population.Context.FrameNumber, lighting, selector,
            common.CurrentAuxiliaryExclusion404, actors, OwnerIdentity + "/" + population.Prepared.SourceRevision);
    }

    public ArmyCurrentPrimitivePublication PrepareCurrentEmptyWorld(
        NativeArmyCpuPrimitivePopulation population, MapSourceCreatedLightingPacket lighting,
        MapFreshCommonResourceSnapshot common, MapWorldKind1CurrentQueue queue)
    {
        ArgumentNullException.ThrowIfNull(population); RequireRetained();
        if (population.Context.FrameNumber != lighting.FrameNumber || population.Actors.Count != 0 ||
            population.CompleteSourcePrimitiveCount != 0 || population.Prepared.OrdinaryActors != 0 ||
            population.Prepared.FlagActors != 0 || population.Prepared.Unresolved.Count != 0)
            throw new NotSupportedException("World empty Army admission requires the actual complete empty BeforeFrame inventory.");
        return ArmyCurrentPrimitivePublication.PrepareEmptyWorld(lighting, common, queue,
            Array.Empty<ArmyCurrentPrimitiveActor>(), OwnerIdentity + "/" + population.Prepared.SourceRevision);
    }

    public NativeArmyCurrentAdmission BindCurrentFrame(NativeArmyCpuPrimitivePopulation population,
        ArmyCurrentPrimitivePublication publication, MapSourceCreatedStaticPrimitiveAdmissionPlan staticAdmission)
    {
        RequireRetained();
        if (population.Context.FrameNumber != staticAdmission.FrameNumber || publication.Counts.FrameNumber != staticAdmission.FrameNumber ||
            publication.Counts.ViewIdentity != staticAdmission.ViewIdentity)
            throw new InvalidDataException("Army admission belongs to another current accepted View frame.");
        var armyCounts = publication.Counts.CompleteCurrentCpuCounts;
        var staticCounts = staticAdmission.StaticKind1RecordCounts;
        if (!armyCounts.Keys.Order().SequenceEqual(staticCounts.Keys.Order()) ||
            staticAdmission.Frame.ViewFields.Kind1Records.Any(r => r.CurrentCpuPrimitiveCount != checked(armyCounts[r.Mask78] + staticCounts[r.Mask78])) ||
            publication.Primitives.Count != population.CompleteSourcePrimitiveCount ||
            population.Actors.Any(a => a.Source.OrdinarySourceDrawIndices.Any(d =>
                publication.Require(a.Actor, d).CurrentSourceIdentity != a.Source.Identity)))
            throw new InvalidDataException("Army publication does not match the complete static plus Army View counters and actual source primitives.");
        return new(this, population, publication, staticAdmission.Frame);
    }

    public NativeArmyCurrentAdmission BindCurrentWorldFrame(NativeArmyCpuPrimitivePopulation population,
        ArmyWorldPrimitivePublication publication, MapSourceCreatedStaticPrimitiveAdmissionPlan staticAdmission)
    {
        ArgumentNullException.ThrowIfNull(population); ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(staticAdmission); RequireRetained();
        if (staticAdmission.CurrentWorldQueue is not { } queue || !ReferenceEquals(queue, publication.Counts.Queue) ||
            !ReferenceEquals(queue, staticAdmission.Frame.ViewFields.CurrentWorldQueue) ||
            population.Context.FrameNumber != staticAdmission.FrameNumber ||
            publication.Primitives.Count != population.CompleteSourcePrimitiveCount ||
            publication.ActualVisibilityCamera != population.Context.PreparedCamera ||
            !population.Actors.Select(a => (a.Family, a.Actor, a.Source.Identity)).ToHashSet()
                .SetEquals(publication.Actors.Select(a => (a.Family, a.ActorIdentity, a.Source.Identity))))
            throw new InvalidDataException("Army World admission needs this exact BeforeFrame inventory, visibility Camera and record-owner queue.");
        var admission = new NativeArmyCurrentAdmission(this, population, publication, staticAdmission);
        admission.RequireCurrentWorldPublication(population.Prepared, publication); return admission;
    }

    internal void RequireCurrentOwner() => RequireRetained();

    internal ModelOrdinaryNativeStencilReceipt PrepareStencil(NativeArmyCurrentAdmission admission,
        ModelGbufferInstancePacket packet, ModelShaderInterfaceSource selectedInterface,
        MapSourceCreatedKind1ViewRecord record)
    {
        RequireRetained();
        if (packet.Raster.NativeStencil is not null)
            throw new InvalidDataException("Army native stencil cannot replace an externally assigned stencil recipe.");
        var graph = admission.Population.Context.TerrainFrame.Source.Graph;
        var queue = ModelRendererStateSourceReader.ReadQueue(graph, 49);
        var source = ModelOrdinaryNativeStencilSource.ReadFreshSelected(queue, selectedInterface, packet.Shader);
        if (!stencilSources.TryGetValue(source.SourceKey, out var cached)) stencilSources.Add(source.SourceKey, cached = source);
        var current = new ModelFreshOwnedStencilPublication(admission.Frame.FrameNumber, admission.Publication.Counts.ViewIdentity,
            packet.InstanceKey, OwnerIdentity, cached.ProviderObjectId, cached.ShaderSha256, true, true, true, true, true,
            OwnerIdentity + "/actual-Army-selected-provider/frame=" + admission.Frame.FrameNumber);
        return cached.PrepareCurrent(new(admission.Frame.FrameNumber, admission.Publication.Counts.ViewIdentity,
            record.Mask78, true, record.CurrentCpuPrimitiveCount, true, record.CurrentCountPublicationIdentity),
            admission.Frame.Common, admission.Frame.Lighting.ContextFlags3E4, current);
    }
    private ArmyPrimitivePublicationSource Source(LoadedArmyCatalog catalog, int category, LoadedRdbAsset model)
    {
        string key = catalog.Catalog.Source.DecodedSha256 + "/" + category + "/" + model.DecodedSha256;
        if (!models.TryGetValue(key, out var source)) models.Add(key, source = ArmyPrimitivePublicationSource.ReadKnownCategory(catalog, category, model));
        source.ValidateSourceUnchanged(); return source;
    }
    private void RequireRetained()
    { if (!retained) throw new InvalidDataException("Army native primitive generation was edited, imported or unresolved."); }
}

/// <summary>The real source record order drives Army raster submission.
/// A family prepares its entire CPU source batch before this method; nothing
/// uses accepted draw statistics to invent current View counts.</summary>
public sealed class NativeArmyCurrentAdmission
{
    private readonly NativeArmyPrimitiveAdmissionOwner owner;
    private readonly ArmyCurrentPrimitivePublication? legacyPublication;
    private readonly MapSourceCreatedStaticPrimitiveAdmissionPlan? worldStaticAdmission;
    private readonly IReadOnlyDictionary<string, int>? worldStaticCounts;
    internal NativeArmyCurrentAdmission(NativeArmyPrimitiveAdmissionOwner owner,
        NativeArmyCpuPrimitivePopulation population, ArmyCurrentPrimitivePublication publication,
        MapSourceCreatedF103CurrentFrame frame)
        => (this.owner, Population, legacyPublication, Frame) = (owner, population, publication, frame);
    internal NativeArmyCurrentAdmission(NativeArmyPrimitiveAdmissionOwner owner,
        NativeArmyCpuPrimitivePopulation population, ArmyWorldPrimitivePublication publication,
        MapSourceCreatedStaticPrimitiveAdmissionPlan staticAdmission)
        => (this.owner, Population, WorldPublication, Frame, worldStaticAdmission, worldStaticCounts) =
            (owner, population, publication, staticAdmission.Frame, staticAdmission, staticAdmission.StaticWorldKind1RecordCounts);
    public NativeArmyCpuPrimitivePopulation Population { get; }
    public ArmyCurrentPrimitivePublication Publication => legacyPublication ??
        throw new NotSupportedException("Record-owner World Army admission has no legacy mask-keyed publication.");
    public ArmyWorldPrimitivePublication? WorldPublication { get; }
    public MapSourceCreatedF103CurrentFrame Frame { get; }
    public void RequireCurrentWorldPublication(NativeMapArmyPreparedFrame actualPrepared,
        ArmyWorldPrimitivePublication actualPublication)
    {
        owner.RequireCurrentOwner();
        if (!ReferenceEquals(actualPrepared, Population.Prepared) || !ReferenceEquals(actualPublication, WorldPublication) ||
            worldStaticAdmission is not { } statics || statics.CurrentWorldQueue is not { } queue ||
            !ReferenceEquals(queue, actualPublication.Counts.Queue) || !ReferenceEquals(queue, Frame.ViewFields.CurrentWorldQueue) ||
            actualPrepared.Context.FrameNumber != Frame.FrameNumber || actualPublication.Counts.Queue.Binding.ViewIdentity != statics.ViewIdentity)
            throw new InvalidDataException("Army World admission was replaced or belongs to another current parent/View record queue.");
        var armyCounts = actualPublication.CompleteCountsByRecordOwner;
        var staticCounts = worldStaticCounts!;
        if (!armyCounts.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(staticCounts.Keys) ||
            Frame.ViewFields.Kind1Records.Any(r => !armyCounts.TryGetValue(r.LightOwnerIdentity, out int army) ||
                !staticCounts.TryGetValue(r.LightOwnerIdentity, out int stationary) ||
                r.CurrentCpuPrimitiveCount != checked(army + stationary)))
            throw new InvalidDataException("Army World admission differs from the actual static plus Army record-owner counts.");
    }
    public IReadOnlyList<ModelGbufferInstancePacket> PrepareFamily(NativeMapFrameContext context,
        string family, IReadOnlyList<ModelGbufferInstancePacket> completePrepared,
        IReadOnlyDictionary<string, ModelShaderInterfaceSource> actualSelectedInterfaces)
    {
        if (!ReferenceEquals(context, Population.Context)||family is not("ArmyOrdinary" or "ArmyFlag"))
            throw new InvalidDataException("Army raster admission requires the exact current accepted context.");
        var actorKeys = Population.Actors.Where(a => a.Family == family).Select(a => a.Actor).ToHashSet(StringComparer.Ordinal);
        var primitives = Publication.Primitives.Where(p => actorKeys.Contains(p.ActorIdentity)).ToArray();
        var packets = completePrepared.ToDictionary(p => p.InstanceKey, StringComparer.Ordinal);
        if (!packets.Keys.Order(StringComparer.Ordinal).SequenceEqual(primitives.Select(p => p.PacketKey).Order(StringComparer.Ordinal)) ||
            !packets.Keys.Order(StringComparer.Ordinal).SequenceEqual(actualSelectedInterfaces.Keys.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Army raster must prepare the complete actual source draw list and selected shader interfaces.");
        foreach(var actor in Population.Actors.Where(a=>a.Family==family))
        {
            actor.Source.ValidateSourceUnchanged();
            foreach(int draw in actor.Source.OrdinarySourceDrawIndices)
            {
                var packet=packets[actor.Actor+"/draw"+draw];
                if(packet.DrawIndex!=draw||packet.Model.Source.ResourceUri!=actor.Source.Model.ResourceUri||
                    packet.Model.Source.DecodedSha256!=actor.Source.Model.DecodedSha256)
                    throw new InvalidDataException("Army raster geometry differs from the exact current CPU primitive source.");
            }
        }
        var output = new List<ModelGbufferInstancePacket>();
        foreach (var record in Frame.ViewFields.Kind1Records)
        foreach (var primitive in primitives)
        {
            if (!primitive.AdmittedRecordMasks.Contains(record.Mask78)) continue;
            var packet = packets[primitive.PacketKey];
            var receipt = owner.PrepareStencil(this, packet, actualSelectedInterfaces[packet.InstanceKey], record);
            if (!receipt.ShouldSubmitPrimitive) continue;
            var native = ModelNativeStencilRaster.Bind(context, packet, receipt);
            output.Add(packet with { InstanceKey = packet.InstanceKey + "|native-record:" + record.Mask78.ToString("X8"),
                Raster = packet.Raster with { NativeStencil = native } });
        }
        return output.AsReadOnly();
    }

    /// <summary>Validate the two actual layer submissions after successful
    /// drawing. Source inventory/CPU counts alone are not GPU completion.</summary>
    public void ValidateCurrentAccepted(NativeMapArmyPopulationAcceptedFrame accepted)
    {
        if (accepted.WorldQueuesCompletedPublication is { } world)
        {
            if (!ReferenceEquals(accepted.NativePrimitiveAdmission, this) || !ReferenceEquals(accepted.Prepared, Population.Prepared) ||
                accepted.OrdinaryGeometrySubmission is not null || accepted.FlagGeometrySubmission is not null)
                throw new InvalidDataException("Army49/50 aggregate cannot be presented as legacy node53 family layers.");
            RequireCurrentWorldPublication(accepted.Prepared, world.CompleteCpuInventory);
            world.RequireAccepted();
            if (!ReferenceEquals(world.ActualTuples.Population, WorldPublication) ||
                !ReferenceEquals(world.BoundTarget48.Context, Population.Context) ||
                world.BoundTarget48.Frame.Source.Node.Index != 48 ||
                accepted.CurrentViewFamilyCandidates is not { } families || !families.SequenceEqual(world.CurrentMode16Candidates))
                throw new InvalidDataException("Accepted Army48 aggregate differs from its actual current parent, final tables or target generation.");
            return;
        }
        if(!ReferenceEquals(accepted.NativePrimitiveAdmission,this)||!ReferenceEquals(accepted.Prepared,Population.Prepared)||
            accepted.OrdinaryGeometrySubmission is not{}ordinary||accepted.FlagGeometrySubmission is not{}flags||
            accepted.FlagStatuses.Any(s=>!s.Submitted))
            throw new InvalidDataException("Army native submission does not have both complete actual current layers.");
        ValidateFamily(ordinary,"ArmyOrdinary");ValidateFamily(flags,"ArmyFlag");
    }
    private void ValidateFamily(ModelGeometrySubmittedFrame actual,string family)
    {
        actual.RequireCurrent();
        if(!ReferenceEquals(actual.Target,Population.Context.TerrainFrame))
            throw new InvalidDataException("Army native geometry submission uses another accepted target.");
        var actors=Population.Actors.Where(a=>a.Family==family).ToDictionary(a=>a.Actor,StringComparer.Ordinal);
        var expected=new List<(ArmyCurrentPrimitiveAdmission Primitive,MapSourceCreatedKind1ViewRecord Record)>();
        foreach(var record in Frame.ViewFields.Kind1Records)
        foreach(var p in Publication.Primitives.Where(p=>actors.ContainsKey(p.ActorIdentity)&&p.AdmittedRecordMasks.Contains(record.Mask78)))
            expected.Add((p,record));
        if(actual.Primitives.Count!=expected.Count)
            throw new InvalidDataException("Army native submitted batch lost a queued source primitive.");
        for(int i=0;i<expected.Count;i++)
        {
            var (primitive,record)=expected[i];var submitted=actual.Primitives[i];var source=actors[primitive.ActorIdentity].Source;
            if(submitted.PacketKey!=primitive.PacketKey+"|native-record:"+record.Mask78.ToString("X8")||
                submitted.SourceDrawIndex!=primitive.SourceDrawIndex||submitted.ModelUri!=source.Model.ResourceUri||
                submitted.ModelSha256!=source.Model.DecodedSha256||submitted.NativeStencil is not{}stencil||
                stencil.Source.CurrentPublication.FrameNumber!=Frame.FrameNumber||
                stencil.Source.CurrentPublication.ViewIdentity!=Publication.Counts.ViewIdentity||
                stencil.Source.DrawTimeState.RecordMask78!=record.Mask78)
                throw new InvalidDataException("Army native submitted primitive differs from actual current source admission.");
        }
    }
}
