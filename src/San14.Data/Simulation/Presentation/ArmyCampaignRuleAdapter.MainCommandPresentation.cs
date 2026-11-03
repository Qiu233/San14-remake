using San14.Core;

namespace San14.Data;

public sealed record MapMainCommandCategoryPresentation(MapCommandCategory Category,
    bool? Enabled, bool? Attention, bool? Badge, IReadOnlyList<string> UnresolvedOwners);

/// <summary>A published child-row predicate and original Game help identity.
/// Unknown admission is distinct from a known source denial.</summary>
public sealed record MapMainCommandEntryPresentation(MapMenuCommand Command, bool? Enabled,
    int? GameMessageId, string? UnresolvedInput = null)
{
    //70D0F0's restricted branch writes the predicate but leaves its existing
    //help output untouched. This differs from unknown or a written empty ID.
    public bool RetainGameMessage { get; init; }

    //Only rows whose real predicate/help producer is projected require a
    //known admission before the bound command can be clicked. Fixed Policy
    //and Events are always source-enabled and keep their existing binding.
    public static bool RequiresPublishedAdmission(MapMenuCommand command) => command is
        MapMenuCommand.Deploy or MapMenuCommand.WithdrawAll or
        MapMenuCommand.Office or MapMenuCommand.Research or MapMenuCommand.CreateDistrict or
        MapMenuCommand.EditDistrict or MapMenuCommand.MoveGroup or MapMenuCommand.EditGroup or
        MapMenuCommand.Search or MapMenuCommand.Recruit or MapMenuCommand.Employ or MapMenuCommand.Items or
        MapMenuCommand.Reward or MapMenuCommand.Merit or MapMenuCommand.Award or MapMenuCommand.Expel or
        MapMenuCommand.Learn or MapMenuCommand.Introduce or
        MapMenuCommand.Poison or MapMenuCommand.Alienate or MapMenuCommand.PlacateArea or MapMenuCommand.Incite or
        MapMenuCommand.ProvokeWar or MapMenuCommand.Sabotage or MapMenuCommand.Rumor or
        MapMenuCommand.Goodwill or MapMenuCommand.Alliance or MapMenuCommand.CancelAlliance or
        MapMenuCommand.ReturnCaptives or MapMenuCommand.RequestAttack or MapMenuCommand.DemandSurrender or MapMenuCommand.Exchange or
        MapMenuCommand.Spy or MapMenuCommand.Scout or MapMenuCommand.Patrol or
        MapMenuCommand.Proposals or MapMenuCommand.CreatedEvents or MapMenuCommand.Trade;
}

public sealed record MapMainCommandPresentation(IReadOnlyList<MapMainCommandCategoryPresentation> Categories,
    IReadOnlyList<string> UnresolvedOwners)
{
    public MapHeaderUpdate Update { get; init; } = MapHeaderUpdate.Updated;
    public IReadOnlyList<MapMainCommandEntryPresentation> Entries { get; init; } = [];

    /// <summary>895B20: real summary manager visibility, or the source camera's
    /// band+fraction coordinate strictly above4. Animation admission stays in
    /// the view owner. This pure scalar rule performs no world queries.</summary>
    public static bool? ShouldAutoHide(bool? mapSummaryVisible, float? zoomLevel)
        => mapSummaryVisible == true || zoomLevel > 4 ? true :
            mapSummaryVisible == false && zoomLevel.HasValue ? false : null;
}

/// <summary>The host's actual independent edited-event manager publication.
/// A different World cannot reuse its ready flag.</summary>
public sealed record MainCommandApplicationFacts(GameState World, bool? EditedEventsReady);

public sealed partial class ArmyCampaignRuleAdapter
{
    private MainCommandApplicationFacts? mainCommandApplicationFacts;
    public void ConfigureMainCommandApplicationFacts(MainCommandApplicationFacts? facts)
    {
        if (ReferenceEquals(mainCommandApplicationFacts, facts) ||
            mainCommandApplicationFacts is { } old && facts is { } next &&
            ReferenceEquals(old.World, next.World) && old.EditedEventsReady == next.EditedEventsReady) return;
        mainCommandApplicationFacts = facts;
        MainCommandPresentationRevision = unchecked(MainCommandPresentationRevision + 1);
    }

