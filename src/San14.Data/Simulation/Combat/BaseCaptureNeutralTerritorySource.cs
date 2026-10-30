using San14.Core;

namespace San14.Data;

internal sealed record NeutralBaseCaptureTerritoryResult(GameState State, bool NativeTerminal);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The neutral District0 form of26DA10(Base,0,null,1,0,0,1,0,1,1).
    /// Existing no-winner callers keep their original eager presentation and
    /// terminal policy. Story149 passes its exact executor/null and flags.</summary>
    internal NeutralBaseCaptureTerritoryResult ApplyNeutralBaseCaptureTerritoryCurrent(
        GameState state, SourceWorldContext current, WorldIndexContext pool, BaseReference target,
        NativeMapSeasonDate? date, List<CampaignEffectRequest> effects, ArmyMapGroupingOwner? groups = null,
        bool eagerAdvice = true, bool stopOnTerminal = true,
        TransferPersonReference? resourceExecutor = null, bool literalNullExecutor = false,
        bool eventPlaybackPresent = false)
    {
        if (!NeutralCaptureNeed(current.ReadBaseNativeValidity(target), "Base_validity")) return new(state, false);
        int location = NeutralCaptureNeed(current.ReadBaseObjectLink(target), "Base4E");
        var obj = current.ReadObjectTableReference(location <= 3000 ? location : 0)
            ?? throw NeutralCaptureMissing("Base_Object");
        if (!NeutralCaptureNeed(current.ReadObjectNativeValidity(obj), "Object_validity")) return new(state, false);
        var destination = current.ReadDistrictTableReference(0) ?? throw NeutralCaptureMissing("District0");
        var force = current.ReadForceTableReference(0) ?? throw NeutralCaptureMissing("Force0");
        var commander = literalNullExecutor ? null : resourceExecutor ??
            current.ReadTransferPersonTableReference(0) ?? throw NeutralCaptureMissing("Person0");
        if (NeutralCaptureNeed(current.ReadDistrictNativeValidity(destination), "District0_validity") ||
            NeutralCaptureNeed(current.ReadDistrictForceSlot(destination), "District0_Force") != 0 ||
            NeutralCaptureNeed(current.ReadForceNativeValidity(force), "Force0_validity") ||
            resourceExecutor is null && !literalNullExecutor &&
            NeutralCaptureNeed(current.ReadPersonNativeValidity(NeutralCaptureNeed(commander?.PhysicalSlot, "Person0_identity")), "Person0_validity"))
            throw NeutralCaptureMissing("nonempty_neutral_placeholder");
        int oldForce = NeutralCaptureNeed(current.ReadBaseForceSlot(target), "old_Force");
        var oldDistrict = current.ReadBaseDistrictReference(target) ?? throw NeutralCaptureMissing("old_District");
        int oldDistrictId = NeutralCaptureNeed(oldDistrict.PhysicalSlot, "old_District_identity");
        var working = state;
        bool terminal = false;
        var operation = CreateCaptureDistrictOperationQueries(current, () => working, value => working = value,
            date, effects, groups: groups, forceRetirementHandler: victim =>
            {
                var retired = new ForceUnregisterSource(this).ApplyDeathCurrent(working, current, pool,
                    current.ReadForceTableReference(victim) ?? throw NeutralCaptureMissing("retired_Force"),
                    date ?? working.NativeMapSeason?.ThirdDate ?? throw NeutralCaptureMissing("retirement_date"), effects);
                terminal = retired.NativeReturn != 0;
                return retired.State;
            });
        CityCaptureAreaScope? areas = target.Kind == BaseKind.City
            ? CityCaptureTerritorySource.CaptureRelatedAreas(current, target, 0) : null;
        var gateAreas = target.Kind == BaseKind.Gate ? current.CreateNearbyTemporary() : null;
        var transferReader = current.TransferModeStateReader;
        try
        {
            ApplyBody();
        }
        finally
        {
            current.TransferModeStateReader = transferReader;
            if (areas is not null) CityCaptureTerritorySource.ReleaseRelatedAreas(current, areas);
            if (gateAreas is not null) current.ClearNearbyTemporary(gateAreas, true);
        }
        return Publish();

        void ApplyBody()
        {
            //26D2E0's invalid-District branch skips resident collection even
            //though param7=1. Its param6=0 old-primary maintenance still runs.
            current.SetArmyReturnBaseGovernor2E(target, 0);
            if (areas is not null)
            {
                current.SetCityDistrictSlot(NeutralCaptureNeed(target.PhysicalSlot, "City_identity"), 0);
                if (NeutralCaptureNeed(current.ReadDistrictNumber(0), "District0_number") == 1)
                    foreach (var gate in DistrictCreationGates(current))
                    {
                        var area = CurrentGateMapQueries.FindArea(gate, current);
                        if (NeutralCaptureNeed(current.ReadAreaValidityCode(area), "repair_Gate_Area34") != target.PhysicalSlot ||
                            NeutralCaptureNeed(current.ReadBaseDistrictSlot(gate), "repair_Gate_District") != 0 ||
                            CurrentGateMapQueries.FindCity(gate, current).PhysicalSlot == target.PhysicalSlot) continue;
                        var previousCity = CurrentGateMapQueries.FindCity(gate, current);
                        var previousDistrict = current.ReadBaseDistrictReference(previousCity)
                            ?? throw NeutralCaptureMissing("repair_Gate_previous_District");
                        foreach (int person in DistrictCreationHomePeople(current, gate))
                            operation.Assignment(person, PersonAssignmentPrecallSource.Apply29C430ForHomeTransfer(
                                current, person, previousDistrict, previousCity));
                        current.AssignAreaCity(area, NeutralCaptureNeed(target.PhysicalSlot, "repair_City"));
                        operation.ChangedAreas.Add(NeutralCaptureNeed(area.PhysicalSlot, "repair_Area"));
                        operation.ChangedGates.Add(NeutralCaptureNeed(gate.PhysicalSlot, "repair_Gate"));
                    }
            }
            else
            {
                var area = CurrentGateMapQueries.FindArea(target, current);
                current.AssignAreaCity(area, 0);
                operation.ChangedAreas.Add(NeutralCaptureNeed(area.PhysicalSlot, "Gate_Area"));
                operation.ChangedGates.Add(NeutralCaptureNeed(target.PhysicalSlot, "Gate_identity"));
            }
            Primary(oldDistrict);
            if (terminal && stopOnTerminal) return;
            current.SetObjectForceSlot13(location, 0);
            current.CurrentSceneryLayout = new(null);
            current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
            if (areas is not null) CityCaptureTerritorySource.ApplyFootprintBeforeResources(working, current, target, 0);
            else GateCaptureTerritorySource.ApplyFootprintBeforeResources(working, current, target, 0);
            BaseCaptureResourceSource.ApplyCurrent(working, current, target, commander, CreateArmyDestructionAbilities(current));
            if (areas is not null) CityCaptureTerritorySource.ClearCityAdministration(current, target);
            else
            {
                int physical = NeutralCaptureNeed(CurrentGateMapQueries.FindArea(target, current).PhysicalSlot, "Gate_Area");
                ushort assigned = NeutralCaptureNeed(current.ReadAssignmentArea36(physical), "Gate_Area36");
                if (assigned != 0)
                {
                    current.SetAssignmentArea36(physical, 0);
                    var removed = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, physical, assigned);
                    if (removed.UnresolvedOwner is not null) throw NeutralCaptureMissing(removed.UnresolvedOwner);
                }
            }
            Primary(oldDistrict);
            if (terminal && stopOnTerminal) return;
            Primary(destination);
            if (terminal && stopOnTerminal) return;
            working = ApplyBaseCaptureHomeArmiesCurrent(working, current, pool, target, date, effects);
            if (oldForce != 0)
            {
                var refreshed = PersonAdvisorResetSource.RefreshWithinBaseCapture(working, current, force);
                working = refreshed.State ?? throw NeutralCaptureMissing(refreshed.UnresolvedOwner ?? "27BE40");
            }
            if (areas is not null)
            {
                CityCaptureTerritorySource.ApplyAbsorptionTerritoryCurrent(working, current, areas, 0,
                    CreateArmyDestructionAbilities(current), operation, fullCentralRadius: true, eagerAdvice: eagerAdvice);
                CityCaptureTerritorySource.RequireNoReachedAreaObjectService(current, areas, pending2336B0: at =>
                {
                    var directory = current.CurrentMapObjectDirectory ?? throw NeutralCaptureMissing("Object_directory");
                    var receiver = directory.ReceiverAtPhysical(NeutralCaptureNeed(at.PhysicalSlot, "Object_identity"));
                    return (working.ReadArmyLifecycle()?.Indexes.PendingObjectRetirements
                        ?? throw NeutralCaptureMissing("Object_F0_pending")).Contains(receiver);
                }, remove: at => working = RemoveMapObjectWithoutActor(working, current, at, effects));
            }
            else GateCaptureTerritorySource.ApplyTerritoryAfterResources(working, current, target, 0, oldDistrictId, 0, eagerAdvice: eagerAdvice);
            int bound = NeutralCaptureNeed(ArmyDestructionParameterSeed.ReadCurrent(working,
                SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "231480");
            byte zero = unchecked((byte)Math.Min(bound, 0));
            current.SetCaptureBaseThreatFields(target, new(zero, zero, 255, 0));
            if (areas is not null)
            {
                CityCaptureTerritorySource.RequireEmperorCaptureNotReached(working, current, areas);
                CityCaptureTerritorySource.ClearTargetRouting(current, target);
                working = ApplyCityCaptureStateAdvantageCurrent(working, current, areas, date, effects,
                    suppressPrimary: !eagerAdvice, eventPlaybackPresent: eventPlaybackPresent);
                //Native-invalid Force0 cannot be the selected player Force.
                CityCaptureTerritorySource.ApplyCityFoodWarning(working, current, target);
            }
            else GateCaptureRoutingSource.ClearTargetRouting(current, target);
        }
        NeutralBaseCaptureTerritoryResult Publish()
        {
            working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
            working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current,
                operation.ChangedAreas, operation.ChangedGates);
            return new(BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true), terminal);
        }
        void Primary(DistrictReference district)
        {
            var result = PersonAssignmentPrecallSource.Apply2E9140PrimaryForCapture(current, district, operation);
            if (result.UnresolvedOwner is not null) throw NeutralCaptureMissing(result.UnresolvedOwner);
        }
    }

    private static T NeutralCaptureNeed<T>(T? value, string name) where T : struct
        => value ?? throw NeutralCaptureMissing(name);
    private static InvalidDataException NeutralCaptureMissing(string name) => new("neutral-Base26DA10." + name);
}
