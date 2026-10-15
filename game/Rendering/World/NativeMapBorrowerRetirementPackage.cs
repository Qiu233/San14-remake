using Godot;
using System.Collections.Concurrent;

/// <summary>Strongly retains partial nested GPU borrowers in dependency
/// order. A failed dispose leaves that borrower and every later dependency
/// reachable; retry resumes at that exact stage.</summary>
public sealed class NativeMapBorrowerRetirementPackage : IAsyncDisposable
{
    private static readonly ConcurrentDictionary<string, NativeMapBorrowerRetirementPackage> failed = new();
    private readonly IAsyncDisposable?[] borrowers;
    private readonly SemaphoreSlim gate = new(1, 1);
    private int stage;
    public string OwnerIdentity { get; }
    public bool Retired => stage == borrowers.Length;
    public static IReadOnlyList<NativeMapBorrowerRetirementPackage> FailedPackages => failed.Values.ToArray();
    public NativeMapBorrowerRetirementPackage(string identity, params IAsyncDisposable?[] orderedBorrowers)
    { OwnerIdentity = identity; borrowers = orderedBorrowers.ToArray(); }

    internal static NativeMapBorrowerRetirementPackage? CreatePending(string identity,
        IEnumerable<IAsyncDisposable> orderedBorrowers,NativeMapBorrowerRetirementPackage? nested=null)
    {
        var remaining=new List<IAsyncDisposable>();
        if(nested is not null)remaining.Add(nested);
        remaining.AddRange(orderedBorrowers);
        return remaining.Count==0?null:new(identity+"/"+Guid.NewGuid().ToString("N"),remaining.ToArray());
    }

    // The caller assigns the result only after successful retirement. A failed
    // await keeps its existing package field and all remaining borrowers alive.
    internal static async Task<NativeMapBorrowerRetirementPackage?> DrainPendingAsync(
        NativeMapBorrowerRetirementPackage? pending)
    {
        if(pending is not null)await pending.DisposeCoreAsync(retainFailure:true);
        return null;
    }

    public ValueTask DisposeAsync()=>DisposeCoreAsync(retainFailure:false);

    private async ValueTask DisposeCoreAsync(bool retainFailure)
    {
        await gate.WaitAsync();
        try
        {
            while (stage < borrowers.Length)
            { if (borrowers[stage] is { } owner) await owner.DisposeAsync(); ++stage; }
            failed.TryRemove(OwnerIdentity, out _);
        }
        catch
        {
            if(retainFailure&&!ReferenceEquals(failed.GetOrAdd(OwnerIdentity,this),this))
                throw new InvalidOperationException("Nested retirement owner is already retired or registered.");
            throw;
        }
        finally { gate.Release(); }
    }

    public void RetainFailure()
    {
        if (Retired || !failed.TryAdd(OwnerIdentity, this))
            throw new InvalidOperationException("Nested retirement owner is already retired or registered.");
    }
}

public sealed class NativeMapBorrowerConstructionException : InvalidOperationException
{
    public Exception ConstructionFailure { get; }
    public Exception RetirementFailure { get; }
    public NativeMapBorrowerRetirementPackage RetainedPackage { get; }
    public NativeMapBorrowerConstructionException(Exception construction, Exception retirement,
        NativeMapBorrowerRetirementPackage package)
        : base("Native construction failed; nested GPU borrowers remain owned for retirement retry.", construction)
    { ConstructionFailure = construction; RetirementFailure = retirement; RetainedPackage = package; }
}

internal sealed class NativeMapOwnedSamplerRetirement(Rid sampler) : IAsyncDisposable
{
    private Rid current = sampler;
    public async ValueTask DisposeAsync()
    {
        if (!current.IsValid) return;
        Rid owned = current;
        await TerrainDeviceTexture.OnRenderThreadAsync(rd => { rd.FreeRid(owned); return true; });
        current = default;
    }
}
