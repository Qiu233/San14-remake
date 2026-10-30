using San14.Data.Formats;

namespace San14.Data;

public sealed partial class CollectionEventCatalog
{
    public CollectionEventDetail ReadDetail(int rowId)
    {
        var row = Row(rowId);
        if (details.TryGetValue(rowId, out var cached)) return cached;
        bool masked = row.EventId >= 950 && row.Unlocked == false;
        int summaryIndex = masked ? 0x7DB : checked(row.EventId * 4 + 2);
        string summary = Data(masked ? 1 : 3, summaryIndex);
        int[] contentIndices = [checked(row.EventId * 4 + 3), checked(row.EventId * 2 + 0x1F49),
            checked(row.EventId * 4 + 4), checked(row.EventId * 2 + 0x1F4A)];
        var parts = contentIndices.Select(index =>
        { string text = Data(3,index); return new CollectionEventText(index,text,text); }).ToArray();
        // Both source append helpers only concatenate stored UTF16 strings.
        // Its line splitter discards empty LF-delimited lines without trimming.
        var lines = parts.Where(part => part.Text!.Length != 0).SelectMany(part => part.Text!.Split('\n'))
            .Where(line => line.Length != 0).ToArray();
        S14EventConditionProgram? condition = null;
        string? missing = null;
        IReadOnlyList<string> conditionLines = [];
        if (row.EventId >= 950)
            conditionLines = Array.AsReadOnly(new[] { Data(1,0x7DB) });
        else
        {
            long slot = (long)row.EventId - events.Document.NativeBaseId;
            if (slot < 0 || slot >= events.Document.ConditionStreams.Count)
                missing = "collection.event.condition_directory";
            else
            {
                var stream = events.Document.ConditionStreams[(int)slot];
                try { condition = S14EventConditionProgram.Read(stream); }
                catch (InvalidDataException error) { missing = "collection.event.condition_program: " + error.Message; }
                if (stream.Operations.Count != 0)
                    missing ??= "collection.event.selected_world_condition_display";
            }
        }
        cached = new(rowId,row.EventId,row.Name,
            new CollectionEventText(summaryIndex,summary,summary) { DataSection = masked ? 1 : 3 },
            Array.AsReadOnly(parts),Array.AsReadOnly(lines),condition)
        {
            ConditionLines = conditionLines,
            ConditionsUnresolvedOwner = missing,
            UnresolvedOwners = missing is null ? [] : [missing]
        };
        details.Add(rowId,cached);
        return cached;
    }

    /// <summary>Pure prerequisites of the real selected-event preparation.
    /// It does not load the scenario, remove people, alter a Force or execute an event.</summary>
    public CollectionEventPlaybackPlan ReadSelectionPlan(int rowId)
    {
        var row = Row(rowId);
        int scenario = CollectionEventDefinitions.Scenarios.FirstOrDefault(p => p.Event == row.EventId).Scenario;
        scenarios.TryGetValue(scenario,out string? path);
        int ruler = CollectionEventDefinitions.Rulers.FirstOrDefault(p => p.Event == row.EventId).Ruler;
        if (ruler == 0 && (row.Kind is 0 or 3)) ruler = 521;
        var date = CollectionEventDefinitions.Dates.FirstOrDefault(p => p.Event == row.EventId);
        CollectionEventDate? selectedDate = date.Event == row.EventId
            ? new(date.Year,date.Month,date.Day) : null;
        var changes = new List<CollectionEventWorldChange>();
        foreach (var change in CollectionEventDefinitions.Directives.Where(d => d.Event == row.EventId))
        {
            CollectionEventWorldChange? semantic = change.Kind switch
            {
                3 => new CollectionEventTransferCity(change.First,change.Second),
                4 => new CollectionEventReplaceRuler(change.First,change.Second),
                5 => new CollectionEventSetKingdomName(change.First,change.Second),
                6 => new CollectionEventAttachOfficer(change.First,change.Second),
                _ => null
            };
            if (semantic is not null) changes.Add(semantic);
        }
        int flag = 1;
        if (CollectionEventDefinitions.Directives.Any(d => d.Event == row.EventId && d.Kind == 1)) flag = 0;
        if (CollectionEventDefinitions.Directives.Any(d => d.Event == row.EventId && d.Kind == 2)) flag = row.VariantIndex;
        int[] scriptIds = row.EventId is 960 or 961 ? [951,row.EventId] : [row.EventId];
        return new(rowId,row.EventId,flag,scenario,path,ruler,selectedDate,changes.AsReadOnly(),Array.AsReadOnly(scriptIds))
        { UnresolvedOwners = path is null ? ["collection.event.selected_scenario_resource"] : [] };
    }

    public CollectionEventPlaybackPlan ReadPlayback(int rowId)
    {
        var row = Row(rowId);
        if (!row.CanPlay) throw new InvalidOperationException(row.UnresolvedOwner ?? "The event has not occurred in the application profile.");
        return ReadSelectionPlan(rowId);
    }

    /// <summary>The original instruction window for the actual Story executor.
    /// Lookup performs no second whole-resource parse, hash or support scan.</summary>
    public S14EventStream ReadExecutionStream(int eventId)
    {
        if (!records.ContainsKey(eventId)) throw new ArgumentOutOfRangeException(nameof(eventId));
        long slot = (long)eventId - events.Document.NativeBaseId;
        if (slot < 0 || slot >= events.Document.ExecutionStreams.Count)
            throw new InvalidDataException("collection.event.execution_directory");
        return events.Document.ExecutionStreams[(int)slot];
    }
}
