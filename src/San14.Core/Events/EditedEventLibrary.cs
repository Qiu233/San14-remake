using System.Collections.Immutable;

namespace San14.Core;

/// <summary>One original editor content row expands to six ordered event
/// statements. Keeping that boundary retains authoring and branch structure.</summary>
public sealed record EditedEventContent(EditedEventProgram Program)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Program);
        Program.Validate();
        if (Program.Instructions.Length != 6)
            throw new InvalidDataException("Edited event content requires its six statements.");
    }
}

/// <summary>A title-library definition, independent of a current campaign's
/// occurrence flags and of the editor's detached working selection.</summary>
public sealed record EditedEventDefinition(int Slot, string Name, string Description,
    byte Category, bool Enabled, bool Automatic, bool Repeat,
    EditedEventProgram Conditions, ImmutableArray<EditedEventContent> Contents,
    EditedEventProgram Results)
{
    public int OccurrenceId => checked(800 + Slot);

    public void Validate()
    {
        if (Slot is < 0 or >= EditedEventLibrary.EventCapacity || Category >= 5 ||
            string.IsNullOrEmpty(Name) || Name.Contains('\0') || Description is null ||
            Description.Contains('\0') || Conditions is null || Results is null || Contents.IsDefault)
            throw new InvalidDataException("Edited event definition is incomplete or outside its source slots.");
        Conditions.Validate(); Results.Validate();
        if (Conditions.Instructions.Length > EditedEventLibrary.ConditionCapacity)
            throw new InvalidDataException("Edited event condition inventory exceeds its source capacity.");
        if (Results.Instructions.Length > EditedEventLibrary.ResultCapacity)
            throw new InvalidDataException("Edited event result inventory exceeds its source capacity.");
        if (Contents.Length > EditedEventLibrary.ContentCapacity)
            throw new InvalidDataException("Edited event content inventory exceeds its source capacity.");
        if (Conditions.Instructions.Length == 0 || Contents.Length == 0 || Results.Instructions.Length == 0)
            throw new InvalidDataException("Edited event requires its condition, contents and results programs.");
        foreach (var content in Contents)
        {
            if (content is null) throw new InvalidDataException("Edited event content is missing.");
            content.Validate();
        }
    }
}

public sealed record EditedEventMessage(int Slot, string Text)
{
    public void Validate()
    {
        if (Slot is < 0 or >= EditedEventLibrary.MessageCapacity || Text is null || Text.Contains('\0'))
            throw new InvalidDataException("Edited message is outside its source slots or contains a terminator.");
    }
}

/// <summary>The independently saved title library. Sparse arrays contain only
/// actual records; absence is a free slot, never a synthetic empty event.</summary>
public sealed record EditedEventLibrary(ImmutableArray<EditedEventDefinition> Events,
    ImmutableArray<EditedEventMessage> Messages)
{
    public const int EventCapacity = 100;
    public const int MessageCapacity = 1000;
    public const int ConditionCapacity = 40;
    public const int ResultCapacity = 20;
    public const int ContentCapacity = 200;
    public static EditedEventLibrary Empty { get; } = new([], []);

    public void Validate()
    {
        if (Events.IsDefault || Messages.IsDefault || Events.Length > EventCapacity || Messages.Length > MessageCapacity)
            throw new InvalidDataException("Edited event library has an invalid slot inventory.");
        var slots = new HashSet<int>();
        foreach (var item in Events)
        {
            if (item is null) throw new InvalidDataException("Edited event definition is missing.");
            item.Validate();
            if (!slots.Add(item.Slot)) throw new InvalidDataException("Edited event slot is duplicated.");
        }
        slots.Clear();
        foreach (var item in Messages)
        {
            if (item is null) throw new InvalidDataException("Edited message is missing.");
            item.Validate();
            if (!slots.Add(item.Slot)) throw new InvalidDataException("Edited message slot is duplicated.");
        }
    }

    public EditedEventDefinition? FindEvent(int slot) => Events.FirstOrDefault(item => item.Slot == slot);
    public EditedEventMessage? FindMessage(int slot) => Messages.FirstOrDefault(item => item.Slot == slot);

    public int? FirstFreeEventSlot() => Enumerable.Range(0, EventCapacity)
        .Select(slot => (int?)slot).FirstOrDefault(slot => Events.All(item => item.Slot != slot));
    public int? FirstFreeMessageSlot() => Enumerable.Range(0, MessageCapacity)
        .Select(slot => (int?)slot).FirstOrDefault(slot => Messages.All(item => item.Slot != slot || item.Text.Length == 0));

    public EditedEventLibrary SetEvent(EditedEventDefinition value)
    {
        ArgumentNullException.ThrowIfNull(value); value.Validate();
        return this with { Events = Events.Where(item => item.Slot != value.Slot).Append(value)
            .OrderBy(item => item.Slot).ToImmutableArray() };
    }

    public EditedEventLibrary SetMessage(EditedEventMessage value)
    {
        ArgumentNullException.ThrowIfNull(value); value.Validate();
        var retained = Messages.Where(item => item.Slot != value.Slot);
        if (value.Text.Length != 0) retained = retained.Append(value);
        return this with { Messages = retained
            .OrderBy(item => item.Slot).ToImmutableArray() };
    }

    public EditedEventLibrary RemoveEvents(IReadOnlySet<int> slots) =>
        this with { Events = Events.Where(item => !slots.Contains(item.Slot)).ToImmutableArray() };
    public EditedEventLibrary RemoveMessages(IReadOnlySet<int> slots) =>
        this with { Messages = Messages.Where(item => !slots.Contains(item.Slot)).ToImmutableArray() };
}
