using System.Collections.Immutable;
using San14.Core;
using CandidateList = San14.Data.AI.Original.OriginalAiOperation.CandidateList;

namespace San14.Data.AI.Original;

/// <summary>Policy-context-owned mutable lists and stable save identities.
/// It retains neither an operation nor a World. Equal contents never establish
/// aliasing: only an actual shared reference or saved shared identity does.</summary>
internal sealed class OriginalAiSharedListRuntime
{
    private readonly Dictionary<int, CandidateList> lists = [];
    private readonly Dictionary<CandidateList, int> identities = new(ReferenceEqualityComparer.Instance);
    private int nextId;

    internal void ImportSaved(IEnumerable<OriginalAiBlackboardSnapshot> snapshots)
    {
        var definitions = new Dictionary<int, OriginalAiBlackboardList>();
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Bindings.IsDefault || snapshot.Lists.IsDefault) throw Missing("saved_arrays");
            var rows = snapshot.Lists.ToDictionary(row => row.ListId);
            foreach (int id in snapshot.Bindings.Select(binding => binding.Value).OfType<OriginalAiSharedListValue>()
                .Select(value => value.ListId).Distinct())
            {
                var row = rows.GetValueOrDefault(id) ?? throw Missing("saved_shared_row");
                if (definitions.TryGetValue(id, out var prior))
                {
                    // This is the format boundary for duplicated definitions
                    // of one saved object, once on bank load, not a query scan.
                    if (prior.Kind != row.Kind || !prior.Values.SequenceEqual(row.Values)) throw Missing("conflicting_saved_shared_row");
                    continue;
                }
                definitions.Add(id, row);
                Import(row);
            }
        }
    }

    private CandidateList Import(OriginalAiBlackboardList row)
    {
        if (lists.TryGetValue(row.ListId, out var existing)) return existing;
        var list = OriginalAiOperation.DecodeBlackboardList(row);
        lists.Add(row.ListId, list); identities.Add(list, row.ListId);
        nextId = Math.Max(nextId, checked(row.ListId + 1));
        return list;
    }

    internal int Identify(CandidateList list)
    {
        if (identities.TryGetValue(list, out int id)) return id;
        id = nextId; nextId = checked(nextId + 1);
        lists.Add(id, list); identities.Add(list, id);
        return id;
    }

    internal CandidateList Resolve(int id) => lists.GetValueOrDefault(id) ?? throw Missing("shared_identity");

    internal OriginalAiBlackboardSnapshot Normalize(OriginalAiBlackboardSnapshot snapshot)
    {
        if (snapshot.Bindings.IsDefault || snapshot.Lists.IsDefault) throw Missing("saved_arrays");
        var rows = snapshot.Lists.ToDictionary(row => row.ListId);
        var local = new Dictionary<int, int>();
        var bindings = snapshot.Bindings.Select(binding =>
        {
            if (binding.Value is OriginalAiSharedListValue shared)
            {
                Import(rows.GetValueOrDefault(shared.ListId) ?? throw Missing("saved_shared_row"));
                return binding;
            }
            if (binding.Value is not OriginalAiListValue value) return binding;
            // Legacy actor-local IDs are decoded separately for each actual
            // context, even when another actor used the same local ID/content.
            if (!local.TryGetValue(value.ListId, out int id))
            {
                id = Identify(OriginalAiOperation.DecodeBlackboardList(rows.GetValueOrDefault(value.ListId)
                    ?? throw Missing("saved_local_row")));
                local.Add(value.ListId, id);
            }
            return binding with { Value = new OriginalAiSharedListValue(id) };
        }).ToImmutableArray();
        return Capture(new(bindings, []));
    }

    internal OriginalAiBlackboardSnapshot Capture(OriginalAiBlackboardSnapshot snapshot)
    {
        var rows = snapshot.Bindings.Select(binding => binding.Value).OfType<OriginalAiSharedListValue>()
            .Select(value => value.ListId).Distinct()
            .Select(id => OriginalAiOperation.EncodeBlackboardList(id, Resolve(id))).ToImmutableArray();
        return new(snapshot.Bindings, rows);
    }

    internal bool TryReadList(OriginalAiBlackboardSnapshot snapshot, uint key, out CandidateList value)
    {
        foreach (var binding in snapshot.Bindings)
            if (binding.Key == key && binding.Value is OriginalAiSharedListValue shared)
            { value = Resolve(shared.ListId); return true; }
        value = null!; return false;
    }

    private static InvalidDataException Missing(string name) => new("original_ai.shared_blackboard_lists." + name);
}
