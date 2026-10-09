using System.Collections.Concurrent;
using San14.Data;

/// <summary>Actual partially built dependencies remain strongly owned after
/// retirement failure. Earlier borrowers retire before lighting/compiler.
/// A retry resumes the failed stage; successful stages are not replayed.</summary>
public sealed class SceneViewNativeMapPartialRetirementPackage
{
    private static readonly ConcurrentDictionary<string, SceneViewNativeMapPartialRetirementPackage> failed = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private Vkd3dShaderCompiler? compiler;
    private readonly IAsyncDisposable?[] borrowers;
    private int stage;
    public string OwnerIdentity { get; }
    public int CompletedRetirementStages => stage;
    public bool Retired => stage == borrowers.Length + 1;
    public static IReadOnlyList<SceneViewNativeMapPartialRetirementPackage> FailedPackages => failed.Values.ToArray();

    internal SceneViewNativeMapPartialRetirementPackage(string owner,
        Vkd3dShaderCompiler? compiler, NativeMapArmyPopulationRenderer? army,
        NativeMapModelPopulationRenderer? population, IAsyncDisposable? sky,
        MapSourceCreatedLightingDeviceOwner? lighting)
    { OwnerIdentity = owner; this.compiler = compiler; borrowers = [army, population, sky, lighting]; }

    internal static void RetainFailure(SceneViewNativeMapPartialRetirementPackage value)
    {
        if (value.Retired || !failed.TryAdd(value.OwnerIdentity, value))
            throw new InvalidOperationException("Partial retirement owner is already retired or registered.");
    }

    public async Task RetireAsync()
    {
        await gate.WaitAsync();
        try
        {
            while (stage < borrowers.Length)
            {
                if (borrowers[stage] is { } owner) await owner.DisposeAsync();
                ++stage;
            }
            if (stage == borrowers.Length)
            { compiler?.Dispose(); compiler = null; ++stage; }
            failed.TryRemove(OwnerIdentity, out _);
        }
        finally { gate.Release(); }
    }
}

public sealed class SceneViewNativeMapPartialConstructionException : InvalidOperationException
{
    public Exception ConstructionFailure { get; }
    public Exception RetirementFailure { get; }
    public SceneViewNativeMapPartialRetirementPackage RetainedPackage { get; }
    internal SceneViewNativeMapPartialConstructionException(Exception construction,
        Exception retirement, SceneViewNativeMapPartialRetirementPackage package)
        : base("Production construction failed; partial owners remain retained for ordered retirement retry.", construction)
    { ConstructionFailure = construction; RetirementFailure = retirement; RetainedPackage = package; }
}
