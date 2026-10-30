using San14.Data.Formats;

namespace San14.Data;

public sealed record CampaignEventConditionDescriptionLine(int OperationIndex, string Text,
    int IndentPixels, bool ShowBox, bool? Passed, int TextPreset = 0);

public sealed record CampaignEventConditionDescription(int EventId,
    IReadOnlyList<CampaignEventConditionDescriptionLine> Lines,
    IReadOnlyList<string> UnresolvedOwners);

internal static partial class CollectionEventConditionSource
{
    /// <summary>3DE4C0's current-campaign condition panel. Results come from
    /// the selected row's real eligibility bitmap. Names use its prepared
    /// final selector context, never a second world capture or profile flag.</summary>
    internal static CampaignEventConditionDescription BuildCurrent(GameResourceRepository repository,
        string locale, int eventId, S14EventConditionProgram program, bool? alternateDispatchMode,
        IReadOnlyList<bool?> operationPassed, CollectionEventConditionTextQueries queries,
        MessageDocument? admittedData = null, MessageDocument? admittedGame = null)
    {
        if (operationPassed.Count != program.Operations.Count)
            throw Missing("current.operation_result_count");
        var messages = admittedData ?? repository.LoadMessages(locale, MessageKind.Data).Messages;
        var formatter = new NarrationResourceMessages(admittedGame ?? repository.LoadMessages(locale, MessageKind.Game).Messages);
        var lines = new List<CampaignEventConditionDescriptionLine>();
        var unresolved = new List<string>();
        var passed = operationPassed.ToArray();
        int depth = 0, listStart = -1;

        string Data(int section, int index)
        {
            if ((uint)section >= (uint)messages.Sections.Count ||
                (uint)index >= (uint)messages.Sections[section].Records.Count ||
                messages.Sections[section].Records[index].Text is not string text)
                throw Missing($"data.{section}.{index:X}");
            return text;
        }

        foreach (var operation in program.Operations)
        {
            int index = operation.Index, code = operation.Code;
            if (operation.RawField06 != 0) continue;
            if (operation.RawField04 is 1 or 2)
            {
                if (alternateDispatchMode is not bool setting)
                {
                    unresolved.Add($"campaign.event.conditions.operation_{index}.setting_predicate");
                    break; // Later indentation cannot be inferred from an unknown dispatch gate.
                }
                if (operation.RawField04 == 1 ? !setting : setting) continue;
            }

            depth += code switch
            {
                3 or 6 or 8 => -1,
                4 => -2,
                225 or 229 or 231 or 373 or 374 => 1,
                _ => 0
            };
            if (code is not (4 or 6 or 8 or 257 or 379))
            {
                try
                {
                    var arguments = new Arguments(queries);
                    int root = PrepareCommon(operation, arguments, queries, Data);
                    string text = arguments.LiteralText ?? (root == 0 ? string.Empty :
                        formatter.ResolveCampaignEventConditionDescription(arguments, root));
                    bool first = true;
                    foreach (string line in text.Split('\n'))
                    {
                        if (line.Length == 0) continue;
                        bool box = first && code is not (225 or 229 or 231 or 369 or 373 or 374);
                        lines.Add(new(index, line, unchecked((byte)depth) * 28, box, null));
                        first = false;
                    }
                }
                catch (InvalidDataException error)
                {
                    // Missing wording has no bearing on the actual eligibility
                    // result. Preserve other known descriptions and callbacks.
                    unresolved.Add($"campaign.event.conditions.operation_{index}_code_{code}:" + error.Message);
                }
            }

            depth += code switch
            {
                1 or 2 or 3 or 5 or 7 => 1,
                225 or 229 or 231 or 373 or 374 => -1,
                _ => 0
            };
            // The source holds one most-recent list opener, not an indentation
            // stack. Its completed aggregate determines every child checkbox.
            if (code is 216 or 372) listStart = index;
            else if ((code is 257 or 379) && listStart >= 0)
                for (int child = listStart + 1; child <= index; child++)
                    passed[child] = passed[listStart];
        }

        for (int index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            bool? result = line.ShowBox ? passed[line.OperationIndex] : null;
            lines[index] = line with { Passed = result };
            if (line.ShowBox && result is null)
                unresolved.Add($"campaign.event.conditions.operation_{line.OperationIndex}.callback_result");
        }
        return new(eventId, Array.AsReadOnly(lines.ToArray()), Array.AsReadOnly(unresolved.ToArray()));
    }
}

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveCampaignEventConditionDescription(INarrationCurrentContext context, int root)
    {
        // Finite vslot1 resource family; it does not admit arbitrary report VM.
        bool Allowed(int id) => id is >= 0x873B and <= 0x8772 or >= 0x8776 and <= 0x877A or
            0x877C or 0x878C or >= 0x8799 and <= 0x879D or 0x87A3 or 0x87B2;
        if (!Allowed(root)) throw new InvalidDataException("campaign.event.conditions.message_root");
        _prefix = "campaign.event.conditions";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, _ => throw Failure("unexpected_random"), root,
                Get(root, true), _ => false, true, null, Allowed,
                (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05",
                    ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, true).Text;
        }
        finally { _report = previous; }
    }
}
