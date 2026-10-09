using System.Numerics;

namespace San14.Data;

/// <summary>963D80's actual24-byte frontend rectangle, in its native field
/// order. Depth endpoints survive both D75E50 and scope exit.</summary>
public readonly record struct WorldGeometryLateNativeViewport(
    int Width, int Height, int Left, int Top, float MinimumDepth, float MaximumDepth);

/// <summary>Current963CB0 wrapper, or95B0C0 fallback selected by the actual
/// frontend owner. The successful scope retains this wrapper's extent.</summary>
public sealed record WorldGeometryLateViewportTarget(int Width50, int Height54, string OwnerIdentity);

/// <summary>Source E6BF80/D75E50 scope. Its exit fills the saved target extent
/// rather than restoring an arbitrary previous viewport. Node59's parent-row
/// restoration is a separate target-stack operation.</summary>
public sealed class WorldGeometryLateViewportScopeState
{
    private readonly Func<WorldGeometryLateNativeViewport> readCurrent;
    private readonly Action<WorldGeometryLateNativeViewport> writeViewportAndScissor;
    private WorldGeometryLateViewportTarget? retained;
    public WorldGeometryLateViewportScopeState(
        Func<WorldGeometryLateNativeViewport> actual963D80,
        Action<WorldGeometryLateNativeViewport> actual95C070)
    {
        ArgumentNullException.ThrowIfNull(actual963D80); ArgumentNullException.ThrowIfNull(actual95C070);
        readCurrent = actual963D80; writeViewportAndScissor = actual95C070;
    }
    public void Begin(WorldGeometryLateViewportTarget? actualSelectedWrapper,
        Vector4? actualSelectedView1540, uint actualContextFlags3F0)
    {
        if (retained is not null) throw new InvalidOperationException("The current viewport scope has not exited.");
        if (actualSelectedWrapper is null) return; // D75E50 returnsfalse for null wrapper.
        Vector4 rect = actualSelectedView1540 ?? new(0,0,1,1);
        if (actualSelectedView1540 is not null && (actualContextFlags3F0 & 0x20) == 0)
        { rect.X = 0; rect.Y = 0; }
        static int SourceRound(float extent, float scalar)
        { float product = extent * scalar; return unchecked((int)MathF.Floor(product + .5f)); }
        var prior = readCurrent();
        var next = new WorldGeometryLateNativeViewport(
            SourceRound(actualSelectedWrapper.Width50,rect.Z),SourceRound(actualSelectedWrapper.Height54,rect.W),
            SourceRound(actualSelectedWrapper.Width50,rect.X),SourceRound(actualSelectedWrapper.Height54,rect.Y),
            prior.MinimumDepth,prior.MaximumDepth);
        writeViewportAndScissor(next); retained = actualSelectedWrapper;
    }
    public void Exit()
    {
        if (retained is not { } wrapper) return;
        var current = readCurrent();
        writeViewportAndScissor(new(wrapper.Width50,wrapper.Height54,0,0,current.MinimumDepth,current.MaximumDepth));
        retained = null;
    }
}
