using Godot;

/// <summary>One interface's common headings. Pages retain the actual controls
/// and borrowed resources; this owner only pairs their display lifetime.</summary>
internal sealed class UiSourceHeaderStack
{
    private readonly List<UiSourceHeader> entries = [];

    internal void Push(UiSourceHeader header)
    {
        if (entries.Count > 0) entries[^1].Hide();
        entries.Add(header);
        header.Show();
    }

    internal void Remove(UiSourceHeader header)
    {
        int index = entries.FindIndex(entry => ReferenceEquals(entry, header));
        if (index < 0) return;
        bool wasTop = index == entries.Count - 1;
        entries.RemoveAt(index);
        // A retiring hidden page cannot pop a newer, unrelated heading.
        if (wasTop && entries.Count > 0)
        {
            var previous = entries[^1];
            if (GodotObject.IsInstanceValid(previous) && previous.IsInsideTree() && !previous.IsQueuedForDeletion())
                previous.Show();
        }
    }
}
