using San14.Core;

namespace San14.Data;

public sealed record GateCaptureCityAssignment(int NewForcePhysical,
    DistrictReference CapturedOldDistrict, int? SelectedCityPhysical,
    AreaCityAssignmentResult? AreaAssignment);
public sealed record GateCaptureHexTransfer(int[] VisitedHexes, int[] ClearedHexes16,
    int[] TransferredHexes, int[] DirtyAreas);

/// <summary>Operation-local ordinary Gate leaves of TC26D2E0/270880 and
/// 26DA10/2DF9F0/185D00. The caller owns the capture transaction, resource
/// stages, three2E9140 calls and publication. Unknown reached owners throw;
/// no partial context is a public game state. Native presentation receivers
/// are absent in this numeric campaign; current territory and pool effects
/// retain their real source order.</summary>
public static class GateCaptureTerritorySource
{
    public static GateCaptureCityAssignment AssignGateCity(
        SourceWorldContext current, BaseReference gate,
        int newDistrictPhysical, int commanderPhysical)
    {
        CheckGate(current, gate);
        if (newDistrictPhysical is < 1 or > 51) throw Missing("branch.nonordinary_new_District");
        var district = current.ReadDistrictTableReference(newDistrictPhysical) ?? throw Missing("new_District_pointer");
        int newForce = K(current.ReadDistrictForceSlot(district), "new_District10");
        CheckForce(newForce);
        var area = CurrentGateMapQueries.FindArea(gate, current);
        var oldDistrict = current.ReadBaseDistrictReference(gate) ?? throw Missing("saved_Gate_virtual80");
        int originalCitySlot = K(current.ReadAreaValidityCode(area), "Area34");
        var originalCity = current.ReadCityTableReference(originalCitySlot <= 51 ? originalCitySlot : 0)
            ?? throw Missing("original_City_pointer");
        int districtIdentity = K(current.ReadDistrictPhysicalSlot(district), "new_District_identity");
        int? selectedCity = null;
        if (K(current.ReadBaseDistrictSlot(originalCity), "original_City_virtual78") == districtIdentity)
            selectedCity = K(originalCity.PhysicalSlot, "original_City22DC80");
        else if (K(current.ReadPersonNativeValidity(commanderPhysical), "Commander_validity"))
        {
            var home = BaseLookup.Find(K(current.ReadPersonFirstLocation(commanderPhysical), "Commander_HomeE2"), current);
            int raw = K(current.ReadBaseObjectLink(home), "Commander_Home4E");
            if (raw > 51)
                raw = K(CurrentGateMapQueries.FindCity(home, current).PhysicalSlot, "Commander_HomeGate_City22DC80");
            if (raw is >= 1 and <= 51) selectedCity = raw;
        }
        AreaCityAssignmentResult? assignment = null;
        if (selectedCity.HasValue)
        {
            //270880 only runs29C430 on changed owner-City. This is the
            //actual CURRENT Home iterator, after preceding personnel moves.
            if (K(current.ReadAreaCitySlot(area), "current_Area35") != selectedCity.Value)
                EnsureHomeEmpty(current, gate);
            assignment = current.AssignAreaCity(CurrentGateMapQueries.FindArea(gate, current), selectedCity.Value);
        }
        //26D2E0 param5=1: allocate/append/scan/free even for an empty Home.
        //A real failed append suppresses the scan, as the source does.
        var affected = current.CreateNearbyTemporary();
        try
        {
            if (current.AppendCaptureBaseTemporary(affected, gate)) EnsureHomeEmpty(current, gate);
        }
        finally { current.ClearNearbyTemporary(affected, true); }
        return new(newForce, oldDistrict, selectedCity, assignment);
    }

    public static GateCaptureHexTransfer ApplyFootprintBeforeResources(
        GameState state, SourceWorldContext current, BaseReference gate, int newForce)
    {
        CheckGate(current, gate); CheckForce(newForce);
        var visited = new List<int>(); var changed = new List<int>(); var dirty = new List<int>();
        foreach (var hex in Footprint(current, gate))
        {
            visited.Add(Physical(hex));
            _ = Apply185D00(state, current, hex, newForce, changed, dirty);
            //26DF31 is unconditional, including a rejected185D00 wrapper.
            ApplyRawOwnership(current, hex, newForce, changed);
        }
        return new(visited.ToArray(), [], changed.ToArray(), dirty.ToArray());
    }

