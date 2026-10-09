using System.Numerics;
using System.Runtime.InteropServices;

/// <summary>Same-request CPU CUnit position, independent of model roots.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct D3D11ArmyLabelUnitPosition
{
    public readonly int UnitId;
    public readonly Vector4 Position;

    public D3D11ArmyLabelUnitPosition(int unitId, Vector4 position)
    {
        UnitId = unitId;
        Position = position;
    }
}
