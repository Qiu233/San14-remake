using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Read-only publication for the selected unsubmitted composition.
    /// The current definition capture is shared, but missing inputs are local
    /// to the group which actually consumes them. No Army or reservation is created.</summary>
    public DeploymentDraftUnitInfo QueryDeploymentDraftUnitInfo(GameState state, int locationId,
        IReadOnlyList<DeploymentDraftRow> rows, int selectedOrdinal)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(rows);
        SourceWorldContext? current = null; string? captureFailure = null;
        try { current = CaptureOfficerCommandCapacityCurrent(state); DeploymentTacticCurrentInputs.BindCurrent(state, current); }
        catch (InvalidDataException error) { captureFailure = error.Message; }
        DeploymentDraftRow Selected() => (uint)selectedOrdinal < (uint)rows.Count ? rows[selectedOrdinal] : throw UnitInfoMissing("selected_ordinal");
        SourceWorldContext Current() => current ?? throw new InvalidDataException(captureFailure!);
        int Person()
        {
            int id = Selected().Request.OfficerId;
            if (id < 0) throw UnitInfoMissing("negative_person_index");
            return UnitInfoNeed((id <= 6000 ? Current().ReadPersonLookupReference(id) : Current().ReadTransferPersonTableReference(0))?.PhysicalSlot, "person_pointer");
        }
        void Profile()
        {
            var p = state.NativeArmySimulation?.Parameters;
            if (p is null || p.Language != Seed.Language || p.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
                throw UnitInfoMissing("rule_profile");
        }
        int Formation(bool naval)
        {
            int id = naval ? Selected().NavalFormationId : Selected().Request.FormationId;
            if (id < 0) throw UnitInfoMissing("negative_formation_index");
            return id < 21 ? id : 0;
        }
        int Maximum()
        {
            //7CE020 returns0 immediately for a negative selected ordinal.
            if (selectedOrdinal < 0) return 0;
            _ = Selected();
            var sourceBase = locationId switch
            {
                >= 1 and <= 51 when state.Cities.ContainsKey(locationId) => new BaseReference(BaseKind.City, locationId),
                >= 52 and <= 61 when state.Gates.ContainsKey(locationId) => new BaseReference(BaseKind.Gate, locationId - 51),
                _ => throw UnitInfoMissing("base_identity")
            };
            int available = UnitInfoNeed(Current().ReadBaseResource(sourceBase, BaseResourceKind.Troops), "current_base_troops");
            for (int index = 0; index < rows.Count; index++)
                if (index != selectedOrdinal && rows[index].Request.Troops > 0)
                    available = unchecked(available - rows[index].Request.Troops);
            var capacity = OfficerCommandCapacity.Resolve(state, Current(), Person());
            return Math.Min(available, capacity.Capacity ?? throw new InvalidDataException(capacity.UnresolvedInput));
        }
        DeploymentFormationPresentation Presentation(bool naval)
        {
            _ = Current(); Profile(); int id = Formation(naval);
            var definition = state.Formations.GetValueOrDefault(id) ?? throw UnitInfoMissing("current_formation_name");
            byte kind = UnitInfoNeed(TacticRemakeCurrentFormations.ReadRawByte(state, id, 0x7E), "current_formation_kind");
            return new(id, definition.Name, kind, ArmyMapUiSources.FormationIcon((byte)id, kind));
        }
        DeploymentTroopClassification Classification(bool naval)
        {
            Profile(); return DeploymentTroopClassificationSource.Read(state, Current(), Person(), Formation(naval), Selected().Request.Troops);
        }
        IReadOnlyList<DeploymentTacticPresentation> Tactics(bool naval)
        {
            var context = Current(); Profile(); var row = Selected();
            //The original first constructs its valid three-slot pointer list.
            //Compatibility only controls compact publication, never selection order.
            if (row.Request.TacticIds.Count != 3) throw UnitInfoMissing("three_selected_tactic_slots");
            int formation = naval ? row.NavalFormationId : row.Request.FormationId;
            var result = new List<DeploymentTacticPresentation>();
            foreach (int requested in row.Request.TacticIds)
            {
                if (requested < 0) throw UnitInfoMissing("negative_tactic_index");
                int id = requested < 201 ? requested : 0;
                var reference = context.ReadSkillTacticReference(id) ?? throw UnitInfoMissing("tactic_pointer");
                if (!UnitInfoNeed(context.ReadSkillTacticValidity(reference), "current_tactic_validity")) continue;
                if ((uint)formation >= 21) continue;
                uint mask = 0;
                for (int part = 0; part < 4; part++)
                    mask |= (uint)UnitInfoNeed(context.ReadSkillTacticRawByte(reference, 0x6C + part), "current_tactic_formation_mask") << (8 * part);
                if ((mask & (1u << formation)) == 0) continue;
                var definition = state.Tactics.GetValueOrDefault(id) ?? throw UnitInfoMissing("current_tactic_name");
                result.Add(new(id, definition.Name));
            }
            return result.AsReadOnly();
        }
        bool SpecialText()
        {
            int p = Person(); var context = Current();
            if (!UnitInfoNeed(context.ReadPersonNativeValidity(p), "current_person_validity")) return false;
            int id = UnitInfoNeed(context.ReadPersonIdentity(p), "current_person_identity");
            if (id is not (>= 4111 and <= 4120 or >= 4131 and <= 4140 or >= 4151 and <= 4160 or >= 4171 and <= 4180 or >= 4191 and <= 4200)) return false;
            if (UnitInfoNeed(context.ReadPersonFirstLocation(p), "current_first_location") is not (>= 1 and <= 61)) return false;
            return UnitInfoNeed(context.ReadPersonForce(p)?.PhysicalSlot, "current_person_force") is not (>= 46 and <= 50);
        }
        int? maximum = null; bool? special = null;
        DeploymentFormationPresentation? land = null, naval = null;
        DeploymentTroopClassification? landTroops = null, navalTroops = null;
        IReadOnlyList<DeploymentTacticPresentation>? landTactics = null, navalTactics = null;
        string? maximumMissing = null, landMissing = null, navalMissing = null, landTroopsMissing = null,
            navalTroopsMissing = null, landTacticsMissing = null, navalTacticsMissing = null, specialMissing = null;
        try { maximum = Maximum(); } catch (InvalidDataException error) { maximumMissing = error.Message; }
        try { land = Presentation(false); } catch (InvalidDataException error) { landMissing = error.Message; }
        try { naval = Presentation(true); } catch (InvalidDataException error) { navalMissing = error.Message; }
        try { landTroops = Classification(false); } catch (InvalidDataException error) { landTroopsMissing = error.Message; }
        try { navalTroops = Classification(true); } catch (InvalidDataException error) { navalTroopsMissing = error.Message; }
        try { landTactics = Tactics(false); } catch (InvalidDataException error) { landTacticsMissing = error.Message; }
        try { navalTactics = Tactics(true); } catch (InvalidDataException error) { navalTacticsMissing = error.Message; }
        try { special = SpecialText(); } catch (InvalidDataException error) { specialMissing = error.Message; }
        return new(maximum, land, naval, landTroops, navalTroops, landTactics, navalTactics, special,
            maximumMissing, landMissing, navalMissing, landTroopsMissing, navalTroopsMissing,
            landTacticsMissing, navalTacticsMissing, specialMissing);
    }
    private static T UnitInfoNeed<T>(T? value, string field) where T : struct => value ?? throw UnitInfoMissing(field);
    private static InvalidDataException UnitInfoMissing(string field) => new("deployment.unit_info." + field);
}
