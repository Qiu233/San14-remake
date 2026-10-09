using Godot;
using San14.Data;
using System.Security.Cryptography;

public sealed record SkyAmbientCubeSourceSelection(string ResourceUri,
    string SourceSha256, uint ObjectId, string SelectionProvenance)
{
    public static SkyAmbientCubeSourceSelection FromDeclaredCube(
        CubeRenderTextureDefinition source, string explicitSelectionProvenance)
        => new(source.Source.Source.ResourceUri,source.Source.Source.DecodedSha256,
            source.Object.ObjectId,explicitSelectionProvenance);
}

/// <summary>Borrowed completed generated cube plus the exact source sampler.
/// Object identity is the declared generated target. A different named wrapper
/// requires its separately proved alias/owner bridge before using these bindings.</summary>
public sealed record SkyAmbientCubeBinding(CubeRenderTextureDevice Cube,
    DeferredDeviceBinding Texture, DeferredDeviceBinding Sampler,
    SkyAmbientCubeSourceSelection Selection, string SamplerSha256);

public sealed record SkySceneAmbientFrame(long FrameNumber, string SourceProvenance,
    SkySceneFrameUpdateResult Update, SkyAmbientCubeBinding? Ambient);

/// <summary>Coherent source lighting→sky inputs→optional producers→complete
/// specular→ambient consumer. No generated cube is published while filtering.
/// It owns only its consumer samplers; scene/producers/cubes remain borrowed.
/// Native wrapper aliases and generation/output policy are not invented.</summary>
public sealed class SkySceneAmbientProvider : IAsyncDisposable
{
    public SkySceneFrameUpdater Updater { get; }
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly Dictionary<string,Rid> samplers=new(StringComparer.Ordinal);
    private SkySceneSourceFramePacket? sourceFrame;
    private SkyAmbientCubeSourceSelection? selection;
    private byte[]? samplerBytes;
    private bool retiring;
    private Task? disposal;
    private readonly object lifetimeGate=new();

    public SkySceneAmbientProvider(SkySceneFrameUpdater updater)
    { ArgumentNullException.ThrowIfNull(updater);Updater=updater; }

    public async Task<SkySceneAmbientFrame> SubmitSourceFrameAsync(uint generationIndex,
        SkySceneSourceFramePacket source, SkySceneProducerFrame producers,
        uint maximumWorkUnits, SkyAmbientCubeSourceSelection selectedOutput,
        ReadOnlyMemory<byte> exactSourceSampler, SkySceneOptionalLightInputs? optionalLight=null)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(selectedOutput);
        if(exactSourceSampler.Length!=52 || string.IsNullOrWhiteSpace(selectedOutput.SelectionProvenance) ||
            string.IsNullOrWhiteSpace(source.SourceProvenance))
            throw new InvalidDataException("Source ambient requires the complete source sampler and explicit source/output provenance.");
        if(generationIndex>=3)throw new ArgumentOutOfRangeException(nameof(generationIndex));
        ValidateSelection(Updater.Scene.SpecularTargets[(int)generationIndex].Source,selectedOutput);
        byte[] copiedSampler=exactSourceSampler.ToArray();
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(sourceFrame is not null && source.FrameNumber<=sourceFrame.FrameNumber)
                throw new InvalidDataException("Source ambient frames require increasing coherent lighting frame identities.");
            var update=await Updater.SubmitFrameAsync(generationIndex,source.SourceFrame,producers,maximumWorkUnits,optionalLight);
            sourceFrame=source;selection=selectedOutput;samplerBytes=copiedSampler;
            return await ResultAsync(update);
        }
        finally{gate.Release();}
    }

    public async Task<SkySceneAmbientFrame> AdvanceAsync(uint maximumWorkUnits)
    {
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(sourceFrame is null)throw new InvalidOperationException("No coherent source ambient frame has been submitted.");
            return await ResultAsync(await Updater.AdvanceAsync(maximumWorkUnits));
        }
        finally{gate.Release();}
    }

    private async Task<SkySceneAmbientFrame> ResultAsync(SkySceneFrameUpdateResult update)
    {
        SkyAmbientCubeBinding? ambient=null;
        if(update.CompletedSpecular is { } cube)
        {
            ValidateSelection(cube.Source,selection!);
            string hash=Convert.ToHexString(SHA256.HashData(samplerBytes!));
            if(!samplers.TryGetValue(hash,out Rid sampler))
            {
                sampler=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {
                    Rid made=DeviceSampler.Create(rd,samplerBytes!);
                    if(!made.IsValid)throw new InvalidDataException("Device rejected the exact source ambient sampler.");
                    return made;
                });
                samplers.Add(hash,sampler);
            }
            string uri=$"{selection!.ResourceUri}@{selection.SourceSha256}#object={selection.ObjectId:X8};"+
                $"completed-sky-generation={update.Progress.GenerationIndex};frame={sourceFrame!.FrameNumber};{selection.SelectionProvenance}";
            ambient=new(cube,new("Texture",7,cube.TextureRid,cube.TextureRid,uri),
                new("Sampler",7,sampler,null,uri+$";sampler={hash}"),selection,hash);
        }
        return new(sourceFrame!.FrameNumber,sourceFrame.SourceProvenance,update,ambient);
    }

    private static void ValidateSelection(CubeRenderTextureDefinition cube,
        SkyAmbientCubeSourceSelection selected)
    {
        if(selected.ObjectId!=cube.Object.ObjectId || selected.ResourceUri!=cube.Source.Source.ResourceUri ||
            selected.SourceSha256!=cube.Source.Source.DecodedSha256)
            throw new InvalidDataException("Requested ambient wrapper does not name the completed generated source cube; resolve its native alias/owner bridge separately.");
    }

    public ValueTask DisposeAsync()
    { lock(lifetimeGate){retiring=true;return new(disposal??=DisposeCoreAsync());} }
    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();
        try
        {
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {foreach(Rid sampler in samplers.Values)rd.FreeRid(sampler);return true;});
            samplers.Clear();
        }
        finally{gate.Release();}
    }
}
