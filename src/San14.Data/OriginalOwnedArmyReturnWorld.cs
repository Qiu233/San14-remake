using System.Text.Json;
using San14.Core;

namespace San14.Data;

/// <summary>Saved complete arena and current owned tables. This capture is
/// not a fresh allocator/manager producer and cannot manufacture one.</summary>
public sealed class OriginalOwnedArmyReturnWorld
{
    public OriginalEventInitializationContext Context { get; }
    public OriginalCurrentListPool Pool { get; }
    private readonly GameState _before;
    private readonly GameState _input;
    private OriginalOwnedArmyReturnWorld(GameState state, OriginalEventInitializationContext context, OriginalCurrentListPool pool)
    { _input = state; _before = JsonSerializer.Deserialize<GameState>(JsonSerializer.SerializeToUtf8Bytes(state))!;
        Context = context; Pool = pool; }

    public static OriginalOwnedArmyReturnWorld Capture(GameState state, OriginalOwnedArmyCurrentDefinitions definitions)
    {
        NativeArmyLifecycleStateValidation.Validate(state);
        var saved = state.NativeArmyLifecycle ?? throw new InvalidDataException("current.complete_saved_Army_arena is unknown.");
        if (saved.Slots.Length != 501) throw new InvalidDataException("CE native Army pointer table must have 501 exact slots.");
        var context = OriginalOwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions);
        var pool = OriginalCurrentListPool.ImportOwnedState(context,saved.Pool);
        context.RestoreOwnedArmyReturnBindings(saved);
        context.RestoreOwnedArmyReturnSlots(state,saved);
        OriginalOwnedArmyDestructorFlags.BindCurrent(saved,context);
        return new(state,context,pool);
    }

    /// <summary>Only an actual sealed whole29F220 packet for THIS world may
    /// publish. Active projection retirement leaves an independent pending
    /// image; manager60/header deletion is still deferred to its source consumer.</summary>
    public GameState? CommitPrepared(GameState current, OriginalArmyOrdinaryReturnPacket packet)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(packet);
        if (!ReferenceEquals(current, _input) || !ReferenceEquals(packet.Owner,Context) || !ReferenceEquals(packet.Pool,Pool)) return null;
        int physical = packet.Army.PhysicalSlot ?? throw new InvalidDataException("Current return Army pointer is null.");
        var saved = _before.NativeArmyLifecycle!; var slot = saved.Slots[physical];
        if (slot.ActiveUnitId is not int id || !_before.Units.TryGetValue(id,out var active))
            throw new InvalidDataException("Whole ordinary return must identify an active owned Army projection.");
        if (!OriginalArmyOrdinaryReturnRuntime.ApplyPrepared(packet)) return null;
        //All publication happens on a detached state. A failed validation cannot
        //mutate the caller's game state or its saved arena.
        var result = OriginalOwnedArmyReturnCurrentFields.PublishCurrent(packet.CaptivesState ?? _before,Context);
        if (packet.HasAssignmentCurrentWrites)
            result = OriginalPersonAssignmentPrecallInputs.PublishCurrent(result, Context);
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
        result = result with { Units=units,Officers=people, NativeArmyLifecycle=saved with
        {
            Pool=Pool.ExportOwnedState(), Slots=slots,
            HexRaw1C=saved.HexRaw1C.ToDictionary(x=>x.Key,x=>Context.ReadHexRaw1C(new(x.Key)))
        } };
        NativeArmyReturnCurrentStateValidation.Validate(result); NativeArmyLifecycleStateValidation.Validate(result); return result;
    }
}

public sealed partial class OriginalEventInitializationContext
{
    internal void RestoreOwnedArmyReturnBindings(NativeArmyLifecycleState saved)
    {
        _registryDescriptors.Clear(); _baseHexListHandles.Clear(); _armyReturnBasePerson88.Clear();
        foreach (var row in saved.RegistryDescriptors)
        {
            if (!Enum.TryParse<OriginalRegistryKind>(row.Key,out var kind) || !Enum.IsDefined(kind))
                throw new InvalidDataException("Saved registry has an unrecognized native owner kind.");
            SetRegistryDescriptor(kind,row.Value is null ? null : new(row.Value.Identity));
        }
        SetArmyReturnPending70Descriptor(saved.Pending70Descriptor is null ? null : new(saved.Pending70Descriptor.Identity));
        SetBaseRegistryHandle(saved.BaseRegistryDescriptor is null ? null : new(saved.BaseRegistryDescriptor.Identity));
        foreach (var row in saved.BaseLists)
        {
            var target = new OriginalBaseReference(row.Kind==NativeArmyBaseKind.City ? OriginalBaseKind.City : OriginalBaseKind.Gate,row.PhysicalSlot);
            SetBaseHexListHandle(target,row.HexDescriptor is null ? null : new(row.HexDescriptor.Identity));
            SetArmyReturnBasePerson88Descriptor(target,row.Person88Descriptor is null ? null : new(row.Person88Descriptor.Identity));
        }
        foreach (var row in saved.HexRaw1C) SetHexRaw1C(row.Key,row.Value);
    }
    internal void RestoreOwnedArmyReturnSlots(GameState state,NativeArmyLifecycleState saved)
    {
        foreach (var slot in saved.Slots)
        {
            int physical=slot.PhysicalSlot;
            SetArmyUnitTablePointer(physical,physical); SetArmyUnitCommanderId(physical,slot.Commander12); SetArmyUnitStatusFlag10(physical,slot.Status10);
            var army=slot.ActiveUnitId is int id ? state.Units[id] : slot.PendingImage;
            if (army is null) continue;
            SetArmyUnitRawHex2A(physical,army.NativeMarch?.CurrentHex2A ?? army.NativeCurrentHex2A); //No HexId fallback.
            SetArmyReturnResourceFields(physical,new(army.Troops is >=0 and <=65535 ? (ushort)army.Troops : null,
                army.WoundedTroops is >=0 and <=65535 ? (ushort)army.WoundedTroops.Value : null,
                army.NativeMorale1A,slot.Gold5C,slot.Supplies60));
            if (army.NativeArmyConfiguration?.TraitSlots64 is { } traits && slot.AuxiliaryAC is { } ac &&
                slot.AuxiliaryF4 is { } f4 && slot.Trait160Descriptors is { } descriptors)
                SetArmyReturnTraitFields(physical,new(traits,ac,f4,descriptors.Select(x=>new OriginalRegistryListHandle(x.Identity))));
        }
    }
}
