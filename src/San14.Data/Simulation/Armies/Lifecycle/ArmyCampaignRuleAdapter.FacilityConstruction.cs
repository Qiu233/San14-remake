using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed record ArmyFacilityConstructionResult(GameState? State, IReadOnlyList<int> CreatedReceiverSlots,
    string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    public ArmyFacilityConstructionResult TryAdvanceArmyFacilityConstruction(GameState state,
        NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.FacilityConstruction ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Facility construction requires reached stage15 entry.");
        try
        {
            var current = CaptureFacilityConstructionCurrent(state);
            var order = current.ReadRegistryReferences(RegistryKind.Army) ?? throw Missing("complete_manager60");
            var requests = new List<CampaignEffectRequest>();
            var created = new List<int>();
            var working = state;
            foreach (var entry in order)
            {
                int physical = entry is { Kind: RegistryKind.Army, PhysicalSlot: int at } ? at : throw Missing("manager_Army_pointer");
                var army = current.ReadArmyUnitTableReference(physical) ?? throw Missing("manager_Army_table");
                var queries = new ArmyMapGroupingCurrentQueries(working, current, _scenario, _governorInputs,
                    Seed.Language, SimulationRuleIdentity.Version);
                //2C4290's first native validity result is ignored.
                if (K(queries.IsAtHome2C96E0(physical), "at_home") || K(queries.IsExcluded233560(physical), "excluded")) continue;
                var image = NotificationArmy(working, physical);
                if (K(ArmyConfigurationSourceAdapter.ReadAction(working, image), "action37") != 8) continue;
                var runtime = image.NativeTacticRuntime ?? throw Missing("Army_runtime");
                if (runtime.Abnormal23 != 0 || runtime.Abnormal25 != 0 || runtime.Abnormal24 != 0 || (runtime.Flags26 & 1) != 0) continue;
                var target = current.ReadArmyLifecycleTargetFields(physical) ?? throw Missing("target_fields");
                byte targetKind = K(target.Kind38, "target_kind");
                ushort rawTarget = K(target.Identity3A, "target_identity");
                int goal = DemolitionGoal(current, targetKind, rawTarget);
                int own = K(current.ReadArmyUnitRawHex2A(army), "current_hex");
                bool adjacent = false;
                for (int direction = 1; direction <= 6; direction++)
                {
                    int neighbor = DemolitionNeighbor(own, direction);
                    if ((uint)neighbor >= 48400) continue;
                    var hex = current.ReadHexTableReference(neighbor) ?? throw Missing("adjacent_Hex_pointer");
                    if (hex.PhysicalSlot == goal) { adjacent = true; break; }
                }
                if (!adjacent) continue;
                if (ConstructionWaypoints(image).Any(value => value < 48400)) continue;
                var site = current.ReadHexTableReference(rawTarget < 48400 ? rawTarget : 0) ?? throw Missing("site_Hex_pointer");
                if (FacilityConstructionLegality.SceneBlocks(current, site) || targetKind != 9) continue;
                if (K(current.ReadHexObjectLink(site), "site_Object_word") != 0 ||
                    unchecked((short)K(current.ReadHexRaw1C(site), "site_Army_word")) != 0) continue;
                var home = current.FindArmyUnitCurrentBase(army);
                var selected = Selected(working, current, image);
                var definition = Read(current, selected);
                int cost = FacilityConstructionCost.Read(working, current, current.ReadArmyUnitForceReference(army), definition, _baseCapacityInputs);
                if (cost > BaseCurrencyAdditionSource.ReadCurrentGold(home, current)) continue;
                //The actor leaf rechecks native validity and goal/definition.
                if (!K(current.ReadArmyUnitNativeValidity(army), "actor_validity") || goal is < 0 or >= 48400) continue;
                selected = Selected(working, current, image);
                if (!K(current.ReadObjectDefinitionValidity(selected), "actor_definition_validity")) continue;
                definition = Read(current, selected);
                int commander = K(current.ReadArmyUnitCommanderId(army), "actor_commander");
                int person = K((current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
                    ?? throw Missing("actor_Person_pointer")).PhysicalSlot, "actor_Person_identity");
                var people = current.CreateNearbyTemporary();
                try
                {
                current.AppendConstructionPersonTemporary(people, person);
                if (!FacilityConstructionLegality.Check(working, current, people, site, selected)) continue;
                if (selected.PhysicalSlot != 7) throw Missing("actor_definition_consumer_outside_kind7");
                int rawForce = definition.Category is 2 or 4 ? current.ReadArmyUnitRawForceSlot(army) : 0;
                var creation = MapObjectCreationSource.CreateFacility(working, current, goal, selected, rawForce);
                working = creation.State;
                if (creation.Object is not { } obj || !K(current.ReadObjectNativeValidity(obj), "created_Object_validity")) continue;
                working = FacilityActivationCurrent.ClearForConstruction(working, current, obj);
                int objectPhysical = K(obj.PhysicalSlot, "created_identity");
                created.Add(objectPhysical);
                var receiver = current.CurrentMapObjectDirectory!.ReceiverAtPhysical(objectPhysical);
                string key = MapObjectSourceDirectory.DefinitionKey(definition.Physical);
                if (ConstructionModelIsRequired(working, current, obj))
                    requests.Add(ConstructionPresentation(FacilityConstructionPresentationOperation.EnsureInitialFacilityModel));
                working = ApplyConstructionSiteOwnership(working, current, army, site, receiver, key, requests);
                //241900(param3=1) writes raw physical identity only when its
                //actual lookup points to a valid Object at this same Hex.
                if (current.AssignHexObject(site, objectPhysical, 1))
                    working = OwnedMapObjectLifecycle.WriteHexBinding(working, current,
                        K(site.PhysicalSlot, "created_site_identity"), checked((ushort)objectPhysical));
                working = AppendConstructionStartSpeech(working, current, army, image, obj, moment, requests);
                requests.Add(ConstructionPresentation(FacilityConstructionPresentationOperation.NotifyFacilityCreated));
                home = current.FindArmyUnitCurrentBase(army);
                working = BaseCurrencyAdditionSource.PayConstruction(working, current, home,
                    () => FacilityConstructionCost.Read(working, current, current.ReadArmyUnitForceReference(army), definition, _baseCapacityInputs),
                    city => BasePostLoad.ReadResourceCapacities(city, current).Gold);
                if (IsPlayerRulerLedDistrict(working, current, image))
                    working = AppendConstructionReport(working, current, image, obj, moment,
                        CampaignReportEvent.PlayerFacilityConstructionStarted, requests);

                CampaignEffectRequest ConstructionPresentation(FacilityConstructionPresentationOperation operation) =>
                    new(null, null) { FacilityConstructionPresentation = new(operation, receiver, goal, key, null) };
                }
                finally { current.ClearNearbyTemporary(people, true); }
            }
            working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with
                { Indexes = current.ReadOwnedCurrentListPool()!.ExportOwnedState() });
            if (requests.Count != 0) working = working with
                { CampaignEffectRequests = (working.CampaignEffectRequests ?? throw Missing("effect_owner"))
                    .Append(CampaignEffectSource.ArmyFacilityConstruction, moment.CalendarCycleOrdinal, requests) };
            return new(working, created.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private static IEnumerable<ushort> ConstructionWaypoints(UnitState image)
    {
        var march = image.March ?? throw Missing("waypoints_march");
        yield return K(ArmyMarchSourceAdapter.EncodeDestination(march.Target), "waypoint_first");
        var continuation = march.FollowingWaypoints ?? throw Missing("waypoint_continuation");
        for (int index = 0; index < 4; index++)
            yield return index < continuation.Destinations.Count
                ? K(ArmyMarchSourceAdapter.EncodeDestination(continuation.Destinations[index]), "waypoint_following") : (ushort)48400;
    }
}
