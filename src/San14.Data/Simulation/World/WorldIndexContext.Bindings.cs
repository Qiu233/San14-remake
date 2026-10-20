using San14.Core;

namespace San14.Data;

public sealed partial class WorldIndexContext
{
    internal sealed record UnitIndexValue(int? UnitId);
    private readonly EntitySourceBindings _officers = new();
    private MapObjectSourceDirectory? _mapObjects;
    internal List<MapObjectReceiverId?>? PendingObjectRetirements { get; set; }
    public WorldIndexesState ExportOwnedState() => _state.ExportWorldIndexes(this);
    //A nested operation may need a domain image while its caller still owns
    //temporary lists. This does not publish or restore the live pool.
    internal WorldIndexesState ProjectOwnedRoles() => _state.ExportWorldIndexes(this, false);
    internal WorldIndexesState BeginChangedWork()
    {
        var next = WorldIndexRules.BeginChangedWork(ExportOwnedState());
        _state.ReplaceChangedIndexValues(this, next); ChangedProcessing = true; return next;
    }
    internal void CompleteChangedWork()
    {
        var next = WorldIndexRules.CompleteChangedWork(ExportOwnedState());
        _state.ReplaceChangedIndexValues(this, next); ChangedProcessing = false;
    }
    public static WorldIndexContext ImportOwnedState(SourceWorldContext context,
        WorldIndexesState indexes, GameState state)
    {
        var result = new WorldIndexContext(context);
        result.BindWorld(state);
        context.ImportWorldIndexes(result, indexes); return result;
    }
    public void BindWorld(GameState state)
    {
        _mapObjects = CampaignSourceKnowledge.From(state.ResourceState).MapObjectSource is { } source ? new(source) : null;
        _armies.Replace(state.ReadArmyLifecycle()?.ReceiverSlots()
            .Where(slot => (slot.ActiveUnitId ?? slot.PendingImage?.Id) is not null)
            .Select(slot => new EntitySourceBinding(slot.PhysicalSlot, slot.ActiveUnitId ?? slot.PendingImage!.Id)) ?? []);
        foreach (int retired in _traitHandles.Keys.Where(id => !_armies.ContainsEntity(id)).ToArray()) ReleaseTraitIndexes(retired);
        _officers.Replace(state.Officers.Values.Select(officer => (officer.Id, Record: _state.ResolveOfficerSource(officer.Id)))
            .Where(row => row.Record is not null).Select(row => new EntitySourceBinding(row.Record!.Value, row.Id)));
    }
    internal WorldEntityReference ToWorld(RegistryObjectReference value) => ToWorld(value.Kind, value.PhysicalSlot);
    internal WorldEntityReference ToWorld(BaseReference value)
        => value.PhysicalSlot == 0 ? new(value.Kind == BaseKind.City ? WorldEntityKind.FallbackCity : WorldEntityKind.FallbackGate, null) :
            new(value.Kind == BaseKind.City ? WorldEntityKind.City : WorldEntityKind.Gate,
                value.PhysicalSlot is int id ? value.Kind == BaseKind.Gate ? id + 51 : id : null);
    internal WorldEntityReference ToWorld(RegistryKind kind, int? physical)
    {
        var domainKind = WorldKind(kind);
        if (physical is null) return new(domainKind, null);
        if (kind == RegistryKind.Army)
            return new(domainKind, physical == 0 ? null : ArmyId(physical.Value));
        if (kind == RegistryKind.Person)
        {
            int? officerId = _officers.EntityAt(physical.Value);
            officerId ??= _state.ReadPersonIdentity(physical.Value);
            return new(domainKind, officerId ?? throw new InvalidDataException("Registered officer identity is unavailable."));
        }
        if (kind == RegistryKind.Object && _mapObjects is not null)
            return new(domainKind, _mapObjects.ReceiverAtPhysical(physical.Value).Value);
        if (kind == RegistryKind.City || kind == RegistryKind.Gate)
            return ToWorld(new BaseReference(kind == RegistryKind.City ? BaseKind.City : BaseKind.Gate, physical));
        return new(domainKind, physical);
    }
    internal int ArmyId(int physical) => _armies.EntityAt(physical) is int id ? id :
        throw new InvalidDataException("A primary unit index references a receiver with no logical unit identity.");
    internal int? ArmyPhysical(int? id)
    {
        return _armies.RecordOf(id); //Retired logical references never bind a recycled receiver.
    }
    internal object? StoreValue(object? value) => value is RegistryObjectReference { Kind: RegistryKind.Army } unit ?
        new UnitIndexValue(unit.PhysicalSlot is int physical && physical != 0 ? ArmyId(physical) : null) : value;
    internal object? ReadValue(object? value) => value is UnitIndexValue unit ?
        new RegistryObjectReference(RegistryKind.Army, ArmyPhysical(unit.UnitId)) : value;
    internal BaseReference ToSourceBase(WorldEntityReference value) => value.Kind switch
    {
        WorldEntityKind.FallbackCity => new(BaseKind.City, 0),
        WorldEntityKind.FallbackGate => new(BaseKind.Gate, 0),
        WorldEntityKind.City => new(BaseKind.City, value.Id),
        WorldEntityKind.Gate => new(BaseKind.Gate, value.Id is int id ? id - 51 : null),
        _ => throw new InvalidDataException("A base index identifies another entity kind.")
    };
    internal static int? FindArmyPhysical(GameState state, int? unitId)
    {
        if (unitId is null) return 0;
        return state.ReadArmyLifecycle()?.ReceiverSlots().FirstOrDefault(slot => slot.ActiveUnitId == unitId || slot.PendingImage?.Id == unitId)?.PhysicalSlot;
    }
    internal RegistryObjectReference ToSource(WorldEntityReference value)
    {
        if (value.Kind is WorldEntityKind.FallbackCity or WorldEntityKind.FallbackGate)
        { var fallback = ToSourceBase(value); return new(fallback.Kind == BaseKind.City ? RegistryKind.City : RegistryKind.Gate, 0); }
        var kind = SourceKind(value.Kind); int? physical = value.Id;
        if (kind == RegistryKind.Army) physical = ArmyPhysical(value.Id);
        else if (kind == RegistryKind.Object && physical is int receiver && _mapObjects is not null)
            physical = _mapObjects.PhysicalOf(new(receiver));
        else if (kind == RegistryKind.Gate && physical is int gate) physical = gate - 51;
        else if (kind == RegistryKind.Person && physical is int person)
            physical = _officers.RecordOf(person) is int source ? source :
                throw new InvalidDataException("A saved officer index has no current officer source identity.");
        return new(kind, physical);
    }
    internal static WorldEntityKind WorldKind(RegistryKind kind) => kind switch
    {
        RegistryKind.Army => WorldEntityKind.Army, RegistryKind.City => WorldEntityKind.City,
        RegistryKind.Gate => WorldEntityKind.Gate, RegistryKind.Force => WorldEntityKind.Force,
        RegistryKind.Area => WorldEntityKind.Area, RegistryKind.District => WorldEntityKind.District,
        RegistryKind.Object => WorldEntityKind.MapObject, RegistryKind.Person => WorldEntityKind.Officer,
        RegistryKind.Item => WorldEntityKind.Item, RegistryKind.Scenery => WorldEntityKind.Landmark,
        RegistryKind.CautionTroops => WorldEntityKind.ArmyCautionGroup, RegistryKind.Table81B50 => WorldEntityKind.ArmyCaution,
        _ => throw new InvalidDataException("Unsupported domain registry kind.")
    };
    internal static RegistryKind SourceKind(WorldEntityKind kind) => kind switch
    {
        WorldEntityKind.Army => RegistryKind.Army, WorldEntityKind.City => RegistryKind.City,
        WorldEntityKind.Gate => RegistryKind.Gate, WorldEntityKind.Force => RegistryKind.Force,
        WorldEntityKind.Area => RegistryKind.Area, WorldEntityKind.District => RegistryKind.District,
        WorldEntityKind.MapObject => RegistryKind.Object, WorldEntityKind.Officer => RegistryKind.Person,
        WorldEntityKind.Item => RegistryKind.Item, WorldEntityKind.Landmark => RegistryKind.Scenery,
        WorldEntityKind.ArmyCautionGroup => RegistryKind.CautionTroops, WorldEntityKind.ArmyCaution => RegistryKind.Table81B50,
        _ => throw new InvalidDataException("Unsupported source registry kind.")
    };
}

