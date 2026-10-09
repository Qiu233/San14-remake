using San14.Core;

namespace San14.Data;

public sealed record ArmyMovementEligibilityCurrentFields(int UnitId, int NativePoolSlot,
    NativeArmyProgressStageMoment Moment, byte? TargetType38, ushort? TargetValue3A,
    byte? Flags150, ushort[]? TargetWords2CTo34, string SourceProvenance);
/// <summary>Actual current raw target fields, never MilitaryPlan or display
/// destinations. Missing independent fields remain null.</summary>
public interface IArmyMovementEligibilityCurrentOwnerQueries
{
    ArmyMovementEligibilityCurrentFields? ReadTargetFields(GameState state, UnitState army,
        NativeArmyProgressStageMoment stage7Moment);
    ushort? ReadObjectWord14(GameState state, ObjectReference obj,
        NativeArmyProgressStageMoment stage7Moment) => null;
    ushort? ReadParentArmyHex2A(GameState state, UnitState army,
        NativeArmyProgressStageMoment stage7Moment) => null;
}
public sealed record ArmyMovementEligibilityCurrentReceipt(bool Eligible, int? CurrentTarget2B54A0,
    int? GoalHex2B5610, string SourceBranch, string SourceProvenance);
public sealed record ArmyMovementEligibilityCurrentResolution(ArmyMovementEligibilityCurrentReceipt? Receipt,
    string? UnresolvedOwner)
{
    public bool? Eligible => Receipt?.Eligible;
}

/// <summary>Ordinary current2C7350 stage7 selector. Each false is a source
/// exclusion; missing scalar/target owners are unknown. No march initialization
/// or construction/route fallback is performed.</summary>
public sealed class ArmyMovementEligibilityCurrentProducer
{
    public const string Revision = "san14:ce-current-stage7-movement-eligibility-20261003";
    private readonly ArmyCampaignRuleAdapter package;
    private readonly LoadedMap map;
    private readonly LoadedTerrainTypes terrain;
    private readonly TacticSourceCatalog catalog;
    private readonly IArmyMovementEligibilityCurrentOwnerQueries? owners;
    public ArmyMovementEligibilityCurrentProducer(GameResourceRepository repository,
        ArmyCampaignRuleAdapter ownedPackage, LoadedMap ownedMap,
        IArmyMovementEligibilityCurrentOwnerQueries? currentOwners = null)
    {
        package = ownedPackage; map = ownedMap; owners = currentOwners;
        var skills = repository.LoadSkillDefinitions($"0010_{package.Seed.Language}/fixdataexce.s14");
        terrain = repository.LoadTerrainTypes($"0010_{package.Seed.Language}/fixdataexce.s14");
        catalog = TacticSourceParameters.Read(skills.Formations, skills.Source);
    }

