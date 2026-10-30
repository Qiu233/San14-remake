using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>257D40's final special-Force service. The actual manager60
    /// order and this host's current/pool are retained throughout the batch.</summary>
    internal GameState ApplyOriginalAiSpecialForceWithdrawalCurrent(GameState state,
        SourceWorldContext current, int forceId)
    {
        var force = current.ReadForceTableReference(forceId is >= 0 and <= 51 ? forceId : 0)
            ?? throw AiSpecialWithdrawalMissing("Force_pointer");
        int physicalForce = AiSpecialWithdrawalNeed(current.ReadForcePhysicalSlot(force), "Force_identity");
        if (physicalForce is < 46 or > 50) return state;
        var registry = (current.ReadRegistryReferences(RegistryKind.Army)
            ?? throw AiSpecialWithdrawalMissing("manager60")).ToArray();
        var working = state;
        var operation = FacilityOperation.Playable(state);
        foreach (var entry in registry)
        {
            int physical = AiSpecialWithdrawalNeed(entry?.PhysicalSlot, "manager60_Army");
            var army = new ArmyUnitReference(physical);
            if (current.IsArmyUnitMapWriteSkipped(army)) continue;
            if (AiSpecialWithdrawalNeed(current.ReadArmyExclusionForQuerySnapshot(working, physical), "Army233560")) continue;
            if (!current.IsArmyUnitBoundToSpecialForce(army)) continue;
            if (AiSpecialWithdrawalNeed(current.ReadArmyUnitForceReference(army).PhysicalSlot, "Army_Force") != physicalForce) continue;
            if (!ShouldWithdrawOriginalAiSpecialArmyCurrent(working, current, army)) continue;
            //The existing normal withdrawal owns command/control, route,
            //override, no-path speech/report and receiver publication rules.
            working = ApplyCampaignFieldWithdrawalCurrent(working, current,
                CampaignRetreatArmyImage(working, physical), operation);
        }
        return working;
    }

    private bool ShouldWithdrawOriginalAiSpecialArmyCurrent(GameState state,
        SourceWorldContext current, ArmyUnitReference army)
    {
        int physical = AiSpecialWithdrawalNeed(army.PhysicalSlot, "Army_identity");
        var image = CampaignRetreatArmyImage(state, physical);
        //22A730 has only the commander validity gate. Preserve its City0
        //lookup instead of FindArmyUnitCurrentBase's additional Army gate.
        int commander = AiSpecialWithdrawalNeed(current.ReadArmyUnitCommanderId(army), "Commander12");
        int person = AiSpecialWithdrawalNeed(current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)?.PhysicalSlot,
            "Commander_pointer");
        int location = AiSpecialWithdrawalNeed(current.ReadPersonNativeValidity(person), "Commander_validity")
            ? AiSpecialWithdrawalNeed(current.ReadPersonFirstLocation(person), "Commander_E2") : 0;
        var home = BaseLookup.Find(location, current);
        int homeHex = ReadOriginalAiSpecialWithdrawalBaseHex(current, home);
        byte action = AiSpecialWithdrawalNeed(ArmyConfigurationSourceAdapter.ReadAction(state, image), "Army37");
        if (action != 7 && action is not (4 or 5)) return false;
        var target = current.ReadArmyLifecycleTargetFields(physical) ?? throw AiSpecialWithdrawalMissing("Army_target");
        byte kind = AiSpecialWithdrawalNeed(target.Kind38, "Army38");
        int identity = AiSpecialWithdrawalNeed(target.Identity3A, "Army3A");
        if (action == 7)
        {
            if (kind != 27) return false;
            var pursued = current.ReadArmyUnitTableReference(identity <= 500 ? identity : 0)
                ?? throw AiSpecialWithdrawalMissing("pursued_Army_pointer");
            int rawHex = AiSpecialWithdrawalNeed(current.ReadArmyUnitRawHex2A(pursued), "pursued_Hex2A");
            var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
                ?? throw AiSpecialWithdrawalMissing("pursued_Hex_pointer");
            int surface = AiSpecialWithdrawalNeed(current.ReadHexSurfaceCode(hex), "pursued_Hex10");
            var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0)
                ?? throw AiSpecialWithdrawalMissing("pursued_Terrain_pointer");
            byte flags = AiSpecialWithdrawalNeed(PersonArmyCaptureInputs.ReadTerrainFlags2C(state,
                AiSpecialWithdrawalNeed(terrain.PhysicalSlot, "pursued_Terrain_identity")), "pursued_Terrain2C");
            return (flags & 2) != 0 || ArmyRouteSearch.Distance(homeHex,
                AiSpecialWithdrawalNeed(hex.PhysicalSlot, "pursued_Hex_identity")) >=
                ReadOriginalAiSpecialWithdrawalParameter(state, "pursuit-distance");
        }

        int ownForce = AiSpecialWithdrawalNeed(current.ReadArmyUnitForceReference(army).PhysicalSlot, "Army_Force");
        var center = current.ReadHexTableReference(homeHex) ?? throw AiSpecialWithdrawalMissing("home_Hex_pointer");
        var nearby = current.CreateNearbyTemporary();
        int enemies = 0, friendlies = 0;
        try
        {
            TrapCandidates.FillNearbyArmies(state, current, center,
                ReadOriginalAiSpecialWithdrawalParameter(state, "enemy-radius"), nearby);
            foreach (var other in ReadOriginalAiSpecialWithdrawalNearby(current, nearby))
            {
                int otherForce = AiSpecialWithdrawalNeed(current.ReadArmyUnitForceReference(other).PhysicalSlot, "nearby_Force");
                if (AiSpecialWithdrawalNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state, otherForce, ownForce), "nearby_hostility"))
                    enemies = unchecked(enemies + AiSpecialWithdrawalNeed(current.ReadArmyReturnResourceFields(other)?.Ready16, "nearby_Army16"));
            }
            //The source clears and reuses the same descriptor between samples.
            TrapCandidates.FillNearbyArmies(state, current, center,
                ReadOriginalAiSpecialWithdrawalParameter(state, "friendly-radius"), nearby);
            foreach (var other in ReadOriginalAiSpecialWithdrawalNearby(current, nearby))
                if (AiSpecialWithdrawalNeed(current.ReadArmyUnitForceReference(other).PhysicalSlot, "nearby_Force") == ownForce)
                    friendlies = unchecked(friendlies + AiSpecialWithdrawalNeed(current.ReadArmyReturnResourceFields(other)?.Ready16, "nearby_Army16"));
        }
        finally { current.ClearNearbyTemporary(nearby, true); }
        int baseTroops = AiSpecialWithdrawalNeed(current.ReadBaseResource(home, BaseResourceKind.Troops), "home_Base3C");
        int threshold = unchecked(unchecked(baseTroops + friendlies) *
            ReadOriginalAiSpecialWithdrawalParameter(state, "strength-percent")) / 100;
        if (enemies > threshold) return true;

        BaseReference? targetBase = kind switch
        {
            5 => current.ReadCityTableReference(identity <= 51 ? identity : 0),
            6 => current.ReadGateTableReference(identity <= 10 ? identity : 0),
            _ => null
        };
        //The original retains targetForce=0 for every other kind.
        int targetForce = targetBase is null ? 0 : AiSpecialWithdrawalNeed(current.ReadBaseForceSlot(targetBase), "target_Base_Force");
        int relation = targetForce < 52 ? DRelations(state, ownForce)[targetForce] : 0;
        return relation >= ReadOriginalAiSpecialWithdrawalParameter(state, "relation-threshold");
    }

    private static IEnumerable<ArmyUnitReference> ReadOriginalAiSpecialWithdrawalNearby(
        SourceWorldContext current, HexListHandle list)
    {
        var node = current.ReadRegistryHead(new(list.Identity)) ?? throw AiSpecialWithdrawalMissing("nearby_head");
        while (node.Identity.HasValue)
        {
            var payload = current.ReadRegistryNodePayload(node) ?? throw AiSpecialWithdrawalMissing("nearby_payload");
            if (payload.Kind != RegistryKind.Army || payload.PhysicalSlot is not int physical)
                throw AiSpecialWithdrawalMissing("nearby_Army_identity");
            yield return new(physical);
            node = current.ReadRegistryNodeNext(node) ?? throw AiSpecialWithdrawalMissing("nearby_next");
        }
    }

    private static int ReadOriginalAiSpecialWithdrawalBaseHex(SourceWorldContext current, BaseReference home)
    {
        int link = AiSpecialWithdrawalNeed(current.ReadBaseObjectLink(home), "home_Base4E");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw AiSpecialWithdrawalMissing("home_Object_pointer");
        if (!AiSpecialWithdrawalNeed(current.ReadObjectNativeValidity(obj), "home_Object_validity")) return 0;
        int raw = AiSpecialWithdrawalNeed(current.ReadObjectHexIndex(obj), "home_Object10");
        return raw < 48400 ? raw : 0; //22D640 returns the actual Hex0 pointer.
    }

    private static int ReadOriginalAiSpecialWithdrawalParameter(GameState state, string name)
    {
        string key = name == "relation-threshold" ? "original-ai.special.relation-threshold"
            : "original-ai.special.withdrawal." + name;
        var parameters = state.NativeArmySimulation?.Parameters ?? throw AiSpecialWithdrawalMissing("parameters");
        if (parameters.Float32Values.ContainsKey(key) || !parameters.Int32Values.TryGetValue(key, out int value))
            throw AiSpecialWithdrawalMissing("parameter." + name);
        return value;
    }
    private static T AiSpecialWithdrawalNeed<T>(T? value, string field) where T : struct =>
        value ?? throw AiSpecialWithdrawalMissing(field);
    private static InvalidDataException AiSpecialWithdrawalMissing(string field) => new("original_ai.special.withdrawal." + field);
}
