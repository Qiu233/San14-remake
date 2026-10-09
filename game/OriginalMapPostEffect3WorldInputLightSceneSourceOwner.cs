using System.Buffers.Binary;
using Godot;
using San14.Data;

/// <summary>The active dedicated LightScene producer.
/// It owns the original 2A691F4A/native107 target and performs its one stock
/// mode1 copy. This borrows the accepted World Scene; it does not run the
/// unrelated A1 effects, alias the Scene, or change the source effect flags.</summary>
public sealed class OriginalMapPostEffect3WorldInputLightSceneSourceOwner:IAsyncDisposable
{
    private readonly OriginalMapSourceCreatedWorldPlayableHost host;
    private readonly OriginalMapFinalOutputPassOwner.NativeRecipeOwner native;
    private readonly OriginalRenderTextureDefinition declaration;
    private readonly OriginalRenderTextureRuntimeDescriptor descriptor;
    private OriginalMapPostEffect3WorldStockGraphicsSourceOwner? world;
    private OriginalMapPostEffect3ResolvedTextureParameter? selection;
    private OriginalModelDeviceBinding? binding;
    private OriginalMapExposurePacket? exposure;
    private long exposureDrawCount;
    private bool retiring;
    private long acceptedFrameNumber,completedDrawCount,completedFrameCount;
    private readonly byte[] nativeSamplerKey;
    public string OwnerIdentity=>descriptor.OwnerIdentity;
    public OriginalRenderTextureRuntimeDescriptor EffectiveSource=>descriptor;
    public long SubmittedDrawCount=>native.SubmittedDrawCount;
    public ReadOnlyMemory<byte> NativeSamplerKey=>nativeSamplerKey.ToArray();
    public string SourceEvidence=>descriptor.SourceProvenance+
        ";dedicated-light-scene-target-2A691F4A;"+
        "black-clear/stock-mode1-Scene-copy/target-pop;separate-actual-image";
    private OriginalMapPostEffect3WorldInputLightSceneSourceOwner(OriginalMapSourceCreatedWorldPlayableHost host,
        OriginalMapFinalOutputPassOwner.NativeRecipeOwner native,OriginalRenderTextureDefinition declaration)
    {
        this.host=host;this.native=native;this.declaration=declaration;
        descriptor=OriginalRenderTextureRuntimeDescriptor.ConstructDeclared(declaration,
            "owned-first-World-dedicated-LightScene:"+Guid.NewGuid().ToString("N"));
        nativeSamplerKey=OriginalDeferredSamplerSources.FinalRenderTextureKey(native.Graph.Source.StockScreen.Native,declaration);
    }
    public static async Task<OriginalMapPostEffect3WorldInputLightSceneSourceOwner> BuildAsync(
        OriginalMapSourceCreatedWorldPlayableHost host,Vkd3dShaderCompiler compiler,bool allowReadback=false)
    {
        var graph=host.Final.Chain.Graph;
        var declaration=OriginalRenderTextureResources.Read(graph.Source.TextureObjects,OriginalMapFinalOutputShaft.LightSceneObjectId);
        var formats=OriginalRenderTextureFormatMath.Resolve(declaration);
        if(declaration.Object.ObjectId!=0x2A691F4A||declaration.PixelFormat!=107||declaration.Width!=0||declaration.Height!=0||
            declaration.LevelNum!=1||declaration.ColorSpace!=0||declaration.OutputColorSpace!=0||
            formats.ShaderViewFormat!=26||formats.TargetViewFormat!=26)
            throw new InvalidDataException("Dedicated LightScene differs from its original dynamic native107 single2D target.");
        var native=await OriginalMapFinalOutputPassOwner.NativeRecipeOwner.BuildAsync(graph,
            [new("light",OriginalMapFinalOutputNativeProgramKind.StockScreenDefault1,-1)],compiler,allowReadback);
        return new(host,native,declaration);
    }

