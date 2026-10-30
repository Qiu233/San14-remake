using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState ApplyConfirmedPlayerCommanderCapacity(GameState state, SourceWorldContext current)
    {
        var force = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw CommanderMissing("capacity_player");
        if (!CommanderNeed(current.ReadForceNativeValidity(force), "capacity_Force_validity")) return state;
        int physical = CommanderNeed(current.ReadForcePhysicalSlot(force), "capacity_Force_identity");
        _moraleWaveInputs.BindCurrent(state, current);
        int rank = CommanderNeed(current.ReadMoraleWaveForceRank47(physical), "capacity_Force47");
        return ApplyPlayerCommanderCapacityForRankCurrent(state, current, force, rank);
    }

    private GameState ApplyPlayerCommanderCapacityForRankCurrent(GameState state, SourceWorldContext current,
        ForceReference force, int rank)
    {
        if (!CommanderNeed(current.ReadForceNativeValidity(force), "capacity_Force_validity") ||
            !TalentForceIsPlayerCurrent(state, current, force)) return state;
        //2EE410 falls back to Rank0 for an overflowing rank. Rank0's native
        //validity is false, so it leaves the existing count untouched.
        if (rank is < 1 or > 12) return state;
        var table = _scenario.Snapshot.RankParameters ?? throw CommanderMissing("capacity_Rank_table");
        if (table.Ranks.Count != 13 || table.Ranks[rank].Id != rank)
            throw CommanderMissing("capacity_Rank_identity");
        //23F880 serializes AC, AD..B1, B2, then B3. Their fixed-row offsets
        //are156,157..161,162,163 respectively; B3 is the card capacity.
        byte[] row = Convert.FromHexString(table.Ranks[rank].RawBytesHex);
        if (row.Length < 164) throw CommanderMissing("capacity_RankB3");
        var saved = state.CommanderSelection ?? throw CommanderMissing("capacity_selection");
        return state with { CommanderSelection = new(Math.Min(5, (int)row[163]), saved.Candidates) };
    }

    /// <summary>The actual selector candidates and independently sorted map
    /// cards. Called on a page/card refresh, never from a draw callback.</summary>
    public CommanderSelectionView QueryCommanderSelection(GameState state, int forceId)
        => QueryCommanderSelectionCurrent(state, forceId, true);

    /// <summary>The map-card consumer does not build the unrelated selector
    /// candidate list or query abilities of unselected people.</summary>
    public CommanderSelectionView QueryCommanderCards(GameState state, int forceId)
        => QueryCommanderSelectionCurrent(state, forceId, false);

    private CommanderSelectionView QueryCommanderSelectionCurrent(GameState state, int forceId, bool includeCandidates, SourceWorldContext? captured = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        int? capacity = state.CommanderSelection?.ActiveCount;
        CommanderSelectionSlot[] slots = [];
        var candidates = new List<CommanderSelectionPerson>();
        var cards = new List<CommanderSelectionCard>();
        try
        {
            var current = captured ?? CaptureOwnedArmyReturnWorld(state).Context;
            var selection = ReadCommanderSelectionCurrent(state, current, forceId);
            capacity = selection.Count;
            slots = selection.Storage.Select((row, ordinal) =>
                new CommanderSelectionSlot(ordinal, row.OfficerId, row.UninterpretedValue)).ToArray();
            var abilities = SearchAbilities(current);
            var people = new Dictionary<int, CommanderSelectionPerson>();
            CommanderSelectionPerson Person(int physical)
            {
                int id = CommanderNeed(current.ReadPersonIdentity(physical), "Person10");
                if (people.TryGetValue(id, out var found)) return found;
                if (!state.Officers.TryGetValue(id, out var person) || current.ResolveOfficerSource(id) != physical)
                    throw CommanderMissing("Person_owner");
                int Dimension(int kind) => abilities.ResolveDimension(state, id, kind,
                    (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect))
                    ?? throw CommanderMissing("Person_ability_" + kind);
                var values = new OfficerAbilities(Dimension(0), Dimension(1), Dimension(2), Dimension(3), Dimension(4));
                var row = new CommanderSelectionPerson(id, person.Name, values,
                    current.ReadPersonPortraitAppearance(physical));
                people.Add(id, row); return row;
            }
            //438A80 sorts by the sum of all five effective abilities. LINQ's
            //stable ordering retains the registered-chain order on equal sums.
            if (includeCandidates)
                candidates.AddRange(ReadCommanderSelectionCandidates(state, current, forceId)
                    .Select(Person).OrderByDescending(row => CommanderAbilitySum(row.Abilities)));

            var displayed = new List<(int Physical, int? Period)>();
            for (int ordinal = 0; ordinal < selection.Count; ordinal++)
            {
                int id = CommanderNeed(selection.Storage[ordinal].OfficerId, "selected_Person_identity");
                if (id is < 0 or > ushort.MaxValue) throw CommanderMissing("selected_Person_encoding");
                var pointer = id < 6001 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
                if (pointer is null) throw CommanderMissing("selected_Person_pointer");
                if (pointer.PhysicalSlot is not int physical ||
                    !CommanderNeed(current.ReadPersonNativeValidity(physical), "selected_Person_validity")) continue;
                int actualId = CommanderNeed(current.ReadPersonIdentity(physical), "selected_actual_Person10");
                //898C30 takes the first matching saved auxiliary value even
                //when a legacy selection contains duplicate identities.
                int? period = selection.Storage.Take(selection.Count)
                    .FirstOrDefault(row => row.OfficerId == actualId)?.UninterpretedValue;
                displayed.Add((physical, period));
            }
            foreach (var row in displayed.OrderBy(row => CardPriority(row.Physical))
                .ThenByDescending(row => Person(row.Physical).Abilities.Intelligence + Person(row.Physical).Abilities.Strength)
                .ThenBy(row => CommanderNeed(current.ReadPersonIdentity(row.Physical), "card_Person10")))
            {
                var person = Person(row.Physical);
                var location = ReadCommanderCardLocation(current, row.Physical);
                cards.Add(new(person.OfficerId, person.Name, person.Abilities, person.PortraitAppearanceIndex,
                    row.Period, location.HexId, location.Available, location.UnresolvedOwner));
            }
            return new(forceId, capacity, candidates.AsReadOnly(), Array.AsReadOnly(slots), cards.AsReadOnly(), null)
            {
                PageSource = includeCandidates ? PrepareCommanderSelectionPages(state, forceId,
                    candidates.Select(person => person.OfficerId).ToArray(), cards.Select(card => card.OfficerId).ToArray()) : null,
                GuideText = includeCandidates ? PrepareCommanderSelectionGuide() : null
            };

            int CardPriority(int physical) => CommanderNeed(current.ReadPersonStatus(physical), "card_PersonE6") switch
                { 1 => 0, 3 => 1, _ => 2 };
        }
        catch (InvalidDataException error)
        { return new(forceId, capacity, candidates.AsReadOnly(), Array.AsReadOnly(slots), cards.AsReadOnly(), error.Message); }
    }

    public CommanderCardLocation QueryCommanderCardLocation(GameState state, int officerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            int physical = current.ResolveOfficerSource(officerId) ?? throw CommanderMissing("location_Person_pointer");
            return ReadCommanderCardLocation(current, physical);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    internal CommandResult ApplyCommanderSelection(GameState state, ForceState force,
        IReadOnlyList<int> officerIds, CommandContext context)
    {
        try
        {
            if (OwnedCurrentPlayerForce.ReadRawForce3A(state) is byte player && player != force.Id)
                return CommandResult.Reject("Only the current player's commander selection can be edited.", "commander-selection.player_control");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var selection = ReadCommanderSelectionCurrent(state, current, force.Id);
            if (selection.Storage.Length < 5) throw CommanderMissing("five_saved_slots_for_write");
            if (officerIds.Count > selection.Count)
                return CommandResult.Reject("The selection exceeds the current commander capacity.", "commander-selection.capacity");
            var eligible = ReadCommanderSelectionCandidates(state, current, force.Id)
                .Select(person => (int)CommanderNeed(current.ReadPersonIdentity(person), "candidate_Person10")).ToHashSet();
            if (officerIds.Any(id => !eligible.Contains(id)))
                return CommandResult.Reject("A selected officer is no longer in the current selector.", "commander-selection.person_ineligible");
            var next = selection.Storage.ToArray();
            //43C000 clears the five identity words only. Auxiliary words of
            //vacant entries survive; ActiveCount continues to mean capacity.
            for (int ordinal = 0; ordinal < 5; ordinal++)
                next[ordinal] = next[ordinal] with { OfficerId = 0 };
            int? currentPeriod = null;
            for (int ordinal = 0; ordinal < officerIds.Count; ordinal++)
            {
                int id = officerIds[ordinal];
                var old = selection.Storage.Take(5).FirstOrDefault(row => row.OfficerId == id);
                int? period = old is not null ? old.UninterpretedValue :
                    (currentPeriod ??= ReadCommanderSelectionPeriod(state));
                next[ordinal] = new(id, period);
            }
            return CommandResult.Accept(state with { CommanderSelection = new(selection.Count, next) });
        }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable(error.Message, "Current commander selection inputs are unresolved."); }
    }

    private static (int Count, CommanderSelectionCandidate[] Storage) ReadCommanderSelectionCurrent(
        GameState state, SourceWorldContext current, int forceId)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw CommanderMissing("World3A_player");
        if (!CommanderNeed(current.ReadForceNativeValidity(player), "player_validity") ||
            CommanderNeed(current.ReadForcePhysicalSlot(player), "player_identity") != forceId)
            throw CommanderMissing("player_control");
        int count = CommanderNeed(state.CommanderSelection?.ActiveCount, "active_count");
        var storage = state.CommanderSelection?.Candidates;
        //An empty active prefix never reads the candidate storage. In
        //particular, a legitimate old(0,null) is not repaired into five slots.
        if (count == 0) return (0, storage ?? []);
        if (count is < 0 or > 5 || storage is null || storage.Length < count)
            throw CommanderMissing("five_saved_slots");
        return (count, storage);
    }

    private static IReadOnlyList<int> ReadCommanderSelectionCandidates(GameState state,
        SourceWorldContext current, int forceId)
    {
        var force = current.ReadForceTableReference(forceId) ?? throw CommanderMissing("candidate_Force_pointer");
        int district = CommanderNeed(ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot, "primary_District");
        var people = new List<int>();
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw CommanderMissing("Person100"))
        {
            int physical = CommanderNeed(entry, "Person100_payload");
            //3345D0 with filter4 accepts every status at the actual District;
            //43C000 then applies native validity and excludes status6 only.
            if (district is < 1 or > 51 || CommanderNeed(current.ReadPersonDistrictSlot(physical), "candidate_E0") != district ||
                !CommanderNeed(current.ReadPersonNativeValidity(physical), "candidate_validity") ||
                CommanderNeed(current.ReadPersonStatus(physical), "candidate_E6") == 6) continue;
            int id = CommanderNeed(current.ReadPersonIdentity(physical), "candidate_identity");
            if (!state.Officers.ContainsKey(id) || current.ResolveOfficerSource(id) != physical)
                throw CommanderMissing("candidate_domain_identity");
            people.Add(physical);
        }
        return people.AsReadOnly();
    }

    private static CommanderCardLocation ReadCommanderCardLocation(SourceWorldContext current, int physical)
    {
        try
        {
            if (!CommanderNeed(current.ReadPersonNativeValidity(physical), "location_Person_validity")) return new(null, null);
            var army = PersonArmyLocationSource.ReadCommanderArmy(current, physical) ?? throw CommanderMissing("location_Army_pointer");
            if (CommanderNeed(current.ReadArmyUnitNativeValidity(army), "location_Army_validity"))
            {
                int hex = CommanderNeed(current.ReadArmyUnitRawHex2A(army), "location_Army2A");
                return new(hex < 48400 ? hex : null, null);
            }
            int home = CommanderNeed(current.ReadPersonFirstLocation(physical), "location_PersonE2");
            var city = BaseLookup.Find(home, current);
            if (!CommanderNeed(current.ReadBaseNativeValidity(city), "location_Base_validity")) return new(null, null);
            int baseHex = current.ReadBaseCurrentHexIndex(city);
            return new(baseHex < 48400 ? baseHex : null, null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private static int ReadCommanderSelectionPeriod(GameState state)
    {
        var world = state.CampaignEventWorld ?? throw CommanderMissing("World454_offset");
        var season = state.NativeMapSeason ?? throw CommanderMissing("World454_calendar");
        return unchecked(world.ElapsedPeriodOffset + (int)season.AdvancedPeriods);
    }

    private static int CommanderAbilitySum(OfficerAbilities values) =>
        values.Leadership + values.Strength + values.Intelligence + values.Politics + values.Charm;
    private static T CommanderNeed<T>(T? value, string field) where T : struct => value ?? throw CommanderMissing(field);
    private static InvalidDataException CommanderMissing(string field) => new("commander-selection." + field);
}
