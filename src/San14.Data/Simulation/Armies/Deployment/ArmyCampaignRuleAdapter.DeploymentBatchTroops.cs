using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public DeploymentBatchTroopEditor QueryDeploymentBatchTroopEditor(GameState state,
        int locationId, IReadOnlyList<DeploymentDraftRow> rows)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0) return new(false, null, null, null, null);
        bool? enabled = null;
        try
        {
            BatchTroopOperation.RequireOrdinaryRows(rows);
            var operation = new BatchTroopOperation(this, state);
            int stock = operation.Stock(locationId);
            if (stock < unchecked(DeploymentDraftDefaults.CeMinimumNewUnitTroops * rows.Count))
                return new(false, null, null, null, null);
            enabled = true;
            // The calculator writer divides a sign-extended stock as uint64,
            // then floors its quotient to hundreds before narrowing to int32.
            ulong quotient = unchecked((ulong)(long)stock) / (ulong)rows.Count;
            int perRow = unchecked((int)(quotient / 100 * 100));
            int largest = 1000;
            foreach (var row in rows) largest = Math.Max(largest, operation.Capacity(row));
            int minimum = DeploymentDraftDefaults.CeMinimumNewUnitTroops;
            return new(true, minimum, minimum, Math.Min(perRow, largest), null);
        }
        catch (InvalidDataException error)
        {
            int? minimum = enabled == true ? DeploymentDraftDefaults.CeMinimumNewUnitTroops : null;
            return new(enabled, minimum, minimum, null, error.Message);
        }
    }

    /// <summary>Applies an accepted calculator value, without rounding it or
    /// normalizing formations, tactics, policies, actions or row ordering.</summary>
    public DeploymentDraftQuery ApplyDeploymentBatchTroops(GameState state,
        IReadOnlyList<DeploymentDraftRow> rows, int troops, int? focusedOfficerId = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(rows);
        try
        {
            BatchTroopOperation.RequireOrdinaryRows(rows);
            var operation = new BatchTroopOperation(this, state);
            var result = new List<DeploymentDraftRow>(rows.Count);
            foreach (var row in rows)
                result.Add(row with { Request = row.Request with { Troops = Math.Min(troops, operation.Capacity(row)) } });
            return new(result.AsReadOnly(), focusedOfficerId, null);
        }
        catch (InvalidDataException error) { return new(null, focusedOfficerId, error.Message); }
    }

    /// <summary>The caller owns the shared enabled gate. The original operation
    /// allocates by ascending command capacity and writes back to the same rows.</summary>
    public DeploymentDraftQuery ApplyDeploymentMaximumTroops(GameState state, int locationId,
        IReadOnlyList<DeploymentDraftRow> rows, int? focusedOfficerId = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0) return new(Array.Empty<DeploymentDraftRow>(), focusedOfficerId, null);
        try
        {
            BatchTroopOperation.RequireOrdinaryRows(rows);
            var operation = new BatchTroopOperation(this, state);
            // Ordinary Make admits at most nine rows; this is the source's
            // stable insertion-sort branch, with no tie breaker for equal caps.
            var ordered = Enumerable.Range(0, rows.Count).ToArray();
            for (int i = 1; i < ordered.Length; i++)
            {
                int item = ordered[i], at = i;
                while (at > 0 && operation.Capacity(rows[item]) < operation.Capacity(rows[ordered[at - 1]]))
                { ordered[at] = ordered[at - 1]; at--; }
                ordered[at] = item;
            }
            int remaining = unchecked(operation.Stock(locationId) - rows.Count * DeploymentDraftDefaults.CeMinimumNewUnitTroops);
            int count = rows.Count;
            var result = rows.ToArray();
            foreach (int index in ordered)
            {
                int share = unchecked((remaining / count + DeploymentDraftDefaults.CeMinimumNewUnitTroops) / 100 * 100);
                int value = Math.Min(share, operation.Capacity(rows[index]));
                result[index] = rows[index] with { Request = rows[index].Request with { Troops = value } };
                remaining = unchecked(remaining + (1000 - value));
                count--;
            }
            return new(Array.AsReadOnly(result), focusedOfficerId, null);
        }
        catch (InvalidDataException error) { return new(null, focusedOfficerId, error.Message); }
    }

    private sealed class BatchTroopOperation(ArmyCampaignRuleAdapter adapter, GameState state)
    {
        internal static void RequireOrdinaryRows(IReadOnlyList<DeploymentDraftRow> rows)
        {
            if (rows.Count > 9) throw Missing("ordinary_row_limit");
        }
        private SourceWorldContext? current;
        private readonly Dictionary<int, int> capacities = [];
        private SourceWorldContext Current() => current ??= adapter.CaptureOfficerCommandCapacityCurrent(state);
        internal int Stock(int locationId)
        {
            var sourceBase = locationId switch
            {
                >= 1 and <= 51 when state.Cities.ContainsKey(locationId) => new BaseReference(BaseKind.City, locationId),
                >= 52 and <= 61 when state.Gates.ContainsKey(locationId) => new BaseReference(BaseKind.Gate, locationId - 51),
                _ => throw Missing("source_base")
            };
            return Current().ReadBaseResource(sourceBase, BaseResourceKind.Troops) ?? throw Missing("base_troops");
        }
        internal int Capacity(DeploymentDraftRow row)
        {
            int id = row.Request.OfficerId;
            if (capacities.TryGetValue(id, out int value)) return value;
            if (id < 0) throw Missing("negative_person_index");
            var context = Current();
            int person = (id < 6001 ? context.ReadPersonLookupReference(id) : context.ReadTransferPersonTableReference(0))?.PhysicalSlot
                ?? throw Missing("person_pointer");
            var capacity = OfficerCommandCapacity.Resolve(state, context, person);
            value = capacity.Capacity ?? throw new InvalidDataException(capacity.UnresolvedInput);
            capacities.Add(id, value);
            return value;
        }
        private static InvalidDataException Missing(string input) => new($"deployment.batch_troops.{input}");
    }
}
