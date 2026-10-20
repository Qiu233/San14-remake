using Godot;
using San14.Data;

public partial class MapSceneView
{
    private D3D11CommandMapPublication? _commandMapDisplay;
    private ulong _commandMapRevision, _commandMapProviderGeneration;

    internal void BeginAdministrationMapDisplay(AreaAdministrationDraft draft)
    {
        if (!IsPlayableMapReady || _commandMapDisplay is { Present: true })
            throw new InvalidOperationException("Command map display requires an available map owner.");
        var data = D3D11CommandMapData.Capture(draft);
        ulong revision = checked(++_commandMapRevision);
        _commandMapDisplay = new(data, revision, revision, checked(++_commandMapProviderGeneration));
        ScheduleD3D11MapFrame();
    }

    internal void EndAdministrationMapDisplay()
    {
        if (_commandMapDisplay is not { Present: true }) return;
        _commandMapDisplay = new(null, checked(++_commandMapRevision), 0, checked(++_commandMapProviderGeneration));
        ScheduleD3D11MapFrame();
    }

    private void UpdateAdministrationMapHover(InputEvent input)
    {
        if (input is not InputEventMouseMotion motion || _commandMapDisplay is not { Data: { } data } display ||
            _dragging || _panning || _sourceCameraLeftUiPressed || _sourceCameraRightHeld || _sourceCameraMiddleHeld ||
            !TryPickCommandTarget(motion.Position, out int hex)) return;
        // Invalid/no pointer retains the previous focus. Area0 is a real raw
        // value; neither held membership nor Area lookup participates here.
        ushort previous = display.HoverHex < D3D11CommandMapData.CellCount
            ? data.RawAreas[display.HoverHex] : (ushort)0;
        if (data.RawAreas[hex] == previous) return;
        _commandMapDisplay = new(data, checked(++_commandMapRevision), display.DataRevision,
            display.ProviderGeneration, (uint)hex);
        ScheduleD3D11MapFrame();
    }
}
