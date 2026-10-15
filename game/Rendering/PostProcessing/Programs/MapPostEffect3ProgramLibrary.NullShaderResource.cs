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
            pendingConstruction=await NativeMapBorrowerRetirementPackage.DrainPendingAsync(pendingConstruction);
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
                return stage with{DeviceTranslation=device,Bindings=binding,
                    ComputeAccessRequirements=CreateComputeAccessRequirements(stage.Source,device,binding,stage.DofSpriteStorageAdaptation)};
            }).ToArray();
            string key="source-null:"+string.Join(';',adapted.Select(s=>$"{s.Source.StageCode}:{s.DeviceTranslation.SpirvSha256}"));
            if(original.StockScreenVertex is{} stock)key+=";embedded-stock-VS:"+stock.DeviceTranslation.SpirvSha256;
            var shader=await GetShaderAsync(key,adapted,original.StockScreenVertex,
                "Original3SPK/pass"+index+"/native-null-SRV","RenderingDevice rejected original source-null specialization.");
            foreach(var n in nulls)n.ValidateCurrent();
            var result=original with{Stages=Array.AsReadOnly(adapted),ShaderRid=shader.Rid};nullPasses.Add(signature,result);return result;
        }
        finally
        {
            if(entered)gate.Release();
            lock(lifetime)if(--accepted==0&&retiring)drained?.TrySetResult();
        }
    }
}
