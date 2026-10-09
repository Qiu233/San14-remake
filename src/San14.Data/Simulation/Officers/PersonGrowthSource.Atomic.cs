using San14.Core;

namespace San14.Data;

public enum PersonGrowthOutcome { Committed, NativeSkip, CurrentInputsUnresolved }
public sealed record PersonGrowthReceipt(int OfficerId, int AbilityIndex, int RequestedGrowth, int AppliedGrowth,
    uint BeforeExperience13C, uint AfterExperience13C, byte BeforeBase124, byte AfterBase124,
    int Trait75, GrowthAdvantageReceipt Advantage7, NativePersonAbilityGrowthNotice[]? Notices, string Revision);
public sealed record PersonGrowthResult(PersonGrowthOutcome Outcome, GameState State,
    PersonGrowthReceipt? Receipt);

public sealed partial class PersonGrowthSource
{
    /// <summary>Standalone atomic XP/base/growth-notice update. A committed
    /// result owns a detached world. NativeSkip and unknown return the input;
    /// the compound normal reward owns its later pending-merit callback.</summary>
    public PersonGrowthResult TryApply(GameState input, SourceWorldContext current,
        PersonGrowthCurrentQueries queries, int personId, int abilityIndex, int requested, int reportContext = 0) =>
        Apply(input, current, queries, personId, abilityIndex, requested, reportContext, true);

    //The enclosing Army operation owns the candidate. Resolve every reached
    //query before replacing this officer; unknown never publishes a prefix.
    internal PersonGrowthResult TryApplyWithinArmyOperation(GameState input, SourceWorldContext current,
        PersonGrowthCurrentQueries queries, int personId, int abilityIndex, int requested, int reportContext) =>
        Apply(input, current, queries, personId, abilityIndex, requested, reportContext, false);

    private PersonGrowthResult Apply(GameState input, SourceWorldContext current,
        PersonGrowthCurrentQueries queries, int personId, int abilityIndex, int requested, int reportContext, bool detachStandalone)
    {
        if (!input.Officers.TryGetValue(personId, out OfficerState? person) || abilityIndex is < 0 or > 4 ||
            person.NativeAbilityRuntime is not { } owner || person.NativeGrowthRuntime is not { } growth ||
            input.NativeArmySimulation?.Parameters is not { } parameters || owner.Language != parameters.Language ||
            owner.RuleVersion != parameters.RuleVersion || owner.Revision != TacticRemakeCurrentAbilities.ProfileId ||
            current.StaticSource is not { } source || owner.SourceScenarioUri != source.ResourceUri ||
            owner.SourceScenarioSha256 != source.Sha256 || input.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256 ||
            growth.Revision != ProfileId || growth.SourceExperienceUri != _source.ResourceUri ||
            growth.SourceExperienceSha256 != _source.Sha256 || input.ContentHashes.GetValueOrDefault(_source.ResourceUri) != _source.Sha256 ||
            current.ReadPersonLookupReference(personId)?.PhysicalSlot != owner.PhysicalSlot ||
            current.ReadPersonIdentity(owner.PhysicalSlot) != personId || current.ReadPersonNativeValidity(owner.PhysicalSlot) is not bool valid)
            return Unknown();
        if (!valid) return Skip();
        var force = current.ReadPersonForce(owner.PhysicalSlot);
        if (force?.PhysicalSlot is not int forceId) return Unknown();
        if (forceId is >= 46 and <= 51) return Skip();
        if (person.BaseAbilities is not { } basic) return Unknown();
        int[] raw = [basic.Leadership, basic.Strength, basic.Intelligence, basic.Politics, basic.Charm];
        if (raw[abilityIndex] == 100) return Skip();
        //The original tail can loop indefinitely for abnormal >100 bases.
        //The source-created normalized profile stays within100; reject an
        //unsupported current value rather than clamp or fabricate termination.
        if (raw[abilityIndex] is < 0 or > 100 || growth.Experience13C is not { Length: 5 } xp ||
            current.ReadForceNativeValidity(force) is not bool forceValid ||
            TacticRemakeCurrentTraits.CommanderEffectSum(current, owner.PhysicalSlot, 75) is not int trait ||
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
        int applied = PersonRewardArithmetic.CalculateGrowth(requested, trait, traitScale,
            advantage.Value, advantageScale, forceValid, policy, policyScale);
        if (selector == 0)
        {
            if (BattleRemakeCurrentSettings.ReadAdvancedParameter(input, 24) is not int percent) return Unknown();
            applied = unchecked(applied * percent) / 100;
        }
        var update = PersonRewardArithmetic.AdvanceAbility(xp[abilityIndex], (byte)raw[abilityIndex],
            applied, nextBase => ReadColumn(nextBase, abilityIndex));
        uint after = update.Experience;
        byte beforeBase = (byte)raw[abilityIndex], afterBase = update.BaseAbility;
        NativePersonAbilityGrowthNotice[]? notices = growth.AbilityGrowthNotices?.ToArray();
        if (afterBase != beforeBase)
        {
            if (notices is null || notices.Length > 5 || notices.Any(n => n is null || n.AbilityIndex is < 0 or > 4) ||
                notices.Select(n => n.AbilityIndex).Distinct().Count() != notices.Length) return Unknown();
            int index = Array.FindIndex(notices, n => n.AbilityIndex == abilityIndex);
            if (index >= 0) notices[index] = notices[index] with { After4 = afterBase, ReportContext8 = reportContext };
            else notices = [..notices, new(abilityIndex, afterBase, beforeBase, reportContext, 0, 1)];
        }
        GameState candidate = detachStandalone ? GameStateCopy.Copy(input) : input;
        uint[] nextXp = xp.ToArray(); nextXp[abilityIndex] = after;
        raw[abilityIndex] = afterBase;
        OfficerState updated = candidate.Officers[personId] with {
            BaseAbilities = new(raw[0], raw[1], raw[2], raw[3], raw[4]),
            NativeGrowthRuntime = candidate.Officers[personId].NativeGrowthRuntime! with {
                Experience13C = nextXp, AbilityGrowthNotices = notices } };
        GameState result = candidate with { Officers = new(candidate.Officers) { [personId] = updated } };
        var receipt = new PersonGrowthReceipt(personId, abilityIndex, requested, applied, xp[abilityIndex], after,
            beforeBase, afterBase, trait, advantage, notices?.ToArray(),
            $"{ProfileId}:{parameters.Revision}:{input.NativeClock!.Revision}:{source.Sha256}");
        return new(PersonGrowthOutcome.Committed, result, receipt);
        PersonGrowthResult Unknown() => new(PersonGrowthOutcome.CurrentInputsUnresolved, input, null);
        PersonGrowthResult Skip() => new(PersonGrowthOutcome.NativeSkip, input, null);
    }
}
