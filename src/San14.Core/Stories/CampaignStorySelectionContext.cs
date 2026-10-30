using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>The six source dialogue selections, in adviser, military, civil,
/// same-family, other-family, remaining order. A null first slot is not latched;
/// the next lookup may generate the roles again. Force null is a cleared pointer,
/// distinct from the actual physical Force0 receiver.</summary>
public sealed record CampaignStoryDialogueRoles(int? ForcePhysicalSlot, ImmutableArray<int?> PersonIds);

/// <summary>One source invocation's selected people and target Forces shared
/// by Contents and Results. Actor slots correspond to -10..-14; Force slots
/// correspond to -2..-5. A null slot is an actual source reset. A null complete
/// SelectionContext on CampaignStoryRequest means unknown legacy knowledge.</summary>
public sealed record CampaignStorySelectionContext(ImmutableArray<int?> ActorIds,
    ImmutableArray<int?> TargetForcePhysicalSlots)
{
    /// <summary>Within a known selection context, null means dialogue role
    /// generation has not been invoked. A present snapshot retains nullable
    /// reset slots; a nonnull adviser is the original generation latch.</summary>
    [JsonRequired] public CampaignStoryDialogueRoles? DialogueRoles { get; init; }
}
