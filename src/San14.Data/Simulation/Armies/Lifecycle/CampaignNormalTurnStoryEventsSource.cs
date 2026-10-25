using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record NormalTurnStoryEventPreparation(GameState State, int[] EventIds,
    CampaignEventOccurrenceDate Date);

public sealed partial class ArmyCampaignRuleAdapter
{
    //3D8850 scans once before its ordered playback loop. The caller retains
    //the remaining IDs while a real CampaignStoryRequest is being consumed.
    internal NormalTurnStoryEventPreparation PrepareNormalTurnStoryEventsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, CampaignStoryBindings? bindings = null)
    {
        var season = state.NativeMapSeason ?? throw PostEconomyMissing("event_calendar");
        var date = new CampaignEventOccurrenceDate((ushort)Math.Clamp((int)season.ThirdDate.Year, 0, 350),
            (byte)Math.Clamp((int)season.ThirdDate.Month, 1, 12), (byte)Math.Clamp((int)season.ThirdDate.Day, 1, 30));
        var applicationContext = ReadApplicationGameplay();
        var mode = bindings?.ExternalMode ?? (applicationContext?.TutorialServicePresent switch
        {
            false => new TransferModeState(false, null),
            true => applicationContext.TutorialActive is bool active ? new TransferModeState(true, active ? 1 : 0) : null,
            _ => null
        }) ?? throw PostEconomyMissing("event_570850_owner");
        if (mode.Present && PostEconomyNeed(mode.Value, "event_570850_value") != 0)
            return new(state, [], date);
        bool application = PostEconomyNeed(bindings?.ApplicationPresent ?? applicationContext?.UserSelectionServicePresent,
            "event_application_pointer");
        int user = application ? PostEconomyNeed(bindings?.ApplicationUserIndex ?? state.CampaignEventWorld?.CurrentUserIndex,
            "event_application_user_index") : -1;
        var source = _campaignEvents.Value;
        if (state.ContentHashes.GetValueOrDefault(source.Source.ResourceUri) != source.Source.Sha256)
            throw PostEconomyMissing("event_resource_revision");
        BindCurrentCampaignEventQueries(state, current);
        current.ConditionApplicationPresent = application;
        var scan = S14EventEligibilityEvaluator.Scan(source.Document, current, new(0, 0, user),
            _ => new S14PlayerEventConditionRuntime(current, _ => ReadOrdinaryEventDispatchInputs(state), current));
        var blocked = Enumerable.Range(0, 992).Select(i => PostEconomyNeed(current.ReadManagerEventFlag(i), "event_manager_result")).ToArray();
        int selector = PostEconomyNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "event_settings_selector");
        int excludeHistory = PostEconomyNeed(BattleRemakeCurrentSettings.ReadInt32(state,
            selector == 0 ? 0x14C : 0x3C), "event_history_setting");
        var ids = scan.Results.Where(result => result.Status == S14EventEligibilityStatus.Satisfied &&
            BinaryPrimitives.ReadInt32LittleEndian(result.Record.RawBytes.AsSpan(8)) == 2 &&
            (excludeHistory == 0 || BinaryPrimitives.ReadInt32LittleEndian(result.Record.RawBytes.AsSpan(4)) != 1))
            .Select(result => result.Record.Id).ToArray();
        var working = state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });
        return new(working with { CampaignEventEligibility = state.CampaignEventEligibility! with { Blocked = blocked } }, ids, date);
    }

    internal CampaignStoryRequest PrepareNormalTurnStoryEventCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int eventId, CampaignStoryBindings bindings, long sequence)
    {
        //The queue was selected by the complete pre-playback scan; do not
        //rescan conditions after a preceding event has changed the world.
        BindCurrentCampaignEventQueries(state, current);
        //Normal Turn's event packet clears the source skip-camera field.
        var inputs = BindCampaignStoryInputs(state, current, false);
        var program = CampaignEventExecutionProgram.ReadEvent(_campaignEvents.Value, eventId);
        var plan = CampaignEventExecution.Stage(program, inputs.Persons,
            _ => ReadOrdinaryEventDispatchInputs(state), bindings.Audio, storyInputs: inputs);
        return CampaignStorySemantics.Bind(plan, sequence);
    }

    internal GameState CompleteNormalTurnStoryEventCurrent(GameState state, SourceWorldContext current,
        int eventId, CampaignEventOccurrenceDate date)
    {
        var history = state.CampaignEvents ?? throw PostEconomyMissing("event_history");
        if (eventId is < 0 or >= 1024) throw PostEconomyMissing("event_id");
        var occurred = history.Occurred.ToArray();
        var dates = history.OccurrenceDates.ToArray();
        occurred[eventId] = true; dates[eventId] = date;
        current.SetWorldEventFlag(eventId, true);
        current.EventConditions.SetOccurrenceDate(eventId, new(date.Year, date.Month, date.Day));
        return state with { CampaignEvents = history with { Occurred = occurred, OccurrenceDates = dates } };
    }
}
