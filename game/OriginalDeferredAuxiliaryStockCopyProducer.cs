using System.Buffers.Binary;
using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The attachment0 selected by the current source framebuffer owner,
/// after its actual graph writer succeeded. An allocated target is insufficient.
/// Binding/current-frame provenance remain explicit caller-owned inputs.</summary>
public sealed record OriginalDeferredAuxiliaryCurrentColorAttachment(
    OriginalRenderTargetDeviceTexture Texture,long FrameNumber,string FrameProducerProvenance,
    uint BoundAttachmentIndex,string CurrentBindingProducerProvenance,
    OriginalDeferredAuxiliaryProductionReceipt CurrentWriterReceipt);

public sealed record OriginalDeferredAuxiliaryStockCopyBackendState(Vector2 CurrentScreenScale29E0,
    Color CurrentColorScale29F0,bool CurrentBlendIsDisabled,string CurrentBackendProducerProvenance);

/// <summary>Native959FD0 mip0 copy, with its exact equal-rectangle hardware path
/// and actual stock VS/default3 PS fallback. No substitute shader, CPU filtering
/// or diagnostic clear generates source pixels.</summary>
public sealed class OriginalDeferredAuxiliaryStockCopyProducer : IAsyncDisposable
{
    public LoadedOriginalDeferredAuxiliaryPixelSourceRecipes Source { get; }
    public LoadedOriginalDeferredAuxiliaryStockCopySource StockSource { get; }
    public OriginalDeferredAuxiliaryPixelProgram Program { get; }
    private readonly Dictionary<bool,Rid> samplers=[];
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object retirementGate=new();
    private bool retiring;
    private Task? disposal;
    private OriginalDeferredAuxiliaryStockCopyProducer(LoadedOriginalDeferredAuxiliaryPixelSourceRecipes source,
        LoadedOriginalDeferredAuxiliaryStockCopySource stock,OriginalDeferredAuxiliaryPixelProgram program)
        =>(Source,StockSource,Program)=(source,stock,program);

    public static async Task<OriginalDeferredAuxiliaryStockCopyProducer> BuildAsync(
        LoadedOriginalDeferredAuxiliaryPixelSourceRecipes source,LoadedOriginalDeferredAuxiliaryStockCopySource stock,
        Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(compiler);
        return new(source,stock,await OriginalDeferredAuxiliaryPixelProgram.BuildStockCopyAsync(stock,compiler));
    }

