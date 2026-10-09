using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>Merit and its current credit cycle. Each channel is independently
/// known; selection counts retain the two imported choice groups without
/// assigning them an unproved reward meaning.</summary>
public sealed record OfficerMeritState
{
    [JsonConstructor]
    public OfficerMeritState(int? merit, int? pendingMerit, int? rankStageId,
        int? firstSelectionCount, int? secondSelectionCount, int? resetCode)
    {
        if (pendingMerit < 0 || rankStageId < 0 || firstSelectionCount < 0 ||
            secondSelectionCount < 0 || resetCode < 0)
            throw new InvalidDataException("Officer merit cycle knowledge cannot be negative.");
        Merit = merit;
        PendingMerit = pendingMerit;
        RankStageId = rankStageId;
        FirstSelectionCount = firstSelectionCount;
        SecondSelectionCount = secondSelectionCount;
        ResetCode = resetCode;
    }

    public int? Merit { get; }
    public int? PendingMerit { get; }
    public int? RankStageId { get; }
    public int? FirstSelectionCount { get; }
    public int? SecondSelectionCount { get; }
    public int? ResetCode { get; }

    public OfficerMeritState WithPendingMerit(int pending) => new(Merit, pending,
        RankStageId, FirstSelectionCount, SecondSelectionCount, ResetCode);
}

public static class OfficerMeritRules
{
    public const int MaximumPendingMerit = 65535;

    public static bool? IsStageOne(OfficerMeritState? state) =>
        state?.RankStageId is int stage ? stage == 1 : null;

    public static bool HasNoSelections(OfficerMeritState? state) =>
        state is { FirstSelectionCount: 0, SecondSelectionCount: 0 };

    /// <summary>A reached release writer replaces the complete credit cycle.</summary>
    public static OfficerMeritState ResetAfterRelease(int merit) => new(merit, 0, 0, 0, 0, 0);
}

public sealed partial record OfficerState
{
    public OfficerMeritState? MeritState { get; init; }
}
