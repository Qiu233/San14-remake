using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>Policy actors use domain identities. Army identities cannot
/// rebind to a different unit that later reuses its source table slot.</summary>
public enum OriginalAiPolicyActorKind { Army = 1, MilitaryGroup = 2, District = 3, Force = 4 }
public readonly record struct OriginalAiPolicyActor(OriginalAiPolicyActorKind Kind, int Id);
public readonly record struct OriginalAiPolicyControl(uint BundleKey, int NodeIndex, int ChildIndex);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(OriginalAiIntegerValue), "integer")]
[JsonDerivedType(typeof(OriginalAiNullObjectValue), "null-object")]
[JsonDerivedType(typeof(OriginalAiEntityValue), "entity")]
[JsonDerivedType(typeof(OriginalAiHexValue), "hex")]
[JsonDerivedType(typeof(OriginalAiMilitaryGroupValue), "military-group")]
[JsonDerivedType(typeof(OriginalAiTradeCountryValue), "trade-country")]
[JsonDerivedType(typeof(OriginalAiHireOfferValue), "hire-offer")]
[JsonDerivedType(typeof(OriginalAiFormationValue), "formation")]
[JsonDerivedType(typeof(OriginalAiTacticValue), "tactic")]
[JsonDerivedType(typeof(OriginalAiListValue), "list")]
[JsonDerivedType(typeof(OriginalAiSharedListValue), "shared-list")]
[JsonDerivedType(typeof(OriginalAiRegionValue), "region")]
[JsonDerivedType(typeof(OriginalAiFacilityDefinitionValue), "facility-definition")]
[JsonDerivedType(typeof(OriginalAiGemDefinitionValue), "gem-definition")]
public abstract record OriginalAiBlackboardValue;

public sealed record OriginalAiIntegerValue(int Value) : OriginalAiBlackboardValue;
public sealed record OriginalAiNullObjectValue : OriginalAiBlackboardValue;
public sealed record OriginalAiEntityValue(WorldEntityKind EntityKind, int Id) : OriginalAiBlackboardValue;
public sealed record OriginalAiHexValue(int Id) : OriginalAiBlackboardValue;
public sealed record OriginalAiMilitaryGroupValue(int Id) : OriginalAiBlackboardValue;
public sealed record OriginalAiTradeCountryValue(int Id) : OriginalAiBlackboardValue;
public sealed record OriginalAiHireOfferValue(int Id) : OriginalAiBlackboardValue;
public sealed record OriginalAiFormationValue(int Id) : OriginalAiBlackboardValue;
public sealed record OriginalAiTacticValue(int Id) : OriginalAiBlackboardValue;
/// <summary>A snapshot-local alias. It is not a native pointer, heap slot or
/// World entity identity. Multiple keys may reference this same list.</summary>
public sealed record OriginalAiListValue(int ListId) : OriginalAiBlackboardValue;
/// <summary>A policy-bank list identity shared by actual actor contexts.
/// It preserves reference aliases, order and duplicates without a native pointer.</summary>
public sealed record OriginalAiSharedListValue(int ListId) : OriginalAiBlackboardValue;
/// <summary>Identity in the normal immutable region-definition table.</summary>
public sealed record OriginalAiRegionValue(int Id) : OriginalAiBlackboardValue;
/// <summary>Static construction-definition identity, independent of a placed object.</summary>
public sealed record OriginalAiFacilityDefinitionValue(int Id) : OriginalAiBlackboardValue;
/// <summary>Tactic-book definition identity, independent of an Item instance.</summary>
public sealed record OriginalAiGemDefinitionValue(int Id) : OriginalAiBlackboardValue;
public sealed record OriginalAiBlackboardBinding(uint Key, OriginalAiBlackboardValue Value);

public enum OriginalAiBlackboardListKind
{
    Officers, HireOffers, Areas, Footholds, Forces, Armies, Districts, Hexes,
    CautionRelations, MilitaryGroups, Formations, Tactics, CautionGroups, MapObjects, Landmarks, FacilityDefinitions, GemDefinitions
}
public sealed record OriginalAiBlackboardList(int ListId, OriginalAiBlackboardListKind Kind,
    ImmutableArray<OriginalAiBlackboardValue> Values);
public sealed record OriginalAiBlackboardEvaluation(OriginalAiBlackboardValue Receiver, int Value);

/// <summary>Only the actual policy context's typed values and reachable list
/// aliases. No borrowed World, resource data, delegate or query cache is saved.</summary>
public sealed record OriginalAiBlackboardSnapshot(
    ImmutableArray<OriginalAiBlackboardBinding> Bindings,
    ImmutableArray<OriginalAiBlackboardList> Lists)
{
    public static OriginalAiBlackboardSnapshot Empty { get; } = new([], []);
}

public sealed record OriginalAiPolicyActorState(OriginalAiPolicyActor Actor, uint BundleKey,
    ImmutableArray<OriginalAiPolicyControl> Controls, OriginalAiBlackboardSnapshot Blackboard);
/// <summary>The actual loaded group prototype's immutable registration
/// inputs. Domain group creation can clone them without loading resources or
/// retaining a Data adapter inside Core.</summary>
public sealed record OriginalAiMilitaryGroupPrototype(uint BundleKey, OriginalAiBlackboardSnapshot InitialBlackboard);

/// <summary>The policy source hashes are those already produced by normal
/// resource loading. They are never recomputed during an actor query/update.</summary>
public sealed record OriginalAiPolicySource(string BehaviorResourceUri, string BehaviorSha256,
    string ScriptResourceUri, string ScriptSha256);
public sealed record OriginalAiPolicyRootBundles(uint Force, uint District, uint MilitaryGroup, uint Army);
public sealed record OriginalAiPolicyExecutionState(OriginalAiPolicySource Source,
    ImmutableArray<OriginalAiPolicyActorState> Actors,
    ImmutableArray<OriginalAiBlackboardEvaluation> Evaluations)
{
    public OriginalAiPolicyRootBundles? RootBundles { get; init; }
}

public sealed partial record GameState
{
    /// <summary>Null retains an uninstalled/legacy-unknown original-policy
    /// context owner. Load migration never creates fresh policy contexts.</summary>
    public OriginalAiPolicyExecutionState? OriginalAiPolicyExecution { get; init; }
}
