using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record EditedEventSampleRow(int Slot, string Name, string Description);
public sealed record EditedEventSampleImport(EditedEventLibrary Library,
    IReadOnlyList<int> AddedEventSlots, IReadOnlyList<int> AddedMessageSlots);

/// <summary>Installed, immutable examples, separate from the player's library.
/// Import prepares an entire detached result; the caller's ordinary atomic
/// library save is the only publication point.</summary>
public sealed class EditedEventSampleCatalog
{
    public const string CapacityFailure = "event_editor.samples.capacity";
    private const int EventStorageCapacity = 512000;
    private readonly EditedEventLibrary samples;
    public IReadOnlyList<EditedEventSampleRow> Rows { get; }

    internal EditedEventSampleCatalog(EditedEventLibrary samples)
    {
        samples.Validate();
        this.samples = samples;
        Rows = Array.AsReadOnly(samples.Events.Select(value =>
            new EditedEventSampleRow(value.Slot, value.Name, value.Description)).ToArray());
        foreach (var value in samples.Events)
            foreach (var content in value.Contents)
            {
                int message = EditedEventContentEditing.Read(content).MessageSlot;
                if (message >= 0 && samples.FindMessage(message) is null)
                    throw new InvalidDataException("event_editor.samples.message_reference");
            }
    }

    public EditedEventSampleImport Import(EditedEventLibrary library, IEnumerable<int> selectedSlots)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(selectedSlots);
        library.Validate();
        int[] selection = selectedSlots.ToArray();
        var selected = selection.ToHashSet();
        if (selection.Length != selected.Count || selected.Any(slot => samples.FindEvent(slot) is null))
            throw new InvalidDataException("event_editor.samples.selection");
        if (selected.Count == 0) return new(library, Array.Empty<int>(), Array.Empty<int>());
        if (library.Events.Length + selected.Count > EditedEventLibrary.EventCapacity)
            throw new InvalidDataException(CapacityFailure);

        var next = library;
        var eventSlots = new List<int>();
        var messageSlots = new List<int>();
        // The original multi-selection copies in catalog order, then content
        // order, always into the first unused event/message slot. Each content
        // reference gets its own editable article; text is not deduplicated.
        foreach (var sample in samples.Events.Where(value => selected.Contains(value.Slot)))
        {
            int slot = next.FirstFreeEventSlot() ?? throw new InvalidDataException(CapacityFailure);
            var contents = ImmutableArray.CreateBuilder<EditedEventContent>(sample.Contents.Length);
            foreach (var content in sample.Contents)
            {
                var values = EditedEventContentEditing.Read(content);
                if (values.MessageSlot < 0) { contents.Add(content); continue; }
                var original = samples.FindMessage(values.MessageSlot)
                    ?? throw new InvalidDataException("event_editor.samples.message_reference");
                int messageSlot = next.FirstFreeMessageSlot() ?? throw new InvalidDataException(CapacityFailure);
                var message = original with { Slot = messageSlot };
                if (!EditedEventArticleStorage.CanStoreMessage(next, message))
                    throw new InvalidDataException(CapacityFailure);
                next = next.SetMessage(message);
                messageSlots.Add(messageSlot);
                contents.Add(EditedEventContentEditing.Apply(content, values with { MessageSlot = messageSlot }));
            }
            next = next.SetEvent(sample with { Slot = slot, Contents = contents.ToImmutable() });
            eventSlots.Add(slot);
        }
        // Original edited storage has 44-byte statements, six per content,
        // a count before each result group and a final zero group count. The
        // semantic library writes one result statement per authoring group.
        long eventBytes = next.Events.Sum(value => 44L * (value.Conditions.Instructions.Length +
            6L * value.Contents.Length + value.Results.Instructions.Length) +
            4L * value.Results.Instructions.Length + 4L);
        if (eventBytes >= EventStorageCapacity ||
            EditedEventArticleStorage.UsedStorageBytes(next) > EditedEventArticleStorage.CapacityBytes)
            throw new InvalidDataException(CapacityFailure);
        return new(next, eventSlots.AsReadOnly(), messageSlots.AsReadOnly());
    }
}
