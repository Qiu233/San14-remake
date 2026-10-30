using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>The event editor's detached authoring selection. Changes remain
/// local until the ordinary confirmation publishes a complete library value.</summary>
public sealed class EditedEventDraft
{
    private readonly EditedEventLibrary source;
    public EditedEventLibrary SourceLibrary => source;
    public int Slot { get; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public byte? Category { get; private set; }
    public bool Enabled { get; }
    public bool Automatic { get; private set; }
    public bool Repeat { get; private set; }
    public EditedEventProgram Conditions { get; private set; }
    public ImmutableArray<EditedEventContent> Contents { get; private set; }
    public EditedEventProgram Results { get; private set; }
    public bool IsDirty { get; private set; }
    public bool CanCommit { get; private set; }
    public string? ValidationFailure { get; private set; }

    private EditedEventDraft(EditedEventLibrary source, int slot, EditedEventDefinition? value)
    {
        this.source = source; Slot = slot;
        var defaults = EditedEventAuthoringCatalog.FreshDefaults;
        Name = value?.Name ?? defaults.Name; Description = value?.Description ?? defaults.Description;
        Category = value?.Category ?? defaults.Category; Enabled = value?.Enabled ?? defaults.Enabled;
        Automatic = value?.Automatic ?? defaults.Automatic; Repeat = value?.Repeat ?? defaults.Repeat;
        Conditions = value?.Conditions ?? new([]);
        Contents = value?.Contents ?? [];
        Results = value?.Results ?? new([]);
        RefreshValidity();
    }

    public static EditedEventDraft Create(EditedEventLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        int slot = library.FirstFreeEventSlot() ?? throw new InvalidOperationException("Event library is full.");
        return new(library, slot, null);
    }

    public static EditedEventDraft Edit(EditedEventLibrary library, int slot)
    {
        ArgumentNullException.ThrowIfNull(library);
        return new(library, slot, library.FindEvent(slot) ?? throw new ArgumentOutOfRangeException(nameof(slot)));
    }

    public static EditedEventDraft Copy(EditedEventLibrary library, int sourceSlot)
    {
        ArgumentNullException.ThrowIfNull(library);
        var value = library.FindEvent(sourceSlot) ?? throw new ArgumentOutOfRangeException(nameof(sourceSlot));
        int slot = library.FirstFreeEventSlot() ?? throw new InvalidOperationException("Event library is full.");
        return new(library, slot, value with { Slot = slot }) { IsDirty = true };
    }

    public void SetSummary(string name, string description, byte? category, bool automatic, bool repeat)
    {
        ArgumentNullException.ThrowIfNull(name); ArgumentNullException.ThrowIfNull(description);
        if (category is >= 5 || name.Contains('\0') || description.Contains('\0'))
            throw new InvalidDataException("Event summary is outside its source fields.");
        if (Name == name && Description == description && Category == category && Automatic == automatic && Repeat == repeat) return;
        Name = name; Description = description; Category = category; Automatic = automatic; Repeat = repeat;
        Changed();
    }

    public void SetConditions(EditedEventProgram program)
    {
        ArgumentNullException.ThrowIfNull(program); program.Validate();
        if (program.Instructions.Length > EditedEventLibrary.ConditionCapacity)
            throw new InvalidDataException("Event condition inventory is full.");
        Conditions = program; Changed();
    }

    public EditedEventProgram ReadEditableConditions() =>
        new(Conditions.Instructions.Where(item => item.Code != 64).ToImmutableArray());

    public void SetEditableConditions(EditedEventProgram program)
    {
        ArgumentNullException.ThrowIfNull(program); program.Validate();
        if (program.Instructions.Any(item => item.Code == 64))
            throw new InvalidDataException("Key people must be changed through their own selection.");
        if (program.Instructions.Length + Conditions.Instructions.Count(item => item.Code == 64) > EditedEventLibrary.ConditionCapacity)
            throw new InvalidDataException("Event condition inventory is full.");
        Conditions = new(Conditions.Instructions.Where(item => item.Code == 64)
            .Concat(program.Instructions).ToImmutableArray()); Changed();
    }

    /// <summary>Key people are the original code64 condition prefix, not a
    /// separate saved bank. Each statement contains up to five ordered IDs.</summary>
    public IReadOnlyList<int> ReadKeyPeople() => Conditions.Instructions.Where(item => item.Code == 64)
        .SelectMany(item => item.Arguments.Take(5)).Where(id => id > 0).ToArray();

    public void SetKeyPeople(IReadOnlyList<int> people)
    {
        ArgumentNullException.ThrowIfNull(people);
        if (people.Count > 20 || people.Any(id => id is <= 0 or > 6000) || people.Distinct().Count() != people.Count ||
            (people.Count + 4) / 5 + Conditions.Instructions.Count(item => item.Code != 64) > EditedEventLibrary.ConditionCapacity)
            throw new InvalidDataException("Event key people require at most twenty distinct person identities.");
        var statements = ImmutableArray.CreateBuilder<EditedEventInstruction>();
        for (int begin = 0; begin < people.Count; begin += 5)
            statements.Add(EditedEventInstruction.Create(64, people.Skip(begin).Take(5).ToArray()));
        statements.AddRange(Conditions.Instructions.Where(item => item.Code != 64));
        Conditions = new(statements.ToImmutable()); Changed();
    }

    public void AddContent(int index)
    {
        if ((uint)index > (uint)Contents.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (Contents.Length >= EditedEventLibrary.ContentCapacity)
            throw new InvalidDataException("event_editor.contents.capacity");
        Contents = Contents.Insert(index, EditedEventAuthoringCatalog.CreateDefaultContent()); Changed();
    }

    public void SetContents(IReadOnlyList<EditedEventContent> contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        if (contents.Count > EditedEventLibrary.ContentCapacity)
            throw new InvalidDataException("event_editor.contents.capacity");
        var next = contents.ToImmutableArray();
        foreach (var content in next)
        {
            if (content is null) throw new InvalidDataException("event_editor.contents.missing_row");
            content.Validate();
        }
        Contents = next; Changed();
    }

    public void ReplaceContent(int index, EditedEventContent content)
    {
        ArgumentNullException.ThrowIfNull(content); content.Validate();
        Contents = Contents.SetItem(index, content); Changed();
    }

    public void RemoveContent(int index) { Contents = Contents.RemoveAt(index); Changed(); }

    public void SetResults(EditedEventProgram program)
    {
        ArgumentNullException.ThrowIfNull(program); program.Validate();
        if (program.Instructions.Length > EditedEventLibrary.ResultCapacity)
            throw new InvalidDataException("event_editor.results.capacity");
        Results = program; Changed();
    }

    /// <summary>Returns a new value of the original editing library. When an
    /// article child has independently saved, merge Build() into the store's
    /// current library instead, preserving that confirmed child transaction.</summary>
    public EditedEventLibrary Commit() => source.SetEvent(Build());

    public EditedEventDefinition Build()
    {
        if (Category is not byte category) throw new InvalidDataException("Event category is not selected.");
        var value = new EditedEventDefinition(Slot, Name, Description, category, Enabled, Automatic, Repeat,
            Conditions, Contents, Results);
        value.Validate();
        // Structural checks occur at actual edits/confirmation, never a UI
        // tick or a cache hit. The shared event language remains authoritative.
        EditedEventProgramCompiler.Compile(Conditions, Slot);
        EditedEventProgramCompiler.Compile(new(Contents.SelectMany(content => content.Program.Instructions).ToImmutableArray()), Slot);
        EditedEventProgramCompiler.Compile(Results, Slot);
        return value;
    }

    private void Changed() { IsDirty = true; RefreshValidity(); }
    private void RefreshValidity()
    {
        try { Build(); CanCommit = true; ValidationFailure = null; }
        catch (InvalidDataException error) { CanCommit = false; ValidationFailure = error.Message; }
    }
}
