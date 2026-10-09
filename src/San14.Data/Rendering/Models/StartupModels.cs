namespace San14.Data;

public enum StartupModelKind { Force, District, Troops, ArmyUnit }

/// <summary>A current data-object identity, independent of scenario IDs and
/// native addresses. Aliased pointer slots supply the same physical identity.</summary>
public sealed record StartupDataReference(StartupModelKind Kind, int? Identity);
public sealed record StartupVisualReference(int? Identity);
public sealed record StartupResourceOwnerReference(int? Identity);

public interface IStartupModelQueries
{
    IEnumerable<StartupDataReference?>? ReadStartupForces();
    IEnumerable<StartupDataReference?>? ReadStartupDistricts(StartupDataReference force);
    IEnumerable<StartupDataReference?>? ReadStartupTroops(StartupDataReference district);
    IEnumerable<StartupDataReference?>? ReadStartupArmyUnits(StartupDataReference troops);
    StartupVisualReference? ReadStartupTroopsModel(StartupDataReference troops);
    void WriteStartupTroopsModel(StartupDataReference troops, StartupVisualReference visual);
}

/// <summary>The host owns rendering/resource lifetime. No empty resource
/// factory or destructor is installed implicitly by the data initializer.</summary>
public interface IStartupModelResources
{
    StartupVisualReference? CreateStartupVisual(StartupDataReference data);
    void ConfigureStartupTroopsVisual(StartupVisualReference visual);
    StartupResourceOwnerReference? ReadStartupVisualOwner(StartupVisualReference visual);
    void RemoveStartupTroopsVisual(StartupModels model, StartupVisualReference visual, int mode);
    void ReleaseStartupTroopsVisual(StartupResourceOwnerReference owner, StartupVisualReference visual);
}

/// <summary>Owned equivalent of the model80/90/A0/B0 maps. Keys are current
/// object identities, never raw scenario IDs. Presence and null payload are
/// distinct. The four source tables do not constrain future map capacities.</summary>
public sealed class StartupModels
{
    private readonly SortedDictionary<int, StartupVisualReference>[] _maps =
        [new(), new(), new(), new()];
    private readonly IStartupModelQueries _queries;
    private readonly IStartupModelResources _resources;

    public StartupModels(IStartupModelQueries queries, IStartupModelResources resources)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(resources);
        _queries = queries;
        _resources = resources;
    }

    public int Count(StartupModelKind kind) => Map(kind).Count;
    public bool Contains(StartupDataReference data) => Map(data.Kind).ContainsKey(Key(data));
    public StartupVisualReference Read(StartupDataReference data)
        => Map(data.Kind).GetValueOrDefault(Key(data)) ?? new(null);

    /// <summary>Insertion helpers keep an existing entry even if its value is
    /// null. Troops30 is written only after successful A0 insertion.</summary>
    public bool Insert(StartupDataReference data, StartupVisualReference visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        int key = Key(data);
        if (!Map(data.Kind).TryAdd(key, visual)) return false;
        if (data.Kind == StartupModelKind.Troops) _queries.WriteStartupTroopsModel(data, visual);
        return true;
    }

    /// <summary>A1240: map-node clear does not destroy visual values. With
    /// argument0, troops resources are removed/released before A0 is cleared.</summary>
    public void Reset(int argument)
    {
        Map(StartupModelKind.Force).Clear();
        Map(StartupModelKind.District).Clear();
        if (argument == 0)
        {
            foreach (var pair in Map(StartupModelKind.Troops))
            {
                StartupVisualReference saved = pair.Value;
                RequireVisual(saved, "reset troops value");
                StartupResourceOwnerReference owner = _resources.ReadStartupVisualOwner(saved)
                    ?? throw new InvalidDataException("Current startup visual owner is unknown.");
                if (!owner.Identity.HasValue) throw new InvalidDataException("Current startup visual owner is null at resource release.");
                _resources.RemoveStartupTroopsVisual(this, saved, 2);
                _resources.ReleaseStartupTroopsVisual(owner, saved);
                _queries.WriteStartupTroopsModel(new(StartupModelKind.Troops, pair.Key), new(null));
            }
            Map(StartupModelKind.Troops).Clear();
        }
        Map(StartupModelKind.ArmyUnit).Clear();
    }

    /// <summary>D20F0 does not consume its argument. All sequences stay live:
    /// factory callbacks can change later pointers, links or membership.</summary>
    public int Build(int argument)
    {
        foreach (StartupDataReference? candidateForce in Sequence(_queries.ReadStartupForces(), "forces"))
        {
            StartupDataReference force = Data(candidateForce, StartupModelKind.Force);
            EnsureMapped(force);
            foreach (StartupDataReference? candidateDistrict in Sequence(_queries.ReadStartupDistricts(force), "force districts"))
            {
                StartupDataReference district = Data(candidateDistrict, StartupModelKind.District);
                EnsureMapped(district);
                foreach (StartupDataReference? candidateTroops in Sequence(_queries.ReadStartupTroops(district), "district troops"))
                {
                    StartupDataReference troops = Data(candidateTroops, StartupModelKind.Troops);
                    if (!Read(troops).Identity.HasValue)
                    {
                        StartupVisualReference visual = _queries.ReadStartupTroopsModel(troops)
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
                    foreach (StartupDataReference? candidateArmy in Sequence(_queries.ReadStartupArmyUnits(troops), "troops army units"))
                        EnsureMapped(Data(candidateArmy, StartupModelKind.ArmyUnit));
                }
            }
        }
        return 1;
    }

    private void EnsureMapped(StartupDataReference data)
    {
        // Native lookup-null calls the insert helper, which first rejects any
        // existing key. It must not create or overwrite its null-valued entry.
        if (Contains(data)) return;
        StartupVisualReference visual = _resources.CreateStartupVisual(data)
            ?? throw new InvalidDataException("Startup model allocation outcome is unknown.");
        Insert(data, visual);
    }
    private static IEnumerable<StartupDataReference?> Sequence(IEnumerable<StartupDataReference?>? values, string name)
        => values ?? throw new InvalidDataException($"Current startup {name} sequence is unknown.");
    private static StartupDataReference Data(StartupDataReference? reference, StartupModelKind kind)
    {
        if (reference is null) throw new InvalidDataException($"Current startup {kind} pointer is unknown.");
        if (reference.Kind != kind) throw new InvalidDataException("Current startup model data has another object kind.");
        _ = Key(reference);
        return reference;
    }
    private static int Key(StartupDataReference data)
    {
        ArgumentNullException.ThrowIfNull(data);
        int key = data.Identity ?? throw new InvalidDataException("Null model data pointer cannot provide a map key.");
        if (key < 0) throw new InvalidDataException("Negative model data identity is outside the owned profile.");
        return key;
    }
    private SortedDictionary<int, StartupVisualReference> Map(StartupModelKind kind)
    {
        int index = (int)kind;
        if ((uint)index >= (uint)_maps.Length) throw new InvalidDataException("Unknown startup model map kind.");
        return _maps[index];
    }
    private static void RequireVisual(StartupVisualReference visual, string operation)
    { if (!visual.Identity.HasValue) throw new InvalidDataException($"Null {operation} cannot accept a resource operation."); }
}
