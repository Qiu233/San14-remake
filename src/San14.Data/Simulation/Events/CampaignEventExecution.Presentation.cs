using San14.Core;

namespace San14.Data;

public sealed record CampaignEventItemIllustration(int OperationIndex, int SourceOffset,
    int? IllustrationIndex, int Setting) : CampaignEventCommand(OperationIndex, SourceOffset, 89);
public sealed record CampaignEventResetStandingRegistration(int OperationIndex, int SourceOffset)
    : CampaignEventCommand(OperationIndex, SourceOffset, 105);
public sealed record CampaignEventOwnedEffect(int OperationIndex, int SourceOffset, StoryOwnedEffect Effect)
    : CampaignEventCommand(OperationIndex, SourceOffset, 109);
public sealed record CampaignEventFormatOnlyMessage(int OperationIndex, int SourceOffset,
    ushort Speaker, int MessageId, IReadOnlyList<int?> PersonParameters, IReadOnlyList<string>? NameParameters)
    : CampaignEventCommand(OperationIndex, SourceOffset, 95)
{
    public IReadOnlyList<int>? CityParameters { get; init; }
    public IReadOnlyList<int>? ForceParameters { get; init; }
    public IReadOnlyList<int>? AreaParameters { get; init; }
    public IReadOnlyList<int>? ItemParameters { get; init; }
    public IReadOnlyList<int?>? NumberParameters { get; init; }
    public IReadOnlyList<int?>? ProvinceParameters { get; init; }
}