    private MapMainCommandPresentation ReadMainCommandPresentationCurrent(GameState state, SourceWorldContext current)
    {
        var issues = new List<string>();
        bool? Read(Func<bool> query)
        { try { return query(); } catch (InvalidDataException error) { issues.Add(error.Message); return null; } }
        bool? tutorial = Read(() => ReadApplicationTutorialActive() ?? throw MainCommandMissing("tutorial_controller"));
        bool? special = Read(() => ReadApplicationInformationRestriction(state) ?? throw MainCommandMissing("selected_user"));
        bool?[] attention = new bool?[7]; bool?[] badges = new bool?[7];
        //The source publishes neither attention0/3 nor badge0/4. These view
        //signals have no producer and remain unlit, without inventing a notice.
        attention[0] = attention[3] = badges[0] = badges[4] = false;
        uint? flags = OwnedArmyReturnServiceInputs.ReadCurrent(state);
        bool? valid = Read(() =>
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw MainCommandMissing("player_pointer");
            current.CurrentPlayerForce = player;
            return current.ReadForceNativeValidity(player) ?? throw MainCommandMissing("player_validity");
        });
        if (flags is null) issues.Add("map.main_command.world_notification_flags");
        //333EF0 or invalid Force does not refresh these Base caches. The view
        //retains its last actual publication; missing first state stays unknown.
        bool refresh = flags is uint bits && (bits & 0x100) == 0 && valid == true;
        bool currentBound = false, indexesBound = false, itemsBound = false, areasBound = false;
        var entryPresentations = new Dictionary<MapMenuCommand, MapMainCommandEntryPresentation>();
        MapMainCommandEntryPresentation EntryPresentation(MapMenuCommand command)
        {
            if (!entryPresentations.TryGetValue(command, out var entry))
            {
                entry = command switch
                {
                    MapMenuCommand.Deploy or MapMenuCommand.WithdrawAll =>
                        ReadCampaignCommandEntry(state, current, command, BindCommandCurrent),
                    MapMenuCommand.Poison or MapMenuCommand.Alienate or MapMenuCommand.PlacateArea or MapMenuCommand.Incite or
                    MapMenuCommand.ProvokeWar or MapMenuCommand.Sabotage or MapMenuCommand.Rumor =>
                        ReadStrategyCommandEntry(state, current, command, special, BindCommandCurrent),
                    MapMenuCommand.Goodwill or MapMenuCommand.Alliance or MapMenuCommand.CancelAlliance or
                    MapMenuCommand.ReturnCaptives or MapMenuCommand.RequestAttack or MapMenuCommand.DemandSurrender or MapMenuCommand.Exchange =>
                        ReadDiplomacyCommandEntry(state, current, command, special, BindCommandCurrent,
                            BindCommandIndexes, BindCommandItems, BindCommandAreas),
                    MapMenuCommand.Events => new(command, true, 0x3142),
                    MapMenuCommand.Spy or MapMenuCommand.Scout or MapMenuCommand.Patrol =>
                        ReadInformationCommandEntry(state, current, command, special, BindCommandCurrent),
                    MapMenuCommand.Search or MapMenuCommand.Recruit or MapMenuCommand.Employ or MapMenuCommand.Items or
                    MapMenuCommand.Reward or MapMenuCommand.Merit or MapMenuCommand.Award or MapMenuCommand.Expel or
                    MapMenuCommand.Learn or MapMenuCommand.Introduce =>
                        ReadPersonnelCommandEntry(state, current, command, special, BindCommandCurrent, BindCommandItems),
                    MapMenuCommand.Policy or MapMenuCommand.Office or MapMenuCommand.Research or MapMenuCommand.CreateDistrict or
                    MapMenuCommand.EditDistrict or MapMenuCommand.MoveGroup or MapMenuCommand.EditGroup =>
                        ReadAppointmentCommandEntry(state, current, command, special, BindCommandCurrent),
                    _ => throw new ArgumentOutOfRangeException(nameof(command))
                };
                entryPresentations.Add(command, entry);
            }
            return entry;
        }
        void BindCommandCurrent()
        {
            if (currentBound) return;
            BindCommandIndexes();
            _moraleWaveInputs.BindCurrent(state, current);
            _baseCapacityInputs.BindCurrent(state, current);
            _personAssignmentPrecallInputs.BindCurrent(state, current);
            PersonReturnCommands.BindCurrent(state, current);
            currentBound = true;
        }
        //Only reached producer branches bind these actual owners. A header
        //publication shares each binding; early denials do not need later data.
        void BindCommandIndexes()
        {
            if (indexesBound) return;
            //CaptureSourcePrefixCore already owns an opening index pool;
            //its existence does not mean the saved runtime graph is imported.
            _ = WorldIndexContext.ImportOwnedState(current,
                (state.ReadArmyLifecycle() ?? throw MainCommandMissing("command_indexes")).Indexes, state);
            indexesBound = true;
        }
        void BindCommandItems()
        {
            if (itemsBound) return;
            BindCommandIndexes();
            if (state.NativeArmySimulation?.Parameters.Int32Values.GetValueOrDefault(PersonArmyCaptureInputs.VersionKey) != 1)
                throw MainCommandMissing("command_Item_current_profile");
            PersonArmyCaptureInputs.BindCurrent(state, current);
            itemsBound = true;
        }
        void BindCommandAreas()
        {
            if (areasBound) return;
            BindCommandIndexes();
            AreaStatisticsSourceAdapter.BindCurrent(state, current);
            areasBound = true;
        }
        if (refresh)
        {
            var available = new Dictionary<MapMenuCommand, bool?>();
            bool? Availability(MapMenuCommand command)
            {
                if (available.TryGetValue(command, out var known)) return known;
                bool? value = Read(() =>
                {
                    if (MapMainCommandEntryPresentation.RequiresPublishedAdmission(command) &&
                        command is not (MapMenuCommand.Proposals or MapMenuCommand.CreatedEvents or MapMenuCommand.Trade))
                    {
                        var entry = EntryPresentation(command);
                        return entry.Enabled ?? throw new InvalidDataException(entry.UnresolvedInput);
                    }
                    BindCommandCurrent();
                    return ReadMainCommandAvailabilityCurrent(state, current, command);
                });
                available.Add(command, value); return value;
            }
            bool? Notice(uint mask, MapMenuCommand? command = null)
                => (flags!.Value & mask) == 0 ? false : command is { } selected ? Availability(selected) : true;
            var player = current.CurrentPlayerForce!;
            int? district = ReadDistrict();
            int? ReadDistrict()
            {
                try
                {
                    //This is the header's first actual District_D0 consumer.
                    //Its saved graph must precede memoized notice predicates.
                    BindCommandIndexes();
                    return ForcePrimaryDistrictSource.Resolve(current, player).PhysicalSlot ?? throw MainCommandMissing("primary_district");
                }
                catch (InvalidDataException error) { issues.Add(error.Message); return null; }
            }
            bool? research = MainAnd(Notice(0x08000000), Read(() => district is int d
                ? (state.Districts.GetValueOrDefault(d)?.OrdersRemaining ?? throw MainCommandMissing("research_command_books")) != 0
                : throw MainCommandMissing("research_primary_district")));
            bool? search = Notice(0x800, MapMenuCommand.Search), recruit = Notice(0x1000, MapMenuCommand.Recruit);
            bool? reward = MainAnd(Notice(0x2000, MapMenuCommand.Reward), () => Read(() =>
                district is int d ? ReadRewardCandidatesCurrent(state, current, d).Any(person => person.Loyalty < 90)
                    : throw MainCommandMissing("reward_primary_district")));
            bool? merit = Notice(0x01000000, MapMenuCommand.Merit);
            bool? goodwill = Notice(0x4000, MapMenuCommand.Goodwill), trade = Notice(0x8000, MapMenuCommand.Trade);
            bool? spy = Notice(0x04000000, MapMenuCommand.Spy);
            bool? normalEvents = state.CampaignEventEligibility?.HasAvailableResult;
            bool? editedEvents = special == true ? false : mainCommandApplicationFacts is { } app && ReferenceEquals(app.World, state)
                ? app.EditedEventsReady : null;
            if (normalEvents is null) issues.Add("map.main_command.normal_event_manager_ready");
            if (special != true && editedEvents is null) issues.Add("map.main_command.edited_event_manager_ready");
            attention[1] = MainAnd(MainNot(tutorial), MainOr(Notice(0x180000), research));
            attention[2] = MainAnd(MainNot(tutorial), MainOr(search, recruit, reward, merit));
            attention[4] = MainAnd(MainNot(tutorial), goodwill);
            attention[5] = MainAnd(MainNot(tutorial), spy);
            attention[6] = MainAnd(MainNot(tutorial), MainOr(normalEvents, trade, MainAnd(MainNot(special), editedEvents)));
            var profile = applicationProfileStore?.Current;
            bool? Seen(int bit) => profile?.ReadMainCommandSeen(bit);
            bool? Badge(int bit, MapMenuCommand? command, bool? notice)
            {
                bool? unseen = MainNot(Seen(bit));
                if (unseen is null) issues.Add("map.main_command.discovery_" + bit);
                return MainAnd(unseen, () => MainAnd(MainNot(notice),
                    () => command is { } selected ? Availability(selected) : true));
            }
            bool? badgeGate = MainAnd(MainNot(tutorial), MainNot(special));
            badges[1] = MainAnd(badgeGate, () => Badge(0, null, research));
            badges[2] = MainAnd(badgeGate, () => MainOr(Badge(3, MapMenuCommand.Award, false),
                Badge(2, MapMenuCommand.Merit, merit), Badge(1, MapMenuCommand.Employ, false)));
            badges[3] = MainAnd(badgeGate, () => Badge(11, MapMenuCommand.Rumor, false));
            badges[5] = MainAnd(badgeGate, () => MainOr(Badge(12, MapMenuCommand.Patrol, false),
                Badge(5, MapMenuCommand.Scout, false), Badge(4, MapMenuCommand.Spy, spy)));
            badges[6] = MainAnd(badgeGate, () => MainAnd(Badge(6, null, normalEvents), MainNot(attention[6])));
        }
        // 897B50 publishes Base32C/3F8 once with this header publication;
        // 892170 consumes that cached allowed/help pair when opening Decisions.
        // CampaignComposition caches the whole header by World/Force/display/revision.
        var entries = new List<MapMainCommandEntryPresentation>();
        if (refresh)
        {
            var proposal = current.CurrentPlayerForce?.PhysicalSlot is int force
                ? QueryProposalAdmission(state, force)
                : AreaAdministrationQuery<ProposalAdmission>.Unknown("map.main_command.proposal_player_identity");
            entries.Add(proposal.Available && proposal.Value is { } admission
                ? new(MapMenuCommand.Proposals, admission.Allowed, admission.GameMessageId)
                : new(MapMenuCommand.Proposals, null, null,
                    proposal.UnresolvedInput ?? "map.main_command.proposal_admission"));
            if (!proposal.Available) issues.Add(entries[0].UnresolvedInput!);
            entries.Add(ReadCreatedEventCommandEntry(state, special, tutorial));
            entries.Add(ReadForeignTradeCommandEntry(state, current, special, BindCommandCurrent));
            foreach (var command in new[] { MapMenuCommand.Spy, MapMenuCommand.Scout, MapMenuCommand.Patrol,
                MapMenuCommand.Policy, MapMenuCommand.Office, MapMenuCommand.Research, MapMenuCommand.CreateDistrict,
                MapMenuCommand.EditDistrict, MapMenuCommand.MoveGroup, MapMenuCommand.EditGroup,
                MapMenuCommand.Search, MapMenuCommand.Recruit, MapMenuCommand.Employ, MapMenuCommand.Items,
                MapMenuCommand.Reward, MapMenuCommand.Merit, MapMenuCommand.Award, MapMenuCommand.Expel,
                MapMenuCommand.Learn, MapMenuCommand.Introduce,
                MapMenuCommand.Deploy, MapMenuCommand.WithdrawAll,
                MapMenuCommand.Poison, MapMenuCommand.Alienate, MapMenuCommand.PlacateArea, MapMenuCommand.Incite,
                MapMenuCommand.ProvokeWar, MapMenuCommand.Sabotage, MapMenuCommand.Rumor,
                MapMenuCommand.Goodwill, MapMenuCommand.Alliance, MapMenuCommand.CancelAlliance,
                MapMenuCommand.ReturnCaptives, MapMenuCommand.RequestAttack, MapMenuCommand.DemandSurrender, MapMenuCommand.Exchange,
                MapMenuCommand.Events })
                entries.Add(EntryPresentation(command));
            foreach (var entry in entries.Skip(1))
                if (entry.UnresolvedInput is { } unresolved) issues.Add(unresolved);
        }
        var rows = Enumerable.Range(0, 7).Select(index =>
        {
            bool? enabled = index is 4 or 5 ? MainNot(special) : true;
            IReadOnlyList<string> missing = enabled is null || attention[index] is null || badges[index] is null
                ? issues.Distinct(StringComparer.Ordinal).ToArray() : Array.Empty<string>();
            return new MapMainCommandCategoryPresentation((MapCommandCategory)index, enabled, attention[index], badges[index], missing);
        }).ToArray();
        return new(rows, issues.Distinct(StringComparer.Ordinal).ToArray())
            { Entries = entries.AsReadOnly(), Update = refresh ? MapHeaderUpdate.Updated : valid is null || flags is null ? MapHeaderUpdate.Unresolved : MapHeaderUpdate.Unchanged };
    }

    private static bool? MainNot(bool? value) => value.HasValue ? !value.Value : null;
    private static bool? MainAnd(bool? first, bool? second) => first == false || second == false ? false :
        first == true && second == true ? true : null;
    private static bool? MainAnd(bool? first, Func<bool?> second) => first == false ? false : MainAnd(first, second());
    private static bool? MainOr(params bool?[] values) => values.Any(value => value == true) ? true :
        values.Any(value => value is null) ? null : false;
    private static InvalidDataException MainCommandMissing(string field) => new("map.main_command." + field);
}