    public ArmyMovementEligibilityCurrentResolution Resolve(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment)
    {
        ValidateMoment(state, moment);
        var definitions = package.CaptureCurrentDefinitions(state);
        if (definitions is null) return Missing("current.Base_Hex_Person_definition_capture");
        var context = new TacticRemakeCurrentContext(definitions, catalog, map, terrain,
            new(package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language)), Revision);
        bool? valid = context.ArmyValid(state, army);
        if (valid is null) return Missing("current.Army_validity");
        if (!valid.Value) return Result(false, null, null, "2C7377.invalid-Army");
        var runtime = army.NativeTacticRuntime!;
        ArmyMovementEligibilityCurrentFields? fields = null;
        bool queried = false;
        if (runtime.Status10 == 2)
        {
            //Both possible150-bit3 outcomes exclude when logistics26 is clear.
            if ((runtime.Flags26 & 2) == 0) return Result(false, null, null, "2C7399.Status2-logistics-clear");
            if (Fields()?.Flags150 is not byte flags) return Missing("current.Army_flags150_for_Status2");
            if ((flags & 8) != 0) return Result(false, null, null, "2C7393.Status2-flags150-bit3");
        }
        if (runtime.Abnormal24 > 0) return Result(false, null, null, "2C739F.abnormal24");
        if (army.NativeMarch is not { } march) return Missing("current.NativeMarch_word2C_and_override3C");
        if (march.CurrentHex2A != army.HexId || march.CurrentHex2A > 48399)
            return Missing("current.Army_Hex2A");
        var overrideFields = ArmyOverrideTargetSourceAdapter.Read(state, army);
        if (army.OverrideTarget?.Kind is not ArmyOverrideTargetKind overrideKind) return Missing("current.Army_override_category");
        int? currentTarget = ResolveCurrentTarget();
        if (currentTarget is null) return Missing("current.target2B54A0_raw38_3A_3E_or_selected_owner");
        if (currentTarget == 48400) return Result(false, currentTarget, null, "2C73AE.no-current-target");
        bool? excluded = context.ArmyManagerExcluded(state, army.Id);
        if (excluded is null) return Missing("current.Army_manager233560");
        if (excluded.Value) return Result(false, currentTarget, null, "2C73C6.manager-excluded");
        //2C96E0 checks current affiliation Base, not retained SourceLocationId.
        if (army.ForceId is >= 46 and <= 50) return Missing("current.special_force_Army232860");
        if (army.ForceId != 51)
        {
            int? home = new ArmyLogisticsPreparation(context, definitions, map, terrain).ReadCommanderBaseHex(state, army);
            if (home is null) return Missing("current.commander_Base2A7840_and_Object_Hex22D880");
            if (home == army.HexId) return Result(false, currentTarget, null, "2C73BA.current-home-Hex");
        }
        if (Fields()?.TargetType38 is not byte targetType || fields?.TargetValue3A is not ushort targetValue)
            return Missing("current.Army_targetType38_and_word3A_for_goal2B5610");
        int? goal = ResolveRawGoal(targetType, targetValue);
        if (goal is null) return Missing("current.goal2B5610_selected_Base_Object_Army_owner");
        if (goal == 48400) return Result(false, currentTarget, goal, "2C73E3.no-goal");
        if (army.NativeArmyConfiguration?.Action37 is not byte action) return Missing("current.Army_action37");
        bool direct = overrideKind == ArmyOverrideTargetKind.None;
        if (direct)
        {
            if (action is 1 or 13 or 15)
                return Result(march.CurrentHex2A != goal || march.TargetHex2C <= 48399, currentTarget, goal, "2C746C.direct-action1-13-15");
            if (action is 2 or 3)
            {
                if (march.CurrentHex2A == goal) return FinalAtGoal();
                if (targetType != 5) return Result(true, currentTarget, goal, "2C76B3.action2-3-other-target");
                bool? adjacent = RingContains(goal.Value, march.CurrentHex2A, 1);
                return adjacent is null ? Missing("current.fixed6_ring_boundary_stack_values") :
                    Result(!adjacent.Value, currentTarget, goal, "2C76D0.action2-3-city-ring1");
            }
            if (action == 14)
                return Result(targetType == 9 && (march.CurrentHex2A != goal || march.TargetHex2C <= 48399),
                    currentTarget, goal, "2C7548.action14-target9");
            if (action is 8 or 9 or 10 or 16)
            {
                bool distanceOne = SourceDistance(goal.Value, march.CurrentHex2A) == 1;
                if (!distanceOne) return Result(true, currentTarget, goal, "2C75D0.action8-10-16-distance-not1");
                if (march.TargetHex2C <= 48399) return Result(true, currentTarget, goal, "2C75BA.first-current-word2C");
                if (fields!.TargetWords2CTo34 is not { Length: 5 } words)
                    return Missing("current.Army_all_five_target_words2C_to34");
                if (words[0] != march.TargetHex2C) throw new InvalidDataException("Captured target words belong to another current word2C.");
                return Result(words.Any(word => word <= 48399) || march.CurrentHex2A == goal,
                    currentTarget, goal, "2C75A4.action8-10-16-five-current-targets");
            }
            if (action is 4 or 5 or 6 or 7)
            {
                if (march.TargetHex2C <= 48399) return Result(true, currentTarget, goal, "2C74B4-2C75EB.current-word2C");
                bool? extended = ExtendedFormation();
                if (extended is null) return Missing("current.Formation_A8_A6_and_Force193_for2C70A0");
                int radius = action == 4 ? extended.Value ? 3 : 2 : extended.Value ? 2 : 1;
                bool? contains = RingContains(goal.Value, march.CurrentHex2A, radius);
                if (contains is null) return Missing("current.fixed6_18_36_ring_boundary_stack_values");
                if (!contains.Value) return Result(true, currentTarget, goal, "2C74F3-2C769F.outside-target-ring");
                if (!extended.Value) return Result(false, currentTarget, goal, "2C7675.inside-normal-target-ring");
                var hex = definitions.ReadHexTableReference(goal.Value <= 48399 ? goal.Value : 0);
                if (hex?.PhysicalSlot is null || definitions.ReadHexObjectLink(hex) is not ushort link)
                    return Missing("current.goal_Hex_Object_link_for2C7627");
                var obj = definitions.ReadObjectTableReference(link <= 3000 ? link : 0);
                if (obj is null || definitions.ReadObjectNativeValidity(obj) is not bool objectValid)
                    return Missing("current.goal_Object_validity_for2C7661");
                if (!objectValid) return Result(false, currentTarget, goal, "2C7668.invalid-target-Object");
                if (owners?.ReadObjectWord14(state, obj, moment) is not ushort objectWord)
                    return Missing("current.goal_Object_word14");
                return Result(objectWord <= 1, currentTarget, goal, "2C766F.target-Object-word14");
            }
            return FinalAtGoal();
        }
        //At-goal fallback actions11/12 always return true, whether the preceding
        //override ring matches or not. Its unused radius value cannot change it.
        if (action is 11 or 12) return Result(true, currentTarget, goal, "2C779B.override-action11-12");
        bool? overrideExtended = ExtendedFormation();
        if (overrideExtended is null) return Missing("current.Formation_A8_A6_and_Force193_for2C70A0");
        if (overrideFields.Identity3E is not ushort overrideValue) return Missing("current.Army_override_word3E");
        int? overrideGoal = overrideKind == ArmyOverrideTargetKind.Unresolved ? 48400 :
            overrideFields.Kind3C is byte encodedKind ? ResolveRawGoal(encodedKind, overrideValue) : null;
        if (overrideGoal is null) return Missing("current.override_goal2B5900_selected_owner");
        int overrideRadius = overrideKind == ArmyOverrideTargetKind.City && overrideExtended.Value ? 3 :
            !overrideExtended.Value && overrideKind != ArmyOverrideTargetKind.City ? 1 : 2;
        bool? nearOverride = RingContains(overrideGoal.Value, march.CurrentHex2A, overrideRadius);
        if (nearOverride is null) return Missing("current.override_fixed_ring_boundary_stack_values");
        return Result(!nearOverride.Value, currentTarget, goal, "2C76E4.override-target-ring");