public sealed partial class SourceWorldContext
{
    public WorldIndexContext? ReadOwnedCurrentListPool() => OwnedIndexes;
    internal void ImportWorldIndexes(WorldIndexContext owner, WorldIndexesState indexes)
    {
        owner.PendingObjectRetirements = indexes.PendingObjectRetirements is { } retirements ? new(retirements) : null;
        _orderedLists = new(); _registryDescriptors.Clear(); _baseHexListHandles.Clear(); _armyReturnBasePerson88.Clear();
        foreach (var row in indexes.Registries)
        {
            var kind = WorldIndexContext.SourceKind(row.Key);
            if (row.Value is null) { SetRegistryDescriptor(kind, null); continue; }
            var handle = owner.AllocateDescriptor(); SetRegistryDescriptor(kind, new(handle.Identity));
            foreach (var entity in row.Value)
                if (entity.Kind == WorldEntityKind.Army) AppendStoredIndexValue(handle, new WorldIndexContext.UnitIndexValue(entity.Id));
                else AppendIndexValue(handle, owner.ToSource(entity));
        }
        if (indexes.Bases is { } bases)
        {
            var handle = owner.AllocateDescriptor(); SetBaseRegistryHandle(new(handle.Identity));
            foreach (var entity in bases) AppendIndexValue(handle, owner.ToSourceBase(entity));
        }
        else SetBaseRegistryHandle(null);
        foreach (var row in indexes.BaseIndexes)
        {
            var target = owner.ToSourceBase(row.Base);
            var hexes = ImportIndexValues(owner, row.TerritoryHexIds?.Select(id => (object?)new HexReference(id)));
            SetBaseHexListHandle(target, hexes);
            var people = ImportIndexValues(owner, row.OfficerIds?.Select(id => (object?)owner.ToSource(new(WorldEntityKind.Officer, id))));
            SetArmyReturnBasePerson88Descriptor(target, people is null ? null : new(people.Identity));
        }
        foreach (var row in indexes.Armies)
        {
            int physical = owner.ArmyPhysical(row.Key)!.Value; var handles = owner.TraitHandles(physical);
            for (int index = 0; index < row.Value.TraitRelatedUnitIds.Length; index++)
                if (row.Value.TraitRelatedUnitIds[index] is { } ids)
                    foreach (int? id in ids) AppendStoredIndexValue(new(handles[index].Identity), new WorldIndexContext.UnitIndexValue(id));
                else MakeCurrentListUnavailable(new(handles[index].Identity));
        }
        if (indexes.PendingUnitIds is { } pending)
        {
            var handle = owner.AllocateDescriptor(); SetArmyReturnPending70Descriptor(new(handle.Identity));
            foreach (int? id in pending) AppendStoredIndexValue(handle, new WorldIndexContext.UnitIndexValue(id));
        }
        else SetArmyReturnPending70Descriptor(null);
        foreach (var row in indexes.CautionGroupMembers)
        {
            var handle = owner.CautionGroupHandle(row.Key);
            if (row.Value is { } members) foreach (int? id in members)
                AppendStoredIndexValue(handle, new WorldIndexContext.UnitIndexValue(id));
            else MakeCurrentListUnavailable(handle);
        }
        if (indexes.ReturningPersonnelTaskOrder is { } tasks)
        {
            var handle = owner.CreateAssociatedTaskIndex();
            foreach (int? id in tasks) AppendIndexValue(handle, new PersonLinkedEntityReference(id));
        }
        if (indexes.ChangedPendingUnitIds is not null || indexes.ChangedWorkingUnitIds is not null || indexes.ChangedProcessing is not null)
        {
            owner.CreateChangedIndexes(indexes.ChangedProcessing ?? false);
            owner.ChangedProcessing = indexes.ChangedProcessing;
            foreach (int? id in indexes.ChangedPendingUnitIds ?? []) AppendStoredIndexValue(owner.ChangedPendingHandle!, new WorldIndexContext.UnitIndexValue(id));
            foreach (int? id in indexes.ChangedWorkingUnitIds ?? []) AppendStoredIndexValue(owner.ChangedWorkingHandle!, new WorldIndexContext.UnitIndexValue(id));
            if (indexes.ChangedPendingUnitIds is null) MakeCurrentListUnavailable(owner.ChangedPendingHandle!);
            if (indexes.ChangedWorkingUnitIds is null) MakeCurrentListUnavailable(owner.ChangedWorkingHandle!);
        }
    }
    private HexListHandle? ImportIndexValues(WorldIndexContext owner, IEnumerable<object?>? values)
    {
        if (values is null) return null;
        var handle = owner.AllocateDescriptor(); foreach (object? value in values) AppendIndexValue(handle, value); return handle;
    }
    internal void AppendIndexValue(HexListHandle handle, object? value)
    { var cursor = AllocateHexListNode(handle); WriteCurrentListPayload(NodeIdentity(cursor), value); }
    internal void AppendStoredIndexValue(HexListHandle handle, object? value)
    { var cursor = AllocateHexListNode(handle); _orderedLists.Write(NodeIdentity(cursor), value); }
    internal void ReplaceChangedIndexValues(WorldIndexContext owner, WorldIndexesState indexes)
    {
        ClearTemporaryHexList(owner.ChangedWorkingHandle!); ClearTemporaryHexList(owner.ChangedPendingHandle!);
        foreach (int? id in indexes.ChangedWorkingUnitIds ?? []) AppendStoredIndexValue(owner.ChangedWorkingHandle!, new WorldIndexContext.UnitIndexValue(id));
        foreach (int? id in indexes.ChangedPendingUnitIds ?? []) AppendStoredIndexValue(owner.ChangedPendingHandle!, new WorldIndexContext.UnitIndexValue(id));
    }
    internal WorldIndexesState ExportWorldIndexes(WorldIndexContext owner, bool requireQuiescent = true)
    {
        var publishedKeys = _registryDescriptors.Values.Select(handle => handle?.Identity)
            .Concat(_baseHexListHandles.Values.Select(handle => handle?.Identity))
            .Concat(_armyReturnBasePerson88.Values.Select(handle => handle?.Identity))
            .Concat([_baseRegistryHandle?.Identity, _armyReturnPending70?.Identity])
            .OfType<int>().Concat(owner.BoundListKeys).ToHashSet();
        if (requireQuiescent && _orderedLists.NonemptyLists.Any(key => !publishedKeys.Contains(key)))
            throw new InvalidDataException("A nonempty current collection has no domain index role at publication.");
        List<WorldEntityReference>? RegistryValues(RegistryListHandle? handle) => handle is { } pointer ?
            ReadIndexValues(new(pointer.Identity))?.Select(value => value switch
            {
                WorldIndexContext.UnitIndexValue unit => new WorldEntityReference(WorldEntityKind.Army, unit.UnitId),
                RegistryObjectReference entity => owner.ToWorld(entity), BaseReference entity => owner.ToWorld(entity),
                _ => throw new InvalidDataException("A permanent registry contains another value kind.")
            }).ToList() : null;
        List<int?>? UnitIds(HexListHandle? handle) => ReadIndexValues(handle)?.Select(value => value switch
        {
            WorldIndexContext.UnitIndexValue unit => unit.UnitId,
            RegistryObjectReference { Kind: RegistryKind.Army, PhysicalSlot: int physical } => physical == 0 ? (int?)null : owner.ArmyId(physical),
            RegistryObjectReference { Kind: RegistryKind.Army, PhysicalSlot: null } => null,
            null => null,
            _ => throw new InvalidDataException("A unit index contains another value kind.")
        }).ToList();
        var bases = _baseHexListHandles.Keys.Union(_armyReturnBasePerson88.Keys).Select(target => new BaseIndexState(owner.ToWorld(target),
            ReadIndexValues(ReadBaseHexListHandle(target))?.Select(value => value is HexReference hex ? hex.PhysicalSlot :
                throw new InvalidDataException("A base territory index contains another value kind.")).ToList(),
            ReadArmyReturnBasePerson88Descriptor(target) is { } people ? ReadIndexValues(new(people.Identity))?.Select(value => value switch
            {
                RegistryObjectReference { Kind: RegistryKind.Person } person => owner.ToWorld(person).Id,
                TransferPersonReference person => owner.ToWorld(RegistryKind.Person, person.PhysicalSlot).Id,
                _ => throw new InvalidDataException("A base officer index contains another value kind.")
            }).ToList() : null)).ToList();
        return new()
        {
            Registries = _registryDescriptors.ToDictionary(row => WorldIndexContext.WorldKind(row.Key), row => RegistryValues(row.Value)),
            Bases = _baseRegistryHandle is { } baseHandle ? ReadIndexValues(new(baseHandle.Identity))?.Select(value => value is BaseReference entity ? owner.ToWorld(entity) :
                throw new InvalidDataException("The base index contains another value kind.")).ToList() : null,
            BaseIndexes = bases,
            Armies = owner.ExportArmyIndexes(UnitIds),
            CautionGroupMembers = owner.ExportCautionIndexes(UnitIds),
            PendingUnitIds = _armyReturnPending70 is { } pending ? UnitIds(new(pending.Identity)) : null,
            ReturningPersonnelTaskOrder = ReadIndexValues(owner.AssociatedTaskHandle)?.Select(value => value is PersonLinkedEntityReference task ? task.Identity :
                throw new InvalidDataException("The personnel task index contains another value kind.")).ToList(),
            ChangedPendingUnitIds = UnitIds(owner.ChangedPendingHandle), ChangedWorkingUnitIds = UnitIds(owner.ChangedWorkingHandle),
            ChangedProcessing = owner.ChangedProcessing
            , PendingObjectRetirements = owner.PendingObjectRetirements is { } retirements ? new(retirements) : null
        };
    }
}
