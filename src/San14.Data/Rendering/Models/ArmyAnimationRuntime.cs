namespace San14.Data;

/// <summary>A selected action and clip with an explicit caller clock, before channel sampling.</summary>
public sealed record ArmyAnimationSelection(ArmyAnimationSourceInput Input,
    LoadedArmyAnimationAction Action, LoadedArmyAnimationClip Animation);

public sealed record ArmyAnimationSourceFrame(ArmyAnimationSourceInput Input,
    LoadedArmyAnimationAction Action, LoadedArmyAnimationClip Animation,
    ArmyAnimationSample Current, ArmyAnimationSample? Previous)
{
    public bool AnimationStateFidelityVerified => false;
}

public sealed record ArmyAnimationLocalFrame(ArmyAnimationSourceFrame Source,
    ArmyAnimationLocalPose Current, ArmyAnimationLocalPose? Previous)
{
    public bool HierarchyResolved => false;
}

public sealed record ArmyAnimationResolvedFrame(ArmyAnimationLocalFrame Local,
    ArmyAnimationHierarchyPose Current, ArmyAnimationHierarchyPose? Previous)
{
    public bool AnimationStateFidelityVerified => false;
}

/// <summary>On-demand original clip sampling for explicit animation bindings.
/// Owns decoded source/action caches, with no GPU allocations or live process
/// access. Recreate it to reload overrides. Resolving hierarchy requires caller
/// root transforms before the result can drive the native render palette.</summary>
public sealed class ArmyAnimationRuntime
{
    private readonly GameResourceRepository _repository;
    private readonly Dictionary<(int Category, int ActionIndex), LoadedArmyAnimationAction> _actions = [];
    private LoadedArmyCatalog? _catalog;

    public ArmyAnimationRuntime(GameResourceRepository repository, LoadedArmyCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        _catalog = catalog;
    }

    public int LoadedActionCount => _actions.Count;

    public ArmyAnimationSourceFrame Sample(ArmyAnimationSourceInput input, LoadedArmyAnimationAction? loadedAction = null)
        => Sample(Select(input, loadedAction));

