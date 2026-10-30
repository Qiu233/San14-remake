using Godot;
using San14.Data;

public sealed partial class ModelGbufferLayer
{
    private sealed record CaptureDrawValue(ModelShaderProgram Program,ModelGeometryDevice Geometry,int DrawIndex,
        ReflectionCaptureSceneFaceTarget Target,ModelRasterInputs Raster,ModelDeviceBinding[] VertexResources,
        ModelDeviceBinding[] PixelResources,ModelDraw Draw);
    private readonly Dictionary<(ReflectionCaptureSceneFaceTarget Target,string Key),CaptureDrawValue> _captureDraws=[];

    private static void RequireColorProgram(ModelShaderProgram value,bool reflectionCapture)
    {
        uint[] expected=reflectionCapture?[0,1,2,3]:[0,1,2,3,4];
        if (!value.ColorOutputLocations.SequenceEqual(expected) ||
            value.PixelBindings.Interface.StageVariables.Where(variable=>variable.StorageClass==3).Any(variable=>
                variable.Type is not {Kind:"vector",ComponentCount:4,ComponentType:{Kind:"float",Bits:32}}) ||
            reflectionCapture&&!value.Source.ReadConfigurations().Records.Any(configuration=>configuration.Name=="ReflectionProbeGBuffer"))
            throw new InvalidDataException("Original model layer program differs from its exact ordinary or capture color contract.");
    }

    /// <summary>Concrete state12 geometry submission into one actual four-color
    /// Cube face. Shares the mature program/geometry/material-image caches;
    /// no RenderGraph target or ordinary GBuffer frame is synthesized.</summary>
    public Task RenderReflectionCaptureAsync(ReflectionCaptureSceneFaceTarget actualTarget,
        IReadOnlyList<ModelGbufferInstancePacket> actualOrderedPrimitives)
    {
        ArgumentNullException.ThrowIfNull(actualTarget);ArgumentNullException.ThrowIfNull(actualOrderedPrimitives);
        AcceptOperation();
        try
        {
            actualTarget.RequireCurrent();var snapshot=actualOrderedPrimitives.Select(Snapshot).ToArray();
            if(snapshot.Select(value=>value.InstanceKey).Distinct().Count()!=snapshot.Length)
                throw new InvalidDataException("reflection.capture_scene.primitive_keys");
            return RenderReflectionCaptureAcceptedAsync(actualTarget,snapshot);
        }
        catch{CompleteOperation();throw;}
    }

