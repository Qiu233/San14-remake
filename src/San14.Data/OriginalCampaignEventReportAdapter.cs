using San14.Core;

namespace San14.Data;

/// <summary>Read-only presentation payload; all text remains with the client locale.</summary>
public sealed record OriginalCampaignEventReportView(string Kind, string EventKey,
    int Period, int? ForceId, int? EventId, int? AreaId, int Amount,
    string? MessageKey, string? ResourceUri, string? ResourceSha256,
    int? SourceOperationIndex);

public static class OriginalCampaignEventReportAdapter
{
    public static IReadOnlyList<OriginalCampaignEventReportView> Create(GameState state, int? forceId = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        PlayableEventStateValidation.Validate(state);
        return Array.AsReadOnly((state.EventReports ?? [])
            .Where(entry => forceId is null || entry.ForceId is null || entry.ForceId == forceId)
            .Select(entry => new OriginalCampaignEventReportView(entry.Kind, entry.EventKey,
                entry.Period, entry.ForceId, entry.Source?.EventId, entry.TargetId, entry.Amount,
                entry.MessageKey, entry.Source?.ResourceUri, entry.Source?.ResourceSha256,
                entry.SourceOperationIndex)).ToArray());
    }
}
