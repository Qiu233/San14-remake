using San14.Core;

namespace San14.Data;

public sealed record CityCaptureAreaScope(BaseReference City, int OldForcePhysical,
    int NewForcePhysical, DistrictReference CapturedOldDistrict, HexListHandle Areas);
public sealed record CityCaptureDistrictAssignment(int NewForcePhysical,
    DistrictReference CapturedOldDistrict, int[] ReassignedGatePhysicalSlots);
public sealed record CityCaptureAdministrationReceipt(int[] AreaPhysicalSlots,
    PersonAreaAssignmentRemovalResult[] AssignmentRemovals);
public sealed record CityCaptureAreaReceipt(int AreaPhysical, AreaCityAssignmentResult[] Assignments,
    GateCaptureHexTransfer? Territory);
public sealed record CityCaptureTerritoryReceipt(int AreaRadius, GateCaptureHexTransfer CenterTerritory,
    CityCaptureAreaReceipt[] Areas);
public sealed record CityCaptureStateTransition(int StatePhysical, int AdvantagePhysical,
    int RequiredCities, int NewOwnedCities, int OldOwnedCities, bool Gained, bool Lost, byte[] Effects);

/// <summary>Ordinary non-last City operation leaves. Holds actual affected
/// Area temporary across the caller's District/resource steps, and reads
/// current native City/Area/Hex relationships, never Core City.AreaIds as a
/// takeover list. Whole2851B0,3x2E9140,resources/reward and publication remain
/// the caller's transaction. Reached Gate reassignment/Emperor/presentation
/// services outside a finite leaf throw concrete unknowns.</summary>
public static class CityCaptureTerritorySource
{
    public static CityCaptureAreaScope CaptureRelatedAreas(SourceWorldContext current,
        BaseReference city, int newForce)
    {
        int cityId = CheckCity(current, city); CheckForce(newForce);
        int oldForce = K(current.ReadBaseForceSlot(city), "saved_City_B0");
        var oldDistrict = current.ReadBaseDistrictReference(city) ?? throw Missing("saved_City_virtual80");
        var temporary = current.CreateNearbyTemporary();
        try
        {
            foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw Missing("related_Area_managerB0"))
            {
                var area = entry ?? throw Missing("related_Area_pointer");
                byte original = K(current.ReadAreaValidityCode(area), "related_Area34");
                if (original == 0) continue; //Source raw34!=0, not native validity1..51.
                byte associated = K(current.ReadAreaCitySlot(area), "related_Area35");
                bool oldMatch = associated == cityId && K(current.ReadAreaCurrentForceSlot(area), "related_old_Area_Hex14") == oldForce;
                bool newMatch = original == cityId && K(current.ReadAreaCurrentForceSlot(area), "related_new_Area_Hex14") == newForce;
                if (!oldMatch && !newMatch) continue;
                var node = current.AllocateRegistryNode(new(temporary.Identity))!;
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Area, area.PhysicalSlot));
            }
            return new(city, oldForce, newForce, oldDistrict, temporary);
        }
        catch { current.ClearNearbyTemporary(temporary, true); throw; }
    }
    public static void ReleaseRelatedAreas(SourceWorldContext current, CityCaptureAreaScope scope)
        => current.ClearNearbyTemporary(scope.Areas, true);

    public static CityCaptureDistrictAssignment AssignCityDistrict(SourceWorldContext current,
        BaseReference city, int newDistrictPhysical)
    {
        int cityId = CheckCity(current, city);
        if (newDistrictPhysical is < 1 or > 51) throw Missing("branch.new_District_outside_ordinary_scope");
        var district = current.ReadDistrictTableReference(newDistrictPhysical) ?? throw Missing("new_District_pointer");
        int newForce = K(current.ReadDistrictForceSlot(district), "new_District10"); CheckForce(newForce);
        var oldDistrict = current.ReadBaseDistrictReference(city) ?? throw Missing("saved_City_virtual80");
        current.SetCityDistrictSlot(cityId, (byte)newDistrictPhysical); //240D90; Governor2E is untouched.
        var affected = current.CreateNearbyTemporary();
        try
        {
            AppendBase(current, affected, city);
            var gates = current.CreateNearbyTemporary();
            try
            {
                foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw Missing("associated_Gate_managerA0"))
                {
                    var gate = entry ?? throw Missing("associated_Gate_pointer");
                    if (CurrentGateMapQueries.FindCity(gate, current) == city) AppendBase(current, gates, gate);
                }
                foreach (var gate in Bases(current, gates))
                    if (K(current.ReadBaseForceSlot(city), "current_City_B0") == K(current.ReadBaseForceSlot(gate), "associated_Gate_B0"))
                        AppendBase(current, affected, gate);
            }
            finally { current.ClearNearbyTemporary(gates, true); }
            foreach (var at in Bases(current, affected)) EnsureHomeEmpty(current, at);
        }
        finally { current.ClearNearbyTemporary(affected, true); }
        var reassigned = new List<int>();
        if (K(current.ReadDistrictRawByte(newDistrictPhysical, 0x11), "new_District11") == 1)
            foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw Missing("primary_Gate_managerA0"))
            {
                var gate = entry ?? throw Missing("primary_Gate_pointer");
                var area = CurrentGateMapQueries.FindArea(gate, current);
                if (K(current.ReadAreaValidityCode(area), "primary_Gate_Area34") != cityId ||
                    K(current.ReadBaseDistrictSlot(gate), "primary_Gate78") != newDistrictPhysical) continue;
                if (CurrentGateMapQueries.FindCity(gate, current).PhysicalSlot == cityId) continue;
                EnsureHomeEmpty(current, gate);
                _ = current.AssignAreaCity(CurrentGateMapQueries.FindArea(gate, current), cityId);
                reassigned.Add(K(gate.PhysicalSlot, "primary_Gate_identity"));
            }
        return new(newForce, oldDistrict, reassigned.ToArray()); //Caller performs first2E9140 now.
    }

    public static CityCaptureAdministrationReceipt ClearCityAdministration(
        SourceWorldContext current, BaseReference city)
    {
        int cityId = CheckCity(current, city);
        current.SetAssignmentCityCA(cityId, 0); current.SetAssignmentCityCC(cityId, 0); current.SetCaptureCityByteCF(cityId, 0);
        var cleared = new List<int>(); var removals = new List<PersonAreaAssignmentRemovalResult>();
        foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw Missing("administration_Area_managerB0"))
        {
            var area = entry ?? throw Missing("administration_Area_pointer");
            if (K(current.ReadAreaCitySlot(area), "administration_Area35") != cityId) continue;
            int physical = K(area.PhysicalSlot, "administration_Area_identity");
            ushort old = K(current.ReadAssignmentArea36(physical), "administration_Area36");
            if (old != 0)
            {
                current.SetAssignmentArea36(physical, 0);
                if (unchecked((short)old) < 0) throw Missing("branch.signed_Area36_person_lookup");
                var removed = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, physical, old);
                if (removed.UnresolvedOwner is not null) throw Missing(removed.UnresolvedOwner);
                removals.Add(removed);
            }
            current.SetAssignmentArea72(physical, 0); current.SetAssignmentArea6D(physical, 0); cleared.Add(physical);
        }
        return new(cleared.ToArray(), removals.ToArray());
    }

    public static GateCaptureHexTransfer ApplyFootprintBeforeResources(GameState state,
        SourceWorldContext current, BaseReference city, int newForce)
    {
        _ = CheckCity(current, city); CheckForce(newForce);
        var visited = new List<int>(); var changed = new List<int>(); var dirty = new List<int>();
        foreach (var hex in Footprint(current, city))
        { visited.Add(Physical(hex)); Apply185D00(state, current, hex, newForce, changed, dirty); Raw(current, hex, newForce, changed); }
        return new(visited.ToArray(), [], changed.ToArray(), dirty.ToArray());
    }

    public static CityCaptureTerritoryReceipt ApplyTerritoryAfterResources(GameState state,
        SourceWorldContext current, CityCaptureAreaScope scope, int newDistrictPhysical,
        IEffectiveOfficerAbilities abilities)
    {
        _ = CheckCity(current, scope.City); CheckForce(scope.NewForcePhysical);
        var center = CurrentGateMapQueries.FindHex(scope.City, current);
        var central = Rings(state, current, center, unchecked(K(BaseCaptureResourceInputs.ReadCurrent(state, SimulationParameterKeys.BaseCapture.CentralTerritoryRadius), "radiusA7A8") + 1), scope.NewForcePhysical);
        var visited = central.VisitedHexes.ToList(); var changed = central.TransferredHexes.ToList();
        foreach (var hex in Footprint(current, scope.City)) { visited.Add(Physical(hex)); Raw(current, hex, scope.NewForcePhysical, changed); }
        central = central with { VisitedHexes = visited.ToArray(), TransferredHexes = changed.ToArray() };
        int areaRadius = ReadAreaRadius(state, current, newDistrictPhysical, scope.NewForcePhysical, abilities);
        var receipts = new List<CityCaptureAreaReceipt>();
        var districts = current.CreateNearbyTemporary(); //26E775, held through all related Areas.
        try
        {
            foreach (var area in Areas(current, scope.Areas))
            {
                int areaId = K(area.PhysicalSlot, "transfer_Area_identity");
                var representative = AreaHex(current, area);
                if (K(current.ReadHexSurfaceCode(representative), "transfer_Area_Hex10") == 17)
                    throw Missing($"branch.related_Gate_Area[{areaId}]_26E83B_26F3E0");
                var assignments = new List<AreaCityAssignmentResult>
                    { current.AssignAreaCity(area, K(scope.City.PhysicalSlot, "captured_City22DC80")) };
                int original = K(current.ReadAreaValidityCode(area), "transfer_Area34");
                var originalCity = current.ReadCityTableReference(original <= 51 ? original : 0) ?? throw Missing("transfer_original_City_pointer");
                if (K(current.ReadBaseNativeValidity(originalCity), "transfer_original_City_validity") &&
                    K(current.ReadBaseForceSlot(originalCity), "transfer_original_City_B0") == scope.NewForcePhysical)
                    assignments.Add(current.AssignAreaCity(area, K(originalCity.PhysicalSlot, "transfer_original_City22DC80")));
                GateCaptureHexTransfer? territory = scope.OldForcePhysical == 0 ? null :
                    Rings(state, current, AreaHex(current, area), areaRadius, scope.NewForcePhysical);
                receipts.Add(new(areaId, assignments.ToArray(), territory));
            }
        }
        finally { current.ClearNearbyTemporary(districts, true); }
        return new(areaRadius, central, receipts.ToArray());
    }

    public static bool RequireEmperorCaptureNotReached(GameState state, SourceWorldContext current,
        CityCaptureAreaScope scope)
    {
        if (scope.OldForcePhysical == scope.NewForcePhysical) return false;
        var force = current.ReadForceTableReference(scope.NewForcePhysical) ?? throw Missing("Emperor_new_Force_pointer");
        if (!K(current.ReadForceNativeValidity(force), "Emperor_new_Force_validity")) return false;
        ushort emperor = K(CityCaptureTerritoryInputs.ReadEmperor1654(state), "Emperor_current_World1654");
        if (emperor == 0) return false;
        var person = emperor <= 6000 ? current.ReadPersonLookupReference(emperor) : current.ReadTransferPersonTableReference(0);
        int physical = (person ?? throw Missing("Emperor_Person_pointer")).PhysicalSlot ?? throw Missing("Emperor_Person_null");
        var position = PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(physical), "Emperor_PersonE4"));
        if (position.Kind != 5 || position.Index != scope.City.PhysicalSlot) return false;
        throw Missing("branch.2F6890_Emperor_in_captured_City");
    }

    public static bool? ApplyCityFoodWarning(GameState state, SourceWorldContext current,
        BaseReference city, Func<int?>? foodExpense22FE10 = null, Func<int?>? predictedFoodIncome22D9E0 = null)
    {
        int cityId = CheckCity(current, city);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("food_World3A");
        if (!K(current.ReadForceNativeValidity(player), "food_Player_validity") || !K(current.ReadBaseNativeValidity(city), "food_City_validity")) return null;
        if (K(current.ReadBaseForceSlot(city), "food_City_B0") != K(current.ReadForcePhysicalSlot(player), "food_Player_identity"))
        { current.SetCaptureCityFoodWarningBit1(cityId, false); return false; }
        int expense = K(foodExpense22FE10?.Invoke(), "food_actual_22FE10_current_foodExpense");
        int food = K(current.ReadBaseResource(city, BaseResourceKind.Supplies), "food_City_virtualA0");
        bool warning = false;
        if (unchecked(food - expense * 3) < 1)
        {
            int income = K(predictedFoodIncome22D9E0?.Invoke(), "food_actual_22D9E0_predicted_income");
            warning = unchecked(K(current.ReadBaseResource(city, BaseResourceKind.Supplies), "food_current_City_virtualA0") + income - expense * 3) < 1;
        }
        current.SetCaptureCityFoodWarningBit1(cityId, warning); return warning;
    }

    public static int[] ClearTargetRouting(SourceWorldContext current, BaseReference city)
    {
        int cityLocation = K(current.ReadBaseObjectLink(city), "routing_City4E"); _ = CheckCity(current, city);
        var result = new List<int>();
        foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("routing_managerD0"))
        {
            int physical = (entry ?? throw Missing("routing_District_pointer")).PhysicalSlot ?? throw Missing("routing_District_null");
            byte kind = K(current.ReadDistrictRawByte(physical, 0x19), "routing_District19");
            if (kind is not (2 or 5)) continue; //kind6 maps52..61 or0, never valid City4E.
            byte target = K(current.ReadDistrictRawByte(physical, 0x18), "routing_District18");
            if ((kind == 5 && target > 51 ? 0 : target) != cityLocation) continue;
            current.SetDistrictRawByte(physical, 0x17, 1); current.SetDistrictRawByte(physical, 0x19, 0xFF);
            current.SetDistrictRawByte(physical, 0x1A, 0); current.SetDistrictRawByte(physical, 0x1B, 0xFF);
            byte high = K(current.ReadDistrictRawByte(physical, 0x1F), "routing_District1C_high");
            for (int at = 0x1C; at < 0x1F; at++) current.SetDistrictRawByte(physical, at, 0xFF);
            current.SetDistrictRawByte(physical, 0x1F, (byte)(high | 0x7F));
            current.SetDistrictRawByte(physical, 0x20, 0); current.SetDistrictRawByte(physical, 0x21, 0);
            current.SetDistrictRawByte(physical, 0x22, 0); current.SetDistrictRawByte(physical, 0x18, 0); result.Add(physical);
        }
        return result.ToArray();
    }

    /// <summary>26F452's numeric294FD0 call admission. With no native type4
    /// candidates the pending F0 query cannot change any result. A reached
    /// type4 candidate requires the actual current2336B0 membership; no
    /// generic Core IsActive substitutes for that list.</summary>
    public static int RequireNoReachedAreaObjectService(SourceWorldContext current,
        CityCaptureAreaScope scope, Func<ObjectReference, bool?>? pending2336B0 = null)
    {
        int inspected = 0;
        foreach (var entry in current.ReadRegisteredObjectReferences() ?? throw Missing("Object_managerE0"))
        {
            var obj = entry ?? throw Missing("Object_E0_pointer"); inspected++;
            bool? excluded = pending2336B0?.Invoke(obj);
            if (excluded == true) continue;
            int physical = K(obj.PhysicalSlot, "Object_E0_identity");
            byte kind = current.ReadPhysicalObject(physical)?.RawField12 ?? throw Missing("Object12");
            var definition = current.ReadObjectDefinitionReference(kind <= 30 ? kind : 0) ?? throw Missing("Object_definition");
            if (!K(current.ReadObjectDefinitionValidity(definition), "Object_definition_validity") ||
                K(current.ReadObjectDefinitionRaw2C(definition), "Object_definition2C") != 4) continue;
            if (!K(excluded, "Object_current_F0_pending_2336B0"))
            {
                ushort location = K(current.ReadObjectHexIndex(obj), "Object10");
                var hex = current.ReadHexTableReference(location <= 48399 ? location : 0) ?? throw Missing("Object_Hex_pointer");
                int areaId = K(current.ReadHexAreaSlot(hex), "Object_Hex12");
                var area = current.ReadAreaTableReference(areaId <= 500 ? areaId : 0) ?? throw Missing("Object_Area_pointer");
                bool inScope = Areas(current, scope.Areas).Any(at => at == area);
                if (inScope && K(current.ReadObjectForceSlot13(obj), "Object13") != scope.NewForcePhysical)
                    throw Missing($"branch.related_Area_Object[{physical}]_294FD0");
            }
        }
        return inspected;
    }

    /// <summary>Mode0 City tail's State threshold, on the operation's current
    /// City Districts after territory transfer. Pure display without a native
    /// receiver has no numeric writer. Reached kind1/2/5 services remain
    /// explicit until their actual saved/current leaf is composed.</summary>
    public static CityCaptureStateTransition? RequireNoSpecialStateAdvantageTransition(GameState state,
        SourceWorldContext current, CityCaptureAreaScope scope)
    {
        _ = CheckCity(current, scope.City);
        int mode = G(PersonGrowthCurrentQueries.WorldMode40Key);
        if (mode is 2 or 4) return null;
        int stateRaw = G(PersonGrowthCurrentQueries.CityStateKey(K(scope.City.PhysicalSlot, "State_City_identity")));
        var province = current.ReadStaticTableReference(0xDC80, stateRaw <= 22 ? stateRaw : 0) ?? throw Missing("State_CityCE_pointer");
        int stateId = K(province.PhysicalSlot, "State_identity");
        if (stateId == 0) return null;
        int advantageRaw = G(PersonGrowthCurrentQueries.StateAdvantageKey(stateId));
        var advantage = current.ReadStaticTableReference(0x85990, advantageRaw <= 17 ? advantageRaw : 0) ?? throw Missing("State_Advantage30_pointer");
        int advantageId = K(advantage.PhysicalSlot, "State_Advantage_identity");
        if (advantageId == 0) return null;
        int required = G(PersonGrowthCurrentQueries.RequirementKey(advantageId));
        byte[] effects = [B(PersonGrowthCurrentQueries.EffectKey(advantageId, 0)), B(PersonGrowthCurrentQueries.EffectKey(advantageId, 1))];
        int newCount = Count(scope.NewForcePhysical), oldCount = Count(scope.OldForcePhysical);
        bool gained = newCount == required, lost = unchecked(oldCount + 1) == required;
        var result = new CityCaptureStateTransition(stateId, advantageId, required, newCount, oldCount, gained, lost, effects);
        if (gained) AdviceForForce(current, scope.NewForcePhysical);
        if (lost)
        {
            var old = current.ReadForceTableReference(scope.OldForcePhysical <= 51 ? scope.OldForcePhysical : 0) ?? throw Missing("State_old_Force_pointer");
            if (K(current.ReadForceNativeValidity(old), "State_old_Force_validity")) AdviceForForce(current, scope.OldForcePhysical);
        }
        if (gained || lost)
            foreach (byte effect in effects)
                if (effect is 1 or 2 or 5)
                    throw Missing($"branch.State_advantage[{stateId}]_effect{effect}_26F95B_266D60");
        return result;

        int Count(int forceId)
        {
            var force = current.ReadForceTableReference(forceId is >= 0 and <= 51 ? forceId : 0) ?? throw Missing("State_Force_pointer");
            if (!K(current.ReadForceNativeValidity(force), "State_Force_validity")) return 0;
            int total = 0;
            foreach (var row in current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("State_City_manager90"))
            {
                int id = (row ?? throw Missing("State_City_pointer")).PhysicalSlot ?? throw Missing("State_City_null");
                var city = new BaseReference(BaseKind.City, id);
                if (!K(current.ReadBaseNativeValidity(city), "State_City_validity") || G(PersonGrowthCurrentQueries.CityStateKey(id)) != stateId) continue;
                if (K(current.ReadBaseForceSlot(city), "State_current_CityB0") == forceId) total++;
            }
            return total;
        }
        int G(string key)
        {
            var bank = state.NativeArmySimulation?.Parameters;
            if (bank?.Int32Values.GetValueOrDefault(PersonGrowthCurrentQueries.VersionKey) != 1 || !bank.Int32Values.TryGetValue(key, out int value))
                throw Missing("State_saved_current." + key);
            return value;
        }
        byte B(string key) { int value = G(key); return value is >= 0 and <= 255 ? (byte)value : throw Missing("State_byte." + key); }
    }

    private static int ReadAreaRadius(GameState state, SourceWorldContext current, int districtPhysical,
        int newForce, IEffectiveOfficerAbilities abilities)
    {
        var district = current.ReadDistrictTableReference(districtPhysical) ?? throw Missing("area_radius_District_pointer");
        var force = current.ReadForceTableReference(newForce) ?? throw Missing("area_radius_Force_pointer");
        if (!K(current.ReadDistrictNativeValidity(district), "area_radius_District_validity") || !K(current.ReadForceNativeValidity(force), "area_radius_Force_validity")) return 1;
        int rank = K(current.ReadMoraleWaveForceRank47(newForce), "area_radius_current_Force47");
        int rankAC = K(OwnedArmyMoraleWaveInputs.ReadRankAC(state, rank <= 12 ? rank : 0), "area_radius_current_RankAC");
        ushort leader = K(current.ReadDistrictLeader(districtPhysical), "area_radius_current_District12");
        int charm = K(abilities.Resolve(state, leader <= 6000 ? leader : 0)?.Charm, "area_radius_full_Leader_charm4");
        int delta = Math.Max(unchecked(charm - P(SimulationParameterKeys.CityCapture.LeaderCharmRadiusThreshold)), 0);
        int radius = unchecked(Divide(10 - rankAC, P(SimulationParameterKeys.CityCapture.RankRadiusDivisor)) + Divide(unchecked(delta * delta), P(SimulationParameterKeys.CityCapture.SquaredCharmRadiusDivisor)));
        return Math.Min(radius, P(SimulationParameterKeys.CityCapture.MaximumCaptureAreaRadius));
        int P(string key) => K(CityCaptureTerritoryInputs.ReadCurrent(state, key), $"area_radius_scalar{key}");
    }
    private static int Divide(int numerator, int denominator)
    { if (denominator == 0 || numerator == int.MinValue && denominator == -1) throw Missing("source_IDIV"); return numerator / denominator; }

    private static GateCaptureHexTransfer Rings(GameState state, SourceWorldContext current,
        HexReference center, int radius, int newForce)
    {
        var visited = new List<int>(); var cleared = new List<int>(); var changed = new List<int>(); var dirty = new List<int>();
        var temporary = current.CreateNearbyTemporary();
        try
        {
            Advice(state, current); //Actual param6=1 eager query after descriptor allocation.
            for (int distance = 0; distance <= radius; distance++)
            {
                current.ClearNearbyTemporary(temporary, false);
                if (distance == 0) current.AppendNearbyHexTemporary(temporary, center);
                else _ = NearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, distance, temporary);
                var node = current.ReadHexListHead(temporary) ?? throw Missing("ring_head");
                while (node.Identity.HasValue)
                {
                    var hex = current.ReadHexListNodePayload(node) ?? throw Missing("ring_payload");
                    int physical = Physical(hex); visited.Add(physical); current.SetCaptureHexWord16(physical, 0); cleared.Add(physical);
                    if (K(current.ReadHexAreaSlot(hex), "ring_Hex12") == K(current.ReadHexAreaSlot(center), "ring_center12") &&
                        K(current.ReadHexForceSlot(hex), "ring_Hex14") != newForce && Eligible(state, current, hex, newForce, true))
                        Apply185D00(state, current, hex, newForce, changed, dirty);
                    node = current.ReadHexListNodeNext(node) ?? throw Missing("ring_next");
                }
            }
        }
        finally { current.ClearNearbyTemporary(temporary, true); }
        return new(visited.ToArray(), cleared.ToArray(), changed.ToArray(), dirty.ToArray());
    }
    private static void Apply185D00(GameState state, SourceWorldContext current, HexReference hex,
        int force, List<int> changed, List<int> dirty)
    {
        if (!Eligible(state, current, hex, force, false) || K(current.ReadHexForceSlot(hex), "wrapper_Hex14") == force) return;
        Raw(current, hex, force, changed);
        var mode = current.ArmyReturnTransferMode ?? throw Missing("wrapper_570850");
        if (mode.Present && K(mode.Value, "wrapper_570850_value") != 0) throw Missing("branch.wrapper_transfer_mode_counter");
        dirty.Add(K(current.ReadHexAreaSlot(hex), "wrapper_dirty_Area12"));
    }
    private static bool Eligible(GameState state, SourceWorldContext current, HexReference candidate, int force, bool noArmy)
    {
        int physical = Physical(candidate);
        var hex = current.ReadHexTableReference(physical) ?? throw Missing("eligibility_Hex_pointer");
        if (!hex.PhysicalSlot.HasValue) return false;
        int surface = K(current.ReadHexSurfaceCode(hex), "eligibility_Hex10");
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0) ?? throw Missing("eligibility_Terrain_pointer");
        if (!terrain.PhysicalSlot.HasValue || K(current.ReadSurfaceTypeRawField2A(terrain), "eligibility_Terrain2A") == 0) return false;
        if (noArmy && unchecked((short)K(current.ReadHexRaw1C(hex), "eligibility_Hex1C")) is >= 1 and <= 500) return false;
        int link = K(current.ReadHexObjectLink(hex), "eligibility_Hex1A");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw Missing("eligibility_Object_pointer");
        int objPhysical = K(obj.PhysicalSlot, "eligibility_Object_identity");
        if (objPhysical is >= 52 and <= 61)
            return K(current.ReadObjectHexIndex(obj), "eligibility_Gate_Object10") == physical &&
                !K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force, K(current.ReadHexForceSlot(hex), "eligibility_Gate_Hex14")), "eligibility_relation2CA650");
        byte kind = current.ReadPhysicalObject(objPhysical)?.RawField12 ?? throw Missing("eligibility_Object12");
        var definition = current.ReadObjectDefinitionReference(kind <= 30 ? kind : 0) ?? throw Missing("eligibility_Object_definition");
        return !K(current.ReadObjectDefinitionValidity(definition), "eligibility_definition_validity") || K(current.ReadObjectDefinitionRaw2C(definition), "eligibility_definition2C") == 4;
    }
    private static void Raw(SourceWorldContext current, HexReference hex, int force, List<int> changed)
    { int before = K(current.ReadHexForceSlot(hex), "raw_Hex14"); current.AssignHexForce(hex, force); if (before != K(current.ReadHexForceSlot(hex), "written_Hex14")) changed.Add(Physical(hex)); }
    private static void Advice(GameState state, SourceWorldContext current)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("advice_World3A");
        int force = K(current.ReadForcePhysicalSlot(player), "advice_Player_identity");
        AdviceForForce(current, force);
    }
    private static void AdviceForForce(SourceWorldContext current, int force)
    {
        var player = current.ReadForceTableReference(force <= 51 ? force : 0) ?? throw Missing("advice_Force_pointer");
        var primary = current.ReadDistrictTableReference(0) ?? throw Missing("advice_District0");
        foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("advice_D0"))
        {
            var district = entry ?? throw Missing("advice_District_pointer"); int at = K(district.PhysicalSlot, "advice_District_identity");
            if (force is < 1 or > 51 || K(current.ReadDistrictRawByte(at, 0x10), "advice_District10") != force) continue;
            if (K(current.ReadDistrictLeader(at), "advice_District12") == K(current.ReadForceRecord(force)?.RawRuler10, "advice_Force10") || K(current.ReadDistrictRawByte(at, 0x11), "advice_District11") == 1)
            { primary = district; break; }
        }
        if (!K(current.ReadDistrictNativeValidity(primary), "advice_District_validity") || !K(current.ReadForceNativeValidity(player), "advice_Player_validity")) return;
        int primaryId = K(primary.PhysicalSlot, "advice_primary_identity");
        int owner = K(current.ReadDistrictRawByte(primaryId, 0x10), "advice_primary_Force10"); if (owner != force) return;
        int id = K(current.ReadAssignmentForceAdvisor52(owner <= 51 ? owner : 0), "advice_current_Force52");
        var person = id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
        int atPerson = (person ?? throw Missing("advice_Person_pointer")).PhysicalSlot ?? throw Missing("advice_Person_null");
        if (!K(current.ReadPersonNativeValidity(atPerson), "advice_Person_validity")) throw Missing("branch.2B44E0_adviser_reselection_24B130");
    }
    private static IEnumerable<AreaReference> Areas(SourceWorldContext current, HexListHandle handle)
    {
        var node = current.ReadRegistryHead(new(handle.Identity)) ?? throw Missing("Area_temporary_head");
        while (node.Identity.HasValue)
        {
            var payload = current.ReadRegistryNodePayload(node) ?? throw Missing("Area_temporary_payload");
            if (payload.Kind != RegistryKind.Area) throw Missing("Area_temporary_payload_kind");
            yield return new(payload.PhysicalSlot); node = current.ReadRegistryNodeNext(node) ?? throw Missing("Area_temporary_next");
        }
    }
    private static IEnumerable<BaseReference> Bases(SourceWorldContext current, HexListHandle handle)
    {
        var node = current.ReadRegistryHead(new(handle.Identity)) ?? throw Missing("Base_temporary_head");
        while (node.Identity.HasValue)
        { yield return current.ReadBaseRegistryNodePayload(node) ?? throw Missing("Base_temporary_payload"); node = current.ReadRegistryNodeNext(node) ?? throw Missing("Base_temporary_next"); }
    }
    private static void AppendBase(SourceWorldContext current, HexListHandle handle, BaseReference target)
    { _ = current.AppendCaptureBaseTemporary(handle, target); }
    private static IEnumerable<HexReference> Footprint(SourceWorldContext current, BaseReference city)
    {
        var node = current.ReadBaseHexListHead(city) ?? throw Missing("footprint_head");
        while (node.Identity.HasValue)
        { yield return current.ReadHexListNodePayload(node) ?? throw Missing("footprint_payload"); node = current.ReadHexListNodeNext(node) ?? throw Missing("footprint_next"); }
    }
    private static HexReference AreaHex(SourceWorldContext current, AreaReference area)
    { int raw = K(current.ReadAreaHexIndex(area), "Area3A"); return current.ReadHexTableReference(raw <= 48399 ? raw : 0) ?? throw Missing("Area_representative_pointer"); }
    private static void EnsureHomeEmpty(SourceWorldContext current, BaseReference target)
    {
        int location = K(current.ReadBaseObjectLink(target), "Home_Base4E");
        foreach (var entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Home_manager100"))
        {
            int person = entry ?? throw Missing("Home_Person_pointer");
            if (K(current.ReadPersonFirstLocation(person), "Home_PersonE2") == location && K(current.ReadPersonStatus(person), "Home_PersonE6") is >= 1 and <= 4)
                throw Missing("branch.nonempty_current_Home_240DB0_or29C430");
        }
    }
    private static int CheckCity(SourceWorldContext current, BaseReference city)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(city);
        if (city.Kind != BaseKind.City || city.PhysicalSlot is not (>= 1 and <= 51) || K(current.ReadBaseObjectLink(city), "City4E") is not (>= 1 and <= 51))
            throw Missing("branch.ordinary_City_receiver"); return city.PhysicalSlot.Value;
    }
    private static void CheckForce(int force) { if (force is < 1 or > 45) throw Missing("branch.ordinary_Force_scope"); }
    private static int Physical(HexReference hex) => K(hex.PhysicalSlot, "Hex_identity");
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.City_capture_territory." + field);
}
