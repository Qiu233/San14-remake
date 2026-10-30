using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record EditedEventResultApplication(GameState State, S14EventConditionResult Dispatch)
{
    public bool IsPaused { get; init; }
}

/// <summary>The actual result-language coverage, independent of Story receipts.
/// Missing ordinary business consumers fail before a working current is built.</summary>
public static class EditedEventResultExecution
{
    public static string? UnresolvedOwner(int code) => code switch
    {
        0 or 5 or 6 or 7 or 8 or 388 or 389 or 391 or 392 or 393 or 394 or 399 or 395 or 396 or 397 or 398 or 400 or 401 or 403 or 404 or 405 or 407 or 408 or 409 or 419 or 429 => null,
        _ => "edited_event.results.statement_not_in_authoring_language"
    };

    internal static void RequireImplemented(S14EventConditionProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        foreach (var operation in program.Operations)
            if (UnresolvedOwner(operation.Code) is { } missing)
                throw new InvalidDataException($"{missing}; operation {operation.Index}.");
    }

    internal static int Change(int before, int operand, int mode) => mode switch
    {
        1 => operand, 2 => unchecked(before + operand),
        3 => unchecked(before - operand), _ => before
    };
    //The original signed branches do not normalize a negative capacity first.
    internal static int Bound(int value, int maximum) => value < 0 ? 0 : Math.Min(value, maximum);
}
