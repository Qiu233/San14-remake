using San14.Data;

internal sealed record NewGameConfirmationSetting(int TitleMessageId, string? Value);
internal sealed record NewGameConfirmationRow(int TitleMessageId, string? Value, bool? NonTrophy = false, string? UnresolvedInput = null);
internal sealed record NewGameConfirmationExtras(string Year, string Month, IReadOnlyList<NewGameConfirmationRow> Rows,
    bool? CollectionEligible, string? UnresolvedInput);
