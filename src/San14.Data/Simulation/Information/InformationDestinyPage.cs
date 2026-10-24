using System.Collections.Immutable;

namespace San14.Data;

public sealed record InformationDestinyRepresentative(int DoctrineId, string DoctrineName,
    int? ForceId, int? RulerId, string? RulerName, int? PortraitAppearanceCode, long? Popularity,
    string? UnresolvedOwner)
{
    public LoadedSprite? Portrait { get; init; }
}
public sealed record InformationDestinyGroup(int Index, ImmutableArray<string> DoctrineNames,
    string TotalCaption, long? Total, string CountCaption, int? ForceCount,
    ImmutableArray<InformationDestinyRepresentative> Representatives);
public sealed record InformationDestinyExplanationRow(int Index, int? MessageId,
    string? Text, string? UnresolvedOwner);
public sealed record InformationDestinyExplanationChannel(int Index, int CaptionMessageId,
    string Caption, ImmutableArray<InformationDestinyExplanationRow> Rows);
public sealed record InformationDestinyPage(string Title, string TopCaption,
    ImmutableArray<string> TopDoctrineNames, ImmutableArray<InformationDestinyGroup> Groups,
    ImmutableArray<InformationDestinyExplanationChannel> ExplanationChannels, string? UnresolvedOwner);
