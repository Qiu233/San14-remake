using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The normal learning selector's three Gem groups over its
    /// already produced inventory, including the separate disabled set.</summary>
    public AreaAdministrationQuery<OfficerInformationPageSet> PreparePersonnelLearningBookPages(
        GameState state, int forceId, IReadOnlyList<PersonnelLearnBook> books)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(books);
        try
        {
            if (!state.Forces.ContainsKey(forceId)) throw LearningBookMissing("Force_owner");
            var source = new PersonnelLearningBookInformation(this, state, books);
            return AreaAdministrationQuery<OfficerInformationPageSet>.Known(source.Pages());
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<OfficerInformationPageSet>.Unknown(error.Message); }
    }

    private sealed class PersonnelLearningBookInformation
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly GameState state;
        private readonly int[] ids;
        private readonly Lazy<SourceWorldContext> capture;
        private readonly bool physicalGemIds;
        private readonly Dictionary<(int Gem, int Column), PreparedBookCell> cells = [];
        private SourceWorldContext Current => capture.Value;

        internal PersonnelLearningBookInformation(ArmyCampaignRuleAdapter rules, GameState state,
            IReadOnlyList<PersonnelLearnBook> books)
        {
            this.rules = rules; this.state = state;
            //6DD400 supplies its Gem lookup order. Do not remove the separate
            //749CF0 ineligible rows or replace this order with a name/ID sort.
            ids = books.Select(book => book.GemId).ToArray();
            capture = new(() =>
            {
                var current = rules.CaptureOwnedArmyReturnWorld(state).Context;
                DeploymentTacticCurrentInputs.BindCurrent(state, current);
                return current;
            });
        }

        // The belongings dispatcher already resolved every Gem table pointer.
        // Its shared current is bound once by the enclosing information reader.
        internal PersonnelLearningBookInformation(ArmyCampaignRuleAdapter rules,GameState state,SourceWorldContext current)
        {
            this.rules=rules;this.state=state;ids=[];physicalGemIds=true;capture=new(()=>current);
        }
        internal OfficerInformationCell ReadBelongingsCell(int id,int column)=>Cell(id,column).Display;
        internal string? ReadBelongingsSortIssue(int id,int column)=>Cell(id,column).SortMissing;
        internal bool BeforeBelongings(int first,int second,int column,bool ascending)=>Before(first,second,column,ascending);

        internal OfficerInformationPageSet Pages()
        {
            int[][] columns = [[0, 1, 2, 3, 4, 5], [0, 6, 7, 8, 9], [0, 10, 11, 12, 13]];
            int[] captions = [0x3CD, 0x404, 0x405];
            var groups = columns.Select((group, index) => new OfficerInformationGroup(index,
                captions[index], Data(1, captions[index]), group.Select(Column).ToImmutableArray())).ToImmutableArray();
            return new(Data(1, 0x5BC), groups, ids,
                (id, column) => Cell(id, column).Display,
                (id, column) => Cell(id, column).SortMissing,
                Before);
        }

        private OfficerInformationColumn Column(int id)
        {
            int[] captions = [0x41E, 0x41F, 0x234, 0x455, 0x456, 0x4B4,
                0x458, 0x459, 0x6C4, 0x45B, 0x458, 0x459, 0x6C4, 0x45B];
            int[] widths = [188, 62, 160, 160, 132, 104, 244, 104, 160, 160, 244, 104, 160, 160];
            //The real mode2 name header still receives clicks. Its comparator
            //returns false both ways; it is not an unavailable name-key sort.
            bool ascending = id is not (1 or 5 or 7 or 8 or 9 or 11 or 12 or 13);
            int format = id is 0 or 2 or 3 or 6 or 10 ? 3 : 4;
            return new(id, captions[id], Data(1, captions[id]), widths[id], format, ascending, true, true);
        }

        private PreparedBookCell Cell(int gemId, int column)
        {
            if (cells.TryGetValue((gemId, column), out var known)) return known;
            string? text = null, missing = null, sortMissing = null;
            BookSortKey? key = null;
            try { text = Display(gemId, column); }
            catch (InvalidDataException error) { missing = error.Message; }
            try { key = Key(gemId, column); }
            catch (InvalidDataException error) { sortMissing = error.Message; }
            var result = new PreparedBookCell(new(text, null, missing) { SortUnresolvedOwner = sortMissing }, key, sortMissing);
            cells.Add((gemId, column), result); return result;
        }

        private string Display(int gemId, int column)
        {
            if (column == 0)
            {
                int physical = Need(Gem(gemId).PhysicalSlot, "Gem_receiver");
                return rules._skills.Gems.Gems[physical].Name;
            }
            if (column == 1)
            {
                var player = Player();
                return Need(Current.ReadForceNativeValidity(player), "player_Force_validity")
                    ? Number(Need(TalentGemAcquisitionSource.Read(state.GemForceInventory, player, Gem(gemId)), "Gem_inventory")) : "";
            }
            var tactic = Tactic(gemId);
            bool valid = Need(Current.ReadSkillTacticValidity(tactic), "Tactic_validity");
            if (column == 2)
                return valid ? state.Tactics.GetValueOrDefault(Need(tactic.PhysicalSlot, "Tactic_receiver"))?.Name
                    ?? throw LearningBookMissing("Tactic_name") : "";
            if (column == 3)
            {
                int system = valid ? Need(Current.ReadSkillTacticRawByte(tactic, 0x40), "Tactic_system") : 0;
                return system is >= 1 and <= 9 ? Data(0, 0x147 + system) : "";
            }
            if (column == 4)
            {
                int factor = valid ? DeploymentTacticSelectionSource.ReadFactor(Current, tactic) : 0;
                return factor == 0 ? "" : factor is 1 or 2 ? Data(1, 0x238 + factor) : Number(factor);
            }
            if (column == 5)
                return Data(1, valid && (Need(Current.ReadSkillTacticFlags(tactic), "Tactic_flags") & 1) != 0 ? 0x338 : 0x339);
            int index = column >= 10 ? 1 : 0;
            int part = column >= 10 ? column - 10 : column - 6;
            return part switch
            {
                0 => valid ? EffectName(Current.ReadTacticEffectOpcode(tactic, index)) : "",
                1 => !valid ? "" : NonzeroNumber(EffectAmount(tactic, index)),
                2 => !valid ? Data(1, 0x353) : NonzeroFormatted(EffectQuarterValue(tactic, index), 0x324),
                3 => !valid ? Data(1, 0x353) : NonzeroFormatted(Current.ReadTacticRaw64(tactic, index), 0x48B),
                _ => throw LearningBookMissing("column")
            };
        }

        private BookSortKey Key(int gemId, int column)
        {
            //777810 reaches the ordinary sort for mode2, but both actual
            //predicates return false.349900's unused Gem+1E is not a tie key.
            if (column == 0) return new(0, 0, null);
            if (column == 1)
            {
                var player = Player();
                return Numeric(Need(Current.ReadForceNativeValidity(player), "sort_player_Force_validity")
                    ? Need(TalentGemAcquisitionSource.Read(state.GemForceInventory, player, Gem(gemId)), "sort_Gem_inventory") : 0);
            }
            var tactic = Tactic(gemId);
            //351E90 reads this actual receiver's independent name key without
            //adding the display getter's native-validity gate.
            if (column == 2)
                return new(1, 0, rules._skills.Formations.Tactics[Need(tactic.PhysicalSlot, "sort_Tactic_receiver")].Pinyin);
            bool valid = Need(Current.ReadSkillTacticValidity(tactic), "sort_Tactic_validity");
            if (column == 3) return Numeric(valid ? Need(Current.ReadSkillTacticRawByte(tactic, 0x40), "sort_Tactic_system") : 0);
            if (column == 4)
            {
                int factor = valid ? DeploymentTacticSelectionSource.ReadFactor(Current, tactic) : 0;
                return factor == 0 ? new(2, int.MaxValue, null) : Numeric(factor);
            }
            if (column == 5) return valid ? Numeric(Need(Current.ReadSkillTacticFlags(tactic), "sort_Tactic_flags") & 1)
                : new(2, int.MaxValue, null);
            if (!valid) return Numeric(0);
            int index = column >= 10 ? 1 : 0;
            return (column >= 10 ? column - 10 : column - 6) switch
            {
                0 => Numeric(Current.ReadTacticEffectOpcode(tactic, index)),
                1 => Numeric(EffectAmount(tactic, index)),
                2 => Numeric(EffectQuarterValue(tactic, index)),
                3 => Numeric(Current.ReadTacticRaw64(tactic, index)),
                _ => throw LearningBookMissing("sort_column")
            };
        }

        private bool Before(int first, int second, int column, bool ascending)
        {
            if (column == 0) return false;
            var a = Cell(first, column).Key ?? throw LearningBookMissing("sort_key");
            var b = Cell(second, column).Key ?? throw LearningBookMissing("sort_key");
            return ascending ? TaggedBefore(a, b) : TaggedBefore(b, a);
        }

        //252F90's mode0 tagged predicate. NA reverses with the operands,
        //unlike mode1's separate rule that always puts NA at the end.
        private static bool TaggedBefore(BookSortKey a, BookSortKey b) => a.Tag switch
        {
            0 => a.Number < b.Number,
            1 => b.Tag == 2 || PersonNameOrder.Compare(a.Text!, b.Text!) < 0,
            2 => false,
            _ => throw LearningBookMissing("sort_tag")
        };

        private int EffectQuarterValue(TacticReference tactic, int index) => Current.ReadTacticEffectOpcode(tactic, index) switch
        {
            >= 2 and <= 14 or 17 or 21 or 22 or 23 => Current.ReadTacticEffectValue(tactic, index) / 4,
            _ => 0
        };

        private int EffectAmount(TacticReference tactic, int index) => Current.ReadTacticEffectOpcode(tactic, index) switch
        {
            1 or 15 or 16 or 18 or 20 => Current.ReadTacticEffectValue(tactic, index),
            >= 5 and <= 14 or 21 or 22 => Current.ReadTacticRaw68(tactic, index),
            _ => 0
        };

        private GemReference Gem(int lookup) => physicalGemIds ? new GemReference(lookup) : Current.ReadSkillGemReference(lookup)
            ?? throw LearningBookMissing("Gem_lookup");
        private TacticReference Tactic(int gemId)
        {
            int raw = Need(Current.ReadSkillGemTactic52(Gem(gemId)), "Gem_Tactic52");
            return Current.ReadSkillTacticReference(raw <= 200 ? raw : 0) ?? throw LearningBookMissing("Tactic_lookup");
        }
        private ForceReference Player() => OwnedCurrentPlayerForce.ReadCurrentReference(state, Current)
            ?? throw LearningBookMissing("current_player_Force");
        private string EffectName(int opcode) => opcode is >= 1 and <= 23 ? Data(0, 0x151 + opcode) : "";
        private string NonzeroNumber(int value) => value == 0 ? Data(1, 0x353) : Number(value);
        private string NonzeroFormatted(int value, int message) => value == 0 ? Data(1, 0x353) : Format(message, value);
        private string Format(int message, int value) => Data(1, message).Replace("%d", Number(value));
        private string Data(int section, int id) => rules.ReadHistoryDataText(section, id);
        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw LearningBookMissing(field);
        private static BookSortKey Numeric(int value) => new(0, value, null);
        private sealed record BookSortKey(int Tag, int Number, string? Text);
        private sealed record PreparedBookCell(OfficerInformationCell Display, BookSortKey? Key, string? SortMissing);
    }

    private static InvalidDataException LearningBookMissing(string field) => new("personnel.learn.book-selector." + field);
}
