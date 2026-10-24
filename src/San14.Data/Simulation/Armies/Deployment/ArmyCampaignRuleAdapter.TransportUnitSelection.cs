using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Actual selected type2 receivers in page order. One current
    /// capture serves all rows and cards. No Box-only filters, Make ownership
    /// requirements, option defaults, route searches or writes.</summary>
    public TransportUnitSelection QueryTransportUnitSelection(GameState state, IReadOnlyList<int> unitIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(unitIds);
        var rows = new List<TransportUnitRow>();
        try
        {
            CheckTransportSelectionShape(unitIds);
            var current = CaptureWritableTransportRouteCurrent(state);
            foreach (int id in unitIds) rows.Add(ReadRow(id));
            bool? canEdit;
            string? permissionMissing = null;
            try { canEdit = ValidateTransportSelectedSet(state, current, unitIds); }
            catch (InvalidDataException error) { canEdit = null; permissionMissing = error.Message; }
            return new(rows.AsReadOnly(), canEdit,
                Aggregate(rows.Select(row => row.ReturnOfficerToSource), "Flags26_bit20"),
                Aggregate(rows.Select(row => row.SourceBehaviorChoice), "Army41"), permissionMissing);

            TransportUnitRow ReadRow(int id)
            {
                var missing = new List<string>();
                T? Value<T>(Func<T?> read, string field) where T : struct
                {
                    try { return read() ?? throw TransportUnitQueryMissing(field); }
                    catch (InvalidDataException error) { missing.Add(error.Message); return null; }
                }
                if (!state.Units.TryGetValue(id, out var unit))
                    return Empty(id, false, "transport.unit-target.unit-absent");
                int? physical = Value<int>(() => TransportReceiver(state, unit), "receiver");
                if (physical is not int at) return Empty(id, null, string.Join("; ", missing));
                var army = new ArmyUnitReference(at);
                bool? valid = Value(() => current.ReadArmyUnitNativeValidity(army), "validity");
                ushort? commander = Value(() => current.ReadArmyUnitCommanderId(army), "commander12");
                if (commander.HasValue && commander.Value != unit.OfficerId)
                    return Empty(id, null, "transport.unit-target.commander-owner") with { ArmyPhysicalSlot = at };
                byte? status = Value(() => current.ReadArmyReturnUnitStatus10(army), "status10");
                ushort? rawHex = Value(() => current.ReadArmyUnitRawHex2A(army), "hex2A");
                int? lookupHex = rawHex.HasValue ? rawHex < 48400 ? rawHex.Value : 0 : null;
                int? physicalHex = lookupHex is int lookup ? Value<int>(() =>
                    (current.ReadHexTableReference(lookup) ?? throw TransportUnitQueryMissing("hex-pointer")).PhysicalSlot,
                    "hex-receiver") : null;
                var resources = current.ReadArmyReturnResourceFields(army);
                int? troops = Value<int>(() => resources?.Ready16, "ready16");
                int? gold = Value(() => resources?.Gold5C, "gold5C");
                int? supplies = Value(() => resources?.Supplies60, "supplies60");
                byte? land = Value(() => ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit), "land1B");
                byte? naval = Value(() => ArmyConfigurationSourceAdapter.ReadNaval(state, unit), "naval1C");
                byte? flags = Value(() => unit.NativeTacticRuntime?.Flags26, "flags26");
                byte? choice = Value(() => TransportRouteSourceAdapter.ReadChoice(state, unit), "choice41");
                if (choice is > 2) { missing.Add("transport.unit-target.choice41-domain"); choice = null; }
                var target = ReadSelectionGroupingFields(state, current, unit, at);
                if (target.Missing is not null) missing.Add(target.Missing);
                DeploymentTargetCard? card = null;
                if (commander.HasValue && land.HasValue && naval.HasValue)
                    card = QueryDeploymentTargetCardsCore(state, [commander.Value], [(land.Value, naval.Value)],
                        suppliedCurrent: current, supplyFlags: [flags.HasValue ? (flags.Value & 2) != 0 : null])[0];
                return new(id, at, commander, valid, status, lookupHex, physicalHex, troops, gold,
                    supplies, land, naval, target.Action, target.Kind, target.Identity,
                    flags.HasValue ? (flags.Value & 0x20) != 0 : null, choice, card, missing.AsReadOnly())
                {
                    // Existing detail pages accept every active header, including
                    // type2 and its receiver-owned cargo. Missing axis values
                    // remain individual unknown fields on the real page.
                    DetailsAvailable = unit.OfficerId != 0 && at != 0 ? status.HasValue ? status != 0 : null : false,
                    DetailsUnresolvedInput = status.HasValue ? null : "transport.unit-target.details-status10",
                    WithdrawalAvailable = unit.NativeTacticRuntime is { } runtime
                        ? runtime.Abnormal23 == 0 && runtime.Abnormal24 == 0 && runtime.Abnormal25 == 0 : null,
                    WithdrawalUnresolvedInput = unit.NativeTacticRuntime is null ? "transport.unit-target.withdrawal-abnormal23-25" : null
                };
            }
        }
        catch (InvalidDataException error)
        {
            return new(rows.AsReadOnly(), null, new(null, null, error.Message),
                new(null, null, error.Message), error.Message);
        }

        static TransportUnitRow Empty(int id, bool? valid, string missing) =>
            new(id, null, null, valid, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, Array.AsReadOnly(new[] { missing }))
            {
                DetailsAvailable = valid == false ? false : null, DetailsUnresolvedInput = missing,
                WithdrawalAvailable = valid == false ? false : null, WithdrawalUnresolvedInput = missing
            };
    }

    private static TransportSelectedOption<T> Aggregate<T>(IEnumerable<T?> source, string field) where T : struct
    {
        T? first = null;
        bool mixed = false;
        foreach (var value in source)
        {
            if (!value.HasValue) return new(null, null, TransportUnitQueryMissing(field).Message);
            if (!first.HasValue) first = value;
            else if (!EqualityComparer<T>.Default.Equals(first.Value, value.Value)) mixed = true;
        }
        return first.HasValue ? new(mixed ? null : first, mixed, null)
            : new(null, null, TransportUnitQueryMissing("selection-empty").Message);
    }

    private static void CheckTransportSelectionShape(IReadOnlyList<int> ids)
    {
        if (ids.Count is < 1 or > 30 || ids.Distinct().Count() != ids.Count)
            throw TransportUnitQueryMissing("selected-set");
    }

    private static int TransportReceiver(GameState state, UnitState unit)
    {
        var slots = (state.ReadArmyLifecycle() ?? throw TransportUnitQueryMissing("lifecycle")).ReceiverSlots();
        int physical = unit.NativeArmyOrigin?.NativeSlotIndex ?? throw TransportUnitQueryMissing("receiver");
        if ((uint)physical >= (uint)slots.Length || slots[physical].ActiveUnitId != unit.Id)
            throw TransportUnitQueryMissing("active-receiver-owner");
        return physical;
    }

    private static bool ValidateTransportSelectedSet(GameState state, SourceWorldContext current, IReadOnlyList<int> ids)
    {
        CheckTransportSelectionShape(ids);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw TransportUnitQueryMissing("player-force-pointer");
        int playerPhysical = TU(current.ReadForceNativeValidity(player), "player-force-validity")
            ? TU(player.PhysicalSlot, "player-force-receiver") : -1;
        foreach (int id in ids)
        {
            if (!state.Units.TryGetValue(id, out var unit)) return false;
            var army = new ArmyUnitReference(TransportReceiver(state, unit));
            if (!TU(current.ReadArmyUnitNativeValidity(army), "selected-validity")) return false;
            if (TU(current.ReadArmyUnitCommanderId(army), "selected-commander") != unit.OfficerId)
                throw TransportUnitQueryMissing("selected-commander-owner");
            if (TU(current.ReadArmyReturnUnitStatus10(army), "selected-status10") != 2) return false;
            if (TU(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit), "selected-land1B") is not (19 or 20)) return false;
            var force = current.ReadArmyUnitForceReference(army);
            if (!TU(current.ReadForceNativeValidity(force), "selected-force-validity") ||
                TU(force.PhysicalSlot, "selected-force-receiver") != playerPhysical) return false;
        }
        return true;
    }

    /// <summary>7CDA50: row Person E4 determines position. A known invalid
    /// Person/location returns null; missing pointers/fields remain unknown.
    /// Result is physical Hex identity, never the Army2A table key.</summary>
    internal static int? ReadTransportUnitTargetPosition(SourceWorldContext current, int officerId)
    {
        if (officerId < 0) throw TransportUnitQueryMissing("position-person-index");
        var person = current.ReadPersonLookupReference(officerId <= 6000 ? officerId : 0)
            ?? throw TransportUnitQueryMissing("position-person-pointer");
        if (person.PhysicalSlot is not int p) return null;
        if (!TU(current.ReadPersonNativeValidity(p), "position-person-validity")) return null;
        var position = PersonPositionRepair.DecodeSecondLocation(TU(current.ReadPersonSecondLocation(p), "position-E4"));
        if (position.Kind == 27)
        {
            var army = current.ReadArmyUnitTableReference(position.Index <= 500 ? position.Index : 0)
                ?? throw TransportUnitQueryMissing("position-army-pointer");
            if (!TU(current.ReadArmyUnitNativeValidity(army), "position-army-validity")) return null;
            int raw = TU(current.ReadArmyUnitRawHex2A(army), "position-army2A");
            return TU((current.ReadHexTableReference(raw < 48400 ? raw : 0)
                ?? throw TransportUnitQueryMissing("position-hex-pointer")).PhysicalSlot, "position-hex-receiver");
        }
        BaseReference? receiver = position.Kind switch
        {
            5 => current.ReadCityTableReference(position.Index),
            6 => current.ReadGateTableReference(position.Index),
            _ => new(BaseKind.City, null)
        };
        if (receiver is null) throw TransportUnitQueryMissing("position-base-pointer");
        return TU(current.ReadBaseNativeValidity(receiver), "position-base-validity")
            ? TU(CurrentGateMapQueries.FindHex(receiver, current).PhysicalSlot, "position-base-hex") : null;
    }

    private static T TU<T>(T? value, string field) where T : struct => value ?? throw TransportUnitQueryMissing(field);
    private static InvalidDataException TransportUnitQueryMissing(string field) => new("transport.unit-target." + field);
}
