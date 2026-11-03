using System.Collections.Immutable;
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
        NativeMapSeasonDate operationDate, List<CampaignEffectRequest> effects,
        AutomaticEmperorCaptureInputs? emperorInputs = null, bool eventPlaybackPresent = false)
        => ApplyForceBaseTransferCurrent(state, current, pool, target, destination, ruler,
            operationDate, effects, preserveResources: true, suppressMaintenance: true,
            automaticEmperorInputs: emperorInputs, allowAutomaticEmperorPause: true,
            eventPlaybackPresent: eventPlaybackPresent);

    /// <summary>NormalTurn13's 26DA10(Base,District,executor,0,0,0,1,0,1,0).
    /// The caller supplies its actual resident-transfer District writer.</summary>
    internal GameState ApplyNeutralBaseTransferCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference target,
        DistrictReference destination, TransferPersonReference ruler,
        NativeMapSeasonDate operationDate, List<CampaignEffectRequest> effects,
        Action<ArmyReturnDistrictOperationQueries> transferDistrict,
        AutomaticEmperorCaptureInputs? emperorInputs = null)
        => ApplyForceBaseTransferCurrent(state, current, pool, target, destination, ruler,
            operationDate, effects, preserveResources: false, suppressMaintenance: false,
            fullCentralRadius: false, transferDistrict: transferDistrict,
            automaticEmperorInputs: emperorInputs, allowAutomaticEmperorPause: true);

    private GameState ApplyForceBaseTransferCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference target,
        DistrictReference destination, TransferPersonReference ruler,
        NativeMapSeasonDate operationDate, List<CampaignEffectRequest> effects,
        bool preserveResources, bool suppressMaintenance, bool fullCentralRadius = true,
        Action<ArmyReturnDistrictOperationQueries>? transferDistrict = null, bool unregisterFormerForce = false,
        int forcedEmperorMode = 0, StoryForcedEmperorCaptureInputs? forcedEmperorInputs = null,
        AutomaticEmperorCaptureInputs? automaticEmperorInputs = null, bool allowAutomaticEmperorPause = false,
        bool eventPlaybackPresent = false)
    {
        // A normal-turn Force creation can enter with only the thin route
        // current. Borrow the complete saved threat bank once for this Base
        // business, preserving any fields already staged in the same current.
        CaptureBaseThreatInputs.BindCurrent(state, current);
        _personAssignmentPrecallInputs.BindMissingPersonAreaVectors(state, current);
        if (state.ForceTransferEmperorTail is { } pending)
        {
            if (target.Kind != BaseKind.City || pending.CityPhysicalSlot != target.PhysicalSlot ||
                pending.DestinationDistrictPhysicalSlot != destination.PhysicalSlot)
                throw AbsorptionBaseMissing("Emperor_resume_transfer_identity");
            return ResumeForceTransferEmperorCurrent(state, current, pool, automaticEmperorInputs,
                suppressPrimary: suppressMaintenance, eventPlaybackPresent: eventPlaybackPresent);
        }
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
            value => working = value, operationDate, effects,
            capturingForceId: unregisterFormerForce ? newForce : null);
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
                CityCaptureTerritorySource.RequireNoReachedAreaObjectService(current, areas, pending2336B0: at =>
                {
                    var directory = current.CurrentMapObjectDirectory ?? throw AbsorptionBaseMissing("Object_directory");
                    var receiver = directory.ReceiverAtPhysical(AbsorptionBaseNeed(at.PhysicalSlot, "Object_identity"));
                    return (working.ReadArmyLifecycle()?.Indexes.PendingObjectRetirements
                        ?? throw AbsorptionBaseMissing("Object_F0_pending")).Contains(receiver);
                }, remove: at => working = RemoveMapObjectWithoutActor(working, current, at, effects));
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
            {
                if (forcedEmperorMode == 0 && areas.OldForcePhysical != areas.NewForcePhysical &&
                    AbsorptionBaseNeed(current.ReadForceNativeValidity(force), "Emperor_new_Force_validity"))
                {
                    //Actual26DA10 param5 supplies the old Force only when
                    //resources are preserved.149/212/2851B0 pass literal null.
                    var excluded = preserveResources ? current.ReadForceTableReference(oldForce)
                        ?? throw AbsorptionBaseMissing("Emperor_excluded_old_Force") : null;
                    var prepared = PrepareAutomaticEmperorCaptureCurrent(working, current,
                        target, excluded, force, automaticEmperorInputs);
                    working = prepared.State;
                    if (prepared.Progress is { } progress)
                    {
                        //Only callers with a real saved parent-tail contract
                        //may publish this suspension. Other reached callers
                        //retain their previous explicit unsupported boundary.
                        if (!allowAutomaticEmperorPause) throw AbsorptionBaseMissing("automatic_Emperor_parent_continuation");
                        working = working with { AutomaticEmperorCapture = progress,
                            ForceTransferEmperorTail = new(AbsorptionBaseNeed(target.PhysicalSlot, "Emperor_City_identity"),
                                oldForce, newForce, AbsorptionBaseNeed(destination.PhysicalSlot, "Emperor_destination_District"),
                                AbsorptionBaseNeed(areas.CapturedOldDistrict.PhysicalSlot, "Emperor_old_District"), operationDate,
                                ReadForceTransferAreaPayloads(current, areas.Areas)) };
                        //Commit every completed pre-Emperor field once. The
                        //caller owns its Base/Force lists; never export them.
                        working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
                        working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current,
                            operation.ChangedAreas, operation.ChangedGates);
                        working = AppendForceTransferEmperorEffects(working, effects);
                        return BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
                    }
                }
                else if (forcedEmperorMode != 0 && areas.OldForcePhysical != areas.NewForcePhysical)
                    //382 passes26DA10 param5=0, so its source2F6890 old
                    //Force argument is literal null, not the former owner.
                    working = ApplyStoryForcedEmperorCaptureCurrent(working, current, pool, target,
                        null, new(newForce), forcedEmperorMode, effects, forcedEmperorInputs);
            }
            if (areas is not null) CityCaptureTerritorySource.ClearTargetRouting(current, target);
            else GateCaptureRoutingSource.ClearTargetRouting(current, target);
            if (areas is not null)
            {
                working = ApplyCityCaptureStateAdvantageCurrent(working, current, areas, operationDate, effects,
                    suppressPrimary: suppressMaintenance, eventPlaybackPresent: eventPlaybackPresent);
                CityCaptureTerritorySource.ApplyCityFoodWarning(working, current, target,
                    () => Quote(ReadCityCaptureFoodQuoteCurrent(working, current, target)),
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

        int Quote(CityCaptureFoodQuote quote)
        {
            int value = quote.Value ?? throw AbsorptionBaseMissing(quote.UnresolvedOwner ?? "City_food_quote");
            //Borrow the live absorption current and its outer temporaries;
            //the complete quote may retire pending Armies before the next
            //income query and final Base/District publication.
            working = quote.State ?? working;
            return value;
        }
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