    public async Task BindAndProduceCurrentAsync(OriginalMapPostEffect3WorldStockGraphicsSourceOwner currentWorld,
        OriginalMapPostEffect3OwnedFreshCameraFrame parameters,OriginalMapCameraConstructionPacket currentCamera,
        OriginalMapPostEffect3OwnedDirectCaller actualCaller)
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        if(currentWorld.Context.FrameNumber<=acceptedFrameNumber||currentWorld.Context.FrameNumber is not(1 or 2))
            throw new InvalidOperationException("Dedicated source LightScene requires a genuinely later bounded World frame.");
        long expectedDraw=native.SubmittedDrawCount+1,expectedFrame=native.AcceptedFrameCount+1;
        acceptedFrameNumber=currentWorld.Context.FrameNumber;binding=null;selection=null;
        currentWorld.RequireCurrent();parameters.RequireCurrent();
        parameters.Route.ValidateSameCamera(currentCamera);actualCaller.ValidateSameCamera(currentCamera);
        if(!actualCaller.ActualLightShaftArgument||actualCaller.FrameNumber!=currentWorld.Context.FrameNumber)
            throw new InvalidDataException("Dedicated LightScene requires the same native positive length/RGB caller predicate.");
        var field=currentCamera.Source.Camera.Properties[142];
        if(field.Index!=142||field.NameHash!=0x4168C674||field.TypeCode!=5||field.ElementCount!=1||field.RawValue.Length!=4||
            BinaryPrimitives.ReadInt32LittleEndian(field.RawValue)==0)
            throw new InvalidDataException("Dedicated LightScene requires the actual fresh nonzero Camera scene-type field.");
        int index=parameters.State.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,"InputLightScene")??
            throw new InvalidDataException("Original InputLightScene parameter is absent.");
        var context=currentWorld.Context;var scene=context.OutputFrame.Textures[0x064B746B];
        var stock=currentWorld.CurrentStock;stock.ValidateCurrent();
        exposure=host.Final.Chain.Exposure.LastPacket??throw new InvalidDataException("The source node1 exposure prefix has not completed.");
        exposureDrawCount=host.Final.Chain.Exposure.DrawCount;
        host.Final.Chain.Exposure.RequireCompletedCurrentFrame(context,exposure,exposureDrawCount);
        int width=scene.ExplicitExtent.X,height=scene.ExplicitExtent.Y;
        // Disabled alpha test writes X=-FLT_MAX. The PS1 reads only X;
        // the other twelve bytes are unread ABI padding, not fabricated source
        // state. The complete source stock VS projection/color is retained from
        // the actual current frontend's six proved words.
        byte[] vs=Floats((2f/width)*stock.CurrentScreenScale29E0.X,(-2f/height)*stock.CurrentScreenScale29E0.Y,-1,1,
            stock.CurrentColorScale29F0.X,stock.CurrentColorScale29F0.Y,stock.CurrentColorScale29F0.Z,stock.CurrentColorScale29F0.W);
        byte[] ps=new byte[16];BinaryPrimitives.WriteSingleLittleEndian(ps,exposure.AlphaTestThreshold);
        byte[] vertices=new byte[96];float[][] corners=[[0,0,0,0],[0,height,0,1],[width,height,1,1],[width,0,1,0]];
        for(int i=0;i<4;i++)
        {
            Write(vertices,i*24,corners[i][0]);Write(vertices,i*24+4,corners[i][1]);Write(vertices,i*24+8,0);
            BinaryPrimitives.WriteUInt32LittleEndian(vertices.AsSpan(i*24+12),uint.MaxValue);
            Write(vertices,i*24+16,corners[i][2]);Write(vertices,i*24+20,corners[i][3]);
        }
        // Actual screen begin sets CullNone, disables depth, and the
        // dedicated copy replaces rather than accumulates destination pixels.
        var attachment=new OriginalModelBlendAttachment(false,RenderingDevice.BlendFactor.One,RenderingDevice.BlendFactor.Zero,
            RenderingDevice.BlendOperation.Add,RenderingDevice.BlendFactor.One,RenderingDevice.BlendFactor.Zero,
            RenderingDevice.BlendOperation.Add,true,true,true,true);
        var raster=new OriginalModelRasterInputs(RenderingDevice.PolygonCullMode.Disabled,RenderingDevice.PolygonFrontFace.Clockwise,
            false,false,RenderingDevice.CompareOperator.Always,[attachment]);
        byte[] key=OriginalDeferredSamplerSources.FinalRenderTextureKey(native.Graph.Source.StockScreen.Native,scene.EffectiveSource.Declaration);
        var packet=new OriginalMapFinalOutputPassDrawPacket(-1,
            new Dictionary<uint,ReadOnlyMemory<byte>>{{0,vs}},new Dictionary<uint,ReadOnlyMemory<byte>>{{0,ps}},vertices,
            new Dictionary<uint,ReadOnlyMemory<byte>>{{0,OriginalDeferredSamplerSources.DecodeKey(native.Graph.Source.StockScreen.Native,key).EncodeD3D11Descriptor()}},raster);
        var targets=await native.RenderAsync(new(context.FrameNumber,
            [new(23,23,width,height,SourceEvidence){SourceDeclaration=declaration}],
            new Dictionary<int,OriginalModelDeviceBinding>{{0,new("Texture",0,scene.ShaderViewRid,scene.StorageRid,currentWorld.SourceEvidence)}},
            [new("light",[23],new Dictionary<uint,int>{{0,0}},packet,new Color(0,0,0,1),SourceEvidence)],SourceEvidence));
        currentWorld.RequireCurrent();parameters.RequireCurrent();actualCaller.ValidateSameCamera(currentCamera);
        host.Final.Chain.Exposure.RequireCompletedCurrentFrame(context,exposure,exposureDrawCount);
        if(native.SubmittedDrawCount!=expectedDraw||native.AcceptedFrameCount!=expectedFrame||!targets.TryGetValue(23,out var produced))
            throw new InvalidDataException("Dedicated source LightScene draw did not complete its actual target publication.");
        world=currentWorld;binding=produced;completedDrawCount=expectedDraw;completedFrameCount=expectedFrame;
        currentWorld.TextureParameters.BindExternal(index,new(context.FrameNumber,OwnerIdentity),SourceEvidence);
        selection=currentWorld.TextureParameters.Require(index);RequireCurrent();
    }
    public void RequireCurrent()
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        if(world is null||selection is null||binding is null||exposure is null||native.SubmittedDrawCount!=completedDrawCount||native.AcceptedFrameCount!=completedFrameCount||
            native.LatestTextures is null||!native.LatestTextures.TryGetValue(23,out var actual)||actual!=binding)
            throw new InvalidDataException("Dedicated LightScene is not an actual completed current source draw.");
        world.RequireCurrent();world.TextureParameters.ValidateCurrent(selection);
        host.Final.Chain.Exposure.RequireCompletedCurrentFrame(world.Context,exposure,exposureDrawCount);
        if(selection.Reference.ProducerOwnerIdentity!=OwnerIdentity||selection.Reference.PhysicalAllocationParameterIndex is not null)
            throw new InvalidDataException("InputLightScene no longer selects this completed separate source image.");
    }
    public IOriginalMapPostEffect3ComputeExternalTextureLease Lease(){RequireCurrent();return new LightLease(this);}
    private sealed record LightLease(OriginalMapPostEffect3WorldInputLightSceneSourceOwner Owner):IOriginalMapPostEffect3ComputeExternalTextureLease
    {
        public long FrameNumber=>Owner.world!.Context.FrameNumber;
        public string ContextOwnerIdentity=>Owner.world!.Bank.Source.ContextOwnerIdentity;
        public int ParameterIndex=>Owner.selection!.ParameterIndex;
        public int InstanceIndex=>0;
        public Rid ShaderViewRid=>Owner.binding!.Resource;
        public Rid StorageRid=>Owner.binding!.OwnerTexture!.Value;
        public uint NativeViewFormat=>26;
        public RenderingDevice.DataFormat DeviceFormat=>RenderingDevice.DataFormat.B10G11R11UfloatPack32;
        public Vector3I Extent{get{var s=Owner.world!.Context.OutputFrame.Textures[0x064B746B].ExplicitExtent;return new(s.X,s.Y,1);}}
        public string ProducerOwnerIdentity=>Owner.OwnerIdentity;
        public string SourceEvidence=>Owner.SourceEvidence;
        public void RequireCurrent()=>Owner.RequireCurrent();
    }
    private static byte[] Floats(params float[] values){byte[] bytes=new byte[values.Length*4];for(int i=0;i<values.Length;i++)Write(bytes,i*4,values[i]);return bytes;}
    private static void Write(byte[] bytes,int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at,4),value);
    public async ValueTask DisposeAsync(){retiring=true;binding=null;selection=null;world=null;await native.DisposeAsync();}
}