    public static GateCaptureHexTransfer ApplyTerritoryAfterResources(
        GameState state, SourceWorldContext current, BaseReference gate,
        int newForce, int oldDistrict, int newDistrict)
    {
        CheckGate(current, gate); CheckForce(newForce);
        //Capture's ordinary source contract has both presentation flags1;
        //their messages have no original native receiver in this client.
        _ = oldDistrict; _ = newDistrict;
        var center = CurrentGateMapQueries.FindHex(gate, current);
        int radius = K(BaseCaptureResourceInputs.ReadCurrent(state, SimulationParameterKeys.BaseCapture.CentralTerritoryRadius), "current_Gate_radiusA7A8");
        var visited = new List<int>(); var cleared = new List<int>();
        var changed = new List<int>(); var dirty = new List<int>();
        var temporary = current.CreateNearbyTemporary(); //2DFA7B, before advice query.
        try
        {
            ReadEagerAdvice2B44E0(state, current);
            for (int distance = 0; distance <= radius; distance++)
            {
                current.ClearNearbyTemporary(temporary, false);
                if (distance == 0) current.AppendNearbyHexTemporary(temporary, center);
                else _ = NearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, distance, temporary);
                var node = current.ReadHexListHead(temporary) ?? throw Missing("territory_Hex_head");
                while (node.Identity.HasValue)
                {
                    var hex = current.ReadHexListNodePayload(node) ?? throw Missing("territory_Hex_payload");
                    int physical = Physical(hex);
                    visited.Add(physical);
                    //18C430 is a native map display dirty notification after
                    //the saved numerical word16 store, including blocked cells.
                    current.SetCaptureHexWord16(physical, 0); cleared.Add(physical);
                    if (K(current.ReadHexAreaSlot(hex), "candidate_Hex12") == K(current.ReadHexAreaSlot(center), "center_Hex12") &&
                        K(current.ReadHexForceSlot(hex), "candidate_Hex14") != newForce &&
                        Eligible(state, current, hex, newForce, requireNoArmy: true))
                        _ = Apply185D00(state, current, hex, newForce, changed, dirty);
                    node = current.ReadHexListNodeNext(node) ?? throw Missing("territory_Hex_next");
                }
            }
        }
        finally { current.ClearNearbyTemporary(temporary, true); }
        //26E605: final footprint is raw2413D0, after releasing the ring list.
        foreach (var hex in Footprint(current, gate))
        { visited.Add(Physical(hex)); ApplyRawOwnership(current, hex, newForce, changed); }
        return new(visited.ToArray(), cleared.ToArray(), changed.ToArray(), dirty.ToArray());
    }

    private static bool Apply185D00(GameState state, SourceWorldContext current,
        HexReference hex, int newForce, List<int> changed, List<int> dirty)
    {
        if (!hex.PhysicalSlot.HasValue || !Eligible(state, current, hex, newForce, requireNoArmy: false)) return false;
        if (K(current.ReadHexForceSlot(hex), "wrapper_Hex14") == newForce) return false;
        //Both actual callers have cityarg0 and overridearg0: no Army order
        //callbacks, Area35 special owner assignment, or type3 override here.
        ApplyRawOwnership(current, hex, newForce, changed);
        var mode = current.ArmyReturnTransferMode ?? throw Missing("wrapper_570850_mode");
        if (mode.Present && K(mode.Value, "wrapper_570850_value") != 0)
            throw Missing("branch.185D00_transfer_mode_counter");
        dirty.Add(K(current.ReadHexAreaSlot(hex), "wrapper_dirty_Area12"));
        return true;
    }

    private static bool Eligible(GameState state, SourceWorldContext current,
        HexReference candidate, int newForce, bool requireNoArmy)
    {
        int identity = Physical(candidate);
        //2DF9F0 reindexes the actual physical identity before terrain/object
        //tests;185D00 does the same through2C6AD0.
        var hex = current.ReadHexTableReference(identity) ?? throw Missing("eligibility_Hex_pointer");
        if (!hex.PhysicalSlot.HasValue) return false;
        byte surface = K(current.ReadHexSurfaceCode(hex), "eligibility_Hex10");
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0) ?? throw Missing("Terrain_pointer");
        if (!terrain.PhysicalSlot.HasValue || K(current.ReadSurfaceTypeRawField2A(terrain), "Terrain2A") == 0) return false;
        if (requireNoArmy && unchecked((short)K(current.ReadHexRaw1C(hex), "eligibility_Hex1C")) is >= 1 and <= 500) return false;
        ushort objectSlot = K(current.ReadHexObjectLink(hex), "eligibility_Hex1A");
        var obj = current.ReadObjectTableReference(objectSlot <= 3000 ? objectSlot : 0) ?? throw Missing("Object_pointer");
        int objectPhysical = K(obj.PhysicalSlot, "Object_physical");
        if (objectPhysical is >= 52 and <= 61)
            return K(current.ReadObjectHexIndex(obj), "Gate_Object10") == identity &&
                !K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, newForce,
                    K(current.ReadHexForceSlot(hex), "Gate_Hex14")), "Gate_relation2CA650");
        byte kind = current.ReadPhysicalObject(objectPhysical)?.RawField12 ?? throw Missing("Object12");
        var definition = current.ReadObjectDefinitionReference(kind <= 30 ? kind : 0) ?? throw Missing("Object_definition_pointer");
        return !K(current.ReadObjectDefinitionValidity(definition), "Object_definition_validity") ||
            K(current.ReadObjectDefinitionRaw2C(definition), "Object_definition2C") == 4;
    }

    private static void ApplyRawOwnership(SourceWorldContext current, HexReference hex,
        int force, List<int> changed)
    {
        byte before = K(current.ReadHexForceSlot(hex), "raw_Hex14");
        current.AssignHexForce(hex, force);
        if (before != K(current.ReadHexForceSlot(hex), "written_Hex14")) changed.Add(Physical(hex));
    }

    private static void ReadEagerAdvice2B44E0(GameState state, SourceWorldContext current)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("advice_World3A");
        int playerPhysical = K(current.ReadForcePhysicalSlot(player), "advice_Player_identity");
        var primary = current.ReadDistrictTableReference(0) ?? throw Missing("advice_District0");
        foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("advice_District_D0"))
        {
            var district = entry ?? throw Missing("advice_District_payload");
            int physical = K(district.PhysicalSlot, "advice_District_pointer");
            if (playerPhysical is < 1 or > 51 || K(current.ReadDistrictForceId(physical), "advice_District10") != playerPhysical) continue;
            ushort ruler = K(current.ReadForceRecord(playerPhysical)?.RawRuler10, "advice_Force10");
            if (K(current.ReadDistrictLeader(physical), "advice_District12") == ruler ||
                K(current.ReadDistrictNumber(physical), "advice_District11") == 1)
            { primary = district; break; }
        }
        if (!K(current.ReadDistrictNativeValidity(primary), "advice_District_validity")) return;
        if (!K(current.ReadForceNativeValidity(player), "advice_Player_validity")) return; //233880.
        int districtPhysical = K(primary.PhysicalSlot, "advice_primary_pointer");
        int force = K(current.ReadDistrictForceId(districtPhysical), "advice_primary_Force10");
        if (force != playerPhysical) return; //Native World9DF8 only feeds absent presentation.
        int advisorId = K(current.ReadAssignmentForceAdvisor52(force <= 51 ? force : 0), "advice_Force52");
        var advisor = advisorId <= 6000 ? current.ReadPersonLookupReference(advisorId) : current.ReadTransferPersonTableReference(0);
        int person = (advisor ?? throw Missing("advice_Person_pointer")).PhysicalSlot ?? throw Missing("advice_Person_null");
        if (!K(current.ReadPersonNativeValidity(person), "advice_Person_validity"))
            throw Missing("branch.2B44E0_adviser_reselection_24B130");
    }

    private static IEnumerable<HexReference> Footprint(SourceWorldContext current, BaseReference gate)
    {
        var handle = current.ReadBaseHexListHandle(gate) ?? throw Missing("footprint_descriptor");
        var node = current.ReadHexListHead(handle) ?? throw Missing("footprint_head");
        while (node.Identity.HasValue)
        {
            yield return current.ReadHexListNodePayload(node) ?? throw Missing("footprint_payload");
            node = current.ReadHexListNodeNext(node) ?? throw Missing("footprint_next");
        }
    }
    private static void EnsureHomeEmpty(SourceWorldContext current, BaseReference gate)
    {
        int location = K(current.ReadBaseObjectLink(gate), "Home_Base4E");
        if (location is < 1 or > 61) return;
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Home_manager100"))
        {
            int person = entry ?? throw Missing("Home_Person_pointer");
            if (K(current.ReadPersonFirstLocation(person), "Home_PersonE2") != location) continue;
            if (K(current.ReadPersonStatus(person), "Home_PersonE6") is >= 1 and <= 4)
                throw Missing("branch.270880_or26D2E0_nonempty_current_Home");
        }
    }
    private static void CheckGate(SourceWorldContext current, BaseReference gate)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(gate);
        if (gate.Kind != BaseKind.Gate || gate.PhysicalSlot is not (>= 1 and <= 10) ||
            K(current.ReadBaseObjectLink(gate), "Gate4E") is not (>= 52 and <= 61))
            throw Missing("branch.ordinary_Gate_receiver");
    }
    private static void CheckForce(int force)
    { if (force is < 1 or > 45) throw Missing("branch.nonordinary_new_Force"); }
    private static int Physical(HexReference hex) => K(hex.PhysicalSlot, "Hex_identity");
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.Gate_capture_territory." + field);
}
