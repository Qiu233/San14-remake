using San14.Core;

namespace San14.Data;

public sealed record TransportArmyNormalLootWrite(BaseReference Home,
    BaseReference CurrencyCity, BaseResourceKind Kind, int Amount,
    int Before, int CallerCapacity, int SetterCapacity, int After);
public sealed record TransportArmyNormalCargoReceipt(int ArmyPhysical, int Ready16Before,
    int AppliedLoss, float LossRatio, int Gold5CBefore, int Supplies60Before,
    int LostGold, int LostSupplies, int Gold5CAfter, int Supplies60After,
    TransportArmyNormalLootWrite[] LootWrites);
public sealed record TransportArmyNormalCargoResult(GameState? State,
    TransportArmyNormalCargoReceipt? Receipt, string? UnresolvedOwner);

/// <summary>Only TC179034..17913C: type2 proportional cargo loss and the
/// real Army-Home or source-Base loot. The enclosing178EC0 operation owns reward,
/// ready/wounded writes, Force/personality and death admission.</summary>
public static class TransportArmyNormalCargoSource
{
    /// <summary>The loss has already been capped against the old ushort16.
    /// This uses one operation-local current view; it never imports a pool or
    /// infers raw cargo from playable combat supplies.</summary>
    public static TransportArmyNormalCargoResult Apply(GameState state,
        SourceWorldContext current, UnitState attacker, UnitState target, int appliedLoss) =>
        ApplyCore(state, current, attacker, target, appliedLoss, false);

    internal static TransportArmyNormalCargoResult ApplyBorrowed(GameState state,
        SourceWorldContext current, UnitState attacker, UnitState target, int appliedLoss) =>
        ApplyCore(state, current, attacker, target, appliedLoss, true);

    internal static TransportArmyNormalCargoResult ApplyFieldObject(GameState state,
        SourceWorldContext current, UnitState target, int appliedLoss) =>
        ApplyCore(state, current, null, target, appliedLoss, true);

    //Ordinary178EC0 with an actually invalid source Army still loses the
    //target cargo. Neither the Army nor Base loot recipient gate succeeds.
    internal static TransportArmyNormalCargoResult ApplyWithoutValidSource(GameState state,
        SourceWorldContext current, UnitState target, int appliedLoss) =>
        ApplyCore(state, current, null, target, appliedLoss, true);

    /// <summary>179078..1790F2's actual Base-source alternative. The
    /// source remains a Base, including a Gate's repeated22A940 currency
    /// routing. Native-invalid source Base skips loot, never target cargo loss.</summary>
    internal static TransportArmyNormalCargoResult ApplyBaseOrigin(GameState state,
        SourceWorldContext current, BaseReference source, UnitState target, int appliedLoss) =>
        ApplyCore(state, current, null, target, appliedLoss, true, source);

