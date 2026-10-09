namespace San14.Data;

public sealed record OriginalArmyReturnResourceResolution(OriginalArmyReturnResourcePacket? Packet, string? UnresolvedOwner);
public sealed class OriginalArmyReturnResourcePacket
{
    public OriginalArmyUnitReference Army { get; }
    public OriginalBaseReference Base { get; }
    public byte Morale44After { get; }
    public int Ready3CAfter { get; }
    public int Wounded40After { get; }
    public int Gold34After { get; }
    public int Supplies38After { get; }
    internal OriginalArmyReturnResourcePacket(OriginalArmyUnitReference army, OriginalBaseReference target,
        byte morale, int ready, int wounded, int gold, int supplies)
    { Army = army; Base = target; Morale44After = morale; Ready3CAfter = ready; Wounded40After = wounded; Gold34After = gold; Supplies38After = supplies; }
}

/// <summary>Ordinary status1 resource block29F483..29F6E2. Actual current
/// fields and existing complete capacity/morale producers determine results.
/// Unknown current inputs refuse the whole packet; this is a resource stage,
/// not authority that earlier traits/Home or later pending/Hex already ran.</summary>
public static class OriginalArmyReturnResourceUpdater
{
    internal static OriginalArmyReturnResourceResolution ApplyCurrentResourceOutputs(OriginalArmyUnitReference army,
        OriginalBaseReference target, OriginalEventInitializationContext context,
        Func<OriginalEventInitializationContext, int, string?>? transportMerit = null)
    {
        ArgumentNullException.ThrowIfNull(army); ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(context);
        try
        {
            if (!Get(context.ReadArmyUnitNativeValidity(army), "current.Army_validity")) throw Missing("branch.invalid_army");
            byte status = Get(context.ReadArmyReturnUnitStatus10(army), "current.Army_status10");
            if (status != 1 && !(status == 2 && transportMerit is not null))
                throw Missing("branch.return_resources_transport_merit");
            ushort commanderId = Get(context.ReadArmyUnitCommanderId(army), "current.Army_commander12");
            int commander = (context.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)
                ?? throw Missing("current.Commander_lookup")).PhysicalSlot ?? throw Missing("current.Commander_pointer");
            if (!Get(context.ReadPersonNativeValidity(commander), "current.Commander_validity")) throw Missing("branch.invalid_commander");
            byte forceSlot = Get(context.ReadPersonOldForceSlot(commander), "current.Army_force22CD80");
            int baseForce = Get(context.ReadBaseForceSlot(target), "current.Base_virtualB0");
            if (forceSlot != baseForce || forceSlot is < 1 or > 45) throw Missing("branch.return_resources_nonordinary_force");
            var force = context.ReadForceTableReference(forceSlot) ?? throw Missing("current.Force_table");
            var fields = context.ReadArmyReturnResourceFields(army) ?? throw Missing("current.Army_return_resource_fields");
            ushort ready = Get(fields.Ready16, "current.Army_ready16"), wounded = Get(fields.Wounded18, "current.Army_wounded18");
            byte morale = Get(fields.Morale1A, "current.Army_morale1A");
            int incomingGold = Get(fields.Gold5C, "current.Army_gold5C"), incomingSupplies = Get(fields.Supplies60, "current.Army_supplies60");
            int oldReady = Get(context.ReadBaseResource(target, OriginalBaseResourceKind.Troops), "current.Base_ready3C");
            int oldWounded = Get(context.ReadArmyReturnBaseWounded40(target), "current.Base_wounded40");
            byte oldMorale = Get(context.ReadBaseMorale44(target), "current.Base_morale44");
            int armyCap = OriginalBasePostLoad.ReadMoraleUpper(force, context); //22EB30→2690D0.
            int mixed = oldMorale;
            if (ready != 0)
            {
                int numerator = unchecked(unchecked(Math.Min((int)morale, armyCap) * ready) + unchecked(oldMorale * oldReady));
                int denominator = unchecked(ready + oldReady);
                float quotient = (float)numerator / (float)denominator;
                //TC101E8A8 /SC101F7A8 truncates first to half units, then to
                //whole units and adds sign-dependent±1 for the half remainder.
                //It is roundf(midpoints away from zero), not the clock's floor.
                float rounded = float.IsFinite(quotient) ? MathF.Round(quotient, MidpointRounding.AwayFromZero) : quotient;
                mixed = !float.IsFinite(rounded) || rounded < int.MinValue || rounded >= 2147483648f ? int.MinValue : (int)rounded;
            }
            int freshCap = OriginalBasePostLoad.ReadMoraleUpper(force, context); //22C990→22EBB0.
            int requestedMorale = Math.Min(mixed, freshCap);
            var draft = context;
            OriginalBasePostLoad.WriteMorale(target, requestedMorale, draft); //241790 fresh Force/cap.
            byte afterMorale = Get(draft.ReadBaseMorale44(target), "current.Base_morale_setter_result");
            if (status == 2)
            {
                int accepted = Math.Min((int)ready, unchecked(Cap().Troops - oldReady));
                if (Get(draft.ReadBaseObjectLink(target), "current.transport_Base4E") < 52)
                {
                    int gold = Get(draft.ReadBaseResource(target, OriginalBaseResourceKind.Gold), "current.transport_BaseGold");
                    int supplies = Get(draft.ReadBaseResource(target, OriginalBaseResourceKind.Supplies), "current.transport_BaseSupplies");
                    accepted = unchecked(accepted + Math.Min(incomingGold, unchecked(Cap().Gold - gold)) +
                        Math.Min(incomingSupplies, unchecked(Cap().Supplies - supplies)));
                }
                if (transportMerit!(draft, accepted) is string unresolved) throw Missing(unresolved);
            }
            int readyCap1 = Cap().Troops;
            int afterReady = Bound(unchecked(oldReady + ready), readyCap1);
            int readyCap2 = Cap().Troops; afterReady = Bound(afterReady, readyCap2);
            int woundedCap1 = Cap().Troops;
            int afterWounded = Bound(unchecked(oldWounded + wounded), woundedCap1);
            int woundedCap2 = Cap().Troops; afterWounded = Bound(afterWounded, woundedCap2);
            int goldCap1 = CurrencyCap().Gold;
            int oldGold = Get(context.ReadBaseResource(CurrencyBase(context), OriginalBaseResourceKind.Gold), "current.Base_virtual90_gold");
            int goldRequest = Bound(unchecked(oldGold + incomingGold), goldCap1);
            int goldCap2 = CurrencyCap().Gold; int afterGold = Bound(goldRequest, goldCap2); //virtual98 setter.
            int suppliesCap1 = CurrencyCap().Supplies;
            int oldSupplies = Get(context.ReadBaseResource(CurrencyBase(context), OriginalBaseResourceKind.Supplies), "current.Base_virtualA0_supplies");
            int suppliesRequest = Bound(unchecked(oldSupplies + incomingSupplies), suppliesCap1);
            int suppliesCap2 = CurrencyCap().Supplies; int afterSupplies = Bound(suppliesRequest, suppliesCap2); //virtualA8 setter.
            var packet = new OriginalArmyReturnResourcePacket(army, target, afterMorale, afterReady, afterWounded,
                afterGold, afterSupplies);
            WriteCurrent(context, packet);
            return new(packet, null);
            OriginalBaseResourceCapacities Cap() => OriginalBasePostLoad.ReadResourceCapacities(target, draft);
            OriginalBaseReference CurrencyBase(OriginalEventInitializationContext current) =>
                target.Kind == OriginalBaseKind.City ? target : OriginalCurrentGateMapQueries.FindCity(target, current);
            OriginalBaseResourceCapacities CurrencyCap() => OriginalBasePostLoad.ReadResourceCapacities(CurrencyBase(draft), draft);
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.ReturnResources_input: " + error.Message); }
    }
    private static void WriteCurrent(OriginalEventInitializationContext context, OriginalArmyReturnResourcePacket packet)
    {
        context.SetBaseResource(packet.Base, OriginalBaseResourceKind.Troops, packet.Ready3CAfter);
        context.SetArmyReturnBaseWounded40(packet.Base, packet.Wounded40After);
        //Gate2416F0/241380 independently reacquire mapped City22A940.
        var goldBase = packet.Base.Kind == OriginalBaseKind.City ? packet.Base : OriginalCurrentGateMapQueries.FindCity(packet.Base, context);
        context.SetBaseResource(goldBase, OriginalBaseResourceKind.Gold, packet.Gold34After);
        var suppliesBase = packet.Base.Kind == OriginalBaseKind.City ? packet.Base : OriginalCurrentGateMapQueries.FindCity(packet.Base, context);
        context.SetBaseResource(suppliesBase, OriginalBaseResourceKind.Supplies, packet.Supplies38After);
    }
    private static int Bound(int value, int upper) => value < 0 ? 0 : Math.Min(value, upper);
    private static T Get<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static MissingOwner Missing(string value) => new(value);
    private sealed class MissingOwner(string value) : Exception(value);
}
