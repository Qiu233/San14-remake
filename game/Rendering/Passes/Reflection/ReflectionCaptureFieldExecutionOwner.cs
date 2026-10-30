using San14.Data;

/// <summary>One actually instantiated Field's capture conversion and filter
/// borrowers. Keeps shader-global indices coherent across multiple wrappers;
/// actual Scene capture/shadow rendering remains an earlier real producer.</summary>
public sealed class ReflectionCaptureFieldExecutionOwner : IAsyncDisposable
{
    public ReflectionCaptureFieldResources Field { get; }
    public ReflectionCaptureContentOwner Capture { get; }
    public ReflectionCaptureFilterOwner Filter { get; }
    private Task? disposal;
    private ReflectionCaptureFieldExecutionOwner(ReflectionCaptureFieldResources field,
        ReflectionCaptureContentOwner capture, ReflectionCaptureFilterOwner filter)
        => (Field, Capture, Filter) = (field, capture, filter);

    public static async Task<ReflectionCaptureFieldExecutionOwner> BuildAsync(ReflectionCaptureFieldResources field,
        ReflectionCaptureFilterSharedShaderState shader, Vkd3dShaderCompiler compiler,
        ReflectionCaptureFilterConfiguration actualConfiguration)
    {
        // FD9230's successful resource setup resets the cursor before the
        // first capture. Do not allocate a fresh wrapper after capturing.
        var filter = await ReflectionCaptureFilterOwner.BuildAsync(field, shader, compiler, actualConfiguration);
        try
        {
            var capture = await ReflectionCaptureContentOwner.BuildAsync(shader.Source, compiler, field);
            return new(field, capture, filter);
        }
        catch { await filter.DisposeAsync(); throw; }
    }

    public async Task ConvertActualCaptureAsync(int actualSourceOrdinal,
        IReadOnlyList<ReflectionCapturedCube?> actualCaptureColor2Color3Color1)
    {
        ArgumentNullException.ThrowIfNull(actualCaptureColor2Color3Color1);
        var cubes = actualCaptureColor2Color3Color1.ToArray();
        await Filter.ShaderState.Gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposal is not null, this);
            await Capture.ConvertCapturedCellAsync(actualSourceOrdinal, cubes);
            if (Field.Instance.WrapperNextIndex < 0)
                Filter.ShaderState.RecordSubmittedInput((int)Field.Plan.AtlasLayers - 1);
            else
                for (int index = cubes.Length - 1; index >= 0; index--)
                    if (cubes[index] is not null) { Filter.ShaderState.RecordSubmittedInput(index); break; }
        }
        finally { Filter.ShaderState.Gate.Release(); }
    }

    public ValueTask DisposeAsync() => new(disposal ??= RetireAsync());
    private async Task RetireAsync()
    { await Capture.DisposeAsync(); await Filter.DisposeAsync(); }
}
