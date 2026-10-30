using San14.Core;

namespace San14.Data;

public static partial class CityCaptureTerritorySource
{
    /// <summary>26E775's Area pass for a whole-Force absorption. The real
    /// Gate branch retains its distinct 26D2E0(...,null,0,1) call instead of
    /// treating a Gate's representative Area as an ordinary region.</summary>
    internal static void ApplyAbsorptionTerritoryCurrent(GameState state, SourceWorldContext current,
        CityCaptureAreaScope scope, int districtPhysical, IEffectiveOfficerAbilities abilities,
        ArmyReturnDistrictOperationQueries operation, bool fullCentralRadius = true, bool eagerAdvice = false)
    {
        int cityId = CheckCity(current, scope.City);
        var center = CurrentGateMapQueries.FindHex(scope.City, current);
        Rings(state, current, center, unchecked(K(BaseCaptureResourceInputs.ReadCurrent(state,
            SimulationParameterKeys.BaseCapture.CentralTerritoryRadius), "absorption_center_radius") +
            (fullCentralRadius ? 1 : 0)), scope.NewForcePhysical, eagerAdvice);
        var changed = new List<int>();
        foreach (var hex in Footprint(current, scope.City)) Raw(current, hex, scope.NewForcePhysical, changed);
        int radius = ReadAreaRadius(operation.Latest(), current, districtPhysical, scope.NewForcePhysical, abilities);
        //The source allocates this list before any related Area, even when
        //the ordinary branch never adds a District to it.
        var districts = current.CreateNearbyTemporary();
        try
        {
            foreach (var area in Areas(current, scope.Areas))
            {
                var hex = AreaHex(current, area);
                int original = K(current.ReadAreaValidityCode(area), "absorption_Area34");
                if (K(current.ReadHexSurfaceCode(hex), "absorption_Area_surface") != 17)
                {
                    current.AssignAreaCity(area, cityId);
                    var originalCity = current.ReadCityTableReference(original <= 51 ? original : 0)
                        ?? throw Missing("absorption_original_City");
                    if (scope.NewForcePhysical != 0 && K(current.ReadBaseNativeValidity(originalCity), "absorption_original_City_validity") &&
                        K(current.ReadBaseForceSlot(originalCity), "absorption_original_City_Force") == scope.NewForcePhysical)
                        current.AssignAreaCity(area, K(originalCity.PhysicalSlot, "absorption_original_City_identity"));
                    if (scope.OldForcePhysical != 0) Rings(operation.Latest(), current, hex, radius, scope.NewForcePhysical, eagerAdvice);
                    continue;
                }
                var neighborhoods = (operation.Package ?? throw Missing("related_Gate_neighborhood_owner"))
                    .CityCaptureNeighborhoodsCurrent();
                _ = ApplyRelatedGateAreaCurrent(current, scope, area, districts, operation, neighborhoods);
            }
            CompleteRelatedGateDistrictsCurrent(current, districts, operation);
        }
        finally { current.ClearNearbyTemporary(districts, true); }
    }
}
