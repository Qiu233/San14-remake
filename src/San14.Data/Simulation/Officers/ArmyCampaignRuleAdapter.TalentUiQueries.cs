using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public TalentUiDraft OpenTalentUiDraft(GameState state, int forceId, int districtId, bool recruitment,
        CampaignAppRandomState? appRandom = null)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        PersonReturnCommands.BindCurrent(state, current);
        current.MaterializeFixedScenery(_landmarkDefinitions);
        for (int slot = 0; slot < 101; slot++)
            current.SetSceneryFlags47(slot, state.LandmarkInvestments?.GetValueOrDefault(slot)?.Flags);
        var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
            Seed.Language, _baseCapacityInputs, _repository);
        var queryState = appRandom is null ? state : state with { CampaignAppRandom32 = appRandom };
        var kind = recruitment ? TalentOrderKind.Recruit : TalentOrderKind.Search;
        var abilities = SearchAbilities(current);
        var source = new TalentUiQuerySource(queryState, current, forceId, districtId, recruitment,
            (working, actor, target) => ResolveTalentAdvisorSampleCurrent(working, current, actor, target, kind,
                () => ReadArmyDestructionSampler(working, current) ?? throw TalentUiQuerySource.Missing("sampler")),
            working => ReadArmyDestructionSampler(working, current) ?? throw TalentUiQuerySource.Missing("sampler"),
            id => ReadTalentOfficerCard(state, current, cards, id),
            (working, id, dimension) => abilities.ResolveDimension(working, id, dimension,
                (f, effect) => _baseCapacityInputs.ReadPanelModifier(working, f, effect))
                ?? throw TalentUiQuerySource.Missing("recommend_ability"));
        return new(source);
    }

    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryTalentOfficerCard(GameState state, int officerId)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            PersonReturnCommands.BindCurrent(state, current);
            var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
                Seed.Language, _baseCapacityInputs, _repository);
            return ReadTalentOfficerCard(state, current, cards, officerId);
        }
        catch (InvalidDataException missing) { return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown(missing.Message); }
    }

    private AreaAdministrationQuery<AreaAdministrationOfficerCard> ReadTalentOfficerCard(GameState state,
        SourceWorldContext current, AreaAdministrationOfficerCardSource cards, int officerId)
    {
        try
        {
            int physical = _definitions.ResolveOfficerSource(officerId) ?? throw TalentUiQuerySource.Missing("card_Person_owner");
            bool valid = current.ReadPersonNativeValidity(physical) ?? throw TalentUiQuerySource.Missing("card_Person_validity");
            // Share portrait/current abilities/flag without pretending that
            // the personnel pane uses the region or transport trait context.
            var card = cards.ReadTransport(0, physical, valid) with
            { Traits = AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>.Unknown("talent.ui.officer_trait_context") };
            return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Known(card);
        }
        catch (InvalidDataException missing) { return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown(missing.Message); }
    }
}
