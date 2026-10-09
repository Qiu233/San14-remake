using San14.Core;

namespace San14.Data;

public enum ArmyDestructionResourceWriteKind
{ BaseWounded40, ArmyWounded18, BaseMorale44, ArmyMorale1A, BaseGold34, BaseSupplies38 }
public sealed record ArmyDestructionResourceWrite(ArmyDestructionResourceWriteKind Kind,
    BaseReference? Base, ArmyUnitReference? Army, int Value)
{
    internal ArmyReturnResourceFields? ArmyFieldsAfter { get; init; }
}
/// <summary>259360 captures these pointers before its Person callbacks. The
/// subsequent resource block must retain those captured receiver identities.</summary>
public sealed record ArmyDestructionResourceOwners(BaseReference VictimHome,
    ForceReference WinnerForce);
public sealed record ArmyDestructionResourceResolution(ArmyDestructionResourcePacket? Packet,
    string? UnresolvedOwner);
public sealed class ArmyDestructionResourcePacket
{
    public IReadOnlyList<ArmyDestructionResourceWrite> Writes { get; }
    public ArmyDestructionResourceOwners CapturedOwners { get; }
    internal SourceWorldContext Owner { get; }
    internal ArmyDestructionResourcePacket(SourceWorldContext owner,
        ArmyDestructionResourceOwners captured, IEnumerable<ArmyDestructionResourceWrite> writes)
    { Owner = owner; CapturedOwners = captured; Writes = Array.AsReadOnly(writes.ToArray()); }
}

/// <summary>Only259360's resource block: home/winner wounds,
/// winner morale and Base loot. Its independent saved parameters/current
/// receivers are consumed without RNG, retirement or whole29F220 execution.</summary>
public static class ArmyDestructionResources
{
    public static ArmyDestructionResourceOwners CaptureOwners(SourceWorldContext current,
        ArmyUnitReference victim, ArmyUnitReference? winner = null, BaseReference? winnerBase = null)
    {
        try
        {
            var force = new ForceReference(null);
            if (ArmyValid(current, winner)) force = current.ReadArmyUnitForceReference(winner!);
            else if (BaseValid(current, winnerBase)) force = BaseForce(current, winnerBase!);
            return new(current.FindArmyUnitCurrentBase(victim), force);
        }
        catch (MissingOwner error) { throw new InvalidDataException(error.Message, error); }
    }

    public static ArmyDestructionResourceResolution Prepare(GameState state,
        SourceWorldContext current, ArmyUnitReference victim,
        ArmyUnitReference? winner = null, BaseReference? winnerBase = null,
        ArmyDestructionResourceOwners? capturedOwners = null)
    {
        var writes = new List<ArmyDestructionResourceWrite>();
        var bases = new Dictionary<(BaseReference Base, ArmyDestructionResourceWriteKind Kind), int>();
        var armies = new Dictionary<ArmyUnitReference, ArmyReturnResourceFields>();
        try
        {
            if (!ArmyValid(current, victim)) throw Missing("branch.resource_victim_invalid");
            var captured = capturedOwners ?? CaptureOwners(current, victim, winner, winnerBase);
            int cc = Get(BattleRemakeCurrentSettings.ReadInt32(state, 0xcc), "current.settings_CC");
            int selector = Get(BattleRemakeCurrentSettings.ReadSelectorE58(state), "current.settings_E58");
            bool woundBlock = selector != 0 && cc == 0;
            if (selector == 0) woundBlock = Advanced7() > 0;

            if (woundBlock && ArmyValid(current, victim) && BaseValid(current, captured.VictimHome))
            {
                int homePercent = P(SimulationParameterKeys.ArmyDestruction.DefeatedResourcesHomePercent), winnerPercent = P(SimulationParameterKeys.ArmyDestruction.DefeatedResourcesWinnerPercent);
                if ((ArmyValid(current, winner) && current.IsArmyUnitBoundToSpecialForce(winner!)) ||
                    (BaseValid(current, winnerBase) && SpecialBase(winnerBase!)))
                { homePercent = P(SimulationParameterKeys.ArmyDestruction.DefeatedResourcesSpecialHomePercent); winnerPercent = P(SimulationParameterKeys.ArmyDestruction.DefeatedResourcesSpecialWinnerPercent); }
                int homeWound = Percent(Wounded(victim), homePercent);
                if (selector == 0) homeWound = Math.Min(Percent(homeWound, Advanced7()), TotalVictim());
                AddBaseWounded(captured.VictimHome, homeWound);

                int callerMoraleCap = ForceValid(captured.WinnerForce)
                    ? BasePostLoad.ReadMoraleUpper(captured.WinnerForce, current) : 100;
                if (ArmyValid(current, winner))
                {
                    if (selector == 0)
                    {
                        int incoming = Math.Min(Percent(unchecked(Ready(victim) + Percent(Wounded(victim), winnerPercent)), Advanced7()), TotalVictim());
                        WriteArmy(winner!, ArmyDestructionResourceWriteKind.ArmyWounded18,
                            Bound(unchecked(Wounded(winner!) + incoming), 20000));
                    }
                    else
                    {
                        WriteArmy(winner!, ArmyDestructionResourceWriteKind.ArmyWounded18,
                            (int)Math.Min(unchecked((uint)Wounded(winner!) + (uint)Ready(victim)), 20000u));
                        WriteArmy(winner!, ArmyDestructionResourceWriteKind.ArmyWounded18,
                            Bound(unchecked(Wounded(winner!) + Percent(Wounded(victim), winnerPercent)), 20000));
                    }
                    int panel41 = ForceValid(captured.WinnerForce)
                        ? Get(current.ReadBaseForceModifier(captured.WinnerForce, 0x29), "current.winner_Force_panel41") : 0;
                    int getterCap = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(winner!), current);
                    int requested = Math.Min(unchecked(Math.Min(Morale(winner!), getterCap) + 10 + panel41), callerMoraleCap);
                    int setterCap = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(winner!), current);
                    WriteArmy(winner!, ArmyDestructionResourceWriteKind.ArmyMorale1A,
                        unchecked((byte)Bound(requested, setterCap)));
                }
                else if (BaseValid(current, winnerBase) && Get(current.ReadBaseObjectLink(winnerBase!), "current.winner_Base4E") < 52)
                {
                    if (selector == 0)
                        AddBaseWounded(winnerBase!, Math.Min(Percent(unchecked(Ready(victim) + Percent(Wounded(victim), winnerPercent)), Advanced7()), TotalVictim()));
                    else
                    { AddBaseWounded(winnerBase!, Ready(victim)); AddBaseWounded(winnerBase!, Percent(Wounded(victim), winnerPercent)); }
                    WriteBaseMorale(winnerBase!, Math.Min(unchecked(BaseNumber(winnerBase!, ArmyDestructionResourceWriteKind.BaseMorale44) + 10), callerMoraleCap));
                }
            }

