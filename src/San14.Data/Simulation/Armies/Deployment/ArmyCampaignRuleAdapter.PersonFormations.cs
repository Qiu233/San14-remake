using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Publishes fixed formation capabilities for the caller's already
    /// published candidates. Unknown cells remain distinct from known false.</summary>
    public DeploymentPersonFormationQuery QueryDeploymentPersonFormationCapabilities(
        GameState state, IReadOnlyList<int> officerIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(officerIds);
        try
        {
            var source = new PersonFormationReader(state, _definitions);
            var rows = new List<DeploymentPersonFormationRow>(officerIds.Count);
            foreach (int officer in officerIds)
            {
                source.RequireOwner(officer);
                source.PrepareCurrent();
                var cells = new List<DeploymentPersonFormationCapability>(11);
                for (int formation = 1; formation <= 10; formation++) Add(formation);
                Add(14);
                rows.Add(new(officer, cells.AsReadOnly()));

                void Add(int formation)
                {
                    DeploymentPersonBasicValue<bool> cell;
                    try { cell = new(source.Allows(officer, formation), null); }
                    catch (InvalidDataException error) { cell = new(null, error.Message); }
                    cells.Add(new(formation, cell));
                }
            }
            return new(rows.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    /// <summary>Both source directions put capable tag0 before empty tag2.
    /// Equal keys preserve current order; only actual comparisons read keys.</summary>
    public DeploymentPersonOrderQuery QueryDeploymentPersonFormationOrder(GameState state,
        IReadOnlyList<DeploymentPersonSortRow> currentRows, int resourceFormationId, bool ascending)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(currentRows);
        try
        {
            if (resourceFormationId is not (>= 1 and <= 10 or 14))
                throw PersonFormationMissing("formation_id");
            var rows = currentRows.ToArray();
            var source = new PersonFormationReader(state, _definitions);
            var keys = new Dictionary<int, bool>();
            for (int index = 1; index < rows.Length; index++)
            {
                var row = rows[index]; int at = index;
                while (at > 0 && Before(row.OfficerId, rows[at - 1].OfficerId))
                { rows[at] = rows[at - 1]; at--; }
                rows[at] = row;
            }
            return new(Array.AsReadOnly(rows.Select(row => row.OfficerId).ToArray()), null);

            bool Key(int id)
            {
                if (!keys.TryGetValue(id, out bool key)) keys[id] = key = source.Allows(id, resourceFormationId);
                return key;
            }
            bool Before(int left, int right)
            {
                if (left == right) return false;
                // Type1 compares tag first in either direction. Both capable
                // values are zero, so reversing never reverses either tie group.
                bool one = Key(left), two = Key(right);
                return one && !two;
            }
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private sealed class PersonFormationReader(GameState state, ArmyOperationContext definitions)
    {
        private SourceWorldContext? _current;
        private readonly Dictionary<int, int> _physicals = [];
        private readonly Dictionary<int, uint?> _masks = [];
        private readonly Dictionary<int, ushort?> _identities = [];
        private SourceWorldContext Current => _current ??= OwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions);

        internal void PrepareCurrent() => _ = Current;

        internal int RequireOwner(int officer)
        {
            if (_physicals.TryGetValue(officer, out int physical)) return physical;
            if (!state.Officers.ContainsKey(officer) || definitions.ResolveOfficerSource(officer) is not int resolved)
                throw PersonFormationMissing("person_owner");
            return _physicals[officer] = resolved;
        }

        internal bool Allows(int officer, int formation)
        {
            int physical = RequireOwner(officer);
            if (!_masks.TryGetValue(physical, out uint? knownMask))
                _masks[physical] = knownMask = Current.ReadPersonFormationMask164(physical);
            uint mask = knownMask ?? throw PersonFormationMissing("current_formation_capabilities");
            int identity = 0;
            if (formation == 14 && (mask & (1u << 14)) != 0)
            {
                if (!_identities.TryGetValue(physical, out ushort? knownIdentity))
                    _identities[physical] = knownIdentity = Current.ReadPersonIdentity(physical);
                identity = knownIdentity ?? throw PersonFormationMissing("current_person_identity");
            }
            return DeploymentFormationEligibility.Allows(mask, formation, identity);
        }
    }

    private static InvalidDataException PersonFormationMissing(string field) =>
        new("deployment.person-formations." + field);
}
