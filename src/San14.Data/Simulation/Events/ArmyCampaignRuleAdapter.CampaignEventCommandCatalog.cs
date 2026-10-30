using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Manual Events, CEventCommandState mode0: live-world conditions,
    /// independent from Collection progress and edited-event slots. Called on
    /// entering/refreshing this command; no frame or input handler scans it.</summary>
    public CampaignEventCommandCatalog ReadCampaignEventCommandCatalog(GameState state)
    {
        var source = _campaignEvents.Value;
        if (state.ContentHashes.GetValueOrDefault(source.Source.ResourceUri) != source.Source.Sha256)
            throw OccupationMissing("campaign_event_resource_revision");
        //One admitted localized table belongs to this catalogue publication.
        //LoadMessages reads, decompresses and parses; it is not a cache lookup.
        var messages = PrepareCampaignEventCommandTextResourcesCurrent(state);
        var document = source.Document;
        var displayNumbers = CampaignEventCommandPresentation.ReadDisplayNumbers(document);
        var current = CaptureCurrentCampaignEventQueries(state);
        int user = ReadCampaignEventCommandUserCurrent(state, current);
        var rows = new List<CampaignEventCommandRow>();
        var unresolved = new List<string>();
        current.ResetResults();
        foreach (var record in S14EventEligibilityEvaluator.ReadCommandCandidates(document, current, user))
        {
            S14EventEligibilityResult? result = null;
            string? missing = null;
            try
            {
                result = S14EventEligibilityEvaluator.Evaluate(record, document, current, _ => new S14PlayerEventConditionRuntime(current, _ => ReadOrdinaryEventDispatchInputs(state), current));
                current.AppendResult(result);
            }
            catch (InvalidDataException error)
            {
                // This is a query-only detached owner. Unknown is not an
                // unsatisfied or cleared manager bit; later dependencies see it.
                current.SetManagerEventFlag(record.Id, null);
                missing = error.Message;
                unresolved.Add(missing);
            }
            var finalResult = result;
            var row = new CampaignEventCommandRow(record.Id,
                ReadCampaignEventCommandText(messages, record.Id, 1),
                ReadCampaignEventCommandText(messages, record.Id, 2), result?.Status,
                current.ReadWorldEventFlag(record.Id), current.ReadWorldResultFlag(record.Id),
                result?.StoredSuccessCount ?? 0, result?.EvaluatedCount ?? 0, missing,
                CommandI32(record, 4), CommandI32(record, 8), CommandI32(record, 0x30) != 0,
                Enumerable.Range(0, 4).Select(i => CommandI32(record, 0x20 + i * 4))
                    .Where(id => id is >= 1 and <= 6000).ToArray())
            {
                SourceOrder = record.RawField34,
                ResultLines = ReadCampaignEventCommandResultLines(messages, record.Id),
                DescriptionSource = new(() => ReadCampaignEventCommandDescriptionSafeCurrent(state, current, messages, record.Id, finalResult))
            };
            rows.Add(row with { Presentation = CampaignEventCommandPresentation.Read(record, row, displayNumbers, Seed.Language) with
            { Progress = CampaignEventCommandPresentation.ReadProgress(document, record, row, state, messages.Data) } });
        }
        return new(rows.AsReadOnly(), rows.Any(row => row.IsEligible && row.Acknowledged == false),
            unresolved.AsReadOnly());
    }

    private int ReadCampaignEventCommandUserCurrent(GameState state, SourceWorldContext current)
    {
        bool present = OccupationKnown(ReadApplicationGameplay()?.UserSelectionServicePresent,
            "campaign_event_command_application");
        current.ConditionApplicationPresent = present;
        return present ? OccupationKnown(state.CampaignEventWorld?.CurrentUserIndex,
            "campaign_event_command_selected_user") : -1;
    }

    internal int RequireCampaignEventCommandSelectionCurrent(GameState state, SourceWorldContext current, int eventId)
    {
        var source = _campaignEvents.Value;
        if (state.ContentHashes.GetValueOrDefault(source.Source.ResourceUri) != source.Source.Sha256)
            throw OccupationMissing("campaign_event_resource_revision");
        BindCurrentCampaignEventQueries(state, current);
        int user = ReadCampaignEventCommandUserCurrent(state, current);
        // The command must requery its actual current owner, not trust an old
        // list row or the independent profile's unlocked flag.
        var scan = S14EventEligibilityEvaluator.Scan(source.Document, current, new(0, 0, user),
            _ => new S14PlayerEventConditionRuntime(current, _ => ReadOrdinaryEventDispatchInputs(state), current));
        if (!scan.Results.Any(row => row.Record.Id == eventId && row.Status == S14EventEligibilityStatus.Satisfied))
            throw OccupationMissing("campaign_event_command_selection_not_eligible");
        return eventId == 900 ? 2 : 3;
    }

    internal GameState CompleteCampaignEventCommandCurrent(GameState state, SourceWorldContext current,
        int eventId, CampaignEventOccurrenceDate date)
    {
        state = CompleteNormalTurnStoryEventCurrent(state, current, eventId, date);
        BindApplicationServiceCurrent(state, current);
        if (!OccupationKnown(ReadApplicationInformationRestriction(state), "campaign_event_command_selected_user"))
            state = PrepareApplicationServiceCurrent(state, current, profile =>
            {
                var application = ReadApplicationGameplay() ?? throw OccupationMissing("campaign_event_command_application");
                var prepared = CampaignEventService.PrepareEventCompletion(new(current, current,
                    id => ReadCampaignServiceRecordKind(_campaignEvents.Value.Document, id),
                    application.UserSelectionServicePresent, state.CampaignEventWorld?.CurrentUserIndex), profile, eventId);
                return prepared.Service ?? throw new InvalidDataException(prepared.UnresolvedInput ?? "campaign_event_command_profile");
            });
        var history = state.CampaignEvents!;
        var acknowledged = history.Acknowledged.ToArray(); acknowledged[eventId] = false;
        current.SetWorldResultFlag(eventId, false);
        state = state with { CampaignEvents = history with { Acknowledged = acknowledged } };
        BindCurrentCampaignEventQueries(state, current);
        _ = ReadCampaignEventCommandUserCurrent(state, current);
        //83BCE0 re-scans without the menu's selected-user filter on return.
        S14EventEligibilityEvaluator.Scan(_campaignEvents.Value.Document, current, new(0, 0, -1),
            _ => new S14PlayerEventConditionRuntime(current, _ => ReadOrdinaryEventDispatchInputs(state), current));
        state = PublishCampaignEventCommandManagerCurrent(state, current);
        state = PrepareApplicationServiceCurrent(state, current, service =>
        { TransferService.Refresh(current, service); return service; });
        if (eventId == 900)
        {
            var ending = PrepareCampaignEnding(state, fromEvent900: true);
            state = ending.State ?? throw OccupationMissing(ending.UnresolvedOwner ?? "campaign_event_command_ending");
        }
        return state;
    }

    internal GameState PublishCampaignEventCommandEligibilityCurrent(GameState state, SourceWorldContext current)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw OccupationMissing("campaign_event_command_list_pool");
        state = state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = pool.ExportOwnedState() });
        return PublishCampaignEventCommandManagerCurrent(state, current);
    }

    private static GameState PublishCampaignEventCommandManagerCurrent(GameState state, SourceWorldContext current)
    {
        var blocked = Enumerable.Range(0, 992).Select(i =>
            OccupationKnown(current.ReadManagerEventFlag(i), "campaign_event_command_manager")).ToArray();
        return state with { CampaignEventEligibility = state.CampaignEventEligibility! with { Blocked = blocked, HasAvailableResult = current.EventEligibility.HasAvailableResult } };
    }

    private static int CommandI32(S14EventRecord row, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(row.RawBytes.AsSpan(offset, 4));
}