        ArmyMovementEligibilityCurrentResolution FinalAtGoal()
        {
            if (action is 11 or 12) return Result(true, currentTarget, goal, "2C779B.action11-12");
            if (action != 17 || !direct) return Result(false, currentTarget, goal, "2C7675.at-goal-other-action");
            if (owners?.ReadParentArmyHex2A(state, army, moment) is not ushort parent)
                return Missing("current.parent_Army229EE0_Hex2A");
            return Result(SourceDistance(march.CurrentHex2A, parent) > 3, currentTarget, goal, "2C77C8.action17-parent-distance");
        }
        bool? ExtendedFormation()
        {
            if (context.EffectiveFormation(state, army) is not int formation || formation is < 0 or > 20 ||
                TacticRemakeCurrentFormations.ReadParameter(state, formation, 0xA6) is not byte bits)
                return null;
            if (formation == 10)
            {
                if (state.NativeArmySimulation!.Forces.GetValueOrDefault(army.ForceId)?.Flags193 is not byte forceFlags) return null;
                if (forceFlags >= 0x80) return true;
            }
            return (bits & 1) != 0;
        }
        ArmyMovementEligibilityCurrentFields? Fields()
        {
            if (queried) return fields;
            queried = true; fields = owners?.ReadTargetFields(state, army, moment);
            if (fields is not null && (fields.UnitId != army.Id || fields.NativePoolSlot != army.NativeArmyOrigin!.NativeSlotIndex ||
                fields.Moment != moment || string.IsNullOrWhiteSpace(fields.SourceProvenance) ||
                fields.TargetWords2CTo34 is { Length: not 5 }))
                throw new InvalidDataException("Eligibility raw target fields belong to another current actor/stage or have invalid shape.");
            return fields;
        }
        int? ResolveRawGoal(byte type, ushort value)
        {
            if (type == 9) return value;
            if (type == 27)
            {
                UnitState[] selected = state.Units.Values.Where(u => u.NativeArmyOrigin?.NativeSlotIndex == (value <= 500 ? value : 0)).ToArray();
                return selected.Length == 1 && context.ArmyValid(state, selected[0]) is true ? selected[0].HexId : null;
            }
            if (type == 11)
            {
                var obj = definitions.ReadObjectTableReference(value <= 3000 ? value : 0);
                return obj?.PhysicalSlot is null ? null : definitions.ReadObjectHexIndex(obj);
            }
            BaseReference? selectedBase = type == 5 ? definitions.ReadCityTableReference(value <= 51 ? value : 0) :
                type == 6 ? definitions.ReadGateTableReference(value <= 10 ? value : 0) : null;
            if (type is not (5 or 6)) return 48400;
            if (selectedBase?.PhysicalSlot is null || definitions.ReadBaseObjectLink(selectedBase) is not ushort link) return null;
            var linked = definitions.ReadObjectTableReference(link <= 3000 ? link : 0);
            return linked?.PhysicalSlot is null ? null : definitions.ReadObjectHexIndex(linked);
        }
        int? ResolveCurrentTarget()
        {
            if (overrideKind == ArmyOverrideTargetKind.None && march.TargetHex2C != 48400) return march.TargetHex2C;
            byte type; ushort value;
            if (overrideKind == ArmyOverrideTargetKind.None)
            {
                if (Fields()?.TargetType38 is not byte t || fields?.TargetValue3A is not ushort v) return null;
                type = t; value = v;
            }
            else
            {
                if (overrideKind == ArmyOverrideTargetKind.Unresolved) return march.TargetHex2C;
                if (overrideFields.Kind3C is not byte encodedKind) return null;
                type = encodedKind;
                if (overrideFields.Identity3E is not ushort v) return null;
                value = v;
            }
            if (type == 9) return value;
            if (type is 5 or 6)
            {
                var selected = type == 5 ? definitions.ReadCityTableReference(value <= 51 ? value : 0) :
                    definitions.ReadGateTableReference(value <= 10 ? value : 0);
                if (selected is null || definitions.ReadBaseNativeValidity(selected) is not bool baseValid) return null;
                if (!baseValid) return march.TargetHex2C;
                if (definitions.ReadBaseObjectLink(selected) is not ushort link) return null;
                var obj = definitions.ReadObjectTableReference(link <= 3000 ? link : 0);
                if (obj is null || definitions.ReadObjectNativeValidity(obj) is not bool objectValid) return null;
                return objectValid ? definitions.ReadObjectHexIndex(obj) : march.TargetHex2C;
            }
            if (type == 11)
            {
                var obj = definitions.ReadObjectTableReference(value <= 3000 ? value : 0);
                if (obj is null || definitions.ReadObjectNativeValidity(obj) is not bool objectValid) return null;
                return objectValid ? definitions.ReadObjectHexIndex(obj) : march.TargetHex2C;
            }
            if (type == 27)
            {
                var selected = state.Units.Values.Where(u => u.NativeArmyOrigin?.NativeSlotIndex == (value <= 500 ? value : 0)).ToArray();
                if (selected.Length != 1 || context.ArmyValid(state, selected[0]) is not bool targetValid) return null;
                return targetValid ? selected[0].HexId : march.TargetHex2C;
            }
            return march.TargetHex2C;
        }
        ArmyMovementEligibilityCurrentResolution Result(bool eligible, int? target, int? goalHex, string branch) =>
            new(new(eligible, target, goalHex, branch, $"{Revision};{package.Seed.Source.RuleVersion};cycle={moment.CalendarCycleOrdinal};{branch}"), null);
    }

    private static bool? RingContains(int center, int current, int radius)
    {
        if (center is < 0 or > 48399) return null;
        int[] ring = TacticRemakeCurrentContext.NativeHexOrder(center, radius).Skip(1).ToArray();
        if (ring.Contains(current)) return true;
        //The original consumer tests a fixed6/18/36 stack array, even if its
        //producer returned fewer boundary cells. Unwritten slots remain unknown.
        return ring.Length == 3 * radius * (radius + 1) ? false : null;
    }
    /// <summary>Complete positive-word input branch of leaf TC2AAC90. Native
    /// quotient/remainder220 and doubled parity-Y are independent of map APIs.</summary>
    private static int SourceDistance(int first, int second)
    {
        int x1 = first % 220, y1 = first / 220, x2 = second % 220, y2 = second / 220;
        int dx = Math.Abs(x2 - x1), dy = Math.Abs(2 * (y2 - y1) - (x1 % 2 == 0 ? 1 : 0) + (x2 % 2 == 0 ? 1 : 0));
        return dy < dx ? dx : dx + (dy - dx) / 2;
    }
    private static void ValidateMoment(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != 7 || moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.CycleWithinAtomicBlock is < 0 or > 2 || state.NativeMapSeason is not { } season ||
            moment.CalendarCycleOrdinal != checked(season.AdvancedCalendarSteps + moment.CycleWithinAtomicBlock) ||
            state.NativeClock?.UpdateOrdinal != checked(season.ProcessedNativeOrdinal + (moment.CycleWithinAtomicBlock == 2 ? 1 : 0)))
            throw new InvalidDataException("Eligibility query belongs to another reached stage7/calendar owner.");
        MapSeasonRuntime.ValidateOwned(season);
        var date = season.ThirdDate;
        for (int i = 0; i < moment.CycleWithinAtomicBlock; i++) date = MapSeasonRuntime.StepCalendarWriter(date).Date;
        if (date != moment.PreWriterThirdDate) throw new InvalidDataException("Eligibility query has another pre-writer date.");
    }
    private static ArmyMovementEligibilityCurrentResolution Missing(string owner) => new(null, owner);
}
