using San14.Data;

/// <summary>The environment page's local key edits. Only Apply publishes these
/// bindings to the application; closing the page discards this owner.</summary>
internal sealed class KeyboardBindingDraft
{
    private readonly KeyboardBindingCatalog catalog;
    private int[] bindings;
    internal IReadOnlyList<int> Bindings => bindings;

    internal KeyboardBindingDraft(KeyboardBindingCatalog catalog, IReadOnlyList<int>? initial = null)
    {
        this.catalog = catalog;
        bindings = Copy(initial ?? catalog.DefaultBindings);
    }

    internal int Read(int actionId, int slot) => bindings[actionId * 2 + slot];
    internal int[] Export() => bindings.ToArray();
    internal bool HasChanges(IReadOnlyList<int> entry) => !bindings.SequenceEqual(entry);
    internal void Apply(IReadOnlyList<int> value) => bindings = Copy(value);
    internal void Reset() => bindings = Copy(catalog.DefaultBindings);
    internal void Rebind(int actionId, int slot, int binding, bool replaceConflict) =>
        bindings = catalog.Rebind(bindings, actionId, slot, binding, replaceConflict);

    private static int[] Copy(IReadOnlyList<int> value)
    {
        if (value.Count != KeyboardBindingCatalog.BindingCount)
            throw new InvalidDataException("Keyboard preferences require all 31 pairs of bindings.");
        return value.ToArray();
    }
}
