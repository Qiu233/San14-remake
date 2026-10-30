using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    CommandResult IArmyInstructionExecution.MoveToAdvantagePosition(GameState state, ForceState force,
        MoveArmyToAdvantagePositionOrder order, CommandContext context)
        => ApplyOriginalAiArmyAction(state, order.UnitId, "move_advantage_position", (current, army) =>
        {
            int? point = FindOriginalAiAdvantagePositionCurrent(state, current, army);
            if (point is null) return (state, false);
            var changed = PrependOriginalAiArmyRelayCurrent(state, current, army, point.Value);
            changed = ArmyAiActionFlagsSource.Or(changed, army.Id, 0x10);
            if (army.NativeTacticRuntime?.Abnormal25 == 0)
                changed = ClearTrackedArmyOverride(changed, current, changed.Units[army.Id]);
            return (changed, true);
        });

    CommandResult IArmyInstructionExecution.MoveToObjectAttackPosition(GameState state, ForceState force,
        MoveArmyToObjectAttackPositionOrder order, CommandContext context)
        => ApplyOriginalAiArmyAction(state, order.UnitId, "move_object_attack_position", (current, army) =>
        {
            int? point = FindOriginalAiObjectAttackPositionCurrent(state, current, army);
            if (point is null) return (state, false);
            var changed = WriteOriginalAiArmyRelayCurrent(state, army, Enumerable.Repeat((ushort)48400, 5).ToArray());
            changed = SetOriginalAiArmyMoveCurrent(changed, changed.Units[army.Id], point.Value);
            changed = ArmyAiActionFlagsSource.Or(changed, army.Id, 0x40);
            if (army.NativeTacticRuntime?.Abnormal25 == 0)
                changed = ClearTrackedArmyOverride(changed, current, changed.Units[army.Id]);
            return (changed, true);
        });

    //0C82A0: move an engaged short-range attacker behind a stronger or
    //long-range friendly attacker. The original ring order breaks ties.
    private int? FindOriginalAiObjectAttackPositionCurrent(GameState state, SourceWorldContext current, UnitState army)
    {
        var header = army.NativeTacticRuntime ?? throw AiPositionMissing("Army_runtime");
        if ((header.Flags26 & 1) == 0) return null;
        int physical = AiPositionNeed(army.NativeArmyOrigin?.NativeSlotIndex, "Army_receiver");
        var target = current.ReadArmyLifecycleTargetFields(physical) ?? throw AiPositionMissing("Army_target");
        int kind = AiPositionNeed(target.Kind38, "Army38");
        int identity = AiPositionNeed(target.Identity3A, "Army3A");
        if (kind is not (5 or 6 or 11)) return null;
        int formation = AiPositionFormation(state, current, army);
        if ((AiPositionNeed(TacticRemakeCurrentFormations.ReadParameter(state, formation, 0xA6), "Formation_flags") & 2) != 0) return null;
        int origin = AiPositionHex(current, AiPositionNeed(ArmyMarchSourceAdapter.ReadCurrentHex(state, army), "Army2A"));
        int ownForce = AiPositionForce(current, new(physical));
        int targetHex;
        if (kind == 6)
        {
            var gate = current.ReadGateTableReference(identity <= 10 ? identity : 0) ?? throw AiPositionMissing("target_Gate");
            if (!AiPositionNeed(current.ReadBaseNativeValidity(gate), "target_Gate_validity")) return null;
            targetHex = current.ReadBaseCurrentHexIndex(gate);
            if (ArmyRouteSearch.Distance(targetHex, origin) >= 3) return null;
        }
        else if (kind == 11)
        {
            var obj = current.ReadObjectTableReference(identity <= 3000 ? identity : 0) ?? throw AiPositionMissing("target_Object");
            if (!AiPositionNeed(current.ReadObjectNativeValidity(obj), "target_Object_validity")) return null;
            targetHex = AiPositionNeed(current.ReadObjectHexIndex(obj), "target_Object_Hex");
            if (ArmyRouteSearch.Distance(targetHex, origin) >= 2) return null;
        }
        else return null; //Source outer kind5 passes, but has no action branch.

        var nearby = AiPositionNearbyArmies(current, new(physical), 2);
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        var rules = new TacticRemakeCurrentContext(current, _catalog, _map, _terrain, _usage, Revision, abilities);
        var queries = new TacticRemakeCurrentBattleQueries(rules, current, Revision);
        var attributes = new BattleRemakeCurrentArmyAttributes(_battleSeed, current, abilities, queries, Revision);
        float Wall(UnitState unit, int selected, bool temporary) =>
            attributes.ResolveWallOffense(state, unit, selected, temporary) ?? throw AiPositionMissing("WallAttack2AA720." + unit.Id);
        float ownWall = Wall(army, formation, kind != 6);
        float best = kind == 6 ? ownWall + 5f : 0f;
        UnitState? selectedArmy = null;
        foreach (var other in nearby)
        {
            if (other.PhysicalSlot == physical || AiPositionForce(current, other) != ownForce) continue;
            var otherImage = CampaignRetreatArmyImage(state, AiPositionNeed(other.PhysicalSlot, "nearby_Army_receiver"));
            if (((otherImage.NativeTacticRuntime ?? throw AiPositionMissing("nearby_runtime")).Flags26 & 1) != 0) continue;
            var otherTarget = current.ReadArmyLifecycleTargetFields(other.PhysicalSlot!.Value) ?? throw AiPositionMissing("nearby_target");
            if (AiPositionNeed(otherTarget.Identity3A, "nearby_Army3A") != identity ||
                AiPositionNeed(otherTarget.Kind38, "nearby_Army38") != (kind == 6 ? 6 : 5)) continue;
            //The Gate branch evaluates the other Army using OUR current
            //Formation. The Object branch uses each Army's own Formation.
            float value = Wall(otherImage, kind == 6 ? formation : AiPositionFormation(state, current, otherImage), kind != 6);
            if (value > ownWall && value > best) { best = value; selectedArmy = otherImage; }
        }
        if (selectedArmy is not null) return AiPositionNeed(ArmyMarchSourceAdapter.ReadCurrentHex(state, selectedArmy), "selected_Army2A");
        if (kind != 6) return null;
        foreach (var other in nearby)
        {
            if (other.PhysicalSlot == physical) continue;
            var otherImage = CampaignRetreatArmyImage(state, AiPositionNeed(other.PhysicalSlot, "nearby_Army_receiver"));
            int otherFormation = AiPositionFormation(state, current, otherImage);
            if ((AiPositionNeed(TacticRemakeCurrentFormations.ReadParameter(state, otherFormation, 0xA6), "nearby_Formation_flags") & 2) == 0 ||
                AiPositionForce(current, other) != ownForce) continue;
            int at = AiPositionNeed(current.ReadArmyUnitRawHex2A(other), "nearby_Hex2A");
            var otherTarget = current.ReadArmyLifecycleTargetFields(other.PhysicalSlot!.Value) ?? throw AiPositionMissing("nearby_target");
            if (ArmyRouteSearch.Distance(targetHex, at) > 1 &&
                AiPositionNeed(otherTarget.Identity3A, "nearby_Army3A") == identity && AiPositionNeed(otherTarget.Kind38, "nearby_Army38") == 6) return at;
        }
        return null;
    }

    private int? FindOriginalAiAdvantagePositionCurrent(GameState state, SourceWorldContext current, UnitState army)
    {
        if (((army.NativeTacticRuntime ?? throw AiPositionMissing("Army_runtime")).Flags26 & 1) == 0) return null;
        int physical = AiPositionNeed(army.NativeArmyOrigin?.NativeSlotIndex, "Army_receiver");
        var target = current.ReadArmyLifecycleTargetFields(physical) ?? throw AiPositionMissing("Army_target");
        var special = ArmyOverrideTargetSourceAdapter.Read(state, army);
        int targetArmy = AiPositionNeed(target.Kind38, "Army38") == 27 ? AiPositionNeed(target.Identity3A, "Army3A")
            : AiPositionNeed(special.Kind3C, "Army3C") == 27 ? AiPositionNeed(special.Identity3E, "Army3E") : -1;
        if (targetArmy < 0) return null;
        int origin = AiPositionHex(current, AiPositionNeed(ArmyMarchSourceAdapter.ReadCurrentHex(state, army), "Army2A"));
        int ownForce = AiPositionForce(current, new(physical));
        for (int direction = 1; direction <= 6; direction++)
        {
            int hex = PersonReturnPathSource.Neighbor(origin, direction);
            if (hex >= 48400) continue;
            var enemy = AiPositionArmyAt(current, hex);
            if (!AiPositionNeed(current.ReadArmyUnitNativeValidity(enemy), "neighbor_Army_validity") || enemy.PhysicalSlot != targetArmy) continue;
            var enemyImage = CampaignRetreatArmyImage(state, AiPositionNeed(enemy.PhysicalSlot, "enemy_Army_receiver"));
            if (AiPositionNeed(ArmyConfigurationSourceAdapter.ReadCategory(state, enemyImage), "enemy_Army40") != 2)
            {
                var enemyTarget = current.ReadArmyLifecycleTargetFields(enemy.PhysicalSlot!.Value) ?? throw AiPositionMissing("enemy_target");
                var enemyOverride = ArmyOverrideTargetSourceAdapter.Read(state, enemyImage);
                bool reciprocal = AiPositionNeed(enemyTarget.Kind38, "enemy_Army38") == 27 && AiPositionNeed(enemyTarget.Identity3A, "enemy_Army3A") == physical ||
                    AiPositionNeed(enemyOverride.Kind3C, "enemy_Army3C") == 27 && AiPositionNeed(enemyOverride.Identity3E, "enemy_Army3E") == physical;
                if (!reciprocal) return null;
            }
            int commander = AiPositionCommander(current, army);
            int surface = AiPositionNeed(current.ReadHexSurfaceCode(new(origin)), "Army_surface");
            int[][] preferences = [[8, 9, 10], [5], [5, 21, 4, 13, 12], [5, 6], [15], [15], [15]];
            int[][] traits = [[69, 71, 72, 73, 74], [70], [75], [76], [65], [66], [216]];
            for (int group = 0; group < traits.Length; group++)
            {
                if (!traits[group].Any(id => AiPositionHasPersonality(current, commander, id))) continue;
                if (preferences[group].Contains(surface)) return null;
                int? preferred = FindOriginalAiPreferredTerrainCurrent(state, current, army, origin, ownForce, preferences[group]);
                if (preferred is not null) return preferred;
            }
            var friendly = new List<(ObjectReference Object, int Kind, int Distance, int Priority)>();
            foreach (var obj in AiPositionNearbyObjects(current, origin, 5))
            {
                int owner = AiPositionNeed(current.ReadObjectForceSlot13(obj), "Object13");
                var forceRef = current.ReadForceTableReference(owner <= 51 ? owner : 0) ?? throw AiPositionMissing("Object_Force_pointer");
                if (AiPositionNeed(current.ReadForceNativeValidity(forceRef), "Object_Force_validity") && owner != ownForce) continue;
                var definition = AiPositionDefinition(current, obj);
                if (!AiPositionNeed(current.ReadObjectDefinitionValidity(definition), "Object_definition_validity")) continue;
                int kind = AiPositionNeed(definition.PhysicalSlot, "Object_definition_identity");
                if (kind < 16 && ((0x8178u >> kind) & 1) != 0 && AiPositionReachable(state, current, army, origin, AiPositionObjectHex(current, obj)))
                    friendly.Add((obj, kind, ArmyRouteSearch.Distance(origin, AiPositionObjectHex(current, obj)), 1));
            }
            if (friendly.Count != 0)
            {
                var selected = friendly.OrderBy(x => x.Distance).ThenByDescending(x => x.Priority).First();
                var coverage = AiPositionObjectRange(state, current, selected.Object);
                int focus = selected.Kind is 3 or 4 ? origin : hex;
                if (!coverage.Contains(focus))
                {
                    int center = AiPositionObjectHex(current, selected.Object);
                    int chosenDirection = AiPositionDirectionToward(center, origin);
                    int point = PersonReturnPathSource.Neighbor(center, chosenDirection);
                    //The source retries this SAME direction six times; it
                    //does not rotate to an arbitrary eligible neighbor.
                    if (point < 48400 && AiPositionEligible(state, current, army, point, ownForce)) return point;
                }
            }
            var hostile = new List<(ObjectReference Object, int Distance, int Priority)>();
            foreach (var obj in AiPositionNearbyObjects(current, origin, 3))
            {
                int owner = AiPositionNeed(current.ReadObjectForceSlot13(obj), "Object13");
                var forceRef = current.ReadForceTableReference(owner <= 51 ? owner : 0) ?? throw AiPositionMissing("Object_Force_pointer");
                if (!AiPositionNeed(current.ReadForceNativeValidity(forceRef), "Object_Force_validity") ||
                    !AiPositionNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state, owner, ownForce), "Object_hostility")) continue;
                var definition = AiPositionDefinition(current, obj);
                if (!AiPositionNeed(current.ReadObjectDefinitionValidity(definition), "Object_definition_validity") ||
                    !AiPositionNeed(FacilityActivationCurrent.ReadEffectsEnabled(current, obj), "Object16")) continue;
                int kind = AiPositionNeed(definition.PhysicalSlot, "Object_definition_identity");
                if (kind is not (5 or 6 or 15) || !AiPositionReachable(state, current, army, origin, AiPositionObjectHex(current, obj))) continue;
                hostile.Add((obj, ArmyRouteSearch.Distance(origin, AiPositionObjectHex(current, obj)), kind == 6 ? 4 : 1));
            }
            if (hostile.Count != 0)
            {
                var selected = hostile.OrderBy(x => x.Distance).OrderByDescending(x => x.Priority).First();
                var coverage = AiPositionObjectRange(state, current, selected.Object);
                if (coverage.Contains(origin))
                {
                    int center = AiPositionObjectHex(current, selected.Object);
                    foreach (int point in AiPositionRing(current, center, 4).Where(point => !coverage.Contains(point) &&
                        !AiPositionInHostileObjectRange(state, current, point, ownForce) && AiPositionEligible(state, current, army, point, ownForce))
                        .OrderBy(point => ArmyRouteSearch.Distance(origin, point))) return point;
                }
            }
            return null; //The source leaves after the first matched opponent.
        }
        return null;
    }
}
