using Godot;
using San14.Data;

/// <summary>Owns one explicitly selected native Static LUT child. This is a
/// source volume upload, not a Camera1828/selector or HDR route capability.
/// The caller must provide the actual native SRV format; this owner never
/// infers a live selection from a LUT declaration, locale or texture shape.</summary>
public sealed partial class MapPostEffect3StaticLutTextureOwner : IAsyncDisposable
{
    public StaticTextureBinding Binding { get; }
    public uint NativeViewFormat => texture.NativeViewFormat;
    public Rid TextureRid => texture.TextureRid;
    public int VolumeDepth => texture.Source.Depth;
    public int MipCount => texture.Source.MipCount;
    public IReadOnlyList<string> MipSha256 => texture.MipSha256;
    public string DefinitionSourceSha256 { get; }
    public string TextureSourceSha256 { get; }
    public string SourceEvidence { get; }
    public string OwnerIdentity { get; }="PostEffect3-Static-Lut:"+Guid.NewGuid().ToString("N");
    public bool ProvesCurrentCameraSelection => false;
    private readonly SkyTextureDevice texture;
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly object lifetime=new();
    private bool retiring;
    private Task? disposal;

    private MapPostEffect3StaticLutTextureOwner(StaticTextureBinding binding,
        SkyTextureDevice texture,string explicitViewEvidence)
    {
        Binding=binding;this.texture=texture;
        DefinitionSourceSha256=binding.Definitions.Source.DecodedSha256;
        TextureSourceSha256=binding.Resource.Source.DecodedSha256;
        SourceEvidence=$"{binding.Definitions.Source.ResourceUri}@{DefinitionSourceSha256}#Static/{binding.Definition.ObjectId:X8};"+
            $"{binding.Resource.Source.ResourceUri}@{TextureSourceSha256}#image/{binding.ImageIndex};"+
            $"explicit-native-SRV-format={texture.NativeViewFormat};"+explicitViewEvidence;
    }

