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
        var saved = state.NativeArmyLifecycle ?? throw new InvalidDataException("current.complete_saved_Army_arena is unknown.");
        if (saved.Slots.Length != 501) throw new InvalidDataException("CE native Army pointer table must have 501 exact slots.");
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
        var original = _input.NativeArmyLifecycle!; var slot = original.Slots[physical];
        if (slot.ActiveUnitId is not int id || !_input.Units.TryGetValue(id,out var active))
            throw new InvalidDataException("Whole ordinary return must identify an active owned Army projection.");
        ArmyOrdinaryReturnRuntime.Commit(packet);
        //Detach retained receiver collections only when this operation succeeds.
        //The simulation owns the enclosing world transaction; publication replaces
        //the changed aggregates and never mutates the input's lifecycle leaves.
        var saved = original.Copy();
        var result = OwnedArmyReturnCurrentFields.PublishCurrent(packet.CaptivesState ?? _input,Context);
        if (packet.HasAssignmentCurrentWrites)
            result = PersonAssignmentPrecallInputs.PublishCurrent(result, Context);
        var units = result.Units.ToDictionary(x=>x.Key,x=>x.Value); units.Remove(id);
        var people = result.Officers.ToDictionary(x=>x.Key,x=>x.Value);
        people[active.OfficerId] = people[active.OfficerId] with { Deployed=false };
        var slots = saved.Slots.ToArray();
        for (int i=0;i<slots.Length;i++)
        {
            var traits = Context.ReadArmyReturnTraitFields(new(i));
            var old = slots[i]; var image = old.ActiveUnitId is int unitId ? result.Units.GetValueOrDefault(unitId) : old.PendingImage;
            if (image is not null && traits is not null)
            {
                image = image with { NativeArmyConfiguration = image.NativeArmyConfiguration! with { TraitSlots64=traits.Traits64.ToArray() } };
                if (old.ActiveUnitId is int live && live != id) units[live]=image;
            }
            slots[i] = old with
            {
                ActiveUnitId=i==physical ? null : old.ActiveUnitId,
                PendingImage=i==physical || old.PendingImage is not null ? image : null,
                AuxiliaryAC=traits?.PersonsAC.ToArray() ?? old.AuxiliaryAC,
                AuxiliaryF4=traits?.AuxiliaryF4.ToArray() ?? old.AuxiliaryF4
            };
        }
        result = result with { Units=units,Officers=people, ArmyMapOccupancy=Context.ExportArmyMapOccupancy(), NativeArmyLifecycle=saved with
        {
            Indexes = Pool.ExportOwnedState(), Slots=slots
        } };
        return result;
    }
}

public sealed partial class SourceWorldContext
{
    internal void RestoreOwnedArmyReturnSlots(GameState state,NativeArmyLifecycleState saved)
    {
        foreach (var slot in saved.Slots)
        {
            int physical=slot.PhysicalSlot;
            SetArmyUnitTablePointer(physical,physical); SetArmyUnitCommanderId(physical,slot.Commander12); SetArmyUnitStatusFlag10(physical,slot.Status10);
            var army=slot.ActiveUnitId is int id ? state.Units[id] : slot.PendingImage;
            if (army is null) continue;
            SetArmyUnitRawHex2A(physical,ArmyMarchSourceAdapter.ReadCurrentHex(state, army)); //No HexId fallback.
            SetArmyReturnResourceFields(physical,new(army.Troops is >=0 and <=65535 ? (ushort)army.Troops : null,
                army.WoundedTroops is >=0 and <=65535 ? (ushort)army.WoundedTroops.Value : null,
                army.NativeMorale1A,slot.StoredResources.Gold,slot.StoredResources.Food));
            if (army.NativeArmyConfiguration?.TraitSlots64 is { } traits && slot.AuxiliaryAC is { } ac &&
                slot.AuxiliaryF4 is { } f4 && OwnedIndexes?.TraitHandles(physical) is { } descriptors)
                SetArmyReturnTraitFields(physical,new(traits,ac,f4,descriptors.Select(x=>new RegistryListHandle(x.Identity))));
        }
    }
}
