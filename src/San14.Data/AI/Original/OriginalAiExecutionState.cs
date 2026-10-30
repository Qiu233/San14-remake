using System.Collections.Immutable;

namespace San14.Data.AI.Original;

/// <summary>Identity of an original-policy actor. ArmyId is the domain unit
/// identity; GroupId identifies the existing military group, not a District.</summary>
public readonly record struct OriginalAiActorIdentity(int Layer, int ForceId,
    int? DistrictId = null, int? GroupId = null, int? ArmyId = null);

/// <summary>Only an entered control node's actual next/running child is saved.
/// Resource identities are data keys, never executable addresses.</summary>
public readonly record struct OriginalAiRunningChild(uint BundleKey, int NodeIndex, int ChildIndex);

public sealed record OriginalAiActorExecution(OriginalAiActorIdentity Actor,
    ImmutableArray<OriginalAiRunningChild> RunningChildren);
