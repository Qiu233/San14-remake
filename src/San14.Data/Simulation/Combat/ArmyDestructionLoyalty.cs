using San14.Core;

namespace San14.Data;

public sealed record ArmyDestructionLoyaltyResolution(ArmyDestructionLoyaltyPacket? Packet,
    string? UnresolvedOwner);
public sealed class ArmyDestructionLoyaltyPacket
{
    public int PersonPhysical { get; }
    public byte? BeforeE8 { get; }
    public byte? AfterE8 { get; }
    public int? PenaltyInt32 { get; }
    public byte? PenaltyLowByte { get; }
    public string Branch { get; }
    public bool WritesLoyalty => AfterE8.HasValue;
    internal SourceWorldContext Owner { get; }
    internal WorldIndexContext? PoolOwner { get; }
    internal WorldIndexContext? PreparedPool { get; }
    internal ArmyDestructionLoyaltyPacket(SourceWorldContext owner, int physical,
        byte? before, byte? after, int? penalty, byte? lowByte, string branch,
        WorldIndexContext? poolOwner = null, WorldIndexContext? preparedPool = null)
    { Owner = owner; PersonPhysical = physical; BeforeE8 = before; AfterE8 = after;
        PenaltyInt32 = penalty; PenaltyLowByte = lowByte; Branch = branch;
        PoolOwner = poolOwner; PreparedPool = preparedPool; }
}

/// <summary>259360's TC PersonE8 loyalty tail.232500/232CB0 are Force51/
///46..50 predicates, not a ruler-role guard. Raw250/255 remains independent
///from the display's ordinary0..100 loyalty range.</summary>
public static class ArmyDestructionLoyalty
{
    public static ArmyDestructionLoyaltyResolution Prepare(GameState state,
        SourceWorldContext current, int personPhysical,
        OwnedArmyGovernorComparatorInputs governorInputs, ForceReference? capturedVictimForce = null)
    {
        try
        {
            var force = current.ReadPersonForce(personPhysical) ?? throw Missing("current.Person_Force22CB40");
            if (Get(current.ReadForcePhysicalSlot(force), "current.232500_Force_physical") == 51)
                return Skip("source.232500_force51");
            force = current.ReadPersonForce(personPhysical) ?? throw Missing("current.Person_Force22CB40_repeated");
            if (unchecked((uint)(Get(current.ReadForcePhysicalSlot(force), "current.232CB0_Force_physical") - 46)) <= 4)
                return Skip("source.232CB0_force46..50");
            if (!Get(current.ReadPersonNativeValidity(personPhysical), "current.Person_native_validity"))
                return Skip("source.invalid_person");
            //local13F8 was captured before259360's Person callbacks. Ordinary
            //standalone use captures the current Force in this entry scope.
            var originalForce = capturedVictimForce ?? force;
            int forcePhysical = Get(current.ReadForcePhysicalSlot(originalForce), "current.captured_victim_Force_physical");
            ushort rulerId = current.ReadForceRecord(forcePhysical)?.RawRuler10
                ?? throw Missing("current.captured_victim_Force_rawRuler10");
            var ruler = rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId) : current.ReadTransferPersonTableReference(0);
            if (ruler is null) throw Missing("current.captured_victim_ruler_ID_lookup");
            if (ruler.PhysicalSlot is not int rulerPhysical || !Get(current.ReadPersonNativeValidity(rulerPhysical), "current.ruler_native_validity"))
                return Skip("source.invalid_ruler");
            var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("current.owned_primary_shared_pool");
            //2A6FA0 has real temporary-list side effects even for same Person
            //and even when flags15E bit13 ultimately assigns255. Prepare them
            //in one detached copy; unknown inputs discard this operation.
            var draft = current.CreateArmyReturnTransactionDraft();
            var preparedPool = pool.DetachForArmyReturn(draft);
            draft.DestructionRelationTier2A6EE0Reader = current.DestructionRelationTier2A6EE0Reader;
            byte relation = Get(draft.ReadDestructionRelationTier2A6EE0(personPhysical, rulerPhysical, 0), "current.relationship2A6EE0");
            byte raw = Get(ReadCurrentRawE8(state, current, personPhysical, governorInputs), "current.Person_rawE8");
            int disposition = Get(current.ReadAllegianceDisposition(personPhysical), "current.Person_allegiance_disposition");
            int loyaltyDivisor = P(SimulationParameterKeys.ArmyDestruction.LoyaltyLossLoyaltyDivisor), wordDivisor = P(SimulationParameterKeys.ArmyDestruction.LoyaltyLossPersonWordDivisor), relationBase = P(SimulationParameterKeys.ArmyDestruction.LoyaltyLossRelationBase);
            float first = (((float)disposition - 20f) * -1f) / (float)wordDivisor;
            float second = (100f - (float)raw) / (float)loyaltyDivisor;
            float requested = (first + second) + (float)unchecked(relationBase - relation);
            int nativeInt = TruncateCvtt(requested);
            int penalty = nativeInt < 1 ? 0 : nativeInt;
            byte low = unchecked((byte)penalty); //Source takes BYTE of local int, not a255 clamp.
            byte after = unchecked((byte)Math.Clamp((int)raw - low, 0, 255));
            bool strong = Get(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(personPhysical)), "current.Person_flags15E");
            if (strong) after = 255;
            return new(new(current, personPhysical, raw, after, penalty, low,
                strong ? "source.flags15E_bit13_output" : "source.numeric_E8", pool, preparedPool), null);

