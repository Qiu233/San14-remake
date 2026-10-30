using San14.Core;

namespace San14.Data;

internal sealed record StoryTerritoryBusinessApplication(GameState State, int? NativeReturn);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary149/299. Selectors and the Area list belong to the
    /// invoking Story, which also owns this live current and registry pool.</summary>
    internal StoryTerritoryBusinessApplication ApplyStoryTerritoryBusinessCurrent(
        GameState state, SourceWorldContext current, WorldIndexContext pool,
        int code, IReadOnlyList<int> arguments, BaseReference? targetBase, int? personPhysical,
        ForceReference? oldForce, ForceReference? newForce, IReadOnlyList<int>? actualAreaSlots,
        Action<int, int>? recordJoined, AutomaticEmperorCaptureInputs? emperorInputs = null)
    {
        if (arguments.Count != 8 || code is not (149 or 299)) throw StoryTerritoryMissing("operation");
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw StoryTerritoryMissing("current_pool");
        var working = state;
        var effects = new List<CampaignEffectRequest>();

        // Bind the existing business providers once before any write. These
        // binders borrow this invocation; there is no second world capture.
        _ = BorrowOwnedArmyReturnWorld(working, current, pool);
        _personRelationInputs.BindCurrent(() => working, current);
        current.BindArmyMapOccupancy(working);
        current.BaseForcePolicyEffectReader = (force, effect) =>
            ForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, force, effect, true).Value;

        if (code == 149)
        {
            var target = targetBase ?? throw StoryTerritoryMissing("selected_Base");
            int person = personPhysical ?? throw StoryTerritoryMissing("selected_Person");
            if (StoryTerritoryNeed(current.ReadBaseNativeValidity(target), "Base_validity"))
            {
                int before = StoryTerritoryNeed(current.ReadBaseForceSlot(target), "previous_Base_Force");
                var date = working.NativeMapSeason?.ThirdDate ?? throw StoryTerritoryMissing("source_date");
                if (StoryTerritoryNeed(current.ReadPersonNativeValidity(person), "Person_validity"))
                {
                    int raw = StoryTerritoryNeed(current.ReadPersonDistrictSlot(person), "PersonE0");
                    var destination = current.ReadDistrictTableReference(raw < 52 ? raw : 0)
                        ?? throw StoryTerritoryMissing("selected_District");
                    if (StoryTerritoryNeed(destination.PhysicalSlot, "selected_District_identity") == 0)
                        working = ApplyNeutralBaseCaptureTerritoryCurrent(working, current, pool, target, date, effects,
                            eagerAdvice: false, stopOnTerminal: false, resourceExecutor: new(person), eventPlaybackPresent: true).State;
                    else working = ApplyForceBaseTransferCurrent(working, current, pool, target, destination, new(person), date, effects,
                        preserveResources: false, suppressMaintenance: false, fullCentralRadius: true,
                        transferDistrict: operation => TransferDistrictAdministrationBaseCurrent(current, target, destination,
                            operation, new(person)), unregisterFormerForce: true, automaticEmperorInputs: emperorInputs, allowAutomaticEmperorPause: true, eventPlaybackPresent: true);
                }
                else
                {
                    working = ApplyNeutralBaseCaptureTerritoryCurrent(working, current, pool, target, date, effects,
                        eagerAdvice: false, stopOnTerminal: false, literalNullExecutor: true, eventPlaybackPresent: true).State;
                }
                if (working.ForceTransferEmperorTail is not null)
                {
                    working = working with { StoryForceTransferContinuation = new(149,
                        StoryTerritoryNeed(target.PhysicalSlot, "Emperor_outer_City"), before,
                        personPhysical, null, [], 0, false) };
                    FlushEffects();
                    return new(working, null);
                }
                working = CompleteStoryFootholdTransferCurrent(working, current, target, before, effects, recordJoined);
                working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            }
            FlushEffects();
            //3A8D20's refresh tail is unconditional, including invalid Base.
            working = CompleteStoryFootholdRefreshCurrent(working, current);
        }
        else
        {
            // Native pointer presence, rather than Force native validity, is
            // the outer299 gate. Force0 remains its real selected receiver.
            if (oldForce is { PhysicalSlot: not null } && newForce is { PhysicalSlot: not null })
            {
                var areas = actualAreaSlots ?? throw StoryTerritoryMissing("invocation_Area_list");
                int from = StoryTerritoryNeed(current.ReadForcePhysicalSlot(oldForce), "old_Force60");
                int to = StoryTerritoryNeed(current.ReadForcePhysicalSlot(newForce), "new_Force60");
                foreach (int physical in areas)
                {
                    if (physical is < 0 or > 500) throw StoryTerritoryMissing("Area_list_physical");
                    var selected = current.ReadAreaTableReference(physical) ?? throw StoryTerritoryMissing("Area_list_pointer");
                    int area = StoryTerritoryNeed(selected.PhysicalSlot, "Area_list_identity");
                    for (int cursor = AreaHexIterator.FindNextIndex(0, area, current); cursor < 48400;
                        cursor = AreaHexIterator.FindNextIndex(cursor + 1, area, current))
                    {
                        var hex = current.ReadHexTableReference(cursor) ?? throw StoryTerritoryMissing("Area_Hex");
                        if (StoryTerritoryNeed(current.ReadHexForceSlot(hex), "Hex14") != from) continue;
                        int rawArea = StoryTerritoryNeed(current.ReadHexAreaSlot(hex), "Hex12");
                        var actual = current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0)
                            ?? throw StoryTerritoryMissing("Hex_Area");
                        if (!StoryTerritoryNeed(current.ReadAreaNativeValidity(actual), "Hex_Area_validity")) continue;
                        int rawObject = StoryTerritoryNeed(current.ReadHexObjectLink(hex), "Hex1A");
                        var obj = current.ReadObjectTableReference(rawObject < 3001 ? rawObject : 0)
                            ?? throw StoryTerritoryMissing("Hex_Object");
                        if (StoryTerritoryNeed(current.ReadObjectNativeValidity(obj), "Hex_Object_validity") &&
                            StoryTerritoryNeed(obj.PhysicalSlot, "Hex_Object_identity") is >= 52 and <= 61) continue;
                        int occupant = unchecked((short)StoryTerritoryNeed(current.ReadHexRaw1C(hex), "Hex1C"));
                        if (occupant is < 1 or > 500)
                        {
                            int original = StoryTerritoryNeed(current.ReadAreaValidityCode(actual), "Area34");
                            var city = current.ReadCityTableReference(original < 52 ? original : 0)
                                ?? throw StoryTerritoryMissing("original_City");
                            if (StoryTerritoryNeed(current.ReadBaseForceSlot(city), "original_City_Force") != to)
                            {
                                int rulerId = StoryTerritoryNeed(current.ReadForceRecord(to)?.RawRuler10, "new_Force_ruler10");
                                int ruler = StoryTerritoryNeed((rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId)
                                    : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "new_ruler_pointer");
                                //22EA10 reads the ruler's Home even for a native-invalid Person.
                                var home = BaseLookup.Find(StoryTerritoryNeed(current.ReadPersonFirstLocation(ruler), "new_ruler_E2"), current);
                                city = CurrentGateMapQueries.FindCity(home, current);
                            }
                            int cityId = StoryTerritoryNeed(city.PhysicalSlot, "selected_City_identity");
                            working = TransferAbsorptionHexCurrent(working, current, hex, to, cityId, enqueueEncirclement: false);
                        }
                        else
                        {
                            var army = current.ReadArmyUnitTableReference(occupant) ?? throw StoryTerritoryMissing("occupant_Army");
                            if (!StoryTerritoryNeed(current.ReadArmyUnitNativeValidity(army), "occupant_Army_validity")) continue;
                            var force = current.ReadArmyUnitForceReference(army) ?? throw StoryTerritoryMissing("occupant_Force");
                            int owner = StoryTerritoryNeed(current.ReadForcePhysicalSlot(force), "occupant_Force60");
                            if (owner == to || owner == from) continue;
                            int raw = StoryTerritoryNeed(current.ReadArmyUnitCommanderId(army), "occupant_Commander12");
                            int commander = StoryTerritoryNeed((raw <= 6000 ? current.ReadPersonLookupReference(raw)
                                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "occupant_Commander_pointer");
                            int home = StoryTerritoryNeed(current.ReadPersonNativeValidity(commander), "occupant_Commander_validity")
                                ? StoryTerritoryNeed(current.ReadPersonFirstLocation(commander), "occupant_CommanderE2") : 0;
                            working = TransferAbsorptionHexCurrent(working, current, hex, owner, home < 52 ? home : 0,
                                enqueueEncirclement: true);
                        }
                    }
                }
                working = UpdateStoryTerritoryRestoreCurrent(working, current, from);
                working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            }
            working = RefreshEditedEventRegionStatistics(working, current);
        }
        working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
        working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
        var lifecycle = working.ReadArmyLifecycle() ?? throw StoryTerritoryMissing("publication_lifecycle");
        working = working.WithArmyLifecycle(lifecycle with { Indexes = pool.ProjectOwnedRoles() });
        FlushEffects();
        return new(working, 1);

        void FlushEffects()
        {
            if (effects.Count == 0) return;
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests ?? throw StoryTerritoryMissing("effect_queue"))
                .Append(CampaignEffectSource.MonthlyMaintenance, working.NativeClock?.UpdateOrdinal
                    ?? throw StoryTerritoryMissing("effect_revision"), effects) };
            effects.Clear();
        }
    }

    //The identical149/382 caller tail:33E2E0,274D70,179A90,
    //190A20,2FFA20. Each caller owns its earlier effect flush and gates.
    private GameState CompleteStoryFootholdRefreshCurrent(GameState state, SourceWorldContext current)
    {
        state = RefreshEditedEventRegionStatistics(state, current);
        state = MaintainEditedDeathArmyCommandsCurrent(state, current);
        var combat = TryRefreshNormalCombatEngagementsCurrent(state, current);
        state = combat.State ?? throw StoryTerritoryMissing(combat.UnresolvedOwner ?? "combat_refresh");
        state = ApplyStoryFacilityAuraRefreshCurrent(state, current);
        var nearby = RefreshBaseNearbyArmiesCurrent(state, current);
        return nearby.State ?? throw StoryTerritoryMissing(nearby.UnresolvedOwner ?? "Base_nearby_refresh");
    }

    //3DF910 runs after the actual26DA10, against the new owner. It records
    //the source manager callback at this point, not inferred numerical deltas.
    private GameState CompleteStoryFootholdTransferCurrent(GameState state, SourceWorldContext current,
        BaseReference target, int previousForce, List<CampaignEffectRequest> effects, Action<int, int>? recordJoined)
    {
        int owner = StoryTerritoryNeed(current.ReadBaseForceSlot(target), "completed_Base_Force");
        var force = current.ReadForceTableReference(owner < 52 ? owner : 0) ?? throw StoryTerritoryMissing("completed_Force");
        int location = StoryTerritoryNeed(current.ReadBaseObjectLink(target), "completed_Base4E");
        if (!StoryTerritoryNeed(current.ReadForceNativeValidity(force), "completed_Force_validity"))
        {
            var forces = new Dictionary<int, ForceState>(state.Forces);
            foreach (int physical in ReadAttackRequestForceOrder(current))
            {
                var objective = AttackRequestObjective(state, physical);
                if (StoryTerritoryNeed(objective.TargetLocationId, "request_target160") != location) continue;
                var at = forces.GetValueOrDefault(physical) ?? throw StoryTerritoryMissing("request_Force_owner");
                forces[physical] = at with { CombatObjective = objective with
                    { RequestingForceId = 0, TargetLocationId = 0, RemainingMonths = 0, Progress = 0 } };
            }
            return state with { Forces = forces };
        }
        state = ApplyCityCaptureAttackRequestsCurrent(state, current, target, force, effects, showRelationChange: false);
        if (location < 52) CityCaptureTerritorySource.ClearCityAdministration(current, target);
        if (previousForce != owner)
            foreach (int person in DistrictCreationHomePeople(current, target))
                if (StoryTerritoryNeed(current.ReadPersonNativeValidity(person), "joined_Person_validity") &&
                    StoryTerritoryNeed(current.ReadEventManagerPresent(), "event_manager_presence"))
                    (recordJoined ?? throw StoryTerritoryMissing("joined_manager_callback"))(owner, person);
        return state;
    }

    // The actual25E520 restoration tree is independent of current Territory.
    //299 updates only an existing stored owner matching the old Force, after
    //all Area writes; it neither adds keys nor clears that outstanding work.
    private static GameState UpdateStoryTerritoryRestoreCurrent(GameState state, SourceWorldContext current, int oldForce)
    {
        var entries = state.CampaignTemporaryHexRestoreEntries
            ?? throw StoryTerritoryMissing("temporary_Hex_restore_owner");
        if (entries.IsDefault) throw StoryTerritoryMissing("temporary_Hex_restore_storage");
        var changed = entries.ToBuilder();
        bool written = false;
        for (int index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var hex = current.ReadHexTableReference(entry.HexPhysicalSlot)
                ?? throw StoryTerritoryMissing("restore_Hex_pointer");
            int rawArea = StoryTerritoryNeed(current.ReadHexAreaSlot(hex), "restore_Hex12");
            var area = current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0)
                ?? throw StoryTerritoryMissing("restore_Area_pointer");
            if (!StoryTerritoryNeed(current.ReadAreaNativeValidity(area), "restore_Area_validity") ||
                entry.ForcePhysicalSlot != oldForce) continue;
            int force = StoryTerritoryNeed(current.ReadHexForceSlot(hex), "restore_Hex14");
            changed[index] = entry with { ForcePhysicalSlot = force };
            written = true;
        }
        // The source tree is keyed by CHex pointer, hence physical-Hex order.
        // Updating its value never reorders keys or borrows a new world view.
        return written ? state with { CampaignTemporaryHexRestoreEntries = changed.ToImmutable() } : state;
    }

    private static T StoryTerritoryNeed<T>(T? value, string name) where T : struct
        => value ?? throw StoryTerritoryMissing(name);
    private static InvalidDataException StoryTerritoryMissing(string name) => new("story.territory." + name);
}
