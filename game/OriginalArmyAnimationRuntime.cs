using San14.Data;

/// <summary>Explicit source action, selected original file, and caller clock.
/// The file ID is the original RRPreview resource ID. No mission, troop count
/// or movement distance supplies an animation ID or elapsed time.</summary>
public sealed record OriginalArmyAnimationSourceInput(int Category, int ActionIndex,
    uint AnimationFileId, float CurrentTimeSeconds)
{
    public float? PreviousTimeSeconds { get; init; }
}

public sealed record OriginalArmyAnimationSourceFrame(OriginalArmyAnimationSourceInput Input,
    LoadedOriginalArmyAnimationAction Action, LoadedOriginalArmyAnimationClip Animation,
    OriginalArmyAnimationSample Current, OriginalArmyAnimationSample? Previous)
{
    public bool OriginalCurrentAnimationVerified => false;
}

public sealed record OriginalArmyAnimationLocalFrame(OriginalArmyAnimationSourceFrame Source,
    OriginalArmyAnimationLocalPose Current, OriginalArmyAnimationLocalPose? Previous)
{
    public bool HierarchyResolved => false;
}

public sealed record OriginalArmyAnimationResolvedFrame(OriginalArmyAnimationLocalFrame Local,
    OriginalArmyAnimationHierarchyPose Current, OriginalArmyAnimationHierarchyPose? Previous)
{
    public bool OriginalCurrentAnimationVerified => false;
}

/// <summary>On-demand original clip sampling for explicit animation bindings.
/// Owns decoded source/action caches, with no GPU allocations or live process
/// access. Recreate it to reload overrides. Resolving hierarchy requires caller
/// root transforms before the result can drive the native render palette.</summary>
public sealed class OriginalArmyAnimationRuntime
{
    private readonly OriginalGameRepository _repository;
    private readonly Dictionary<(int Category, int ActionIndex), LoadedOriginalArmyAnimationAction> _actions = [];
    private LoadedOriginalArmyCatalog? _catalog;

    public OriginalArmyAnimationRuntime(OriginalGameRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public int LoadedActionCount => _actions.Count;

    public OriginalArmyAnimationSourceFrame Sample(OriginalArmyAnimationSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!float.IsFinite(input.CurrentTimeSeconds) ||
            input.PreviousTimeSeconds is { } previous && !float.IsFinite(previous))
            throw new InvalidDataException("Army animation requires finite explicit current and previous times.");
        var key = (input.Category, input.ActionIndex);
        if (!_actions.TryGetValue(key, out var action))
        {
            _catalog ??= OriginalCeArmyModels.LoadCatalog(_repository);
            action = OriginalArmyAnimationResources.LoadAction(_repository, _catalog, input.Category, input.ActionIndex);
            _actions.Add(key, action);
        }
        string uri = GameResourceId.Rdb("RRPreview", input.AnimationFileId).ToString();
        var candidates = action.AnimationData.Where(item => item.Source.ResourceUri == uri).ToArray();
        if (candidates.Length != 1)
            throw new InvalidDataException($"Army action {action.ActionObject.ObjectId:X8} requires one declared animation file {uri}; explicit selection is unavailable or ambiguous.");
        var animation = candidates[0];
        var current = OriginalArmyAnimationSampler.Sample(animation.Clip, input.CurrentTimeSeconds);
        var prior = input.PreviousTimeSeconds is { } time
            ? OriginalArmyAnimationSampler.Sample(animation.Clip, time) : null;
        return new(input, action, animation, current, prior);
    }

    public OriginalArmyAnimationLocalFrame SampleLocalPose(OriginalArmyAnimationSourceInput input,
        LoadedModelTransforms sourceModel, byte[] completeCurrentLocalPoseTemplate,
        byte[]? completePreviousLocalPoseTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(sourceModel);
        ArgumentNullException.ThrowIfNull(completeCurrentLocalPoseTemplate);
        var frame = Sample(input);
        string modelUri = GameResourceId.Rdb("CharacterEditor", frame.Action.Category.ModelFileId).ToString();
        if (sourceModel.Source.ResourceUri != modelUri)
            throw new InvalidDataException("Army animation and explicit source skeleton refer to different original models.");
        if ((frame.Previous is null) != (completePreviousLocalPoseTemplate is null))
            throw new InvalidDataException("Army previous pose requires both an explicit previous time and its complete local pose template.");
        var current = OriginalArmyAnimationLocalPoseWriter.Build(sourceModel.Transforms,
            frame.Animation.Clip, frame.Current, completeCurrentLocalPoseTemplate);
        var previous = frame.Previous is { } prior
            ? OriginalArmyAnimationLocalPoseWriter.Build(sourceModel.Transforms, frame.Animation.Clip,
                prior, completePreviousLocalPoseTemplate!) : null;
        return new(frame, current, previous);
    }

    public OriginalArmyAnimationResolvedFrame SampleResolvedPose(OriginalArmyAnimationSourceInput input,
        LoadedModelTransforms sourceModel, byte[] completeCurrentLocalPoseTemplate,
        OriginalArmyAnimationRootTransform explicitCurrentRoot,
        byte[]? completePreviousLocalPoseTemplate = null,
        OriginalArmyAnimationRootTransform? explicitPreviousRoot = null,
        bool useAlternateSourceRunCount = false)
    {
        ArgumentNullException.ThrowIfNull(explicitCurrentRoot);
        var local = SampleLocalPose(input, sourceModel, completeCurrentLocalPoseTemplate, completePreviousLocalPoseTemplate);
        if ((local.Previous is null) != (explicitPreviousRoot is null))
            throw new InvalidDataException("Army previous hierarchy requires both its explicit previous time/pose and previous root transform.");
        var current = OriginalArmyAnimationHierarchy.Build(sourceModel.Transforms, local.Current,
            explicitCurrentRoot, useAlternateSourceRunCount);
        var previous = local.Previous is { } prior
            ? OriginalArmyAnimationHierarchy.Build(sourceModel.Transforms, prior, explicitPreviousRoot!, useAlternateSourceRunCount) : null;
        return new(local, current, previous);
    }
}