    private async Task RenderReflectionCaptureAcceptedAsync(ReflectionCaptureSceneFaceTarget target,
        ModelGbufferInstancePacket[] instances)
    {
        await _gate.WaitAsync();bool adopted=false;var next=new List<(string Key,CaptureDrawValue Value,ModelStageInputs Vertex,ModelStageInputs Pixel,bool Reused)>();
        try
        {
            _drawRetirement=await NativeMapBorrowerRetirementPackage.DrainPendingAsync(_drawRetirement);target.RequireCurrent();
            foreach(var instance in instances)
            {
                var program=await ProgramAsync(instance,true);
                var geometry=await GeometryAsync(instance.Model,program,instance.DrawIndex,instance.ArmySkinProfile);
                var vertex=await StageAsync(instance.Vertex);var pixel=await StageAsync(instance.Pixel);
                ValidateConstants(program.VertexBindings,vertex.Constants);ValidateConstants(program.PixelBindings,pixel.Constants);
                bool same=_captureDraws.TryGetValue((target,instance.InstanceKey),out var prior)&&ReferenceEquals(prior.Program,program)&&
                    ReferenceEquals(prior.Geometry,geometry)&&prior.DrawIndex==instance.DrawIndex&&SameRaster(prior.Raster,instance.Raster)&&
                    prior.VertexResources.SequenceEqual(vertex.Resources)&&prior.PixelResources.SequenceEqual(pixel.Resources);
                CaptureDrawValue value;
                if(same)value=prior!;
                else
                {
                    var draw=await ModelDraw.BuildAsync(program,geometry,instance.DrawIndex,target.DrawTarget,vertex,pixel,instance.Raster);
                    DrawCreationCount++;
                    value=new(program,geometry,instance.DrawIndex,target,instance.Raster,vertex.Resources.ToArray(),pixel.Resources.ToArray(),draw);
                }
                next.Add((instance.InstanceKey,value,vertex,pixel,same));
            }
            var updates=next.Where(value=>value.Reused).Select(value=>(value.Value.Draw,
                Constants:value.Value.Draw.SnapshotUpdate(value.Vertex.Constants,value.Pixel.Constants))).ToArray();
            var written=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                target.RequireCurrent();if(!rd.FramebufferIsValid(target.DrawTarget.Framebuffer))throw new InvalidDataException("reflection.capture_scene.target");
                ModelConstantUpdateStatistics total=default;
                foreach(var update in updates)total+=update.Draw.UpdateOnRenderThread(rd,update.Constants.Vertex,update.Constants.Pixel);
                if(next.Count!=0)
                {
                    long list=rd.DrawListBegin(target.DrawTarget.Framebuffer,RenderingDevice.DrawFlags.DefaultAll,[],1,0);
                    try{foreach(var value in next)value.Value.Draw.DrawInList(rd,list);}
                    finally{rd.DrawListEnd();}
                }
                return total;
            });
            LastConstantUpdates=written;ConstantBufferUpdateCount+=written.BufferUpdates;ConstantBufferUpdatedBytes+=written.UpdatedBytes;FrameBatchDispatchCount++;
            var obsolete=_captureDraws.Where(value=>ReferenceEquals(value.Key.Target,target)).ToArray();
            foreach(var value in obsolete)_captureDraws.Remove(value.Key);
            foreach(var value in next)_captureDraws.Add((target,value.Key),value.Value);
            adopted=true;var kept=next.Select(value=>value.Value).ToHashSet(ReferenceEqualityComparer.Instance);
            _drawRetirement=NativeMapBorrowerRetirementPackage.CreatePending("Model-layer-capture-replaced",
                obsolete.Select(value=>value.Value).Where(value=>!kept.Contains(value)).Reverse().Select(value=>value.Draw));
            _drawRetirement=await NativeMapBorrowerRetirementPackage.DrainPendingAsync(_drawRetirement);
        }
        catch(Exception failure)
        {
            if(adopted)throw;
            _drawRetirement=NativeMapBorrowerRetirementPackage.CreatePending("Model-layer-capture-failed",
                next.Where(value=>!value.Reused).Reverse().Select(value=>value.Value.Draw),
                (failure as NativeMapBorrowerConstructionException)?.RetainedPackage);
            try{_drawRetirement=await NativeMapBorrowerRetirementPackage.DrainPendingAsync(_drawRetirement);}
            catch(Exception retirement){throw new NativeMapBorrowerConstructionException(failure,retirement,_drawRetirement!);}
            throw;
        }
        finally{_gate.Release();CompleteOperation();}
    }

    public Task RetireReflectionCaptureTargetsAsync(ReflectionCaptureSceneResources actualOwner)
    {ArgumentNullException.ThrowIfNull(actualOwner);AcceptOperation();return RetireReflectionCaptureAcceptedAsync(actualOwner);}
    private async Task RetireReflectionCaptureAcceptedAsync(ReflectionCaptureSceneResources owner)
    {
        await _gate.WaitAsync();
        try
        {
            _drawRetirement=await NativeMapBorrowerRetirementPackage.DrainPendingAsync(_drawRetirement);
            var retired=_captureDraws.Where(value=>ReferenceEquals(value.Key.Target.Owner,owner)).ToArray();
            _drawRetirement=NativeMapBorrowerRetirementPackage.CreatePending("Model-layer-capture-targets",retired.Reverse().Select(value=>value.Value.Draw));
            foreach(var value in retired)_captureDraws.Remove(value.Key);
            _drawRetirement=await NativeMapBorrowerRetirementPackage.DrainPendingAsync(_drawRetirement);
        }
        finally{_gate.Release();CompleteOperation();}
    }
}