    public ArmyAnimationSelection Select(ArmyAnimationSourceInput input, LoadedArmyAnimationAction? loadedAction = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!float.IsFinite(input.CurrentTimeSeconds) ||
            input.PreviousTimeSeconds is { } previous && !float.IsFinite(previous))
            throw new InvalidDataException("Army animation requires finite explicit current and previous times.");
        var key = (input.Category, input.ActionIndex);
        var action = loadedAction;
        if (action is null && !_actions.TryGetValue(key, out action))
        {
            _catalog ??= CeArmyModels.LoadCatalog(_repository);
            action = ArmyAnimationResources.LoadAction(_repository, _catalog, input.Category, input.ActionIndex);
            _actions.Add(key, action);
        }
        if (action.Category.Category != input.Category || action.ActionIndex != input.ActionIndex)
            throw new InvalidDataException("Army animation input does not select the supplied action declaration.");
        string uri = GameResourceId.Rdb("RRPreview", input.AnimationFileId).ToString();
        var candidates = action.AnimationData.Where(item => item.Source.ResourceUri == uri).ToArray();
        if (candidates.Length != 1)
            throw new InvalidDataException($"Army action {action.ActionObject.ObjectId:X8} requires one declared animation file {uri}; explicit selection is unavailable or ambiguous.");
        return new(input, action, candidates[0]);
    }

    private static ArmyAnimationSourceFrame Sample(ArmyAnimationSelection selection)
    {
        var input = selection.Input;
        var current = ArmyAnimationSampler.Sample(selection.Animation.Clip, input.CurrentTimeSeconds);
        var prior = input.PreviousTimeSeconds is { } time
            ? ArmyAnimationSampler.Sample(selection.Animation.Clip, time) : null;
        return new(input, selection.Action, selection.Animation, current, prior);
    }

    public ArmyAnimationLocalFrame SampleLocalPose(ArmyAnimationSourceInput input,
        LoadedModelTransforms sourceModel, byte[] completeCurrentLocalPoseTemplate,
        byte[]? completePreviousLocalPoseTemplate = null, LoadedArmyAnimationAction? loadedAction = null)
    {
        return CreateLocalPose(Sample(input, loadedAction), sourceModel, completeCurrentLocalPoseTemplate,
            completePreviousLocalPoseTemplate, actionSamplingEnabled: true);
    }

    private static ArmyAnimationLocalFrame CreateLocalPose(ArmyAnimationSourceFrame frame,
        LoadedModelTransforms sourceModel, byte[] completeCurrentLocalPoseTemplate,
        byte[]? completePreviousLocalPoseTemplate, bool actionSamplingEnabled)
    {
        ArgumentNullException.ThrowIfNull(sourceModel);
        ArgumentNullException.ThrowIfNull(completeCurrentLocalPoseTemplate);
        string modelUri = GameResourceId.Rdb("CharacterEditor", frame.Action.Category.ModelFileId).ToString();
        if (sourceModel.Source.ResourceUri != modelUri)
            throw new InvalidDataException("Army animation and explicit source skeleton refer to different original models.");
        if ((frame.Previous is null) != (completePreviousLocalPoseTemplate is null))
            throw new InvalidDataException("Army previous pose requires both an explicit previous time and its complete local pose template.");
        ArmyAnimationLocalPose Prepare(ArmyAnimationSample sample, byte[] template) => actionSamplingEnabled
            ? ArmyAnimationLocalPoseWriter.Build(sourceModel.Transforms, frame.Animation.Clip, sample, template)
            : new(sample.TimeSeconds, sample.FixedFrameTime, template.ToArray(), []);
        var current = Prepare(frame.Current, completeCurrentLocalPoseTemplate);
        var previous = frame.Previous is { } prior ? Prepare(prior, completePreviousLocalPoseTemplate!) : null;
        return new(frame, current, previous);
    }

    public ArmyAnimationResolvedFrame SampleResolvedPose(ArmyAnimationSourceInput input,
        LoadedModelTransforms sourceModel, byte[] completeCurrentLocalPoseTemplate,
        ArmyAnimationRootTransform explicitCurrentRoot,
        byte[]? completePreviousLocalPoseTemplate = null,
        ArmyAnimationRootTransform? explicitPreviousRoot = null,
        bool useAlternateSourceRunCount = false, LoadedArmyAnimationAction? loadedAction = null)
    {
        return SampleResolvedPose(Select(input, loadedAction), sourceModel, completeCurrentLocalPoseTemplate,
            explicitCurrentRoot, completePreviousLocalPoseTemplate, explicitPreviousRoot, useAlternateSourceRunCount);
    }

    public ArmyAnimationResolvedFrame SampleResolvedPose(ArmyAnimationSelection selection,
        LoadedModelTransforms sourceModel, byte[] completeCurrentLocalPoseTemplate,
        ArmyAnimationRootTransform explicitCurrentRoot,
        byte[]? completePreviousLocalPoseTemplate = null,
        ArmyAnimationRootTransform? explicitPreviousRoot = null,
        bool useAlternateSourceRunCount = false, bool actionSamplingEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(explicitCurrentRoot);
        var local = CreateLocalPose(Sample(selection), sourceModel, completeCurrentLocalPoseTemplate,
            completePreviousLocalPoseTemplate, actionSamplingEnabled);
        if ((local.Previous is null) != (explicitPreviousRoot is null))
            throw new InvalidDataException("Army previous hierarchy requires both its explicit previous time/pose and previous root transform.");
        var current = ArmyAnimationHierarchy.Build(sourceModel.Transforms, local.Current,
            explicitCurrentRoot, useAlternateSourceRunCount);
        var previous = local.Previous is { } prior
            ? ArmyAnimationHierarchy.Build(sourceModel.Transforms, prior, explicitPreviousRoot!, useAlternateSourceRunCount) : null;
        return new(local, current, previous);
    }
}
