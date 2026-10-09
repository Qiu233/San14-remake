using San14.Core;

namespace San14.Data;

public sealed record OwnedArmyCityReturnResourceResult(GameState State,
    ArmyReturnResourcePacket? Packet, string? MissingInput)
{
    public bool Applied => Packet is not null;
}

/// <summary>Operation-local ordinary Army/city return resource stage. Its
/// output is a draft for the caller's complete arrival operation; it keeps the
/// Army and Person alive and does not run trait/Home/pending/destruction work.</summary>
public sealed class OwnedArmyCityReturnResourceStage : IPlayableCityEntryResourceRules
{
    public const string Revision = "san14:ce-owned-army-city-return-resource-stage-20261007";
    private readonly ArmyCampaignRuleAdapter _package;
    public OwnedArmyCityReturnResourceStage(ArmyCampaignRuleAdapter package)
        => _package = package ?? throw new ArgumentNullException(nameof(package));

    public PlayableCityEntryResourceQuote? Quote(GameState state, UnitState unit, CityState city)
    {
        if (state.Units.GetValueOrDefault(unit.Id) != unit || state.Cities.GetValueOrDefault(city.Id) != city)
            return null;
        var applied = TryApply(_package, state, unit.Id, city.Id);
        return applied.Applied ? new(applied.State.Cities[city.Id], Revision) : null;
    }

    public static OwnedArmyCityReturnResourceResult TryApply(ArmyCampaignRuleAdapter package,
        GameState state, int unitId, int cityId)
    {
        ArgumentNullException.ThrowIfNull(package); ArgumentNullException.ThrowIfNull(state);
        if (!state.Units.TryGetValue(unitId, out UnitState? army) ||
            !state.Cities.TryGetValue(cityId, out CityState? city)) return Missing("current.Army_or_City");
        if (city.ForceId != army.ForceId || city.HexId != army.HexId) return Missing("branch.not_friendly_city_arrival");
        if (state.NativeArmySimulation?.Parameters is not { } parameters ||
            army.NativeArmyOrigin is not { NativeSlotIndex: >= 1 and <= 500 } origin ||
            origin.Language != parameters.Language || origin.RuleVersion != parameters.RuleVersion ||
            army.NativeTacticRuntime is not { Status10: 1 } runtime) return Missing("current.ordinary_source_Army");
        ArmyStoreKnowledge? receiver = null;
        if (state.NativeArmyLifecycle is { } lifecycle)
        {
            int physical = origin.NativeSlotIndex!.Value;
            if (physical >= lifecycle.Slots.Length || lifecycle.Slots[physical] is not { } arenaSlot ||
                arenaSlot.ActiveUnitId != army.Id || arenaSlot.PhysicalSlot != physical)
                return Missing("current.Arena_Army_resources5C60");
            receiver = arenaSlot.StoredResources;
            if (receiver.Known is null) return Missing("current.Arena_Army_resources5C60");
        }
        var resolved = ArmyStoreRules.Resolve(army.CarriedStores, receiver);
        if (resolved.Status == ArmyStoreResolutionStatus.Conflicting)
            return Missing("current.Army_resource_owners_disagree");
        if (resolved.Stores is not { } resources) return Missing("current.Army_resources5C60");
        if (army.Troops is < 0 or > ushort.MaxValue || army.WoundedTroops is not >= 0 or > ushort.MaxValue ||
            army.NativeMorale1A is not byte morale) return Missing("current.Army16_18_1A");
        if (city.NativeReturnRuntime is not { Wounded40: not null, Morale44: not null })
            return Missing("current.Base40_44");
        var context = package.CaptureCurrentDefinitions(state);
        if (context is null) return Missing("current.Base_Person_definitions");
        int slot = origin.NativeSlotIndex!.Value;
        context.SetArmyUnitTablePointer(slot, slot);
        context.SetArmyUnitCommanderId(slot, checked((ushort)army.OfficerId));
        context.SetArmyUnitStatusFlag10(slot, runtime.Status10);
        context.SetArmyReturnResourceFields(slot, new((ushort)army.Troops,
            (ushort)army.WoundedTroops!.Value, morale, resources.Gold, resources.Food));
        var prepared = ArmyReturnResourceUpdater.ApplyCurrentResourceOutputs(new(slot),
            new(BaseKind.City, cityId), context);
        if (prepared.Packet is not { } packet) return Missing(prepared.UnresolvedOwner ?? "current.return_resource_packet");
        CityState updated = city with { Troops = packet.Ready3CAfter, Gold = packet.Gold34After,
            Supplies = packet.Supplies38After, Morale = packet.Morale44After,
            NativeReturnRuntime = city.NativeReturnRuntime with
                { Morale44 = packet.Morale44After, Wounded40 = packet.Wounded40After } };
        GameState result = state with { Cities = new(state.Cities) { [cityId] = updated } };
        return new(result, packet, null);

        OwnedArmyCityReturnResourceResult Missing(string reason) => new(state, null, reason);
    }
}
