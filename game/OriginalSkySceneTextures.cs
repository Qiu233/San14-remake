using San14.Data;
using San14.Data.Formats;
using System.Security.Cryptography;

public sealed record OriginalSkySceneTextureSelection(string Key,
    LoadedTextureContainer Source, int ImageIndex, uint NativeViewFormat)
{
    public OriginalStaticTextureBinding? StaticBinding { get; init; }
    public ReadOnlyMemory<byte>? RetainedSamplerDescriptor { get; init; }

    /// <summary>Use the typed, source-resolved sampling identity and its exact
    /// source sampler. Its Camera object key remains distinct from the file ID.</summary>
    public static OriginalSkySceneTextureSelection FromSampling(LoadedOriginalSkySamplingResource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(source.SourceName, source.Resource, source.ImageIndex, source.NativeViewFormat)
        { StaticBinding = source.StaticBinding, RetainedSamplerDescriptor = source.GetSamplerDescriptor() };
    }

    /// <summary>Resolve the exact DOK Static binding already selected by the
    /// caller. The object reference and its file ContextHash stay distinct.</summary>
    public static OriginalSkySceneTextureSelection FromStatic(string explicitKey,
        OriginalStaticTextureBinding binding, uint explicitNativeViewFormat,
        ReadOnlyMemory<byte>? explicitRetainedSampler = null)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return new(explicitKey, binding.Resource, binding.ImageIndex, explicitNativeViewFormat)
        { StaticBinding = binding, RetainedSamplerDescriptor = explicitRetainedSampler };
    }
}

public sealed record OriginalSkySceneTextureMetadata(string SourceResourceUri,
    string SourceSha256, int ImageIndex, uint NativeViewFormat, uint SourceDimension,
    int Width, int Height, int Depth, int MipCount, string SamplerDescriptorSha256);

/// <summary>Lazy, scene-side source texture owner. Explicit selections retain
/// full repository/override provenance, TID and native SRV format; source bytes
/// remain loaded from the local install. Equivalent image/view selections share
/// one device texture, including aliases across Sky2, Cloud and Stars. Dispose
/// all draws/producers borrowing these textures before retiring this owner.</summary>
public sealed class OriginalSkySceneTextures : IAsyncDisposable
{
    public IReadOnlyDictionary<string, OriginalSkySceneTextureSelection> Selections { get; }
    private readonly Dictionary<(string Uri, string Hash, int Image, uint Format), OriginalSkyTextureDevice> _loaded = new();
    private readonly Dictionary<string, G1TextureImage> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _retainedSamplers = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lifetimeGate = new();
    private volatile bool _retiring;
    private Task? _disposal;

    public OriginalSkySceneTextures(IReadOnlyList<OriginalSkySceneTextureSelection> explicitSelections)
    {
        ArgumentNullException.ThrowIfNull(explicitSelections);
        if (explicitSelections.Any(s => s is null || string.IsNullOrWhiteSpace(s.Key) || s.Source is null || s.Source.Source is null ||
            string.IsNullOrWhiteSpace(s.Source.Source.ResourceUri) || s.ImageIndex < 0 ||
            s.RetainedSamplerDescriptor is { Length: not 52 }) ||
            explicitSelections.Select(s => s.Key).Distinct(StringComparer.Ordinal).Count() != explicitSelections.Count)
            throw new InvalidDataException("Sky texture selections require unique explicit keys, complete sources and declared image indices.");
        Selections = new System.Collections.ObjectModel.ReadOnlyDictionary<string, OriginalSkySceneTextureSelection>(
            explicitSelections.ToDictionary(s => s.Key, StringComparer.Ordinal));
        foreach (var selection in explicitSelections)
        {
            ValidateSource(selection);
            var canonical = G1TextureContainerReader.Read(selection.Source.Source.Bytes);
            if (selection.ImageIndex >= canonical.Textures.Count)
                throw new InvalidDataException("Sky texture image index lies outside the complete source bytes.");
            var image = canonical.Textures[selection.ImageIndex];
            if (image.LayerCount != 1 || image.SubresourceLayout is not
                (G1TextureSubresourceLayout.SingleLayerMipChain or G1TextureSubresourceLayout.VolumeMipChain))
                throw new InvalidDataException("Sky selections require a verified 2D or volume mip chain.");
            if ((image.PixelFormat, selection.NativeViewFormat) is not
                ((0x01, 87) or (0x01, 91) or (0x03, 10) or (0x59, 71) or
                 (0x59, 72) or (0x5F, 98) or (0x5F, 99)))
                throw new InvalidDataException("Sky source encoding and explicit native SRV format do not match.");
            if (selection.StaticBinding is { } binding &&
                (!ReferenceEquals(binding.Resource, selection.Source) || binding.ImageIndex != selection.ImageIndex))
                throw new InvalidDataException("Sky selection differs from its explicit Static source binding.");
            _images.Add(selection.Key, image);
            if (selection.RetainedSamplerDescriptor is { } sampler)
                _retainedSamplers.Add(selection.Key, sampler.ToArray());
        }
    }

