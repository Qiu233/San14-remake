using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class ArmyInformationCurrent
    {
        private readonly Dictionary<(int UnitId, bool Land), UnitSourceAxes> formationAxes = [];
        private readonly Dictionary<int, IReadOnlyList<int>> currentTacticLists = [];
        private TacticRemakeCurrentAbilities? formationAbilities;
        private bool formationInputsBound;
        private bool tacticInputsBound;

        private PreparedCell FormationCell(Unit unit, int column)
        {
            bool land = column < 23;
            int field = column - (land ? 15 : 23);
            return field switch
            {
                0 => MakeCell(() => FormationDefinition(unit, land).Name,
                    () => new NameKey(FormationDefinition(unit, land).Pinyin)),
                1 => MakeCell(() => FormationTroopCategory(unit, land).ToString(CultureInfo.InvariantCulture),
                    () => new NumberKey(FormationTroopCategory(unit, land))),
                >= 2 and <= 6 => MakeCell(() => FormationAxis(unit, land, field).ToString(CultureInfo.InvariantCulture),
                    () => new NumberKey(FormationAxis(unit, land, field))),
                7 => MakeCell(() => rules.UnitDetailData(1, FormationExtendedRange(unit, land) ? 0x338 : 0x339),
                    () => new NumberKey(FormationExtendedRange(unit, land) ? 1 : 0)),
                _ => throw FormationMissing("column")
            };
        }

        private PreparedCell TacticCell(Unit unit, int column)
        {
            int index = column - 31;
            int? Id()
            {
                var list = CurrentArmyTactics(unit);
                return (uint)index < (uint)list.Count ? list[index] : null;
            }
            return MakeCell(() => Id() is int id
                    ? state.Tactics.GetValueOrDefault(id)?.Name ?? throw FormationMissing("tactic_name") : "",
                () => Id() is int id ? new NameKey(rules._skills.Formations.Tactics[id].Pinyin) : new NameKey("", true));
        }

        private int SelectedFormation(Unit unit, bool land)
        {
            int raw = FormationNeed(land ? ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit.Owner)
                : ArmyConfigurationSourceAdapter.ReadNaval(state, unit.Owner), "selected_formation");
            var pointer = Current.ReadStaticTableReference(0x77428, raw <= 20 ? raw : 0)
                ?? throw FormationMissing("formation_pointer");
            return FormationNeed(pointer.PhysicalSlot, "formation_receiver");
        }

        private Formats.S14FormationDefinition FormationDefinition(Unit unit, bool land) =>
            rules._skills.Formations.Formations[SelectedFormation(unit, land)];

        private int FormationPerson(Unit unit)
        {
            var pointer = unit.Owner.OfficerId is >= 0 and <= 6000
                ? Current.ReadPersonLookupReference(unit.Owner.OfficerId) : Current.ReadTransferPersonTableReference(0);
            return FormationNeed(pointer?.PhysicalSlot, "commander_pointer");
        }

        private int FormationTroopCategory(Unit unit, bool land)
        {
            int selected = SelectedFormation(unit, land), person = FormationPerson(unit);
            //2B4F00 leaves both caller-initialized thresholds zero for either
            //invalid receiver; the list consequently publishes category1.
            if (selected == 0 || !FormationNeed(Current.ReadPersonNativeValidity(person), "commander_validity")) return 1;
            var classification = DeploymentTroopClassificationSource.Read(state, Current, person, selected, unit.Owner.Troops);
            return classification.Marker switch
            {
                DeploymentTroopMarker.AtSecondaryThreshold => 5,
                DeploymentTroopMarker.Default => 3,
                _ => 1
            };
        }

        private bool FormationExtendedRange(Unit unit, bool land)
        {
            int selected = SelectedFormation(unit, land);
            byte bits = FormationNeed(TacticRemakeCurrentFormations.ReadParameter(state, selected, 0xA6), "formation_range_bits");
            //2C70A0 reads Force193 even when this is not formation10.
            var commander = Current.ReadPersonForce(FormationPerson(unit)) ?? throw FormationMissing("force_pointer");
            int force = FormationNeed(commander.PhysicalSlot, "force_receiver");
            byte flags = FormationNeed(PlaceholderWorldCurrentSource.ReadForceAdministrationFlags(state, force), "force_administration_flags");
            return (bits & 1) != 0 || flags >= 0x80 && selected == 10;
        }

        private int FormationAxis(Unit unit, bool land, int field)
        {
            if (!formationAxes.TryGetValue((unit.Id, land), out var axes))
            {
                BindFormationInputs();
                axes = rules.ReadUnitSourceAxesCurrent(state, unit.Id, land, Current, Rules,
                    formationAbilities, null, listPreview: true, conditionalModifiers: true);
                formationAxes[(unit.Id, land)] = axes;
            }
            var values = axes.WithConditionalModifiers;
            (int? value, string key) = field switch
            {
                2 => (values.Mobility, "mobility"),
                3 => (values.FieldAttack, "field"),
                4 => (values.SiegeTroopAttack, "siege"),
                5 => (values.WallAttack, "wall"),
                6 => (values.Defense, "defense"),
                _ => throw FormationMissing("axis_column")
            };
            if (value.HasValue) return value.Value;
            throw new InvalidDataException(axes.Missing.GetValueOrDefault("with." + key)
                ?? axes.Missing.GetValueOrDefault("with.common") ?? FormationMissing("axis_current").Message);
        }

        private void BindFormationInputs()
        {
            if (formationInputsBound) return;
            var parameters = state.NativeArmySimulation?.Parameters;
            if (parameters is null || parameters.Language != rules.Seed.Language ||
                parameters.RuleVersion != SimulationRuleIdentity.ForLocale(rules.Seed.Language))
                throw FormationMissing("rule_profile");
            rules._personRelationInputs.BindCurrent(state, Current);
            formationAbilities = new(Current, rules._scenario, rules.Seed.Language, parameters.RuleVersion);
            formationInputsBound = true;
        }

        private IReadOnlyList<int> CurrentArmyTactics(Unit unit)
        {
            if (currentTacticLists.TryGetValue(unit.Id, out var saved)) return saved;
            //2ADFF0 ->2AE180 uses the actual Hex terrain, independently of
            //either of the list's two formation previews. Source duplicates
            //and source slot order are retained.
            var list = new List<int>(3);
            if (FormationNeed(Current.ReadArmyUnitNativeValidity(new(unit.PhysicalSlot)), "army_validity"))
            {
                int raw = FormationNeed(ArmyMarchSourceAdapter.ReadCurrentHex(state, unit.Owner), "current_hex");
                var hex = Current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw FormationMissing("hex_pointer");
                int code = hex.PhysicalSlot.HasValue ? FormationNeed(Current.ReadHexSurfaceCode(hex), "hex_surface") : 0;
                var terrain = Current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw FormationMissing("terrain_pointer");
                if (terrain.PhysicalSlot is int surface)
                {
                    bool naval = (rules._terrain.Catalog.Types[surface].RawField2C & 2) != 0;
                    if (!naval)
                    {
                        if (unit.Owner.TacticIds is not { Length: 3 } slots) throw FormationMissing("three_army_tactic_slots");
                        foreach (int requested in slots)
                        {
                            if (requested < 0) throw FormationMissing("negative_tactic_identity");
                            var tactic = Current.ReadSkillTacticReference(requested <= 200 ? requested : 0) ?? throw FormationMissing("tactic_pointer");
                            if (FormationNeed(Current.ReadSkillTacticValidity(tactic), "tactic_validity"))
                                list.Add(FormationNeed(tactic.PhysicalSlot, "tactic_receiver"));
                        }
                    }
                    else
                    {
                        int person = FormationPerson(unit);
                        if (FormationNeed(Current.ReadPersonNativeValidity(person), "commander_validity"))
                        {
                            if (!tacticInputsBound)
                            {
                                DeploymentTacticCurrentInputs.BindCurrent(state, Current);
                                tacticInputsBound = true;
                            }
                            int identity = FormationNeed(Current.ReadPersonIdentity(person), "commander_identity");
                            int selected = FormationNeed(ArmyConfigurationSourceAdapter.ReadNaval(state, unit.Owner), "naval_formation");
                            for (int index = 0; index < 10 && list.Count < 3; index++)
                            {
                                var tactic = DeploymentTacticSelectionSource.ReadCurrentSlot(Current, identity, person, index);
                                if (!FormationNeed(Current.ReadSkillTacticValidity(tactic), "tactic_validity")) continue;
                                uint mask = 0;
                                for (int part = 0; part < 4; part++)
                                    mask |= (uint)FormationNeed(Current.ReadSkillTacticRawByte(tactic, 0x6C + part), "naval_tactic_mask") << (part * 8);
                                if (selected < 21 && (mask & (1u << selected)) != 0) list.Add(FormationNeed(tactic.PhysicalSlot, "tactic_receiver"));
                            }
                        }
                    }
                }
            }
            return currentTacticLists[unit.Id] = list.AsReadOnly();
        }

        private static T FormationNeed<T>(T? value, string field) where T : struct => value ?? throw FormationMissing(field);
        private static InvalidDataException FormationMissing(string field) => new("army-information.formations." + field);
    }
}
