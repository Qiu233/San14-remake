using System.Runtime.CompilerServices;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>
/// Owns an exact source mip chain on the global RenderingDevice. Does not
/// infer original color space, material semantics, UVs or physical layer names.
/// Compatibility/headless rendering has no RD and must use its explicit preview path.
/// </summary>
public sealed class TerrainDeviceTexture : IDisposable, IAsyncDisposable
{
    public Texture Texture { get; }
    public TerrainTextureBinding Binding { get; }
    public G1TextureImage SourceImage { get; }
    public Rid DeviceTextureRid { get; }
    private Task? _disposal;

    private TerrainDeviceTexture(Texture texture, TerrainTextureBinding binding,
        G1TextureImage sourceImage, Rid rid)
        => (Texture, Binding, SourceImage, DeviceTextureRid) = (texture, binding, sourceImage, rid);

    public static async Task<TerrainDeviceTexture> BuildAsync(
        LoadedTerrainTextureGroup group, uint slot, bool srgb = false, bool allowBc1RgbView = false)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (RenderingServer.GetRenderingDevice() is null)
            throw new NotSupportedException("Exact source mip upload requires a RenderingDevice renderer.");
        TerrainTextureBinding binding = TerrainTexturePreparation.Select(group, slot);
        var prepared = TerrainTexturePreparation.PrepareDevice(binding, srgb, allowBc1RgbView);
        LoadedRdbAsset source = binding.Resource.Source;
        G1TextureImage image = prepared.Image;
        var data = new Godot.Collections.Array<byte[]>();
        Rid rid = default;
        Texture? texture = null;
        try
        {
            foreach (var layer in prepared.Layers) data.Add(layer.ToArray());
            rid = await OnRenderThreadAsync(rd =>
            {
                RenderingDevice.TextureUsageBits usage = RenderingDevice.TextureUsageBits.SamplingBit |
                    RenderingDevice.TextureUsageBits.CanCopyFromBit;
                (RenderingDevice.DataFormat Linear, RenderingDevice.DataFormat? Color) formats = image.PixelFormat switch
                {
                    0x00 => (RenderingDevice.DataFormat.R8G8B8A8Unorm, RenderingDevice.DataFormat.R8G8B8A8Srgb),
                    0x01 => (RenderingDevice.DataFormat.B8G8R8A8Unorm, RenderingDevice.DataFormat.B8G8R8A8Srgb),
                    0x02 => (RenderingDevice.DataFormat.R32Sfloat, null),
                    0x59 => (RenderingDevice.DataFormat.Bc1RgbUnormBlock, RenderingDevice.DataFormat.Bc1RgbSrgbBlock),
                    0x5B => (RenderingDevice.DataFormat.Bc3UnormBlock, RenderingDevice.DataFormat.Bc3SrgbBlock),
                    _ => throw new InvalidDataException("Unsupported source texture format.")
                };
                RenderingDevice.DataFormat linear = formats.Linear;
                RenderingDevice.DataFormat? color = formats.Color;
                RenderingDevice.DataFormat encoding = srgb ? color!.Value : linear;
                if (!rd.TextureIsFormatSupportedForUsage(encoding, usage))
                    throw new NotSupportedException("This RenderingDevice cannot sample/read back the requested source format.");
                using var format = new RDTextureFormat
                {
                    Format = encoding,
                    TextureType = image.LayerCount > 1 ? RenderingDevice.TextureType.Type2DArray : RenderingDevice.TextureType.Type2D,
                    Width = (uint)image.Width, Height = (uint)image.Height, Depth = 1,
                    ArrayLayers = (uint)image.LayerCount, Mipmaps = (uint)image.MipCount,
                    UsageBits = usage
                };
                // Allow Godot's material color-space views without re-encoding data.
                if (color is RenderingDevice.DataFormat colorFormat)
                {
                    format.AddShareableFormat(linear);
                    format.AddShareableFormat(colorFormat);
                }
                using var view = new RDTextureView();
                Rid created = rd.TextureCreate(format, view, data);
                if (!created.IsValid) throw new InvalidDataException("RenderingDevice source texture creation failed.");
                try
                {
                    using RDTextureFormat actual = rd.TextureGetFormat(created);
                    if (actual.Width != image.Width || actual.Height != image.Height ||
                        actual.ArrayLayers != image.LayerCount || actual.Mipmaps != image.MipCount ||
                        actual.Format != encoding)
                        throw new InvalidDataException("RenderingDevice did not retain the source texture shape/encoding.");
                    return created;
                }
                catch { rd.FreeRid(created); throw; }
            });
            texture = image.LayerCount > 1
                ? new Texture2DArrayRD { TextureRdRid = rid }
                : new Texture2Drd { TextureRdRid = rid };
            // The wrapper setter itself queues a render-thread operation. Keep it
            // alive and wait for that operation before exposing its RID/shape.
            bool attached = await OnRenderThreadAsync(_ => texture is TextureLayeredRD array
                ? array.TextureRdRid == rid : ((Texture2Drd)texture).TextureRdRid == rid);
            if (!attached) throw new InvalidDataException("RenderingServer did not attach the source device texture.");
            texture.ResourceName = $"TerrainDeviceSource_{source.FileId:X8}_Image{binding.TextureIndex}";
            texture.SetMeta("table_resource_uri", group.TableSource.ResourceUri);
            texture.SetMeta("table_decoded_sha256", group.TableSource.DecodedSha256);
            texture.SetMeta("slot", slot);
            texture.SetMeta("proxy_object_id", binding.ProxyObject.ObjectId);
            texture.SetMeta("source_resource_uri", source.ResourceUri);
            texture.SetMeta("source_decoded_sha256", source.DecodedSha256);
            texture.SetMeta("source_is_override", source.OverrideSource is not null || group.TableSource.OverrideSource is not null);
            texture.SetMeta("image_index", binding.TextureIndex);
            texture.SetMeta("source_mip_count", image.MipCount);
            texture.SetMeta("source_pixel_format", image.PixelFormat);
            texture.SetMeta("bc1_rgb_only_view", image.PixelFormat == 0x59);
            texture.SetMeta("uploaded_mip_count", image.MipCount);
            texture.SetMeta("uploaded_layer_count", image.LayerCount);
            texture.SetMeta("uploaded_as_array", image.LayerCount > 1);
            texture.SetMeta("uploaded_rd_srgb", srgb);
            texture.SetMeta("original_color_space_verified", false);
            texture.SetMeta("original_shader_binding_verified", false);
            texture.SetMeta("resized", false);
            texture.SetMeta("recompressed", false);
            texture.SetMeta("generated_mipmaps", false);
            return new(texture, binding, image, rid);
        }
        catch
        {
            Detach(texture);
            if (rid.IsValid) await OnRenderThreadAsync(rd => { rd.FreeRid(rid); return true; });
            texture?.Dispose();
            throw;
        }
        finally { data.Clear(); }
    }

    /// <summary>Returns a full encoded mip chain for diagnostics, never an image conversion.</summary>
    public Task<byte[]> ReadEncodedLayerAsync(uint layer)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        if (layer >= SourceImage.LayerCount) throw new ArgumentOutOfRangeException(nameof(layer));
        return OnRenderThreadAsync(rd => rd.TextureGetData(DeviceTextureRid, layer));
    }

    internal static Task<T> OnRenderThreadAsync<T>(Func<RenderingDevice, T> action,
        [CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timing = RenderThreadSchedulingDiagnostics.Enqueued(completion.Task, member, file, line);
        RenderingServer.CallOnRenderThread(Callable.From(() =>
        {
            timing?.CallbackStarted();
            try
            {
                RenderingDevice rd = RenderingServer.GetRenderingDevice()
                    ?? throw new NotSupportedException("The renderer has no RenderingDevice.");
                T result = action(rd);
                timing?.CallbackFinished();
                completion.SetResult(result);
            }
            catch (Exception error) { timing?.CallbackFinished(failed: true); completion.SetException(error); }
        }));
        return completion.Task;
    }

    private Task BeginDisposal()
    {
        if (_disposal is not null) return _disposal;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _disposal = completion.Task;
        _ = DisposeCoreAsync(completion);
        return _disposal;
    }
    private async Task DisposeCoreAsync(TaskCompletionSource completion)
    {
        try
        {
            Detach(Texture);
            // Detach is queued too. Free the underlying RID after it, and retain
            // the wrapper until both operations have run on the render thread.
            await OnRenderThreadAsync(rd => { rd.FreeRid(DeviceTextureRid); return true; });
            Texture.Dispose();
            completion.SetResult();
        }
        catch (Exception error) { completion.SetException(error); }
    }
    private static void Detach(Texture? texture)
    {
        if (texture is TextureLayeredRD array) array.TextureRdRid = default;
        else if (texture is Texture2Drd single) single.TextureRdRid = default;
    }
    public void Dispose() => _ = BeginDisposal();
    public ValueTask DisposeAsync() => new(BeginDisposal());
}