    public async Task<OriginalDeferredAuxiliaryProductionReceipt> CopyCurrentColorToSpecularMipZeroAsync(
        OriginalDeferredF103SelectedSource selection,OriginalDeferredTransparencyImageSelection transparency,
        OriginalDeferredAuxiliaryCurrentColorAttachment attachment,
        OriginalDeferredAuxiliaryTargetDevice destination,OriginalDeferredAuxiliaryStockCopyBackendState backend)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(attachment);
        ArgumentNullException.ThrowIfNull(destination);ArgumentNullException.ThrowIfNull(backend);
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);selection.Validate();Program.ValidateSource();
            if(selection.Camera.ExecutableLocale!=StockSource.Native.Locale||attachment.FrameNumber!=selection.Current.FrameNumber||
                attachment.FrameProducerProvenance!=selection.Current.CurrentProducerProvenance||attachment.BoundAttachmentIndex!=0||
                string.IsNullOrWhiteSpace(attachment.CurrentBindingProducerProvenance)||
                string.IsNullOrWhiteSpace(backend.CurrentBackendProducerProvenance)||
                selection.SourcePlan.Graph.Source.ResourceUri!=Source.Graph.Source.ResourceUri||
                selection.SourcePlan.Graph.Source.DecodedSha256!=Source.Graph.Source.DecodedSha256)
                throw new InvalidDataException("Mip0 copy lacks its accepted current source attachment0, camera and graph writer.");
            var expected=Source.SelectSpecular(transparency);
            OriginalDeferredAuxiliaryTargetSources.Validate(destination.Construction);
            if(destination.Source.Source.Source.ResourceUri!=expected.Texture.Source.Source.ResourceUri||
                destination.Source.Source.Source.DecodedSha256!=expected.Texture.Source.Source.DecodedSha256||
                destination.Source with{Source=expected.Texture.Source,Object=expected.Texture.Object}!=expected.Texture||
                destination.Construction.NativeFrameExtent!=expected.NativeFrameExtent||destination.Construction.Extent!=expected.Extent||
                destination.MipCount!=7||destination.TargetFormat!=RenderingDevice.DataFormat.B10G11R11UfloatPack32)
                throw new InvalidDataException("Mip0 copy destination differs from its actual selected seven-mip specular source owner.");
            var input=attachment.Texture??throw new InvalidDataException("Current color attachment is absent.");
            var writer=attachment.CurrentWriterReceipt??throw new InvalidDataException("Current color attachment has no actual source writer receipt.");
            if(writer.OriginalGraph is null||writer.GraphResourceUri!=Source.Graph.Source.ResourceUri||
                writer.GraphSha256!=Source.Graph.Source.DecodedSha256||
                writer.FrameNumber!=selection.Current.FrameNumber||writer.ProducerProvenance!=selection.Current.CurrentProducerProvenance||
                writer.TextureObjectId!=input.Source.Object.ObjectId||writer.SurfaceIds is not [0]||
                writer.WriterNodeIndex<0||writer.WriterNodeIndex>=Source.Node.Index||
                writer.WriterFactoryClassId!=0xF103DB00||
                Source.Graph.Document.Nodes[writer.WriterNodeIndex].Call.FactoryClassId!=writer.WriterFactoryClassId||
                string.IsNullOrWhiteSpace(writer.SourceScope)||
                Convert.ToHexString(SHA256.HashData(writer.OriginalGraph.Source.Bytes))!=writer.GraphSha256)
                throw new InvalidDataException("Mip0 source needs a successful original earlier deferred writer; late node166 creates a dependency cycle.");
            var actual=OriginalRenderTextureResources.Read(new LoadedObjects(DokReader.Read(input.Source.Source.Source.Bytes),input.Source.Source.Source),
                input.Source.Object.ObjectId);
            var physicalInput=input.EffectiveSource;
            var nativeInput=OriginalRenderTextureFormatMath.Resolve(physicalInput.PixelFormat,
                physicalInput.ColorSpace,physicalInput.OutputColorSpace);
            if(input.Source.Source.Source.OverrideSource is not null||
                input.Source.Source.Source.ResourceUri!=Source.MainDepth.Source.Source.ResourceUri||
                input.Source.Source.Source.DecodedSha256!=Source.MainDepth.Source.Source.DecodedSha256||
                Convert.ToHexString(SHA256.HashData(input.Source.Source.Source.Bytes))!=Source.MainDepth.Source.Source.DecodedSha256||
                actual with{Source=input.Source.Source,Object=input.Source.Object}!=input.Source||
                physicalInput.Kind!=OriginalRenderTextureKind.Texture2D||physicalInput.LevelNum!=1||
                nativeInput.TargetViewFormat is 20 or 40||
                physicalInput.MultiSample!=0||input.ExplicitExtent.X<=0||input.ExplicitExtent.Y<=0||
                input.TargetFormat!=OriginalRenderTargetFrame.ViewFormat(nativeInput.TargetViewFormat)||
                input.StorageRid==destination.StorageRid)
                throw new InvalidDataException("Mip0 copy input lacks its actual current original single-sample2D color owner.");
            bool equalExtent=input.ExplicitExtent==destination.ExplicitExtent;
            bool direct=equalExtent&&physicalInput.PixelFormat==destination.Source.PixelFormat&&
                physicalInput.OutputColorSpace==destination.Source.OutputColorSpace&&input.TargetFormat==destination.TargetFormat;
            var receipt=OriginalDeferredAuxiliaryProductionReceipt.FromOriginalWriter(Source.Graph,Source.Node.Index,
                selection.Current.FrameNumber,selection.Current.CurrentProducerProvenance,
                "OriginalNode137Attachment0MipZeroCopy;path="+(direct?"CopySubresourceRegion":"stockDefault3")+
                ";source="+input.Source.Object.ObjectId.ToString("X8")+
                ";bindingProducer="+attachment.CurrentBindingProducerProvenance+
                ";backendProducer="+backend.CurrentBackendProducerProvenance,destination.Source.Object.ObjectId,[0]);
            if(direct)
            {
                await destination.ProduceAsync(receipt,(rd,surfaces)=>
                {
                    if(surfaces.Count!=1||surfaces[0].Source.MipLevel!=0||
                        !rd.TextureIsValid(input.StorageRid))throw new InvalidDataException("Source hardware copy dependencies retired.");
                    using var sourceFormat=rd.TextureGetFormat(input.StorageRid);using var targetFormat=rd.TextureGetFormat(destination.StorageRid);
                    if(sourceFormat.Format!=destination.TargetFormat||targetFormat.Format!=sourceFormat.Format||
                        !sourceFormat.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.CanCopyFromBit)||
                        !targetFormat.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.CanCopyToBit))
                        throw new InvalidDataException("Exact hardware copy requires matching source formats and explicit GPU-copy usage.");
                    var error=rd.TextureCopy(input.StorageRid,destination.StorageRid,Vector3.Zero,Vector3.Zero,
                        new Vector3(destination.ExplicitExtent.X,destination.ExplicitExtent.Y,1),0,0,0,0);
                    if(error!=Error.Ok)throw new InvalidDataException("Actual attachment0 hardware copy failed: "+error);
                });
                return receipt;
            }
            if(!backend.CurrentBlendIsDisabled||backend.CurrentScreenScale29E0.X<1||backend.CurrentScreenScale29E0.Y<1)
                throw new InvalidDataException("Stock copy fallback lacks its current full-coverage unblended backend state.");
            var quad=OriginalDeferredAuxiliaryStockQuad.FromCurrentViewport(destination.ExplicitExtent,
                backend.CurrentScreenScale29E0,backend.CurrentColorScale29F0,backend.CurrentBackendProducerProvenance);
            byte[] key=new byte[8];BinaryPrimitives.WriteUInt64LittleEndian(key,equalExtent?
                OriginalDeferredAuxiliaryStockCopySource.EqualRectangleSamplerKey:OriginalDeferredAuxiliaryStockCopySource.ResizedRectangleSamplerKey);
            byte[] descriptor=OriginalDeferredSamplerSources.DecodeKey(StockSource.Native,key).EncodeD3D11Descriptor();
            Rid sampler=await SamplerAsync(!equalExtent,descriptor);
            byte[] pixelConstants=new byte[16];BinaryPrimitives.WriteUInt32LittleEndian(pixelConstants,0xFF7FFFFF);
            var constants=new Dictionary<(uint,uint),ReadOnlyMemory<byte>>{{(1,0),quad.VertexConstantBytes},{(0,0),pixelConstants}};
            var inputs=new OriginalDeferredDeviceBinding[]{
                new("Texture",0,input.ShaderViewRid,input.StorageRid,input.Source.Source.Source.ResourceUri+"#"+input.Source.Object.ObjectId.ToString("X8")+"/attachment0"),
                new("Sampler",0,sampler,null,StockSource.Native.ResourceUri+"#source-copy-key/"+Convert.ToHexString(key))};
            await using var draw=await OriginalDeferredAuxiliaryPixelDraw.BuildAsync(Program,destination,0,constants,inputs,
                4,RenderingDevice.RenderPrimitive.Triangles,quad);
            await destination.ProduceAsync(receipt,(rd,surfaces)=>draw.Submit(rd));
            return receipt;
        }
        finally{gate.Release();}
    }

    private Task<Rid> SamplerAsync(bool resized,byte[] descriptor)
    {
        if(samplers.TryGetValue(resized,out var existing))return Task.FromResult(existing);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {Rid created=OriginalDeviceSampler.Create(rd,descriptor);samplers.Add(resized,created);return created;});
    }
    public ValueTask DisposeAsync(){lock(retirementGate)return new(disposal??=RetireAsync());}
    private async Task RetireAsync()
    {
        await gate.WaitAsync();
        try
        {
            retiring=true;await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {foreach(var sampler in samplers.Values)rd.FreeRid(sampler);return true;});
            await Program.DisposeAsync();
        }
        finally{gate.Release();}
    }
}
