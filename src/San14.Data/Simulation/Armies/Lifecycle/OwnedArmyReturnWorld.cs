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
    private OwnedArmyReturnWorld(GameState state, SourceWorldContext context, WorldIndexContext pool)
    { _input = state;
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

    /// <summary>Publishes this world's prepared return. The active unit becomes
    /// an independent pending receiver image; deferred retirement still runs
    /// through the campaign lifecycle operation.</summary>
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
        result = (result with { ArmyMapOccupancy = Context.ExportArmyMapOccupancy() })
            .WithArmyLifecycle(result.ReadArmyLifecycle()! with { Indexes = Pool.ExportOwnedState() });
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
