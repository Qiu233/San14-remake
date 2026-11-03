namespace San14.Data;

public sealed partial class AreaAdministrationDraft
{
    /// <summary>Raw Hex12 for the area-focus display provider. This deliberately
    /// does not perform Area lookup, validity or held-target filtering.</summary>
    public AreaAdministrationQuery<ushort?> QueryMapRawArea(int hexId)
    {
        try
        {
            EnsureOpen();
            return AreaAdministrationQuery<ushort?>.Known(
                K(_current.ReadHexAreaSlot(MapHex(hexId)), "map_raw_area"));
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<ushort?>.Unknown(unknown.Message); }
    }

    /// <summary>The original map tint predicate, independently of input and held-target admission.</summary>
    public AreaAdministrationQuery<bool?> QueryMapTint(int hexId)
    {
        try
        {
            EnsureOpen();
            var hex = MapHex(hexId);
            int code = K(_current.ReadHexSurfaceCode(hex), "map_surface");
            var surface = _current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw Missing("map_surface_pointer");
            if (K(_current.ReadSurfaceTypeRawField2A(surface), "map_surface_cost") == 0)
                return AreaAdministrationQuery<bool?>.Known(true);
            var city = _current.ReadCityTableReference(_cityId) ?? throw Missing("map_city_pointer");
            int rawCityForce = K(_current.ReadBaseForceSlot(city), "map_city_force");
            var cityForce = _current.ReadForceTableReference(rawCityForce <= 51 ? rawCityForce : 0) ?? throw Missing("map_city_force_pointer");
            var area = MapArea(hex);
            int physical = K(area.PhysicalSlot, "map_area_receiver");
            if (ParentAreaForce(physical).PhysicalSlot != cityForce.PhysicalSlot)
                return AreaAdministrationQuery<bool?>.Known(false);
            int rawHexForce = K(_current.ReadHexForceSlot(hex), "map_hex_force");
            var hexForce = _current.ReadForceTableReference(rawHexForce <= 51 ? rawHexForce : 0) ?? throw Missing("map_hex_force_pointer");
            if (hexForce.PhysicalSlot != cityForce.PhysicalSlot)
                return AreaAdministrationQuery<bool?>.Known(false);
            int cityPhysical = K(city.PhysicalSlot, "map_city_receiver");
            if (cityPhysical is < 1 or > 51) return AreaAdministrationQuery<bool?>.Known(false);
            // Construction already validates the complete registered Area35
            // relation. Draft personnel/focus changes do not rewrite it.
            return AreaAdministrationQuery<bool?>.Known(_registeredCityAreas.Contains(physical));
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<bool?>.Unknown(unknown.Message); }
    }

    /// <summary>Resolves a map target; its held membership does not substitute for frame/input gates.</summary>
    public AreaAdministrationQuery<AreaAdministrationMapTarget> QueryMapTarget(int hexId)
    {
        try
        {
            EnsureOpen(); var hex = MapHex(hexId);
            if (!hex.PhysicalSlot.HasValue)
                return AreaAdministrationQuery<AreaAdministrationMapTarget>.Known(new(null, false));
            var area = MapArea(hex);
            return AreaAdministrationQuery<AreaAdministrationMapTarget>.Known(new(area.PhysicalSlot,
                area.PhysicalSlot is int physical && _held.Contains(physical)));
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<AreaAdministrationMapTarget>.Unknown(unknown.Message); }
    }

    /// <summary>43F660's current representative-hex classification; other known surfaces hide the icon.</summary>
    public AreaAdministrationQuery<AreaAdministrationPlaceKind?> QueryAreaPlaceKind(int areaId)
    {
        try
        {
            EnsureOpen();
            if (!_held.Contains(areaId)) throw Missing("place_area_outside_held");
            int raw = K(_current.ReadAreaHexIndex(new(areaId)), "place_representative_hex");
            var hex = _current.ReadHexTableReference(raw <= 48399 ? raw : 0) ?? throw Missing("place_hex_pointer");
            return AreaAdministrationQuery<AreaAdministrationPlaceKind?>.Known(K(_current.ReadHexSurfaceCode(hex), "place_surface") switch
            { 16 => AreaAdministrationPlaceKind.City, 17 => AreaAdministrationPlaceKind.Gate,
              18 => AreaAdministrationPlaceKind.Port, _ => AreaAdministrationPlaceKind.Hidden });
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<AreaAdministrationPlaceKind?>.Unknown(unknown.Message); }
    }

    private HexReference MapHex(int hexId)
    {
        if (hexId is < 0 or >= 48400) throw Missing("map_hex_index");
        return _current.ReadHexTableReference(hexId) ?? throw Missing("map_hex_pointer");
    }
    private AreaReference MapArea(HexReference hex)
    {
        int raw = K(_current.ReadHexAreaSlot(hex), "map_hex_area");
        return _current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw Missing("map_area_pointer");
    }
}
