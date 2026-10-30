using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal (GameState State, int NativeReturn) ApplyStoryArmyBusinessCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments, Func<int, int> resolvePerson,
        Func<int, ForceReference> resolveForce, IReadOnlyList<S14EventConditionCityPointer>? cities = null,
        IReadOnlyList<int>? areas = null)
    {
        if (arguments.Count != 8) throw StoryArmyBusinessMissing("arguments");
        if (code is 349 or 350 or 352 or 356)
            return new EventPersonMovementCurrent(this, state, current,
                new EditedEventResultSelectors(current, null, () => BindCampaignStoryInputs(state, current, false)))
                .ApplyStoryArmyLifecycle(code, arguments, resolvePerson, resolveForce, cities);
        if (code is not (301 or 302 or 303 or 304)) throw StoryArmyBusinessMissing("opcode");
        ForceReference? selected = null;
        if (code != 301)
        {
            selected = resolveForce(arguments[0]);
            if (!StoryArmyBusinessNeed(current.ReadForceNativeValidity(selected), "Force_validity")) return (state, 1);
        }
        var oldPolicy = current.BaseForcePolicyEffectReader;
        try
        {
            //The caller already owns the normal captured Story current. Only
            //the capacity/logistics policy reader differs at this source point.
            //Defeat, fire and speech retain their mature reached binders.
            if (code is 301 or 302)
            {
                _baseCapacityInputs.BindCurrent(state, current);
                current.BaseForcePolicyEffectReader = (owner, effect) =>
                    ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, owner, effect, true).Value;
            }
            var pool = current.ReadOwnedCurrentListPool() ?? throw StoryArmyBusinessMissing("current_pool");
            return (new NormalTurnTerritoryMaintenanceSource(this, state, current, pool)
                .ApplyStoryArmyBusiness(code, arguments, selected, areas), 1);
        }
        finally { current.BaseForcePolicyEffectReader = oldPolicy; }
    }

    private sealed partial class EventPersonMovementCurrent
    {
        internal (GameState State, int NativeReturn) ApplyStoryArmyLifecycle(int code, IReadOnlyList<int> a,
            Func<int, int> resolvePerson, Func<int, ForceReference> resolveForce,
            IReadOnlyList<S14EventConditionCityPointer>? cities)
        {
            var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
            var oldAreaRemoval = current.AssignmentAreaOfficerRemoval;
            var oldPolicy = current.BaseForcePolicyEffectReader;
            storyImmediateRetirement = true;
            int native = 1;
            try
            {
                if (code == 356)
                {
                    native = 0;
                    int person = resolvePerson(a[0]);
                    if (Need(current.ReadPersonNativeValidity(person), "story_destroy_Person_validity"))
                    {
                        var army = PersonArmyLocationSource.ReadCommanderArmy(current, person)
                            ?? throw Missing("story_destroy_commander_Army");
                        if (Need(current.ReadArmyUnitNativeValidity(army), "story_destroy_Army_validity"))
                        { DestroyStoryArmy(army); FinishRecall(); native = 1; }
                    }
                }
                else if (code == 350)
                {
                    foreach (var entry in cities ?? throw Missing("story_end_absent_City_list"))
                    {
                        var city = StoryArmyCity(entry);
                        if (!Need(current.ReadBaseNativeValidity(city), "story_end_absent_City_validity")) continue;
                        var home = BaseLookup.Find(Need(current.ReadBaseObjectLink(city), "story_end_absent_City4E"), current);
                        EndAbsentByHome(home); //Mode0 Home/status iterator, then real cancel/arrival/retire.
                    }
                    //3B2AE0 has no28E6D0/274D70 tail.
                }
                else
                {
                    var force = resolveForce(a[0]);
                    if (!Need(current.ReadForceNativeValidity(force), "story_recall_Force_validity")) native = 0;
                    else if (code == 349)
                    {
                        foreach (var entry in cities ?? throw Missing("story_recall_City_list"))
                        {
                            var city = StoryArmyCity(entry);
                            if (!Need(current.ReadBaseNativeValidity(city), "story_recall_City_validity")) continue;
                            var owner = StoryTargetForce(Need(current.ReadBaseForceSlot(city), "story_recall_City_Force"));
                            if (owner != force) continue;
                            var home = BaseLookup.Find(Need(current.ReadBaseObjectLink(city), "story_recall_City4E"), current);
                            foreach (var army in StoryHomeArmies(city)) RecallArmy(army, home);
                        }
                        FinishRecall();
                    }
                    else
                    {
                        //Source resolves A1 only after the first Force validity gate.
                        var targetForce = resolveForce(a[1]);
                        if (!Need(current.ReadForceNativeValidity(targetForce), "story_recall_target_Force_validity")) native = 0;
                        else
                        {
                            int forceId = Need(current.ReadForcePhysicalSlot(force), "story_recall_Force60");
                            foreach (int physical in UnitLifecycleRegistry(RegistryKind.Army))
                            {
                                var army = new ArmyUnitReference(physical);
                                if (forceId is < 1 or > 51 || current.ReadArmyUnitForceReference(army) != force ||
                                    Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(latest, current, physical), "story_recall_Army233560")) continue;
                                if (!Need(current.ReadArmyUnitNativeValidity(army), "story_recall_Army_validity")) continue;
                                if (StoryArmyTargetForce(army) == targetForce) RecallArmy(army, StoryArmyHome(army));
                            }
                            FinishRecall();
                        }
                    }
                }
                if (unitLifecycleInputsBound) Publish();
                if (effects.Count != 0) latest = AppendEditedForceResultEffects(latest, effects);
                return (latest, native);
            }
            finally
            {
                current.AssignmentAdvisorResetCallbacks = oldAdvisor;
                current.AssignmentAreaOfficerRemoval = oldAreaRemoval;
                current.BaseForcePolicyEffectReader = oldPolicy;
            }
        }

        private BaseReference StoryArmyCity(S14EventConditionCityPointer city) =>
            new(BaseKind.City, city.PhysicalSlot); //Known-null remains the actual null pointer.
        private ForceReference StoryTargetForce(int raw) => current.ReadForceTableReference(raw <= 51 ? raw : 0)
            ?? throw Missing("story_target_Force_pointer");
        private ForceReference? StoryArmyTargetForce(ArmyUnitReference army)
        {
            int physical = Need(army.PhysicalSlot, "story_target_Army_pointer");
            byte kind = Need(current.ReadTacticClearByte(new(RegistryKind.Army, physical), 0x3C), "story_target_override_kind");
            ushort identity;
            if (kind == byte.MaxValue)
            {
                var fields = current.ReadArmyLifecycleTargetFields(physical) ?? throw Missing("story_target_main_fields");
                kind = Need(fields.Kind38, "story_target_main_kind");
                identity = Need(fields.Identity3A, "story_target_main_identity");
            }
            else identity = Need(current.ReadTacticArmyTarget3E(army), "story_target_override_identity");
            if (kind == 27)
            {
                var target = current.ReadArmyUnitTableReference(identity < 501 ? identity : 0) ?? throw Missing("story_target_Army");
                return Need(current.ReadArmyUnitNativeValidity(target), "story_target_Army_validity")
                    ? current.ReadArmyUnitForceReference(target) : null;
            }
            if (kind is 5 or 6)
            {
                var target = kind == 5 ? current.ReadCityTableReference(identity < 52 ? identity : 0)
                    : current.ReadGateTableReference(identity < 11 ? identity : 0);
                if (target is null) throw Missing("story_target_Base");
                return Need(current.ReadBaseNativeValidity(target), "story_target_Base_validity")
                    ? StoryTargetForce(Need(current.ReadBaseForceSlot(target), "story_target_Base_Force")) : null;
            }
            if (kind == 10)
            {
                var target = current.ReadAreaTableReference(identity < 501 ? identity : 0) ?? throw Missing("story_target_Area");
                return Need(current.ReadAreaNativeValidity(target), "story_target_Area_validity")
                    ? StoryTargetForce(Need(current.ReadAreaCurrentForceSlot(target), "story_target_Area_Force")) : null;
            }
            if (kind == 9)
            {
                var target = current.ReadHexTableReference(identity < 48400 ? identity : 0) ?? throw Missing("story_target_Hex");
                return target.PhysicalSlot.HasValue ? StoryTargetForce(Need(current.ReadHexForceSlot(target), "story_target_Hex_Force")) : null;
            }
            return null;
        }
    }

    private sealed partial class NormalTurnTerritoryMaintenanceSource
    {
        internal GameState ApplyStoryArmyBusiness(int code, IReadOnlyList<int> a, ForceReference? force,
            IReadOnlyList<int>? areas)
        {
            if (code == 301) UpdateLogistics(); //2774B0(0): all live Armies; no266B40 upkeep or pending tail.
            else
            {
                current.BindArmyMapOccupancy(state);
                float rate = a[1] switch { 1 => (float)a[2], 2 => (float)a[2] + 100f, 3 => 100f - (float)a[2], _ => 100f };
                foreach (int area in areas ?? throw StoryArmyBusinessMissing("Area_list"))
                {
                    if ((uint)area > 500) throw StoryArmyBusinessMissing("Area_pointer");
                    for (int index = AreaHexIterator.FindNextIndex(0, area, current); index < 48400;
                        index = AreaHexIterator.FindNextIndex(index + 1, area, current))
                    {
                        var hex = Hex(index);
                        if (!hex.PhysicalSlot.HasValue) continue;
                        int physical = unchecked((short)N(current.ReadHexRaw1C(hex), "story_Area_Hex1C"));
                        if (physical is < 1 or > 500) continue; //2C6D20, not Army native validity.
                        var army = current.ReadArmyUnitTableReference(physical) ?? throw Missing("story_Area_Army_pointer");
                        if (ForceId(current.ReadArmyUnitForceReference(army)) != ForceId(force!)) continue;
                        physical = N(army.PhysicalSlot, "story_Area_Army_receiver");
                        if (code == 302)
                        {
                            var fields = current.ReadArmyReturnResourceFields(army) ?? throw Missing("story_rate_Army_resources");
                            int requested = NativeTruncate((float)N(fields.Ready16, "story_rate_ready16") * rate / 100f);
                            int person = Person(N(current.ReadArmyUnitCommanderId(army), "story_rate_Commander12"));
                            byte formation = N(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, Image(physical)), "story_rate_formation1B");
                            if (formation is not (19 or 20))
                            {
                                var capacity = OfficerCommandCapacity.Resolve(state, current, person);
                                requested = Math.Min(requested, capacity.Capacity ?? throw Missing(capacity.UnresolvedInput ?? "story_rate_capacity"));
                            }
                            ushort after = (ushort)Math.Clamp(requested, 0, ushort.MaxValue);
                            current.BindArmyReturnResourceFields(physical, fields with { Ready16 = after });
                            WriteImage(physical, Image(physical) with { Troops = after });
                        }
                        else if (code == 303)
                        {
                            ApplyLogisticsAbnormal(army, physical, 0, a[1], explicitReportSource: string.Empty);
                            ApplyLogisticsAbnormal(army, physical, 1, a[2], explicitReportSource: string.Empty);
                        }
                        else
                        {
                            var changes = new List<HexFireChange>();
                            state = package.IgniteCurrentHex(state, current, index, a[1], commandFire: false,
                                reached => (package.ReadArmyDestructionSampler(reached, current) ?? throw Missing("story_fire_World450")).Day450,
                                state.NativeClock?.UpdateOrdinal ?? throw Missing("story_fire_revision"), changes, [], effects,
                                state.NativeMapSeason?.ThirdDate);
                        }
                    }
                }
                if (logisticsFeedback is not null) state = logisticsFeedback.Publish(state);
                if (code == 304) state = BaseCaptureCurrentInputs.PublishCurrent(state, current);
            }
            PublishPeople();
            return AppendEditedForceResultEffects(state, effects);
        }
    }

    private static T StoryArmyBusinessNeed<T>(T? value, string field) where T : struct => value ?? throw StoryArmyBusinessMissing(field);
    private static InvalidDataException StoryArmyBusinessMissing(string field) => new("story.army_business." + field);
}
