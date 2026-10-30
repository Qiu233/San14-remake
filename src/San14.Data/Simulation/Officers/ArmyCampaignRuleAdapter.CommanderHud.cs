using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : ICommanderAutomaticTacticsCommandRules
{
    /// <summary>One capture for all five selected commanders. The caller owns
    /// either the committed world or an actually stopped playback checkpoint.</summary>
    public CommanderHudPublication QueryCommanderHud(GameState state, int forceId, bool progress)
    {
        ArgumentNullException.ThrowIfNull(state);
        var rows = new List<CommanderHudCard>(5);
        int? capacity = state.CommanderSelection?.ActiveCount;
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var cards = QueryCommanderSelectionCurrent(state, forceId, false, current);
            if (cards.UnresolvedOwner is string missing) throw CommanderHudMissing(missing);
            capacity = cards.Capacity ?? throw CommanderHudMissing("capacity");
            var slots = state.ReadArmyLifecycle()?.ReceiverSlots() ?? throw CommanderHudMissing("Army_arena");
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
            var tacticRules = Context(current);
            int period = ReadCommanderSelectionPeriod(state);
            if (state.NativeArmySimulation?.Parameters.Int32Values is not { } parameters ||
                !parameters.TryGetValue(SimulationParameterKeys.Tactics.ManualInterventionMinimumPeriods, out int minimum))
                throw CommanderHudMissing("waiting_minimum");
            for (int ordinal = 0; ordinal < 5; ordinal++)
            {
                if (ordinal >= cards.Cards.Count)
                {
                    bool locked = ordinal >= capacity;
                    var issues = new Dictionary<string, string>();
                    int? rank = null; string? text = null;
                    if (locked)
                    {
                        try
                        {
                            rank = ReadCommanderHudUnlockRank(state, ordinal);
                            string name = _scenario.Snapshot.RankParameters!.Ranks[rank.Value].Name;
                            text = FormatCommanderHudOperand(ReadHistoryDataText(1, 0x617), name, 's');
                        }
                        catch (InvalidDataException error) { issues["unlock"] = error.Message; }
                    }
                    rows.Add(new(ordinal, null, "", locked ? CommanderHudCardState.Locked : CommanderHudCardState.Empty)
                    { UnlockRankId = rank, UnlockText = text, UnresolvedFields = new ReadOnlyDictionary<string, string>(issues) });
                    continue;
                }
                var card = cards.Cards[ordinal];
                rows.Add(CaptureCard(ordinal, card));
            }
            return new(state, forceId, capacity, rows.AsReadOnly(), null);

            CommanderHudCard CaptureCard(int ordinal, CommanderSelectionCard card)
            {
                var issues = new Dictionary<string, string>();
                int person = current.ResolveOfficerSource(card.OfficerId) ?? throw CommanderHudMissing("Person_pointer");
                if (!CommanderHudNeed(current.ReadPersonNativeValidity(person), "Person_validity"))
                    throw CommanderHudMissing("selected_Person_retired");
                var army = PersonArmyLocationSource.ReadCommanderArmy(current, person)
                    ?? throw CommanderHudMissing("Person_Army_pointer");
                bool armyValid = CommanderHudNeed(current.ReadArmyUnitNativeValidity(army), "Army_validity");
                int previous = CommanderHudNeed(card.SelectedAtPeriod, "selected_at_period");
                bool waiting = unchecked(period - previous) < minimum;
                var status = armyValid ? waiting ? CommanderHudCardState.WaitingArmy : CommanderHudCardState.ReadyArmy
                    : waiting ? CommanderHudCardState.WaitingWithoutArmy : CommanderHudCardState.ReadyWithoutArmy;
                bool? automatic = Value("automatic", () =>
                    (CommanderHudNeed(current.ReadArmyReturnPersonFlags196(person), "Person_flags") & 0x20) != 0);
                int? days = waiting ? Value("waiting_days", () =>
                {
                    int day = progress ? (tacticRules.ReadCurrentSamplerWorld(state)
                        ?? throw CommanderHudMissing("current_day")).Day450 % 10 : 0;
                    return unchecked((previous - period + minimum) * 10 - day);
                }) : null;
                string? text = Reference("status_text", () => status switch
                {
                    CommanderHudCardState.ReadyArmy => "",
                    CommanderHudCardState.ReadyWithoutArmy => ReadHistoryDataText(1, 0x357),
                    CommanderHudCardState.WaitingArmy => WaitingText(),
                    _ => ReadHistoryDataText(1, 0x357) + "\n" + WaitingText()
                });
                string WaitingText() => FormatCommanderHudOperand(ReadHistoryDataText(1, 0x35A),
                    CommanderHudNeed(days, "waiting_days").ToString(CultureInfo.InvariantCulture), 'd');
                UnitState? unit = null;
                if (armyValid)
                {
                    int physical = CommanderHudNeed(army.PhysicalSlot, "Army_receiver");
                    if ((uint)physical >= slots.Length || slots[physical].ActiveUnitId is not int id ||
                        !state.Units.TryGetValue(id, out unit)) throw CommanderHudMissing("active_Army_image");
                }
                int? troops = unit is null ? null : Value("troops", () => unit.Troops is >= 0 and <= ushort.MaxValue
                    ? unit.Troops : throw CommanderHudMissing("Army_ready_troops"));
                int? supply = unit is null ? null : Value("supply_palette", () => ReadUnitDetailSupplyPalette(state, current, unit));
                int? upper = unit is null ? null : Value("morale_maximum", () => BasePostLoad.ReadMoraleUpper(
                    current.ReadArmyUnitForceReference(army) ?? throw CommanderHudMissing("Army_Force"), current));
                int? morale = unit is null ? null : Value("morale", () => Math.Min(
                    CommanderHudNeed(ArmyMoraleSourceAdapter.Read(unit), "Army_morale"), CommanderHudNeed(upper, "morale_maximum")));
                byte? marker = unit is null ? null : Value("map_marker", () => CommanderHudNeed(unit.NativeMapUiByte13C ??
                    slots[CommanderHudNeed(army.PhysicalSlot, "Army_receiver")].MapUiByte13C, "Army_map_marker"));
                ArmyMapIconSelection? icon = unit is null ? null : Value("formation_icon", () =>
                    ReadCommanderHudFormation(state, tacticRules, unit, CommanderHudNeed(marker, "Army_map_marker")));
                IReadOnlyList<int?> frames = unit is null ? new int?[] { 0, 0, 0 } :
                    Reference("waiting_gauges", () => ReadCommanderHudGaugeFrames(state, tacticRules, unit)) ?? new int?[] { null, null, null };
                IReadOnlyList<CommanderHudTacticSlot> tactics = status != CommanderHudCardState.ReadyArmy ? [] :
                    Reference("tactics", () =>
                    {
                        var equipped = ReadManualCommandEquippedTacticsCurrent(state, current, unit!);
                        return (IReadOnlyList<CommanderHudTacticSlot>)Enumerable.Range(0, 3).Select(slot =>
                        {
                            if (slot >= equipped.Count) return new CommanderHudTacticSlot(slot, null, null, false, null);
                            int id = equipped[slot];
                            string name = state.Tactics.GetValueOrDefault(id)?.Name ?? throw CommanderHudMissing("tactic_name");
                            bool? enabled = progress ? tacticRules.ReadManualInterventionEligibility(state, unit!, id, period) : false;
                            return new CommanderHudTacticSlot(slot, id, name, enabled, enabled is null ? "manual_tactic.command_eligibility" : null);
                        }).ToArray();
                    }) ?? [];
                return new(ordinal, card.OfficerId, card.Name, status)
                {
                    SelectedAtPeriod = previous, UnitId = unit?.Id, PortraitAppearanceIndex = card.PortraitAppearanceIndex,
                    HexId = card.HexId, Troops = troops, SupplyPalette = supply, Morale = morale, MoraleMaximum = upper,
                    MapMarker = marker, FormationIcon = icon, GaugeFrames = frames, AutomaticEnabled = automatic,
                    CanRemove = !progress && state.Phase == TurnPhase.Strategy, Tactics = tactics, WaitingDays = days, StatusText = text,
                    UnresolvedFields = new ReadOnlyDictionary<string, string>(issues)
                };
                T? Value<T>(string field, Func<T> read) where T : struct
                { try { return read(); } catch (InvalidDataException error) { issues[field] = error.Message; return null; } }
                T? Reference<T>(string field, Func<T> read) where T : class
                { try { return read(); } catch (InvalidDataException error) { issues[field] = error.Message; return null; } }
            }
        }
        catch (InvalidDataException error) { return new(state, forceId, capacity, rows.AsReadOnly(), error.Message); }
    }

    private int ReadCommanderHudUnlockRank(GameState state, int ordinal)
    {
        var ranks = _scenario.Snapshot.RankParameters?.Ranks ?? throw CommanderHudMissing("Rank_table");
        if (ranks.Count != 13) throw CommanderHudMissing("Rank_table_count");
        int selected = 0;
        byte best = CommanderHudNeed(OwnedArmyMoraleWaveInputs.ReadRankAC(state, 0), "Rank0_AC");
        for (int index = 0; index < 13; index++)
        {
            byte[] raw = Convert.FromHexString(ranks[index].RawBytesHex);
            if (raw.Length < 164 || ranks[index].Id != index) throw CommanderHudMissing("Rank_B3");
            byte ac = CommanderHudNeed(OwnedArmyMoraleWaveInputs.ReadRankAC(state, index), "Rank_AC");
            if (raw[163] == ordinal + 1 && ac > best) { selected = index; best = ac; }
        }
        return selected;
    }

    private static IReadOnlyList<int?> ReadCommanderHudGaugeFrames(GameState state, TacticRemakeCurrentContext rules, UnitState unit)
    {
        var native = unit.NativeTacticRuntime ?? throw CommanderHudMissing("Army_waiting");
        int[] caps = Enumerable.Range(0, 3).Select(index =>
            CommanderHudNeed(rules.ReadWaitingUpperBound(state, unit, index), "waiting_capacity")).ToArray();
        if (caps.Any(value => value <= 0)) throw CommanderHudMissing("positive_waiting_capacity");
        int remaining = unchecked(caps.Sum() - native.Waiting20 - native.Waiting21 - native.Waiting22);
        int current = 0;
        while (current < 3)
        {
            int next = remaining - caps[current];
            if (next < 1) break;
            remaining = next; current++;
        }
        remaining = Math.Max(remaining, 0);
        int denominator = caps[current == 3 ? 0 : current]; //2314A0 falls back to its first cap.
        return Enumerable.Range(0, 3).Select(index => (int?)(index < current ? 100 : index > current ? 0 :
            (int)((float)remaining / denominator * 100.0f))).ToArray();
    }

    private static ArmyMapIconSelection ReadCommanderHudFormation(GameState state, TacticRemakeCurrentContext rules,
        UnitState unit, byte marker)
    {
        if (marker != 0) return new(0x1F0, 9);
        int formation = CommanderHudNeed(rules.EffectiveFormation(state, unit), "effective_formation");
        byte kind = CommanderHudNeed(TacticRemakeCurrentFormations.ReadRawByte(state,
            formation <= 20 ? formation : 0, 0x7E), "formation_class");
        return ArmyMapUiSources.FormationIconWithoutOutline((byte)formation, kind);
    }

    private static string FormatCommanderHudOperand(string format, string value, char kind)
    {
        var output = new StringBuilder(); bool consumed = false;
        for (int index = 0; index < format.Length; index++)
        {
            if (format[index] != '%') { output.Append(format[index]); continue; }
            if (++index >= format.Length) throw CommanderHudMissing("message_format");
            if (format[index] == '%') { output.Append('%'); continue; }
            if (format[index] != kind || consumed) throw CommanderHudMissing("message_operand");
            output.Append(value); consumed = true;
        }
        return consumed ? output.ToString() : throw CommanderHudMissing("message_operand_count");
    }
    private static T CommanderHudNeed<T>(T? value, string field) where T : struct => value ?? throw CommanderHudMissing(field);
    private static InvalidDataException CommanderHudMissing(string field) => new("commander-hud." + field);
}
