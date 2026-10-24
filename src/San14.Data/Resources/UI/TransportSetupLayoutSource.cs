namespace San14.Data;

/// <summary>The transport Make page's actual Common-named layout. This
/// presentation loader does not infer campaign state or transport limits.</summary>
public static class TransportSetupLayoutSource
{
    public static LoadedLayout Load(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return MapMarkerLayout.LoadNamed(repository, "06_03_08_yusou");
    }
}
