using System.Numerics;

namespace San14.Data;

/// <summary>A caller-constructed owned Data handle associated with one actual
/// seasonal root. This binding does not certify a CPU type or LightData payload.</summary>
public sealed record OriginalMapSeasonWorldLightDataBinding(uint RootObjectId, OriginalOwnedWorldData Data);

/// <summary>Environment Code's bulk append of the seasonal outer light list
/// into node5's exact world-light output. It retains the five roots in source
/// order, with no group splitting, child selection or implicit actor creation.</summary>
public static class OriginalMapSeasonWorldLightRegistration
{
    public static bool Append(LoadedOriginalEnvironmentWorldLightPublicationSource publication,
        OriginalMapSeasonDirectionalLightSourceSet season, OriginalOwnedWorldLists owner,
        long currentFrame, IReadOnlyList<OriginalMapSeasonWorldLightDataBinding> rootBindings)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(season);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(rootBindings);
        publication.ValidateSourceUnchanged();
        var actualPublication = OriginalEnvironmentWorldLightPublicationSource.Read(publication.FolderResource,
            publication.ProjectResource, publication.CodeResource, publication.TaskGraph);
        if (publication.Plan != actualPublication.Plan)
            throw new InvalidDataException("Environment publication plan differs from its checked source program.");
        var actualSeason = OriginalMapSeasonDirectionalLightSources.Read(season.Database);
        RequireSameSeason(season, actualSeason);
        var plan = actualPublication.Plan;
        if (plan.SourceListObjectId != actualSeason.OuterListObjectId || plan.SourceListItemIndex != 0 ||
            plan.SourceStartIndex != 0 || plan.RequestedSourceCount != -1)
            throw new NotSupportedException("World light registration requires the source Environment bulk append of the whole outer list.");
        uint destination = plan.ResolveDestination(actualPublication.OutputDataObjectIds);
        var bindings = rootBindings.ToArray();
        if (bindings.Length != actualSeason.Roots.Count)
            throw new ArgumentException("World light registration requires exactly one binding for every declared root.", nameof(rootBindings));
        var byRoot = new Dictionary<uint, OriginalOwnedWorldData>();
        foreach (var binding in bindings)
        {
            ArgumentNullException.ThrowIfNull(binding);
            ArgumentNullException.ThrowIfNull(binding.Data);
            if (!byRoot.TryAdd(binding.RootObjectId, binding.Data))
                throw new ArgumentException("World light root binding is duplicated.", nameof(rootBindings));
        }
        var ordered = actualSeason.Roots.Select(root => byRoot.TryGetValue(root.Root.ObjectId, out var data) ? data :
            throw new ArgumentException($"World light root {root.Root.ObjectId:X8} has no owned Data binding.", nameof(rootBindings))).ToArray();
        // Append validates the whole batch's frame, owner and capacity before
        // retaining any occurrence. Repeat execution preserves native duplicates.
        return owner.Append(currentFrame, destination, ordered);
    }

    private static void RequireSameSeason(OriginalMapSeasonDirectionalLightSourceSet supplied,
        OriginalMapSeasonDirectionalLightSourceSet actual)
    {
        if (supplied.Source != actual.Source || supplied.OuterListObjectId != actual.OuterListObjectId ||
            supplied.Roots.Count != actual.Roots.Count)
            throw new InvalidDataException("Season world light source identity or outer topology changed.");
        for (int i = 0; i < actual.Roots.Count; i++)
        {
            var a = supplied.Roots[i]; var b = actual.Roots[i];
            if (!Same(a.Root, b.Root) || a.ProviderListObjectId != b.ProviderListObjectId ||
                a.Children.Count != b.Children.Count || !Same(a.Selector, b.Selector) ||
                !a.Children.Zip(b.Children).All(pair => Same(pair.First, pair.Second)))
                throw new InvalidDataException("Season world light metadata differs from its re-read source declaration.");
        }
    }

    private static bool Same(OriginalDirectionalLightDeclaration a, OriginalDirectionalLightDeclaration b) =>
        a.ObjectId == b.ObjectId && a.SourceGroupMask == b.SourceGroupMask &&
        a.WorldQuaternions.SelectMany(Components).Select(Bits).SequenceEqual(b.WorldQuaternions.SelectMany(Components).Select(Bits)) &&
        a.Colors.SelectMany(Components).Select(Bits).SequenceEqual(b.Colors.SelectMany(Components).Select(Bits));
    private static bool Same(OriginalMapSeasonLightSelectorDeclaration a, OriginalMapSeasonLightSelectorDeclaration b) =>
        a.ObjectId == b.ObjectId && a.ObjectKey == b.ObjectKey && a.FirstWords.SequenceEqual(b.FirstWords) &&
        a.SecondWords.SequenceEqual(b.SecondWords) && a.Scalars.Select(Bits).SequenceEqual(b.Scalars.Select(Bits)) &&
        a.FirstAxis.Select(Bits).SequenceEqual(b.FirstAxis.Select(Bits)) && a.SecondAxis.Select(Bits).SequenceEqual(b.SecondAxis.Select(Bits)) &&
        a.Indices.SequenceEqual(b.Indices);
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);
    private static IEnumerable<float> Components(Vector4 value) => [value.X, value.Y, value.Z, value.W];
    private static IEnumerable<float> Components(Vector3 value) => [value.X, value.Y, value.Z];
}
