using San14.Core;

namespace San14.Data;

public enum OriginalArmyDestructionResourceWriteKind
{ BaseWounded40, ArmyWounded18, BaseMorale44, ArmyMorale1A, BaseGold34, BaseSupplies38 }
public sealed record OriginalArmyDestructionResourceWrite(OriginalArmyDestructionResourceWriteKind Kind,
    OriginalBaseReference? Base, OriginalArmyUnitReference? Army, int Value)
{
    internal OriginalArmyReturnResourceFields? ArmyFieldsAfter { get; init; }
}
/// <summary>259360 captures these pointers before its Person callbacks. The
/// subsequent resource block must retain those captured receiver identities.</summary>
public sealed record OriginalArmyDestructionResourceOwners(OriginalBaseReference VictimHome,
    OriginalForceReference WinnerForce);
public sealed record OriginalArmyDestructionResourceResolution(OriginalArmyDestructionResourcePacket? Packet,
    string? UnresolvedOwner);
public sealed class OriginalArmyDestructionResourcePacket
{
    public IReadOnlyList<OriginalArmyDestructionResourceWrite> Writes { get; }
    public OriginalArmyDestructionResourceOwners CapturedOwners { get; }
    internal OriginalEventInitializationContext Owner { get; }
    internal OriginalArmyDestructionResourcePacket(OriginalEventInitializationContext owner,
        OriginalArmyDestructionResourceOwners captured, IEnumerable<OriginalArmyDestructionResourceWrite> writes)
    { Owner = owner; CapturedOwners = captured; Writes = Array.AsReadOnly(writes.ToArray()); }
}

/// <summary>Only259360's resource block: home/winner wounds,
/// winner morale and Base loot. Its independent saved parameters/current
/// receivers are consumed without RNG, retirement or whole29F220 execution.</summary>
public static class OriginalArmyDestructionResources
{
    public static OriginalArmyDestructionResourceOwners CaptureOwners(OriginalEventInitializationContext current,
        OriginalArmyUnitReference victim, OriginalArmyUnitReference? winner = null, OriginalBaseReference? winnerBase = null)
    {
        try
        {
            var force = new OriginalForceReference(null);
            if (ArmyValid(current, winner)) force = current.ReadArmyUnitForceReference(winner!);
            else if (BaseValid(current, winnerBase)) force = BaseForce(current, winnerBase!);
            return new(current.FindArmyUnitCurrentBase(victim), force);
        }
        catch (MissingOwner error) { throw new InvalidDataException(error.Message, error); }
    }