    private static TransportArmyNormalCargoResult ApplyCore(GameState state,
        SourceWorldContext current, UnitState? attacker, UnitState target, int appliedLoss,
        bool preserveCurrentBindings, BaseReference? baseOrigin = null)
    {
        try
        {
            int physical = Bind(target);
            if (target.NativeTacticRuntime?.Status10 != 2 || target.Mission is not { Kind: UnitMissionKind.Transport } mission)
                throw Missing("branch.normal_cargo_not_type2");
            if (target.Troops is < 0 or > ushort.MaxValue || appliedLoss > target.Troops)
                throw Missing("current.normal_cargo_capped_ushort16");
            var arena = state.ReadArmyLifecycle()!;
            var slot = arena.ReceiverSlots()[physical];
            var resolved = ArmyStoreRules.Resolve(target.CarriedStores, slot.StoredResources);
            if (resolved.Status == ArmyStoreResolutionStatus.Unknown)
                throw Missing("current.normal_cargo_saved5C60");
            if (resolved.Status == ArmyStoreResolutionStatus.Conflicting)
                throw Missing("current.normal_cargo_owners_disagree");
            var stores = resolved.Stores!;
            int gold = stores.Gold, supplies = stores.Food;
            var loss = ArmyStoreRules.LoseCargo(stores, target.Troops, appliedLoss);
            float ratio = loss.Ratio;
            int lostGold = loss.Lost.Gold, lostSupplies = loss.Lost.Food;
            int afterGold = loss.Remaining.Gold, afterSupplies = loss.Remaining.Food;
            var writes = new List<TransportArmyNormalLootWrite>();
            var cityChanges = new Dictionary<int, CityState>(state.Cities);
            int? winnerPhysical = attacker is null ? null : Bind(attacker, allowPendingSource: preserveCurrentBindings);
            var winner = new ArmyUnitReference(winnerPhysical);
            if (Need(current.ReadArmyUnitNativeValidity(winner), "attacker native validity"))
            {
                var force = current.ReadArmyUnitForceReference(winner);
                if (Need(current.ReadForceNativeValidity(force), "attacker Force validity"))
                {
                    int first = Need(current.ReadForcePhysicalSlot(force), "attacker first Force identity");
                    if (first != 51 && unchecked((uint)(Need(current.ReadForcePhysicalSlot(force), "attacker repeated Force identity") - 46)) > 4)
                    {
                        var home = current.FindArmyUnitCurrentBase(winner); //22A730, current CommanderE2.
                        if (Need(current.ReadBaseNativeValidity(home), "attacker Home validity"))
                        {
                            Add(home, BaseResourceKind.Gold, lostGold);
                            Add(home, BaseResourceKind.Supplies, lostSupplies);
                        }
                    }
                }
            }
            else if (baseOrigin is not null && Need(current.ReadBaseNativeValidity(baseOrigin), "source Base validity"))
            {
                //The Army-source Force/category restrictions do not belong
                //to this alternative. Each add independently resolves the
                //Base's currency receiver and its caller/setter capacities.
                Add(baseOrigin, BaseResourceKind.Gold, lostGold);
                Add(baseOrigin, BaseResourceKind.Supplies, lostSupplies);
            }
            //All reached reads succeeded before the first business write.
            foreach (var write in writes) current.SetBaseResource(write.CurrencyCity, write.Kind, write.After);
            current.BindArmyReturnResourceFields(physical, new((ushort)target.Troops,
                target.WoundedTroops is >= 0 and <= ushort.MaxValue ? (ushort)target.WoundedTroops.Value : null,
                ArmyMoraleSourceAdapter.Read(target), afterGold, afterSupplies));
            var updated = target with { CarriedStores = new(afterGold, afterSupplies), Supplies = afterSupplies,
                Mission = mission with { CargoGold = afterGold } };
            var slots = arena.ReceiverSlots().ToArray();
            slots[physical] = slot with { StoredResources = new(afterGold, afterSupplies) };
            var result = (state with { Cities = cityChanges,
                Units = new(state.Units) { [target.Id] = updated } }).WithArmyLifecycle(arena.WithReceiverSlots(slots));
            return new(result, new(physical, target.Troops, appliedLoss, ratio, gold, supplies,
                lostGold, lostSupplies, afterGold, afterSupplies, writes.ToArray()), null);

            int Bind(UnitState army, bool allowPendingSource = false)
            {
                if (army.NativeArmyOrigin?.NativeSlotIndex is not int index || index is < 1 or > 500 ||
                    state.ReadArmyLifecycle()?.ReceiverSlots() is not { Length: 501 } rows ||
                    rows[index].Commander12 is not ushort commander || rows[index].Status10 is not byte status)
                    throw Missing("current.normal_cargo_Army_receiver");
                bool active = rows[index].ActiveUnitId == army.Id && state.Units.GetValueOrDefault(army.Id) == army;
                bool pending = allowPendingSource && preserveCurrentBindings && rows[index].ActiveUnitId is null &&
                    rows[index].PendingImage == army;
                if (!active && !pending) throw Missing("current.normal_cargo_Army_receiver");
                if (!preserveCurrentBindings)
                {
                    current.SetArmyUnitTablePointer(index, index);
                    current.SetArmyUnitCommanderId(index, commander);
                    current.SetArmyUnitStatusFlag10(index, status);
                }
                return index;
            }
            BaseReference Currency(BaseReference home) => home.Kind == BaseKind.City
                ? home : CurrentGateMapQueries.FindCity(home, current);
            int Capacity(BaseReference home, BaseResourceKind kind)
            {
                var cap = BasePostLoad.ReadResourceCapacities(Currency(home), current);
                return kind == BaseResourceKind.Gold ? cap.Gold : cap.Supplies;
            }
            void Add(BaseReference home, BaseResourceKind kind, int amount)
            {
                int cap = Capacity(home, kind); //027300 B8 /169890 C0.
                var readCity = Currency(home); //Separate current virtual90/A0.
                int before = Need(current.ReadBaseResource(readCity, kind), "attacker Home virtual resource");
                int requested = Bound(unchecked(before + amount), cap);
                var setCity = Currency(home); //Gate98/A8 reacquire22A940.
                if (setCity.PhysicalSlot is not int cityId || !cityChanges.TryGetValue(cityId, out var city))
                    throw Missing("branch.normal_cargo_currency_placeholder");
                int setterCap = Capacity(setCity, kind); //City98/A8 repeated cap.
                int after = Bound(requested, setterCap);
                writes.Add(new(home, setCity, kind, amount, before, cap, setterCap, after));
                cityChanges[cityId] = kind == BaseResourceKind.Gold
                    ? city with { Gold = after } : city with { Supplies = after };
            }
        }
        catch (MissingOwner error) { return new(null, null, error.Message); }
        catch (InvalidDataException error) { return new(null, null, "current.normal_cargo: " + error.Message); }
    }

    private static int Bound(int value, int upper) => value < 0 ? 0 : Math.Min(value, upper);
    private static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing("current.normal_cargo_" + name);
    private static MissingOwner Missing(string name) => new(name);
    private sealed class MissingOwner(string name) : Exception(name);
}
