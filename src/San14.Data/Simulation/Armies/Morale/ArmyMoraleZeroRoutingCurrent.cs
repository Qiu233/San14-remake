using San14.Core;

namespace San14.Data;

internal sealed record ArmyMoraleZeroRoutingResult(GameState? State,
    ArmyMoraleAdjustmentReceipt? Receipt, int? NativeReturn, string? UnresolvedOwner);

/// <summary>2709A0 as called by an Object trap: known-null source Hex,
/// mode1, cause0 and deferred admission. The enclosing operation owns rollback
/// of the current context, pool and any immediate settlement on Unknown.</summary>
internal static class ArmyMoraleZeroRoutingCurrent
{
    internal static ArmyMoraleZeroRoutingResult Apply(GameState state, SourceWorldContext current,
        ArmyUnitReference army, int adjustment, ArmyMobilityCurrentProducer mobility,
        Func<GameState, UnitDefeatRequest, GameState> immediate)
    {
        ArmyMoraleAdjustmentReceipt? receipt = null;
        try
        {
            var scalar = ArmyMoraleNonzeroCurrent.Apply(state, current, army, adjustment);
            receipt = scalar.Receipt;
            if (receipt is null)
                return scalar.NativeReturn is int invalidNative && scalar.UnresolvedOwner is null
                    ? new(state, null, invalidNative, null) : new(null, null, null, scalar.UnresolvedOwner);

            int victim = ArmyDestructionQueue.ResolveUnitId(state, army.PhysicalSlot)
                ?? throw Missing("victim_entity_mapping");
            var image = state.Units.GetValueOrDefault(victim) ?? throw Missing("victim_active_image");
            state = state with { Units = new(state.Units) { [victim] = ArmyMoraleSourceAdapter.WithCurrent(image, receipt.After1A) } };
            if (receipt.RoutingTestValue != 0)
            {
                if (scalar.NativeReturn is not int nonzeroNative || scalar.UnresolvedOwner is not null)
                    throw Missing(scalar.UnresolvedOwner ?? "nonzero_native_return");
                return new(state, receipt, nonzeroNative, null);
            }
            if (scalar.UnresolvedOwner != "branch.2709A0_zero_morale_routing")
                throw Missing(scalar.UnresolvedOwner ?? "zero_routing_prefix");

            Bind(state, current);
            var winner = SelectWinner(state, current, army, mobility);
            //The source service conjunct is false for current WorldBC=-1.
            //A missing or active service owner is not a known-null App.
            if (state.CampaignEventWorld?.CurrentUserIndex != -1)
                throw Missing("service_current_user_statistics");
            var request = new UnitDefeatRequest(victim, winner.Army, winner.Base);
            var queue = state.PendingCombatSettlements ?? throw Missing("pending140_queue");
            var admission = CombatSettlementRules.Admit(queue, request,
                ArmyDestructionQueue.BindCurrent(state, current));
            if (admission.Kind is null) throw Missing(admission.MissingInput ?? "pending140_admission");
            //Capacity is checked before duplicate victims by the domain rule.
            //Full executes this one request and leaves the existing128 intact.
            state = admission.Kind == CombatSettlementAdmissionKind.Full
                ? immediate(state, request) : state with { PendingCombatSettlements = admission.Queue };
            Bind(state, current);
            return new(state, receipt, 1, null);
        }
        catch (PersonArmyCaptureSource.MissingOwner error) { return new(null, receipt, null, error.Message); }
        catch (InvalidDataException error) { return new(null, receipt, null, error.Message); }
    }

