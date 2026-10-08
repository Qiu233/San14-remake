using Godot;
using San14.Data;

public sealed record OriginalDeferredGbufferDeviceInput(OriginalDeferredTextureInput Source,
    Rid ShaderView, RenderingDevice.DataFormat Format, Vector2I Extent, uint Layers);

/// <summary>Connects the source node's five named inputs to the full device
/// shader views, retaining array layers. No cleared or synthetic textures are supplied.</summary>
public static class OriginalDeferredGbufferInputs
{
    public static Task<IReadOnlyList<OriginalDeferredGbufferDeviceInput>> ReadAsync(
        LoadedOriginalDeferredTexturePlan plan, OriginalRenderTargetFrame frame)
    {
        ArgumentNullException.ThrowIfNull(plan); ArgumentNullException.ThrowIfNull(frame);
        if (plan.Graph.Source.ResourceUri != frame.Source.Graph.Source.ResourceUri ||
            plan.Graph.Source.DecodedSha256 != frame.Source.Graph.Source.DecodedSha256 ||
            plan.TextureObjects.Source.ResourceUri != frame.Source.TextureObjects.Source.ResourceUri ||
            plan.TextureObjects.Source.DecodedSha256 != frame.Source.TextureObjects.Source.DecodedSha256 ||
            plan.Inputs.Count != 5)
            throw new InvalidDataException("Deferred inputs and terrain framebuffer differ in source identity.");
        var rows = new List<OriginalDeferredGbufferDeviceInput>();
        foreach (var input in plan.Inputs)
        {
            if (!frame.Textures.TryGetValue(input.Texture.Object.ObjectId, out var device) ||
                device.Source.Object.ObjectId != input.Texture.Object.ObjectId ||
                device.Source.Kind != input.Texture.Kind || device.Source.PixelFormat != input.Texture.PixelFormat ||
                device.Source.ColorSpace != input.Texture.ColorSpace || device.Source.OutputColorSpace != input.Texture.OutputColorSpace ||
                device.Source.LevelNum != input.Texture.LevelNum || device.Source.ArrayDepth != input.Texture.ArrayDepth)
                throw new InvalidDataException($"Deferred {input.Role} source has no matching declared device texture.");
            rows.Add(new(input, device.ShaderViewRid, device.ShaderFormat, device.ExplicitExtent,
                input.Texture.Kind == OriginalRenderTextureKind.Texture2D ? 1u : (uint)input.Texture.ArrayDepth!.Value));
        }
        if (!rows.Select(r => r.Source.Role).Order().SequenceEqual(new[] { "AB", "D1", "NS", "RR", "RS" }))
            throw new InvalidDataException("Deferred input roles differ from the original five-target contract.");
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync<IReadOnlyList<OriginalDeferredGbufferDeviceInput>>(rd => {
            foreach (var row in rows)
            {
                if (!rd.TextureIsValid(row.ShaderView)) throw new InvalidDataException("Deferred shader view is not a live texture.");
                using var actual = rd.TextureGetFormat(row.ShaderView);
                var expectedType = row.Source.Texture.Kind == OriginalRenderTextureKind.Texture2D
                    ? RenderingDevice.TextureType.Type2D : RenderingDevice.TextureType.Type2DArray;
                if (actual.Format != row.Format || actual.Width != row.Extent.X || actual.Height != row.Extent.Y ||
                    actual.ArrayLayers != row.Layers || actual.Mipmaps != 1 || actual.TextureType != expectedType ||
                    !actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                    throw new InvalidDataException($"Deferred {row.Source.Role} device view shape/format differs from its original plan.");
            }
            return rows.AsReadOnly();
        });
    }
}
