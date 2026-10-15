namespace San14.Data;

/// <summary>A caller-constructed owned Data handle associated with one actual
/// seasonal root. This binding does not certify a CPU type or LightData payload.</summary>
public sealed record MapSeasonWorldLightDataBinding(uint RootObjectId, OwnedWorldData Data);

/// <summary>Environment Code's bulk append of the seasonal outer light list
/// into node5's exact world-light output. It retains the five roots in source
/// order, with no group splitting, child selection or implicit actor creation.</summary>
public static class MapSeasonWorldLightRegistration
{
    public static bool Append(LoadedEnvironmentWorldLightPublicationSource publication,
        MapSeasonDirectionalLightSourceSet season, OwnedWorldLists owner,
        long currentFrame, IReadOnlyList<MapSeasonWorldLightDataBinding> rootBindings)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(season);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(rootBindings);
        publication.ValidateSourceUnchanged();
        var actualPublication = EnvironmentWorldLightPublicationSource.Read(publication.FolderResource,
            publication.ProjectResource, publication.CodeResource, publication.TaskGraph);
        if (publication.Plan != actualPublication.Plan)
            throw new InvalidDataException("Environment publication plan differs from its checked source program.");
        var plan = actualPublication.Plan;
        if (plan.SourceListObjectId != season.OuterListObjectId || plan.SourceListItemIndex != 0 ||
            plan.SourceStartIndex != 0 || plan.RequestedSourceCount != -1)
            throw new NotSupportedException("World light registration requires the source Environment bulk append of the whole outer list.");
        uint destination = plan.ResolveDestination(actualPublication.OutputDataObjectIds);
        var bindings = rootBindings.ToArray();
        if (bindings.Length != season.Roots.Count)
            throw new ArgumentException("World light registration requires exactly one binding for every declared root.", nameof(rootBindings));
        var byRoot = new Dictionary<uint, OwnedWorldData>();
        foreach (var binding in bindings)
        {
            ArgumentNullException.ThrowIfNull(binding);
            ArgumentNullException.ThrowIfNull(binding.Data);
            if (!byRoot.TryAdd(binding.RootObjectId, binding.Data))
                throw new ArgumentException("World light root binding is duplicated.", nameof(rootBindings));
        }
        var ordered = season.Roots.Select(root => byRoot.TryGetValue(root.Root.ObjectId, out var data) ? data :
            throw new ArgumentException($"World light root {root.Root.ObjectId:X8} has no owned Data binding.", nameof(rootBindings))).ToArray();
        // Append validates the whole batch's frame, owner and capacity before
        // retaining any occurrence. Repeat execution preserves native duplicates.
        return owner.Append(currentFrame, destination, ordered);
    }

}
