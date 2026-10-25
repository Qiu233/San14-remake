using San14.Core;

namespace San14.Data;

public static partial class CityCaptureTerritorySource
{
    /// <summary>26E775's Area pass for a whole-Force absorption. The real
    /// Gate branch retains its distinct 26D2E0(...,null,0,1) call instead of
    /// treating a Gate's representative Area as an ordinary region.</summary>
    internal static void ApplyAbsorptionTerritoryCurrent(GameState state, SourceWorldContext current,
        CityCaptureAreaScope scope, int districtPhysical, IEffectiveOfficerAbilities abilities,
        ArmyReturnDistrictOperationQueries operation)
    {
        int cityId = CheckCity(current, scope.City);
        var center = CurrentGateMapQueries.FindHex(scope.City, current);
        Rings(state, current, center, unchecked(K(BaseCaptureResourceInputs.ReadCurrent(state,
            SimulationParameterKeys.BaseCapture.CentralTerritoryRadius), "absorption_center_radius") + 1), scope.NewForcePhysical, eagerAdvice: false);
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
                    if (K(current.ReadBaseNativeValidity(originalCity), "absorption_original_City_validity") &&
                        K(current.ReadBaseForceSlot(originalCity), "absorption_original_City_Force") == scope.NewForcePhysical)
                        current.AssignAreaCity(area, K(originalCity.PhysicalSlot, "absorption_original_City_identity"));
                    if (scope.OldForcePhysical != 0) Rings(operation.Latest(), current, hex, radius, scope.NewForcePhysical, eagerAdvice: false);
                    continue;
                }
                ushort objectId = K(current.ReadHexObjectLink(hex), "absorption_Gate_Hex1A");
                if (objectId is < 52 or > 61) throw Missing("absorption_Gate_Object_identity");
                var gate = BaseLookup.Find(objectId, current);
                int gateForce = K(current.ReadBaseForceSlot(gate), "absorption_Gate_B0");
                if (gateForce == 0)
                {
                    current.AssignAreaCity(area, 0);
                    operation.ChangedAreas.Add(K(area.PhysicalSlot, "absorption_neutral_Area"));
                    operation.ChangedGates.Add(K(gate.PhysicalSlot, "absorption_neutral_Gate"));
                }
                else if (gateForce == scope.NewForcePhysical)
                {
                    if (original != cityId) continue;
                    int associated = K(current.ReadAreaCitySlot(area), "absorption_Gate_Area35");
                    var associatedCity = current.ReadCityTableReference(associated <= 51 ? associated : 0)
                        ?? throw Missing("absorption_Gate_associated_City");
                    var destination = current.ReadBaseDistrictReference(associatedCity)
                        ?? throw Missing("absorption_Gate_associated_District");
                    //TC26EFA1..26F01B compares two fresh virtual78 calls
                    //on that same associated City, then passes R8=R9=0 and
                    //stack argument5=1 to the shared administration leaf.
                    ArmyCampaignRuleAdapter.TransferDistrictAdministrationBaseCurrent(current, gate, destination, operation);
                }
                else if (K(current.ReadAreaCitySlot(area), "absorption_foreign_Gate_Area35") == cityId)
                {
                    //Base B0 resolves through this Area35's current City.
                    //A foreign B0 here therefore requires an independent
                    //special Base owner rather than the ordinary Gate model.
                    throw Missing("absorption_special_Gate_owner_relocation_26E9C5");
                }
            }
        }
        finally { current.ClearNearbyTemporary(districts, true); }
    }
}
