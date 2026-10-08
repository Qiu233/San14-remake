using System.Buffers.Binary;
using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Owns one explicitly selected native Static LUT child. This is a
/// source volume upload, not a Camera1828/selector or HDR route capability.
/// The caller must provide the actual native SRV format; this owner never
/// infers a live selection from a LUT declaration, locale or texture shape.</summary>
public sealed partial class OriginalMapPostEffect3StaticLutTextureOwner : IAsyncDisposable
{
    public OriginalStaticTextureBinding Binding { get; }
    public uint NativeViewFormat => texture.NativeViewFormat;
    public Rid TextureRid => texture.TextureRid;
    public int VolumeDepth => texture.Image.Depth;
    public int MipCount => texture.Image.MipCount;
    public IReadOnlyList<string> MipSha256 => texture.MipSha256;
    public string EncodedVolumeSha256 { get; }
    public string DefinitionSourceSha256 { get; }
    public string TextureSourceSha256 { get; }
    public string SourceEvidence { get; }
    public string OwnerIdentity { get; }="PostEffect3-Static-Lut:"+Guid.NewGuid().ToString("N");
    public bool ProvesCurrentCameraSelection => false;
    private readonly OriginalSkyTextureDevice texture;
    private readonly byte[] contextBytes,imageBytes;
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly object lifetime=new();
    private bool retiring;
    private Task? disposal;

    private OriginalMapPostEffect3StaticLutTextureOwner(OriginalStaticTextureBinding binding,
        OriginalSkyTextureDevice texture,string explicitViewEvidence)
    {
        Binding=binding;this.texture=texture;
        contextBytes=binding.ContextReference.RawValue!.ToArray();imageBytes=binding.ImageReference.RawValue!.ToArray();
        DefinitionSourceSha256=binding.Definitions.Source.DecodedSha256;
        TextureSourceSha256=binding.Resource.Source.DecodedSha256;
        EncodedVolumeSha256=Hash(texture.Image.EncodedDataRange.Slice(binding.Resource.Source.Bytes));
        SourceEvidence=$"{binding.Definitions.Source.ResourceUri}@{DefinitionSourceSha256}#Static/{binding.Definition.ObjectId:X8};"+
            $"{binding.Resource.Source.ResourceUri}@{TextureSourceSha256}#image/{binding.ImageIndex};"+
            $"explicit-native-SRV-format={texture.NativeViewFormat};"+explicitViewEvidence;
    }

