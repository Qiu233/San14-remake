using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record HexFireChange(int HexId, ushort Before, ushort After, bool Spread);
public sealed record HexFireSpreadTrial(int HexId, int AdjacentMaximum, int Threshold,
    int Sample, int? DurationSample, int? RequestedDuration);
public sealed record HexFireStageResult(GameState? State, HexFireChange[] Changes,
    HexFireSpreadTrial[] Trials, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Stage20 consumes saved fire durations, then spreads in source
    /// first-discovery order, including Army/Base immunity and unplayed
    /// commander requests. Nothing escapes a failed stage transaction.</summary>
    public HexFireStageResult TryAdvanceOrdinaryHexFire(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.HexFire ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("Hex fire requires reached source stage20.");
        if (!NativeArmyProgressAtomicRuntime.IsArmyWaitingDue(moment.PreWriterThirdDate)) return new(state, [], [], null);
        try
        {
            var owner = state.NativeBaseCaptureCurrent ?? throw OccupationMissing("fire_saved_durations");
            if (owner.HexWords16.Length != 48400) throw OccupationMissing("fire_duration_table");
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            var working = state;
            var changes = new List<HexFireChange>();
            var trials = new List<HexFireSpreadTrial>();
            var announcements = new List<ArmyFireAnnouncement>(state.PendingArmyFireAnnouncements);
            var reports = new List<PeriodReportEntry>(state.PendingCommandReports);
            var seeds = new List<int>();
            //2A2200 walks the complete Hex table, not just playable territory.
            for (int hexId = 0; hexId < owner.HexWords16.Length; hexId++)
            {
                ushort before = Fire(hexId);
                if (before == 0) continue;
                int reduced = Math.Max(0, unchecked(before - P(SimulationParameterKeys.HexFire.Decay)));
                if (Hex(hexId).PhysicalSlot.HasValue) Write(hexId, Math.Clamp(reduced, 0, 720), false);
                if (Fire(hexId) != 0) seeds.Add(hexId);
            }
            //2A1A20 first builds one stable, unique candidate list. New fire
            //does not add candidates, but affects later neighbor maxima.
            var candidates = new List<int>();
            var seen = new HashSet<int>();
            foreach (int seed in seeds)
                foreach (int candidate in TacticRemakeCurrentContext.NativeHexOrder(seed, 1).Skip(1))
                {
                    var surface = Surface(candidate);
                    if (surface.RawField41 == 0 && !HasArmyOccupancy(candidate)) continue;
                    if (seen.Add(candidate)) candidates.Add(candidate);
                }
            TacticSourceSamplerWorld? sampler = null;
            foreach (int candidate in candidates)
            {
                byte category = Surface(candidate).RawField41;
                int coefficient = P(category == 1 ? SimulationParameterKeys.HexFire.CategoryOneSpread :
                    category == 3 ? SimulationParameterKeys.HexFire.CategoryThreeSpread : SimulationParameterKeys.HexFire.OtherCategorySpread);
                int maximum = 0;
                foreach (int adjacent in TacticRemakeCurrentContext.NativeHexOrder(candidate, 1).Skip(1)) maximum = Math.Max(maximum, Fire(adjacent));
                int divisor = P(SimulationParameterKeys.HexFire.SpreadDivisor);
                if (divisor == 0) throw OccupationMissing("fire_zero_spread_divisor");
                int threshold = Math.Max(0, NativeTruncate(MathF.Round(((float)coefficient * maximum) / divisor, MidpointRounding.AwayFromZero)));
                sampler ??= ReadFireStageSampler(working, moment);
                var date = moment.PreWriterThirdDate;
                int sample = TacticEffectSourceSamplerMath.Sample(100, 1, date.Year + date.Month + date.Day, date.Hour, 0, 0, sampler);
                int? durationSample = null, duration = null;
                if (sample < threshold)
                {
                    durationSample = TacticEffectSourceSamplerMath.Sample(P(SimulationParameterKeys.HexFire.DurationRandomBound), 1,
                        candidate, date.Hour, 1, 0, sampler);
                    duration = NativeTruncate(MathF.Round((((float)durationSample.Value + P(SimulationParameterKeys.HexFire.DurationPercentBase)) / 100f) * maximum,
                        MidpointRounding.AwayFromZero));
                    working = IgniteCurrentHex(working, current, candidate, duration.Value, false, moment, changes, announcements, reports);
                }
                trials.Add(new(candidate, maximum, threshold, sample, durationSample, duration));
            }
            return new(BaseCaptureCurrentInputs.PublishCurrent(working, current) with
            { PendingArmyFireAnnouncements = announcements.ToArray(), PendingCommandReports = reports.ToArray() },
                changes.ToArray(), trials.ToArray(), null);

            ushort Fire(int hexId) => OccupationKnown(current.ReadCaptureHexWord16(hexId), "fire_current_duration");
            HexReference Hex(int hexId) => current.ReadHexTableReference(hexId) ?? throw OccupationMissing("fire_Hex_pointer");
            S14TerrainTypeDefinition Surface(int hexId)
            {
                int code = OccupationKnown(current.ReadHexSurfaceCode(Hex(hexId)), "fire_Hex_terrain");
                int physical = OccupationKnown(current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0)?.PhysicalSlot, "fire_terrain_pointer");
                return _terrain.Catalog.Types[physical]; // Valid nonnull CTerrainData includes slot0.
            }
            bool HasArmyOccupancy(int hexId)
            {
                if (!Hex(hexId).PhysicalSlot.HasValue) return false;
                int occupant = unchecked((short)OccupationKnown(current.ReadHexRaw1C(Hex(hexId)), "fire_occupant"));
                return occupant is >= 1 and <= 500;
            }
            void Write(int hexId, int duration, bool spread)
            {
                ushort before = Fire(hexId), after = (ushort)duration;
                current.SetCaptureHexWord16(hexId, after);
                changes.Add(new(hexId, before, after, spread));
            }
            int P(string key) => working.NativeArmySimulation?.Parameters is { } parameters &&
                !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value)
                ? value : throw OccupationMissing(key);
        }
        catch (InvalidDataException error) { return new(null, [], [], error.Message); }
    }

    private GameState IgniteCurrentHex(GameState state, SourceWorldContext current, int hexId, int duration,
        bool commandFire, NativeArmyProgressStageMoment moment, List<HexFireChange> changes,
        List<ArmyFireAnnouncement> announcements, List<PeriodReportEntry> reports)
        => IgniteCurrentHex(state, current, hexId, duration, commandFire,
            latest => ReadFireStageSampler(latest, moment).Day450, moment.CalendarCycleOrdinal,
            changes, announcements);

    //The real2E3CB0 body is also called by automatic tactics outside the
    //scheduled fire stages. Its writer takes the actual day getter and sink;
    //no invented scheduler moment or second world is needed by that caller.
    private GameState IgniteCurrentHex(GameState state, SourceWorldContext current, int hexId, int duration,
        bool commandFire, Func<GameState, int> readDay, long revision, List<HexFireChange> changes,
        List<ArmyFireAnnouncement> announcements, List<CampaignEffectRequest>? feedback = null)
    {
        var latest = state;
        Apply();
        return latest;
        ushort Fire(int id) => OccupationKnown(current.ReadCaptureHexWord16(id), "fire_current_duration");
        HexReference Hex(int id) => current.ReadHexTableReference(id) ?? throw OccupationMissing("fire_Hex_pointer");
        S14TerrainTypeDefinition Surface(int id)
        {
            int code = OccupationKnown(current.ReadHexSurfaceCode(Hex(id)), "fire_Hex_terrain");
            int physical = OccupationKnown(current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0)?.PhysicalSlot, "fire_terrain_pointer");
            return _terrain.Catalog.Types[physical];
        }
        void Write(int id, int value, bool spread)
        {
            ushort before = Fire(id);
            current.SetCaptureHexWord16(id, (ushort)value);
            changes.Add(new(id, before, (ushort)value, spread));
        }
        void Apply()
        {
            var originalHex = Hex(hexId);
            if (!originalHex.PhysicalSlot.HasValue) return;
            var terrain = Surface(hexId);
            //2E3CB0 checks the current Army and Base protection branches
            //before the terrain/old-fire gates. Do not skip those reads.
            int raw = unchecked((short)OccupationKnown(current.ReadHexRaw1C(Hex(hexId)), "fire_ignition_occupant"));
            if (raw < 0) throw OccupationMissing("fire_negative_Army_lookup");
            var army = current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0) ?? throw OccupationMissing("fire_Army_pointer");
            bool validArmy = OccupationKnown(current.ReadArmyUnitNativeValidity(army), "fire_Army_validity");
            //22A9E0/22D0D0 use Hex1A directly, with their actual City0/
            //Gate0 fallback. They do not consult the generic Object table.
            int link = OccupationKnown(current.ReadHexObjectLink(Hex(hexId)), "fire_Base_link");
            var city = current.ReadCityTableReference(link <= 51 ? link : 0) ?? throw OccupationMissing("fire_City_pointer");
            bool basePresent = OccupationKnown(current.ReadBaseNativeValidity(city), "fire_City_validity");
            BaseReference target = city;
            if (!basePresent)
            {
                var gate = current.ReadGateTableReference(link is >= 52 and <= 61 ? link - 51 : 0) ?? throw OccupationMissing("fire_Gate_pointer");
                basePresent = OccupationKnown(current.ReadBaseNativeValidity(gate), "fire_Gate_validity");
                target = gate;
            }
            int? unitId = null;
            int officerId = 0;
            if (validArmy)
            {
                int physical = OccupationKnown(army.PhysicalSlot, "fire_Army_receiver");
                if (OccupationKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(latest, current,
                    physical), "fire_Army_excluded")) return;
                unitId = ArmyDestructionQueue.ResolveUnitId(latest, physical) ?? throw OccupationMissing("fire_Army_owner");
                int person = Person(OccupationKnown(current.ReadArmyUnitCommanderId(army), "fire_commander"));
                officerId = OccupationKnown(current.ReadPersonIdentity(person), "fire_commander_identity");
                if (OccupationKnown(current.ReadPersonNativeValidity(person), "fire_commander_validity") &&
                    FirstImmunity(person) is int trait)
                {
                    ImmunityReport(trait, army, null, originalHex);
                    announcements.Add(new(unitId.Value, officerId, trait));
                    return;
                }
            }
            else if (basePresent)
            {
                int location = OccupationKnown(current.ReadBaseObjectLink(target), "fire_Base_location");
                if (OccupationKnown(OwnedArmyDestructionCurrent.ReadBaseExcluded2334B0(latest, current, location), "fire_Base_excluded")) return;
                int person = Person(OccupationKnown(current.ReadArmyReturnBaseGovernor2E(target), "fire_governor"));
                if (OccupationKnown(current.ReadPersonNativeValidity(person), "fire_governor_validity"))
                {
                    //304B50 retains base locations1..61 and returns0 for
                    //Army/Area positions; this is not the full location decoder.
                    int position = OccupationKnown(current.ReadPersonSecondLocation(person), "fire_governor_position");
                    bool present = position is >= 1 and <= 61 &&
                        position == OccupationKnown(current.ReadPersonFirstLocation(person), "fire_governor_home");
                    // The source selects the first matching trait even if
                    // the governor is absent. It never tries later matches.
                    int? trait = FirstImmunity(person, present);
                    if (present && trait is int immunity)
                    {
                        ImmunityReport(immunity, null, target, originalHex);
                        return;
                    }
                }
            }
            if (terrain.RawField2A == 0 || Fire(hexId) != 0 ||
                terrain.RawField41 == 0 && ((terrain.RawField2C & 2) == 0 || !validArmy)) return;
            Write(hexId, Math.Clamp(duration, 0, 720), true);
            if (unitId is int burnedUnit) announcements.Add(new(burnedUnit, officerId, null) { FromFireCommand = commandFire });
        }
        int Person(int id) => OccupationKnown(current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot, "fire_Person_pointer");
        int? FirstImmunity(int person, bool testCondition = true)
        {
            for (int i = 0; i < 9; i++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, i)
                    ?? throw OccupationMissing("fire_Person_trait");
                if (!OccupationKnown(current.ReadSkillPersonalityValidity(trait), "fire_trait_validity")) continue;
                if (OccupationKnown(current.ReadPersonalityByte(trait, 0xB6), "fire_trait_effect1") != 25 &&
                    OccupationKnown(current.ReadPersonalityByte(trait, 0xBA), "fire_trait_effect2") != 25) continue;
                return testCondition && OccupationKnown(current.ReadPersonalityWord(trait, 0xB4), "fire_trait_condition") == ushort.MaxValue
                    ? OccupationKnown(trait.PhysicalSlot, "fire_trait_identity") : null;
            }
            return null;
        }
        void ImmunityReport(int trait, ArmyUnitReference? reportArmy, BaseReference? reportBase, HexReference originalHex)
        {
            //Materialize the main text before reading writer metadata. The
            //Base operand is its actual4E, not a later campaign location ID.
            string traitName = current.ReadPersonalityName(new(trait));
            ushort? baseOperand = reportBase is null ? null :
                OccupationKnown(current.ReadBaseObjectLink(reportBase), "fire_report_Base4E");
            var formatted = FormatHistoryReport(latest, current, reportArmy is not null ? 0x3239 : 0x323A,
                new HistoryMessageOperands(Army0: reportArmy, Base0: baseOperand, String0: traitName));
            latest = formatted.State;
            int day = readDay(latest);
            int reportHex = OccupationKnown(current.ReadHexPhysicalSlot(originalHex), "fire_report_original_Hex22DEF0");
            int district;
            if (reportArmy is not null)
            {
                int commander = Person(OccupationKnown(current.ReadArmyUnitCommanderId(reportArmy), "fire_report_Commander12"));
                district = OccupationKnown(current.ReadPersonNativeValidity(commander), "fire_report_Commander_validity")
                    ? OccupationKnown(current.ReadPersonDistrictSlot(commander), "fire_report_Army_District22BC70") : 0;
            }
            else district = OccupationKnown(current.ReadBaseDistrictSlot(reportBase!), "fire_report_Base_virtual78");
            var effects = new List<CampaignEffectRequest>();
            latest = WriteHistory(latest, current, new ReportCommand(0, district, reportHex, day, formatted.Text,
                CampaignHistoryWriter.Supplemental(latest), 5012, MarkBit4: 1), effects);
            if (effects.Count != 0)
            {
                if (feedback is not null) feedback.AddRange(effects);
                else latest = latest with { CampaignEffectRequests =
                    (latest.CampaignEffectRequests ?? throw OccupationMissing("fire_history_pending_owner"))
                    .Append(CampaignEffectSource.ArmyProgressNotifications, revision, effects) };
            }
            //The old semantic PeriodReportEntry is no longer emitted for
            //this native log. The ring and source-time notification own it.
        }
    }

    private static TacticSourceSamplerWorld ReadFireStageSampler(GameState state, NativeArmyProgressStageMoment moment)
    {
        var season = state.NativeMapSeason ?? throw OccupationMissing("fire_saved_calendar");
        var clock = state.NativeClock ?? throw OccupationMissing("fire_current_clock");
        var epoch = state.NativeArmySimulation?.World.SamplerEpoch ?? throw OccupationMissing("fire_sampler_epoch");
        if (moment.CycleWithinAtomicBlock is < 0 or >= NativeArmyProgressAtomicRuntime.CyclesPerSaveBlock ||
            moment.CalendarCycleOrdinal != checked(season.AdvancedCalendarSteps + moment.CycleWithinAtomicBlock) ||
            clock.UpdateOrdinal != checked(season.ProcessedNativeOrdinal + 1) ||
            clock.Revision != season.NativeClockRevision || epoch.NativeClockRevision != clock.Revision ||
            epoch.SourceScenarioUri != season.SourceScenarioUri || epoch.SourceScenarioSha256 != season.SourceScenarioSha256 ||
            epoch.SourceWorldSha256 != season.SourceWorldSha256 ||
            state.ContentHashes.GetValueOrDefault(epoch.SourceScenarioUri) != epoch.SourceScenarioSha256)
            throw OccupationMissing("fire_reached_sampler_clock");
        MapSeasonRuntime.ValidateOwned(season);
        var date = season.ThirdDate;
        long days = season.AdvancedDays;
        for (int i = 0; i < moment.CycleWithinAtomicBlock; i++)
        {
            var advanced = MapSeasonRuntime.StepCalendarWriter(date);
            date = advanced.Date;
            if (advanced.DayAdvanced) days++;
        }
        if (date != moment.PreWriterThirdDate) throw OccupationMissing("fire_reached_sampler_date");
        return new(unchecked(epoch.Day450 + (int)days), epoch.Salt45C);
    }
}
