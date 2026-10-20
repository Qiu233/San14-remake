using System.Text.Json.Serialization;
namespace San14.Core;

/// <summary>A private operation/save snapshot, not another running App owner.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CampaignAppRandomState(uint State);

/// <summary>The ordinary serial host's single authoritative App stream.
/// Installing a committed state is a nonthrowing final memory assignment;
/// it must not bootstrap, draw, or acknowledge any presentation.</summary>
public interface ICampaignAppRandomBinding
{
    uint ReadCurrentState();
    void InstallCommittedState(uint state);
}

public sealed partial record GameState
{
    public CampaignAppRandomState? CampaignAppRandom32 { get; init; }
}
