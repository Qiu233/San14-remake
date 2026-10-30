using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal (GameState State, int? NativeReturn) ApplyStoryCityArmyCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments, Func<int, int> resolvePerson,
        ForceReference? force = null, BaseReference? target = null,
        IReadOnlyList<S14EventConditionCityPointer>? cities = null, IReadOnlyList<int>? areas = null,
        Action<int, int>? recordJoined = null, AutomaticEmperorCaptureInputs? emperorInputs = null)
    {
        if (arguments.Count != 8) throw StoryCityArmyMissing("arguments");
        if (code == 171)
        {
            int person = resolvePerson(arguments[0]);
            //1ED230 reaches neither allocation nor initializer/date for an
            //invalid Person, an existing association or a nonpositive delay.
            if (!StoryCityArmyNeed(current.ReadPersonNativeValidity(person), "absence_Person_validity")) return (state, 1);
            var linked = current.ReadPersonLinkedEntity(person) ?? throw StoryCityArmyMissing("absence_association");
            if (StoryCityArmyNeed(current.ReadLinkedEntityNativeValidity(linked), "absence_association_validity") || arguments[1] <= 0)
                return (state, 1);
            //Registration rechecks the exact valid-Person/unassociated/positive
            //delay gates and owns the actual dispatch slot and expiry lifecycle.
            if (current.ReadOwnedPersonCommandManager() is null) PersonReturnCommands.BindCurrent(state, current);
            var date = state.NativeMapSeason?.ThirdDate ?? throw StoryCityArmyMissing("absence_date");
            var result = PersonReturnCommands.RegisterStoryDisappearance(state, current, person, arguments[1], date);
            var working = result.State ?? throw StoryCityArmyMissing(result.UnresolvedOwner ?? "absence_registration");
            if (result.TaskId is null) return (working, 1);
            var pool = current.ReadOwnedCurrentListPool() ?? throw StoryCityArmyMissing("absence_pool");
            working = PublishNormalTurnCurrent(working, current, pool);
            return (working, 1);
        }
        if (code is 212 or 213)
            return ApplyStoryCityListOwnershipCurrent(state, current, code, arguments, force,
                code == 212 ? resolvePerson(arguments[1]) : null,
                cities ?? throw StoryCityArmyMissing("City_list"), recordJoined, emperorInputs);
        if (code is not (211 or 286 or 323 or 351 or 359)) throw StoryCityArmyMissing("opcode");
        return new EventPersonMovementCurrent(this, state, current,
            new EditedEventResultSelectors(current, null, () => BindCampaignStoryInputs(state, current, false)))
            .ApplyCityArmyLifecycle(code, arguments, resolvePerson, force, target, cities, areas);
    }

    private sealed partial class EventPersonMovementCurrent
    {
        internal (GameState State, int NativeReturn) ApplyCityArmyLifecycle(int code, IReadOnlyList<int> arguments,
            Func<int, int> resolvePerson, ForceReference? force, BaseReference? target,
            IReadOnlyList<S14EventConditionCityPointer>? cities, IReadOnlyList<int>? areas)
        {
            var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
            var oldAreaRemoval = current.AssignmentAreaOfficerRemoval;
            var oldPolicy = current.BaseForcePolicyEffectReader;
            storyImmediateRetirement = true;
            int native = 1;
            try
            {
                if (code == 359)
                {
                    int person = resolvePerson(arguments[0]);
                    if (Need(current.ReadPersonNativeValidity(person), "story_recall_Person_validity"))
                    {
                        var army = PersonArmyLocationSource.ReadCommanderArmy(current, person)
                            ?? throw Missing("story_recall_commander_Army");
                        if (Need(current.ReadArmyUnitNativeValidity(army), "story_recall_Army_validity"))
                        { RecallArmy(army, StoryArmyHome(army)); FinishRecall(); }
                    }
                }
                else if (code is 323 or 351)
                {
                    var selected = areas ?? throw Missing("story_Army_Area_list");
                    var restriction = force ?? throw Missing("story_Army_Area_Force");
                    current.BindArmyMapOccupancy(latest);
                    foreach (int area in selected)
                    {
                        if ((uint)area > 500) throw Missing("story_Army_Area_pointer");
                        for (int index = AreaHexIterator.FindNextIndex(0, area, current); index < 48400;
                            index = AreaHexIterator.FindNextIndex(index + 1, area, current))
                        {
                            var hex = current.ReadHexTableReference(index) ?? throw Missing("story_Army_Hex_pointer");
                            int occupant = unchecked((short)Need(current.ReadHexRaw1C(hex), "story_Army_Hex1C"));
                            if (occupant < 0) throw Missing("story_Army_negative_Hex1C_receiver");
                            var army = current.ReadArmyUnitTableReference(occupant < 501 ? occupant : 0)
                                ?? throw Missing("story_Army_occupant_pointer");
                            if (!Need(current.ReadArmyUnitNativeValidity(army), "story_Army_occupant_validity")) continue;
                            if (Need(current.ReadForceNativeValidity(restriction), "story_Army_restriction_validity") &&
                                current.ReadArmyUnitForceReference(army) != restriction) continue;
                            if (code == 351) RecallArmy(army, StoryArmyHome(army));
                            else DestroyStoryArmy(army);
                        }
                    }
                    FinishRecall();
                    native = selected.Count != 0 ? 1 : 0;
                }
                else if (code == 286)
                {
                    var home = target ?? throw Missing("story_Army_Foothold");
                    if (Need(current.ReadBaseNativeValidity(home), "story_Army_Foothold_validity"))
                        foreach (var army in StoryHomeArmies(home)) DestroyStoryArmy(army);
                    FinishRecall(); //Unconditional28E6D0 then274D70.
                }
                else
                {
                    foreach (var city in cities ?? throw Missing("story_morale_City_list"))
                        foreach (var army in StoryHomeArmies(new(BaseKind.City,
                            city.PhysicalSlot ?? throw Missing("story_morale_City_pointer"))))
                        {
                            BindUnitLifecycleInputs();
                            int physical = Need(army.PhysicalSlot, "story_morale_Army_pointer");
                            int getterUpper = MoraleUpper();
                            int before = Math.Min((int)Need(current.ReadArmyReturnResourceFields(army)?.Morale1A, "story_morale_raw1A"), getterUpper);
                            int requested = arguments[0] switch { 1 => arguments[1], 2 => unchecked(before + arguments[1]),
                                3 => unchecked(before - arguments[1]), _ => before };
                            int upper = MoraleUpper();
                            requested = requested < 0 ? 0 : Math.Min(requested, upper);
                            byte after = ArmyMoraleSource.SetByte2419A0(requested, MoraleUpper());
                            current.WriteArmyReturnMorale(physical, after);
                            latest = rules.WriteEditedEventArmyMorale(latest, physical, after);
                            int MoraleUpper() => BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
                        }
                }
                if (unitLifecycleInputsBound) Publish();
                if (effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests ?? throw Missing("story_Army_effect_queue"))
                        .Append(CampaignEffectSource.MonthlyMaintenance, latest.NativeClock?.UpdateOrdinal
                            ?? throw Missing("story_Army_effect_revision"), effects) };
                return (latest, native);
            }
            finally
            {
                current.AssignmentAdvisorResetCallbacks = oldAdvisor;
                current.AssignmentAreaOfficerRemoval = oldAreaRemoval;
                current.BaseForcePolicyEffectReader = oldPolicy;
            }
        }

        private BaseReference StoryArmyHome(ArmyUnitReference army)
        {
            int commander = Person(Need(current.ReadArmyUnitCommanderId(army), "story_Army_commander"));
            ushort home = Need(current.ReadPersonNativeValidity(commander), "story_Army_commander_validity")
                ? Need(current.ReadPersonFirstLocation(commander), "story_Army_commander_E2") : (ushort)0;
            return BaseLookup.Find(home, current);
        }
        private IEnumerable<ArmyUnitReference> StoryHomeArmies(BaseReference target)
        {
            int home = Need(current.ReadBaseObjectLink(target), "story_home_Army_Base4E");
            if (home is < 1 or > 61) yield break;
            foreach (int physical in UnitLifecycleRegistry(RegistryKind.Army))
            {
                if (Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(latest, current, physical), "story_home_Army233560")) continue;
                var army = new ArmyUnitReference(physical);
                int commander = Person(Need(current.ReadArmyUnitCommanderId(army), "story_home_Army12"));
                int location = Need(current.ReadPersonNativeValidity(commander), "story_home_Commander_validity")
                    ? Need(current.ReadPersonFirstLocation(commander), "story_home_Commander_E2") : 0;
                if (location == home) yield return army;
            }
        }
        private void DestroyStoryArmy(ArmyUnitReference army)
        {
            //259360 owns its own initial validity and pending70 early returns.
            if (!Need(current.ReadArmyUnitNativeValidity(army), "story_destroy_Army_validity")) return;
            BindUnitLifecycleInputs();
            int physical = Need(army.PhysicalSlot, "story_destroy_Army_pointer");
            var result = new OwnedArmyDestructionStage(rules).ApplyStoryCause4Current(latest, current, Pool, physical, Date, effects);
            latest = result.State ?? throw Missing(result.UnresolvedOwner ?? "story_destroy_259360");
        }
    }

    private static T StoryCityArmyNeed<T>(T? value, string field) where T : struct => value ?? throw StoryCityArmyMissing(field);
    private static InvalidDataException StoryCityArmyMissing(string field) => new("story.city_army." + field);
}
