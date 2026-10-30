using Godot;
using San14.Data;

public sealed class ReflectionCaptureSceneFaceTarget
{
    internal ReflectionCaptureSceneFaceTarget(ReflectionCaptureSceneResources owner,uint face,ModelDrawTarget target)
        => (Owner,Face,DrawTarget)=(owner,face,target);
    public ReflectionCaptureSceneResources Owner {get;}
    public uint Face {get;}
    public ModelDrawTarget DrawTarget {get;}
    public void RequireCurrent()=>Owner.RequireCurrent();
}

/// <summary>EC9960's actual four RGBA16_FLOAT Cube attachments and one D32
/// Cube depth, each256 square and one mip. Owns six true four-MRT face targets;
/// these are not screen GBuffers or Field radiance substitutes.</summary>
public sealed class ReflectionCaptureSceneResources:IAsyncDisposable
{
    public LoadedReflectionProducerPlan Source {get;}
    public IReadOnlyList<Rid> Colors {get;}
    public Rid Depth {get;}
    public IReadOnlyList<ReflectionCaptureSceneFaceTarget> Faces {get;private set;}=[];
    private readonly RenderDeviceResources resources;
    private bool retiring;
    private ReflectionCaptureSceneResources(LoadedReflectionProducerPlan source,Rid[] colors,Rid depth,RenderDeviceResources resources)
        =>(Source,Colors,Depth,this.resources)=(source,Array.AsReadOnly(colors),depth,resources);
    public static Task<ReflectionCaptureSceneResources> BuildAsync(LoadedReflectionProducerPlan actualRefProbe)
    {
        ArgumentNullException.ThrowIfNull(actualRefProbe);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var resources=new RenderDeviceResources("Original reflection capture Cube resources were rejected.");
            try
            {
                Rid Cube(RenderingDevice.DataFormat format,RenderingDevice.TextureUsageBits usage)
                {
                    using var declaration=new RDTextureFormat{Format=format,Width=256,Height=256,Depth=1,ArrayLayers=6,Mipmaps=1,
                        TextureType=RenderingDevice.TextureType.Cube,Samples=RenderingDevice.TextureSamples.Samples1,UsageBits=usage};
                    using var view=new RDTextureView();return resources.Own(rd.TextureCreate(declaration,view));
                }
                var colors=Enumerable.Range(0,4).Select(_=>Cube(RenderingDevice.DataFormat.R16G16B16A16Sfloat,
                    RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.ColorAttachmentBit)).ToArray();
                var depth=Cube(RenderingDevice.DataFormat.D32Sfloat,
                    RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit);
                var owner=new ReflectionCaptureSceneResources(actualRefProbe,colors,depth,resources);
                var faces=new List<ReflectionCaptureSceneFaceTarget>();
                for(uint face=0;face<6;face++)
                {
                    Rid Slice(Rid texture)
                    {
                        using var view=new RDTextureView();
                        return resources.Own(rd.TextureCreateSharedFromSlice(view,texture,face,0,1,RenderingDevice.TextureSliceType.Slice2D));
                    }
                    var views=colors.Select(Slice).ToArray();var depthView=Slice(depth);
                    var attachments=new Godot.Collections.Array<Rid>(views);attachments.Add(depthView);
                    Rid framebuffer=resources.Own(rd.FramebufferCreate(attachments));
                    if(!rd.FramebufferIsValid(framebuffer))throw new InvalidDataException("reflection.capture_scene.four_mrt_target");
                    var target=new ModelDrawTarget(framebuffer,Array.AsReadOnly(views),depthView,
                        Array.AsReadOnly(colors.Append(depth).ToArray()),actualRefProbe.Graph.Source.ResourceUri+"/RefProbe/capture-face/"+face);
                    faces.Add(new(owner,face,target));
                }
                owner.Faces=faces.AsReadOnly();return owner;
            }
            catch(Exception failure){resources.ReleaseAfterConstructionFailure(rd,failure,"Reflection-capture-scene-partial");throw;}
        });
    }
    public void RequireCurrent()=>ObjectDisposedException.ThrowIf(retiring,this);
    public ValueTask DisposeAsync(){retiring=true;return resources.DisposeAsync();}
}
