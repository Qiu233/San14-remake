using San14.Core;

namespace San14.Data;

public enum SceneryMapFlagUpdateKind { Unknown, NoUpdate, Update }
public sealed record SceneryMapFlagUiContext(int? MapInfoMode, byte? MapInfoFlags15, bool? IsTitleState);
public sealed record SceneryMapFlagUpdate(SceneryMapFlagUpdateKind Kind,
    MapCornerColors? Fill, MapCornerColors? InactiveFill,
    MapCornerColors? Outline, MapCornerColors? InactiveOutline, bool? ShowOutline,
    int? OwnerForcePhysicalSlot, int? DistrictPhysicalSlot, bool? DistrictValid,
    byte? DistrictForce10, byte? DistrictNumber11, string? NoUpdateReason,
    IReadOnlyList<string> Unavailable);

/// <summary>Read-only Scenery 1A6940 publication over the caller's same current
/// context. Names, positions, registry and presentation lifetime are independent.
/// Only explicitly published Map0/category15=0 or4/non-Title outline profiles
/// are implemented; unsupported or missing profiles retain partial flag knowledge.</summary>
public sealed class SceneryMapFlagCurrentSource(AreaMapMarkerSources markerSources, MapSpritePalettes palettes)
{
    public SceneryMapFlagUpdate Read(GameState world, SourceWorldContext current,
        SceneryReference scenery, SceneryMapFlagUiContext ui)
    {
        var missing = new List<string>();
        int? ownerPhysical = null, districtPhysical = null;
        bool? districtValid = null;
        byte? districtForce = null, districtNumber = null;
        ForceReference owner;
        DistrictReference district;
        try
        {
            bool valid = Need(current.ReadSceneryNativeValidity(scenery), "Scenery.validity");
            if (!valid) return Result(SceneryMapFlagUpdateKind.NoUpdate, "Scenery.invalid");
            // 22CFC0 is the raw byte getter. Unlike 22CBC0, its nonzero gate
            // must retain an out-of-range byte until the representative comparison.
            byte rawForce = Need(current.ReadConditionSceneryForceSlot(scenery), "Scenery.rawForce");
            owner = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
                ?? throw new Unresolved("Scenery.ownerForce.pointer");
            if (owner.PhysicalSlot is null)
                return Result(SceneryMapFlagUpdateKind.NoUpdate, "Scenery.ownerForce.null");
            ownerPhysical = Need(current.ReadForcePhysicalSlot(owner), "Scenery.ownerForce.physical");
            district = ReadDistrict(current, scenery, rawForce)
                ?? throw new Unresolved("Scenery.District.pointer");
            if (district.PhysicalSlot is not int physical)
                return Result(SceneryMapFlagUpdateKind.NoUpdate, "Scenery.District.null");
            districtPhysical = physical;
            districtForce = current.ReadDistrictForceId(physical);
            districtNumber = current.ReadDistrictNumber(physical);
            districtValid = current.ReadDistrictNativeValidity(district);
        }
        catch (Unresolved unknown)
        {
            missing.Add(unknown.Field);
            return Result(SceneryMapFlagUpdateKind.Unknown);
        }

        MapCornerColors? fill = null, inactiveFill = null, outline = null, inactiveOutline = null;
        bool? selectionOutline = null;
        if (current.ReadForceReportColor(owner) is byte color)
        {
            fill = Solid(markerSources.ForceFillColor(color));
            inactiveFill = Darken(fill.Value);
        }
        else missing.Add("Fill.Force.Color49"); // No Force native-validity gate.

        if (ui.MapInfoMode == 0 && ui.MapInfoFlags15 is 0 or 4 && ui.IsTitleState == false)
        {
            var selected = ui.MapInfoFlags15 == 4
                ? ArmySimpleOutlineSource.ReadConfiguredSceneryMap(world, current, district)
                : ArmySimpleOutlineSource.ReadOrdinaryMap(world, current, district);
            if (selected.Selection is { } selection)
            {
                selectionOutline = selection.UsesOutline;
                outline = Materialize(selection.Primary);
                inactiveOutline = Materialize(selection.Disabled);
            }
            else missing.Add("Outline." + selected.Missing);
        }
        else
        {
            if (ui.MapInfoMode is null) missing.Add("Outline.MapInfo.mode0C");
            if (ui.MapInfoFlags15 is null) missing.Add("Outline.MapInfo.flags15");
            if (ui.IsTitleState is null) missing.Add("Outline.TitleState");
            if (ui.MapInfoMode is not null && ui.MapInfoFlags15 is not null && ui.IsTitleState is not null)
                missing.Add("Outline.unsupportedMapInfoProfile");
        }
        // The final comparison uses the raw District10 byte, not the resolved
        // District Force pointer. It can force visibility even if style is unknown.
        bool? show = districtForce is byte raw
            ? ownerPhysical != raw ? true : selectionOutline : null;
        if (districtForce is null) missing.Add("Outline.District10");
        return new(SceneryMapFlagUpdateKind.Update, fill, inactiveFill, outline, inactiveOutline,
            show, ownerPhysical, districtPhysical, districtValid, districtForce, districtNumber,
            null, missing.AsReadOnly());

        SceneryMapFlagUpdate Result(SceneryMapFlagUpdateKind kind, string? reason = null) =>
            new(kind, null, null, null, null, null, ownerPhysical, districtPhysical,
                districtValid, districtForce, districtNumber, reason, missing.AsReadOnly());
    }

