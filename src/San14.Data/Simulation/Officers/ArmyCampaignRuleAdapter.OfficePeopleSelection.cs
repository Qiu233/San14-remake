using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private AreaAdministrationQuery<OfficerOfficePeopleSelection> CreateOfficePeopleSelectionCurrent(
        GameState state, OfficeCommandCurrentRules rules, IReadOnlyList<OfficeCommandPerson> orderedPeople,
        IReadOnlyDictionary<int, int> assignments, int targetOffice,
        Func<int, int, AreaAdministrationQuery<int>> ability)
    {
        try
        {
            return AreaAdministrationQuery<OfficerOfficePeopleSelection>.Known(
                new OfficePeopleSelectionCurrent(this, state, rules, orderedPeople,
                    assignments, ability).Capture(targetOffice));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<OfficerOfficePeopleSelection>.Unknown(error.Message); }
    }

    /// <summary>Only adapts established appointment/effective-ability readers to
    /// the custom source cells. It neither edits a draft nor captures a new world.</summary>
    private sealed class OfficePeopleSelectionCurrent
    {
        private readonly ArmyCampaignRuleAdapter owner;
        private readonly GameState state;
        private readonly OfficeCommandCurrentRules rules;
        private readonly IReadOnlyList<OfficeCommandPerson> ordered;
        private readonly Dictionary<int, OfficeCommandPerson> people;
        private readonly Dictionary<int, int> assignments;
        private readonly Func<int, int, AreaAdministrationQuery<int>> ability;
        private readonly Dictionary<(int Person, int Column), Prepared> cells = [];
        private NarrationResourceMessages? focusMessages;
        private TacticRemakeCurrentAbilities? focusAbilities;
        private SourceWorldContext Current => rules.Current;
        private static readonly int[] Columns = [0, 1, 2, 3, 10, 4, 5, 6, 7, 8, 9];

        internal OfficePeopleSelectionCurrent(ArmyCampaignRuleAdapter owner, GameState state,
            OfficeCommandCurrentRules rules, IReadOnlyList<OfficeCommandPerson> ordered,
            IReadOnlyDictionary<int, int> assignments, Func<int, int, AreaAdministrationQuery<int>> ability)
        {
            this.owner = owner; this.state = state; this.rules = rules; this.ordered = ordered;
            people = ordered.ToDictionary(person => person.Id);
            // A single bounded draft snapshot belongs to this child lifetime.
            this.assignments = assignments.ToDictionary(pair => pair.Key, pair => pair.Value);
            this.ability = ability;
        }

        internal OfficerOfficePeopleSelection Capture(int targetOffice)
        {
            int? holder = null;
            foreach (var (person, post) in assignments)
                if (post == targetOffice) { holder = person; break; }
            bool canDismiss = holder is int id && Need(Current.ReadPersonNativeValidity(
                Current.ResolveOfficerSource(id) ?? throw Missing("holder_pointer")), "holder_validity");
            var admissions = ordered.ToImmutableDictionary(person => person.Id, person =>
            {
                bool enabled = person.CanAssign && (!canDismiss || person.Id != holder);
                return new OfficerOfficePeopleAdmission(enabled,
                    !person.CanAssign ? person.DisabledReason : canDismiss && person.Id == holder
                        ? "officer-office.current_slot_holder" : null)
                { FocusHint = enabled ? null : PrepareFocusHint(person, holder) };
            });
            var columns = Columns.Select(Column).ToImmutableArray();
            var groups = ImmutableArray.Create(new OfficerInformationGroup(0, 0x3CD, Data(0x3CD), columns));
            var pages = new OfficerInformationPageSet(Data(0x32E), groups,
                ordered.Select(person => person.Id).ToArray(),
                (person, column) => Cell(person, column).Display with
                    { SortUnresolvedOwner = Cell(person, column).SortMissing },
                (person, column) => Cell(person, column).SortMissing,
                (left, right, column, ascending) =>
                    (Cell(left, column).Key ?? throw Missing("sort_key"))
                        .Before(Cell(right, column).Key ?? throw Missing("sort_key"), ascending));
            //74CEC0 supplies no selected set. The initial checked-column sort
            //therefore preserves this exact caller order and leaves selection empty.
            return new(pages, admissions, canDismiss)
            { GuideText = owner.PrepareCommanderSelectionGuide() };
        }

        private AreaAdministrationQuery<string> PrepareFocusHint(OfficeCommandPerson person, int? holder)
        {
            try
            {
                //75D5B0 tests the actual target holder first, then the ordinary
                //stage1 gate. DisabledReason is presentation-independent metadata.
                int root = person.Id == holder ? 0x597A : Stage(person) == 1 ? 0x597B : 0x5883;
                focusMessages ??= new(owner._repository.LoadMessages(owner.Seed.Language,
                    San14.Data.Formats.MessageKind.Game).Messages);
                focusAbilities ??= new(Current, owner._scenario, owner.Seed.Language,
                    SimulationRuleIdentity.ForLocale(owner.Seed.Language));
                var context = new DeploymentNarrationCurrentPeople(state, Current, focusAbilities,
                    speaker: root == 0x5883 ? person.Id : null, ruler: null,
                    appearanceLanguage: owner.Seed.Language);
                return AreaAdministrationQuery<string>.Known(
                    focusMessages.ResolveOfficePeopleSelectionFocusHint(context, root));
            }
            catch (InvalidDataException error)
            { return AreaAdministrationQuery<string>.Unknown(error.Message); }
        }

        private OfficerInformationColumn Column(int id)
        {
            (int caption, int width, int format, bool ascending) = id switch
            {
                0 => (0x5A8, 132, 3, true), 1 => (0x5DD, 132, 3, true),
                2 => (0x5C8, 160, 3, true), 3 => (0x21D, 118, 5, false),
                10 => (0x822, 132, 5, true), 4 => (0x3D2, 188, 5, false),
                5 => (0x429, 76, 5, false), 6 => (0x238, 118, 5, false),
                7 => (0x239, 118, 5, false), 8 => (0x23A, 118, 5, false),
                9 => (0x23B, 118, 5, false), _ => throw Missing("column")
            };
            return new(id, caption, Data(caption), width, format, ascending, true, true);
        }

        private Prepared Cell(int id, int column)
        {
            if (cells.TryGetValue((id, column), out var result)) return result;
            try
            {
                var person = people.GetValueOrDefault(id) ?? throw Missing("person_member");
                result = column switch
                {
                    0 => Prepare(() => Plain(Person(id).Name), () => Text(Person(id).CurrentNameSortKey
                        ?? throw Missing("person_name_sort_key"))),
                    1 => Prepare(() => Plain(PostValid(OldPost(person)) ? PostName(OldPost(person)) : ""),
                        () => PostValid(OldPost(person)) ? Text(PostSort(OldPost(person))) : Empty()),
                    2 => Prepare(() => NewPost(person), () => Text(PostSort(DraftPost(person)))),
                    3 => Prepare(() => Loyalty(person), () => Numeric(LoyaltyValues(person).Next)),
                    10 => Prepare(() => Plain(StageText(Stage(person))), () => Numeric(Stage(person))),
                    4 => Prepare(() => Command(person), () => Numeric(PostCapacity(DraftPost(person)))),
                    5 => Prepare(() => Plain(Number(Salary(DraftPost(person)), 2)),
                        () => Numeric(Salary(DraftPost(person)))),
                    >= 6 and <= 9 => Prepare(() => Ability(person, column - 6),
                        () => Numeric(AbilityValues(person, column - 6).Next)),
                    _ => throw Missing("column")
                };
            }
            catch (InvalidDataException error)
            { result = new(new(null, null, error.Message), null, error.Message); }
            cells.Add((id, column), result); return result;
        }

        private static Prepared Prepare(Func<Display> display, Func<Key> key)
        {
            OfficerInformationCell cell; Key? sort = null; string? issue = null;
            try
            {
                var shown = display();
                cell = new(shown.Text, null, null) { SourceFormattedText = shown.SourceText };
            }
            catch (InvalidDataException error) { cell = new(null, null, error.Message); }
            try { sort = key(); } catch (InvalidDataException error) { issue = error.Message; }
            return new(cell, sort, issue);
        }

        private Display NewPost(OfficeCommandPerson person)
        {
            int old = OldPost(person), next = DraftPost(person);
            if (old == next) return Plain("");
            if (!PostValid(next)) return Plain(Data(0x53F));
            string name = PostName(next);
            int oldGrade = Need(OwnedArmyMoraleWaveInputs.ReadPost37(state, old), "old_post_grade");
            int grade = Need(OwnedArmyMoraleWaveInputs.ReadPost37(state, next), "draft_post_grade");
            if (oldGrade == grade) return Plain(name);
            return StyledSuffix(name, Data(grade < oldGrade ? 0x63F : 0x640), grade < oldGrade);
        }

        private (int Raw, int LowerClamped, int Next) LoyaltyValues(OfficeCommandPerson person)
        {
            int raw = Need(ArmyDestructionLoyalty.ReadCurrentRawE8(state, Current, person.Physical,
                owner._governorInputs), "loyalty_E8");
            int old = OldPost(person), next = DraftPost(person);
            int delta = Need(Current.ReadPersonNativeValidity(person.Physical), "person_validity") &&
                PostValid(old) && PostValid(next)
                ? owner.OfficeLoyaltyDeltaCurrent(state, Current, person.Physical, next, old) : 0;
            int lower = Math.Max(unchecked(raw + delta), 0);
            return (raw, lower, Math.Min(lower, 100));
        }

        private Display Loyalty(OfficeCommandPerson person)
        {
            var value = LoyaltyValues(person);
            string number = Number(value.Next, 3);
            if (value.LowerClamped == value.Raw || value.Raw > 99 && value.LowerClamped > 99)
                return Plain(number);
            int delta = unchecked(value.Next - value.Raw);
            return Changed(number, delta);
        }

        private (int Next, int Difference) AbilityValues(OfficeCommandPerson person, int dimension)
        {
            var query = ability(person.Id, dimension);
            if (!query.Available) throw new InvalidDataException(query.UnresolvedInput ?? Missing("ability").Message);
            int next = PostAbilityBonus(DraftPost(person), dimension);
            int old = PostAbilityBonus(OldPost(person), dimension);
            int difference = unchecked(next - old);
            return (unchecked(query.Value + difference), difference);
        }

        private Display Ability(OfficeCommandPerson person, int dimension)
        {
            var value = AbilityValues(person, dimension);
            return value.Difference == 0 ? Plain(Number(value.Next, 3)) : Changed(Number(value.Next, 3), value.Difference);
        }

        private Display Command(OfficeCommandPerson person)
        {
            int next = PostCapacity(DraftPost(person)), old = PostCapacity(OldPost(person));
            return next == old ? Plain(Number(next, 5)) : Changed(Number(next, 3), unchecked(next - old));
        }

        private int Stage(OfficeCommandPerson person)
        {
            if (Need(Current.ReadPersonStatus(person.Physical), "person_status") == 1 ||
                DuelAftermathSpecialPerson(Current, person.Physical) || OfficeSpecial(Current, person.Physical)) return 0;
            return Need(OfficerMeritSourceAdapter.EncodeStage(Person(person.Id).MeritState), "merit_stage_1BC");
        }

        private string StageText(int stage)
        {
            if (stage is < 1 or > 6) return Data(0x353);
            var catalog = owner._scenario.Snapshot.CreditRankParameters ?? throw Missing("credit_rank_catalog");
            if (catalog.Ranks.Count != 7 || catalog.Ranks[stage].Slot != stage) throw Missing("credit_rank_slot");
            return catalog.Ranks[stage].Name;
        }

        private int PostAbilityBonus(int post, int dimension)
        {
            //1EE360 reads the actual raw Post operand even when its native
            //validity rejects it as an assignable slot. Use the same full-table
            //operands as the existing Office definition cells, not filtered Slots.
            if (dimension == 0) return Need(OwnedArmyMoraleWaveInputs.ReadPost38(state, Current, post), "post_leadership_bonus");
            var row = state.OfficerOffices.GetValueOrDefault(post) ?? throw Missing("post_ability_definition");
            return dimension switch
            {
                1 => row.AbilityBonus.Strength, 2 => row.AbilityBonus.Intelligence,
                3 => row.AbilityBonus.Politics, _ => throw new ArgumentOutOfRangeException(nameof(dimension))
            };
        }

        private int Salary(int post) => unchecked(OfficeByte(state,
            OwnedArmyGovernorComparatorInputs.OfficeGuard36Key(post)) *
            NativeMonthlySalaryRules.ForLanguage(owner.Seed.Language).OfficeMultiplier);
        private int PostCapacity(int post) => Need(OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state, post), "post_capacity");
        private bool PostValid(int post) => Need(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, post), "post_validity");
        private int OldPost(OfficeCommandPerson person) => Need(Current.ReadMoraleWavePersonPostE7(person.Physical), "original_raw_post_E7");
        private int DraftPost(OfficeCommandPerson person) => assignments.GetValueOrDefault(person.Id, OfficeNone);
        private string PostName(int post) => (state.OfficerOffices.GetValueOrDefault(post) ?? throw Missing("post_name")).Name;
        private string PostSort(int post)
        {
            var catalog = owner._scenario.Snapshot.RankParameters ?? throw Missing("post_sort_catalog");
            if ((uint)post >= (uint)catalog.Offices.Count || catalog.Offices[post].Id != post) throw Missing("post_sort_slot");
            return catalog.Offices[post].Pinyin;
        }
        private OfficerState Person(int id) => state.Officers.GetValueOrDefault(id) ?? throw Missing("person_owner");
        private string Data(int id) => owner.ReadHistoryDataText(1, id);
        private static string Number(int value, int width) => value.ToString(CultureInfo.InvariantCulture).PadLeft(width);
        private static Display Plain(string text) => new(text, text);
        private static Display Changed(string number, int delta)
        {
            string signed = delta.ToString("+0;-0;0", CultureInfo.InvariantCulture);
            //309990 increments the preset index in its opcode7 operand.
            string preset = delta > 0 ? "66" : "68";
            return new(number + "(" + signed + ")", number + "(^07~" + preset + "~" + signed + "^00~0~)");
        }
        private static Display StyledSuffix(string prefix, string suffix, bool positive) =>
            new(prefix + suffix, prefix + "^07~" + (positive ? "66" : "68") + "~" + suffix + "^00~0~");
        private static Key Numeric(int value) => new(value, null, false);
        private static Key Text(string value) => new(0, value, false);
        private static Key Empty() => new(0, null, true);
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("officer-office.people." + field);
        private sealed record Display(string Text, string SourceText);
        private sealed record Prepared(OfficerInformationCell Display, Key? Key, string? SortMissing);
        private sealed record Key(int Number, string? Text, bool Empty)
        {
            internal bool Before(Key right, bool ascending)
            {
                //All eleven source columns use comparison mode0. Reversing
                //exchanges typed operands, including the tag2 empty priority.
                if (Empty != right.Empty) return ascending ? !Empty : Empty;
                if (Empty) return false;
                if (Text is not null && right.Text is not null)
                    return ascending ? PersonNameOrder.Compare(Text, right.Text) < 0
                        : PersonNameOrder.Compare(right.Text, Text) < 0;
                return ascending ? Number < right.Number : Number > right.Number;
            }
        }
    }
}


//The feature's three finite hint roots use the established message parser and
//the caller's retained Person context. This adds no text dialect or sampler.
internal sealed partial class NarrationResourceMessages
{
    internal string ResolveOfficePeopleSelectionFocusHint(INarrationCurrentContext context, int root)
    {
        if (root is not (0x597A or 0x597B or 0x5883))
            throw new InvalidDataException("officer-office.people.focus_hint_root");
        _prefix = "officer-office.people.focus_hint";
        bool previous = _report; _report = true;
        try
        {
            //Both installed locales have no references or random expressions;
            //5883 alone reads the actual bound Person0 name field.
            return ExecuteMessage(context, _ => throw Failure("unexpected_random_argument"),
                root, Get(root, true), _ => false, true, null, _ => false).Text;
        }
        finally { _report = previous; }
    }
}
