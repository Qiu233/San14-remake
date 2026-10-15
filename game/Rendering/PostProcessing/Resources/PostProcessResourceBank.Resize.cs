using Godot;
using San14.Data;

public sealed partial class PostProcessResourceBank
{
    private sealed class PendingResize(PostProcessResourceBank replacement)
    {
        public PostProcessResourceBank Replacement{get;}=replacement;
        public HashSet<Rid>? ObsoleteStorage{get;set;}
    }
    private PendingResize? pendingResize;
    private void RequireCompleteResize()
    {
        if(pendingResize is not null)
            throw new InvalidOperationException("The bank's previous resource resize must finish before execution.");
    }
    /// <summary>Run the original resize-only allocation on this same module.
    /// Caller serializes requests and completes all previous consumers first.
    /// Fixed images/buffers, CBs and their completed history remain alive.</summary>
    public async Task ResizeOwnedResourcesAsync(Vector2I extent)
    {
        await serial.WaitAsync();
        try
        {
            lock(lifetime)
            {
                ObjectDisposedException.ThrowIf(retiring,this);
                if(accepted!=0)throw new InvalidOperationException("Cannot resize a bank with active device consumers.");
            }
            await DrainPendingResourcesCoreAsync();
            if(pendingResize is not null)await CompleteResizeCoreAsync();
            if(source.ContextWidth==extent.X&&source.ContextHeight==extent.Y)return;
            var resize=MapPostEffect3AllocationReader.Read(source.Source,
                checked((uint)extent.X),checked((uint)extent.Y),source.ContextOwnerIdentity,resizeOnly:true);
            PostProcessResourceBank replacement;
            try{replacement=await BuildDeviceResourcesAsync(resize,allowReadback);}
            catch(NativeMapBorrowerConstructionException failure)
            {pendingResources=failure.RetainedPackage;throw;}
            // Both generations remain owned until the old groups retire and
            // the new generation is installed. A partial release cannot roll back.
            lock(lifetime)pendingResize=new(replacement);
            await CompleteResizeCoreAsync();
        }
        finally{serial.Release();}
    }
    private async Task CompleteResizeCoreAsync()
    {
        var pending=pendingResize!;var replacement=pending.Replacement;
        var changedTextures=replacement.textureRows.Keys.ToHashSet();
        var changedBuffers=replacement.bufferRows.Keys.ToHashSet();
        if(pending.ObsoleteStorage is null)
        {
            var obsolete=new HashSet<Rid>();
            foreach(int index in changedTextures)
                foreach(var texture in textureRows[index])
                    obsolete.Add(texture.StorageRid);
            foreach(int index in changedBuffers)
                foreach(var buffer in bufferRows[index])
                    obsolete.Add(buffer.StorageRid);
            pending.ObsoleteStorage=obsolete;
        }
        // Parent identity includes uncached old bridges. Derived groups
        // retire first because groups retain their original creation order.
        await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            for(int i=owned.Count-1;i>=0;i--)
                if(pending.ObsoleteStorage.Contains(owned[i].ParentStorage))ReleaseResources(rd,owned[i]);
            return true;
        });
        lock(lifetime)
        {
            foreach(var key in eyeMipViews.Keys.Where(k=>changedTextures.Contains(k.Physical)).ToArray())eyeMipViews.Remove(key);
            foreach(var key in lensFlare2Views.Keys.Where(k=>changedTextures.Contains(k.Physical)).ToArray())lensFlare2Views.Remove(key);
            foreach(var key in bufferShaderReadViews.Keys.Where(k=>changedBuffers.Contains(k.Index)).ToArray())bufferShaderReadViews.Remove(key);
            foreach(var key in textureContents.Keys.Where(k=>changedTextures.Contains(k.Parameter)).ToArray())textureContents.Remove(key);
            foreach(var key in toneInfoRegions.Keys.Where(k=>changedTextures.Contains(k.Parameter)).ToArray())toneInfoRegions.Remove(key);
            if(changedBuffers.Contains(5))dofArgumentDomains.Clear();
            foreach(var row in replacement.textureRows)textureRows[row.Key]=row.Value;
            foreach(var row in replacement.bufferRows)bufferRows[row.Key]=row.Value;
            owned.AddRange(replacement.owned);replacement.owned.Clear();
            source.InstallCompletedOwnedResize(replacement.source);
            pendingResize=null;
        }
    }
}
