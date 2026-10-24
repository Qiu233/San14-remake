using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Captures one appointment page. Formal submission independently
    /// reads its current command rules; draft edits never write this world.</summary>
    public OfficerOfficeDraft OpenOfficerOfficeDraft(GameState state, int forceId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var rules = CaptureOfficeCommandCurrentRules(state, forceId);
            var current = rules.Current;
            var people = new Dictionary<int, OfficeAppointmentPerson>();
            var original = new Dictionary<int, int>();
            foreach (int id in rules.ForcePersonIds)
            {
                var person = state.Officers.GetValueOrDefault(id) ?? throw OfficeMissing("query_Person_owner");
                int physical = current.ResolveOfficerSource(id) ?? throw OfficeMissing("query_Person_pointer");
                int office = OfficeCurrentDraftPostCurrent(state, current, physical);
                if (!rules.OfficeGrades.ContainsKey(office)) throw OfficeMissing("query_current_Post_grade");
                people.Add(id, new(id, person.Name));
                original.Add(id, office);
            }
            var rows = rules.Slots.OrderBy(slot => slot.Grade).ThenBy(slot => slot.Id)
                .Select(slot => new OfficeAppointmentRow(slot.Id, slot.Name, slot.CommandLimit, slot.AbilityBonus,
                    slot.CurrentHolderId is int holder ? people.GetValueOrDefault(holder)
                        ?? throw OfficeMissing("query_current_holder") : null,
                    slot.CurrentHolderId is int assigned ? people.GetValueOrDefault(assigned) : null,
                    slot.Enabled, slot.DisabledReason)).ToArray();
            var displayed = rules.People.OrderBy(person => rules.OfficeGrades[person.CurrentOfficeId])
                .ThenBy(person => person.CurrentOfficeId).ThenBy(person => person.Id).ToArray();
            var abilitySource = SearchAbilities(current);
            var dimensions = new Dictionary<(int Person, int Dimension), AreaAdministrationQuery<int>>();
            var cards = new Dictionary<int, (AreaAdministrationQuery<OfficerAbilities> Abilities,
                AreaAdministrationQuery<int> Capacity)>();
            return new(forceId, rows, original, people, rules.OfficeGrades, Candidates,
                (draft, promote) => ApplyOfficeAutomaticAssignmentCurrent(state, rules, draft, promote),
                draft => OfficeAutomaticAssignmentSource.ReadAvailability(rules, draft),
                rules.People.Where(person => person.CanRemove).Select(person => person.Id), Preview);

            AreaAdministrationQuery<int> Dimension(int person, int dimension)
            {
                var key = (person, dimension);
                if (dimensions.TryGetValue(key, out var result)) return result;
                try
                {
                    result = AreaAdministrationQuery<int>.Known(abilitySource.ResolveDimension(state, person, dimension,
                        (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect))
                        ?? throw OfficeMissing("query_effective_ability_" + dimension));
                }
                catch (InvalidDataException error) { result = AreaAdministrationQuery<int>.Unknown(error.Message); }
                dimensions.Add(key, result); return result;
            }

            (AreaAdministrationQuery<OfficerAbilities> Abilities, AreaAdministrationQuery<int> Capacity) Card(OfficeCommandPerson person)
            {
                if (cards.TryGetValue(person.Id, out var found)) return found;
                AreaAdministrationQuery<OfficerAbilities> abilities;
                try
                {
                    int Value(int dimension)
                    {
                        var query = Dimension(person.Id, dimension);
                        if (!query.Available) throw new InvalidDataException(query.UnresolvedInput);
                        return query.Value;
                    }
                    abilities = AreaAdministrationQuery<OfficerAbilities>.Known(new(
                        Value(0), Value(1), Value(2), Value(3), Value(4)));
                }
                catch (InvalidDataException error)
                { abilities = AreaAdministrationQuery<OfficerAbilities>.Unknown(error.Message); }
                var capacity = OfficerCommandCapacity.Resolve(state, current, person.Physical);
                var result = (abilities, capacity.Available
                    ? AreaAdministrationQuery<int>.Known(capacity.Capacity!.Value)
                    : AreaAdministrationQuery<int>.Unknown(capacity.UnresolvedInput ?? "officer-office.query_command_limit"));
                cards.Add(person.Id, result); return result;
            }

            IReadOnlyList<OfficeAppointmentCandidate> Candidates(int officeId, IReadOnlyDictionary<int, int> draft)
            {
                int? holder = null;
                foreach (var (id, office) in draft)
                    if (office == officeId) { holder = id; break; }
                return displayed.Select(person =>
                {
                    var card = Card(person);
                    int office = draft.TryGetValue(person.Id, out int selected) ? selected : person.CurrentOfficeId;
                    string officeName = rules.AllSlots.FirstOrDefault(slot => slot.Id == office)?.Name
                        ?? throw OfficeMissing("query_draft_Post_name");
                    bool enabled = person.CanAssign && person.Id != holder;
                    return new OfficeAppointmentCandidate(person.Id, people[person.Id].Name, officeName,
                        card.Abilities, card.Capacity, enabled,
                        !person.CanAssign ? person.DisabledReason : person.Id == holder ? "officer-office.current_slot_holder" : null);
                }).ToArray();
            }

            OfficeAppointmentPreview Preview(int office, int? holder)
            {
                var absent = AreaAdministrationQuery<string>.Known("");
                AreaAdministrationQuery<string> Pending(string field) =>
                    AreaAdministrationQuery<string>.Unknown("officer-office.preview_" + field);
                AreaAdministrationQuery<string> Loyalty()
                {
                    if (holder is not int person) return absent;
                    try
                    {
                        int physical = current.ResolveOfficerSource(person) ?? throw OfficeMissing("preview_Person_pointer");
                        int old = OfficeNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, physical,
                            _governorInputs), "preview_LoyaltyE8");
                        int delta = OfficeLoyaltyDeltaCurrent(state, current, physical, office, original[person]);
                        int unclamped = Math.Max(unchecked(old + delta), 0);
                        int next = Math.Min(unclamped, 100);
                        string number = next.ToString(CultureInfo.InvariantCulture).PadLeft(3);
                        if (unclamped == old || old > 99 && unclamped > 99)
                            return AreaAdministrationQuery<string>.Known(number);
                        int change = unchecked(next - old);
                        return AreaAdministrationQuery<string>.Known(number + "(" +
                            change.ToString("+0;-0;0", CultureInfo.InvariantCulture) + ")");
                    }
                    catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
                }
                AreaAdministrationQuery<string> Ability(int dimension)
                {
                    try
                    {
                        int bonus = OfficeAutomaticAssignmentSource.ReadOfficeAbilityBonus(rules, office, dimension);
                        if (holder is not int person)
                            return bonus > 0 ? AreaAdministrationQuery<string>.Unknown("officer-office.preview_unoccupied_bonus_format") : absent;
                        var currentAbility = Dimension(person, dimension);
                        if (!currentAbility.Available) return AreaAdministrationQuery<string>.Unknown(currentAbility.UnresolvedInput!);
                        int previous = original[person];
                        int oldBonus = OfficeAutomaticAssignmentSource.ReadOfficeAbilityBonus(rules, previous, dimension);
                        int value = unchecked(bonus - oldBonus + currentAbility.Value);
                        string number = value.ToString(CultureInfo.InvariantCulture);
                        return AreaAdministrationQuery<string>.Known(bonus > 0
                            ? number.PadLeft(3) + "(+" + bonus.ToString(CultureInfo.InvariantCulture) + ")"
                            : number);
                    }
                    catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
                }
                return new(Loyalty(), Pending("merit_stage_779340"), Pending("salary_779EA0"),
                    Ability(0), Ability(1), Ability(2), Ability(3));
            }
        }
        catch (InvalidDataException error) { return new(forceId, error.Message); }
    }
}