    private static DistrictReference? ReadDistrict(SourceWorldContext current,
        SceneryReference scenery, byte rawForce)
    {
        if (rawForce == 0) return current.ReadDistrictTableReference(0);
        ushort rawHex = Need(current.ReadSceneryHexIndex(scenery), "Scenery.Hex44");
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
            ?? throw new Unresolved("Scenery.Hex.pointer");
        int areaSlot = 0;
        if (hex.PhysicalSlot.HasValue)
        {
            ushort rawArea = Need(current.ReadHexAreaSlot(hex), "Scenery.Hex.Area12");
            if (rawArea <= 500) areaSlot = rawArea;
        }
        var area = current.ReadAreaTableReference(areaSlot)
            ?? throw new Unresolved("Scenery.Area.pointer");
        if (area.PhysicalSlot is null) throw new Unresolved("Scenery.Area.unsafeNull");
        ushort representativeHex = Need(current.ReadAreaHexIndex(area), "Scenery.Area.Hex3A");
        var representative = current.ReadHexTableReference(representativeHex < 48400 ? representativeHex : 0)
            ?? throw new Unresolved("Scenery.Area.Hex.pointer");
        if (representative.PhysicalSlot is null) throw new Unresolved("Scenery.Area.Hex.unsafeNull");
        byte representativeForce = Need(current.ReadHexForceSlot(representative), "Scenery.Area.Hex.Force14");
        if (rawForce != representativeForce) return current.ReadDistrictTableReference(0);
        // 22AA50 repeats this same read-only equality before taking Area35.
        // No Area native-validity gate is added by 22AA10 or Base.v80.
        byte citySlot = Need(current.ReadAreaCitySlot(area), "Scenery.Area.City35");
        var city = current.ReadCityTableReference(citySlot < 52 ? citySlot : 0)
            ?? throw new Unresolved("Scenery.Base.pointer");
        if (city.PhysicalSlot is null) throw new Unresolved("Scenery.Base.unsafeNull");
        return current.ReadBaseDistrictReference(city);
    }

    private MapCornerColors? Materialize(ArmySimpleOutlineColorChoice choice)
    {
        MapCornerColors? color = choice.Kind switch
        {
            ArmySimpleOutlineColorKind.Preserve => null,
            ArmySimpleOutlineColorKind.DistrictNumber => Solid(markerSources.DistrictColor((int)choice.Value)),
            ArmySimpleOutlineColorKind.ForceFill => Solid(markerSources.ForceFillColor((int)choice.Value)),
            ArmySimpleOutlineColorKind.SpritePalette => palettes[(int)choice.Value],
            ArmySimpleOutlineColorKind.PackedLiteral => Solid(new(choice.Value, choice.Value)),
            _ => throw new InvalidDataException("Unsupported source outline color choice.")
        };
        if (color is null || choice.RgbMultiplier == 1) return color;
        if (choice.RgbMultiplier == 0.4f) return Darken(color.Value);
        throw new InvalidDataException("Unsupported source outline RGB operation.");
    }

    private static MapCornerColors Solid(AreaMarkerColor color) => new(color, color, color, color);
    private static MapCornerColors Darken(MapCornerColors color) =>
        new(Darken(color.LT), Darken(color.RT), Darken(color.LB), Darken(color.RB));
    private static AreaMarkerColor Darken(AreaMarkerColor color) =>
        new(AreaMapMarkerSources.DarkenRgb(color.AuthoredPacked), AreaMapMarkerSources.DarkenRgb(color.CpuPacked));
    private static T Need<T>(T? value, string field) where T : struct =>
        value ?? throw new Unresolved(field);
    private sealed class Unresolved(string field) : Exception
    {
        internal string Field { get; } = field;
    }
}
