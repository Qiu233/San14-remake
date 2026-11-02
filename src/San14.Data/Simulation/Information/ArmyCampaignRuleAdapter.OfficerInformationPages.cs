using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public OfficerInformationPageSet PrepareOfficerInformationPages(GameState state, int playerForceId,
        IReadOnlyList<int> actualOfficerIds, bool? dynamicPaletteEnabled = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actualOfficerIds);
        return new OfficerInformationCurrent(this, state, playerForceId, rowPaletteEnabled: dynamicPaletteEnabled).Pages(actualOfficerIds);
    }

    public AreaAdministrationQuery<OfficerInformationPageSet> PrepareAllOfficerInformationPages(
        GameState state, int playerForceId, bool ownForceOnly)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            // Ordinary Info type14 passes its actual OwnForce tab predicate
            // as the source dialog's dynamic-colour enable argument.
            var source = new OfficerInformationCurrent(this, state, playerForceId, rowPaletteEnabled: ownForceOnly);
            return AreaAdministrationQuery<OfficerInformationPageSet>.Known(
                source.Pages(source.OrdinaryCandidates(ownForceOnly)));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<OfficerInformationPageSet>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<OfficerInformationPageSet> PrepareArmyCaptiveOfficerPages(
        GameState state, int playerForceId, int unitId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var source = new OfficerInformationCurrent(this, state, playerForceId);
            var unit = state.Units.GetValueOrDefault(unitId) ?? throw OfficerInformationCurrent.Missing("captives.Army_owner");
            int[] ids = EnumerateUnitDetailCaptivePhysicalSlots(state, source.current, unit)
                .Select(physical => (int)OfficerInformationCurrent.Need(source.current.ReadPersonIdentity(physical), "captives.Person_id")).ToArray();
            return AreaAdministrationQuery<OfficerInformationPageSet>.Known(source.Pages(ids));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<OfficerInformationPageSet>.Unknown(error.Message); }
    }

    private sealed partial class OfficerInformationCurrent
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly GameState state;
        private readonly int playerForceId;
        private readonly bool? rowPaletteEnabled;
        private readonly Lazy<SourceWorldContext> captured;
        internal SourceWorldContext current => captured.Value;
        private readonly NewGameOfficerListInputs? prestartListInputs;
        private CityDomesticPersonCellSource? cells;
        private CityDomesticPersonCellSource Cells => cells ??= new(state, current, rules._definitions,
            rules._governorInputs, rules._scenario, rules._repository, rules.Seed.Language,
            rules.ReadApplicationGameplay()?.UserSelectionServicePresent, prestartListInputs);
        private TacticRemakeCurrentAbilities? abilities;
        private TacticRemakeCurrentAbilities Abilities => abilities ??= new(current, rules._scenario,
            rules.Seed.Language, SimulationRuleIdentity.ForLocale(rules.Seed.Language));
        private readonly Dictionary<int, Person> people = [];
        private readonly Dictionary<(int Person, int Column), PreparedCell> prepared = [];

        internal OfficerInformationCurrent(ArmyCampaignRuleAdapter rules, GameState state, int playerForceId,
            bool? rowPaletteEnabled = null, SourceWorldContext? retainedCurrent = null, bool bindGameplayInputs = true, NewGameOfficerAbilityProducer? prestartAbilities = null)
        {
            this.rules = rules; this.state = state; this.playerForceId = playerForceId;
            this.rowPaletteEnabled = rowPaletteEnabled;
            if (!bindGameplayInputs)
                prestartListInputs = rules.CreateNewGameOfficerListInputs(state,
                    retainedCurrent ?? throw Missing("normal-load.retained-current"),
                    prestartAbilities ?? throw Missing("normal-load.ability-inputs"));
            newGameAbilityProducer = prestartAbilities;
            captured = new(() =>
            {
                var result = retainedCurrent ?? rules.CaptureDeploymentPersonSelectionCurrent(state);
                //A pre-start caller lends its real normal-load Person owner.
                //Its clock, Army lifecycle and ability banks do not exist yet;
                //do not create them just to display the common Person columns.
                if (!bindGameplayInputs) return result;
                result.RestoreOwnedArmyReturnSlots(state, state.ReadArmyLifecycle() ?? throw Missing("army_lifecycle"));
                ForceUnregisterInputs.BindCurrent(state, result);
                OwnedArmyReturnServiceInputs.BindCurrent(state, result);
                OwnedArmyLifecycleDetachedInputs.BindCurrent(state, result);
                PersonArmyCaptureInputs.BindCurrent(state, result);
                rules._personRelationInputs.BindCurrent(state, result);
                rules._moraleWaveInputs.BindCurrent(state, result);
                rules._baseCapacityInputs.BindCurrent(state, result);
                DeploymentTacticCurrentInputs.BindCurrent(state, result);
                result.EventConditions.CurrentYear = state.NativeMapSeason?.ThirdDate.Year;
                result.EventConditions.ScenarioMode = state.CampaignEventWorld?.ScenarioMode;
                return result;
            });
        }

        internal OfficerInformationPageSet Pages(IReadOnlyList<int> ids, string? title = null,
            ImmutableArray<OfficerInformationGroup>? groups = null) => new(title ?? Data(1, 0x5d1),
            groups ?? OfficerInformationSources.Create(Data), ids,
            (id, column) => Cell(id, column).Display with { SortUnresolvedOwner = Cell(id, column).SortUnresolvedOwner },
            (id, column) => Cell(id, column).SortUnresolvedOwner,
            (left, right, column, ascending) => (Cell(left, column).Key ?? throw Missing("sort_key"))
                .Before(Cell(right, column).Key ?? throw Missing("sort_key"), ascending)) { DynamicPaletteEnabled = rowPaletteEnabled };

        internal int[] OrdinaryCandidates(bool own)
        {
            var result = new List<int>();
            int player = Need(current.ReadForceTableReference(playerForceId is >= 0 and < 52 ? playerForceId : 0)?.PhysicalSlot,
                "candidate_player_force");
            //The normal information caller's actual Person table filter, not
            //the Domain dictionary or a projected complete nine-column page.
            for (int lookup = 0; lookup < 1851; lookup++)
            {
                var reference = current.ReadRegistryTableReference(RegistryKind.Person, lookup) ?? throw Missing("candidate_lookup");
                if (!Need(current.ReadRegistryObjectNativeValidity(reference), "candidate_validity")) continue;
                int physical = Need(reference.PhysicalSlot, "candidate_receiver");
                byte status = Need(current.ReadPersonStatus(physical), "candidate_status");
                ushort flags = Need(current.ReadArmyReturnPersonFlags196(physical), "candidate_flags");
                if (status is < 1 or > 6 || status == 5 && (flags & 0x4000) != 0) continue;
                int force = Need(current.ReadPersonForce(physical)?.PhysicalSlot, "candidate_force");
                ushort home = Need(current.ReadPersonFirstLocation(physical), "candidate_home");
                if (force == 51 || home is not (>= 1 and <= 61) && force is >= 46 and <= 50 || own && force != player) continue;
                result.Add(Need(current.ReadPersonIdentity(physical), "candidate_id"));
            }
            //Retain the existing ordinary caller's initial name order, but
            //do not invent a user header action or its next-direction state.
            return result.Select(id => (Id: id, Key: Cells.Name(ReadPerson(id).Candidate, true)))
                .OrderBy(row => row.Key, Comparer<string>.Create(PersonNameOrder.Compare))
                .Select(row => row.Id).ToArray();
        }

        private Person ReadPerson(int id)
        {
            if (people.TryGetValue(id, out var saved)) return saved;
            if (id is < 1 or > 6000) throw Missing("Person_identity");
            int physical = Need(current.ReadPersonLookupReference(id)?.PhysicalSlot, "Person_lookup");
            var owner = state.Officers.GetValueOrDefault(id) ?? throw Missing("Person_owner");
            return people[id] = new(id, physical, owner);
        }

        private PreparedCell Cell(int id, int column)
        {
            if (prepared.TryGetValue((id, column), out var saved)) return saved;
            try
            {
                var person = ReadPerson(id);
                saved = diplomacyPrisonerRansoms is not null && column is 78 or 79 or 113
                    ? DiplomacyPrisonerCell(person, column)
                    : diplomacyExecutorCells is not null && column is 75 or 102 or 105
                    ? DiplomacyExecutorCell(person, column) : column switch
                {
                    39 or 102 or 132 or 133 or 134 or 139 or 208 or 209 or 210 => MovementCell(person, column),
                    >= 40 and <= 65 or 97 or 98 or 99 or 26 or 101 => MilitaryCell(person, column),
                    24 or 28 or 29 or 30 or 31 or 32 or 33 or >= 186 and <= 192 => PersonalCell(person, column),
                    >= 8 and <= 12 or 18 or 20 or 22 or 193 or 194 or 196 or 197 or 198 => AbilitiesCreditCell(person, column),
                    37 or 86 or 88 or 34 or 204 => TasksCell(person, column),
                    _ => BasicCell(person, column)
                };
            }
            catch (InvalidDataException error) { saved = new(new(null, null, error.Message), null, error.Message); }
            return prepared[(id, column)] = saved;
        }

        private PreparedCell MakeCell(Func<string?> display, Func<SortKey>? sort = null, Func<int?>? palette = null, Func<InformationTextStatePresets?>? statePalette = null)
        {
            string? text = null, issue = null, sortIssue = null, presetIssue = null;
            int? preset = null; InformationTextStatePresets? presets = null; SortKey? key = null;
            try { text = display() ?? ""; } catch (InvalidDataException error) { issue = error.Message; }
            if (palette is not null)
                try { preset = palette(); } catch (InvalidDataException error) { presetIssue = error.Message; }
            if (sort is not null)
                try { key = sort(); } catch (InvalidDataException error) { sortIssue = error.Message; }
            if (statePalette is not null)
                try { presets = statePalette(); } catch (InvalidDataException error) { presetIssue = error.Message; }
            return new(new(text, preset, issue) { PresetUnresolvedOwner = presetIssue, StatePresets = presets }, key, sortIssue);
        }

        private string Data(int section, int id) => rules.ReadHistoryDataText(section, id);
        private static string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
        internal static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        internal static InvalidDataException Missing(string field) => new("officer-information." + field);
        private static string? Join(string? first, string second) => first is null ? second : first + ";" + second;
        private sealed record Person(int Id, int PhysicalSlot, OfficerState Owner)
        { internal CityDomesticCandidate Candidate => new(checked((ushort)Id), PhysicalSlot); }
        private sealed record PreparedCell(OfficerInformationCell Display, SortKey? Key, string? SortUnresolvedOwner);
        private abstract record SortKey { internal abstract bool Before(SortKey other, bool ascending); }
        private sealed record NumberKey(long Value, bool Empty = false) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending)
            {
                var right = (NumberKey)other;
                if (Empty != right.Empty) return !Empty;
                return !Empty && (ascending ? Value < right.Value : Value > right.Value);
            }
        }
        private sealed record NameKey(string Value, bool Empty = false) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending)
            {
                var right = (NameKey)other;
                if (Empty != right.Empty) return !Empty;
                return !Empty && (ascending ? PersonNameOrder.Compare(Value, right.Value) < 0 : PersonNameOrder.Compare(right.Value, Value) < 0);
            }
        }
    }
}