    public static async Task<OriginalMapPostEffect3StaticLutTextureOwner> BuildAsync(
        OriginalStaticTextureBinding exactStaticBinding,uint explicitNativeViewFormat,string actualViewSelectionEvidence)
    {
        ArgumentNullException.ThrowIfNull(exactStaticBinding);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualViewSelectionEvidence);
        var b=exactStaticBinding;
        if(Hash(b.Definitions.Source.Bytes)!=b.Definitions.Source.DecodedSha256)
            throw new InvalidDataException("Static LUT definition source hash changed.");
        if(Hash(b.Resource.Source.Bytes)!=b.Resource.Source.DecodedSha256)
            throw new InvalidDataException("Static LUT texture source hash changed.");
        var canonical=DokReader.Read(b.Definitions.Source.Bytes).Objects.SingleOrDefault(o=>o.ObjectId==b.Definition.ObjectId);
        if(canonical is null||canonical.Kind!=DokObjectKind.Inline||canonical.ClassId!=0xFF7DBFD4||
            b.Definition.Kind!=canonical.Kind||b.Definition.ClassId!=canonical.ClassId)
            throw new InvalidDataException("LUT upload requires its exact original inline Static declaration.");
        var context=canonical.Properties.Single(p=>p.NameHash==0x6C7321D2);
        var image=canonical.Properties.Single(p=>p.NameHash==0x00200571);
        ValidateField(context,b.ContextReference,5);ValidateField(image,b.ImageReference,4);
        if(BinaryPrimitives.ReadUInt32LittleEndian(context.RawValue!)!=b.Resource.Source.FileId||
            BinaryPrimitives.ReadUInt32LittleEndian(image.RawValue!)!=b.ImageIndex)
            throw new InvalidDataException("Static LUT context/TID no longer identifies its supplied texture child.");
        var imageSource=G1TextureContainerReader.Read(b.Resource.Source.Bytes).Textures[b.ImageIndex];
        if(imageSource.TextureSystem!=2||imageSource.LayerCount!=1||imageSource.Depth<=1||
            imageSource.SubresourceLayout!=G1TextureSubresourceLayout.VolumeMipChain)
            throw new InvalidDataException("PostEffect3 Static LUT requires an encoded 3D volume, not a flattened 2D array.");
        var texture=await OriginalSkyTextureDevice.BuildAsync(b.Resource,b.ImageIndex,explicitNativeViewFormat);
        try{return new(b,texture,actualViewSelectionEvidence);}
        catch{await texture.DisposeAsync();throw;}
    }

    public void RequireCurrent()
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!texture.TextureRid.IsValid||Binding.Definitions.Source.DecodedSha256!=DefinitionSourceSha256||
                Binding.Resource.Source.DecodedSha256!=TextureSourceSha256||
                !Binding.ContextReference.RawValue!.AsSpan().SequenceEqual(contextBytes)||
                !Binding.ImageReference.RawValue!.AsSpan().SequenceEqual(imageBytes))
                throw new InvalidDataException("Static LUT source binding changed or its owned texture retired.");
        }
    }

    /// <summary>Submit this nonnull owned Static wrapper to the actual HDR/LDR
    /// slot, then retain its pointer assignment for shader consumption. A null
    /// controller choice must skip this setter, as the original does.</summary>
    public IOriginalMapPostEffect3ComputeExternalTextureLease BindCurrentParameter(
        OriginalMapPostEffect3TextureParameterState parameters,string actualParameterName,string sourceWriterEvidence)
    {
        ArgumentNullException.ThrowIfNull(parameters);ArgumentException.ThrowIfNullOrWhiteSpace(sourceWriterEvidence);
        RequireCurrent();
        if(actualParameterName is not ("InputHdr3dLut" or "InputLdr3dLut"))
            throw new InvalidDataException("Static LUT setter requires the original controller's HDR or LDR texture slot.");
        var names=OriginalMapPostEffect3ParameterState.CreateUninitialized(parameters.Allocation.Source,
            parameters.FrameNumber,parameters.ContextOwnerIdentity);
        int index=names.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,actualParameterName)
            ??throw new InvalidDataException("Original controller LUT parameter is absent.");
        parameters.BindExternal(index,new(parameters.FrameNumber,OwnerIdentity),SourceEvidence+";"+sourceWriterEvidence);
        var lease=new ParameterLease(this,parameters,parameters.Require(index));lease.RequireCurrent();return lease;
    }

    private sealed record ParameterLease(OriginalMapPostEffect3StaticLutTextureOwner Owner,
        OriginalMapPostEffect3TextureParameterState Table,OriginalMapPostEffect3ResolvedTextureParameter Accepted)
        :IOriginalMapPostEffect3ComputeExternalTextureLease,IOriginalMapPostEffect3ComputeMipTextureLease
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
        public Vector3I Extent=>new(Owner.texture.Image.Width,Owner.texture.Image.Height,Owner.VolumeDepth);
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
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static void ValidateField(DokProperty canonical,DokProperty supplied,ushort expectedType)
    {
        if(canonical.TypeCode!=expectedType||canonical.ElementCount!=1||canonical.ElementSize!=4||
            canonical.RawValue is not {Length:4}||supplied.NameHash!=canonical.NameHash||
            supplied.TypeCode!=canonical.TypeCode||supplied.ElementCount!=1||supplied.ElementSize!=4||
            supplied.RawValue is not {Length:4}||!supplied.RawValue.AsSpan().SequenceEqual(canonical.RawValue))
            throw new InvalidDataException("Static LUT context/TID field differs from its original source bytes.");
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
