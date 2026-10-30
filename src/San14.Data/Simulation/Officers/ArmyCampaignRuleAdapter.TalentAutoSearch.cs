using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public TalentAutoSearchDraft OpenTalentAutoSearchDraft(GameState state, int forceId, int districtId,
        CampaignAppRandomState? appRandom = null)
    {
        if (Seed.Language is not ("TC" or "SC")) throw new InvalidDataException("talent.auto.edition_parameters");
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        PersonReturnCommands.BindCurrent(state, current);
        _personAssignmentPrecallInputs.BindAreaAdministrationScalars(state, current);
        //716BA0's fast advice reaches280D60. The Army-return capture has
        //table identities, but does not import fixed Scenery44 positions.
        //Bind that resource once for this page; discovery47 stays owned by
        //the saved campaign, independently of its opening scenario flags.
        current.MaterializeFixedScenery(_landmarkDefinitions);
        for (int slot = 0; slot < 101; slot++)
            current.SetSceneryFlags47(slot, state.LandmarkInvestments?.GetValueOrDefault(slot)?.Flags);
        var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
            Seed.Language, _baseCapacityInputs, _repository);
        var queryState = appRandom is null ? state : state with { CampaignAppRandom32 = appRandom };
        var abilities = SearchAbilities(current);
        var fastAdvice = CreateTalentSearchFastAdvice(current, abilities);
        var page = new TalentUiQuerySource(queryState, current, forceId, districtId, false,
            (working, actor, target) => ResolveTalentAdvisorSampleCurrent(working, current, actor, target, TalentOrderKind.Search,
                () => ReadArmyDestructionSampler(working, current) ?? throw TalentUiQuerySource.Missing("sampler")),
            working => ReadArmyDestructionSampler(working, current) ?? throw TalentUiQuerySource.Missing("sampler"),
            id => ReadTalentOfficerCard(state, current, cards, id),
            (working, id, dimension) => abilities.ResolveDimension(working, id, dimension,
                (f, effect) => _baseCapacityInputs.ReadPanelModifier(working, f, effect))
                ?? throw TalentUiQuerySource.Missing("auto_ability"), state);
        var source = new TalentAutoSearchQuerySource(page, current, forceId, districtId,
            (working, id) => abilities.ResolveDimension(working, id, 3,
                (f, effect) => _baseCapacityInputs.ReadPanelModifier(working, f, effect))
                ?? throw TalentUiQuerySource.Missing("auto_Politics"), fastAdvice);
        return new(source, state.TalentAutoSearchPreferences);

    }
}