    /// <summary>Source resolution and the exact sampler can be inspected before
    /// any GPU allocation. Returned sampler bytes never alias the retained copy.</summary>
    public OriginalSkySceneTextureMetadata GetMetadata(string explicitKey)
    {
        var selection = GetSelection(explicitKey); var image = _images[explicitKey];
        return new(selection.Source.Source.ResourceUri, selection.Source.Source.DecodedSha256,
            selection.ImageIndex, selection.NativeViewFormat, image.TextureSystem == 2 ? 8u : 4u,
            image.Width, image.Height, image.Depth, image.MipCount,
            Convert.ToHexString(SHA256.HashData(GetSamplerDescriptor(explicitKey))));
    }

    public byte[] GetSamplerDescriptor(string explicitKey)
    {
        GetSelection(explicitKey);
        return _retainedSamplers.TryGetValue(explicitKey, out var sampler) ? sampler.ToArray()
            : G1TextureSamplerReader.ReadRetainedSource(_images[explicitKey]).EncodeD3D11Descriptor();
    }

    private OriginalSkySceneTextureSelection GetSelection(string explicitKey)
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        if (!Selections.TryGetValue(explicitKey, out var selection))
            throw new InvalidDataException($"No explicit sky texture selection for '{explicitKey}'.");
        ValidateSource(selection);
        return selection;
    }

    private static void ValidateSource(OriginalSkySceneTextureSelection selection)
    {
        if (selection.Source.Source.Bytes is null ||
            Convert.ToHexString(SHA256.HashData(selection.Source.Source.Bytes)) != selection.Source.Source.DecodedSha256)
            throw new InvalidDataException("Sky texture source differs from its retained decoded hash.");
    }

    public async Task<OriginalSkyTextureDevice> GetTextureAsync(string explicitKey)
    {
        var selection = GetSelection(explicitKey);
        var key = (selection.Source.Source.ResourceUri, selection.Source.Source.DecodedSha256,
            selection.ImageIndex, selection.NativeViewFormat);
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            ValidateSource(selection);
            if (!_loaded.TryGetValue(key, out var texture))
            {
                texture = await OriginalSkyTextureDevice.BuildAsync(selection.Source,
                    selection.ImageIndex, selection.NativeViewFormat);
                _loaded.Add(key, texture);
            }
            return texture;
        }
        finally { _gate.Release(); }
    }

    public async Task<OriginalSky2DeviceInput> GetRetainedInputAsync(uint explicitBindPoint, string explicitKey)
    {
        GetSelection(explicitKey);
        return await GetInputAsync(explicitBindPoint, explicitKey,
            G1TextureSamplerReader.ReadRetainedSource(_images[explicitKey]).EncodeD3D11Descriptor());
    }

    public Task<OriginalSky2DeviceInput> GetInputAsync(uint explicitBindPoint, string explicitKey)
        => GetInputAsync(explicitBindPoint, explicitKey, GetSamplerDescriptor(explicitKey));

    /// <summary>Use when the source caller retained a modified sampler rather
    /// than the raw G1T header. The complete52-byte descriptor stays explicit.</summary>
    public async Task<OriginalSky2DeviceInput> GetInputAsync(uint explicitBindPoint, string explicitKey,
        ReadOnlyMemory<byte> explicitSamplerDescriptor)
    {
        if (explicitSamplerDescriptor.Length != 52)
            throw new InvalidDataException("Sky input needs the complete retained source sampler.");
        byte[] sampler = explicitSamplerDescriptor.ToArray();
        var texture = await GetTextureAsync(explicitKey);
        return new(explicitBindPoint, texture.Image.TextureSystem == 2 ? 8u : 4u,
            texture.TextureRid, texture.TextureRid, sampler,
            texture.Source.Source.ResourceUri + "#image=" + texture.Image.Index);
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetimeGate) { _retiring = true; return new(_disposal ??= DisposeCoreAsync()); }
    }
    private async Task DisposeCoreAsync()
    {
        await _gate.WaitAsync();
        try
        {
            foreach (var texture in _loaded.Values.Reverse()) await texture.DisposeAsync();
            _loaded.Clear();
        }
        finally { _gate.Release(); }
    }
}
