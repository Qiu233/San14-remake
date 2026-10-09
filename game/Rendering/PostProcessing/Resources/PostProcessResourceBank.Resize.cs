using Godot;
using San14.Data;

public sealed partial class PostProcessResourceBank
{
    /// <summary>Run the original resize-only allocation on this same module.
    /// Caller serializes requests and completes all previous consumers first.
    /// Fixed images/buffers, CBs and their completed history remain alive.</summary>
    public async Task ResizeOwnedResourcesAsync(Vector2I extent)
    {
        await serial.WaitAsync();
        PostProcessResourceBank? replacement=null;
        try
        {
            lock(lifetime)
            {
                ObjectDisposedException.ThrowIf(retiring,this);
                if(accepted!=0)throw new InvalidOperationException("Cannot resize a bank with active device consumers.");
            }
            if(Source.ContextWidth==extent.X&&Source.ContextHeight==extent.Y)return;
            var resize=MapPostEffect3AllocationReader.Read(Source.Source,
                checked((uint)extent.X),checked((uint)extent.Y),Source.ContextOwnerIdentity,resizeOnly:true);
            replacement=await BuildDeviceResourcesAsync(resize,allowReadback);
            var changedTextures=replacement.textureRows.Keys.ToHashSet();
            var changedBuffers=replacement.bufferRows.Keys.ToHashSet();
            var obsolete=new HashSet<Rid>();
            foreach(int index in changedTextures)
                foreach(var texture in textureRows[index])
                {obsolete.Add(texture.StorageRid);foreach(var view in texture.NativeFormatViews.Values)obsolete.Add(view);}
            foreach(int index in changedBuffers)
                foreach(var buffer in bufferRows[index])
                {obsolete.Add(buffer.StorageRid);if(buffer.PreparedShaderReadRid.IsValid)obsolete.Add(buffer.PreparedShaderReadRid);}
            foreach(var key in eyeMipViews.Keys.Where(k=>changedTextures.Contains(k.Physical)).ToArray())
            {obsolete.Add(eyeMipViews[key].ViewRid);eyeMipViews.Remove(key);}
            foreach(var key in lensFlare2Views.Keys.Where(k=>changedTextures.Contains(k.Physical)).ToArray())
            {obsolete.Add(lensFlare2Views[key].ViewRid);lensFlare2Views.Remove(key);}
            foreach(var key in bufferShaderReadViews.Keys.Where(k=>changedBuffers.Contains(k.Index)).ToArray())
            {obsolete.Add(bufferShaderReadViews[key].View);bufferShaderReadViews.Remove(key);}
            // Derived views retire before their parent storage (owned records
            // are appended in creation order). No uninitialised resized pixels
            // inherit a previous completed-write receipt.
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                for(int i=owned.Count-1;i>=0;i--)
                    if(obsolete.Contains(owned[i])){rd.FreeRid(owned[i]);owned.RemoveAt(i);}
                return true;
            });
            foreach(var key in textureContents.Keys.Where(k=>changedTextures.Contains(k.Parameter)).ToArray())textureContents.Remove(key);
            foreach(var key in toneInfoRegions.Keys.Where(k=>changedTextures.Contains(k.Parameter)).ToArray())toneInfoRegions.Remove(key);
            if(changedBuffers.Contains(5))dofArgumentDomains.Clear();
            foreach(var row in replacement.textureRows)textureRows[row.Key]=row.Value;
            foreach(var row in replacement.bufferRows)bufferRows[row.Key]=row.Value;
            owned.AddRange(replacement.owned);replacement.owned.Clear();
            Source.InstallCompletedOwnedResize(resize);OwnedRidCount=owned.Count;
        }
        finally
        {
            try{if(replacement is not null)await replacement.DisposeAsync();}
            finally{serial.Release();}
        }
    }
}
