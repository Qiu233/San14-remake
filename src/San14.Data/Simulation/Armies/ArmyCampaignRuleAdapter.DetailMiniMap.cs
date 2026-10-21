using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Save preview85AF20 shows only the existing city slots, with
    /// saved colours and no selection. This project's colours come from the
    /// decoded save's current city/force authority, not an original SAV header.</summary>
    public DetailMiniMapValues? ReadCampaignSaveMiniMap(GameState state)
    {
        var current = CaptureDetailForceInputs(state);
        if (current is null) return null;
        var cities = new List<DetailMiniMapMarker>();
        for (int cityId = 1; cityId <= 46; cityId++)
        {
            var city = current.ReadCityTableReference(cityId);
            if (city is null || ReadMiniMapColor(current, current.ReadBaseForceSlot(city)) is not byte color)
                return null;
            cities.Add(DetailMiniMapProjection.Project(DetailMiniMapIconKind.City, cityId,
                DetailMiniMapLocations.ReadHex(cityId), color));
        }
        return new(cities.AsReadOnly(), null);
    }

    public DetailMiniMapValues? ReadCityDetailMiniMap(GameState state, int cityId) =>
        ReadDetailMiniMap(CaptureDetailForceInputs(state), DetailMiniMapIconKind.City, cityId);

    public DetailMiniMapValues? ReadAreaDetailMiniMap(GameState state, int areaId) =>
        ReadDetailMiniMap(CaptureDetailForceInputs(state), DetailMiniMapIconKind.Area, areaId);

    /// <summary>A newly created Force detail map. Native invalid entrance
    /// forces leave the initialized base points without creating a selection;
    /// null means a reached current input is unknown.</summary>
    public DetailMiniMapValues? ReadForceDetailMiniMap(GameState state, int forceId)
    {
        if ((uint)forceId >= 52)
            throw new InvalidDataException("Detail force identity is outside the source table.");
        var current = CaptureDetailForceInputs(state);
        if (current is null) return null;
        try
        {
            var bases = ReadDetailMiniMapBases(current);
            if (bases is null) return null;
            var force = current.ReadForceTableReference(forceId);
            if (force is null || current.ReadForceNativeValidity(force) is not bool valid) return null;
            if (!valid) return new(bases, null);
            if (current.ReadForcePhysicalSlot(force) is not int physicalForce ||
                current.ReadForceRecord(physicalForce)?.RawRuler10 is not ushort rawRuler) return null;
            var person = rawRuler <= 6000 ? current.ReadPersonLookupReference(rawRuler)
                : current.ReadTransferPersonTableReference(0);
            // 906C50 reads E2 directly; the nameplate's Person validity gate
            // does not guard this independent map operand.
            if (person?.PhysicalSlot is not int physicalPerson ||
                current.ReadPersonFirstLocation(physicalPerson) is not ushort firstLocation) return null;
            var receiver = BaseLookup.Find(firstLocation, current);
            ushort hex = current.ReadBaseCurrentHexIndex(receiver);
            // The fresh marker consumer84E2F0 looks up the entrance physical
            // identity again, then reads that Force's validity and color.
            if (ReadMiniMapColor(current, physicalForce) is not byte color) return null;
            var selection = DetailMiniMapProjection.ProjectForce(physicalForce, hex, color);
            return new(bases, selection);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static DetailMiniMapValues? ReadDetailMiniMap(SourceWorldContext? current,
        DetailMiniMapIconKind selectionKind, int selectionId)
    {
        if (current is null) return null;
        try
        {
            var bases = ReadDetailMiniMapBases(current);
            if (bases is null) return null;
            int? selectedHex, selectedForce;
            if (selectionKind == DetailMiniMapIconKind.City)
            {
                var city = current.ReadCityTableReference(selectionId);
                if (city is null) return null;
                selectedHex = current.ReadBaseCurrentHexIndex(city);
                selectedForce = current.ReadBaseForceSlot(city);
            }
            else
            {
                var area = current.ReadAreaTableReference(selectionId);
                selectedHex = area is null ? null : current.ReadAreaHexIndex(area);
                selectedForce = area is null ? null : current.ReadAreaCurrentForceSlot(area);
            }
            if (selectedHex is not (>= 0 and < 48400) || ReadMiniMapColor(current, selectedForce) is not byte selectedColor)
                return null;
            var selection = DetailMiniMapProjection.Project(selectionKind, selectionId, selectedHex.Value, selectedColor);
            return new(bases, selection);
        }
        catch (InvalidDataException)
        {
            // Existing registered-base/map leaves signal missing reached
            // fields this way. A partial map is not a current publication.
            return null;
        }
    }

    private static IReadOnlyList<DetailMiniMapMarker>? ReadDetailMiniMapBases(SourceWorldContext current)
    {
        var bases = new List<DetailMiniMapMarker>();
        for (int location = 1; location <= 61; location++)
        {
            // The original does not construct icons for these five cities.
            if (location is >= 47 and <= 51) continue;
            var receiver = BaseLookup.Find(location, current);
            if (current.ReadBaseNativeValidity(receiver) is not bool valid) return null;
            if (!valid) continue;
            if (ReadMiniMapColor(current, current.ReadBaseForceSlot(receiver)) is not byte color) return null;
            bases.Add(DetailMiniMapProjection.Project(location <= 51 ? DetailMiniMapIconKind.City : DetailMiniMapIconKind.Gate,
                location, DetailMiniMapLocations.ReadHex(location), color));
        }
        return bases.AsReadOnly();
    }

    private static byte? ReadMiniMapColor(SourceWorldContext current, int? rawForce)
    {
        if (rawForce is not int id) return null;
        var force = current.ReadForceTableReference((uint)id < 52 ? id : 0);
        if (force is null || current.ReadForceNativeValidity(force) is not bool valid) return null;
        // Both the base refresh and a newly created selection use this fixed
        // white entry for invalid forces, independently of Force0.Color49.
        return valid ? current.ReadForceReportColor(force) : (byte)60;
    }
}
