using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal int ReadOriginalAiFightingStrengthCurrent(GameState state, SourceWorldContext current,
        BaseReference source, int radius, int reservedTroops, bool requireGoalInRange, bool onlyAvailablePeople)
    {
        //07D6B0 explicitly disables home-army augmentation in the Base kernel.
        float total = ReadOriginalAiWeightedStrengthCurrent(state, current, source, false, reservedTroops, onlyAvailablePeople);
        if (radius <= 0) return BattleUnitSourceMath.TruncateSingleToInt32(total);
        int force = FightingKnown(current.ReadBaseForceSlot(source), "Base_Force");
        int homeHex = current.ReadBaseCurrentHexIndex(source);
        foreach (var army in FightingForceArmies(state, current, force))
        {
            //The optional main-target gate precedes the status/current-Hex gate.
            if (requireGoalInRange && FireCommandDistance(FightingArmyGoal(current, army), homeHex) > radius) continue;
            if (onlyAvailablePeople && FightingKnown(current.ReadArmyReturnUnitStatus10(army), "Army10") == 2) continue;
            if (FireCommandDistance(FightingKnown(current.ReadArmyUnitRawHex2A(army), "Army2A"), homeHex) > radius) continue;
            ushort troops = FightingKnown(current.ReadArmyReturnResourceFields(army)?.Ready16, "Army16");
            float amount = (float)troops * ReadOriginalAiArmyCommonCurrent(state, current, army);
            amount /= FightingParameter(state, OriginalAiCrisisSource.StrengthDivisorKey);
            total += amount;
        }
        return BattleUnitSourceMath.TruncateSingleToInt32(total);
    }

    internal int? ReadOriginalAiForceEvaluationCurrent(GameState state, SourceWorldContext current,
        OriginalAiActor actor, int queriedForce, int mode, bool variableNode)
    {
        if (mode == 4)
        {
            int ruler = FightingForceRuler(current, queriedForce);
            int preference = FightingKnown(RulerCaptureReleaseInputs.ReadCurrentWord(state, ruler, 0x16A), "ruler16A");
            int coefficient = FightingParameter(state, "original-ai.force-defense-personality-percent");
            int total = 0;
            if (queriedForce is >= 1 and <= 51)
                foreach (var entry in current.ReadRegistryReferences(RegistryKind.District) ?? throw FightingMissing("WorldD0"))
                {
                    if (entry is not { Kind: RegistryKind.District, PhysicalSlot: int district }) throw FightingMissing("District_pointer");
                    if (FightingKnown(current.ReadDistrictForceSlot(new(district)), "District10") != queriedForce) continue;
                    foreach (var registered in current.ReadRegisteredBases() ?? throw FightingMissing("World80"))
                    {
                        var at = registered ?? throw FightingMissing("Base_pointer");
                        if (FightingKnown(current.ReadBaseDistrictSlot(at), "Base_virtual78") != district) continue;
                        int troops = FightingKnown(current.ReadBaseResource(at, BaseResourceKind.Troops), "Base3C");
                        int morale = FightingKnown(current.ReadBaseMorale44(at), "Base44");
                        int weighted = unchecked(unchecked(troops * coefficient * preference) / 10000 + troops);
                        total = unchecked(total + unchecked(morale * weighted) / 100);
                    }
                }
            return variableNode && total == 0 ? 1 : total;
        }
        if (mode != 5) throw FightingMissing("ForceInfo_mode." + mode);
        int disposition;
        if (actor.AgentKind == OriginalAiAgentKind.Troops)
            disposition = ReadOriginalAiTroopsDispositionCurrent(state, current,
                actor.Troops ?? throw FightingMissing("actor_Troops_reference"));
        else
        {
            int leader;
            if (actor.AgentKind == OriginalAiAgentKind.District)
            {
                int district = actor.DistrictId ?? throw FightingMissing("actor_District_identity");
                int raw = FightingKnown(current.ReadDistrictLeader(district is >= 0 and <= 51 ? district : 0), "actor_District_leader");
                leader = FightingPerson(current, raw);
            }
            else if (actor.AgentKind == OriginalAiAgentKind.Force) leader = FightingForceRuler(current, actor.ForceId);
            else throw FightingMissing("actor_AgentType." + actor.AgentKind);
            disposition = FightingKnown(current.ReadAllegianceDisposition(leader), "actor_leader16C");
        }
        var own = ReadOriginalAiMyselfForce(current, actor);
        if (own is not { } active || !ReadOriginalAiForceValidity(current, active.Id)) return null;
        int relation = 0;
        if ((uint)queriedForce < 52)
        {
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(active.Id, out var reset) == true)
                relation = FightingKnown(reset[0x13 + queriedForce], "actor_reset_relation13");
            else
            {
                var values = active.Id == 0 ? state.NativeForceUnregisterCurrent?.PlaceholderRelations13
                    : state.NativeArmySimulation?.Forces.GetValueOrDefault(active.Id)?.NormalAftermath?.Relations13;
                relation = values is { Length: 52 } ? values[queriedForce] : throw FightingMissing("actor_relation13");
            }
        }
        return unchecked(unchecked(FightingParameter(state, "original-ai.force-relationship-personality-percent") * relation * disposition) / 10000 + relation);
    }

    private static IEnumerable<ArmyUnitReference> FightingForceArmies(GameState state, SourceWorldContext current, int force)
    {
        if (force is < 1 or > 51) yield break;
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw FightingMissing("World60"))
        {
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical }) throw FightingMissing("Army_pointer");
            var army = new ArmyUnitReference(physical);
            var owner = current.ReadArmyUnitForceReference(army);
            if (FightingKnown(current.ReadForcePhysicalSlot(owner), "Army_Force") != force) continue;
            if (FightingKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "Army233560")) continue;
            yield return army;
        }
    }

    private static int FightingArmyGoal(SourceWorldContext current, ArmyUnitReference army)
    {
        var target = current.ReadArmyLifecycleTargetFields(FightingKnown(army.PhysicalSlot, "goal_Army_pointer"))
            ?? throw FightingMissing("Army_target_owner");
        byte kind = FightingKnown(target.Kind38, "Army38");
        if (kind is not (5 or 6 or 9 or 11 or 27)) return 48400;
        ushort value = FightingKnown(target.Identity3A, "Army3A");
        if (kind == 9) return value;
        if (kind == 27)
        {
            var other = current.ReadArmyUnitTableReference(value <= 500 ? value : 0) ?? throw FightingMissing("goal_Army_lookup");
            return FightingKnown(current.ReadArmyUnitRawHex2A(other), "goal_Army2A");
        }
        if (kind is 5 or 6)
        {
            var at = kind == 5 ? current.ReadCityTableReference(value <= 51 ? value : 0)
                : current.ReadGateTableReference(value <= 10 ? value : 0);
            value = FightingKnown(at is null ? null : current.ReadBaseObjectLink(at), "goal_Base4E");
        }
        var obj = current.ReadObjectTableReference(value <= 3000 ? value : 0) ?? throw FightingMissing("goal_Object_lookup");
        return FightingKnown(current.ReadObjectHexIndex(obj), "goal_Object10");
    }

    private static int FightingForceRuler(SourceWorldContext current, int force)
    {
        int raw = current.ReadForceRecord(force is >= 0 and <= 51 ? force : 0)?.RawRuler10
            ?? throw FightingMissing("Force_ruler10");
        return FightingPerson(current, raw);
    }
    private static int FightingPerson(SourceWorldContext current, int identity) => FightingKnown(
        (identity is >= 0 and <= 6000 ? current.ReadPersonLookupReference(identity) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
        "Person_lookup");
    private static int FightingParameter(GameState state, string key) => state.NativeArmySimulation?.Parameters.Int32Values is { } bank &&
        bank.TryGetValue(key, out int value) ? value : throw FightingMissing("current_parameter." + key);
    private static T FightingKnown<T>(T? value, string field) where T : struct => value ?? throw FightingMissing(field);
    private static InvalidDataException FightingMissing(string field) => new("original_ai.fighting_strength." + field);
}
