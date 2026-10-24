using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public DiplomacyUiDraft OpenDiplomacyUiDraft(GameState state, int forceId, int districtId,
        DiplomaticMissionKind kind, CampaignAppRandomState? appRandom = null)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        PersonReturnCommands.BindCurrent(state, current);
        var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
            Seed.Language, _baseCapacityInputs, _repository);
        var queryState = appRandom is null ? state : state with { CampaignAppRandom32 = appRandom };
        return new(new DiplomacyUiSource(this, queryState, current, _scenario, forceId, districtId, kind, Card));

        AreaAdministrationQuery<AreaAdministrationOfficerCard> Card(int id)
        {
            try
            {
                int physical = _definitions.ResolveOfficerSource(id) ?? throw DiplomacyUiSource.Missing("card_Person_owner");
                bool valid = current.ReadPersonNativeValidity(physical) ?? throw DiplomacyUiSource.Missing("card_Person_validity");
                //701220/701C20/7024F0/7032F0 mount context10. Their fresh
                //89BCC0 owner leaves the trait observer1C0 null; the supplied
                //8A3130 callback is the person's name, not that observer.
                var card = Seed.Language == "TC" ? cards.ReadCity(0, physical, valid, 10)
                    : cards.ReadTransport(0, physical, valid);
                if (Seed.Language != "TC" && valid) card = card with
                { Traits = AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>.Unknown("diplomacy.ui.SC_trait_context10") };
                return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Known(card);
            }
            catch (InvalidDataException missing)
            { return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown(missing.Message); }
        }
    }
}
