using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public DeploymentPersonLinkageQuery QueryDeploymentPersonLinkage(GameState state,
        IReadOnlyList<int> candidateIds, IReadOnlyList<int> orderedSelectedIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(candidateIds);
        ArgumentNullException.ThrowIfNull(orderedSelectedIds);
        try
        {
            var slots = LinkageSlots(orderedSelectedIds);
            var source = new PersonLinkageReader(state, _definitions);
            foreach (int selected in orderedSelectedIds) source.RequireOwner(selected);
            foreach (int candidate in candidateIds) source.RequireOwner(candidate);
            var rows = new List<DeploymentPersonLinkageRow>(candidateIds.Count);
            foreach (int candidate in candidateIds)
            {
                var tiers = new List<DeploymentPersonBasicValue<byte>>(9);
                foreach (int? selected in slots)
                {
                    try { tiers.Add(new(source.Tier(candidate, selected), null)); }
                    catch (InvalidDataException error) { tiers.Add(new(null, error.Message)); }
                }
                rows.Add(new(candidate, tiers.AsReadOnly()));
            }
            return new(Array.AsReadOnly(slots), rows.AsReadOnly(), null);
        }
        catch (LinkageStructureError error) { return new(null, null, error.Message); }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    public DeploymentPersonOrderQuery QueryDeploymentPersonLinkageOrder(GameState state,
        IReadOnlyList<DeploymentPersonSortRow> currentRows,
        IReadOnlyList<int> orderedSelectedIds, int slotOrdinal, bool ascending)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(currentRows);
        ArgumentNullException.ThrowIfNull(orderedSelectedIds);
        try
        {
            if ((uint)slotOrdinal >= 9) throw LinkageMissing("slot_ordinal");
            int? selected = LinkageSlots(orderedSelectedIds)[slotOrdinal];
            var source = new PersonLinkageReader(state, _definitions);
            var rows = currentRows.ToArray();
            var keys = new Dictionary<int, byte?>();
            for (int index = 1; index < rows.Length; index++)
            {
                var row = rows[index]; int at = index;
                while (at > 0 && Before(row.OfficerId, rows[at - 1].OfficerId))
                { rows[at] = rows[at - 1]; at--; }
                rows[at] = row;
            }
            return new(Array.AsReadOnly(rows.Select(row => row.OfficerId).ToArray()), null);

            byte? Key(int id)
            {
                if (!keys.TryGetValue(id, out byte? value)) keys[id] = value = source.Tier(id, selected);
                return value;
            }
            bool Before(int left, int right)
            {
                if (left == right) return false;
                byte? one = Key(left), two = Key(right);
                if (one.HasValue != two.HasValue) return one.HasValue;
                if (!one.HasValue) return false;
                return ascending ? one.Value < two!.Value : one.Value > two!.Value;
            }
        }
        catch (LinkageStructureError error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private static int?[] LinkageSlots(IReadOnlyList<int> selected)
    {
        if (selected.Count > 9) throw LinkageMissing("selected_count");
        var slots = new int?[9];
        for (int index = 0; index < selected.Count; index++) slots[index] = selected[index];
        return slots;
    }

    private sealed class PersonLinkageReader(GameState state, ArmyOperationContext definitions)
    {
        private SourceWorldContext? _current;
        private bool _poolAttempted;
        private string? _poolError;
        private readonly Dictionary<int, int> _physicals = [];
        private readonly Dictionary<int, bool?> _validity = [];
        private readonly Dictionary<(int Candidate, int Selected), byte> _tiers = [];

        private SourceWorldContext Current
        {
            get
            {
                if (_current is not null) return _current;
                try { return _current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions); }
                catch (InvalidDataException error) { throw new LinkageStructureError(error.Message); }
            }
        }

        internal int RequireOwner(int officer)
        {
            if (_physicals.TryGetValue(officer, out int physical)) return physical;
            if (!state.Officers.ContainsKey(officer) || definitions.ResolveOfficerSource(officer) is not int resolved ||
                definitions.ReadOfficerSourceIdentity(resolved) != officer)
                throw new LinkageStructureError(LinkageMissing("person_owner").Message);
            return _physicals[officer] = resolved;
        }

        private bool Valid(int physical)
        {
            if (!_validity.TryGetValue(physical, out bool? value))
                _validity[physical] = value = Current.ReadPersonNativeValidity(physical);
            return value ?? throw LinkageMissing("person_validity");
        }

        internal byte? Tier(int candidate, int? selected)
        {
            if (selected is not int selectedId) return null;
            int other = RequireOwner(selectedId);
            if (!Valid(other)) return null;
            int person = RequireOwner(candidate);
            if (!Valid(person)) return null;
            if (person == other) return 2;
            if (_tiers.TryGetValue((person, other), out byte tier)) return tier;
            tier = OwnedPersonRelationInputs.ComputeCurrentWithPrimaryPool(state, Current,
                person, other, 1, PreparePrimaryPool).Tier;
            return _tiers[(person, other)] = tier;
        }

        private void PreparePrimaryPool()
        {
            if (!_poolAttempted)
            {
                _poolAttempted = true;
                try
                {
                    var arena = state.ReadArmyLifecycle() ?? throw LinkageMissing("saved_indexes");
                    _ = WorldIndexContext.ImportOwnedState(Current, arena.Indexes, state);
                }
                catch (InvalidDataException error) { _poolError = error.Message; }
            }
            if (_poolError is not null) throw new InvalidDataException(_poolError);
        }
    }

    private sealed class LinkageStructureError(string message) : Exception(message);
    private static InvalidDataException LinkageMissing(string field) =>
        new("deployment.person-linkage." + field);
}
