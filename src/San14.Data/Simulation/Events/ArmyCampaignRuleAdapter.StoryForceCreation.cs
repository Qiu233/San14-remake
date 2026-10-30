using San14.Core;

namespace San14.Data;

internal sealed record StoryForceCreationApplication(GameState State, int NativeReturn);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary115/446, using the caller's actual invocation/current
    /// and pool. Selection is resolved by that invocation before this writer.</summary>
    internal StoryForceCreationApplication ApplyStoryForceCreationCurrent(
        GameState state, SourceWorldContext current, WorldIndexContext pool,
        int code, IReadOnlyList<int> arguments, int? rulerPhysical, BaseReference? home,
        Action<int> recordRaisedArmy)
    {
        if (code is not (115 or 446) || arguments.Count != 8)
            throw StoryForceCreationMissing("operation");
        ArgumentNullException.ThrowIfNull(recordRaisedArmy);
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw StoryForceCreationMissing("current_pool");
        int person = rulerPhysical ?? throw StoryForceCreationMissing("selected_Person");
        var selectedCity = home ?? throw StoryForceCreationMissing("selected_City");
        if (selectedCity.Kind != BaseKind.City)
            throw StoryForceCreationMissing("City_selector");
        var working = state;
        var effects = new List<CampaignEffectRequest>();
        if (IndependenceForceNeed(current.ReadPersonNativeValidity(person), "selected_Person_validity") &&
            IndependenceForceNeed(current.ReadBaseNativeValidity(selectedCity), "selected_City_validity"))
        {
            var date = working.NativeMapSeason?.ThirdDate ?? throw StoryForceCreationMissing("source_date");

            // The normal Story view already owns its live registry and pool.
            // Install only the mature business inputs omitted by that route;
            // never capture/restore another world over this invocation.
            _personDestructionInputs.BindCurrent(working, current);
            _personRelationInputs.BindCurrent(() => working, current);
            _force164Inputs.BindCurrent(working, current);
            _moraleWaveInputs.BindCurrent(working, current);
            PersonArmyCaptureInputs.BindCurrent(working, current);
            RulerCaptureReleaseSource.BindCurrent(working, current);
            CaptureBaseThreatInputs.BindCurrent(working, current);
            _personAssignmentPrecallInputs.BindCurrent(working, current);
            _baseCapacityInputs.BindCurrent(working, current);
            PersonAdvisorResetSource.BindCurrent(working, current);
            ForceUnregisterInputs.BindCurrent(working, current);
            BaseCaptureCurrentInputs.BindCurrent(working, current);
            OwnedArmyReturnServiceInputs.BindCurrent(working, current);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(working, current);
            if (current.ReadOwnedPersonCommandManager() is null)
                PersonReturnCommands.BindCurrent(working, current);
            current.BaseForcePolicyEffectReader = (force, effect) =>
                ForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, force, effect, true).Value;

            var created = CreateForceCurrent(working, current, pool, person, selectedCity, date, effects,
                transferResidents: true, preferredForceSlot: code == 446 ? arguments[2] : 0, eventPlaybackPresent: true);
            working = created.State;
            if (code == 446)
            {
                // Only446 tests the allocator's actual result. On failure the
                // source returns before both manager notification and33E2E0.
                if (!IndependenceForceNeed(current.ReadForceNativeValidity(created.Force), "created_Force_validity"))
                    return new(working, 0);
                if (IndependenceForceNeed(current.ReadEventManagerPresent(), "event_manager_presence"))
                    recordRaisedArmy(person);
            }
        }

        // Both handlers execute this real region writer after their common
        // invalid-input branch;115 also does so on allocation failure.
        working = RefreshEditedEventRegionStatistics(working, current);
        working = PublishNormalTurnCurrent(working, current, pool);
        if (effects.Count != 0)
            working = working with
            {
                CampaignEffectRequests = (working.CampaignEffectRequests ?? throw StoryForceCreationMissing("effect_queue"))
                    .Append(CampaignEffectSource.MonthlyMaintenance,
                        working.NativeClock?.UpdateOrdinal ?? throw StoryForceCreationMissing("effect_revision"), effects)
            };
        return new(working, 1);
    }

    private static InvalidDataException StoryForceCreationMissing(string field)
        => new("story.force_creation." + field);
}
