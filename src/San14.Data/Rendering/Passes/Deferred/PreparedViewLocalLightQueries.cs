namespace San14.Data;

public enum LocalLightQueryState { Unresolved, Absent, Present }

/// <summary>A local-light query can be unknown, have no resource, or retain
/// the actual value of a present resource. A present zero remains distinct
/// from an absent resource even though both query as zero.</summary>
public readonly record struct LocalLightQuery
{
    private readonly uint value;
    private LocalLightQuery(LocalLightQueryState state, uint value)
    {
        State = state;
        this.value = value;
    }
    public LocalLightQueryState State { get; }
    public static LocalLightQuery Unresolved => default;
    public static LocalLightQuery Absent => new(LocalLightQueryState.Absent, 0);
    public static LocalLightQuery Present(uint value) => new(LocalLightQueryState.Present, value);
    public uint RequireValue() => State switch
    {
        LocalLightQueryState.Absent => 0,
        LocalLightQueryState.Present => value,
        _ => throw new InvalidDataException("The prepared View local-light query is unresolved.")
    };
}

/// <summary>The two local-light query results belonging to one prepared View.
/// Their order is retained without assigning unsupported light-family names.</summary>
public sealed class PreparedViewLocalLightQueries
{
    private readonly MapSourceCreatedViewFieldsPacket view;
    public PreparedViewLocalLightQueries(MapSourceCreatedViewFieldsPacket view,
        int selectedViewIndex, LocalLightQuery first, LocalLightQuery second)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (selectedViewIndex is < 0 or >= 32)
            throw new ArgumentOutOfRangeException(nameof(selectedViewIndex));
        this.view = view;
        SelectedViewIndex = selectedViewIndex;
        First = first;
        Second = second;
    }
    public int SelectedViewIndex { get; }
    public LocalLightQuery First { get; }
    public LocalLightQuery Second { get; }
    public bool AreEmpty
    {
        get
        {
            uint first = First.RequireValue();
            uint second = Second.RequireValue();
            return first == 0 && second == 0;
        }
    }

    /// <summary>The supported newly constructed ordinary View starts with
    /// both local-light resources absent. Imported or reused resources must
    /// supply their own resolved or unresolved states.</summary>
    public static PreparedViewLocalLightQueries CreateFresh(
        MapSourceCreatedViewFieldsPacket view, int selectedViewIndex) =>
        new(view, selectedViewIndex, LocalLightQuery.Absent, LocalLightQuery.Absent);

    public void RequireView(MapSourceCreatedViewFieldsPacket acceptedView)
    {
        if (!ReferenceEquals(view, acceptedView))
            throw new InvalidDataException("Local-light queries belong to another prepared View.");
    }

    public DeferredEarlyF103CurrentVariantInputs BindCurrentFrame(
        MapSourceCreatedF103CurrentFrame acceptedFrame, uint nativeColorMode) =>
        BindCurrentFrame(acceptedFrame, nativeColorMode, SelectedViewIndex);

    public DeferredEarlyF103CurrentVariantInputs BindCurrentFrame(
        MapSourceCreatedF103CurrentFrame acceptedFrame, uint nativeColorMode,
        int selectedViewIndex)
    {
        ArgumentNullException.ThrowIfNull(acceptedFrame);
        RequireView(acceptedFrame.ViewFields);
        if (selectedViewIndex != SelectedViewIndex)
            throw new InvalidDataException("Local-light queries select another registry View.");
        return new(acceptedFrame.Variant with { NativeColorMode = nativeColorMode }, this);
    }
}
