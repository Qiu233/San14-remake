using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2A2760's actual 26DA10(Base,primary,ruler,1,1,1,0,0,1,0).
    /// Borrows the enclosing absorption's current and primary list pool. There
    /// is no winning Army, battle prefix, resource loss or Army return here.</summary>
    internal GameState ApplyForceAbsorptionBaseTransferCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference target,
        DistrictReference destination, TransferPersonReference ruler,
        NativeMapSeasonDate operationDate, List<CampaignEffectRequest> effects)
        => ApplyForceBaseTransferCurrent(state, current, pool, target, destination, ruler,
            operationDate, effects, preserveResources: true, suppressMaintenance: true);

    /// <summary>NormalTurn13's 26DA10(Base,District,executor,0,0,0,1,0,1,0).
    /// The caller supplies its actual resident-transfer District writer.</summary>
    internal GameState ApplyNeutralBaseTransferCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference target,
        DistrictReference destination, TransferPersonReference ruler,
        NativeMapSeasonDate operationDate, List<CampaignEffectRequest> effects,
        Action<ArmyReturnDistrictOperationQueries> transferDistrict)
        => ApplyForceBaseTransferCurrent(state, current, pool, target, destination, ruler,
            operationDate, effects, preserveResources: false, suppressMaintenance: false,
            fullCentralRadius: false, transferDistrict: transferDistrict);

    private GameState ApplyForceBaseTransferCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference target,
        DistrictReference destination, TransferPersonReference ruler,
        NativeMapSeasonDate operationDate, List<CampaignEffectRequest> effects,
        bool preserveResources, bool suppressMaintenance, bool fullCentralRadius = true,
        Action<ArmyReturnDistrictOperationQueries>? transferDistrict = null)
    {
        if (!AbsorptionBaseNeed(current.ReadBaseNativeValidity(target), "Base_validity")) return state;
        int location = AbsorptionBaseNeed(current.ReadBaseObjectLink(target), "Base4E");
        var obj = current.ReadObjectTableReference(location <= 3000 ? location : 0)
            ?? throw AbsorptionBaseMissing("Base_Object");
        if (!AbsorptionBaseNeed(current.ReadObjectNativeValidity(obj), "Object_validity")) return state;
        int newForce = AbsorptionBaseNeed(current.ReadDistrictForceSlot(destination), "destination_Force");
        int oldForce = AbsorptionBaseNeed(current.ReadBaseForceSlot(target), "old_Force");
        var oldDistrict = current.ReadBaseDistrictReference(target) ?? throw AbsorptionBaseMissing("old_District");
        if (newForce is < 1 or > 45 || oldForce is < 0 or > 45 || preserveResources && oldForce == 0)
            throw AbsorptionBaseMissing("nonordinary_Force");
        var force = current.ReadForceTableReference(newForce) ?? throw AbsorptionBaseMissing("new_Force_pointer");
        var working = state;
        var operation = CreateCaptureDistrictOperationQueries(current, () => working,
            value => working = value, operationDate, effects);
        CityCaptureAreaScope? areas = target.Kind == BaseKind.City
            ? CityCaptureTerritorySource.CaptureRelatedAreas(current, target, newForce) : null;
        var gateAreas = target.Kind == BaseKind.Gate ? current.CreateNearbyTemporary() : null;
        var transferReader = current.TransferModeStateReader;
        try
        {
            //26D2E0 receives (Base,District,ruler,1,0): param7=0 skips
            //the resident list, then param6=1 skips Gate repair/old 2E9140.
            if (transferDistrict is not null) transferDistrict(operation);
            else TransferForceAbsorptionBaseDistrictCurrent(current, target, destination, ruler, operation, suppressMaintenance);
            current.SetObjectForceSlot13(location, (byte)newForce);
            current.CurrentSceneryLayout = new(null);
            current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
            if (areas is not null) CityCaptureTerritorySource.ApplyFootprintBeforeResources(working, current, target, newForce);
            else GateCaptureTerritorySource.ApplyFootprintBeforeResources(working, current, target, newForce);

            //param5=1 preserves gold, food, troops and wounded. Only the
            //unconditional City wall/flag update precedes the morale clamp.
            if (!preserveResources)
                BaseCaptureResourceSource.ApplyCurrent(working, current, target, ruler, CreateArmyDestructionAbilities(current));
            else if (target.Kind == BaseKind.City)
            {
                int wallCap = BasePostLoad.ReadCapacities(target, current).Field46;
                if (wallCap <= BasePostLoad.ReadLinkedField14(target, current))
                    BasePostLoad.WriteLinkedField14(target, wallCap, current);
                current.SetBasePostLoadFlag66(target, BasePostLoad.ReadFlag66(target, current));
            }
            if (preserveResources) BasePostLoad.WriteMorale(target,
                Math.Min((int)AbsorptionBaseNeed(current.ReadBaseMorale44(target), "Base44"),
                    BasePostLoad.ReadMoraleUpper(force, current)), current);

            if (areas is not null) CityCaptureTerritorySource.ClearCityAdministration(current, target);
            else
            {
                var area = CurrentGateMapQueries.FindArea(target, current);
                int physical = AbsorptionBaseNeed(area.PhysicalSlot, "Gate_Area");
                ushort assigned = AbsorptionBaseNeed(current.ReadAssignmentArea36(physical), "Gate_Area36");
                if (assigned != 0)
                {
                    current.SetAssignmentArea36(physical, 0);
                    var removed = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, physical, assigned);
                    if (removed.UnresolvedOwner is not null) throw AbsorptionBaseMissing(removed.UnresolvedOwner);
                }
            }
            //Both old-District maintenance and HomeArmy retreat require
            //param5=0 && param6=0. Absorption reaches only the new District.
            if (!preserveResources && !suppressMaintenance) Primary(oldDistrict);
            var primary = PersonAssignmentPrecallSource.Apply2E9140PrimaryForCapture(current, destination, operation);
            if (primary.UnresolvedOwner is not null) throw AbsorptionBaseMissing(primary.UnresolvedOwner);
            if (!preserveResources && !suppressMaintenance)
                working = TransferStrategicIndependenceHomeArmiesCurrent(working, current, pool, target, operationDate, effects);
            if (oldForce != newForce)
            {
                var refreshed = PersonAdvisorResetSource.RefreshWithinBaseCapture(working, current, force);
                working = refreshed.State ?? throw AbsorptionBaseMissing(refreshed.UnresolvedOwner ?? "27BE40");
            }
            if (areas is not null)
            {
                CityCaptureTerritorySource.ApplyAbsorptionTerritoryCurrent(working, current, areas,
                    AbsorptionBaseNeed(destination.PhysicalSlot, "destination"), CreateArmyDestructionAbilities(current), operation,
                    fullCentralRadius);
                CityCaptureTerritorySource.RequireNoReachedAreaObjectService(current, areas);
            }
            else GateCaptureTerritorySource.ApplyTerritoryAfterResources(working, current, target, newForce,
                0, AbsorptionBaseNeed(destination.PhysicalSlot, "destination"), eagerAdvice: false,
                centralRadiusAdjustment: fullCentralRadius ? 0 : -1);

            int bound = AbsorptionBaseNeed(ArmyDestructionParameterSeed.ReadCurrent(working,
                SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "231480");
            var threat = current.ReadCaptureBaseThreatFields(target) ?? throw AbsorptionBaseMissing("Base_threat");
            current.SetCaptureBaseThreatFields(target, threat with
            { Byte49 = unchecked((byte)Math.Min(bound, 0)), Byte4B = unchecked((byte)Math.Min(bound, 0)), Byte62 = 255, Word64 = 0 });
            if (areas is not null)
                CityCaptureTerritorySource.RequireEmperorCaptureNotReached(working, current, areas);
            if (areas is not null) CityCaptureTerritorySource.ClearTargetRouting(current, target);
            else GateCaptureRoutingSource.ClearTargetRouting(current, target);
            if (areas is not null)
            {
                CityCaptureTerritorySource.RequireNoSpecialStateAdvantageTransition(working, current, areas);
                CityCaptureTerritorySource.ApplyCityFoodWarning(working, current, target,
                    () => Quote(CityCaptureFoodWarningSource.TryReadFoodExpense(working, current, target)),
                    () => Quote(CityCaptureFoodWarningSource.TryReadPredictedFoodIncome(working, current, target,
                        operationDate, CreateArmyDestructionAbilities(current), FoodIncomeSceneries)));
            }
        }
        finally
        {
            current.TransferModeStateReader = transferReader;
            if (areas is not null) CityCaptureTerritorySource.ReleaseRelatedAreas(current, areas);
            if (gateAreas is not null) current.ClearNearbyTemporary(gateAreas, true);
        }
        working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
        working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current,
            operation.ChangedAreas, operation.ChangedGates);
        //The caller still owns its Base and District temporaries. Exporting
        //the pool here would retire or reject those legitimate live lists.
        return BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);

        static int Quote(CityCaptureFoodQuote quote) => quote.Value
            ?? throw AbsorptionBaseMissing(quote.UnresolvedOwner ?? "City_food_quote");
        void Primary(DistrictReference at)
        {
            var result = PersonAssignmentPrecallSource.Apply2E9140PrimaryForCapture(current, at, operation);
            if (result.UnresolvedOwner is not null) throw AbsorptionBaseMissing(result.UnresolvedOwner);
        }
    }

    private static void TransferForceAbsorptionBaseDistrictCurrent(SourceWorldContext current,
        BaseReference target, DistrictReference destination, TransferPersonReference ruler,
        ArmyReturnDistrictOperationQueries operation, bool suppressMaintenance)
    {
        int district = AbsorptionBaseNeed(destination.PhysicalSlot, "District_identity");
        var oldDistrict = current.ReadBaseDistrictReference(target) ?? throw AbsorptionBaseMissing("district_old_District");
        int location = AbsorptionBaseNeed(current.ReadBaseObjectLink(target), "district_Base4E");
        if (location < 52) current.SetCityDistrictSlot(AbsorptionBaseNeed(target.PhysicalSlot, "City"), (byte)district);
        else
        {
            var area = CurrentGateMapQueries.FindArea(target, current);
            int original = AbsorptionBaseNeed(current.ReadAreaValidityCode(area), "Gate_Area34");
            var city = current.ReadCityTableReference(original <= 51 ? original : 0)
                ?? throw AbsorptionBaseMissing("Gate_original_City");
            if (AbsorptionBaseNeed(current.ReadBaseDistrictSlot(city), "Gate_original_District") == district)
                AssignGate(target, AbsorptionBaseNeed(city.PhysicalSlot, "Gate_original_City_id"));
            else if (AbsorptionBaseNeed(current.ReadPersonNativeValidity(AbsorptionBaseNeed(ruler.PhysicalSlot, "ruler")), "ruler_validity"))
            {
                var home = BaseLookup.Find(AbsorptionBaseNeed(current.ReadPersonFirstLocation(ruler.PhysicalSlot!.Value), "ruler_E2"), current);
                int homeId = AbsorptionBaseNeed(current.ReadBaseObjectLink(home), "ruler_Home4E");
                if (homeId > 51) homeId = AbsorptionBaseNeed(CurrentGateMapQueries.FindCity(home, current).PhysicalSlot, "ruler_HomeCity");
                if (homeId is >= 1 and <= 51) AssignGate(target, homeId);
            }
        }
        if (suppressMaintenance) return; //param5=0 skips residents; param4=1 skips this tail.
        if (location < 52 && AbsorptionBaseNeed(current.ReadDistrictNumber(district), "primary_number") == 1)
            foreach (var gate in DistrictCreationGates(current))
            {
                var area = CurrentGateMapQueries.FindArea(gate, current);
                if (AbsorptionBaseNeed(current.ReadAreaValidityCode(area), "repair_Gate_original_City") != target.PhysicalSlot ||
                    AbsorptionBaseNeed(current.ReadBaseDistrictSlot(gate), "repair_Gate_District") != district ||
                    CurrentGateMapQueries.FindCity(gate, current) == target) continue;
                AssignGate(gate, AbsorptionBaseNeed(target.PhysicalSlot, "repair_City"));
            }
        var maintained = PersonAssignmentPrecallSource.Apply2E9140PrimaryForCapture(current, oldDistrict, operation);
        if (maintained.UnresolvedOwner is not null) throw AbsorptionBaseMissing(maintained.UnresolvedOwner);
        return;

        void AssignGate(BaseReference gate, int city)
        {
            var area = CurrentGateMapQueries.FindArea(gate, current);
            int previous = AbsorptionBaseNeed(current.ReadAreaCitySlot(area), "Gate_Area35");
            if (previous != city)
            {
                var before = current.ReadCityTableReference(previous <= 51 ? previous : 0)
                    ?? throw AbsorptionBaseMissing("Gate_previous_City");
                var oldDistrict = current.ReadBaseDistrictReference(before) ?? throw AbsorptionBaseMissing("Gate_previous_District");
                foreach (int person in DistrictCreationHomePeople(current, gate))
                    operation.Assignment(person, PersonAssignmentPrecallSource.Apply29C430ForHomeTransfer(current, person, oldDistrict, before));
            }
            current.AssignAreaCity(area, city);
            operation.ChangedAreas.Add(AbsorptionBaseNeed(area.PhysicalSlot, "changed_Area"));
            operation.ChangedGates.Add(AbsorptionBaseNeed(gate.PhysicalSlot, "changed_Gate"));
        }
    }

    private static T AbsorptionBaseNeed<T>(T? value, string field) where T : struct
        => value ?? throw AbsorptionBaseMissing(field);
    private static InvalidDataException AbsorptionBaseMissing(string field)
        => new("force-absorption.Base26DA10." + field);
}
