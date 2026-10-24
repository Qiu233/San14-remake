using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One normal reward page, including its actual opening
    /// recommendation. All later selection edits borrow this same current.</summary>
    public PersonnelRewardDraft OpenPersonnelRewardDraft(GameState state, int forceId)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            int district = PersonnelAdministrationPageDistrict(current, forceId);
            var candidates = ReadRewardCandidatesCurrent(state, current, district);
            var previews = ReadPersonnelRewardPreviewCurrent(state, current, district,
                candidates.Select(row => row.OfficerId).ToArray());
            var people = previews.People.ToDictionary(row => row.OfficerId);
            var cards = candidates.OrderBy(candidate => candidate.Loyalty).Select(candidate =>
            {
                var person = state.Officers.GetValueOrDefault(candidate.OfficerId)
                    ?? throw PersonnelPageMissing("reward_person_owner");
                if (!people.TryGetValue(person.Id, out var preview)) throw PersonnelPageMissing("reward_person_preview");
                var portrait = PersonnelAdministrationPagePortrait(state, current, candidate.PhysicalSlot, person.Id);
                return new PersonnelRewardCard(person.Id, person.Name, portrait, preview.LoyaltyBefore,
                    preview.LoyaltyAfter, false, true, null);
            }).ToArray();
            var initial = ReadPersonnelRewardPreviewCurrent(state, current, district, []);
            return new(forceId, district, cards, Quote, () =>
                PersonnelAdministrationCandidates.SelectAutomaticReward(candidates, initial.GoldBefore)
                    .Select(row => row.OfficerId).ToArray());

            PersonnelRewardBudget Quote(IReadOnlyList<int> ids)
            {
                try
                {
                    var value = ReadPersonnelRewardPreviewCurrent(state, current, district, ids);
                    //753870's selection maximum is the payer's current gold /100.
                    //It is independent of the actual loyalty gain of each person.
                    return new(value.DistrictId, value.GoldBefore, value.GoldAfter, value.GoldCost,
                        value.BooksBefore, value.BooksAfter, value.BookCost, initial.GoldBefore / 100, null);
                }
                catch (InvalidDataException error) { return new(district, null, null, null, null, null, null, null, error.Message); }
            }
        }
        catch (InvalidDataException error) { return new(forceId, error.Message); }
    }

    public PersonnelBanishDraft OpenPersonnelBanishDraft(GameState state, int forceId)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var candidates = QueryBanishCandidatesCurrent(state, current, forceId);
            if (candidates.Value is not { } rows || !candidates.Available)
                throw new InvalidDataException(candidates.UnresolvedInput ?? "personnel-banish.candidates");
            return new(forceId, rows, ids => QueryPersonnelBanishConfirmationCurrent(state, current, ids));
        }
        catch (InvalidDataException error) { return new(forceId, error.Message); }
    }

    public PersonnelCreditDraft OpenPersonnelCreditRankupDraft(GameState state, int forceId) =>
        OpenPersonnelCreditDraft(state, forceId, true);
    public PersonnelCreditDraft OpenPersonnelCreditGiveDraft(GameState state, int forceId) =>
        OpenPersonnelCreditDraft(state, forceId, false);

    private PersonnelCreditDraft OpenPersonnelCreditDraft(GameState state, int forceId, bool rankup)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            int district = PersonnelAdministrationPageDistrict(current, forceId);
            var rules = CaptureCreditCommandCurrentRules(state, current, district, rankup);
            var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
                Seed.Language, _baseCapacityInputs, _repository);
            var cardCache = new Dictionary<int, AreaAdministrationOfficerCard>();
            Dictionary<int, int> points = rankup ? rules.People.ToDictionary(person => person.Id, ReadCreditRankupPointAddition) : [];
            string[] titles = rankup ? Enumerable.Range(1, 6).Select(slot => ReadPersonnelCreditChoiceTitle(state, current, slot)).ToArray() : [];
            return new(forceId, rankup, rules, Project,
                (person, mode) => ApplyCreditAutomaticChoiceCurrent(state, rules, person, mode), Reserves);

            string RankName(int stage) => stage is >= 0 and <= 6 ? CreditRanks()[stage].Name : "";
            PersonnelCreditRow Project(CreditCommandPerson person, bool selected, byte code)
            {
                if (!cardCache.TryGetValue(person.Id, out var card))
                    cardCache[person.Id] = card = cards.ReadCity(state.PeriodNumber, person.Physical, true, 0);
                var officer = state.Officers[person.Id];
                int? doctrine = OfficerDoctrineRules.ReadDoctrineId(officer.DoctrineId, officer.AbilityState);
                var choices = new List<PersonnelCreditChoiceView>(6);
                if (rankup)
                {
                    var states = CreditChoiceSource.QueryChoices(person, code);
                    for (int group = 0; group < 6; group++)
                    {
                        int count = group < 3 ? code & 15 : code >> 4;
                        bool enabled = states[group] == 1;
                        choices.Add(new(group, titles[group], states[group], group % 3 < count,
                            enabled, states[group] == 2, enabled ? CreditChoiceSource.ToggleChoice(person, code, group) : null));
                    }
                }
                return new(person.Id, person.Name, card.Portrait, selected, person.Merit,
                    rankup ? person.Merit : person.MeritAfterGive, person.CurrentStage, person.NextStage,
                    RankName(person.CurrentStage), RankName(person.NextStage),
                    doctrine is int d ? state.Doctrines.GetValueOrDefault(d)?.Name : null, code, choices.AsReadOnly())
                {
                    OfficerCard = card,
                    DoctrinePointCount = rankup ? points[person.Id] : null,
                    StageNames = Enumerable.Range(2, 5).Select(stage => (string?)RankName(stage)).ToArray()
                };
            }
            IReadOnlyList<PersonnelCreditDoctrineReserve> Reserves(IReadOnlyList<int> ids)
            {
                var rows = new List<PersonnelCreditDoctrineReserve>(6);
                var additions = new int[6];
                string? missing = null;
                foreach (var person in rules.People.Where(person => ids.Contains(person.Id)))
                {
                    var officer = state.Officers[person.Id];
                    int? doctrine = OfficerDoctrineRules.ReadDoctrineId(officer.DoctrineId, officer.AbilityState);
                    if (doctrine is null) { missing = "personnel-credit.page.Person_doctrine"; continue; }
                    if (doctrine is >= 1 and <= 6) additions[doctrine.Value - 1] = unchecked(additions[doctrine.Value - 1] + points[person.Id]);
                }
                ImmutableArray<byte> before = default;
                bool known = state.ForceCreditReserves is { } reserve && reserve.PointsByForce.TryGetValue(rules.ForceId, out before) && before.Length == 6;
                for (int id = 1; id <= 6; id++)
                {
                    int? value = known ? before[id - 1] : null;
                    int? addition = missing is null ? additions[id - 1] : null;
                    int? after = value is int original && addition is int added ? Math.Clamp(unchecked(original + added), 0, 99) : null;
                    rows.Add(new(id, state.Doctrines.GetValueOrDefault(id)?.Name ?? "", value, after, addition,
                        missing ?? (known ? null : "personnel-credit.page.Force_doctrine_reserve")));
                }
                return rows.AsReadOnly();
            }
        }
        catch (InvalidDataException error) { return new(forceId, rankup, error.Message); }
    }

    private AreaAdministrationQuery<string> QueryPersonnelBanishConfirmationCurrent(GameState state,
        SourceWorldContext current, IReadOnlyList<int> ids)
    {
        try
        {
            int[] ordered = OrderBanishSelectionCurrent(state, current, ids);
            if (ordered.Length == 0) throw PersonnelPageMissing("banish_no_targets");
            int first = ordered[0];
            int physical = current.ResolveOfficerSource(first) ?? throw PersonnelPageMissing("banish_first_Person");
            byte status = current.ReadPersonStatus(physical) ?? throw PersonnelPageMissing("banish_first_status");
            int root = ordered.Length > 1 ? 0x5887 : status == 6 ? 0x5886 : 0x5885;
            //These three closed roots contain only the actual first person
            //and count operands. They do not select report text or draw App.
            string text = FormatReportMessage(state, current, root,
                new(Person0: first, Integers: [ordered.Length]), ReportMessageOrigin.PersonnelAdministration, null).Text;
            return AreaAdministrationQuery<string>.Known(text);
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
    }

    private string ReadPersonnelCreditChoiceTitle(GameState state, SourceWorldContext current, int slot)
    {
        //3576F0 ->357870 uses the actual CCreditData receiver's kind and
        //addition. It is not the group ordinal added to a message ID.
        int physical = current.ReadStaticTableReference(0x85df0, slot)?.PhysicalSlot
            ?? throw PersonnelPageMissing("credit_choice_definition");
        int kind = OwnedArmyMoraleWaveInputs.ReadCreditEffectKind(state, physical)
            ?? throw PersonnelPageMissing("credit_choice_effect_kind");
        ushort addition = OwnedArmyMoraleWaveInputs.ReadCreditEffectAddition(state, physical)
            ?? throw PersonnelPageMissing("credit_choice_effect_addition");
        return ReadHistoryDataText(1, 0x825 + kind).Replace("%d",
            addition.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static int PersonnelAdministrationPageDistrict(SourceWorldContext current, int forceId)
    {
        var force = current.ReadForceTableReference(forceId is >= 0 and < 52 ? forceId : 0)
            ?? throw PersonnelPageMissing("Force_lookup");
        if (current.ReadForceNativeValidity(force) != true) throw PersonnelPageMissing("Force_validity");
        return ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot
            ?? throw PersonnelPageMissing("primary_District");
    }

    private OfficerPortraitRoute? PersonnelAdministrationPagePortrait(GameState state, SourceWorldContext current,
        int physical, int officerId)
    {
        if (current.ReadPersonPortraitAppearance(physical) is not ushort appearance) return null;
        return ReadOfficerPortraitAppearance(state, current, _definitions, officerId, appearance, ArmyMapUiSources.Create());
    }

    private static InvalidDataException PersonnelPageMissing(string field) => new("personnel-administration.page." + field);
}
