using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Original basic-page layout with verified state bindings; display modifiers remain unresolved.</summary>
public partial class OfficerDetailsView : DetailsView
{
    internal void Configure(GameResourceRepository repository, GameState world,
        ScenarioCityPreview snapshot, int officerId, PreviewText text, Action close)
    {
        OfficerState officer = world.Officers[officerId];
        CharacterPreviewRecord? source = snapshot.Characters.FirstOrDefault(person =>
            person.CharacterId != 0 && person.CharacterId == officerId);
        string unknown = text.Get("detail_unknown");
        string location = officer.LocationId is int locationId
            ? world.Cities.GetValueOrDefault(locationId)?.Name ??
              world.Gates.GetValueOrDefault(locationId)?.Name ?? unknown : unknown;
        var values = new Dictionary<string, string>
        {
            ["name"] = officer.Name,
            ["sex"] = officer.Sex switch
            {
                OfficerSex.Male => text.Get("detail_sex_male"),
                OfficerSex.Female => text.Get("detail_sex_female"),
                _ => unknown
            },
            ["force"] = officer.ForceId is int forceId ? world.Forces[forceId].Name : unknown,
            ["district"] = officer.DistrictId is int districtId
                ? text.DistrictNumber(world.Districts[districtId].Number) : unknown,
            ["role"] = text.Get(officer.Presence switch
            {
                OfficerPresence.NotAppeared => "detail_presence_not_appeared",
                OfficerPresence.Undiscovered => "detail_presence_undiscovered",
                OfficerPresence.Dead => "detail_presence_dead",
                _ => officer.Role switch
                {
                    OfficerRole.Ruler => "detail_role_ruler",
                    OfficerRole.DistrictLeader => "detail_role_district_leader",
                    OfficerRole.Governor => "detail_role_governor",
                    OfficerRole.Officer => "detail_role_officer",
                    OfficerRole.Free => "detail_role_free",
                    _ => "detail_unknown"
                }
            }),
            // One scenario placement is known. It does not prove the independent home affiliation.
            ["location"] = location
        };
        ConfigureLayout(repository, "res://ui/officer-details.json", "OfficerDetails",
            "detail_officer_title", text, values, source?.AppearanceCode, officer.Name, close);
        SetMeta("officer_id", officer.Id);
        if (officer.BaseAbilities is OfficerAbilities abilities)
        {
            int[] basic = [abilities.Leadership, abilities.Strength, abilities.Intelligence,
                abilities.Politics, abilities.Charm];
            SetMeta("base_abilities", basic);
        }
        // Rank definitions are retained separately; no partial modifier sum is presented as a final ability.
        if (officer.ForceId is int id && world.Forces[id].RulerId == officer.Id &&
            world.Forces[id].RankId is int rankId && world.ForceRanks.TryGetValue(rankId, out var rank))
        {
            SetMeta("ruler_rank_id", rankId);
            SetMeta("ruler_rank_bonus", new int[] { rank.AbilityBonus.Leadership, rank.AbilityBonus.Strength,
                rank.AbilityBonus.Intelligence, rank.AbilityBonus.Politics, rank.AbilityBonus.Charm });
        }
    }
}