    private static (int? Army, SettlementBase? Base) SelectWinner(GameState state,
        SourceWorldContext current, ArmyUnitReference victim, ArmyMobilityCurrentProducer mobility)
    {
        if (current.ReadOwnedCurrentListPool() is null) throw Missing("primary_pool");
        var range = current.CreateNearbyTemporary();
        try
        {
            var threats = current.CreateNearbyTemporary();
            try
            {
                NearbyArmyTemporarySource.FillNearbyHexTemporary(current, VictimHex(), 3, range);
                foreach (var hex in FacilityEffectTargets.ReadHexes(current, range))
                {
                    var selected = Occupant(state, current, hex);
                    if (selected.NativeKind is not (27 or 5 or 6)) continue;
                    var canonical = hex;
                    var obj = ObjectAt(current, hex);
                    if (K(current.ReadObjectNativeValidity(obj), "canonical_Object_validity"))
                    {
                        ushort raw = K(current.ReadObjectHexIndex(obj), "canonical_Object10");
                        canonical = current.ReadHexTableReference(raw < 48400 ? raw : 0)
                            ?? throw Missing("canonical_Hex_pointer");
                    }
                    //Inspect the real chain, including suppressed node appends.
                    if (FacilityEffectTargets.ReadHexes(current, threats).Contains(canonical)) continue;
                    int center = K(current.ReadArmyUnitRawHex2A(victim), "threat_victim2A");
                    int candidate = HexIdentity(current, canonical);
                    RequireIdempotentHex(current, canonical, candidate);
                    if (PersonArmyCaptureSource.Threatens(state, current, candidate, center))
                        current.AppendNearbyHexTemporary(threats, canonical);
                }
                if (K(current.ReadTemporaryHexListCount(threats), "threat_count") == 0) return (null, null);
                var centerHex = VictimHex();
                StableSort(state, current, threats, centerHex, mobility);
                var head = current.ReadHexListHead(threats) ?? throw Missing("winner_head");
                var first = current.ReadHexListNodePayload(head) ?? throw Missing("winner_Hex");
                return WinnerAt(state, current, first);
            }
            finally { current.ClearNearbyTemporary(threats, true); }
        }
        finally { current.ClearNearbyTemporary(range, true); }

        HexReference VictimHex()
        {
            ushort raw = K(current.ReadArmyUnitRawHex2A(victim), "victim2A");
            return current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("victim_Hex_pointer");
        }
    }

    private static void StableSort(GameState state, SourceWorldContext current, HexListHandle list,
        HexReference center, ArmyMobilityCurrentProducer mobility)
    {
        var nodes = new List<HexListNodeReference>();
        var cursor = current.ReadHexListHead(list) ?? throw Missing("sort_head");
        while (cursor.Identity.HasValue)
        {
            nodes.Add(cursor);
            cursor = current.ReadHexListNodeNext(cursor) ?? throw Missing("sort_next");
        }
        //24BD00 moves a node left only on strict253C40 less-than. Keep its
        //comparison sequence and its stable equal-mobility Army ties.
        for (int index = 1; index < nodes.Count; index++)
        {
            var moving = nodes[index];
            int at = index;
            while (at > 0 && Less(Payload(moving), Payload(nodes[at - 1]))) at--;
            if (at == index) continue;
            nodes.RemoveAt(index); nodes.Insert(at, moving);
            current.ReorderCurrentList(list, nodes);
        }

        HexReference Payload(HexListNodeReference node) =>
            current.ReadHexListNodePayload(node) ?? throw Missing("sort_payload");
        bool Less(HexReference first, HexReference second)
        {
            var left = Occupant(state, current, first);
            var right = Occupant(state, current, second);
            if (left.NativeKind != right.NativeKind)
            {
                if (left.NativeKind == 27) return true;
                if (right.NativeKind == 27) return false;
                if (left.NativeKind == 5) return true;
                if (right.NativeKind == 5) return false;
                if (left.NativeKind == 6) return true;
                if (right.NativeKind == 6) return false;
            }
            int leftAt = HexIdentity(current, first);
            int leftCenter = HexIdentity(current, center);
            int leftDistance = ArmyRouteSearch.Distance(leftCenter, leftAt);
            int rightAt = HexIdentity(current, second);
            int rightCenter = HexIdentity(current, center);
            int rightDistance = ArmyRouteSearch.Distance(rightCenter, rightAt);
            if (leftDistance != rightDistance) return leftDistance < rightDistance;
            if (left.NativeKind == 27)
            {
                var a = ArmyPointer(current, left.NativePhysicalSlot);
                var b = ArmyPointer(current, right.NativePhysicalSlot);
                if (K(current.ReadArmyUnitNativeValidity(a), "sort_left_Army_validity") &&
                    K(current.ReadArmyUnitNativeValidity(b), "sort_right_Army_validity"))
                    return Mobility(a) < Mobility(b);
            }
            return left.NativePhysicalSlot < right.NativePhysicalSlot;
        }
        float Mobility(ArmyUnitReference army)
        {
            int physical = K(army.PhysicalSlot, "sort_mobility_Army_pointer");
            var arena = state.ReadArmyLifecycle() ?? throw Missing("sort_mobility_registry");
            var slot = arena.ReceiverSlots()[physical];
            var image = (slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage)
                ?? throw Missing("sort_mobility_Army_image");
            var result = mobility.ResolveCurrent(state, image, current);
            return result.Receipt?.Value ?? throw Missing(result.UnresolvedOwner ?? "sort_mobility2B0660_flags1");
        }
    }

