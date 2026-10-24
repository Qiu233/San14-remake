using San14.Core;

namespace San14.Data;

/// <summary>Edits an appointment proposal using the ordinary automatic
/// promotion/assignment rules. This never writes officers or spends resources.</summary>
internal static class OfficeAutomaticAssignmentSource
{
    private const int None = 0x49;

    internal static Dictionary<int, int> Apply(OfficeCommandCurrentRules rules,
        IReadOnlyDictionary<int, int> draft, bool promote)
        => Apply(rules, draft, promote, null);

    internal static Dictionary<int, int> Apply(OfficeCommandCurrentRules rules,
        IReadOnlyDictionary<int, int> draft, bool promote, Func<OfficeCommandPerson, int>? readMeritWeight)
    {
        var result = new Dictionary<int, int>(draft);
        if (!RankValid(rules)) return result;
        var offices = rules.AllSlots.ToDictionary(slot => slot.Id);
        //6B7A10 supplies490 minus4A0.1DE550 then partitions by current
        //draft appointment, not the original saved PersonE7 alone.
        var candidates = rules.People.Where(person => person.CanAssign &&
            (Post(rules, result, person.Id) != None) == promote).ToArray();
        if (candidates.Length == 0) return result;
        var scores = new Dictionary<int, (int Total, int Military, int Civil, ushort Identity)>();
        foreach (var person in candidates)
        {
            var raw = person.BaseAbilities ?? throw Missing("automatic_current_base_abilities");
            int merit = readMeritWeight?.Invoke(person) ?? PlayerMeritWeight(rules, person);
            ushort identity = Need(rules.Current.ReadPersonIdentity(person.Physical), "automatic_Person10");
            scores.Add(person.Id, (unchecked(raw.Leadership + raw.Strength + raw.Intelligence + raw.Politics + raw.Charm + merit * 5),
                unchecked(raw.Leadership + raw.Strength + merit * 2),
                unchecked(raw.Intelligence + raw.Politics + merit * 2), identity));
        }
        //1D7360/1D75C0/1D7490. Each tie uses ascending native Person10.
        var all = candidates.OrderByDescending(p => scores[p.Id].Total).ThenBy(p => scores[p.Id].Identity).ToList();
        var military = candidates.OrderByDescending(p => scores[p.Id].Military).ThenBy(p => scores[p.Id].Identity).ToList();
        var civil = candidates.OrderByDescending(p => scores[p.Id].Civil).ThenBy(p => scores[p.Id].Identity).ToList();
        foreach (var office in rules.AllSlots)
        {
            if (all.Count == 0) break;
            if (!AutomaticOfficeAllowed(rules, office)) continue;
            int mask = BonusMask(office.AbilityBonus);
            bool everyThree = office.AbilityBonus.Leadership >= 3 && office.AbilityBonus.Strength >= 3 &&
                office.AbilityBonus.Intelligence >= 3 && office.AbilityBonus.Politics >= 3;
            var ordered = mask is 0 or 15 ? everyThree ? civil : all : (mask & 3) != 0 ? military : civil;
            if (Occupied(rules, result, office.Id)) continue;
            OfficeCommandPerson? selected = null;
            foreach (var person in ordered)
            {
                int currentPost = Post(rules, result, person.Id);
                if (!promote)
                {
                    if (currentPost == None) { selected = person; break; }
                    continue;
                }
                var previous = offices.GetValueOrDefault(currentPost) ?? throw Missing("automatic_current_Post");
                //1EE820 requires the previous office to benefit the same
                //military/civil side for a mixed target bonus mask.
                if (mask is not (0 or 15))
                {
                    int previousMask = BonusMask(previous.AbilityBonus);
                    if ((previousMask & ((mask & 3) != 0 ? 3 : 12)) == 0) continue;
                }
                if (office.Grade < previous.Grade) { selected = person; break; }
            }
            if (selected is null) continue;
            result[selected.Id] = office.Id;
            //The selected person is removed from every ordering. A later
            //vacancy never assigns the same person a second time in this pass.
            all.Remove(selected); military.Remove(selected); civil.Remove(selected);
        }
        return result;
    }

    //78F040 uses its own strict Force-title bits and the first empty office
    //in native table order, then2346D0's on-Base Person test. It does not use
    //the manual picker list or the effective-ability preview.
    internal static (bool Promote, bool Assign) ReadAvailability(OfficeCommandCurrentRules rules,
        IReadOnlyDictionary<int, int> draft)
    {
        if (!RankValid(rules)) return (false, false);
        var vacancy = rules.AllSlots.FirstOrDefault(slot => FooterOfficeAllowed(rules, slot) &&
            !Occupied(rules, draft, slot.Id));
        if (vacancy is null) return (false, false);
        bool promote = false, assign = false;
        foreach (int id in rules.ForcePersonIds)
        {
            int physical = rules.Current.ResolveOfficerSource(id) ?? throw Missing("automatic_footer_Person");
            int kind = PersonPositionRepair.DecodeSecondLocation(Need(rules.Current.ReadPersonSecondLocation(physical),
                "automatic_footer_PersonE4")).Kind;
            if (kind is not (5 or 6)) continue;
            byte status = Need(rules.Current.ReadPersonStatus(physical), "automatic_footer_PersonE6");
            if (status is 1 or 6) continue;
            int post = Post(rules, draft, id);
            if (post == None) assign = true;
            else if (rules.OfficeGrades.TryGetValue(post, out int grade)) promote |= vacancy.Grade < grade;
            else throw Missing("automatic_footer_Post_grade");
        }
        return (promote, assign);
    }

