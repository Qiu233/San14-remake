using San14.Core;

namespace San14.Data;

public sealed record DiplomacyNegotiationPage(int DisplayedForceId, int RelationLevel,
    AreaAdministrationQuery<AreaAdministrationOfficerCard>? Ruler, bool CanAccept, string? NamedCondition);

public sealed partial class ArmyCampaignRuleAdapter
{
    public DiplomacyNegotiationPage QueryDiplomacyNegotiationPage(GameState state,
        DiplomacyNegotiationRequest request, int playerForceId)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        PersonReturnCommands.BindCurrent(state, current);
        return ReadDiplomacyNegotiationPageCurrent(state, current, request, playerForceId,
            request.Negotiation?.Proposal ?? request.InitialOffer);
    }

    internal DiplomacyNegotiationPage ReadDiplomacyNegotiationPageCurrent(GameState state, SourceWorldContext current,
        DiplomacyNegotiationRequest request, int playerForceId, DiplomacyOffer offer)
    {
        bool sender = request.ForceId == playerForceId;
        bool spy = request.Negotiation?.Stage != DiplomaticNegotiationStage.RecipientDecision &&
            (request.Negotiation is not null || sender);
        int shown = spy ? sender ? request.TargetForceId : request.ForceId : request.Kind == DiplomaticMissionKind.Goodwill || !sender
            ? request.ForceId : request.TargetForceId;
        AreaAdministrationQuery<AreaAdministrationOfficerCard>? ruler = null;
        if (!spy && sender && request.Kind != DiplomaticMissionKind.Goodwill)
        {
            var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
                Seed.Language, _baseCapacityInputs, _repository);
            ruler = ReadTalentOfficerCard(state, current, cards,
                state.Forces[shown].RulerId ?? throw DiplomacyMissing("negotiation_ui_recipient_ruler"));
        }
        int relation = ReadDiplomacyRelationCurrent(state, current, request.ForceId, request.TargetForceId).Level;
        bool canAccept = true;
        if (sender && request.Kind != DiplomaticMissionKind.Goodwill &&
            (offer.Gold != 0 || offer.Supplies != 0))
        {
            int person = current.ResolveOfficerSource(state.Forces[request.ForceId].RulerId
                ?? throw DiplomacyMissing("negotiation_ui_sender_ruler"))
                ?? throw DiplomacyMissing("negotiation_ui_ruler");
            int home = current.ReadPersonFirstLocation(person) ?? throw DiplomacyMissing("negotiation_ui_ruler_home");
            var sourceBase = BaseLookup.Find(home, current);
            var escrow = state.PersonnelCommands?.Diplomacies?.GetValueOrDefault(request.TaskId)?.InitialOffer;
            bool gold = offer.Gold != 0;
            int budget = current.ReadBaseResource(sourceBase, gold ? BaseResourceKind.Gold : BaseResourceKind.Supplies)
                ?? throw DiplomacyMissing("negotiation_ui_budget");
            int reserved = gold ? escrow?.Gold ?? 0 : escrow?.Supplies ?? 0;
            canAccept = (gold ? offer.Gold : offer.Supplies) <= unchecked(budget + reserved);
        }
        string? named = null;
        if (offer.ItemId is int item && Valid(RegistryKind.Item,item))
        {
            var held=state.Items?.GetValueOrDefault(item)??throw DiplomacyMissing("negotiation_ui_item");
            named = _scenario.Snapshot.ItemDefinitions?.Definitions.FirstOrDefault(value => value.Slot == held.DefinitionId)?.Name
                ?? throw DiplomacyMissing("negotiation_ui_item_name");
        }
        else if (offer.AreaId is int area && Valid(RegistryKind.Area,area))
            named = _scenario.Snapshot.Areas.FirstOrDefault(value => value.Slot == area)?.Name
                ?? throw DiplomacyMissing("negotiation_ui_area_name");
        return new(shown, relation, ruler, canAccept, named);

        bool Valid(RegistryKind kind,int physical)
        {
            var reference=current.ReadRegistryTableReference(kind,physical)
                ??throw DiplomacyMissing("negotiation_ui_condition_pointer");
            return current.ReadRegistryObjectNativeValidity(reference)
                ??throw DiplomacyMissing("negotiation_ui_condition_validity");
        }
    }
}