            //Loot is outside the CC/E58 injury gate. Only victim Army5C/60
            //supply this numeric input, never UnitState.Supplies combat cargo.
            if (ArmyValid(current, winner))
            {
                if (ForceValid(captured.WinnerForce) && OrdinaryForce(captured.WinnerForce))
                {
                    var home = current.FindArmyUnitCurrentBase(winner!);
                    if (BaseValid(current, home))
                    {
                        AddLoot(home);
                        var commander = Commander(winner!);
                        foreach (var item in new[] { (0x6d, ArmyDestructionResourceWriteKind.BaseGold34),
                            (0x6e, ArmyDestructionResourceWriteKind.BaseSupplies38) })
                        {
                            if (!Get(current.ReadPersonNativeValidity(commander), "current.winner_Commander_validity")) continue;
                            if (!Get(TacticRemakeCurrentTraits.CommanderHasEffect(current, commander, item.Item1), "current.Commander_presence2C5830")) continue;
                            int amount = Get(TacticRemakeCurrentTraits.CommanderEffectSum(current, commander, item.Item1), "current.Commander_sum2B3170");
                            AddBaseResource(home, item.Item2, amount);
                        }
                    }
                }
            }
            else if (BaseValid(current, winnerBase))
            {
                var ownerCity = CurrentGateMapQueries.FindCity(winnerBase!, current); //22A940.
                if (BaseValid(current, ownerCity)) AddLoot(winnerBase!);
            }
            return new(new(current, captured, writes), null);

