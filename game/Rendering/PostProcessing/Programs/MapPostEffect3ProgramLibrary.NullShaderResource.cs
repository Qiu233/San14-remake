using Godot;
using San14.Data;
using San14.Data.Formats;

public sealed partial class MapPostEffect3ProgramLibrary
{
    private readonly Dictionary<string,MapPostEffect3CompiledPass> nullPasses=new(StringComparer.Ordinal);

    /// <summary>Compiles only the exact original shader with explicitly known
    /// native null SRV reads. Source DXBC/reflection remain unchanged; no dummy
    /// resource is created and original geometry/CB/sampler code is retained.</summary>
    public Task<MapPostEffect3CompiledPass> GetCompiledPassAsync(int sourcePassIndex,
        IReadOnlyList<MapPostEffect3NullShaderResource> actualNulls)
    {
        ArgumentNullException.ThrowIfNull(actualNulls);
        if(actualNulls.Count==0)return GetCompiledPassAsync(sourcePassIndex);
        var frozen=actualNulls.ToArray();
        foreach(var n in frozen)
        {
            n.ValidateCurrent();
            if(n.SourcePassIndex!=sourcePassIndex||n.SourceSha256!=SourceSha256)
                throw new InvalidDataException("Null shader specialization belongs to another original pass/module.");
        }
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var ordinary=GetCompiledPassAsync(sourcePassIndex);accepted++;
            return GetAcceptedNullAsync(ordinary,sourcePassIndex,frozen);
        }
    }

    private async Task<MapPostEffect3CompiledPass> GetAcceptedNullAsync(
        Task<MapPostEffect3CompiledPass> ordinary,int index,MapPostEffect3NullShaderResource[] nulls)
    {
        bool entered=false;
        try
        {
            var original=await ordinary;await gate.WaitAsync();entered=true;
            foreach(var n in nulls)n.ValidateCurrent();
            string signature=index+":"+string.Join(',',nulls.OrderBy(n=>n.SourceStageCode).ThenBy(n=>n.SourceRegister)
                .Select(n=>$"{n.SourceStageCode}/t{n.SourceRegister}"));
            if(nullPasses.TryGetValue(signature,out var cached))return cached;
            var adapted=original.Stages.Select(stage=>
            {
                var selected=nulls.Where(n=>n.SourceStageCode==stage.Source.StageCode).ToArray();
                if(selected.Length==0)return stage;
                var patch=MapPostEffect3NullShaderResourceAdapter.Adapt(stage.Source,stage.DeviceTranslation,stage.Bindings,selected);
                var device=stage.DeviceTranslation with{Bytes=patch.Bytes,SpirvSha256=patch.Sha256};
                // ReadBindings retains all original RDEF rows; only removed
                // SRV descriptors disappear from the actual device interface.
                var binding=ReadBindings(stage.Bindings.SourceDefinitions,device.Bytes,stage.Source.StageCode,
                    stage.Translation.BufferUavsAsStorageBuffers);
                return stage with{DeviceTranslation=device,Bindings=binding};
            }).ToArray();
            string key="source-null:"+string.Join(';',adapted.Select(s=>$"{s.Source.StageCode}:{s.DeviceTranslation.SpirvSha256}"));
            if(original.StockScreenVertex is{} stock)key+=";embedded-stock-VS:"+stock.DeviceTranslation.SpirvSha256;
            if(!shaders.TryGetValue(key,out var shader))
            {
                shader=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {
                    using var spirv=new RDShaderSpirV();
                    foreach(var stage in adapted)spirv.SetStageBytecode(DeviceStage(stage.Source.StageCode),stage.DeviceTranslation.Bytes);
                    if(original.StockScreenVertex is{} vertex)spirv.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,vertex.DeviceTranslation.Bytes);
                    var rid=rd.ShaderCreateFromSpirV(spirv,"Original3SPK/pass"+index+"/native-null-SRV");
                    if(!rid.IsValid)throw new InvalidDataException("RenderingDevice rejected original source-null specialization.");
                    return rid;
                });shaders.Add(key,shader);
            }
            foreach(var n in nulls)n.ValidateCurrent();
            var result=original with{Stages=Array.AsReadOnly(adapted),ShaderRid=shader};nullPasses.Add(signature,result);return result;
        }
        finally
        {
            if(entered)gate.Release();
            lock(lifetime)if(--accepted==0&&retiring)drained?.TrySetResult();
        }
    }
}
