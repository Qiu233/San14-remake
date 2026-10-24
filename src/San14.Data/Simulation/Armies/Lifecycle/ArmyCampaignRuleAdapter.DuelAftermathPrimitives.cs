using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The actual duel reward writer: qualification, Strength growth,
    /// then pending merit, against the caller's same operation-local current.
    /// The caller publishes the enclosing aftermath only after all callbacks succeed.</summary>
    public GameState ApplyDuelRewardDuringArmyOperation(GameState state, SourceWorldContext current,
        int personId, int outcome, int opponentPersonId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(current);
        int person = DuelAftermathPerson(current, personId);
        if (!DuelAftermathNeed(current.ReadPersonNativeValidity(person), "reward_Person_validity")) return state;
        int opponent = DuelAftermathPerson(current, opponentPersonId);
        if (!DuelAftermathNeed(current.ReadPersonNativeValidity(opponent), "reward_opponent_validity")) return state;
        //232A50 is tested before233240. Its Force lookup is conditional on E2.
        if (DuelAftermathSpecialForce(current, person)) return state;
        if (DuelAftermathSpecialPerson(current, person)) return state;
        int id = DuelAftermathNeed(current.ReadPersonIdentity(person), "reward_Person_identity");
        int xp, merit = 0;
        if (outcome == 0)
        {
            int first = DuelAftermathStrength(state, current, person);
            int second = DuelAftermathStrength(state, current, opponent);
            int difference = unchecked(first - second);
            int sign = difference >> 31;
            int absolute = unchecked((difference ^ sign) - sign);
            //Saved overrides win over the proved immutable TC loader defaults.
            //Even absolute0 still reads the signed cap in the original order.
            int bonus = unchecked(absolute * DuelAftermathPrimitiveParameters.Read(state, "winner-difference-growth-multiplier"));
            int cap = DuelAftermathPrimitiveParameters.Read(state, "winner-difference-growth-cap");
            bonus = Math.Min(bonus, cap);
            xp = unchecked(DuelAftermathPrimitiveParameters.Read(state, "winner-strength-growth") + bonus);
            first = DuelAftermathStrength(state, current, person);
            second = DuelAftermathStrength(state, current, opponent);
            int deficit = Math.Max(0, unchecked(second - first));
            merit = unchecked(unchecked(unchecked(deficit + 100) *
                DuelAftermathPrimitiveParameters.Read(state, "winner-pending-merit")) * 10) / 100;
        }
        else if (outcome == 1)
        {
            xp = DuelAftermathPrimitiveParameters.Read(state, "loser-strength-growth");
        }
        else
        {
            xp = DuelAftermathPrimitiveParameters.Read(state, "draw-strength-growth");
            merit = unchecked(DuelAftermathPrimitiveParameters.Read(state, "draw-pending-merit") * 10);
        }
        var growth = _growthSource.TryApplyWithinArmyOperation(state, current, _growthQueries, id, 1, xp, 0);
        if (growth.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved)
            throw DuelAftermathPrimitiveParameters.Missing("reward_strength_growth_owner");
        var pending = _pendingMerit.TryApplyWithinArmyOperation(growth.State, current, id, merit);
        if (pending.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved)
            throw DuelAftermathPrimitiveParameters.Missing("reward_pending_merit_owner");
        return pending.State;
    }

    /// <summary>Direct2E7730 readiness writer. The valid-person/F1&lt;100 filter
    /// belongs to its registry-loop caller and is deliberately not added here.</summary>
    public GameState RestoreDuelReadinessDuringArmyOperation(GameState state, SourceWorldContext current, int personId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(current);
        int person = DuelAftermathPerson(current, personId);
        int id = DuelAftermathNeed(current.ReadPersonIdentity(person), "readiness_Person_identity");
        var officer = state.Officers.GetValueOrDefault(id)
            ?? throw DuelAftermathPrimitiveParameters.Missing("readiness_Person_owner");
        //Health is owned by the passed draft. A preceding injury callback can
        //have replaced it since this operation captured current's lookups.
        byte health = DuelAftermathNeed(OfficerHealthSourceAdapter.Encode(officer.Health), "readiness_health");
        if (health > 3) throw DuelAftermathPrimitiveParameters.Missing("readiness_health_cap_index");
        int cap = DuelAftermathPrimitiveParameters.Read(state, "readiness-health-cap:" + health);
        int strength = DuelAftermathStrength(state, current, person);
        int divisor = DuelAftermathPrimitiveParameters.Read(state, "readiness-strength-divisor");
        if (divisor == 0) throw DuelAftermathPrimitiveParameters.Missing("readiness_strength_divisor_zero");
        var condition = officer.DuelCondition ?? throw DuelAftermathPrimitiveParameters.Missing("readiness_condition_owner");
        byte before = DuelAftermathNeed(condition.Readiness, "readiness_F1");
        int step = Math.Clamp(strength / divisor, 5, 30);
        byte after = (byte)Math.Clamp(Math.Min(cap, unchecked(before + step)), 0, 100);
        if (after == before) return state;
        return state with { Officers = new(state.Officers)
            { [id] = officer with { DuelCondition = condition with { Readiness = after } } } };
    }

    private int DuelAftermathStrength(GameState state, SourceWorldContext current, int physical)
    {
        if (!DuelAftermathNeed(current.ReadPersonNativeValidity(physical), "ability_Person_validity")) return 0;
        int id = DuelAftermathNeed(current.ReadPersonIdentity(physical), "ability_Person_identity");
        if (current.ResolveOfficerSource(id) != physical) throw DuelAftermathPrimitiveParameters.Missing("ability_Person_binding");
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        //Every callback consumes this invocation's latest immutable draft,
        //including any preceding reward's changed base ability and health.
        return DuelAftermathNeed(abilities.ResolveDimension(state, id, 1,
            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect)), "effective_strength");
    }

    private static bool DuelAftermathSpecialForce(SourceWorldContext current, int physical)
    {
        ushort first = DuelAftermathNeed(current.ReadPersonFirstLocation(physical), "reward_232A50_E2");
        return first is >= 1 and <= 61 && DuelAftermathForce(current, physical) is >= 46 and <= 50;
    }

    private static bool DuelAftermathSpecialPerson(SourceWorldContext current, int physical)
    {
        int id = DuelAftermathNeed(current.ReadPersonIdentity(physical), "reward_233240_identity");
        if (!BattleRemakeCurrentRegionFactor.IsNativeBandPerson(id)) return false;
        ushort first = DuelAftermathNeed(current.ReadPersonFirstLocation(physical), "reward_233240_E2");
        return first is >= 1 and <= 61 && DuelAftermathForce(current, physical) is not (>= 46 and <= 50);
    }

    private static int DuelAftermathForce(SourceWorldContext current, int physical)
    {
        var force = current.ReadPersonForce(physical) ?? throw DuelAftermathPrimitiveParameters.Missing("reward_Force_pointer");
        return DuelAftermathNeed(current.ReadForcePhysicalSlot(force), "reward_Force_ordinal");
    }

    private static int DuelAftermathPerson(SourceWorldContext current, int id)
    {
        var pointer = (id is >= 0 and <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
            ?? throw DuelAftermathPrimitiveParameters.Missing("Person_pointer");
        return pointer.PhysicalSlot ?? throw DuelAftermathPrimitiveParameters.Missing("Person_receiver");
    }

    private static T DuelAftermathNeed<T>(T? value, string field) where T : struct =>
        value ?? throw DuelAftermathPrimitiveParameters.Missing(field);
}
