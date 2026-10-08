using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
    public bool UsesResourceOutputProjection { get; }
    internal OriginalEventInitializationContext Owner { get; }
    internal string Fingerprint { get; }
    internal OriginalArmyReturnResourcePacket(OriginalArmyUnitReference army, OriginalBaseReference target,
        byte morale, int ready, int wounded, int gold, int supplies, OriginalEventInitializationContext owner, string fingerprint,
        bool resourceOutputProjection = false)
    { Army = army; Base = target; Morale44After = morale; Ready3CAfter = ready; Wounded40After = wounded; Gold34After = gold; Supplies38After = supplies; Owner = owner; Fingerprint = fingerprint; UsesResourceOutputProjection = resourceOutputProjection; }
}

/// <summary>Ordinary status1 resource block29F483..29F6E2. Actual current
/// fields and existing complete capacity/morale producers determine results.
/// Unknown current inputs refuse the whole packet; this is a resource stage,
/// not authority that earlier traits/Home or later pending/Hex already ran.</summary>
public static class OriginalArmyReturnResourceUpdater
{
    public static OriginalArmyReturnResourceResolution Prepare(OriginalArmyUnitReference army,
        OriginalBaseReference target, OriginalEventInitializationContext context) => PrepareCore(army, target, context, false);

    /// <summary>Same resource mathematics with the capacity helper's three
    /// consumed outputs. This does not prove the whole capacity function's
    /// fourth-output callbacks or the complete29F220/pool lifecycle.</summary>
    public static OriginalArmyReturnResourceResolution PrepareResourceOutputs(OriginalArmyUnitReference army,
        OriginalBaseReference target, OriginalEventInitializationContext context) => PrepareCore(army, target, context, true);

    internal static OriginalArmyReturnResourceResolution ApplyCurrentResourceOutputs(OriginalArmyUnitReference army,
        OriginalBaseReference target, OriginalEventInitializationContext context,
        Func<OriginalEventInitializationContext, int, string?>? transportMerit = null)
    {
        var result = PrepareCore(army, target, context, true, false, transportMerit);
        if (result.Packet is { } packet) WriteCurrent(packet);
        return result;
    }

    private static OriginalArmyReturnResourceResolution PrepareCore(OriginalArmyUnitReference army,
        OriginalBaseReference target, OriginalEventInitializationContext context, bool resourceOutputProjection,
        bool captureFingerprint = true, Func<OriginalEventInitializationContext, int, string?>? transportMerit = null)
    {
        ArgumentNullException.ThrowIfNull(army); ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(context);
        var reads = new List<object>();
        try
        {
            if (!Get(context.ReadArmyUnitNativeValidity(army), "current.Army_validity")) throw Missing("branch.invalid_army");
            byte status = Get(context.ReadArmyReturnUnitStatus10(army), "current.Army_status10");
            if (status != 1 && !(status == 2 && !captureFingerprint && transportMerit is not null))
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
            reads.Add(new { status, commanderId, commander, forceSlot, baseForce, force, fields, oldReady, oldWounded, oldMorale });
            int armyCap = OriginalBasePostLoad.ReadMoraleUpper(force, context); //22EB30→2690D0.
            reads.Add(new { armyCap });
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
                reads.Add(new { numerator, denominator, quotient = BitConverter.SingleToInt32Bits(quotient), rounded = BitConverter.SingleToInt32Bits(rounded), mixed });
            }
            int freshCap = OriginalBasePostLoad.ReadMoraleUpper(force, context); //22C990→22EBB0.
            int requestedMorale = Math.Min(mixed, freshCap);
            var draft = captureFingerprint ? context.CreateArmyReturnHomeDraft() : context;
            OriginalBasePostLoad.WriteMorale(target, requestedMorale, draft); //241790 fresh Force/cap.
            byte afterMorale = Get(draft.ReadBaseMorale44(target), "current.Base_morale_setter_result");
            reads.Add(new { freshCap, requestedMorale, afterMorale });
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
            reads.Add(new { readyCap1, readyCap2, woundedCap1, woundedCap2, oldGold, goldCap1, goldCap2, oldSupplies, suppliesCap1, suppliesCap2,
                afterReady, afterWounded, afterGold, afterSupplies });
            string fingerprint = captureFingerprint ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reads)))) : "";
            return new(new(army, target, afterMorale, afterReady, afterWounded, afterGold, afterSupplies, context, fingerprint, resourceOutputProjection), null);
            OriginalBaseResourceCapacities Cap()
            {
                if (resourceOutputProjection)
                { var projected = OriginalBasePostLoad.ReadResourceCapacities(target, draft); reads.Add(projected); return projected; }
                var result = OriginalBasePostLoad.ReadCapacities(target, draft); reads.Add(result);
                return new(result.Gold, result.Supplies, result.Troops);
            }
            OriginalBaseReference CurrencyBase(OriginalEventInitializationContext current) =>
                target.Kind == OriginalBaseKind.City ? target : OriginalCurrentGateMapQueries.FindCity(target, current);
            OriginalBaseResourceCapacities CurrencyCap()
            {
                var at = CurrencyBase(draft);
                if (resourceOutputProjection)
                { var projected = OriginalBasePostLoad.ReadResourceCapacities(at, draft); reads.Add(projected); return projected; }
                var result = OriginalBasePostLoad.ReadCapacities(at, draft); reads.Add(result);
                return new(result.Gold, result.Supplies, result.Troops);
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.ReturnResources_input: " + error.Message); }
    }
    public static bool ApplyPrepared(OriginalArmyReturnResourcePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var current = PrepareCore(packet.Army, packet.Base, packet.Owner, packet.UsesResourceOutputProjection).Packet;
        if (current is null || current.Fingerprint != packet.Fingerprint) return false;
        WriteCurrent(current); return true;
    }
    private static void WriteCurrent(OriginalArmyReturnResourcePacket packet)
    {
        packet.Owner.SetBaseMorale44(packet.Base, packet.Morale44After);
        packet.Owner.SetBaseResource(packet.Base, OriginalBaseResourceKind.Troops, packet.Ready3CAfter);
        packet.Owner.SetArmyReturnBaseWounded40(packet.Base, packet.Wounded40After);
        //Gate2416F0/241380 independently reacquire mapped City22A940.
        var goldBase = packet.Base.Kind == OriginalBaseKind.City ? packet.Base : OriginalCurrentGateMapQueries.FindCity(packet.Base, packet.Owner);
        packet.Owner.SetBaseResource(goldBase, OriginalBaseResourceKind.Gold, packet.Gold34After);
        var suppliesBase = packet.Base.Kind == OriginalBaseKind.City ? packet.Base : OriginalCurrentGateMapQueries.FindCity(packet.Base, packet.Owner);
        packet.Owner.SetBaseResource(suppliesBase, OriginalBaseResourceKind.Supplies, packet.Supplies38After);
    }
    private static int Bound(int value, int upper) => value < 0 ? 0 : Math.Min(value, upper);
    private static T Get<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static MissingOwner Missing(string value) => new(value);
    private sealed class MissingOwner(string value) : Exception(value);
}
