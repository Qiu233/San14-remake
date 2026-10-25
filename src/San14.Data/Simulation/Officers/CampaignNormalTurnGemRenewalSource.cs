using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //33CD50 replenishes the five discovery sites of each valid Gem. This
    //31-object table is independent of the101 scenery/landmark definitions.
    internal GameState ApplyNormalTurnMonthlyGemRenewalCurrent(GameState state, SourceWorldContext current)
    {
        var saved = state.TalentGemDiscoveries;
        Dictionary<int, GemDiscoverySite[]>? changed = null;
        for (int slot = 0; slot < 31; slot++)
        {
            var gem = current.ReadSkillGemReference(slot) ?? throw GemRenewalMissing("Gem_pointer");
            if (!(current.ReadSkillGemValidity(gem) ?? throw GemRenewalMissing("Gem_validity"))) continue;
            int physical = gem.PhysicalSlot ?? throw GemRenewalMissing("Gem_physical");
            if (saved is null || !saved.Gems.TryGetValue(physical, out var sites) || sites is not { Length: 5 })
                throw GemRenewalMissing("five_saved_sites");
            GemDiscoverySite[]? replacement = null;
            for (int index = 0; index < 5; index++)
            {
                var site = sites[index] ?? throw GemRenewalMissing("saved_site");
                if (site.Area == 0) continue;
                byte cap = current.ReadSkillGemRawByte(gem, 0x4D + index) ?? throw GemRenewalMissing("site_maximum");
                if (site.Remaining >= cap) continue;
                replacement ??= (GemDiscoverySite[])sites.Clone();
                replacement[index] = site with { Remaining = checked((byte)Math.Min(site.Remaining + 1, cap)) };
            }
            if (replacement is null) continue;
            changed ??= new(saved!.Gems);
            changed[physical] = replacement;
        }
        return changed is null ? state : state with { TalentGemDiscoveries = new(changed) };
    }

    private static InvalidDataException GemRenewalMissing(string field) => new("normal.turn.gem-renewal." + field);
}