    internal static int ReadOfficeAbilityBonus(OfficeCommandCurrentRules rules, int officeId, int dimension)
    {
        var office = rules.AllSlots.FirstOrDefault(slot => slot.Id == officeId)
            ?? throw Missing("ability_bonus_Post");
        return dimension switch
        {
            0 => office.AbilityBonus.Leadership,
            1 => office.AbilityBonus.Strength,
            2 => office.AbilityBonus.Intelligence,
            3 => office.AbilityBonus.Politics,
            _ => throw new ArgumentOutOfRangeException(nameof(dimension))
        };
    }

    private static bool RankValid(OfficeCommandCurrentRules rules)
        => Need(rules.Current.ReadMoraleWaveForceRank47(rules.ForceId), "automatic_Force47") is >= 1 and <= 12;
    private static bool AutomaticOfficeAllowed(OfficeCommandCurrentRules rules, OfficeCommandSlot slot)
    {
        if (!slot.NativeValid || slot.Id == None || rules.RankAC - 1 >= slot.Grade) return false;
        int title = rules.ForceTitle48, flags = slot.RawFlags3D;
        if ((title == 6) != ((flags & 1) != 0)) return false;
        return (title is 44 or 45 || (flags & 6) == 0) &&
            (title != 44 || (flags & 2) != 0) && (title != 45 || (flags & 4) != 0);
    }
    private static bool FooterOfficeAllowed(OfficeCommandCurrentRules rules, OfficeCommandSlot slot)
        => slot.NativeValid && slot.Id != None && rules.RankAC - 1 < slot.Grade &&
            (rules.ForceTitle48 == 6) == ((slot.RawFlags3D & 1) != 0) &&
            (rules.ForceTitle48 == 44) == ((slot.RawFlags3D & 2) != 0) &&
            (rules.ForceTitle48 == 45) == ((slot.RawFlags3D & 4) != 0);
    private static int BonusMask(OfficerAbilities bonus)
        => (bonus.Leadership > 0 ? 1 : 0) | (bonus.Strength > 0 ? 2 : 0) |
            (bonus.Intelligence > 0 ? 4 : 0) | (bonus.Politics > 0 ? 8 : 0);
    private static int Post(OfficeCommandCurrentRules rules, IReadOnlyDictionary<int, int> draft, int id)
    {
        int value;
        if (!draft.TryGetValue(id, out value))
        {
            int physical = rules.Current.ResolveOfficerSource(id) ?? throw Missing("automatic_Post_Person");
            value = Need(rules.Current.ReadMoraleWavePersonPostE7(physical), "automatic_PersonE7");
        }
        //1EE270 resolves an invalid appointment to the real None row73.
        return rules.OfficeGrades.ContainsKey(value) ? value : None;
    }
    private static bool Occupied(OfficeCommandCurrentRules rules, IReadOnlyDictionary<int, int> draft, int office)
    {
        foreach (int person in rules.ForcePersonIds)
            if (Post(rules, draft, person) == office) return true;
        return false;
    }
    private static int PlayerMeritWeight(OfficeCommandCurrentRules rules, OfficeCommandPerson person)
    {
        var force = rules.Current.ReadPersonForce(person.Physical) ?? throw Missing("automatic_Person_Force");
        if (force.PhysicalSlot is null) return 0;
        var player = rules.Current.CurrentPlayerForce ?? throw Missing("automatic_player_Force");
        if (force.PhysicalSlot == player.PhysicalSlot && player.PhysicalSlot != 0) return 0;
        throw Missing("automatic_NPC_merit_reader");
    }
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("officer-office." + field);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal Dictionary<int, int> ApplyOfficeAutomaticAssignmentCurrent(GameState state,
        OfficeCommandCurrentRules rules, IReadOnlyDictionary<int, int> draft, bool promote)
        => OfficeAutomaticAssignmentSource.Apply(rules, draft, promote, person =>
        {
            var force = rules.Current.ReadPersonForce(person.Physical) ?? throw OfficeMissing("automatic_Person_Force");
            if (force.PhysicalSlot is null) return 0;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, rules.Current) ?? throw OfficeMissing("automatic_player_Force");
            if (force.PhysicalSlot == player.PhysicalSlot && player.PhysicalSlot != 0) return 0;
            if (Seed.Language != "TC") throw OfficeMissing("automatic_SC_NPC_sort_rule");
            int word = OfficeNeed(_governorInputs.ReadCurrentPhysicalWord166(state, person.Physical), "automatic_NPC_Person166");
            //The TC comparison rule's immutable merit coefficient is3.
            return unchecked(unchecked(word * 3 + 1) / 10);
        });
}
