using Godot;
using System.Collections.Concurrent;

/// <summary>Strongly retains partial nested GPU borrowers in dependency
/// order. A failed dispose leaves that borrower and every later dependency
/// reachable; retry resumes at that exact stage.</summary>
public sealed class OriginalNativeMapBorrowerRetirementPackage : IAsyncDisposable
{
    private static readonly ConcurrentDictionary<string, OriginalNativeMapBorrowerRetirementPackage> failed = new();
    private readonly IAsyncDisposable?[] borrowers;
    private readonly SemaphoreSlim gate = new(1, 1);
    private int stage;
    public string OwnerIdentity { get; }
    public bool Retired => stage == borrowers.Length;
    public static IReadOnlyList<OriginalNativeMapBorrowerRetirementPackage> FailedPackages => failed.Values.ToArray();
    public OriginalNativeMapBorrowerRetirementPackage(string identity, params IAsyncDisposable?[] orderedBorrowers)
    { OwnerIdentity = identity; borrowers = orderedBorrowers.ToArray(); }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            while (stage < borrowers.Length)
            { if (borrowers[stage] is { } owner) await owner.DisposeAsync(); ++stage; }
            failed.TryRemove(OwnerIdentity, out _);
        }
        finally { gate.Release(); }
    }

    public void RetainFailure()
    {
        if (Retired || !failed.TryAdd(OwnerIdentity, this))
            throw new InvalidOperationException("Nested retirement owner is already retired or registered.");
    }
}

public sealed class OriginalNativeMapBorrowerConstructionException : InvalidOperationException
{
    public Exception ConstructionFailure { get; }
    public Exception RetirementFailure { get; }
    public OriginalNativeMapBorrowerRetirementPackage RetainedPackage { get; }
    public OriginalNativeMapBorrowerConstructionException(Exception construction, Exception retirement,
        OriginalNativeMapBorrowerRetirementPackage package)
        : base("Native construction failed; nested GPU borrowers remain owned for retirement retry.", construction)
    { ConstructionFailure = construction; RetirementFailure = retirement; RetainedPackage = package; }
}

internal sealed class OriginalNativeMapOwnedSamplerRetirement(Rid sampler) : IAsyncDisposable
{
    private Rid current = sampler;
    public async ValueTask DisposeAsync()
    {
        if (!current.IsValid) return;
        Rid owned = current;
        await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => { rd.FreeRid(owned); return true; });
        current = default;
    }
}
