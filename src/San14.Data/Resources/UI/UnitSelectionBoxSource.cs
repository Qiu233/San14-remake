namespace San14.Data;

/// <summary>92E490's actual Common named drag-selection layout. It carries
/// presentation only; the live selector owns the rectangle and visibility.</summary>
public static class UnitSelectionBoxSource
{
    public static LoadedLayout Load(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return MapMarkerLayout.LoadNamed(repository, "cmn_area_select");
    }
}
