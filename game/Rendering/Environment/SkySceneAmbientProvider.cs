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
    private readonly RenderDeviceResources samplerResources=new("Device rejected the exact source ambient sampler.");
    private sealed record Submission(SkyScenePreparedFrame Prepared,
        SkySceneSourceFramePacket Source, SkyAmbientCubeSourceSelection Selection, byte[] SamplerBytes);
    private Submission? submission;
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
            if(submission is not null && source.FrameNumber<=submission.Source.FrameNumber)
                throw new InvalidDataException("Source ambient frames require increasing coherent lighting frame identities.");
            var update=await Updater.SubmitFrameAsync(generationIndex,source.SourceFrame,producers,maximumWorkUnits,optionalLight);
            submission=new(Updater.StagedFrame,source,selectedOutput,copiedSampler);
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
            RequireSubmission();
            return await ResultAsync(await Updater.AdvanceAsync(maximumWorkUnits));
        }
        finally{gate.Release();}
    }

    private Submission RequireSubmission()
    {
        var current=submission??throw new InvalidOperationException("No coherent source ambient frame has been submitted.");
        Updater.RequireStagedFrame(current.Prepared);
        return current;
    }

    private async Task<SkySceneAmbientFrame> ResultAsync(SkySceneFrameUpdateResult update)
    {
        var current=RequireSubmission();
        SkyAmbientCubeBinding? ambient=null;
        if(update.CompletedSpecular is { } cube)
        {
            ValidateSelection(cube.Source,current.Selection);
            string hash=Convert.ToHexString(SHA256.HashData(current.SamplerBytes));
            if(!samplers.TryGetValue(hash,out Rid sampler))
            {
                sampler=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {
                    return samplerResources.Own(DeviceSampler.Create(rd,current.SamplerBytes));
                });
                samplers.Add(hash,sampler);
            }
            string uri=$"{current.Selection.ResourceUri}@{current.Selection.SourceSha256}#object={current.Selection.ObjectId:X8};"+
                $"completed-sky-generation={update.Progress.GenerationIndex};frame={current.Source.FrameNumber};{current.Selection.SelectionProvenance}";
            ambient=new(cube,new("Texture",7,cube.TextureRid,cube.TextureRid,uri),
                new("Sampler",7,sampler,null,uri+$";sampler={hash}"),current.Selection,hash);
        }
        Updater.RequireStagedFrame(current.Prepared);
        return new(current.Source.FrameNumber,current.Source.SourceProvenance,update,ambient);
    }

    private static void ValidateSelection(CubeRenderTextureDefinition cube,
        SkyAmbientCubeSourceSelection selected)
    {
        if(selected.ObjectId!=cube.Object.ObjectId || selected.ResourceUri!=cube.Source.Source.ResourceUri ||
            selected.SourceSha256!=cube.Source.Source.DecodedSha256)
            throw new InvalidDataException("Requested ambient wrapper does not name the completed generated source cube; resolve its native alias/owner bridge separately.");
    }

    public ValueTask DisposeAsync()
    {
        lock(lifetimeGate)
        {
            retiring=true;
            if(disposal is null||disposal.IsFaulted||disposal.IsCanceled)disposal=DisposeCoreAsync();
            return new(disposal);
        }
    }
    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();
        try
        {
            await samplerResources.DisposeAsync();
            samplers.Clear();
        }
        finally{gate.Release();}
    }
}
