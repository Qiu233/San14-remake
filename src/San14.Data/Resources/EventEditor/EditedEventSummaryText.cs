namespace San14.Data;

public sealed record EditedEventTextEvaluation(string Text, bool CanConfirm,
    int? MaximumUtf16Length, string? UnresolvedOwner);

/// <summary>Semantic text admission without inventing a Unicode limit from
/// the original narrow-string input descriptor.</summary>
public static class EditedEventSummaryText
{
    public static EditedEventTextEvaluation EvaluateName(string text) => Evaluate(text, true);
    public static EditedEventTextEvaluation EvaluateDescription(string text) => Evaluate(text, false);

    private static EditedEventTextEvaluation Evaluate(string text, bool required)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new(text, (!required || text.Length != 0) && !text.Contains('\0'), null,
            "event_editor.summary.narrow_input_capacity");
    }
}
