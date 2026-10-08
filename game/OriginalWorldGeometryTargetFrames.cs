using San14.Data;

/// <summary>The original geometry scopes use two attachment sets over the
/// same four color textures and depth/stencil image. The scene owns storage;
/// this owner adds the node48 RR2 view and six-target framebuffer. Selection,
/// geometry submissions and depth-copy execution belong to the ordered caller.</summary>
public sealed class OriginalWorldGeometryTargetFrames : IAsyncDisposable
{
    private OriginalWorldGeometryTargetFrames(OriginalArmyWorldGraphicsTargetSource source,
        OriginalRenderTargetFrame army, OriginalRenderTargetFrame ordinary, OriginalOrdinaryRs4TargetFrameOwner? rs4)
        => (ArmySource, ArmyFrame, OrdinaryFrame, Rs4) = (source, army, ordinary, rs4);

    public OriginalArmyWorldGraphicsTargetSource ArmySource { get; }
    public OriginalRenderTargetFrame ArmyFrame { get; }
    public OriginalRenderTargetFrame OrdinaryFrame { get; }
    public OriginalOrdinaryRs4TargetFrameOwner? Rs4 { get; }

    public static async Task<OriginalWorldGeometryTargetFrames> BuildAsync(
        OriginalRenderTargetFrame actualNode53Frame, LoadedOriginalOrdinaryRs4Source? actualRs4Source = null)
    {
        ArgumentNullException.ThrowIfNull(actualNode53Frame);
        var ordinary = actualNode53Frame.Source;
        if (ordinary.Node.Index != 53 || ordinary.Flag != 0x46 || ordinary.ColorTargets.Count != 5 ||
            ordinary.DepthObjectNameHash != 0xE13DDB4F || ordinary.DepthSurfaceId != 0)
            throw new InvalidDataException("World geometry storage must be the actual source node53 five-target scope.");
        var source = OriginalArmyWorldGraphicsTargetSource.Read(OriginalRenderGraphTargetResources.Read(
            ordinary.Graph, 48, ordinary.TextureObjects));
        if (!ordinary.ColorTargets.Select(t => (t.Slot, t.ObjectNameHash, t.SurfaceId))
            .SequenceEqual(source.Source.ColorTargets.Take(5).Select(t => (t.Slot, t.ObjectNameHash, t.SurfaceId))))
            throw new InvalidDataException("Original node48 and node53 must share their first five surfaces.");
        var army = await OriginalRenderTargetFrame.BuildAttachmentFrameAsync(source.Source, actualNode53Frame);
        try
        {
            var rs4 = actualRs4Source is null ? null :
                await OriginalOrdinaryRs4TargetFrameOwner.BuildAsync(actualRs4Source, actualNode53Frame);
            return new(source, army, actualNode53Frame, rs4);
        }
        catch { await army.DisposeAsync(); throw; }
    }

    /// <summary>Retire all draws which borrow ArmyFrame first. OrdinaryFrame
    /// remains owned by the scene and is never released here.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Rs4 is not null) await Rs4.DisposeAsync();
        await ArmyFrame.DisposeAsync();
    }
}
