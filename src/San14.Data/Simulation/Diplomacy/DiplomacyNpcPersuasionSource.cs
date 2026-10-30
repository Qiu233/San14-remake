using San14.Core;

namespace San14.Data;

internal sealed record DiplomacyNpcPersuasionResult(GameState State, DiplomacyTrialResult Trial,
    int OwnFirstExpense, int TargetExpense, int OwnSecondExpense);

public sealed partial class ArmyCampaignRuleAdapter
{
    private static readonly IReadOnlyDictionary<string, int> DiplomacyNpcStaticParameters = new Dictionary<string, int>
    {
        ["diplomacy:npc-low-probability-threshold"] = 30,
        ["diplomacy:npc-goodwill-high-probability-threshold"] = 80,
        ["diplomacy:npc-alliance-high-probability-threshold"] = 90,
        ["diplomacy:npc-attack-high-probability-threshold"] = 90,
        ["diplomacy:npc-goodwill-budget-percent"] = 20,
        ["diplomacy:npc-alliance-budget-percent"] = 35,
        ["diplomacy:npc-attack-budget-percent"] = 35,
        ["diplomacy:npc-expense-share-middle-threshold"] = 10,
        ["diplomacy:npc-expense-share-high-threshold"] = 15,
        ["diplomacy:npc-expense-middle-percent"] = 10,
        ["diplomacy:npc-expense-high-percent"] = 15,
        ["diplomacy:npc-low-probability-relation-threshold"] = 30,
        ["diplomacy:npc-minimum-own-improvement"] = 5,
        ["diplomacy:npc-minimum-opponent-improvement"] = 5,
        ["diplomacy:npc-low-probability-minimum-improvement"] = 10,
        ["diplomacy:npc-politics-high-threshold"] = 100,
        ["diplomacy:npc-politics-high-scale"] = 150,
        ["diplomacy:npc-politics-middle-threshold"] = 90,
        ["diplomacy:npc-politics-middle-scale"] = 130,
        ["diplomacy:npc-politics-low-threshold"] = 80,
        ["diplomacy:npc-politics-low-scale"] = 110,
        ["diplomacy:npc-politics-base-scale"] = 100,
        ["diplomacy:npc-goodwill-cost-scale"] = 100,
        ["diplomacy:npc-goodwill-effect-scale"] = 10,
        ["diplomacy:npc-goodwill-rate-offset"] = 3,
        ["diplomacy:npc-goodwill-rate-divisor"] = 40,
        ["diplomacy:npc-alliance-cost-scale"] = 100,
        ["diplomacy:npc-alliance-effect-scale"] = 10,
        ["diplomacy:npc-alliance-rate-offset"] = 3,
        ["diplomacy:npc-alliance-rate-divisor"] = 40,
        ["diplomacy:npc-attack-cost-scale"] = 100,
        ["diplomacy:npc-attack-effect-scale"] = 10,
        ["diplomacy:npc-attack-rate-offset"] = 3,
        ["diplomacy:npc-attack-rate-divisor"] = 40,
        ["diplomacy:npc-politics-scale-divisor"] = 100,
        ["diplomacy:npc-probability-round-scale"] = 10000,
        //These independently qualified TC/SC scalars are loader-zero initialized with only
        //read references in the original program, with no address escape.
        //They are not values sampled from a running game. Saved overrides
        //still take precedence in NpcParameter.
        ["diplomacy:npc-goodwill-rate-floor"] = 0,
        ["diplomacy:npc-alliance-rate-floor"] = 0,
        ["diplomacy:npc-attack-rate-floor"] = 0,
        ["diplomacy:npc-return-captives-high-probability-threshold"]=90,
        ["diplomacy:npc-return-captives-budget-percent"]=35,
        ["diplomacy:npc-return-captives-cost-scale"]=100,
        ["diplomacy:npc-return-captives-effect-scale"]=10,
        ["diplomacy:npc-return-captives-rate-offset"]=3,
        ["diplomacy:npc-return-captives-rate-divisor"]=40,
        ["diplomacy:npc-return-captives-rate-floor"]=0,
        //The negotiation window constructors consume the same proved
        //loader-initialized, read-only scalar block for their minimum.
        ["diplomacy:negotiation-intelligence-minimum"] = 0,
    };

