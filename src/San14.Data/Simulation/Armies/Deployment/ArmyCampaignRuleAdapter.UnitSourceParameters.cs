using System.Collections.ObjectModel;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One current capture for the parameter page's ordered tactic
    /// names and effect descriptions. Selecting a returned row needs no new
    /// query. The existing army and the selected formation are never changed.</summary>
    public UnitSourceParameters QueryUnitSourceParameters(GameState state, int unitId, bool? landSelected = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var missing = new Dictionary<string, string>();
        IReadOnlyDictionary<string, string> Missing() => new ReadOnlyDictionary<string, string>(missing);
        if (!state.Units.TryGetValue(unitId, out var unit))
            return new(unitId, landSelected, [], null, null, false, Missing());

        SourceWorldContext? current = null;
        IReadOnlyList<MessageRecord>? descriptions = null;
        string? captureFailure = null;
        try
        {
            current = CaptureOfficerCommandCapacityCurrent(state);
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
        }
        catch (InvalidDataException error) { current = null; captureFailure = error.Message; }
        SourceWorldContext Current() => current ?? throw new InvalidDataException(captureFailure!);
        T Need<T>(T? value, string field) where T : struct => value ?? throw ParameterDetailsMissing(field);
        int Formation(bool land) => Need(land ? ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit)
            : ArmyConfigurationSourceAdapter.ReadNaval(state, unit), "current_formation");

        if (landSelected is null)
        {
            try
            {
                int currentHex = Need(ArmyMarchSourceAdapter.ReadCurrentHex(state, unit), "current_hex");
                var hex = Current().ReadHexTableReference(currentHex) ?? throw ParameterDetailsMissing("hex_pointer");
                int code = Need(Current().ReadHexSurfaceCode(hex), "hex_surface");
                bool naval = (_terrain.Catalog.Types[code <= 30 ? code : 0].RawField2C & 2) != 0;
                int effective = Formation(!naval);
                byte kind = Need(TacticRemakeCurrentFormations.ReadRawByte(state, effective <= 20 ? effective : 0, 0x7E), "current_formation_kind");
                landSelected = kind != 2;
            }
            catch (InvalidDataException error) { missing["formation.selection"] = error.Message; }
        }
        IReadOnlyList<UnitSourceTactic>? tactics = null;
        try
        {
            var context = Current();
            if (state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != Seed.Language ||
                parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
                throw ParameterDetailsMissing("rule_profile");
            bool land = landSelected ?? throw ParameterDetailsMissing("formation_selection");
            int selected = Formation(land);
            if (selected is < 1 or > 20 || !state.Formations.ContainsKey(selected))
                throw ParameterDetailsMissing("selected_formation_validity");
            var references = new List<TacticReference>(3);
            if (land)
            {
                if (unit.TacticIds is not { Length: 3 } slots) throw ParameterDetailsMissing("three_army_tactic_slots");
                foreach (int requested in slots)
                {
                    if (requested < 0) throw ParameterDetailsMissing("negative_tactic_identity");
                    var tactic = context.ReadSkillTacticReference(requested < 201 ? requested : 0)
                        ?? throw ParameterDetailsMissing("tactic_pointer");
                    if (Need(context.ReadSkillTacticValidity(tactic), "tactic_validity")) references.Add(tactic);
                }
            }
            else
            {
                var person = unit.OfficerId is >= 0 and <= 6000 ? context.ReadPersonLookupReference(unit.OfficerId)
                    : context.ReadTransferPersonTableReference(0);
                int physical = person?.PhysicalSlot ?? throw ParameterDetailsMissing("commander_pointer");
                int identity = Need(context.ReadPersonIdentity(physical), "commander_identity");
                int naval = Formation(false);
                for (int index = 0; index < 10 && references.Count < 3; index++)
                {
                    var tactic = DeploymentTacticSelectionSource.ReadCurrentSlot(context, identity, physical, index);
                    if (!Need(context.ReadSkillTacticValidity(tactic), "tactic_validity")) continue;
                    uint mask = 0;
                    for (int part = 0; part < 4; part++)
                        mask |= (uint)Need(context.ReadSkillTacticRawByte(tactic, 0x6C + part), "naval_tactic_mask") << (part * 8);
                    if (naval < 21 && (mask & (1u << naval)) != 0) references.Add(tactic);
                }
            }
            // Resolve list identity/name before publishing it. Only individual
            // effect text is allowed to remain unknown inside a complete list.
            var rows = new List<UnitSourceTactic>(references.Count);
            foreach (var tactic in references)
            {
                int id = Need(context.ReadSkillTacticPhysicalSlot(tactic), "tactic_identity");
                string name = state.Tactics.GetValueOrDefault(id)?.Name ?? throw ParameterDetailsMissing("tactic_name");
                string? effects = null, effectsMissing = null;
                try
                {
                    byte first = context.ReadTacticEffectOpcode(tactic, 0), second = context.ReadTacticEffectOpcode(tactic, 1);
                    effects = Description(first) + "\n" + Description(second);
                }
                catch (InvalidDataException error) { effectsMissing = error.Message; }
                rows.Add(new(id, name, effects, effectsMissing));
            }
            tactics = rows.AsReadOnly();
        }
        catch (InvalidDataException error) { missing["tactics"] = error.Message; }

        int? waiting = unit.NativeTacticRuntime?.Waiting20;
        if (waiting is null) missing["waiting.counter"] = "unit.parameters.current_waiting_counter";
        bool? visible = tactics is null ? null : tactics.Count != 0;
        // The display divisor is a separate original input. Existing waiting
        // update-rate/calendar constants do not establish that display value.
        if (visible != false) missing["waiting.days"] = "unit.parameters.waiting_display_divisor";
        missing["waiting.gauge"] = "unit.parameters.waiting_gauge_fraction";
        return new(unitId, landSelected, tactics, waiting, null, visible, Missing());

        string Description(byte opcode)
        {
            if (opcode is < 1 or > 23) return "";
            if (descriptions is null)
            {
                var sections = _repository.LoadMessages(Seed.Language, MessageKind.Data).Messages.Sections;
                if (sections.Count == 0) throw ParameterDetailsMissing("effect_message_section");
                descriptions = sections[0].Records;
            }
            int index = opcode + 0x151;
            if ((uint)index >= (uint)descriptions.Count) throw ParameterDetailsMissing("effect_message_index");
            return descriptions[index].Text ?? throw ParameterDetailsMissing("effect_description");
        }
    }
    private static InvalidDataException ParameterDetailsMissing(string field) => new("unit.parameters." + field);
}