    public static Task<MapPostEffect3StaticLutTextureOwner> BuildAsync(
        StaticTextureBinding exactStaticBinding,uint explicitNativeViewFormat,string actualViewSelectionEvidence)
    {
        ArgumentNullException.ThrowIfNull(exactStaticBinding);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualViewSelectionEvidence);
        return BuildImportedAsync(exactStaticBinding,
            StaticTextureResources.ImportVolume(exactStaticBinding,explicitNativeViewFormat),actualViewSelectionEvidence);
    }

    private static async Task<MapPostEffect3StaticLutTextureOwner> BuildImportedAsync(
        StaticTextureBinding binding,SkyTextureSource source,string viewEvidence)
    {
        var texture=await SkyTextureDevice.BuildAsync(source);
        try{return new(binding,texture,viewEvidence);}
        catch(Exception failure)
        {
            var partial=new NativeMapBorrowerRetirementPackage("Static-LUT-partial/"+Guid.NewGuid().ToString("N"),texture);
            try{await partial.DisposeAsync();}
            catch(Exception cleanup)
            {
                partial.RetainFailure();
                throw new NativeMapBorrowerConstructionException(failure,cleanup,partial);
            }
            throw;
        }
    }

    public void RequireCurrent()
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
        }
    }

    /// <summary>Submit this nonnull owned Static wrapper to the actual HDR/LDR
    /// slot, then retain its pointer assignment for shader consumption. A null
    /// controller choice must skip this setter, as the original does.</summary>
    public IMapPostEffect3ComputeExternalTextureLease BindCurrentParameter(
        MapPostEffect3TextureParameterState parameters,string actualParameterName,string sourceWriterEvidence)
    {
        ArgumentNullException.ThrowIfNull(parameters);ArgumentException.ThrowIfNullOrWhiteSpace(sourceWriterEvidence);
        RequireCurrent();
        if(actualParameterName is not ("InputHdr3dLut" or "InputLdr3dLut"))
            throw new InvalidDataException("Static LUT setter requires the original controller's HDR or LDR texture slot.");
        var names=MapPostEffect3ParameterState.CreateUninitialized(parameters.Allocation.Source,
            parameters.FrameNumber,parameters.ContextOwnerIdentity);
        int index=names.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,actualParameterName)
            ??throw new InvalidDataException("Original controller LUT parameter is absent.");
        parameters.BindExternal(index,new(parameters.FrameNumber,OwnerIdentity),SourceEvidence+";"+sourceWriterEvidence);
        var lease=new ParameterLease(this,parameters,parameters.Require(index));lease.RequireCurrent();return lease;
    }

    private sealed record ParameterLease(MapPostEffect3StaticLutTextureOwner Owner,
        MapPostEffect3TextureParameterState Table,MapPostEffect3ResolvedTextureParameter Accepted)
        :IMapPostEffect3ComputeExternalTextureLease,IMapPostEffect3ComputeMipTextureLease
    {
        public long FrameNumber=>Accepted.FrameNumber;
        public string ContextOwnerIdentity=>Accepted.ContextOwnerIdentity;
        public int ParameterIndex=>Accepted.ParameterIndex;
        public int InstanceIndex=>0;
        public Rid ShaderViewRid=>Owner.TextureRid;
        public Rid StorageRid=>Owner.TextureRid;
        public uint NativeViewFormat=>Owner.NativeViewFormat;
        public RenderingDevice.DataFormat DeviceFormat=>NativeViewFormat switch{
            87=>RenderingDevice.DataFormat.B8G8R8A8Unorm,91=>RenderingDevice.DataFormat.B8G8R8A8Srgb,
            10=>RenderingDevice.DataFormat.R16G16B16A16Sfloat,
            98=>RenderingDevice.DataFormat.Bc7UnormBlock,99=>RenderingDevice.DataFormat.Bc7SrgbBlock,
            _=>throw new InvalidDataException("Uploaded LUT native view is outside supported volume encodings.")};
        public Vector3I Extent=>new(Owner.texture.Source.Width,Owner.texture.Source.Height,Owner.VolumeDepth);
        public string ProducerOwnerIdentity=>Owner.OwnerIdentity;
        public string SourceEvidence=>Accepted.Reference.SourceEvidence+";complete-original-Static-volume-upload";
        public void RequireCurrent()
        {
            Owner.RequireCurrent();Table.ValidateCurrent(Accepted);
            if(Accepted.Reference.PhysicalAllocationParameterIndex is not null||
                Accepted.Reference.ProducerOwnerIdentity!=Owner.OwnerIdentity)
                throw new InvalidDataException("LUT shader input no longer selects its exact owned Static texture.");
        }
        public void RequireReadableMipRange(uint firstMipLevel,uint mipLevelCount)
        {
            RequireCurrent();
            if(mipLevelCount==0||firstMipLevel>=(uint)Owner.MipCount||mipLevelCount>(uint)Owner.MipCount-firstMipLevel)
                throw new InvalidDataException("LUT shader mip range exceeds its completely uploaded original volume.");
        }
    }

    /// <summary>Returns original encoded depth slices/mips without exporting
    /// an image. Disposal drains an accepted read before freeing the texture.</summary>
    public async Task<byte[]> ReadEncodedVolumeAsync()
    {
        RequireCurrent();await serial.WaitAsync();
        try{RequireCurrent();return await texture.ReadEncodedChainAsync();}
        finally{serial.Release();}
    }
    public ValueTask DisposeAsync()
    {
        lock(lifetime)
        {
            retiring=true;
            if(disposal is null||disposal.IsFaulted||disposal.IsCanceled)disposal=RetireAsync();
            return new(disposal);
        }
    }
    private async Task RetireAsync()
    {
        await serial.WaitAsync();
        try{await texture.DisposeAsync();}finally{serial.Release();}
    }
}
