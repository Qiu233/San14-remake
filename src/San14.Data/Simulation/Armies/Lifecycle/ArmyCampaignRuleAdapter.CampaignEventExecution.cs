using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Source-time continuation identity. This is neither a played receipt
/// nor a world commit; the eventual saved consumer must own its resume boundary.</summary>
public sealed record CampaignLandmarkStoryContinuation(int LandmarkId, int EventId,
    CampaignEventOccurrenceDate Date, EventSourceReference Source);
public sealed record CampaignLandmarkStoryPreparation(GameState ConditionState,
    S14EventEligibilityScanResult? Eligibility, CampaignEventExecutionPlan? Story,
    CampaignLandmarkStoryContinuation? Continuation);

public sealed partial class ArmyCampaignRuleAdapter
{
    public CampaignEventExecutionInputs CaptureCampaignStoryInputs(GameState state, bool? skipCamera,
        NativeArmyProgressStageMoment? moment = null)
        => BindCampaignStoryInputs(state, CaptureCurrentCampaignEventQueries(state, moment), skipCamera);

    internal CampaignEventExecutionInputs BindCampaignStoryInputs(GameState state, SourceWorldContext current,
        bool? skipCamera)
    {
        _baseCapacityInputs.BindCurrent(state, current);
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        var cache = new Dictionary<int, OfficerAbilities?>();
        return new(current, (person, key) =>
        {
            if (current.ReadPersonIdentity(person.PhysicalSlot) is not ushort id ||
                current.ResolveOfficerSource(id) != person.PhysicalSlot) return null;
            if (!cache.TryGetValue(id, out var value)) cache[id] = value = abilities.Resolve(state, id);
            return key switch { 0 => value?.Leadership, 1 => value?.Strength, 2 => value?.Intelligence,
                3 => value?.Politics, 4 => value?.Charm, _ => null };
        }, scenery => current.ReadSceneryHexIndex(scenery), scenery =>
            scenery.PhysicalSlot is int slot && slot is >= 0 and < 101
                ? current.SceneryDefinitions?.Sceneries[slot].Name : null, skipCamera);
    }

    /// <summary>Runs the actual complete pool, then the caller's type/status/ID
    /// selection. Preparation changes no occurrence, date, rewards or service.</summary>
    public CampaignLandmarkStoryPreparation PrepareMaximumLandmarkStory(GameState state, int landmarkId,
        IS14AudioEventExecutionState audio, bool? skipCamera, TransferModeState? externalMode,
        bool? applicationPresent, int? currentUserIndex, NativeArmyProgressStageMoment? moment = null)
    {
        if (landmarkId is < 1 or > 100) throw new InvalidDataException("Player landmark story requires physical landmark1..100.");
        if (externalMode is null) throw OccupationMissing("story_external_mode_pointer");
        if (externalMode.Present && OccupationKnown(externalMode.Value, "story_external_mode_value") != 0)
            return new(state, null, null, null);
        //The existing ordinary scanner has the original no-selected-user
        //arguments. Do not silently use -1 for an actual selected App user.
        if (OccupationKnown(applicationPresent, "story_application_pointer") &&
            OccupationKnown(currentUserIndex, "story_current_user_index") != -1)
            throw OccupationMissing("story_selected_user_scan_consumer");
        var scan = ScanCampaignEventConditions(state, -1, moment, applicationPresent);
        int target = checked(landmarkId + 338);
        var selected = scan.Eligibility.Results.FirstOrDefault(result => result.Record.Id == target &&
            result.Status == S14EventEligibilityStatus.Satisfied &&
            BinaryPrimitives.ReadInt32LittleEndian(result.Record.RawBytes.AsSpan(4)) == 2 &&
            BinaryPrimitives.ReadInt32LittleEndian(result.Record.RawBytes.AsSpan(8)) == 2);
        if (selected is null) return new(scan.State, scan.Eligibility, null, null);
        var inputs = CaptureCampaignStoryInputs(scan.State, skipCamera, moment);
        var program = CampaignEventExecutionProgram.ReadEvent(_campaignEvents.Value, target);
        var story = CampaignEventExecution.Stage(program, inputs.Persons,
            _ => ReadOrdinaryEventDispatchInputs(scan.State), audio, storyInputs: inputs);
        var date = ((SourceWorldContext)inputs.Persons).EventConditions.CurrentDate
            ?? throw OccupationMissing("story_current_date");
        return new(scan.State, scan.Eligibility, story,
            new(landmarkId, target, new((ushort)Math.Clamp((int)date.Year, 0, 350),
                (byte)Math.Clamp((int)date.Month, 1, 12), (byte)Math.Clamp((int)date.Day, 1, 30)), story.Source));
    }

    public CampaignEventServicePreparation PrepareMaximumLandmarkService(GameState state,
        TransferServiceState service, int? storyEventId, bool? applicationPresent,
        int? currentUserIndex, TransferModeState? externalMode, NativeArmyProgressStageMoment? moment = null,
        CampaignEventServicePart part = CampaignEventServicePart.All)
    {
        var current = CaptureCurrentCampaignEventQueries(state, moment);
        OwnedArmyReturnServiceInputs.BindCurrent(state, current);
        current.TransferModeStateReader = () => externalMode;
        var source = _campaignEvents.Value.Document;
        return CampaignEventService.PrepareLandmarkCompletion(new(current, current,
            id => ReadCampaignServiceRecordKind(source, id),
            applicationPresent, currentUserIndex), service, storyEventId, part);
    }

    //Native event achievement getters select the resource table by directory
    //identity, independently of an overridden row's stored event ID.
    public static int ReadCampaignServiceRecordKind(S14EventDocument source, int index)
    {
        long slot = (long)index - source.NativeBaseId;
        return slot >= 0 && slot < source.Events.Count
            ? BinaryPrimitives.ReadInt32LittleEndian(source.Events[(int)slot].RawBytes.AsSpan(4)) : 0;
    }
}
