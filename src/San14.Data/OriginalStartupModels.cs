namespace San14.Data;

public enum OriginalStartupModelKind { Force, District, Troops, ArmyUnit }

/// <summary>A current data-object identity, independent of scenario IDs and
/// native addresses. Aliased pointer slots supply the same physical identity.</summary>
public sealed record OriginalStartupDataReference(OriginalStartupModelKind Kind, int? Identity);
public sealed record OriginalStartupVisualReference(int? Identity);
public sealed record OriginalStartupResourceOwnerReference(int? Identity);

public interface IOriginalStartupModelQueries
{
    IEnumerable<OriginalStartupDataReference?>? ReadStartupForces();
    IEnumerable<OriginalStartupDataReference?>? ReadStartupDistricts(OriginalStartupDataReference force);
    IEnumerable<OriginalStartupDataReference?>? ReadStartupTroops(OriginalStartupDataReference district);
    IEnumerable<OriginalStartupDataReference?>? ReadStartupArmyUnits(OriginalStartupDataReference troops);
    OriginalStartupVisualReference? ReadStartupTroopsModel(OriginalStartupDataReference troops);
    void WriteStartupTroopsModel(OriginalStartupDataReference troops, OriginalStartupVisualReference visual);
}

/// <summary>The host owns rendering/resource lifetime. No empty resource
/// factory or destructor is installed implicitly by the data initializer.</summary>
public interface IOriginalStartupModelResources
{
    OriginalStartupVisualReference? CreateStartupVisual(OriginalStartupDataReference data);
    void ConfigureStartupTroopsVisual(OriginalStartupVisualReference visual);
    OriginalStartupResourceOwnerReference? ReadStartupVisualOwner(OriginalStartupVisualReference visual);
    void RemoveStartupTroopsVisual(OriginalStartupModels model, OriginalStartupVisualReference visual, int mode);
    void ReleaseStartupTroopsVisual(OriginalStartupResourceOwnerReference owner, OriginalStartupVisualReference visual);
}

/// <summary>Owned equivalent of the model80/90/A0/B0 maps. Keys are current
/// object identities, never raw scenario IDs. Presence and null payload are
/// distinct. The four source tables do not constrain future map capacities.</summary>
public sealed class OriginalStartupModels
{
    private readonly SortedDictionary<int, OriginalStartupVisualReference>[] _maps =
        [new(), new(), new(), new()];
    private readonly IOriginalStartupModelQueries _queries;
    private readonly IOriginalStartupModelResources _resources;

