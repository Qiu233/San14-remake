using Godot;
using San14.Data;

/// <summary>The actual ordered primitive packet list for one selected capture
/// CView face, after its current visibility/culling preparation. Zero visibility
/// has its real bind/clear branch; no empty list is synthesized by the owner.</summary>
public sealed record ReflectionCaptureSceneFaceWork(uint Face,bool CurrentVisible,
    IReadOnlyList<ModelGbufferInstancePacket> ActualOrderedPrimitives);

/// <summary>Concrete capture Scene geometry submission and original conversion.
/// Actual state12 source packets carry the current culling-dependent camera,
/// clip planes, material, light and ambient bindings. No GPU completion/ready
/// receipt or callback can acknowledge unsubmitted Scene work.</summary>
public sealed class ReflectionCaptureSceneOwner:IAsyncDisposable
{
    public ReflectionCaptureSceneResources Resources {get;}
    public ModelGbufferLayer Models {get;}
    private readonly SemaphoreSlim gate=new(1,1);
    private Task? disposal;
    private ReflectionCaptureSceneOwner(ReflectionCaptureSceneResources resources,ModelGbufferLayer models)
        =>(Resources,Models)=(resources,models);
    public static async Task<ReflectionCaptureSceneOwner> BuildAsync(LoadedReflectionProducerPlan actualRefProbe,
        ModelGbufferLayer actualSharedModelCache)
    {
        ArgumentNullException.ThrowIfNull(actualSharedModelCache);
        return new(await ReflectionCaptureSceneResources.BuildAsync(actualRefProbe),actualSharedModelCache);
    }

    public async Task CaptureAndConvertAsync(int actualSourceOrdinal,
        IReadOnlyList<ReflectionCaptureSceneFaceWork> actualCurrentSixFaces,
        Rid actualCubeConversionSampler,ReflectionCaptureFieldExecutionOwner actualField)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentSixFaces);ArgumentNullException.ThrowIfNull(actualField);
        var faces=actualCurrentSixFaces.Select(value=>value with{ActualOrderedPrimitives=value.ActualOrderedPrimitives.ToArray()}).ToArray();
        if(faces.Length!=6||!faces.Select(value=>value.Face).Order().SequenceEqual(new uint[]{0,1,2,3,4,5}) ||
            faces.Any(value=>!value.CurrentVisible&&value.ActualOrderedPrimitives.Count!=0) || !actualCubeConversionSampler.IsValid ||
            !ReferenceEquals(actualField.Filter.ShaderState.Source,Resources.Source))
            throw new InvalidDataException("reflection.capture_scene.actual_view_inputs");
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposal is not null,this);Resources.RequireCurrent();actualField.Field.RequireCurrent();
            foreach(var face in faces.OrderBy(value=>value.Face))
            {
                var target=Resources.Faces[(int)face.Face];
                // Fresh CView's source clear mask3F binds all four colors/depth.
                // EC7100 also explicitly clears color3 with packedFF0000FF.
                // Same pre-draw contents are written in one real device clear.
                await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {
                    target.RequireCurrent();
                    long list=rd.DrawListBegin(target.DrawTarget.Framebuffer,
                        RenderingDevice.DrawFlags.ClearColorAll|RenderingDevice.DrawFlags.ClearDepth,
                        [new Color(0,0,0,0),new Color(0,0,0,0),new Color(0,0,0,0),new Color(1,0,0,1)],1,0);
                    rd.DrawListEnd();return true;
                });
                if(face.CurrentVisible)await Models.RenderReflectionCaptureAsync(target,face.ActualOrderedPrimitives);
            }
            ReflectionCapturedCube Cube(int color)=>new(Resources.Colors[color],Resources.Colors[color],actualCubeConversionSampler,
                Resources.Source.Graph.Source.ResourceUri+"/RefProbe/actual-capture-color/"+color);
            await actualField.ConvertActualCaptureAsync(actualSourceOrdinal,[Cube(2),Cube(3),Cube(1)]);
        }
        finally{gate.Release();}
    }
    public ValueTask DisposeAsync()=>new(disposal??=RetireAsync());
    private async Task RetireAsync()
    {
        await gate.WaitAsync();
        try{await Models.RetireReflectionCaptureTargetsAsync(Resources);await Resources.DisposeAsync();}
        finally{gate.Release();}
    }
}
