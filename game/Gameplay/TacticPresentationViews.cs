using System.Numerics;
using San14.Data;

namespace San14.Client.Gameplay;

/// <summary>The map publishes immutable controller and renderer facts. A
/// campaign worker borrows this cell without reading engine nodes or a World.</summary>
internal sealed class TacticPresentationViews
{
    private sealed record Frame(int? ZoomBand, bool? ClipBypass, Matrix4x4? WorldToClip,
        IReadOnlyList<D3D11ArmyLabelUnitPosition>? ArmyPositions);
    private Frame? current;

    internal void Publish(MainMapCameraFrame controller, bool? clipBypass,
        MainMapCameraFrame? renderedCamera, IReadOnlyList<D3D11ArmyLabelUnitPosition>? armyPositions)
    {
        // Band and matrix belong to one frozen camera attempt. The controller
        // can prepare a newer frame while the renderer is still using this one.
        var camera = renderedCamera ?? controller;
        Volatile.Write(ref current, new(camera.ZoomSelection.Band1E0, clipBypass,
            renderedCamera?.Camera.SourceWorldToProjection, armyPositions));
    }

    internal void Clear() => Volatile.Write(ref current, null);

    internal OrdinaryTacticDirectionHostSnapshot? Read(int unitId)
    {
        var frame = Volatile.Read(ref current);
        if (frame is null) return null;
        Vector4? position = null;
        if (frame.ArmyPositions is { } positions)
        {
            // The actual renderer lookup can return no CUnit. Its caller
            // retains this initialized value; a missing receipt is unknown.
            position = new Vector4(0, 0, 0, 1);
            foreach (var row in positions)
                if (row.UnitId == unitId) { position = row.Position; break; }
        }
        return new(frame.ZoomBand, frame.WorldToClip is null ? null : true,
            frame.ClipBypass, frame.WorldToClip, position);
    }
}