            int P(string key) => Get(ArmyDestructionParameterSeed.ReadCurrent(state, key), $"current.destruction_parameter{key}");
            int Advanced7() => Get(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 7), "current.settings_advanced7");
            bool ForceValid(ForceReference force) => force.PhysicalSlot.HasValue && Get(current.ReadForceNativeValidity(force), "current.winner_Force_validity");
            bool OrdinaryForce(ForceReference force)
            {
                int first = Get(current.ReadForcePhysicalSlot(force), "current.winner_Force_physical_first");
                if (first == 51) return false;
                int repeated = Get(current.ReadForcePhysicalSlot(force), "current.winner_Force_physical_repeated");
                return unchecked((uint)(repeated - 46)) > 4;
            }
            bool SpecialBase(BaseReference target)
            {
                ushort id = Get(current.ReadArmyReturnBaseGovernor2E(target), "current.winner_Base_governor2E");
                var person = Person(id);
                if (person.PhysicalSlot is not int physical) return false;
                ushort first = Get(current.ReadPersonFirstLocation(physical), "current.winner_BaseGovernor_E2");
                if (first == 0 || unchecked((uint)(first - 1)) >= 61) return false;
                int force = Get(current.ReadForcePhysicalSlot(BaseForce(current, target)), "current.winner_BaseForce_physical");
                return unchecked((uint)(force - 46)) <= 4;
            }
            TransferPersonReference Person(ushort id) => (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("current.Commander_ID_lookup");
            int Commander(ArmyUnitReference army) => Person(Get(current.ReadArmyUnitCommanderId(army), "current.Commander12")).PhysicalSlot
                ?? throw Missing("current.Commander_pointer");
            ArmyReturnResourceFields Fields(ArmyUnitReference army)
            {
                if (armies.TryGetValue(army, out var saved)) return saved;
                var fields = current.ReadArmyReturnResourceFields(army) ?? throw Missing("current.Army_resource_fields");
                armies.Add(army, fields); return fields;
            }
            int Ready(ArmyUnitReference army) => Get(Fields(army).Ready16, "current.Army_ready16");
            int Wounded(ArmyUnitReference army) => Get(Fields(army).Wounded18, "current.Army_wounded18");
            int Morale(ArmyUnitReference army) => Get(Fields(army).Morale1A, "current.Army_morale1A");
            int TotalVictim() => unchecked(Ready(victim) + Wounded(victim));
            void WriteArmy(ArmyUnitReference army, ArmyDestructionResourceWriteKind kind, int value)
            {
                var fields = kind == ArmyDestructionResourceWriteKind.ArmyWounded18
                    ? Fields(army) with { Wounded18 = unchecked((ushort)value) }
                    : Fields(army) with { Morale1A = unchecked((byte)value) };
                armies[army] = fields;
                writes.Add(new(kind, null, army, value) { ArmyFieldsAfter = fields });
            }
            int BaseNumber(BaseReference target, ArmyDestructionResourceWriteKind kind)
            {
                if (bases.TryGetValue((target, kind), out int value)) return value;
                return kind switch
                {
                    ArmyDestructionResourceWriteKind.BaseWounded40 => Get(current.ReadArmyReturnBaseWounded40(target), "current.Base_wounded40"),
                    ArmyDestructionResourceWriteKind.BaseMorale44 => Get(current.ReadBaseMorale44(target), "current.Base_morale44"),
                    ArmyDestructionResourceWriteKind.BaseGold34 => Get(current.ReadBaseResource(target, BaseResourceKind.Gold), "current.Base_gold34"),
                    _ => Get(current.ReadBaseResource(target, BaseResourceKind.Supplies), "current.Base_supplies38")
                };
            }
            void WriteBase(BaseReference target, ArmyDestructionResourceWriteKind kind, int value)
            { bases[(target, kind)] = value; writes.Add(new(kind, target, null, value)); }
            int Cap(BaseReference target, ArmyDestructionResourceWriteKind kind)
            {
                var caps = BasePostLoad.ReadResourceCapacities(target, current);
                return kind switch { ArmyDestructionResourceWriteKind.BaseWounded40 => caps.Troops,
                    ArmyDestructionResourceWriteKind.BaseGold34 => caps.Gold, _ => caps.Supplies };
            }
            void AddBaseWounded(BaseReference target, int amount)
            {
                int old = BaseNumber(target, ArmyDestructionResourceWriteKind.BaseWounded40);
                int first = Bound(unchecked(old + amount), Cap(target, ArmyDestructionResourceWriteKind.BaseWounded40));
                WriteBase(target, ArmyDestructionResourceWriteKind.BaseWounded40,
                    Bound(first, Cap(target, ArmyDestructionResourceWriteKind.BaseWounded40)));
            }
            void AddBaseResource(BaseReference target, ArmyDestructionResourceWriteKind kind, int amount)
            {
                int firstCap = Cap(target, kind), old = BaseNumber(target, kind);
                int first = Bound(unchecked(old + amount), firstCap);
                WriteBase(target, kind, Bound(first, Cap(target, kind)));
            }
            void AddLoot(BaseReference target)
            {
                AddBaseResource(target, ArmyDestructionResourceWriteKind.BaseGold34,
                    Percent(Get(Fields(victim).Gold5C, "current.victim_Army_gold5C"), P(SimulationParameterKeys.ArmyDestruction.DefeatedResourcesWinnerPercent)));
                AddBaseResource(target, ArmyDestructionResourceWriteKind.BaseSupplies38,
                    Percent(Get(Fields(victim).Supplies60, "current.victim_Army_supplies60"), P(SimulationParameterKeys.ArmyDestruction.DefeatedResourcesWinnerPercent)));
            }
            void WriteBaseMorale(BaseReference target, int requested)
            {
                var first = BaseForce(current, target); var second = BaseForce(current, target);
                if (unchecked((uint)(Get(current.ReadForcePhysicalSlot(second), "current.Base_morale_secondForce") - 46)) <= 4) return;
                WriteBase(target, ArmyDestructionResourceWriteKind.BaseMorale44,
                    unchecked((byte)Bound(requested, BasePostLoad.ReadMoraleUpper(first, current))));
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.DestructionResources_input: " + error.Message); }
    }

    /// <summary>Only prepared writes are applied, in original order. This
    /// operation-local packet has no mutable-RNG or second query/fingerprint.</summary>
    public static void ApplyContext(ArmyDestructionResourcePacket packet)
    {
        var current = packet.Owner;
        foreach (var write in packet.Writes)
            switch (write.Kind)
            {
                case ArmyDestructionResourceWriteKind.ArmyWounded18:
                case ArmyDestructionResourceWriteKind.ArmyMorale1A:
                    current.SetArmyReturnResourceFields(write.Army!.PhysicalSlot!.Value, write.ArmyFieldsAfter!); break;
                case ArmyDestructionResourceWriteKind.BaseWounded40:
                    current.SetArmyReturnBaseWounded40(write.Base!, write.Value); break;
                case ArmyDestructionResourceWriteKind.BaseMorale44:
                    current.SetBaseMorale44(write.Base!, unchecked((byte)write.Value)); break;
                case ArmyDestructionResourceWriteKind.BaseGold34:
                    current.SetBaseResource(write.Base!, BaseResourceKind.Gold, write.Value); break;
                case ArmyDestructionResourceWriteKind.BaseSupplies38:
                    current.SetBaseResource(write.Base!, BaseResourceKind.Supplies, write.Value); break;
            }
    }

    /// <summary>Resource-only saved publication. No retirement, pending70 or
    /// Person lifecycle is implied. Gate numeric saved owners remain outside
    /// the current owned City resource projection.</summary>
    public static GameState PublishResources(GameState state, ArmyDestructionResourcePacket packet)
    {
        var cities = new Dictionary<int, CityState>(state.Cities);
        var units = new Dictionary<int, UnitState>(state.Units);
        var arena = state.ReadArmyLifecycle() ?? throw new InvalidDataException("Current resource publication lacks its saved Army owner.");
        var slots = arena.ReceiverSlots().ToArray();
        foreach (var write in packet.Writes)
        {
            if (write.Base is { } target)
            {
                if (target.Kind != BaseKind.City || target.PhysicalSlot is not int physical ||
                    !cities.TryGetValue(physical, out var city) || city.NativeReturnRuntime is not { } native)
                    throw new InvalidDataException("Current destruction Base numeric saved owner is missing.");
                cities[physical] = write.Kind switch
                {
                    ArmyDestructionResourceWriteKind.BaseWounded40 => city with { NativeReturnRuntime = native with { Wounded40 = write.Value } },
                    ArmyDestructionResourceWriteKind.BaseMorale44 => city with { Morale = write.Value, NativeReturnRuntime = native with { Morale44 = unchecked((byte)write.Value) } },
                    ArmyDestructionResourceWriteKind.BaseGold34 => city with { Gold = write.Value },
                    _ => city with { Supplies = write.Value }
                };
            }
            else
            {
                int physical = write.Army!.PhysicalSlot!.Value;
                var slot = slots[physical];
                var army = slot.ActiveUnitId is int id ? units.GetValueOrDefault(id) : slot.PendingImage;
                if (army is null) throw new InvalidDataException("Current destruction Army numeric saved image is missing.");
                var next = write.Kind == ArmyDestructionResourceWriteKind.ArmyWounded18
                    ? army with { WoundedTroops = write.Value } : army with { NativeMorale1A = unchecked((byte)write.Value) };
                if (slot.ActiveUnitId is int active) units[active] = next;
                else slots[physical] = slot with { PendingImage = next };
            }
        }
        return (state with { Cities = cities,
                Units = units }).WithArmyLifecycle(arena.WithReceiverSlots(slots));
    }

    private static bool ArmyValid(SourceWorldContext current, ArmyUnitReference? army)
        => army?.PhysicalSlot is not null && Get(current.ReadArmyUnitNativeValidity(army), "current.Army_native_validity");
    private static bool BaseValid(SourceWorldContext current, BaseReference? target)
        => target?.PhysicalSlot is not null && Get(current.ReadBaseNativeValidity(target), "current.Base_native_validity");
    private static ForceReference BaseForce(SourceWorldContext current, BaseReference target)
    {
        int raw = Get(current.ReadBaseForceSlot(target), "current.Base_force_virtualB0");
        return current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("current.Base_Force_pointer");
    }
    private static int Percent(int value, int percent) => unchecked(value * percent) / 100;
    private static int Bound(int value, int upper) => value < 0 ? 0 : Math.Min(value, upper);
    private static T Get<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static MissingOwner Missing(string owner) => new(owner);
    private sealed class MissingOwner(string owner) : Exception(owner);
}
