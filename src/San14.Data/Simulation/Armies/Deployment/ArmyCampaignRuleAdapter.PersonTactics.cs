using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public DeploymentPersonTacticQuery QueryDeploymentPersonTactics(GameState state,
        IReadOnlyList<int> candidateIds, int firstSlot)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(candidateIds);
        try
        {
            if (firstSlot is not (0 or 5)) throw PersonTacticMissing("first_slot");
            var source = new PersonTacticReader(state, _definitions);
            var rows = new List<DeploymentPersonTacticRow>(candidateIds.Count);
            foreach (int candidate in candidateIds)
            {
                source.RequireOwner(candidate);
                var cells = new List<DeploymentPersonBasicValue<int>>(5);
                for (int slot = firstSlot; slot < firstSlot + 5; slot++)
                {
                    try { cells.Add(new(source.Read(candidate, slot), null)); }
                    catch (InvalidDataException error) { cells.Add(new(null, error.Message)); }
                }
                rows.Add(new(candidate, cells.AsReadOnly()));
            }
            return new(rows.AsReadOnly(), null);
        }
        catch (PersonTacticStructureError error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    public DeploymentPersonOrderQuery QueryDeploymentPersonTacticOrder(GameState state,
        IReadOnlyList<DeploymentPersonSortRow> currentRows, int slot, bool ascending)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(currentRows);
        try
        {
            if ((uint)slot >= 10) throw PersonTacticMissing("slot");
            var source = new PersonTacticReader(state, _definitions);
            var rows = currentRows.ToArray();
            for (int index = 1; index < rows.Length; index++)
            {
                var row = rows[index]; int at = index;
                while (at > 0 && Before(row.OfficerId, rows[at - 1].OfficerId))
                { rows[at] = rows[at - 1]; at--; }
                rows[at] = row;
            }
            return new(Array.AsReadOnly(rows.Select(row => row.OfficerId).ToArray()), null);

            bool Before(int left, int right)
            {
                if (left == right) return false;
                int? one = source.Read(left, slot), two = source.Read(right, slot);
                if (one.HasValue != two.HasValue) return one.HasValue;
                if (!one.HasValue) return false;
                return ascending ? one.Value < two!.Value : one.Value > two!.Value;
            }
        }
        catch (PersonTacticStructureError error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private sealed class PersonTacticReader(GameState state, ArmyOperationContext definitions)
    {
        private SourceWorldContext? _current;
        private readonly Dictionary<int, int> _physicals = [];
        private readonly Dictionary<int, ushort?> _identities = [];
        private readonly Dictionary<(int Physical, int Slot), int?> _tactics = [];
        private SourceWorldContext Current
        {
            get
            {
                if (_current is not null) return _current;
                try
                {
                    var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions);
                    DeploymentTacticCurrentInputs.BindCurrent(state, current);
                    return _current = current;
                }
                catch (InvalidDataException error) { throw new PersonTacticStructureError(error.Message); }
            }
        }

        internal int RequireOwner(int id)
        {
            if (_physicals.TryGetValue(id, out int physical)) return physical;
            if (!state.Officers.ContainsKey(id) || definitions.ResolveOfficerSource(id) is not int resolved ||
                definitions.ReadOfficerSourceIdentity(resolved) != id)
                throw new PersonTacticStructureError(PersonTacticMissing("person_owner").Message);
            return _physicals[id] = resolved;
        }

        internal int? Read(int id, int slot)
        {
            int physical = RequireOwner(id);
            if (_tactics.TryGetValue((physical, slot), out int? known)) return known;
            var current = Current;
            if (!_identities.TryGetValue(physical, out ushort? identity))
                _identities[physical] = identity = current.ReadPersonIdentity(physical);
            int actual = identity ?? throw PersonTacticMissing("person_identity");
            var reference = DeploymentTacticSelectionSource.ReadCurrentSlot(current, actual, physical, slot);
            bool valid = current.ReadSkillTacticValidity(reference) ?? throw PersonTacticMissing("tactic_validity");
            int? value = valid ? current.ReadSkillTacticPhysicalSlot(reference)
                ?? throw PersonTacticMissing("tactic_physical") : null;
            _tactics[(physical, slot)] = value;
            return value;
        }
    }

    private sealed class PersonTacticStructureError(string message) : Exception(message);
    private static InvalidDataException PersonTacticMissing(string field) =>
        new("deployment.person-tactics." + field);
}
