using Godot;
using San14.Data;

public sealed partial class ModelGbufferLayer
{
    private RenderDeviceResources? _knownNullReflectionAmbientResources;
    private ReflectionCaptureAmbientCube? _knownNullReflectionAmbient;

    /// <summary>Backend representation of a known-null D3D11 float4 Cube
    /// SampleLevel read. This is not an original environment image or sampler.
    /// The caller must first distinguish known null from unknown or no-write.</summary>
    public Task<ReflectionCaptureAmbientCube> GetKnownNullReflectionAmbientAsync(
        ReflectionCaptureModelSourceDraw actualDraw)
    {
        ArgumentNullException.ThrowIfNull(actualDraw);
        ReflectionCaptureKnownNullAmbientSource.RequireProfile(actualDraw.Shader, actualDraw.CombinationIndex);
        AcceptOperation(); return GetKnownNullReflectionAmbientAcceptedAsync();
    }

    private async Task<ReflectionCaptureAmbientCube> GetKnownNullReflectionAmbientAcceptedAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_knownNullReflectionAmbient is { } current) return current;
            var built = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                var resources = new RenderDeviceResources("Device rejected known-null reflection ambient reads.");
                try
                {
                    const RenderingDevice.DataFormat format = RenderingDevice.DataFormat.R32G32B32A32Sfloat;
                    const RenderingDevice.TextureUsageBits usage = RenderingDevice.TextureUsageBits.SamplingBit;
                    if (!rd.TextureIsFormatSupportedForUsage(format, usage))
                        throw new NotSupportedException("Device cannot represent known-null float4 Cube reads.");
                    using var shape = new RDTextureFormat { Format=format, Width=1, Height=1, Depth=1,
                        ArrayLayers=6, Mipmaps=1, TextureType=RenderingDevice.TextureType.Cube, UsageBits=usage };
                    using var view = new RDTextureView();
                    var faces = new Godot.Collections.Array<byte[]>();
                    for (int face=0; face<6; face++) faces.Add(new byte[16]); // RGBA=(0,0,0,0).
                    Rid texture = resources.Own(rd.TextureCreate(shape, view, faces));
                    using var samplerState = new RDSamplerState {
                        MinFilter=RenderingDevice.SamplerFilter.Nearest, MagFilter=RenderingDevice.SamplerFilter.Nearest,
                        MipFilter=RenderingDevice.SamplerFilter.Nearest,
                        RepeatU=RenderingDevice.SamplerRepeatMode.ClampToEdge,
                        RepeatV=RenderingDevice.SamplerRepeatMode.ClampToEdge,
                        RepeatW=RenderingDevice.SamplerRepeatMode.ClampToEdge,
                        MinLod=0, MaxLod=0, UseAnisotropy=false, EnableCompare=false };
                    Rid sampler = resources.Own(rd.SamplerCreate(samplerState));
                    const string uri = "native-null://D3D11/reflection-state12/float4-cube-SampleLevel0";
                    var binding = new ReflectionCaptureAmbientCube(new("Texture",5,texture,texture,uri),
                        new("Sampler",5,sampler,null,uri+"/backend-sampler"));
                    return (Resources:resources, Binding:binding);
                }
                catch(Exception failure)
                { resources.ReleaseAfterConstructionFailure(rd,failure,"Model-known-null-reflection-partial"); throw; }
            });
            _knownNullReflectionAmbientResources = built.Resources;
            return _knownNullReflectionAmbient = built.Binding;
        }
        finally { _gate.Release(); CompleteOperation(); }
    }
}
