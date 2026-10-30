using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>Fresh-game selections. They are not permanent Enabled flags in
/// the editor archive and never replay when restoring a saved campaign.</summary>
public sealed record ItemEditorNewGameSelection(ImmutableArray<int> HistoricalIds,
    ImmutableArray<int> CreatedIds)
{
    public static ItemEditorNewGameSelection Empty { get; } = new([], []);
    public ImmutableArray<ItemEditorCustodyState> Custodies { get; init; } = [];
    public uint? CrtAfterManagement { get; init; }
    public void Validate(ItemEditorLibrary library)
    {
        if (HistoricalIds.IsDefault || CreatedIds.IsDefault || Custodies.IsDefault ||
            HistoricalIds.Distinct().Count() != HistoricalIds.Length || CreatedIds.Distinct().Count() != CreatedIds.Length ||
            HistoricalIds.Any(id => library.FindHistorical(id) is null) ||
            CreatedIds.Any(id => library.FindCreated(id) is null) ||
            Custodies.Any(item => item is null || library.FindCreated(item.ItemId) is null) ||
            Custodies.Select(item => item.ItemId).Distinct().Count() != Custodies.Length ||
            CreatedIds.Length != 0 && (!CrtAfterManagement.HasValue || CreatedIds.Any(id => !Custodies.Any(item => item.ItemId == id))))
            throw new InvalidDataException("Fresh item adoption references an absent editor record.");
        foreach (var item in Custodies) item.Validate();
    }
}

/// <summary>The selected management page's current custody. Retained location
/// is kept independently when a person is selected, just like 324580.</summary>
public sealed record ItemEditorCustodyState(int ItemId, int? OfficerId, ItemLifecycleKnowledge Lifecycle)
{
    public bool Flag8 => (Lifecycle.RetainedFlags!.Value & 8) != 0;
    public int? AreaId => Lifecycle.Status?.Code == 2 ? Lifecycle.RetainedLocationCode : null;
    internal void Validate()
    {
        if (ItemId is < 152 or > 201 || OfficerId is < 1 or > 6000 || Lifecycle is null ||
            Lifecycle.Status?.Kind != ItemStatusKnowledgeKind.ExactSourceState ||
            !Lifecycle.RetainedLocationCode.HasValue || !Lifecycle.RetainedFlags.HasValue)
            throw new InvalidDataException("Fresh custom item custody is incomplete.");
    }
}

public sealed record ItemEditorCustodyOfficerChoice(int Id, string Name);

public sealed record ItemEditorInitialState(ItemEditorNewGameSelection Selection,
    ImmutableDictionary<int, ItemAppearance> Appearances);

