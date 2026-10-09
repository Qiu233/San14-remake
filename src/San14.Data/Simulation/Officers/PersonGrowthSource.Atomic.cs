using San14.Core;

namespace San14.Data;

public enum PersonGrowthOutcome { Committed, NativeSkip, CurrentInputsUnresolved }
public sealed record PersonGrowthReceipt(int OfficerId, int AbilityIndex, int RequestedGrowth, int AppliedGrowth,
    uint BeforeExperience, uint AfterExperience, int BeforeBaseAbility, int AfterBaseAbility,
    int Trait75, GrowthAdvantageReceipt Advantage7, OfficerAbilityGrowthNotice[]? Notices, string Revision);
public sealed record PersonGrowthResult(PersonGrowthOutcome Outcome, GameState State,
    PersonGrowthReceipt? Receipt);

public sealed partial class PersonGrowthSource
{
    /// <summary>Standalone atomic XP/base/growth-notice update. A committed
    /// result replaces this officer's immutable growth owner. Unchanged world
    /// aggregates are shared. NativeSkip and unknown return the input;
    /// the compound normal reward owns its later pending-merit callback.</summary>
    public PersonGrowthResult TryApply(GameState input, SourceWorldContext current,
        PersonGrowthCurrentQueries queries, int personId, int abilityIndex, int requested, int reportContext = 0) =>
        Apply(input, current, queries, personId, abilityIndex, requested, reportContext);

    //The enclosing Army operation owns the candidate. Resolve every reached
    //query before replacing this officer; unknown never publishes a prefix.
    internal PersonGrowthResult TryApplyWithinArmyOperation(GameState input, SourceWorldContext current,
        PersonGrowthCurrentQueries queries, int personId, int abilityIndex, int requested, int reportContext) =>
        Apply(input, current, queries, personId, abilityIndex, requested, reportContext);

    private PersonGrowthResult Apply(GameState input, SourceWorldContext current,
        PersonGrowthCurrentQueries queries, int personId, int abilityIndex, int requested, int reportContext)
    {
        if (!input.Officers.TryGetValue(personId, out OfficerState? person) || abilityIndex is < 0 or > 4 ||
            person.Growth is not { } growth ||
            input.NativeArmySimulation?.Parameters is not { } parameters ||
            current.StaticSource is not { } source || input.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256 ||
            input.ContentHashes.GetValueOrDefault(_source.ResourceUri) != _source.Sha256 ||
            current.ResolveOfficerSource(personId) is not int physical || current.ReadPersonNativeValidity(physical) is not bool valid)
            return Unknown();
        if (!valid) return Skip();
        var force = current.ReadPersonForce(physical);
        if (force?.PhysicalSlot is not int forceId) return Unknown();
        if (forceId is >= 46 and <= 51) return Skip();
        if (person.BaseAbilities is not { } basic) return Unknown();
        int[] raw = [basic.Leadership, basic.Strength, basic.Intelligence, basic.Politics, basic.Charm];
        if (raw[abilityIndex] == OfficerGrowthRules.MaximumBaseAbility) return Skip();
        //The original tail can loop indefinitely for abnormal >100 bases.
        //The source-created normalized profile stays within100; reject an
        //unsupported current value rather than clamp or fabricate termination.
        if (raw[abilityIndex] is < 0 or > OfficerGrowthRules.MaximumBaseAbility ||
            OfficerGrowthSourceAdapter.ReadExperience(growth, abilityIndex) is not uint experience ||
            current.ReadForceNativeValidity(force) is not bool forceValid ||
            TacticRemakeCurrentTraits.CommanderEffectSum(current, physical, 75) is not int trait ||
            queries.ReadAdvantage7(input, current, force) is not { } advantage ||
            queries.ReadCurrent(input, SimulationParameterKeys.PersonGrowth.TraitGrowthScale) is not int traitScale ||
            queries.ReadCurrent(input, SimulationParameterKeys.PersonGrowth.ProvinceAdvantageGrowthScale) is not int advantageScale ||
            BattleRemakeCurrentSettings.ReadSelectorE58(input) is not int selector)
            return Unknown();
        int policy = 0, policyScale = 0;
        if (forceValid)
        {
            if (!input.Forces.TryGetValue(forceId, out var savedForce) ||
                input.NativeArmySimulation.Forces.GetValueOrDefault(forceId)?.Flags12 is not byte flags) return Unknown();
            if ((flags & 8) != 0)
            {
                //22C230 reaches the current ruler184 and Doctrine validity.
                //234160 also tests its current localized name, beyond physical
                //slot identity. That owner is not modeled yet; do not replace
                //it with a nonzero slot or the independent Core doctrine view.
                return Unknown();
            }
            if (BattleRemakeCurrentForcePolicies.ReadForceEffect(input, savedForce, 40) is not int value ||
                queries.ReadCurrent(input, SimulationParameterKeys.PersonGrowth.PolicyGrowthScale) is not int scale40) return Unknown();
            policy = value; policyScale = scale40;
        }
        int applied = OfficerGrowthRules.CalculateExperienceGain(requested, trait, traitScale,
            advantage.Value, advantageScale, forceValid, policy, policyScale);
        if (selector == 0)
        {
            if (BattleRemakeCurrentSettings.ReadAdvancedParameter(input, 24) is not int percent) return Unknown();
            applied = unchecked(applied * percent) / 100;
        }
        var update = OfficerGrowthRules.AdvanceAbility(experience, raw[abilityIndex],
            applied, nextBase => ReadColumn(nextBase, abilityIndex));
        uint after = update.Experience;
        int beforeBase = raw[abilityIndex], afterBase = update.BaseAbility;
        if (afterBase != beforeBase && growth.Notifications is null) return Unknown();
        OfficerGrowthState nextGrowth = growth.Advance(abilityIndex, after, beforeBase, afterBase, reportContext);
        raw[abilityIndex] = afterBase;
        OfficerState updated = person with {
            BaseAbilities = new(raw[0], raw[1], raw[2], raw[3], raw[4]),
            Growth = nextGrowth };
        GameState result = input with { Officers = new(input.Officers) { [personId] = updated } };
        var receipt = new PersonGrowthReceipt(personId, abilityIndex, requested, applied, experience, after,
            beforeBase, afterBase, trait, advantage, nextGrowth.Notifications,
            $"{ProfileId}:{parameters.Revision}:{input.NativeClock!.Revision}:{source.Sha256}");
        return new(PersonGrowthOutcome.Committed, result, receipt);
        PersonGrowthResult Unknown() => new(PersonGrowthOutcome.CurrentInputsUnresolved, input, null);
        PersonGrowthResult Skip() => new(PersonGrowthOutcome.NativeSkip, input, null);
    }
}