    private static (int? Army, SettlementBase? Base) WinnerAt(GameState state,
        SourceWorldContext current, HexReference hex)
    {
        var obj = ObjectAt(current, hex);
        if (K(current.ReadObjectNativeValidity(obj), "winner_Object_validity"))
        {
            int identity = K(current.ReadObjectPhysicalSlot(obj), "winner_Object_identity");
            if (identity is >= 52 and <= 61) throw Missing("branch.nonordinary_City_winner");
            if (identity is not (>= 1 and <= 51)) return (null, null);
            var city = current.ReadCityTableReference(identity) ?? throw Missing("winner_City_pointer");
            if (!city.PhysicalSlot.HasValue) return (null, null);
            int location = K(current.ReadBaseObjectLink(city), "winner_City_location");
            if (!state.Cities.ContainsKey(location)) throw Missing("winner_City_entity_mapping");
            return (null, new(SettlementBaseKind.City, location));
        }
        int key = HexIdentity(current, hex);
        var indexed = current.ReadHexTableReference(key < 48400 ? key : 0) ?? throw Missing("winner_indexed_Hex_pointer");
        if (!indexed.PhysicalSlot.HasValue) return (null, null);
        int first = unchecked((short)K(current.ReadHexRaw1C(indexed), "winner_indexed_Hex_signed1C"));
        if (first is < 1 or > 500) return (null, null);
        int raw = unchecked((short)K(current.ReadHexRaw1C(hex), "winner_Hex_signed1C_again"));
        if (raw < 0) throw Missing("winner_negative_Army_index");
        var army = ArmyPointer(current, raw);
        int? winner = ArmyDestructionQueue.ResolveUnitId(state, army.PhysicalSlot);
        if (winner is null && K(current.ReadArmyUnitNativeValidity(army), "winner_unmapped_Army_validity"))
            throw Missing("winner_Army_entity_mapping");
        return (winner, null);
    }

    private static FieldHexOccupantReceipt Occupant(GameState state, SourceWorldContext current, HexReference hex)
    {
        int key = HexIdentity(current, hex);
        RequireIdempotentHex(current, hex, key);
        var result = FieldHexOccupantSource.ReadCurrent(state, current, key);
        return result.Receipt ?? throw Missing(result.UnresolvedOwner ?? "16EB20");
    }
    private static void RequireIdempotentHex(SourceWorldContext current, HexReference hex, int key)
    {
        if (current.ReadHexTableReference(key) != hex) throw Missing("Hex_alias_16EB20_or2CA7B0");
    }
    private static ObjectReference ObjectAt(SourceWorldContext current, HexReference hex)
    {
        ushort raw = K(current.ReadHexObjectLink(hex), "Hex_Object1A");
        return current.ReadObjectTableReference(raw <= 3000 ? raw : 0) ?? throw Missing("Hex_Object_pointer");
    }
    private static ArmyUnitReference ArmyPointer(SourceWorldContext current, int raw)
    {
        if (raw < 0) throw Missing("negative_Army_pointer_index");
        return current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0) ?? throw Missing("Army_pointer");
    }
    private static int HexIdentity(SourceWorldContext current, HexReference hex) =>
        K(current.ReadHexPhysicalSlot(hex), "Hex_identity");
    private static void Bind(GameState state, SourceWorldContext current) =>
        current.BindOwnedDestructionQueue(state.PendingCombatSettlements ?? throw Missing("pending140_queue"), state);
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("Current2709A0 zero routing owner is unknown: " + field);
}