public sealed class ItemEditorNewGameDraft
{
    private readonly ItemEditorLibrary library;
    private readonly HashSet<int> historical = [], created = [];
    private readonly Dictionary<int, ItemEditorCustodyState> custody = [];
    private readonly HashSet<int> validAreas;
    private readonly HashSet<int> officerIds;
    public IReadOnlyList<ItemEditorCustodyOfficerChoice> Officers { get; }
    public uint CrtState { get; private set; }
    public ItemEditorNewGameDraft(ItemEditorLibrary library, LoadedScenario source, uint initializationSeed,
        ItemEditorNewGameSelection? initial = null)
    {
        library.Validate(); this.library = library;
        CrtState = source.InitialRegisteredOfficerRandom?.State ?? initializationSeed;
        validAreas = source.Snapshot.Areas.Where(area => area.Slot is > 0 and <= 500 && area.CitySlot is >= 1 and <= 51)
            .Select(area => area.Slot).ToHashSet();
        var items = source.Snapshot.ScenarioItems?.Items.ToDictionary(item => item.Slot)
            ?? throw new InvalidDataException("New item custody requires actual scenario item records.");
        foreach (var item in library.Created)
        {
            if (!items.TryGetValue(item.Id, out var original) || original.RawStatusCode is not byte status ||
                original.RetainedLocationCode is not ushort location || original.RetainedFlags is not byte flags)
                throw new InvalidDataException("Initial custom item custody is unavailable.");
            custody.Add(item.Id, new(item.Id, original.OwnerId == 0 ? null : original.OwnerId,
                new(ItemStatusKnowledge.Exact(status), location, flags)));
        }
        //518290 traverses the actual main, special, guest and registered
        //Person table groups in this order. Status is the independent 232460.
        var people = source.Snapshot.Characters.Where(person => person.CharacterId != 0)
            .ToDictionary(person => person.CharacterId);
        var officers = new List<ItemEditorCustodyOfficerChoice>();
        foreach (int id in Enumerable.Range(1, 1000).Concat(Enumerable.Range(2001, 88))
                     .Concat(Enumerable.Range(5101, 100)).Concat(Enumerable.Range(3001, 150)))
        {
            if (!people.TryGetValue(id, out var person) || person.StatusCode is < 1 or > 4) continue;
            if (person.CreditReference is not { } credit)
                throw new InvalidDataException("Item custody Person native validity fields are unavailable.");
            bool valid = NativeValidity.Person(person.Slot, checked((ushort)person.CharacterId),
                credit.RawFlagsWord, credit.RawTailCode, person.StatusCode)
                ?? throw new InvalidDataException("Item custody Person native validity is unknown.");
            if (valid) officers.Add(new(id, person.Name));
        }
        Officers = officers; officerIds = officers.Select(officer => officer.Id).ToHashSet();
        if(initial is not null)
        {
            initial.Validate(library);
            foreach(int id in initial.HistoricalIds)historical.Add(id);
            foreach(int id in initial.CreatedIds)created.Add(id);
            foreach(var item in initial.Custodies)custody[item.ItemId]=item;
            // Returning from force selection restores the accepted management
            // cursor. The later 6225D0 placement is not repeated in this draft.
            if(initial.CrtAfterManagement is uint cursor)CrtState=cursor;
        }
    }
    public bool IsSelected(int id) => id < 152 ? historical.Contains(id) : created.Contains(id);
    public void SelectHistorical(int id, bool selected)
    {
        if (library.FindHistorical(id) is null) throw new InvalidDataException("Historical item has no saved edit.");
        if (selected) historical.Add(id); else historical.Remove(id);
    }
    public void SelectCreated(int id, bool selected)
    {
        if (library.FindCreated(id) is null) throw new InvalidDataException("Created item slot is unused.");
        if (selected) created.Add(id); else created.Remove(id);
    }
    public ItemEditorCustodyState Custody(int itemId) => custody.GetValueOrDefault(itemId)
        ?? throw new InvalidDataException("Created item custody slot is unused.");
    public void AssignCustody(IReadOnlyList<int> ids, int? officerId, bool flag8)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count || ids.Any(id => !created.Contains(id)) ||
            officerId is int person && !officerIds.Contains(person))
            throw new InvalidDataException("Custody assignment is outside the selected custom items or actual Person candidates.");
        uint nextRandom = CrtState;
        var updates = new List<ItemEditorCustodyState>(ids.Count);
        foreach (int id in ids)
        {
            var previous = Custody(id); var lifecycle = previous.Lifecycle;
            byte flags = (byte)((lifecycle.RetainedFlags!.Value & 0xF7) | (flag8 ? 8 : 0));
            int? owner = previous.OfficerId;
            if (officerId is int chosen)
            {
                owner = chosen;
                lifecycle = lifecycle with { Status = ItemStatusKnowledge.Exact(1), RetainedFlags = flags };
            }
            else if (lifecycle.Status!.Code != 2)
            {
                if (validAreas.Count == 0) throw new InvalidDataException("Custom item custody has no valid original Area.");
                int area;
                do { nextRandom = unchecked(nextRandom * 0x343FDu + 0x269EC3u); area = (int)((nextRandom >> 16) & 0x7FFF) % 501; }
                while (!validAreas.Contains(area));
                owner = null;
                lifecycle = lifecycle with { Status = ItemStatusKnowledge.Exact(2), RetainedLocationCode = checked((ushort)area), RetainedFlags = flags };
            }
            else lifecycle = lifecycle with { RetainedFlags = flags };
            updates.Add(previous with { OfficerId = owner, Lifecycle = lifecycle });
        }
        foreach (var update in updates) custody[update.ItemId] = update;
        CrtState = nextRandom;
    }
    public ItemEditorNewGameSelection Commit() => new(historical.Order().ToImmutableArray(), created.Order().ToImmutableArray())
    { Custodies = custody.Values.OrderBy(item => item.ItemId).ToImmutableArray(), CrtAfterManagement = CrtState };
}
