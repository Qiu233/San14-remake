using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    CommandResult IArmyInstructionExecution.OccupyNeighborCapital(GameState state, ForceState force,
        OccupyArmyNeighborCapitalOrder order, CommandContext context)
        => ApplyOriginalAiArmyAction(state, order.UnitId, "occupy_neighbor_capital", (current, army) =>
            ApplyOriginalAiArmyNeighborCurrent(state, current, army, capital: true));

    CommandResult IArmyInstructionExecution.OccupyNeighborScenery(GameState state, ForceState force,
        OccupyArmyNeighborSceneryOrder order, CommandContext context)
        => ApplyOriginalAiArmyAction(state, order.UnitId, "occupy_neighbor_scenery", (current, army) =>
            ApplyOriginalAiArmyNeighborCurrent(state, current, army, capital: false));

    private (GameState State, bool Applied) ApplyOriginalAiArmyNeighborCurrent(GameState state,
        SourceWorldContext current, UnitState army, bool capital)
    {
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw AiArmyNeighborMissing("Army_receiver");
        var actor = new ArmyUnitReference(physical);
        if (OriginalAiArmyNeighborGroupBlocks(state, current, army, actor)) return (state, false);
        int raw = AiArmyNeighborNeed(current.ReadArmyUnitRawHex2A(actor), "Army_Hex2A");
        var start = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw AiArmyNeighborMissing("Army_Hex_pointer");
        if (start.PhysicalSlot is not int origin) return (state, false);
        byte surface = AiArmyNeighborNeed(current.ReadHexSurfaceCode(start), "origin_Surface10");
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0)
            ?? throw AiArmyNeighborMissing("origin_Terrain_pointer");
        bool naval = AiArmyNeighborNeed(current.ReadSurfaceTypeWoundedCategory42(terrain), "origin_Terrain_category42") == 4;
        int formation = AiArmyNeighborNeed(naval ? ArmyConfigurationSourceAdapter.ReadNaval(state, army)
            : ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army), "selected_Formation");
        if (formation >= 21) formation = 0;
        int owner = AiArmyNeighborNeed(current.ReadArmyUnitForceReference(actor)?.PhysicalSlot, "Army_Force");
        //2AD9C0(kind1/2,1) is only the current Formation mobility coefficient,
        //not route mobility, traits, terrain cost or a prepared Army stat page.
        int radius = ReadOriginalAiFormationInformationCurrent(state, formation, owner, 0) / 10;
        if (radius <= 0) return (state, false);
        var working = state;
        int? destination = FindOriginalAiArmyNeighborCurrent(working, current, owner, origin, radius, capital);
        if (destination is not int chosen) return (state, false);

        //CC210 and CC5B0 call the opposite selector once, centered on their
        //chosen target, before prepending their own target. The nested return
        //does not decide the primary action's success. It does not recurse.
        int? secondary = FindOriginalAiArmyNeighborCurrent(working, current, owner, chosen, radius, !capital);
        if (secondary is int other)
        {
            working = PrependOriginalAiArmyRelayCurrent(working, current, working.Units[army.Id], other);
        }
        working = PrependOriginalAiArmyRelayCurrent(working, current, working.Units[army.Id], chosen);
        working = ArmyAiActionFlagsSource.Or(working, army.Id, capital ? (ushort)2 : (ushort)0x80);
        var changed = working.Units[army.Id];
        if ((changed.NativeTacticRuntime ?? throw AiArmyNeighborMissing("Army_runtime")).Abnormal25 == 0)
            working = ClearTrackedArmyOverride(working, current, changed);
        return (working, true);
    }

    private static bool OriginalAiArmyNeighborGroupBlocks(GameState state, SourceWorldContext current,
        UnitState army, ArmyUnitReference actor)
    {
        //Borrow only this receiver's saved58 assignment. The general writable
        //route capture need not rebuild all501 Army/group rows for this test.
        var groups = state.ReadArmyLifecycle()?.ArmyGroups ?? throw AiArmyNeighborMissing("Army_group_owner");
        var assignment = groups.UnitAssignments.GetValueOrDefault(army.Id)
            ?? throw AiArmyNeighborMissing("Army_group_assignment58");
        if (!assignment.IsKnown) throw AiArmyNeighborMissing("Army_group_assignment58_unknown");
        int group = assignment.GroupId ?? 0;
        if (group is < 0 or > 500) group = 0;
        var purpose = groups.Objectives?.GetValueOrDefault(group)?.Purpose ?? throw AiArmyNeighborMissing("Group_purpose14_16");
        if (purpose.Kind != ArmyGroupPurposeKind.CaptureArea) return false;
        //The source intentionally reads this stored Area-ID word16 as a Hex
        //in this consumer. Preserve that behavior; do not convert it to a center.
        int raw = purpose.Target.Id;
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw AiArmyNeighborMissing("Group_target_Hex");
        var area = OccupationArea(current, hex);
        int center = AiArmyNeighborNeed(current.ReadAreaHexIndex(area), "Group_target_Area_center");
        int at = AiArmyNeighborNeed(hex.PhysicalSlot, "Group_target_Hex_identity");
        if (center != at) return false;
        int owner = AiArmyNeighborNeed(current.ReadArmyUnitForceReference(actor)?.PhysicalSlot, "Group_Army_Force");
        return AiArmyNeighborNeed(current.ReadAreaOccupationClaimForceSlot(area), "Group_target_Area73") == owner;
    }

    private static int? FindOriginalAiArmyNeighborCurrent(GameState state, SourceWorldContext current,
        int forceId, int origin, int radius, bool capital)
    {
        var own = current.ReadForceTableReference(forceId is >= 0 and <= 51 ? forceId : 0)
            ?? throw AiArmyNeighborMissing("candidate_actor_Force");
        //2A7EE0 is the source ring order excluding the center. It does not
        //sort by a route score or choose the nearest domain Area/Scenery list.
        foreach (int candidate in TacticRemakeCurrentContext.NativeHexOrder(origin, radius).Skip(1))
        {
            var hex = current.ReadHexTableReference(candidate) ?? throw AiArmyNeighborMissing("candidate_Hex");
            if (hex.PhysicalSlot is null) continue;
            var area = OccupationArea(current, hex);
            if (capital)
            {
                int rawCenter = AiArmyNeighborNeed(current.ReadAreaHexIndex(area), "candidate_Area_center");
                var center = current.ReadHexTableReference(rawCenter < 48400 ? rawCenter : 0)
                    ?? throw AiArmyNeighborMissing("candidate_Area_center_pointer");
                if (hex != center) continue;
            }
            else
            {
                int rawScenery = AiArmyNeighborNeed(current.ReadHexScenerySlot(hex), "candidate_Scenery1E");
                var scenery = current.ReadSceneryTableReference(rawScenery < 101 ? rawScenery : 0)
                    ?? throw AiArmyNeighborMissing("candidate_Scenery_pointer");
                if (!AiArmyNeighborNeed(current.ReadSceneryNativeValidity(scenery), "candidate_Scenery_validity") ||
                    (AiArmyNeighborNeed(current.ReadSceneryFlags47(scenery), "candidate_Scenery_flags47") & 1) == 0) continue;
            }
            if (IsOccupationCityArea(current, area)) continue;
            int rawAreaHex = AiArmyNeighborNeed(current.ReadAreaHexIndex(area), "candidate_Area_Hex3A");
            var areaHex = current.ReadHexTableReference(rawAreaHex < 48400 ? rawAreaHex : 0)
                ?? throw AiArmyNeighborMissing("candidate_Area_Hex_pointer");
            if (AiArmyNeighborNeed(current.ReadHexSurfaceCode(areaHex), "candidate_Area_center_surface") == 17) continue;
            int rawForce = AiArmyNeighborNeed(current.ReadHexForceSlot(hex), "candidate_Force14");
            var other = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0) ?? throw AiArmyNeighborMissing("candidate_Force_pointer");
            //2C5420 is !2C3CA0, not the asymmetric path-cost hostility gate.
            if (AiArmyNeighborNeed(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, own, other), "candidate_friendly_relation")) continue;
            int occupied = AiArmyNeighborNeed(current.ReadHexRaw1C(hex), "candidate_Army1C");
            if (occupied is >= 1 and <= 500) continue;
            return AiArmyNeighborNeed(hex.PhysicalSlot, "candidate_Hex_identity");
        }
        return null;
    }

    private static T AiArmyNeighborNeed<T>(T? value, string field) where T : struct => value ?? throw AiArmyNeighborMissing(field);
    private static InvalidDataException AiArmyNeighborMissing(string field) => new("original_ai.Army_neighbor." + field);
}
