using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //1ED380's real R9=1 association removal. Draft R9=0 never enters this
    //consumer. The shared formatter/writer owns actual operands, App draws,
    //source day/location and the conditional history-notification postwrite.
    private GameState ApplyAreaAdministrationAssociationReports(GameState state,
        SourceWorldContext current, int person, PersonAssignmentPrecallResult assignment,
        List<CampaignEffectRequest> effects) =>
        ApplyPersonAssociationRemovalReports(state, current, person, assignment,
            () => (ReadArmyDestructionSampler(state, current)
                ?? throw new InvalidDataException("area-administration.association_report_World450")).Day450,
            effects);

    private static GameState PublishAreaAdministrationAssociationNotifications(GameState state,
        IReadOnlyList<CampaignEffectRequest> effects) => state with
    {
        CampaignEffectRequests = (state.CampaignEffectRequests
            ?? throw new InvalidDataException("area-administration.association_report_effect_owner")).Append(
                CampaignEffectSource.ForceAssociationRemoval,
                state.NativeClock?.UpdateOrdinal
                    ?? throw new InvalidDataException("area-administration.association_report_revision"),
                effects)
    };
}
