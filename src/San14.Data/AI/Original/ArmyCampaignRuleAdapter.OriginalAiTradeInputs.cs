using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>084C70: current eligible countries, the candidate's own current
    /// inter-country relation filter, then097590's strict source ordering.</summary>
    internal OriginalAiTradeCountry? ReadOriginalAiTradePriorityCountry(GameState state,
        SourceWorldContext current, int forceId)
    {
        var force = current.ReadForceTableReference(forceId) ?? throw TradeInputMissing("Force_pointer");
        if (!TradeInputNeed(current.ReadForceNativeValidity(force), "Force_validity")) return null;
        var district = ForcePrimaryDistrictSource.Resolve(current, force);
        int primaryForce = TradeInputNeed(current.ReadDistrictForceSlot(district), "primary_District10");
        var eligibleForce = current.ReadForceTableReference(primaryForce < 52 ? primaryForce : 0)
            ?? throw TradeInputMissing("primary_Force_pointer");
        var candidates = new List<int>();
        //6DFC80 visits the five real table slots. The Country0 sentinel is
        //not eligible; the four definitions are the ordinary resource owner.
        for (int country = 1; country < 5; country++)
        {
            var definition = ReadForeignTradeCountry(country);
            if (definition.Id != country) throw TradeInputMissing("Country_definition_identity");
            if (!PersonReturnPathSource.ReadForeignTradeEligibility(state, current, eligibleForce,
                country, ForeignTradeApplication())) continue;
            var tasks = state.PersonnelCommands?.Caravans ?? throw TradeInputMissing("caravan_owner");
            if (tasks.Values.Any(task => task.SourceForceId == primaryForce && task.CountryId == country && !task.IsRetiring))
                continue;
            candidates.Add(country);
        }
        //084C70 removes a country when any other valid Country's relation
        //byte is not1. These are independent saved Country objects, not Forces.
        for (int index = 0; index < candidates.Count;)
        {
            int country = candidates[index];
            var row = state.ForeignCountryRelations?.Countries.GetValueOrDefault(country)
                ?? throw TradeInputMissing("Country_relations_owner." + country);
            if (row.Id != country) throw TradeInputMissing("Country_relations_identity");
            bool remove = false;
            for (int peer = 1; peer < 5; peer++)
            {
                if (peer == country) continue;
                byte relation = row.Relations.TryGetValue(peer, out byte value) ? value :
                    throw TradeInputMissing($"Country_relation.{country}.{peer}");
                if (relation == 1) continue;
                remove = true; break;
            }
            if (remove) candidates.RemoveAt(index); else index++;
        }
        if (candidates.Count == 0) return null;
        if (candidates.Count > 1)
        {
            var progress = ForeignTradeCurrentSource.ReadProgress(state, forceId);
            //074430 performs strict insertion ordering. Only the nontrivial
            //case reads friendship; one remaining country needs no ranking owner.
            for (int index = 1; index < candidates.Count; index++)
            {
                int selected = candidates[index], position = index;
                while (position > 0 && Before(selected, candidates[position - 1]))
                { candidates[position] = candidates[position - 1]; position--; }
                candidates[position] = selected;
            }
            bool Before(int left, int right)
            {
                int a = progress.FriendshipPoints[left], b = progress.FriendshipPoints[right];
                if ((a < 1000) != (b < 1000)) return a < 1000;
                return a == b ? left < right : a > b;
            }
        }
        return new(candidates[0]);
    }

    private static T TradeInputNeed<T>(T? value, string field) where T : struct => value ?? throw TradeInputMissing(field);
    private static InvalidDataException TradeInputMissing(string field) => new("original-ai.trade-input." + field);
}
