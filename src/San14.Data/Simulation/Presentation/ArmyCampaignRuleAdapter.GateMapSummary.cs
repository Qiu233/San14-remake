using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal MapSummaryPage QueryGateMapSummaryCurrent(GameState state, SourceWorldContext current, MapSummaryReceiver receiver)
        => QueryFootholdMapSummaryCurrent(state, current, receiver, true);

    private sealed partial class GateInformationCurrent
    {
        internal GateInformationCurrent(ArmyCampaignRuleAdapter rules, GameState state, SourceWorldContext current)
        { this.rules = rules; this.state = state; playerForceId = 0; captured = new(() => current); }
        internal string SummaryName(int id) => ReadGate(id).Owner.Name;
        internal int SummaryResource(int id, BaseResourceKind kind) => LinkedCityResource(ReadGate(id), kind);
        internal int? SummaryTactic(int id) => Tactic(ReadGate(id));
    }
}
