namespace San14.Data;

public sealed partial class CityDomesticDraft
{
    private ushort? _defaultHintBaseLink;

    /// <summary>The default hint binds the selected City's current object link
    /// once. Null is the source field's rejected argument; an empty string is
    /// a known empty Base name. Formatting and append capacity belong to UI.</summary>
    public AreaAdministrationQuery<string?> QueryDefaultHintBaseName()
    {
        try
        {
            EnsureOpen();
            ushort raw = _defaultHintBaseLink ??= K(_current.ReadBaseObjectLink(City()), "message_base_link");
            if (raw is 0 or > 61) return AreaAdministrationQuery<string?>.Known(null);

            BaseReference selected = BaseLookup.Find(raw, _current);
            int physical = K(selected.PhysicalSlot, "message_base_pointer");
            string? name = selected.Kind switch
            {
                BaseKind.City => _state.Cities.TryGetValue(physical, out var city) ? city.Name : null,
                BaseKind.Gate => _state.Gates.TryGetValue(physical + 51, out var gate) ? gate.Name : null,
                _ => null
            };
            return name is not null ? AreaAdministrationQuery<string?>.Known(name)
                : AreaAdministrationQuery<string?>.Unknown("city-domestic.message_base_name");
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<string?>.Unknown(unknown.Message); }
    }
}
