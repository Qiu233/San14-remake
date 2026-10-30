using San14.Core;

namespace San14.Data;

/// <summary>One source-captured Hex appearance plus this actual map owner's
/// final display input. A missing old Force color stays unknown; final-state
/// presentation need not invent that color while animation is deferred.</summary>
public sealed record RegionEncirclementHexPublication(int HexId, int? PreviousForceId,
    byte? PreviousForceReportColor, int? CurrentForceId, byte CurrentForceReportColor,
    string? PreviousLookUnresolved);
public sealed record RegionEncirclementAreaPublication(int AreaId, string Name,
    int RepresentativeHexId, int? CurrentForceId, int? CurrentCityId);
public sealed record RegionEncirclementPublication(RegionEncirclementPresentation Request,
    int ForceId, int? CityId, IReadOnlyList<RegionEncirclementAreaPublication> Areas,
    IReadOnlyList<RegionEncirclementHexPublication> Hexes);

/// <summary>294FD0's actual category4 obstacle destruction. Position is the
/// source-time Hex; effect allocation, ground, camera cull and spatial sound
/// acceptance belong to the real map, independently of numerical retirement.</summary>
public sealed record OccupationObstaclePresentationPublication(MapObjectPresentationRequest Request,
    int HexId, int DefinitionId, int EffectId, float Scale, int SoundId);

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<RegionEncirclementPublication> QueryRegionEncirclementPresentation(
        GameState world, RegionEncirclementPresentation request)
    {
        try
        {
            var pending = world.RegionOccupationFollowups ?? throw EncirclementPresentationMissing("queue_owner");
            if (pending.EncirclementPresentations.Count == 0 ||
                !ReferenceEquals(pending.EncirclementPresentations[0], request))
                throw EncirclementPresentationMissing("current_request_head");
            var areas = new List<RegionEncirclementAreaPublication>(request.AreaIds.Count);
            foreach (int id in request.AreaIds)
            {
                var area = world.Areas.GetValueOrDefault(id) ?? throw EncirclementPresentationMissing("Area_owner");
                areas.Add(new(id, area.Name, area.HexId ?? throw EncirclementPresentationMissing("Area_Hex"), area.ForceId, area.CityId));
            }
            var hexes = new List<RegionEncirclementHexPublication>(request.Hexes.Count);
            foreach (var source in request.Hexes)
            {
                var hex = world.Territory.GetValueOrDefault(source.HexId) ?? throw EncirclementPresentationMissing("Hex_owner");
                byte color = CurrentColor(hex.ForceId);
                byte? oldColor;
                string? missing = null;
                if (source.PreviousForceId is null or 0) oldColor = 60; //Known native Force0: no tint.
                else
                {
                    oldColor = world.Forces.GetValueOrDefault(source.PreviousForceId.Value)?.ReportColorIndex;
                    if (oldColor is null) missing = "region.encirclement.presentation.source_previous_Force_color";
                }
                hexes.Add(new(source.HexId, source.PreviousForceId, oldColor, hex.ForceId, color, missing));
            }
            return AreaAdministrationQuery<RegionEncirclementPublication>.Known(new(request,
                request.ForceId, request.CityId, areas.AsReadOnly(), hexes.AsReadOnly()));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<RegionEncirclementPublication>.Unknown(error.Message); }

        byte CurrentColor(int? force) => force is null or 0 ? (byte)60 :
            world.Forces.GetValueOrDefault(force.Value)?.ReportColorIndex
                ?? throw EncirclementPresentationMissing("current_Force_color");
    }

    public AreaAdministrationQuery<OccupationObstaclePresentationPublication> QueryOccupationObstaclePresentation(
        GameState world, MapObjectPresentationRequest request)
    {
        try
        {
            if (request.Event is not (MapObjectPresentationEvent.DestructionVisual or MapObjectPresentationEvent.DestructionSound) ||
                request.HexId is not (>= 0 and < 48400) || request.DefinitionKey is null)
                throw EncirclementPresentationMissing("obstacle_request");
            if (world.MapObjects?.Find(request.ReceiverId) is null)
                throw EncirclementPresentationMissing("obstacle_receiver");
            int id = MapObjectSourceDirectory.DefinitionSlot(request.DefinitionKey);
            var catalog = _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath).Catalog.Definitions;
            //294FD0 uses the same80/C4 presentation for every actual
            //Object definition. Category4 suppresses only its history suffix.
            if ((uint)id >= (uint)catalog.Count)
                throw EncirclementPresentationMissing("Object_definition");
            //The source294FD0 calls37E6C0(kind80,scale1) and405370(C4).
            //These normal resource IDs are independent of renderer handles.
            return AreaAdministrationQuery<OccupationObstaclePresentationPublication>.Known(
                new(request, request.HexId.Value, id, 80, 1f, 0xC4));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<OccupationObstaclePresentationPublication>.Unknown(error.Message); }
    }

    private static InvalidDataException EncirclementPresentationMissing(string field) =>
        new("region.encirclement.presentation." + field);
}