    //The saved bank has precedence, including explicit overrides. Only
    //independently proved TC/SC defaults may cover an absent key in an older save.
    private static int ReadDiplomacyStaticRuleCurrent(GameState state, string name)
    {
        string key = "diplomacy:" + name;
        var bank = state.NativeArmySimulation?.Parameters;
        if (bank is null || bank.Language is not ("TC" or "SC") || bank.RuleVersion != SimulationRuleIdentity.ForLocale(bank.Language))
            throw DiplomacyNumericMissing("NPC_parameter_bank");
        if (bank.Float32Values.ContainsKey(key) || bank.Int32Values.ContainsKey(key)) return DParameter(state, name);
        return DiplomacyNpcStaticParameters.TryGetValue(key, out int value) ? value : DParameter(state, name);
    }

    //Independently sourced TC/SC rules from file-backed constants and qualified loader-zero
    //scalars. No current process or original executable is read at runtime.
    internal GameState InstallFreshDiplomacyNpcParameters(GameState state)
    {
        if (Seed.Language is not ("TC" or "SC")) return state;
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } native || native.Parameters.Language != Seed.Language ||
            native.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
            throw DiplomacyNumericMissing("NPC_fresh_parameter_lifecycle");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var (key, value) in DiplomacyNpcStaticParameters)
        {
            if (values.ContainsKey(key) || native.Parameters.Float32Values.ContainsKey(key))
                throw DiplomacyNumericMissing("NPC_fresh_parameter_collision_" + key);
            values.Add(key, value);
        }
        return state with { NativeArmySimulation = native with
        { Parameters = native.Parameters with { Int32Values = values } } };
    }

    //807BD0: the three expenditure passes precede3254D0(param7=0).
    //They share the same current District stocks and never consume App32.
    internal DiplomacyNpcPersuasionResult ResolveDiplomacyNpcArrivalCurrent(GameState state,
        SourceWorldContext current, PersonnelDiplomacyTask task)
    {
        var query = new DiplomacyQuery(this, state, current, task);
        var sourceDistrict = ForcePrimaryDistrictSource.Resolve(current, query.Own);
        //The original prefix resolves the ruler's home even on a shortcut.
        _ = BaseLookup.Find(DNeed(current.ReadPersonFirstLocation(query.Ruler(query.Own)), "NPC_ruler_homeE2"), current);
        if (task.NativeKind == 4 && query.OtherOrdinal is >= 46 and <= 50)
            return new(state, new(true, 100, null, "NPC-special-force-goodwill"), 0, 0, 0);
        if (task.NativeKind == 9 && query.AttackBlockReason() != 0)
            return new(state, new(false, 0, null, "NPC-request-attack-blocked"), 0, 0, 0);
        if (query.Level(query.OwnOrdinal, query.OtherOrdinal) >= 5)
            return new(state, new(true, 100, null, "NPC-maximum-relation"), 0, 0, 0);
        int probability = query.Probability(task.InitialOffer);
        int ownFirst = 0, target = 0, ownSecond = 0;
        if (probability < 100)
        {
            ownFirst = query.NpcPersuasion(7, 0, ref probability);
            if (ownFirst > 0) state = SpendDiplomacyDistrictCurrent(state, current, sourceDistrict, ownFirst);
        }
        int beforeCounter = probability;
        if (beforeCounter > 0)
        {
            target = query.NpcPersuasion(9, 0, ref probability);
            if (target > 0) state = SpendDiplomacyDistrictCurrent(state, current,
                ForcePrimaryDistrictSource.Resolve(current, query.Other), target);
        }
        if (probability < 100 && probability < beforeCounter)
        {
            ownSecond = query.NpcPersuasion(11, ownFirst, ref probability);
            if (ownSecond > 0) state = SpendDiplomacyDistrictCurrent(state, current, sourceDistrict, ownSecond);
        }
        var trial = DrawDiplomacyFinalProbabilityCurrent(state, current, task, probability,
            () => ReadArmyDestructionSampler(state, current) ?? throw DiplomacyMissing("NPC_trial_sampler"));
        return new(state, trial, ownFirst, target, ownSecond);
    }

    //200690 ->231310/231350. District24 is the district treasury, not a City.
    private static GameState SpendDiplomacyDistrictCurrent(GameState state, SourceWorldContext current,
        DistrictReference district, int amount)
    {
        int id = DNeed(district.PhysicalSlot, "NPC_expense_District");
        int force = DNeed(current.ReadDistrictForceId(id), "NPC_expense_Force10");
        if (force >= 52) force = 0;
        int rank = DNeed(current.ReadMoraleWaveForceRank47(force), "NPC_expense_Force_rank47");
        int cap = rank switch { 1 or 2 or 3 or 10 or 11 or 12 => 200000, 4 or 5 => 150000,
            6 or 7 => 100000, _ => 50000 };
        int value = Math.Clamp(unchecked(ReadDiplomacyDistrictBudget(current, id) - amount), 0, cap);
        for (int i = 0; i < 4; i++) current.SetDistrictRawByte(id, 0x24 + i, unchecked((byte)(value >> (i * 8))));
        var owner = state.NativeForceUnregisterCurrent ?? throw DiplomacyNumericMissing("NPC_saved_District_windows");
        return state with { NativeForceUnregisterCurrent = owner with
        {
            DistrictWindows10 = new(owner.DistrictWindows10)
            { [id] = Enumerable.Range(0x10, 28).Select(at => current.ReadDistrictRawByte(id, at)).ToArray() }
        }};
    }
    private static int ReadDiplomacyDistrictBudget(SourceWorldContext current, int physical)
    {
        uint value = 0;
        for (int i = 0; i < 4; i++) value |= (uint)DNeed(current.ReadDistrictRawByte(physical, 0x24 + i), "NPC_District24") << (i * 8);
        return unchecked((int)value);
    }

    private sealed partial class DiplomacyQuery
    {
        private string NpcKind => NativeKind switch { 4 => "goodwill", 5 => "alliance", 9 => "attack",8=>"return-captives",
            _ => throw DiplomacyNumericMissing("NPC_persuasion_kind") };
        private int NpcParameter(string suffix) => ReadDiplomacyStaticRuleCurrent(State, "npc-" + suffix);

        //806110. Even an expense of zero may publish a clamped probability;
        //the caller must retain that ref result without inventing a debit.
        internal int NpcPersuasion(int operation, int alreadySpent, ref int probability)
        {
            //1D3840 &&1D2A90!=-1. The ordinary current's actual -1 makes
            //this false regardless of the external task-scene pointer.
            if (State.CampaignEventWorld?.CurrentUserIndex != -1)
                throw DiplomacyNumericMissing("NPC_persuasion_TaskScene_current_user");
            var ownDistrict = ForcePrimaryDistrictSource.Resolve(Current, Own);
            var targetDistrict = ForcePrimaryDistrictSource.Resolve(Current, Other);
            int lane = probability <= NpcParameter("low-probability-threshold") ? 0 :
                probability < NpcParameter(NpcKind + "-high-probability-threshold") ? 1 : 2;
            if (operation == 11 && lane > 1) return 0;
            int district = DNeed((operation == 9 ? targetDistrict : ownDistrict).PhysicalSlot, "NPC_budget_District");
            int budget = ReadDiplomacyDistrictBudget(Current, district);
            if (budget <= 0) return 0;
            int allowance = unchecked(DDiv(unchecked(unchecked(budget + alreadySpent) *
                NpcParameter(NpcKind + "-budget-percent")), 100, "NPC_budget_percent") - alreadySpent);
            if (allowance <= 0) return 0;
            bool opponent = operation == 9;
            int required = NpcRequiredExpense(probability, opponent);
            int share = DDiv(unchecked(required * 100), budget, "NPC_expense_share");
            if (share >= NpcParameter("expense-share-middle-threshold"))
            {
                int percent = NpcParameter("expense-middle-percent");
                if (share >= NpcParameter("expense-share-high-threshold")) percent = NpcParameter("expense-high-percent");
                required = DDiv(unchecked(percent * budget), 100, "NPC_expense_percent");
            }
            int expense = Math.Min(allowance, required);
            if (expense <= 0) return 0;
            int after = Math.Clamp(NpcAdjustedProbability(probability, expense, opponent), 0, 100);
            int before = Math.Clamp(probability, 0, 100);
            int improvement = opponent ? unchecked(before - after) : unchecked(after - before);
            bool enough = improvement >= NpcParameter(opponent ? "minimum-opponent-improvement" : "minimum-own-improvement");
            if (improvement < NpcParameter("low-probability-minimum-improvement") && lane == 0 &&
                Relation(OwnOrdinal, OtherOrdinal) < NpcParameter("low-probability-relation-threshold")) expense = 0;
            probability = enough ? after : before;
            return enough ? expense : 0;
        }

        //322FA0 and3231E0 both use effective politics of the acting officer;
        //the counter-persuasion pass substitutes the recipient's adviser52.
        private int NpcPoliticsScale(bool opponent)
        {
            int person = Executor;
            if (opponent)
            {
                int id = DNeed(Current.ReadAssignmentForceAdvisor52(OtherOrdinal), "NPC_target_adviser52");
                person = P(Current.ReadPersonLookupReference(id <= 6000 ? id : 0)
                    ?? throw DiplomacyNumericMissing("NPC_adviser_Person"));
            }
            //22FE40 itself has no Person-nativevalid front gate. The shared
            //ability facade's invalid-Person literal0 belongs to Army callers;
            //do not borrow it for an invalid adviser or special raw receiver.
            if (!Valid(person)) throw DiplomacyNumericMissing("NPC_raw_invalid_Person22FE40_politics");
            int ability = DNeed(new TacticRemakeCurrentAbilities(Current, package._scenario,
                package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language)).ResolveDimension(State, Id(person), 3,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(State, force, effect)), "NPC_current_politics");
            int high = NpcParameter("politics-high-threshold");
            int scale = NpcParameter("politics-base-scale");
            if (ability >= high) return NpcParameter("politics-high-scale");
            if (ability >= NpcParameter("politics-middle-threshold")) return NpcParameter("politics-middle-scale");
            return ability >= NpcParameter("politics-low-threshold") ? NpcParameter("politics-low-scale") : scale;
        }
        private int NpcRequiredExpense(int probability, bool opponent)
        {
            int scale = NpcPoliticsScale(opponent);
            int cost = NpcParameter(NpcKind + "-cost-scale");
            int effect = NpcParameter(NpcKind + "-effect-scale");
            int floor = NpcParameter(NpcKind + "-rate-floor");
            int offset = NpcParameter(NpcKind + "-rate-offset");
            int divisor = NpcParameter(NpcKind + "-rate-divisor");
            int before = Math.Min(probability, 100);
            int distance = opponent ? unchecked(before - 1) : unchecked(100 - before);
            int numerator = unchecked(unchecked(unchecked(Math.Max(floor, before) + unchecked(divisor * offset)) * distance) * cost * 100);
            int denominator = unchecked(unchecked(divisor * effect) * scale);
            double value = (double)numerator / denominator;
            return opponent ? unchecked(NpcCvtt(Math.Floor(value)) + 1) : NpcCvtt(Math.Ceiling(value));
        }
        private int NpcAdjustedProbability(int probability, int expense, bool opponent)
        {
            int scale = NpcPoliticsScale(opponent);
            int before = Math.Min(probability, 100);
            double politics = (double)scale * expense / NpcParameter("politics-scale-divisor");
            int cost = NpcParameter(NpcKind + "-cost-scale");
            int effect = NpcParameter(NpcKind + "-effect-scale");
            int floor = NpcParameter(NpcKind + "-rate-floor");
            int offset = NpcParameter(NpcKind + "-rate-offset");
            int divisor = NpcParameter(NpcKind + "-rate-divisor");
            double delta = effect * politics / cost / ((double)Math.Max(floor, before) / divisor + offset);
            double result = (opponent ? before - delta : before + delta) * NpcParameter("probability-round-scale");
            return NpcCvtt(Math.Round(result, MidpointRounding.AwayFromZero)) / 10000;
        }
        private static int NpcCvtt(double value) => !double.IsFinite(value) || value < int.MinValue || value >= 2147483648d
            ? int.MinValue : (int)value;
    }
}
