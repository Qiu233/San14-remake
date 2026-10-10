using Godot;
using San14.Data;

/// <summary>Borrow the actual successful first renderer noise initialization
/// through its unchanged saved Common template. The one white texel is source
/// output, not a replacement for absent hardware texture or arbitrary noise.</summary>
public sealed class MapPostEffect3WorldInputNoiseSourceOwner
{
    private readonly MapPostEffect3WorldStockGraphicsSourceOwner world;
    private readonly MapFinalOutputNoiseOwner noise;
    private readonly MapPostEffect3ResolvedTextureParameter selection;
    private MapPostEffect3WorldInputNoiseSourceOwner(MapPostEffect3WorldStockGraphicsSourceOwner world,
        MapFinalOutputNoiseOwner noise,MapPostEffect3ResolvedTextureParameter selection)
        =>(this.world,this.noise,this.selection)=(world,noise,selection);
    public string OwnerIdentity=>noise.OwnerIdentity;
    public ReadOnlyMemory<byte> NativeSamplerKey=>noise.NativeSamplerKey.ToArray();
    public string SourceEvidence=>noise.Binding.SourceResourceUri+
        ";source-saved-Common899F101B-successful-init/noise-wrapper-copy;current-noise-shader-view;source-Camera-noise140=0";
    public static MapPostEffect3WorldInputNoiseSourceOwner BindCurrent(
        MapSourceCreatedWorldPlayableHost host,MapPostEffect3WorldStockGraphicsSourceOwner world,
        MapPostEffect3OwnedFreshCameraFrame parameters,MapCameraConstructionPacket currentCamera)
    {
        world.RequireCurrent();parameters.RequireCurrent();parameters.Route.ValidateSameCamera(currentCamera);
        var current=host.Session.CurrentFrame??throw new InvalidDataException("Noise source has no current World.");
        var common=current.PreparedSourceFrame!.Common;
        var field=currentCamera.Source.Camera.Properties[140];
        if(common.Scope!=CommonRenderPublicationKind.InitialSettings||
            field.Index!=140||field.NameHash!=0x9CBC0FA6||field.TypeCode!=5||field.ElementCount!=1||field.RawValue.Length!=4||
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue)!=0)
            throw new NotSupportedException("InputNoise needs another actual producer for a later Common callback or Camera noise override.");
        var noise=host.Final.Chain.CompleteEffects?.Noise??throw new InvalidDataException("Actual successful saved-template noise initialization is absent.");
        noise.RequireSuccessfulFirstInitialization();
        if(noise.Source.Graph.Source.DecodedSha256!=world.Context.OutputFrame.Source.Graph.Source.DecodedSha256||
            noise.NativeSamplerKey.Span.SequenceEqual(Convert.FromHexString("1514300000000000"))!=true)
            throw new InvalidDataException("Initialized noise differs from this source graph/descriptor key.");
        int index=parameters.State.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,"InputNoise")??
            throw new InvalidDataException("Original InputNoise parameter is absent.");
        world.TextureParameters.BindExternal(index,new(world.Context.FrameNumber,noise.OwnerIdentity),
            noise.Binding.SourceResourceUri+";saved-noise-wrapper-non-owning-runtime-view");
        var owner=new MapPostEffect3WorldInputNoiseSourceOwner(world,noise,world.TextureParameters.Require(index));
        owner.RequireCurrent();return owner;
    }
    public void RequireCurrent()
    {
        world.RequireCurrent();noise.RequireSuccessfulFirstInitialization();world.TextureParameters.ValidateCurrent(selection);
        if(selection.Reference.ProducerOwnerIdentity!=noise.OwnerIdentity||selection.Reference.PhysicalAllocationParameterIndex is not null)
            throw new InvalidDataException("Current noise table does not retain this actual initialized source producer.");
    }
    public IMapPostEffect3ComputeExternalTextureLease Lease()
    {RequireCurrent();return new NoiseLease(this);}
    private sealed record NoiseLease(MapPostEffect3WorldInputNoiseSourceOwner Owner):IMapPostEffect3ComputeExternalTextureLease
    {
        public long FrameNumber=>Owner.world.Context.FrameNumber;
        public string ContextOwnerIdentity=>Owner.world.Bank.Source.ContextOwnerIdentity;
        public int ParameterIndex=>Owner.selection.ParameterIndex;
        public int InstanceIndex=>0;
        public Rid ShaderViewRid=>Owner.noise.Binding.Resource;
        public Rid StorageRid=>Owner.noise.Binding.OwnerTexture!.Value;
        public uint NativeViewFormat=>87;
        public RenderingDevice.DataFormat DeviceFormat=>RenderingDevice.DataFormat.B8G8R8A8Unorm;
        public Vector3I Extent=>new(1,1,1);
        public string ProducerOwnerIdentity=>Owner.OwnerIdentity;
        public string SourceEvidence=>Owner.SourceEvidence;
        public void RequireCurrent()=>Owner.RequireCurrent();
    }
}
