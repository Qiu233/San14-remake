using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed partial class UiAssets
{
    /// <summary>The existing application generation's locale-neutral portrait
    /// images. Only UiAssets owners retain this object; pages borrow it.</summary>
    internal sealed partial class PortraitImages
    {
        private readonly object sourceGate=new();
        private readonly Dictionary<(string Pack,string Sprite),LoadedUiSpriteSource> sources=[];
        private readonly Dictionary<(string Hash,int Index),LoadedUiSpriteSource> images=[];
        private readonly Dictionary<(string Hash,int Index),Texture2Drd> textures=[];
        private readonly Dictionary<(string Hash,int Index),Rid> rids=[];
        private readonly List<string> unavailable=[];
        private readonly Dictionary<GameResourceId,ResourceReadGeneration> dependencies=[];
        private int references;
        private bool prepared;
        private volatile bool disposed;
        private Task? materialization;
        private Task? uploadCleanup;
        private readonly TaskCompletionSource retirement=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Retirement=>retirement.Task;
        internal bool IsDisposed=>disposed;
        internal bool IsPrepared=>prepared;
        internal int SourceCount=>sources.Count;
        internal int ImageCount=>images.Count;
        internal long EncodedGpuBytes {get;private set;}
        internal IReadOnlyList<string> Unavailable=>unavailable;
        internal PortraitImages Retain()
        {ObjectDisposedException.ThrowIf(disposed,this);Interlocked.Increment(ref references);return this;}

        internal void Prepare(GameResourceRepository repository,CancellationToken cancellationToken)
        {
            lock(sourceGate)
            {
                ObjectDisposedException.ThrowIf(disposed,this);
                if(prepared)
                {
                    repository.ObserveSharedResourceReads(dependencies.Values);
                    return;
                }
                var caller=repository;
                repository=repository.CreateReadScope(RecordDependency);
                var declared=new LocalizedUiSpriteSource(repository);var naming=ArmyMapUiSources.Create();
                var stockRequests=new List<(string Pack,string Sprite)>();
                foreach(int appearance in RegisteredOfficerPortraitCatalog.StockAppearances)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var face=naming.PortraitNames((ushort)appearance);AddDeclared(face.ResourceName,face.SpriteName);
                    string number=face.ResolvedAppearanceIndex.ToString("D4",CultureInfo.InvariantCulture)+(face.ResolvedAppearanceIndex!=appearance?"b":"");
                    AddDeclared("texture_chara_unique_otomo_"+number,"face_"+number+"_unique");
                    var standing=RegisteredOfficerPortraitCatalog.StandingPortrait(appearance);
                    foreach(string sprite in standing.SpriteNames)AddDeclared(standing.ResourceName,sprite);
                }
                // Preload only the actual declared stock payloads. Immutable
                // decoded assets enter the existing shared resident cache;
                // all ordered sprite/format/sampler admission stays below.
                declared.PrepareTexturePackPayloads(stockRequests.Select(row=>row.Pack).Distinct(StringComparer.Ordinal),cancellationToken);
                foreach(var request in stockRequests)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Add(request.Pack,request.Sprite,declared);
                }
                foreach(var descriptor in DlcPortraitDescriptor.All)
                    for(int appearance=descriptor.First;appearance<descriptor.First+descriptor.Count;appearance++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var admission=repository.ReadDlcPortraitAvailability(appearance);
                        if(!admission.Available)
                        {
                            if(admission.UnresolvedInput?.StartsWith("officer-editor.portrait.platform_entitlement:",StringComparison.Ordinal)!=true)
                                throw new InvalidDataException(admission.UnresolvedInput);
                            unavailable.Add(admission.UnresolvedInput!);continue;
                        }
                        if(!admission.Value){unavailable.Add("known-unavailable:"+descriptor.ResourceUri+":"+appearance);continue;}
                        foreach(int bank in new[]{9,10,11})
                            if(bank==9)foreach(int part in new[]{1,2,3})Add(descriptor.ResourceUri,descriptor.SpriteName(appearance,bank,part),declared);
                            else Add(descriptor.ResourceUri,descriptor.SpriteName(appearance,bank),declared);
                    }
                var user=repository.LoadUserPortraits();
                foreach(var registration in user.Registered)foreach(int bank in new[]{9,10,11})
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string resource=GameResourceId.File(UserPortraitRegistration.RelativePath(registration.Slot,bank)).ToString();
                    if(!user.HasResource(registration.Appearance,bank)){unavailable.Add("known-missing-user-bank:"+resource);continue;}
                    Add(resource,"portrait",declared);
                }
                prepared=true;
                caller.ObserveSharedResourceReads(dependencies.Values);
                void AddDeclared(string pack,string sprite)
                {
                    // Numerical ranges include reserved IDs. Only actual
                    // Common declarations form the installed stock closure.
                    if(declared.HasTexturePackDeclaration(pack))stockRequests.Add((pack,sprite));
                }
            }
        }

        private void RecordDependency(ResourceReadGeneration observed)
        {
            if(dependencies.TryGetValue(observed.Id,out var previous)&&previous!=observed)
                throw new ResourceReadGenerationChangedException(observed.Id);
            dependencies[observed.Id]=observed;
        }

        // Shared by the real startup closure and a bounded component fixture.
        internal void PrepareSources(GameResourceRepository repository,IEnumerable<(string Pack,string Sprite)> selected,
            CancellationToken cancellationToken)
        {
            lock(sourceGate)
            {
                if(prepared)throw new InvalidOperationException("Portrait source set was already prepared.");
                var caller=repository;
                repository=repository.CreateReadScope(RecordDependency);
                var declared=new LocalizedUiSpriteSource(repository);
                foreach(var row in selected){cancellationToken.ThrowIfCancellationRequested();Add(row.Pack,row.Sprite,declared);}
                prepared=true;
                caller.ObserveSharedResourceReads(dependencies.Values);
            }
        }
        private void Add(string pack,string sprite,LocalizedUiSpriteSource declared)
        {
            if(sources.ContainsKey((pack,sprite)))return;
            var source=declared.LoadTexturePackSource(pack,sprite);var image=source.Image;
            var sampling=G1TextureSamplerReader.ReadRetainedSource(image);
            if(image.PixelFormat!=0x5B||image.MipCount!=1||image.LayerCount!=1||image.TextureSystem!=0||image.RawHeader.Length<20||
                sampling.Filter!=0x14||sampling.AddressU is not(1 or 3)||sampling.AddressV is not(1 or 3)||
                sampling.MinimumLodBits!=0||sampling.MaximumLodBits!=0)
                throw new NotSupportedException("Startup portrait source needs an explicit supported format/sampler: "+pack+"/"+sprite);
            sources.Add((pack,sprite),source);var key=(source.Resource.Source.DecodedSha256,source.ImageIndex);
            if(images.TryAdd(key,source))
            {
                EncodedGpuBytes=checked(EncodedGpuBytes+image.EncodedDataRange.Length);
                if(!nativeSpriteImages.ContainsKey(key))pendingUploads.Add(key,source);
            }
        }
        internal LoadedUiSpriteSource RequireSource(string pack,string sprite)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            return sources.TryGetValue((pack,sprite),out var source)?source:
                throw new InvalidDataException("Portrait resource is outside the prepared application generation: "+pack+"/"+sprite);
        }
        internal Texture2D RequireTexture(LoadedUiSpriteSource source)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            return textures.TryGetValue((source.Resource.Source.DecodedSha256,source.ImageIndex),out var image)?image:
                throw new InvalidOperationException("Portrait image was not materialized before page admission.");
        }
        internal Task MaterializeAsync()
        {
            lock(sourceGate)
            {
                ObjectDisposedException.ThrowIf(disposed,this);
                if(!prepared)throw new InvalidOperationException("UI image sources must be prepared before upload.");
                if(pendingUploads.Count==0)return materialization??Task.CompletedTask;
                var batch=pendingUploads.ToArray();pendingUploads.Clear();
                return materialization=UploadAfterAsync(materialization,batch);
            }
        }
        private async Task UploadAfterAsync(Task? previous,KeyValuePair<(string Hash,int Index),LoadedUiSpriteSource>[] batch)
        {
            if(previous is not null)await previous.ConfigureAwait(false);
            await UploadAsync(batch).ConfigureAwait(false);
        }
        private static RenderingDevice.DataFormat SourceFormat(G1TextureImage image)
        {
            bool srgb=image.RawHeader[19]!=0;
            return image.PixelFormat switch
            {
                0x00=>srgb?RenderingDevice.DataFormat.R8G8B8A8Srgb:RenderingDevice.DataFormat.R8G8B8A8Unorm,
                0x01=>srgb?RenderingDevice.DataFormat.B8G8R8A8Srgb:RenderingDevice.DataFormat.B8G8R8A8Unorm,
                0x59=>srgb?RenderingDevice.DataFormat.Bc1RgbaSrgbBlock:RenderingDevice.DataFormat.Bc1RgbaUnormBlock,
                0x5B=>srgb?RenderingDevice.DataFormat.Bc3SrgbBlock:RenderingDevice.DataFormat.Bc3UnormBlock,
                _=>throw new NotSupportedException("UI source needs its explicit original device format.")
            };
        }
        private async Task UploadAsync(KeyValuePair<(string Hash,int Index),LoadedUiSpriteSource>[] batch)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(!prepared)throw new InvalidOperationException("Portrait sources must be prepared before upload.");
            var allocated=new Dictionary<(string,int),Rid>();
            try
            {
                await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {
                    var usage=RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.CanCopyFromBit;
                    if(!ClassDB.ClassGetPropertyList("Texture2DRD").Any(property=>property["name"].AsString()=="preserve_format"))
                        throw new NotSupportedException("Original portrait hardware sampling requires the explicit Texture2DRD preserve_format adapter.");
                    foreach(var encoding in batch.Select(row=>SourceFormat(row.Value.Image)).Distinct())
                        if(!rd.TextureIsFormatSupportedForUsage(encoding,usage))
                            throw new NotSupportedException("This RenderingDevice cannot retain/sample the original typed BC3 portrait encoding.");
                    foreach(var (key,source) in batch)
                    {
                        if(disposed)throw new OperationCanceledException("Application portrait generation retired during upload.");
                        var image=source.Image;
                        using var format=new RDTextureFormat{Format=SourceFormat(image),
                            TextureType=RenderingDevice.TextureType.Type2D,Width=(uint)image.Width,Height=(uint)image.Height,
                            Depth=1,ArrayLayers=1,Mipmaps=1,UsageBits=usage};
                        using var view=new RDTextureView();
                        var bytes=new Godot.Collections.Array<byte[]>{image.GetEncodedMip(source.Resource.Source.Bytes,0).ToArray()};
                        Rid rid=rd.TextureCreate(format,view,bytes);bytes.Clear();
                        if(!rid.IsValid)throw new InvalidDataException("Original portrait BC3 upload failed: "+source.Resource.Source.ResourceUri);
                        allocated.Add(key,rid);
                        using var actual=rd.TextureGetFormat(rid);
                        if(actual.Format!=SourceFormat(image)||actual.Width!=image.Width||actual.Height!=image.Height||actual.Mipmaps!=1)
                            throw new InvalidDataException("RenderingDevice did not retain original portrait shape/encoding.");
                    }
                    foreach(var(key,rid)in allocated)
                    {
                        rids.Add(key,rid);var texture=new Texture2Drd();textures.Add(key,texture);
                        // This public adapter is opt-in. Existing RD outputs
                        // and ordinary imported UI textures keep their views.
                        texture.Set("preserve_format",true);texture.TextureRdRid=rid;
                    }
                    return true;
                }).ConfigureAwait(false);
                await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {
                    foreach(var(key,_)in batch)
                    {
                        var texture=textures[key];
                        if(texture.TextureRdRid!=rids[key])throw new InvalidDataException("RenderingServer did not attach the original portrait device image.");
                        // TextureGetFormat on a shared RID reports its parent
                        // storage format, not the driver's SRV override. The
                        // typed sampling semantics are checked by the finite
                        // GPU component, not fabricated from that metadata.
                        if(!rd.TextureIsValid(RenderingServer.TextureGetRdTexture(texture.GetRid())))
                            throw new InvalidDataException("The default portrait sampling view was not attached before page admission.");
                    }
                    return true;
                }).ConfigureAwait(false);
            }
            catch
            {
                uploadCleanup=ReleaseTexturesAsync(allocated.ToArray());
                await uploadCleanup.ConfigureAwait(false);throw;
            }
        }
        internal void Release()
        {
            if(Interlocked.Decrement(ref references)!=0)return;
            _=BeginDisposal();
        }
        internal Task FinishUnborrowedRetirementAsync()
        {
            if(Volatile.Read(ref references)!=0)
                throw new InvalidOperationException("Application portrait facade still owns its image generation.");
            // A cancelled worker can retire before constructing any UiAssets.
            // Such a generation owns no GPU images but must still complete.
            return BeginDisposal();
        }
        private Task BeginDisposal()
        {
            if(disposed)return retirement.Task;
            disposed=true;_=RetireAsync(retirement);return retirement.Task;
        }
        private async Task ReleaseTexturesAsync(IReadOnlyList<KeyValuePair<(string Hash,int Index),Rid>> allocated)
        {
            foreach(var(key,_)in allocated)
                if(textures.TryGetValue(key,out var image))image.TextureRdRid=default;
            // Texture2Drd detachment is queued on the same server. This real
            // render-thread callback runs after it before freeing native RIDs.
            if(allocated.Count>0)await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{foreach(var row in allocated)rd.FreeRid(row.Value);return true;}).ConfigureAwait(false);
            foreach(var(key,_)in allocated)
            {
                if(textures.Remove(key,out var image))image.Dispose();
                rids.Remove(key);
            }
        }
        private async Task RetireAsync(TaskCompletionSource completion)
        {
            // The existing application UiAssets exit boundary calls this only
            // after its actual page borrowers have retired their atlas wrappers.
            // Upload failures were already returned to the startup caller;
            // retirement still detaches any surviving resources.
            try
            {
                if(materialization is not null)try{await materialization.ConfigureAwait(false);}catch{}
                // A failed upload has its own real cleanup task; do not lose a
                // detach/fence/free error behind the original startup failure.
                if(uploadCleanup is not null)await uploadCleanup.ConfigureAwait(false);
                await ReleaseTexturesAsync(rids.ToArray()).ConfigureAwait(false);
                sources.Clear();images.Clear();nativeSpriteImages.Clear();unsupportedNativeSprites.Clear();pendingUploads.Clear();
                dependencies.Clear();completion.SetResult();
            }
            catch(Exception error){completion.SetException(error);}
        }
    }
}
