using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Normal Turn actions7 and8 borrow the enclosing current and
    /// list pool. Each step receives the previous step's actual candidate;
    /// neither captures a new world nor commits the enclosing normal period.</summary>
    internal sealed partial class CampaignNormalTurnPersonnelAgingSource(
        ArmyCampaignRuleAdapter package, GameState state, SourceWorldContext current,
        WorldIndexContext pool, Action<string, GameState>? observe = null)
    {
        private void Observe(string point)
        {
            if (observe is not null) observe(point, GameStateCopy.Copy(state));
        }
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("normal.turn.personnel-aging." + field);
    }
}
