using System.Text.Json;
using San14.Core;

namespace San14.Data;

public enum OriginalPersonGrowthOutcome { Committed, NativeSkip, CurrentInputsUnresolved }
public sealed record OriginalPersonGrowthReceipt(int OfficerId, int AbilityIndex, int RequestedGrowth, int AppliedGrowth,
    uint BeforeExperience13C, uint AfterExperience13C, byte BeforeBase124, byte AfterBase124,
    int Trait75, OriginalGrowthAdvantageReceipt Advantage7, NativePersonAbilityGrowthNotice[]? Notices, string Revision);
public sealed record OriginalPersonGrowthResult(OriginalPersonGrowthOutcome Outcome, GameState State,
    OriginalPersonGrowthReceipt? Receipt);

public sealed partial class OriginalPersonGrowthSource
{
    /// <summary>Actual2B9FA0 atomic XP/base/258B40 vector writer. It is an
    /// independent callback; normal2B9110 pending-merit remains unowned.</summary>
    public OriginalPersonGrowthResult TryApply(GameState input, OriginalEventInitializationContext current,
        OriginalPersonGrowthCurrentQueries queries, int personId, int abilityIndex, int requested, int reportContext = 0) =>
        Apply(input, current, queries, personId, abilityIndex, requested, reportContext, true);

    //Source callbacks can run while earlier Army requests still own zero
    //receivers. Whole-world validation belongs to the block's publication.
    internal OriginalPersonGrowthResult TryApplyWithinArmyOperation(GameState input, OriginalEventInitializationContext current,
        OriginalPersonGrowthCurrentQueries queries, int personId, int abilityIndex, int requested, int reportContext) =>
        Apply(input, current, queries, personId, abilityIndex, requested, reportContext, false);

    private OriginalPersonGrowthResult Apply(GameState input, OriginalEventInitializationContext current,
        OriginalPersonGrowthCurrentQueries queries, int personId, int abilityIndex, int requested, int reportContext, bool validateWorld)
    {
        if (!input.Officers.TryGetValue(personId, out OfficerState? person) || abilityIndex is < 0 or > 4 ||
            person.NativeAbilityRuntime is not { } owner || person.NativeGrowthRuntime is not { } growth ||
            input.NativeArmySimulation?.Parameters is not { } parameters || owner.Language != parameters.Language ||
            owner.RuleVersion != parameters.RuleVersion || owner.Revision != OriginalTacticRemakeCurrentAbilities.ProfileId ||
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
            OriginalTacticRemakeCurrentTraits.CommanderEffectSum(current, owner.PhysicalSlot, 75) is not int trait ||
            queries.ReadAdvantage7(input, current, force) is not { } advantage ||
            queries.ReadGlobal(input, 0) is not int traitScale || queries.ReadGlobal(input, 1) is not int advantageScale ||
            OriginalBattleRemakeCurrentSettings.ReadSelectorE58(input) is not int selector)
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
            if (OriginalBattleRemakeCurrentForcePolicies.ReadForceEffect(input, savedForce, 40) is not int value ||
                queries.ReadGlobal(input, 3) is not int scale40) return Unknown();
            policy = value; policyScale = scale40;
        }
        uint bits = unchecked((uint)requested);
        uint bonus = Math.Max(trait != 0 ? 1u : 0u, Percent(unchecked(traitScale * trait), bits));
        bonus = unchecked(bonus + Percent(unchecked(advantageScale * advantage.Value), bits));
        uint primary = forceValid ? Percent(unchecked(policyScale * policy + 100), bits) : bits;
        int applied = unchecked((int)(bonus + primary));
        if (selector == 0)
        {
            if (OriginalBattleRemakeCurrentSettings.ReadAdvancedParameter(input, 24) is not int percent) return Unknown();
            applied = unchecked(applied * percent) / 100;
        }
        uint after = unchecked(xp[abilityIndex] + (uint)applied);
        byte beforeBase = (byte)raw[abilityIndex], afterBase = beforeBase;
        while (afterBase < 100 && ReadColumn(afterBase + 1, abilityIndex) <= after) afterBase++;
        NativePersonAbilityGrowthNotice[]? notices = growth.AbilityGrowthNotices?.ToArray();
        if (afterBase != beforeBase)
        {
            if (notices is null || notices.Length > 5 || notices.Any(n => n is null || n.AbilityIndex is < 0 or > 4) ||
                notices.Select(n => n.AbilityIndex).Distinct().Count() != notices.Length) return Unknown();
            int index = Array.FindIndex(notices, n => n.AbilityIndex == abilityIndex);
            if (index >= 0) notices[index] = notices[index] with { After4 = afterBase, ReportContext8 = reportContext };
            else notices = [..notices, new(abilityIndex, afterBase, beforeBase, reportContext, 0, 1)];
        }
        GameState detached = JsonSerializer.Deserialize<GameState>(JsonSerializer.SerializeToUtf8Bytes(input))
            ?? throw new InvalidDataException("Growth state could not be detached.");
        uint[] nextXp = detached.Officers[personId].NativeGrowthRuntime!.Experience13C!.ToArray(); nextXp[abilityIndex] = after;
        raw[abilityIndex] = afterBase;
        OfficerState updated = detached.Officers[personId] with {
            BaseAbilities = new(raw[0], raw[1], raw[2], raw[3], raw[4]),
            NativeGrowthRuntime = detached.Officers[personId].NativeGrowthRuntime! with {
                Experience13C = nextXp, AbilityGrowthNotices = notices } };
        GameState result = detached with { Officers = new(detached.Officers) { [personId] = updated } };
        if (validateWorld) StateValidator.Validate(result);
        var receipt = new OriginalPersonGrowthReceipt(personId, abilityIndex, requested, applied, xp[abilityIndex], after,
            beforeBase, afterBase, trait, advantage, notices?.ToArray(),
            $"{ProfileId}:{parameters.Revision}:{input.NativeClock!.Revision}:{source.Sha256}");
        return new(OriginalPersonGrowthOutcome.Committed, result, receipt);
        OriginalPersonGrowthResult Unknown() => new(OriginalPersonGrowthOutcome.CurrentInputsUnresolved, input, null);
        OriginalPersonGrowthResult Skip() => new(OriginalPersonGrowthOutcome.NativeSkip, input, null);
        static uint Percent(int coefficient, uint quantity) => unchecked((uint)coefficient * quantity) / 100;
    }
}