    public OriginalStartupModels(IOriginalStartupModelQueries queries, IOriginalStartupModelResources resources)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(resources);
        _queries = queries;
        _resources = resources;
    }

    public int Count(OriginalStartupModelKind kind) => Map(kind).Count;
    public bool Contains(OriginalStartupDataReference data) => Map(data.Kind).ContainsKey(Key(data));
    public OriginalStartupVisualReference Read(OriginalStartupDataReference data)
        => Map(data.Kind).GetValueOrDefault(Key(data)) ?? new(null);

    /// <summary>Insertion helpers keep an existing entry even if its value is
    /// null. Troops30 is written only after successful A0 insertion.</summary>
    public bool Insert(OriginalStartupDataReference data, OriginalStartupVisualReference visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        int key = Key(data);
        if (!Map(data.Kind).TryAdd(key, visual)) return false;
        if (data.Kind == OriginalStartupModelKind.Troops) _queries.WriteStartupTroopsModel(data, visual);
        return true;
    }

    /// <summary>A1240: map-node clear does not destroy visual values. With
    /// argument0, troops resources are removed/released before A0 is cleared.</summary>
    public void Reset(int argument)
    {
        Map(OriginalStartupModelKind.Force).Clear();
        Map(OriginalStartupModelKind.District).Clear();
        if (argument == 0)
        {
            foreach (var pair in Map(OriginalStartupModelKind.Troops))
            {
                OriginalStartupVisualReference saved = pair.Value;
                RequireVisual(saved, "reset troops value");
                OriginalStartupResourceOwnerReference owner = _resources.ReadStartupVisualOwner(saved)
                    ?? throw new InvalidDataException("Current startup visual owner is unknown.");
                if (!owner.Identity.HasValue) throw new InvalidDataException("Current startup visual owner is null at resource release.");
                _resources.RemoveStartupTroopsVisual(this, saved, 2);
                _resources.ReleaseStartupTroopsVisual(owner, saved);
                _queries.WriteStartupTroopsModel(new(OriginalStartupModelKind.Troops, pair.Key), new(null));
            }
            Map(OriginalStartupModelKind.Troops).Clear();
        }
        Map(OriginalStartupModelKind.ArmyUnit).Clear();
    }

    /// <summary>D20F0 does not consume its argument. All sequences stay live:
    /// factory callbacks can change later pointers, links or membership.</summary>
    public int Build(int argument)
    {
        foreach (OriginalStartupDataReference? candidateForce in Sequence(_queries.ReadStartupForces(), "forces"))
        {
            OriginalStartupDataReference force = Data(candidateForce, OriginalStartupModelKind.Force);
            EnsureMapped(force);
            foreach (OriginalStartupDataReference? candidateDistrict in Sequence(_queries.ReadStartupDistricts(force), "force districts"))
            {
                OriginalStartupDataReference district = Data(candidateDistrict, OriginalStartupModelKind.District);
                EnsureMapped(district);
                foreach (OriginalStartupDataReference? candidateTroops in Sequence(_queries.ReadStartupTroops(district), "district troops"))
                {
                    OriginalStartupDataReference troops = Data(candidateTroops, OriginalStartupModelKind.Troops);
                    if (!Read(troops).Identity.HasValue)
                    {
                        OriginalStartupVisualReference visual = _queries.ReadStartupTroopsModel(troops)
                            ?? throw new InvalidDataException("Current troops30 model pointer is unknown.");
                        if (!visual.Identity.HasValue)
                        {
                            visual = _resources.CreateStartupVisual(troops)
                                ?? throw new InvalidDataException("Startup troops visual allocation outcome is unknown.");
                            RequireVisual(visual, "created troops visual");
                            _resources.ConfigureStartupTroopsVisual(visual);
                        }
                        Insert(troops, visual);
                    }
                    foreach (OriginalStartupDataReference? candidateArmy in Sequence(_queries.ReadStartupArmyUnits(troops), "troops army units"))
                        EnsureMapped(Data(candidateArmy, OriginalStartupModelKind.ArmyUnit));
                }
            }
        }
        return 1;
    }

    private void EnsureMapped(OriginalStartupDataReference data)
    {
        // Native lookup-null calls the insert helper, which first rejects any
        // existing key. It must not create or overwrite its null-valued entry.
        if (Contains(data)) return;
        OriginalStartupVisualReference visual = _resources.CreateStartupVisual(data)
            ?? throw new InvalidDataException("Startup model allocation outcome is unknown.");
        Insert(data, visual);
    }
    private static IEnumerable<OriginalStartupDataReference?> Sequence(IEnumerable<OriginalStartupDataReference?>? values, string name)
        => values ?? throw new InvalidDataException($"Current startup {name} sequence is unknown.");
    private static OriginalStartupDataReference Data(OriginalStartupDataReference? reference, OriginalStartupModelKind kind)
    {
        if (reference is null) throw new InvalidDataException($"Current startup {kind} pointer is unknown.");
        if (reference.Kind != kind) throw new InvalidDataException("Current startup model data has another object kind.");
        _ = Key(reference);
        return reference;
    }
    private static int Key(OriginalStartupDataReference data)
    {
        ArgumentNullException.ThrowIfNull(data);
        int key = data.Identity ?? throw new InvalidDataException("Null model data pointer cannot provide a map key.");
        if (key < 0) throw new InvalidDataException("Negative model data identity is outside the owned profile.");
        return key;
    }
    private SortedDictionary<int, OriginalStartupVisualReference> Map(OriginalStartupModelKind kind)
    {
        int index = (int)kind;
        if ((uint)index >= (uint)_maps.Length) throw new InvalidDataException("Unknown startup model map kind.");
        return _maps[index];
    }
    private static void RequireVisual(OriginalStartupVisualReference visual, string operation)
    { if (!visual.Identity.HasValue) throw new InvalidDataException($"Null {operation} cannot accept a resource operation."); }
}
