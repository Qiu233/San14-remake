using San14.Core;

namespace San14.Data;

/// <summary>Operation-local current entity views over saved world indexes.
/// Capturing retains registration order and pending receiver images rather
/// than rebuilding membership from entity validity.</summary>
public sealed class OwnedArmyReturnWorld
{
    public SourceWorldContext Context { get; }
    public WorldIndexContext Pool { get; }
    private readonly GameState _input;
    private readonly bool _borrowed;
    private OwnedArmyReturnWorld(GameState state, SourceWorldContext context, WorldIndexContext pool, bool borrowed = false)
    { _input = state; _borrowed = borrowed;
        Context = context; Pool = pool; }

    public static OwnedArmyReturnWorld Capture(GameState state, ArmyOperationContext definitions)
    {
        var saved = state.ReadArmyLifecycle() ?? throw new InvalidDataException("current.complete_saved_Army_arena is unknown.");
        if (saved.ReceiverSlots().Length != 501) throw new InvalidDataException("CE native Army pointer table must have 501 exact slots.");
        var context = OwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions);
        var pool = WorldIndexContext.ImportOwnedState(context,saved.Indexes,state);
        context.BindArmyMapOccupancy(state);
        context.RestoreOwnedArmyReturnSlots(state,saved);
        OwnedArmyDestructorFlags.BindCurrent(saved,context);
        return new(state,context,pool);
    }

    /// <summary>Borrows the private operation's current world and live pool.
    /// The exact input still owns Prepare/Commit; no state or list is recaptured.</summary>
    internal static OwnedArmyReturnWorld Borrow(GameState state, SourceWorldContext context, WorldIndexContext pool)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pool);
        if (!ReferenceEquals(pool.ArmyReturnOwner, context) || !pool.OwnsArmyReturnProfile())
            throw new InvalidDataException("Borrowed Army return requires the same operation's current pool.");
        return new(state, context, pool, borrowed: true);
    }

    /// <summary>Publishes this world's prepared return. The active unit becomes
    /// an independent pending receiver image; deferred retirement still runs
    /// through the campaign lifecycle operation.</summary>
    internal GameState CommitSuspension(GameState current, ArmyOrdinaryReturnSuspension suspension)
    {
        if (!ReferenceEquals(current, _input) || !ReferenceEquals(suspension.Owner, Context) ||
            !ReferenceEquals(suspension.Pool, Pool) || suspension.State.ArmyReturnCaptiveContinuation is null)
            throw new InvalidDataException("Return suspension does not belong to this actual operation.");
        return ArmyOrdinaryReturnRuntime.CommitSuspension(suspension, _borrowed);
    }

    public GameState? CommitPrepared(GameState current, ArmyOrdinaryReturnPacket packet)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(packet);
        if (!ReferenceEquals(current, _input) || !ReferenceEquals(packet.Owner,Context) || !ReferenceEquals(packet.Pool,Pool)) return null;
        int physical = packet.Army.PhysicalSlot ?? throw new InvalidDataException("Current return Army pointer is null.");
        var original = _input.ReadArmyLifecycle()!; var slot = original.ReceiverSlots()[physical];
        if (slot.ActiveUnitId is not int id || !_input.Units.ContainsKey(id))
            throw new InvalidDataException("Whole ordinary return must identify an active owned Army projection.");
        ArmyOrdinaryReturnRuntime.Commit(packet);
        var result = OwnedArmyReturnCurrentFields.PublishCurrent(packet.CaptivesState ?? _input,Context);
        if (packet.HasAssignmentCurrentWrites)
            result = PersonAssignmentPrecallInputs.PublishCurrent(result, Context);
        result = ArmyReturnImagePublication.Publish(result, Context, [physical]);
        //Ruler District reorganization can complete and reschedule real
        //personnel tasks. Rebind their metadata to this same committed pool,
        //without rebuilding its live associated/retirement lists.
        if (packet.DistrictOperation is not null &&
            !ReferenceEquals(Context.ReadOwnedPersonCommandManager(), result.PersonnelCommands))
            PersonReturnCommands.BindCurrent(result, Context);
        result = (result with { ArmyMapOccupancy = Context.ExportArmyMapOccupancy() })
            .WithArmyLifecycle(result.ReadArmyLifecycle()! with { Indexes = _borrowed ? Pool.ProjectOwnedRoles() : Pool.ExportOwnedState() });
        return result;
    }
}

public sealed partial class SourceWorldContext
{
    internal void RestoreOwnedArmyReturnSlots(GameState state,ArmyLifecycleSourceView saved)
    {
        BindArmyReturnImages(saved);
        foreach (var slot in saved.ReceiverSlots())
        {
            int physical=slot.PhysicalSlot;
            SetArmyUnitTablePointer(physical,physical); SetArmyUnitCommanderId(physical,slot.Commander12); SetArmyUnitStatusFlag10(physical,slot.Status10);
            var army=slot.ActiveUnitId is int id ? state.Units[id] : slot.PendingImage;
            if (army is null) continue;
            //The same saved image owns all three current abnormal counters.
            //Override kind3C/identity3E already come from BindArmyOverrides;
            //bind these bytes in this existing restore prefix, not at a later
            //damage query or by substituting a guessed zero.
            var abnormal = new RegistryObjectReference(RegistryKind.Army, physical);
            SetTacticAbnormalRawByte(abnormal, 0x23, army.NativeTacticRuntime?.Abnormal23);
            SetTacticAbnormalRawByte(abnormal, 0x24, army.NativeTacticRuntime?.Abnormal24);
            SetTacticAbnormalRawByte(abnormal, 0x25, army.NativeTacticRuntime?.Abnormal25);
            SetArmyUnitRawHex2A(physical,ArmyMarchSourceAdapter.ReadCurrentHex(state, army)); //No HexId fallback.
            BindArmyReturnResourceFields(physical,new(army.Troops is >=0 and <=65535 ? (ushort)army.Troops : null,
                army.WoundedTroops is >=0 and <=65535 ? (ushort)army.WoundedTroops.Value : null,
                ArmyMoraleSourceAdapter.Read(army),slot.StoredResources.Gold,slot.StoredResources.Food));
            if (ArmyTraitSourceAdapter.Encode(state, army) is { } traits && slot.AuxiliaryAC is { } ac &&
                slot.AuxiliaryF4 is { } f4 && OwnedIndexes?.TraitHandles(physical) is { } descriptors)
                BindArmyReturnTraitFields(physical,new(traits,ac,f4,descriptors.Select(x=>new RegistryListHandle(x.Identity))));
        }
    }
}
