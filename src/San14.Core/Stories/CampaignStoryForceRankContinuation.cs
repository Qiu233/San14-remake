using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace San14.Core;

public enum CampaignStoryForceRankStage { KingdomName = 0, Messages = 1 }
public sealed record CampaignStoryForceRankMessage(string Text, TalentOfficerPresentationContext? Speaker);

/// <summary>The actual in-flight rank writer. Its world mutations precede
/// Messages and are never replayed while the real modal owner is waiting.</summary>
public sealed record CampaignStoryForceRankContinuation(int ForcePhysicalSlot, int Rank,
    int RequestedKingdomNameId, int RulerPhysicalSlot, int EmperorPhysicalSlot,
    bool SelfProclaimed, int ImperialOwnerForceSlot, CampaignStoryForceRankStage Stage)
{
    [JsonRequired] public EventSourceReference Source { get; init; } = null!;
    [JsonRequired] public long StorySequence { get; init; }
    [JsonRequired] public int OperationIndex { get; init; }
    [JsonRequired] public int SourceOffset { get; init; }
    public ImmutableArray<CampaignEndingKingdomChoice> KingdomChoices { get; init; } = [];
    public int? AutomaticKingdomNameId { get; init; }
    public bool KingdomSelectionResolved { get; init; }
    public int? SelectedKingdomNameId { get; init; }
    public ImmutableArray<CampaignStoryForceRankMessage> Messages { get; init; } = [];
    public int NextMessage { get; init; }
    public bool MessagesCompleted { get; init; }

    [JsonIgnore] public bool IsAwaitingInput => Stage == CampaignStoryForceRankStage.KingdomName
        ? !KingdomSelectionResolved : !MessagesCompleted;

    internal void Validate()
    {
        PlayableEventStateValidation.ValidateSource(Source);
        if (StorySequence < 1 || OperationIndex < 0 || SourceOffset < 0 || ForcePhysicalSlot is < 0 or > 51 ||
            Rank is < 1 or > 12 || RulerPhysicalSlot is < 0 or >= 1851 || EmperorPhysicalSlot is < 0 or >= 1851 ||
            ImperialOwnerForceSlot is < 0 or > 51 || !Enum.IsDefined(Stage) || KingdomChoices.IsDefault ||
            KingdomChoices.Any(choice => choice is null || choice.Id is < 1 or > 100 || choice.Name is null || choice.Description is null) ||
            AutomaticKingdomNameId is < 0 or > 100 ||
            !KingdomSelectionResolved && SelectedKingdomNameId is not null ||
            SelectedKingdomNameId is < 1 or > 100 ||
            Stage == CampaignStoryForceRankStage.KingdomName && SelectedKingdomNameId is int selected && !KingdomChoices.Any(choice => choice.Id == selected) ||
            Messages.IsDefault || Messages.Any(message => message is null || message.Text is null ||
                message.Speaker is { } speaker && !CampaignStoryPreparationValidation.Speaker(speaker)) ||
            NextMessage < 0 || NextMessage > Messages.Length ||
            Stage == CampaignStoryForceRankStage.KingdomName && (Messages.Length != 0 || NextMessage != 0 || MessagesCompleted) ||
            Stage == CampaignStoryForceRankStage.Messages && (Messages.Length == 0 || MessagesCompleted != (NextMessage == Messages.Length)))
            throw new InvalidDataException("The ordinary Story rank continuation is incomplete or invalid.");
    }
}