            int P(string key) => Get(ArmyDestructionParameterSeed.ReadCurrent(state, key), $"current.destruction_parameter{key}");
            ArmyDestructionLoyaltyResolution Skip(string branch) => new(new(current, personPhysical, null, null, null, null, branch), null);
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.DestructionLoyalty_input: " + error.Message); }
    }

    public static void ApplyContext(ArmyDestructionLoyaltyPacket packet)
    {
        if (packet.PreparedPool is { } prepared) packet.PoolOwner!.CommitArmyReturnPool(prepared);
        if (packet.AfterE8 is byte after) packet.Owner.SetPersonDestructionLoyaltyE8(packet.PersonPhysical, after);
    }

    public static GameState PublishCurrent(GameState state, ArmyDestructionLoyaltyPacket packet)
        => PublishCurrent(state, packet, projectOwnedRoles: false);

    internal static GameState PublishCurrent(GameState state, ArmyDestructionLoyaltyPacket packet,
        bool projectOwnedRoles)
    {
        if (packet.AfterE8 is not byte after) return state;
        GameState published = PublishCurrentRawE8(state, packet.Owner, packet.PersonPhysical, after);
        return published.WithArmyLifecycle(packet.PoolOwner is { } pool
                ? (state.ReadArmyLifecycle() ?? throw new InvalidDataException("Current loyalty pool has no saved lifecycle owner."))
                    with { Indexes = projectOwnedRoles ? pool.ProjectOwnedRoles() : pool.ExportOwnedState() }
                : state.ReadArmyLifecycle());
    }

    /// <summary>Publish one actual raw-E8 write to its existing saved owner.
    /// The caller performs the current-context write at its source point;
    /// this mirror does not run destruction arithmetic or export a live pool.</summary>
    internal static GameState PublishCurrentRawE8(GameState state, SourceWorldContext current,
        int physical, byte after)
    {
        ushort id = current.ReadPersonIdentity(physical)
            ?? throw new InvalidDataException("Current loyalty Person identity is unknown.");
        //Ordinary160 has no native-validity gate and may write the real
        //table-zero Person. Its raw byte already has an independent saved
        //owner; retain that field without inventing a displayed Officer0.
        if (physical == 0 && id == 0 && state.NativeArmySimulation is { } zeroSimulation &&
            zeroSimulation.Parameters.Int32Values.GetValueOrDefault(OwnedArmyGovernorComparatorInputs.VersionKey) == 1 &&
            zeroSimulation.Parameters.Int32Values.ContainsKey(OwnedArmyGovernorComparatorInputs.PersonRawE8Key(physical)))
            return state with { NativeArmySimulation = zeroSimulation with { Parameters = zeroSimulation.Parameters with
            { Int32Values = new(zeroSimulation.Parameters.Int32Values)
                { [OwnedArmyGovernorComparatorInputs.PersonRawE8Key(physical)] = after } } } };
        if (!state.Officers.TryGetValue(id, out var person) || current.ResolveOfficerSource(id) != physical ||
            state.NativeArmySimulation is not { } simulation ||
            simulation.Parameters.Int32Values.GetValueOrDefault(OwnedArmyGovernorComparatorInputs.VersionKey) != 1)
            throw new InvalidDataException("Current loyalty publication lacks its saved raw-E8 owner.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
        { [OwnedArmyGovernorComparatorInputs.PersonRawE8Key(physical)] = after };
        return state with { Officers = new(state.Officers) { [id] = person with { Loyalty = after <= 100 ? after : null } },
            NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    internal static byte? ReadCurrentRawE8(GameState state, SourceWorldContext current, int physical,
        OwnedArmyGovernorComparatorInputs governor)
    {
        if (current.TryReadPersonDestructionLoyaltyE8(physical, out var field)) return field;
        //The raw E8 owner is shared with the governor module; its Office3D/37
        //fields are not inputs of259360. Preserve the same current Core write
        //precedence without asking the full comparator for an unrelated Office.
        ArgumentNullException.ThrowIfNull(governor);
        if (physical == 0 && current.ReadPersonIdentity(physical) == 0 &&
            state.NativeArmySimulation?.Parameters.Int32Values is { } zeroValues &&
            zeroValues.GetValueOrDefault(OwnedArmyGovernorComparatorInputs.VersionKey) == 1 &&
            zeroValues.TryGetValue(OwnedArmyGovernorComparatorInputs.PersonRawE8Key(physical), out int zeroRaw))
            return zeroRaw is >= 0 and <= 255 ? (byte)zeroRaw : null;
        if (state.NativeArmySimulation?.Parameters.Int32Values is not { } values ||
            values.GetValueOrDefault(OwnedArmyGovernorComparatorInputs.VersionKey) != 1 ||
            current.ReadPersonIdentity(physical) is not ushort id || !state.Officers.TryGetValue(id, out var person) ||
            current.ResolveOfficerSource(id) != physical) return null;
        if (person.Loyalty is int loyalty) return loyalty is >= 0 and <= 255 ? (byte)loyalty : null;
        return values.TryGetValue(OwnedArmyGovernorComparatorInputs.PersonRawE8Key(physical), out int raw) &&
            raw is >= 0 and <= 255 ? (byte)raw : null;
    }
    private static int TruncateCvtt(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f
        ? int.MinValue : (int)value;
    private static T Get<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static MissingOwner Missing(string owner) => new(owner);
    private sealed class MissingOwner(string owner) : Exception(owner);
}

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, byte?> _personDestructionLoyaltyE8 = [];
    public Func<SourceWorldContext, int, int, int, byte?>? DestructionRelationTier2A6EE0Reader { get; set; }
    public byte? ReadDestructionRelationTier2A6EE0(int person, int ruler, int mode)
        => DestructionRelationTier2A6EE0Reader?.Invoke(this, person, ruler, mode);
    public void SetPersonDestructionLoyaltyE8(int physical, byte? value)
    { Check(physical, 1851, "current Person loyaltyE8"); _personDestructionLoyaltyE8[physical] = value; }
    internal bool TryReadPersonDestructionLoyaltyE8(int physical, out byte? value)
    { Check(physical, 1851, "current Person loyaltyE8"); return _personDestructionLoyaltyE8.TryGetValue(physical, out value); }
}
