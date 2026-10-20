using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

internal enum CampaignSaveSlotKind { Empty, Available, Unreadable }

/// <summary>Independent display values. A slot list never retains fifty worlds
/// or restores one into the active session just to inspect its label.</summary>
internal readonly record struct CampaignSaveElapsed(ushort Years, int Months);

internal sealed record CampaignSaveSummary(string ScenarioKey, NativeMapSeasonDate? Date,
    string ForceName, string RulerName, DateTimeOffset? SavedAt, int Cities, int Officers)
{
    internal string? RankName { get; init; }
    internal string? DoctrineName { get; init; }
    internal string? DifficultyId { get; init; }
    internal bool? AdvancedSettings { get; init; }
    internal OfficerPortraitRoute? Portrait { get; init; }
    internal DetailForceAppearance? Flag { get; init; }
    internal DetailMiniMapValues? MiniMap { get; init; }
    internal string? PresentationIssue { get; init; }
    internal CampaignSaveElapsed? Elapsed { get; init; }

    internal static CampaignSaveSummary Capture(GameSessionSave saved)
    {
        var world = saved.State;
        ForceState force = world.Forces[saved.PlayerForceId];
        OfficerState? ruler = force.RulerId is int id ? world.Officers.GetValueOrDefault(id) : null;
        return new(world.ScenarioKey, world.NativeMapSeason?.ThirdDate, force.Name, ruler?.Name ?? "", saved.SavedAt,
            world.Cities.Values.Count(city => city.ForceId == saved.PlayerForceId),
            world.Officers.Values.Count(officer => officer.ForceId == saved.PlayerForceId &&
                officer.Presence is null or OfficerPresence.Present))
        {
            RankName = force.RankId is int rank ? world.ForceRanks.GetValueOrDefault(rank)?.Name : null,
            DoctrineName = ruler?.DoctrineId is int doctrine ? world.Doctrines.GetValueOrDefault(doctrine)?.Name : null,
            DifficultyId = world.DifficultyId,
            Elapsed = ReadElapsed(world.NativeMapSeason)
        };
    }

    private static CampaignSaveElapsed? ReadElapsed(NativeMapSeasonState? calendar)
    {
        if (calendar is null) return null;
        // Save metadata counts complete native 360-day years, truncating
        // toward zero, then stores one word which the row reads unsigned.
        int years = (DayNumber(calendar.ThirdDate) - DayNumber(calendar.SecondDate)) / 360;
        // The row separately reads both saved month bytes unsigned. Its one
        // addition of twelve is deliberately not a general modulo operation.
        int currentMonth = unchecked((byte)calendar.ThirdDate.Month);
        int secondMonth = unchecked((byte)calendar.SecondDate.Month);
        int months = currentMonth - secondMonth;
        if (currentMonth < secondMonth) months += 12;
        return new(unchecked((ushort)years), months);

        static int DayNumber(NativeMapSeasonDate date) =>
            (date.Year * 12 + date.Month) * 30 + date.Day - 31;
    }
}

internal sealed record CampaignSaveSlot(int Number, CampaignSaveSlotKind Kind,
    CampaignSaveSummary? Summary = null, string? Error = null)
{
    internal bool CanLoad => Kind == CampaignSaveSlotKind.Available;
}
