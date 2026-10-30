using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A refresh of the independent manual edited-event eligibility
/// owner, bound to its real campaign and title library. Unknown stays null.</summary>
public sealed record ManualEditedEventReadiness(GameState World, EditedEventLibrary Library,
    string LibraryRevision, bool? Ready, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>39C630's availability publication: at least one enabled,
    /// condition-satisfied and unacknowledged definition. Content/result
    /// implementation support and Automatic metadata are not this predicate.
    /// The actual Strategy/library host retains this snapshot between refreshes;
    /// this query does not schedule playback or write occurrence history.</summary>
    public ManualEditedEventReadiness QueryManualEditedEventsReadiness(GameState state,
        EditedEventStore store)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(store);
        var library = store.Current;
        string revision = store.Revision;
        SourceWorldContext? current = null;
        bool currentAttempted = false;
        string? currentMissing = null, unresolved = null;
        bool ready = false;
        //The store admits only complete, valid source definitions. Disabled
        //records cannot contribute to the final OR; their content is irrelevant.
        foreach (var definition in library.Events.Where(item => item.Enabled).OrderBy(item => item.Slot))
        {
            try
            {
                var history = state.CampaignEvents ?? throw EditedReadinessMissing("world_occurrence_owner");
                //399660 records status3 without evaluating conditions here.
                if (history.Occurred[definition.OccurrenceId] && !definition.Repeat) continue;
                //3DEA40 requires the independent second bitmap to be clear.
                //A known acknowledged row cannot contribute even if a condition
                //provider for that row is currently unavailable.
                if (history.Acknowledged[definition.OccurrenceId]) continue;
                if (!currentAttempted)
                {
                    currentAttempted = true;
                    try { current = CaptureCurrentCampaignEventQueries(state); }
                    catch (InvalidDataException error) { currentMissing = error.Message; }
                }
                if (current is null) throw new InvalidDataException(currentMissing
                    ?? "edited_event.readiness.current");
                //The original eligibility producer compiles Conditions only.
                //A missing result consumer must not turn real attention off.
                var program = EditedEventProgramCompiler.Compile(definition.Conditions, definition.Slot);
                var condition = EditedEventProgramCompiler.Evaluate(program,
                    new S14PlayerEventConditionRuntime(current, _ => ReadOrdinaryEventDispatchInputs(state), current));
                if (condition.Status == S14ConditionStatus.Satisfied) ready = true;
            }
            catch (InvalidDataException error)
            {
                unresolved ??= "edited_event.readiness.slot:" + definition.Slot + ":" + error.Message;
            }
        }
        //A real positive witness resolves the existential even if an unrelated
        //enabled definition has unavailable inputs. Otherwise preserve unknown.
        return new(state, library, revision, ready ? true : unresolved is null ? false : null,
            ready ? null : unresolved);
    }

    private static InvalidDataException EditedReadinessMissing(string owner)
        => new("edited_event.readiness." + owner);
}
