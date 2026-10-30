using San14.Core;

namespace San14.Data;

public sealed record DeploymentTacticChoice(int Occurrence, int TacticId, string Name,
    bool Compatible, bool Selected, bool Enabled);
public sealed record DeploymentTacticChoices(IReadOnlyList<DeploymentTacticChoice>? Choices,
    string? UnresolvedInput);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ten AUTab controls use the qualified person's complete list,
    /// including incompatible entries. This differs from automatic composition's
    /// compatible, ability-ranked candidates.</summary>
    public DeploymentTacticChoices QueryDeploymentTacticChoices(GameState state, int officerId,
        int landFormationId, IReadOnlyList<int> selectedTactics)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(selectedTactics);
        try
        {
            if (selectedTactics.Count != 3 || selectedTactics.Any(id => id < 0))
                throw TacticChoiceMissing("three_selected_slots");
            if ((uint)landFormationId >= 21) throw TacticChoiceMissing("formation_identity");
            if (!state.Officers.ContainsKey(officerId)) throw TacticChoiceMissing("current_person_owner");
            var parameters = state.NativeArmySimulation?.Parameters;
            if (parameters is null || parameters.Language != Seed.Language ||
                parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
                throw TacticChoiceMissing("rule_profile");
            var current = CaptureOfficerCommandCapacityCurrent(state);
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
            int physical = _definitions.ResolveOfficerSource(officerId) ?? throw TacticChoiceMissing("person_identity");
            if (current.ReadPersonIdentity(physical) != officerId) throw TacticChoiceMissing("current_person_identity");
            if (!(current.ReadPersonNativeValidity(physical) ?? throw TacticChoiceMissing("person_validity")))
                return new(Array.Empty<DeploymentTacticChoice>(), null);
            var candidates = new List<(int Id, bool Compatible)>();
            for (int slot = 0; slot < 10; slot++)
            {
                var tactic = DeploymentTacticSelectionSource.ReadCurrentSlot(current, officerId, physical, slot);
                if (!(current.ReadSkillTacticValidity(tactic) ?? throw TacticChoiceMissing("tactic_validity"))) continue;
                int id = tactic.PhysicalSlot ?? throw TacticChoiceMissing("tactic_identity");
                uint mask = 0;
                for (int part = 0; part < 4; part++)
                    mask |= (uint)(current.ReadSkillTacticRawByte(tactic, 0x6C + part) ??
                        throw TacticChoiceMissing("formation_mask")) << (part * 8);
                candidates.Add((id, landFormationId != 7 && (mask & (1u << landFormationId)) != 0));
            }
            //28AA60's optional complete list is sorted by24FC40, without the
            //compatible list's effect/ability partitions. Equal entries stay in
            //their original person-slot order; duplicate occurrences survive.
            var ordered = candidates.OrderByDescending(item => item.Id is >= 31 and <= 130 or >= 140 and <= 200).ToArray();
            var selected = new bool[ordered.Length];
            foreach (int requested in selectedTactics)
            {
                int id = requested < 201 ? requested : 0;
                int index = Array.FindIndex(ordered, item => item.Id == id);
                if (index >= 0) selected[index] = true;
            }
            int count = selected.Count(value => value);
            return new(Array.AsReadOnly(ordered.Select((item, index) => new DeploymentTacticChoice(index,
                item.Id, state.Tactics.GetValueOrDefault(item.Id)?.Name ?? throw TacticChoiceMissing("tactic_name"),
                item.Compatible, selected[index], item.Compatible && (selected[index] || count < 3))).ToArray()), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    /// <summary>A control release writes the selected occurrences in button order,
    /// clears the three slots first, normalizes them, and updates the ordinary
    /// formation backup. It does not change campaign state.</summary>
    public DeploymentLandFormationEditResult QueryDeploymentTacticEdit(GameState state, int officerId,
        DeploymentLandFormationEditState previous, int occurrence)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var query = QueryDeploymentTacticChoices(state, officerId, previous.LandFormationId, previous.Tactics.ToArray());
        if (query.Choices is not { } choices) return new(null, query.UnresolvedInput);
        if ((uint)occurrence >= (uint)choices.Count || !choices[occurrence].Enabled)
            return new(null, "deployment.tactic_choices.selection_not_available");
        var slots = new int[3]; int at = 0;
        foreach (var choice in choices)
            if (choice.Occurrence == occurrence ? !choice.Selected : choice.Selected)
                slots[at++] = choice.TacticId;
        try
        {
            var current = CaptureOfficerCommandCapacityCurrent(state);
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
            var tactics = DeploymentTacticSelectionSource.NormalizeThree(current, DeploymentTacticSlots.From(slots));
            return new(previous with { Tactics = tactics, BackupTactics =
                unchecked((uint)(previous.LandFormationId - 8)) > 2 ? tactics : previous.BackupTactics }, null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private static InvalidDataException TacticChoiceMissing(string field) => new("deployment.tactic_choices." + field);
}
