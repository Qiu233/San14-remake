using San14.Core;

namespace San14.Data;

public sealed record DeploymentOfficerPanePublication(int OfficerId, string Name, OfficerPortraitRoute Portrait,
    IReadOnlyList<AreaAdministrationOfficerAbility> Abilities,
    AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>> Traits,
    AreaAdministrationQuery<int?> CommandCapacity, OfficerDetailRelations Relations)
{
    public Func<int,AreaAdministrationQuery<string?>>? TraitTooltip { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Selected setup Person data only. The unsubmitted row contributes
    /// its Person identity; no Army, draft edit or world write is performed.</summary>
    public AreaAdministrationQuery<DeploymentOfficerPanePublication> QueryDeploymentOfficerPane(GameState state, int officerId)
    {
        try
        {
            var current = CaptureOfficerCommandCapacityCurrent(state);
            _personRelationInputs.BindCurrent(state, current);
            current.EventConditions.CurrentYear = state.NativeMapSeason?.ThirdDate.Year;
            current.EventConditions.ScenarioMode = state.CampaignEventWorld?.ScenarioMode;
            int physical = current.ResolveOfficerSource(officerId) ?? throw PaneMissing("person_receiver");
            if (current.ReadPersonIdentity(physical) != officerId || current.ReadPersonNativeValidity(physical) != true ||
                !state.Officers.TryGetValue(officerId, out var person)) throw PaneMissing("person_owner");
            var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
                Seed.Language, _baseCapacityInputs, _repository);
            OfficerPortraitRoute portrait;
            try
            {
                portrait = ReadOfficerPortraitAppearance(state, current, _definitions, officerId,
                    current.ReadPersonPortraitAppearance(physical) ?? throw PaneMissing("portrait_appearance"), ArmyMapUiSources.Create());
            }
            catch (InvalidDataException error)
            { portrait = new(officerId, OfficerPortraitRouteKind.Unresolved, null, null, null, null, null, error.Message); }
            AreaAdministrationQuery<int?> capacity;
            try
            {
                var value = OfficerCommandCapacity.Resolve(state, current, physical);
                capacity = value.Capacity is int known ? AreaAdministrationQuery<int?>.Known(known)
                    : AreaAdministrationQuery<int?>.Unknown(value.UnresolvedInput ?? "deployment.officer-pane.command_capacity");
            }
            catch (InvalidDataException error) { capacity = AreaAdministrationQuery<int?>.Unknown(error.Message); }
            return AreaAdministrationQuery<DeploymentOfficerPanePublication>.Known(new(officerId, person.Name, portrait,
                cards.ReadAbilities(person, officerId, true), cards.ReadDeploymentTraits(physical), capacity,
                ReadDeploymentOfficerRelationsCurrent(state, current, physical)) { TraitTooltip = cards.ReadTraitTooltip });
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<DeploymentOfficerPanePublication>.Unknown(error.Message); }
    }
    private static InvalidDataException PaneMissing(string name) => new("deployment.officer-pane." + name);
}
