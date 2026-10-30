using Godot;
using San14.Data;

public partial class MapSceneView
{
    private LoadedUniqueTacticEffectInputs? _uniqueEffectInputs;
    private readonly HashSet<UniqueTacticEffectHost> _uniqueEffectHosts = [];
    private ulong _uniqueEffectGeneration;

    internal UniqueTacticEffectHost CreateUniqueTacticEffectHost(Control back, Control front, Func<int, byte> sourceAlpha, bool singleBackLayer = false)
    {
        var context = _d3d11ResourceContext ?? throw new InvalidOperationException("unique_effect.resource_context_unavailable");
        var shared = _captureEffects ?? throw new InvalidOperationException("unique_effect.shared_manager_unavailable");
        if (_d3d11PresentedInput?.CurrentSeason is null)
            throw new InvalidOperationException("unique_effect.current_camera_level_unavailable");
        _uniqueEffectInputs ??= LoadedUniqueTacticEffectInputs.Load(_repository, _text.SourceLocale);
        string libraryDefault = OS.HasFeature("editor")
            ? "res://../native/San14.Render.D3D11/bin/x64/Release/San14.Render.D3D11.dll"
            : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!, "native", "windows-x64", "San14.Render.D3D11.dll");
        string configured = System.Environment.GetEnvironmentVariable("SAN14_RENDER_NATIVE_LIBRARY")
            ?? ProjectSettings.GetSetting("san14/render/native_library", libraryDefault).AsString();
        string library = Path.GetFullPath(ProjectSettings.GlobalizePath(configured));
        var host = new UniqueTacticEffectHost(_repository, context, library, shared, back, front,
            () => _d3d11PresentedInput?.CurrentSeason?.Mode.EcB0Index ??
                throw new InvalidOperationException("unique_effect.current_camera_level_unavailable"),
            checked(++_uniqueEffectGeneration), sourceAlpha, singleBackLayer);
        _uniqueEffectHosts.Add(host); return host;
    }

    internal Task AdvanceUniqueTacticEffects(UniqueTacticEffectHost host, double elapsedSeconds, int framesPerSecond)
    {
        if (!_uniqueEffectHosts.Contains(host)) throw new InvalidOperationException("unique_effect.foreign_host");
        var inputs = _uniqueEffectInputs ?? throw new InvalidOperationException("unique_effect.ui_inputs_unavailable");
        return host.AdvanceAsync(elapsedSeconds, framesPerSecond, selector =>
        {
            var extent = host.TargetSize(selector);
            var frame = inputs.CreateFrame(selector, extent.X, extent.Y);
            if (frame.Missing.Count != 0) throw new InvalidOperationException(string.Join("; ", frame.Missing));
            return new(frame.CpuCamera, frame.PacketInputs, frame.ReverseDepth, frame.EmitterTransformProvider);
        });
    }

    internal async Task RetireUniqueTacticEffectHostAsync(UniqueTacticEffectHost host)
    {
        await host.DisposeAsync();
        _uniqueEffectHosts.Remove(host);
    }

    // Must precede disposal of the map's shared arena/resource context.
    internal async Task RetireUniqueTacticEffectsAsync()
    {
        await RetireAllStoryEffectsAsync();
        var retiring = _uniqueEffectHosts.ToArray(); _uniqueEffectHosts.Clear();
        foreach (var host in retiring) await host.DisposeAsync();
        _uniqueEffectInputs = null;
    }
}
