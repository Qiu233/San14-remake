using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The three ordinary prisoner-release callers share real mode1
    /// registry iterators and 3D80E0's release/old-Home correction. They do not
    /// report an event-manager Free occurrence or apply editor eligibility.</summary>
    internal (GameState State, int NativeReturn) ApplyStoryPrisonerReleaseCurrent(
        GameState state, SourceWorldContext current, int code, IReadOnlyList<int> arguments,
        BaseReference? selectedBase, IReadOnlyList<S14EventConditionCityPointer>? cities)
    {
        if (arguments.Count != 8) throw StoryPrisonerReleaseMissing("operands");
        var operation = new EventPersonMovementCurrent(this, state, current,
            new EditedEventResultSelectors(current, null, () => BindCampaignStoryInputs(state, current, false)));
        return (operation.ApplyStoryPrisonerRelease(code, selectedBase, cities), 1);
    }

    private sealed partial class EventPersonMovementCurrent
    {
        internal GameState ApplyStoryPrisonerRelease(int code, BaseReference? selectedBase,
            IReadOnlyList<S14EventConditionCityPointer>? cities)
        {
            if (code is not (263 or 267 or 289)) throw StoryPrisonerReleaseMissing("opcode");
            storyImmediateRetirement = true;
            var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
            var oldAreaRemoval = current.AssignmentAreaOfficerRemoval;
            var oldPolicy = current.BaseForcePolicyEffectReader;
            bool reached = false;
            try
            {
                if (code == 289)
                {
                    foreach (var pointer in cities ?? throw StoryPrisonerReleaseMissing("City_list_owner"))
                        ReleaseAt(new(BaseKind.City, pointer.PhysicalSlot), forceIterator: true, requireValidForce: false);
                }
                else
                    ReleaseAt(selectedBase ?? throw StoryPrisonerReleaseMissing("Base_selector"),
                        forceIterator: code == 263, requireValidForce: code == 263);
                if (reached) Publish();
                if (effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                        ?? throw StoryPrisonerReleaseMissing("effect_owner")).Append(CampaignEffectSource.ForceAssociationRemoval,
                            latest.NativeMapSeason?.AdvancedCalendarSteps ?? throw StoryPrisonerReleaseMissing("effect_calendar"), effects.ToArray()) };
                return latest;
            }
            finally
            {
                current.AssignmentAdvisorResetCallbacks = oldAdvisor;
                current.AssignmentAreaOfficerRemoval = oldAreaRemoval;
                current.BaseForcePolicyEffectReader = oldPolicy;
            }

            void ReleaseAt(BaseReference receiver, bool forceIterator, bool requireValidForce)
            {
                if (!Need(current.ReadBaseNativeValidity(receiver), "story_release_Base_validity")) return;
                int filter;
                if (forceIterator)
                {
                    int raw = Need(current.ReadBaseForceSlot(receiver), "story_release_Base_Force");
                    var force = current.ReadForceTableReference(raw < 52 ? raw : 0)
                        ?? throw StoryPrisonerReleaseMissing("Base_Force_pointer");
                    if (requireValidForce && !Need(current.ReadForceNativeValidity(force), "story_release_Force_validity")) return;
                    filter = Need(current.ReadForcePhysicalSlot(force), "story_release_Force_identity");
                    if (filter is < 1 or > 51) return;
                }
                else
                {
                    filter = Need(current.ReadBaseObjectLink(receiver), "story_release_Base4E");
                    if (filter is < 1 or > 61) return;
                }
                //The native100 registry order is retained. Status/Home/Force
                //are read at each reached node after earlier actual releases.
                foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots()
                    ?? throw StoryPrisonerReleaseMissing("Person_registry"))
                {
                    int person = Need(entry, "story_release_Person_pointer");
                    if (forceIterator)
                    {
                        if (Need(current.ReadPersonOldForceSlot(person), "story_release_Person_Force") != filter) continue;
                    }
                    else if (Need(current.ReadPersonFirstLocation(person), "story_release_PersonE2") != filter) continue;
                    if (Need(current.ReadPersonStatus(person), "story_release_Person_status") != 6) continue;
                    if (forceIterator && BaseLookup.Find(Need(current.ReadPersonFirstLocation(person), "story_release_matching_Home"), current) != receiver)
                        continue;
                    if (!reached) BindInputs();
                    reached = true;
                    ReleaseOne(person);
                }
            }
        }

        private void ReleaseOne(int person)
        {
            var originalHome = BaseLookup.Find(Need(current.ReadPersonFirstLocation(person), "story_release_original_Home"), current);
            //3D80E0 calls2E7890(Person,0): retain all actual affiliation,
            //return/freedom writers while suppressing the release report.
            latest = rules.ApplyCaptiveReleaseCurrent(latest, current, person, Date, Day, effects,
                rules.ReadArmyDestructionSampler(latest, current), reportRelease: false);
            var nextHome = BaseLookup.Find(Need(current.ReadPersonFirstLocation(person), "story_release_new_Home"), current);
            if (!Need(current.ReadBaseNativeValidity(originalHome), "story_release_old_Home_validity") ||
                !Need(current.ReadBaseNativeValidity(nextHome), "story_release_new_Home_validity") || nextHome == originalHome) return;
            int raw = Need(current.ReadBaseForceSlot(originalHome), "story_release_old_Home_Force");
            var originalOwner = current.ReadForceTableReference(raw < 52 ? raw : 0)
                ?? throw StoryPrisonerReleaseMissing("old_Home_Force_pointer");
            var affiliation = current.ReadPersonForce(person) ?? throw StoryPrisonerReleaseMissing("new_Person_Force_pointer");
            if (originalOwner != affiliation) return;
            //Actual3D7A40 singleton: commander returns, pending retirement,
            //associated-command reset, then normal271320 arrival and governor.
            ChangeStoryHome(new[] { person }, originalHome);
        }
    }

    private static InvalidDataException StoryPrisonerReleaseMissing(string field)
        => new("normal-turn.story-prisoner-release." + field);
}