    public static OriginalArmyDestructionResourceResolution Prepare(GameState state,
        OriginalEventInitializationContext current, OriginalArmyUnitReference victim,
        OriginalArmyUnitReference? winner = null, OriginalBaseReference? winnerBase = null,
        OriginalArmyDestructionResourceOwners? capturedOwners = null)
    {
        var writes = new List<OriginalArmyDestructionResourceWrite>();
        var bases = new Dictionary<(OriginalBaseReference Base, OriginalArmyDestructionResourceWriteKind Kind), int>();
        var armies = new Dictionary<OriginalArmyUnitReference, OriginalArmyReturnResourceFields>();
        try
        {
            if (!ArmyValid(current, victim)) throw Missing("branch.resource_victim_invalid");
            var captured = capturedOwners ?? CaptureOwners(current, victim, winner, winnerBase);
            int cc = Get(OriginalBattleRemakeCurrentSettings.ReadInt32(state, 0xcc), "current.settings_CC");
            int selector = Get(OriginalBattleRemakeCurrentSettings.ReadSelectorE58(state), "current.settings_E58");
            bool woundBlock = selector != 0 && cc == 0;
            if (selector == 0) woundBlock = Advanced7() > 0;

            if (woundBlock && ArmyValid(current, victim) && BaseValid(current, captured.VictimHome))
            {
                int homePercent = P(0x1a4b480), winnerPercent = P(0x1a4b48c);
                if ((ArmyValid(current, winner) && current.IsArmyUnitBoundToSpecialForce(winner!)) ||
                    (BaseValid(current, winnerBase) && SpecialBase(winnerBase!)))
                { homePercent = P(0x1a4b498); winnerPercent = P(0x1a4b4a4); }
                int homeWound = Percent(Wounded(victim), homePercent);
                if (selector == 0) homeWound = Math.Min(Percent(homeWound, Advanced7()), TotalVictim());
                AddBaseWounded(captured.VictimHome, homeWound);

                int callerMoraleCap = ForceValid(captured.WinnerForce)
                    ? OriginalBasePostLoad.ReadMoraleUpper(captured.WinnerForce, current) : 100;
                if (ArmyValid(current, winner))
                {
                    if (selector == 0)
                    {
                        int incoming = Math.Min(Percent(unchecked(Ready(victim) + Percent(Wounded(victim), winnerPercent)), Advanced7()), TotalVictim());
                        WriteArmy(winner!, OriginalArmyDestructionResourceWriteKind.ArmyWounded18,
                            Bound(unchecked(Wounded(winner!) + incoming), 20000));
                    }
                    else
                    {
                        WriteArmy(winner!, OriginalArmyDestructionResourceWriteKind.ArmyWounded18,
                            (int)Math.Min(unchecked((uint)Wounded(winner!) + (uint)Ready(victim)), 20000u));
                        WriteArmy(winner!, OriginalArmyDestructionResourceWriteKind.ArmyWounded18,
                            Bound(unchecked(Wounded(winner!) + Percent(Wounded(victim), winnerPercent)), 20000));
                    }
                    int panel41 = ForceValid(captured.WinnerForce)
                        ? Get(current.ReadBaseForceModifier(captured.WinnerForce, 0x29), "current.winner_Force_panel41") : 0;
                    int getterCap = OriginalBasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(winner!), current);
                    int requested = Math.Min(unchecked(Math.Min(Morale(winner!), getterCap) + 10 + panel41), callerMoraleCap);
                    int setterCap = OriginalBasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(winner!), current);
                    WriteArmy(winner!, OriginalArmyDestructionResourceWriteKind.ArmyMorale1A,
                        unchecked((byte)Bound(requested, setterCap)));
                }
                else if (BaseValid(current, winnerBase) && Get(current.ReadBaseObjectLink(winnerBase!), "current.winner_Base4E") < 52)
                {
                    if (selector == 0)
                        AddBaseWounded(winnerBase!, Math.Min(Percent(unchecked(Ready(victim) + Percent(Wounded(victim), winnerPercent)), Advanced7()), TotalVictim()));
                    else
                    { AddBaseWounded(winnerBase!, Ready(victim)); AddBaseWounded(winnerBase!, Percent(Wounded(victim), winnerPercent)); }
                    WriteBaseMorale(winnerBase!, Math.Min(unchecked(BaseNumber(winnerBase!, OriginalArmyDestructionResourceWriteKind.BaseMorale44) + 10), callerMoraleCap));
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
                        foreach (var item in new[] { (0x6d, OriginalArmyDestructionResourceWriteKind.BaseGold34),
                            (0x6e, OriginalArmyDestructionResourceWriteKind.BaseSupplies38) })
                        {
                            if (!Get(current.ReadPersonNativeValidity(commander), "current.winner_Commander_validity")) continue;
                            if (!Get(OriginalTacticRemakeCurrentTraits.CommanderHasEffect(current, commander, item.Item1), "current.Commander_presence2C5830")) continue;
                            int amount = Get(OriginalTacticRemakeCurrentTraits.CommanderEffectSum(current, commander, item.Item1), "current.Commander_sum2B3170");
                            AddBaseResource(home, item.Item2, amount);
                        }
                    }
                }
            }
            else if (BaseValid(current, winnerBase))
            {
                var ownerCity = OriginalCurrentGateMapQueries.FindCity(winnerBase!, current); //22A940.
                if (BaseValid(current, ownerCity)) AddLoot(winnerBase!);
            }
            return new(new(current, captured, writes), null);

            int P(int tcRva) => Get(OriginalArmyDestructionParameterSeed.ReadCurrentTc(state, tcRva), $"current.destruction_parameter{tcRva:X}");
            int Advanced7() => Get(OriginalBattleRemakeCurrentSettings.ReadAdvancedParameter(state, 7), "current.settings_advanced7");
            bool ForceValid(OriginalForceReference force) => force.PhysicalSlot.HasValue && Get(current.ReadForceNativeValidity(force), "current.winner_Force_validity");
            bool OrdinaryForce(OriginalForceReference force)
            {
                int first = Get(current.ReadForcePhysicalSlot(force), "current.winner_Force_physical_first");
                if (first == 51) return false;
                int repeated = Get(current.ReadForcePhysicalSlot(force), "current.winner_Force_physical_repeated");
                return unchecked((uint)(repeated - 46)) > 4;
            }
            bool SpecialBase(OriginalBaseReference target)
            {
                ushort id = Get(current.ReadArmyReturnBaseGovernor2E(target), "current.winner_Base_governor2E");
                var person = Person(id);
                if (person.PhysicalSlot is not int physical) return false;
                ushort first = Get(current.ReadPersonFirstLocation(physical), "current.winner_BaseGovernor_E2");
                if (first == 0 || unchecked((uint)(first - 1)) >= 61) return false;
                int force = Get(current.ReadForcePhysicalSlot(BaseForce(current, target)), "current.winner_BaseForce_physical");
                return unchecked((uint)(force - 46)) <= 4;
            }
            OriginalTransferPersonReference Person(ushort id) => (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("current.Commander_ID_lookup");
            int Commander(OriginalArmyUnitReference army) => Person(Get(current.ReadArmyUnitCommanderId(army), "current.Commander12")).PhysicalSlot
                ?? throw Missing("current.Commander_pointer");
            OriginalArmyReturnResourceFields Fields(OriginalArmyUnitReference army)
            {
                if (armies.TryGetValue(army, out var saved)) return saved;
                var fields = current.ReadArmyReturnResourceFields(army) ?? throw Missing("current.Army_resource_fields");
                armies.Add(army, fields); return fields;
            }
            int Ready(OriginalArmyUnitReference army) => Get(Fields(army).Ready16, "current.Army_ready16");
            int Wounded(OriginalArmyUnitReference army) => Get(Fields(army).Wounded18, "current.Army_wounded18");
            int Morale(OriginalArmyUnitReference army) => Get(Fields(army).Morale1A, "current.Army_morale1A");
            int TotalVictim() => unchecked(Ready(victim) + Wounded(victim));
            void WriteArmy(OriginalArmyUnitReference army, OriginalArmyDestructionResourceWriteKind kind, int value)
            {
                var fields = kind == OriginalArmyDestructionResourceWriteKind.ArmyWounded18
                    ? Fields(army) with { Wounded18 = unchecked((ushort)value) }
                    : Fields(army) with { Morale1A = unchecked((byte)value) };
                armies[army] = fields;
                writes.Add(new(kind, null, army, value) { ArmyFieldsAfter = fields });
            }
            int BaseNumber(OriginalBaseReference target, OriginalArmyDestructionResourceWriteKind kind)
            {
                if (bases.TryGetValue((target, kind), out int value)) return value;
                return kind switch
                {
                    OriginalArmyDestructionResourceWriteKind.BaseWounded40 => Get(current.ReadArmyReturnBaseWounded40(target), "current.Base_wounded40"),
                    OriginalArmyDestructionResourceWriteKind.BaseMorale44 => Get(current.ReadBaseMorale44(target), "current.Base_morale44"),
                    OriginalArmyDestructionResourceWriteKind.BaseGold34 => Get(current.ReadBaseResource(target, OriginalBaseResourceKind.Gold), "current.Base_gold34"),
                    _ => Get(current.ReadBaseResource(target, OriginalBaseResourceKind.Supplies), "current.Base_supplies38")
                };
            }
            void WriteBase(OriginalBaseReference target, OriginalArmyDestructionResourceWriteKind kind, int value)
            { bases[(target, kind)] = value; writes.Add(new(kind, target, null, value)); }
            int Cap(OriginalBaseReference target, OriginalArmyDestructionResourceWriteKind kind)
            {
                var caps = OriginalBasePostLoad.ReadResourceCapacities(target, current);
                return kind switch { OriginalArmyDestructionResourceWriteKind.BaseWounded40 => caps.Troops,
                    OriginalArmyDestructionResourceWriteKind.BaseGold34 => caps.Gold, _ => caps.Supplies };
            }
            void AddBaseWounded(OriginalBaseReference target, int amount)
            {
                int old = BaseNumber(target, OriginalArmyDestructionResourceWriteKind.BaseWounded40);
                int first = Bound(unchecked(old + amount), Cap(target, OriginalArmyDestructionResourceWriteKind.BaseWounded40));
                WriteBase(target, OriginalArmyDestructionResourceWriteKind.BaseWounded40,
                    Bound(first, Cap(target, OriginalArmyDestructionResourceWriteKind.BaseWounded40)));
            }
            void AddBaseResource(OriginalBaseReference target, OriginalArmyDestructionResourceWriteKind kind, int amount)
            {
                int firstCap = Cap(target, kind), old = BaseNumber(target, kind);
                int first = Bound(unchecked(old + amount), firstCap);
                WriteBase(target, kind, Bound(first, Cap(target, kind)));
            }
            void AddLoot(OriginalBaseReference target)
            {
                AddBaseResource(target, OriginalArmyDestructionResourceWriteKind.BaseGold34,
                    Percent(Get(Fields(victim).Gold5C, "current.victim_Army_gold5C"), P(0x1a4b48c)));
                AddBaseResource(target, OriginalArmyDestructionResourceWriteKind.BaseSupplies38,
                    Percent(Get(Fields(victim).Supplies60, "current.victim_Army_supplies60"), P(0x1a4b48c)));
            }
            void WriteBaseMorale(OriginalBaseReference target, int requested)
            {
                var first = BaseForce(current, target); var second = BaseForce(current, target);
                if (unchecked((uint)(Get(current.ReadForcePhysicalSlot(second), "current.Base_morale_secondForce") - 46)) <= 4) return;
                WriteBase(target, OriginalArmyDestructionResourceWriteKind.BaseMorale44,
                    unchecked((byte)Bound(requested, OriginalBasePostLoad.ReadMoraleUpper(first, current))));
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.DestructionResources_input: " + error.Message); }
    }

    /// <summary>Only prepared writes are applied, in original order. This
    /// operation-local packet has no mutable-RNG or second query/fingerprint.</summary>
    public static void ApplyContext(OriginalArmyDestructionResourcePacket packet)
    {
        var current = packet.Owner;
        foreach (var write in packet.Writes)
            switch (write.Kind)
            {
                case OriginalArmyDestructionResourceWriteKind.ArmyWounded18:
                case OriginalArmyDestructionResourceWriteKind.ArmyMorale1A:
                    current.SetArmyReturnResourceFields(write.Army!.PhysicalSlot!.Value, write.ArmyFieldsAfter!); break;
                case OriginalArmyDestructionResourceWriteKind.BaseWounded40:
                    current.SetArmyReturnBaseWounded40(write.Base!, write.Value); break;
                case OriginalArmyDestructionResourceWriteKind.BaseMorale44:
                    current.SetBaseMorale44(write.Base!, unchecked((byte)write.Value)); break;
                case OriginalArmyDestructionResourceWriteKind.BaseGold34:
                    current.SetBaseResource(write.Base!, OriginalBaseResourceKind.Gold, write.Value); break;
                case OriginalArmyDestructionResourceWriteKind.BaseSupplies38:
                    current.SetBaseResource(write.Base!, OriginalBaseResourceKind.Supplies, write.Value); break;
            }
    }

    /// <summary>Resource-only saved publication. No retirement, pending70 or
    /// Person lifecycle is implied. Gate numeric saved owners remain outside
    /// the current owned City resource projection.</summary>
    public static GameState PublishResources(GameState state, OriginalArmyDestructionResourcePacket packet)
    {
        var cities = new Dictionary<int, CityState>(state.Cities);
        var units = new Dictionary<int, UnitState>(state.Units);
        var arena = state.NativeArmyLifecycle ?? throw new InvalidDataException("Current resource publication lacks its saved Army owner.");
        var slots = arena.Slots.ToArray();
        foreach (var write in packet.Writes)
        {
            if (write.Base is { } target)
            {
                if (target.Kind != OriginalBaseKind.City || target.PhysicalSlot is not int physical ||
                    !cities.TryGetValue(physical, out var city) || city.NativeReturnRuntime is not { } native)
                    throw new InvalidDataException("Current destruction Base numeric saved owner is missing.");
                cities[physical] = write.Kind switch
                {
                    OriginalArmyDestructionResourceWriteKind.BaseWounded40 => city with { NativeReturnRuntime = native with { Wounded40 = write.Value } },
                    OriginalArmyDestructionResourceWriteKind.BaseMorale44 => city with { Morale = write.Value, NativeReturnRuntime = native with { Morale44 = unchecked((byte)write.Value) } },
                    OriginalArmyDestructionResourceWriteKind.BaseGold34 => city with { Gold = write.Value },
                    _ => city with { Supplies = write.Value }
                };
            }
            else
            {
                int physical = write.Army!.PhysicalSlot!.Value;
                var slot = slots[physical];
                var army = slot.ActiveUnitId is int id ? units.GetValueOrDefault(id) : slot.PendingImage;
                if (army is null) throw new InvalidDataException("Current destruction Army numeric saved image is missing.");
                var next = write.Kind == OriginalArmyDestructionResourceWriteKind.ArmyWounded18
                    ? army with { WoundedTroops = write.Value } : army with { NativeMorale1A = unchecked((byte)write.Value) };
                if (slot.ActiveUnitId is int active) units[active] = next;
                else slots[physical] = slot with { PendingImage = next };
            }
        }
        return state with { Cities = cities, Units = units, NativeArmyLifecycle = arena with { Slots = slots } };
    }

    private static bool ArmyValid(OriginalEventInitializationContext current, OriginalArmyUnitReference? army)
        => army?.PhysicalSlot is not null && Get(current.ReadArmyUnitNativeValidity(army), "current.Army_native_validity");
    private static bool BaseValid(OriginalEventInitializationContext current, OriginalBaseReference? target)
        => target?.PhysicalSlot is not null && Get(current.ReadBaseNativeValidity(target), "current.Base_native_validity");
    private static OriginalForceReference BaseForce(OriginalEventInitializationContext current, OriginalBaseReference target)
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
