using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal SourceWorldContext CaptureOriginalAiDomesticCurrent(GameState state)
    {
        var current = CaptureCurrentCampaignEventQueries(state);
        // The event/route capture does not project Army58 group assignments or
        // caution eligibility. Restore these actual saved owners through the
        // same normal projection used by the owned Army-return world; never
        // substitute source constructor values for an unimported owner.
        var lifecycle = state.ReadArmyLifecycle() ?? throw OriginalAiDomesticMissing("army_lifecycle_owner");
        OwnedArmyLifecycleAuxiliaries.RestoreCurrent(current, lifecycle);
        // Current16C belongs to the saved Officer aftermath owner, not to the
        // scenario or ruler-word bank. Project its complete observations once
        // for Troops/Force evaluation and the caution relationship predicate.
        _personDestructionInputs.BindCurrent(state, current);
        _baseCapacityInputs.BindCurrent(state, current);
        return current;
    }
    internal int ReadOriginalAiDomesticFoodBasis(GameState state, int area)
    {
        var source = state.Areas.GetValueOrDefault(area)?.EconomySource ?? throw OriginalAiDomesticMissing("Area_food_basis");
        if (source.FixedDataSha256 != _scenario.FixedDataSource.Sha256 || source.ScenarioSha256 != _scenario.ScenarioSource.Sha256)
            throw OriginalAiDomesticMissing("Area_food_basis_source");
        return source.RawField46 is >= 0 and <= ushort.MaxValue ? source.RawField46 : throw OriginalAiDomesticMissing("Area_food_basis_domain");
    }
    internal int ReadOriginalAiDomesticPolitics(GameState state, SourceWorldContext current, int officerId)
    {
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        return abilities.ResolveDimension(state, officerId, 3,
            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect))
            ?? throw OriginalAiDomesticMissing("Person_politics_current");
    }
}
