using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMapFinalOutputProductionPrepared(
    long FrameNumber,OriginalMapCameraConstructionPacket Camera,
    OriginalMapExposurePacket Exposure,OriginalMapFinalOutputDofWeightPacket Weight,
    IReadOnlyDictionary<int,ReadOnlyMemory<byte>> CurrentParameters,
    IReadOnlyDictionary<int,IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>>> RetainedTemplatesByPass,
    ReadOnlyMemory<byte> FinalVertex,ReadOnlyMemory<byte> FinalPixel,
    ReadOnlyMemory<byte> RadialUv,ReadOnlyMemory<byte> StockPixel,float ApplyLuminance,
    IReadOnlyList<string> SourceWrites,IReadOnlyList<string> UnreadStorageRanges,string SourceEvidence);

/// <summary>Owned source SPK parameter and stage/register shadow state. New
/// shadow storage has unknown bytes, represented by zero only in unread ABI
/// padding. Source parameter/Camera/functor writers must cover every shader
/// read before a packet is published. No captured native template is claimed.</summary>
public sealed class OriginalMapFinalOutputProductionState
{
    public OriginalMapFinalOutputPassGraph Graph { get; }
    public long CommittedFrameNumber { get; private set; }
    public float SourceApplyLuminance { get; private set; }=1f;
    public string OwnedApplyLuminanceReferenceIdentity { get; }="owned-Final-ApplyLuminance-reference:"+Guid.NewGuid().ToString("N");
    private Dictionary<int,ReadOnlyMemory<byte>> parameters;
    private readonly Dictionary<(uint,uint),byte[]> shadow=[];
    private readonly Dictionary<(uint,uint),bool[]> known=[];
    private byte[] radialUv=new byte[48];
    private OriginalMapFinalOutputProductionPrepared? pending;
    private Dictionary<(uint,uint),byte[]>? pendingShadow;
    private Dictionary<(uint,uint),bool[]>? pendingKnown;
    private byte[]? pendingRadial;
    private static readonly int[] OrdinaryPasses=[0,1,2,3,10,11,12,13,14,15,16,17,19,20,21,22,23,24,27];
    public OriginalMapFinalOutputProductionState(OriginalMapFinalOutputPassGraph graph)
    {Graph=graph??throw new ArgumentNullException(nameof(graph));parameters=graph.CreateFreshSourceParameterState().Parameters.ToDictionary(p=>p.Key,p=>p.Value);}

    /// <summary>Explicit later writer of the owned global27DDDE0 object's18
    /// value. Its source constructor9A4A77 starts with literal1; node1 replaces
    /// it with its clamped selected luminance before this owner's Final draw.</summary>
    public void WriteApplyLuminance(float value,string actualWriter)
    {if(pending is not null||!float.IsFinite(value)||string.IsNullOrWhiteSpace(actualWriter))throw new InvalidDataException("Apply luminance needs a finite explicit writer outside pending work.");SourceApplyLuminance=value;}

    public OriginalMapFinalOutputProductionPrepared Prepare(
        OriginalMapCameraConstructionPacket currentCamera,OriginalMapSourceCreatedLightingPacket acceptedLighting,
        OriginalMapSeasonRuntimeFrame currentSeason,OriginalMapExposureViewAux selectedViewAux,
        int width,int height,bool currentTargetIsSrgb,OriginalNativeCameraTangentDispatch tangent,
        string actualFrameOwner,OriginalMapExposureSelectedLightReceipt? selectedExposure=null,
        OriginalMapFinalOutputHdrRoute? selectedHdr=null,float? resolvedBloomMaxLum=null,
        OriginalMapFinalOutputCameraBloomSource? sourceBloom=null)
    {
        ArgumentNullException.ThrowIfNull(currentCamera);ArgumentNullException.ThrowIfNull(acceptedLighting);
        ArgumentNullException.ThrowIfNull(currentSeason);ArgumentNullException.ThrowIfNull(selectedViewAux);
        if(pending is not null||currentCamera.FrameNumber<=CommittedFrameNumber||currentCamera.FrameNumber!=acceptedLighting.FrameNumber||
            currentCamera.FrameNumber!=currentSeason.FrameNumber||width<=0||height<=0||width>ushort.MaxValue||height>ushort.MaxValue||
            selectedViewAux.ToneType3A4!=2||(selectedViewAux.Flags8&((1u<<22)|(1u<<23)))!=0||
            !selectedViewAux.CustomPixelShaderAbsent||string.IsNullOrWhiteSpace(actualFrameOwner)||
            !currentCamera.Prefix.Span[..0x170].SequenceEqual(acceptedLighting.PreparedCameraPrefix.Span[..0x170]))
            throw new InvalidDataException("Production final requires one increasing accepted Camera/lighting/season frame and the source saved Tone2 Host route.");
        OriginalMapSeasonRuntime.ValidateOwned(currentSeason.State);
        var canonical=OriginalMapSeasonRuntime.PrepareFrame(currentSeason.State,currentSeason.FrameNumber);
        if(canonical!=currentSeason)throw new InvalidDataException("Post-output season is not its canonical current owned World frame.");
        if(selectedHdr is not null&&(selectedHdr.FrameNumber!=currentCamera.FrameNumber||selectedHdr.Width!=width||
            selectedHdr.Height!=height||selectedHdr.CallerOwnerIdentity!=actualFrameOwner||
            resolvedBloomMaxLum is not float bloom||!float.IsFinite(bloom)))
            throw new InvalidDataException("Resolved HDR needs this actual working extent/frame/owner and an explicit finite Bloom caller value.");
        if(selectedHdr is null&&resolvedBloomMaxLum is not null)
            throw new InvalidDataException("Bloom caller override requires its resolved HDR route.");
        if(sourceBloom is not null)
        {
            sourceBloom.ValidateSameCamera(currentCamera);
            if(selectedHdr is null||resolvedBloomMaxLum!=sourceBloom.CurrentMaxLum)
                throw new InvalidDataException("Fresh source Bloom receipt must supply the same resolved caller value.");
        }
        bool trueHdr=selectedHdr?.ResolvedTrueHdr??false;
        // Existing explicit mode2 fixtures keep their narrow absence proof.
        // Production Directional frames resolve the selected View record and
        // native exposure branch through its independent typed receipt.
        if(selectedExposure is not null)
        {
            if(selectedExposure.OwnedApplyReferenceIdentity!=OwnedApplyLuminanceReferenceIdentity)
                throw new InvalidDataException("Exposure receipt must use this Final owner's actual ApplyLuminance reference.");
            selectedExposure.ValidateFor(acceptedLighting);
        }
        else if(acceptedLighting.LocalLightData.Length!=OriginalMainLightConstruction.LightDataBytes||
            acceptedLighting.LocalLightData.Span[0x10B0]!=2||(acceptedLighting.LocalLightData.Span[0xFDC]&1)!=0)
            throw new InvalidDataException("Production final exposure requires the actual owned mode2/FDC0 Camera-luminance branch; external selected-light ownership is unresolved.");
        var camera=new OriginalMapCameraConstructionPacket(acceptedLighting.PreparedCameraPrefix.ToArray(),currentCamera.Source,
            currentCamera.CurrentPose,currentCamera.Lens,currentCamera.Matrices,currentCamera.ExecutableLocale,
            currentCamera.FrameNumber,currentCamera.FrameProvenance+";"+acceptedLighting.SourceProvenance+";"+currentSeason.SourceProvenance);
        var exposure=OriginalMapExposureSource.Prepare(new(camera.FrameNumber,camera.Prefix,camera.FrameProvenance,selectedViewAux,
            OriginalMapExposureSurface.ExistingViewModeInvariant(selectedViewAux,actualFrameOwner+"/actual-node73-working-surface"),
            camera.SourceSpecialRectangleFlag1510!=0,null,selectedExposure?.SelectedExternalLuminance));
        // EA97E2..EA98A3 updates the same reference18 consumed by Final.
        // Exposure's own tone packet uses the unclamped selected luminance.
        float epsilon=BitConverter.UInt32BitsToSingle(0x358637BD);
        float applyLuminance=exposure.Luminance<epsilon?epsilon:exposure.Luminance;
        var weight=OriginalMapFinalOutputDofWeight.PrepareSourceCreatedCamera(Graph.Source,camera,height,0,1,tangent,
            actualFrameOwner+";owned-full-viewport-native-depth0..1");
        var current=parameters.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
        current[14]=Floats(0,0,1,1);current[15]=Floats(0,0,1,1);current[16]=Floats(weight.EyeToAtLength);
        foreach(var (index,at) in new[]{(17,0x15C),(18,0x148),(19,0x140),(20,0x144)})current[index]=camera.Prefix.Slice(at,4).ToArray();
        uint dof=camera.Source.CpuWord(0xBF0);current[25]=Floats(dof<=2?dof:0);
        if(dof!=0)throw new NotSupportedException("Production final source owner selects its proved DOF0 route.");
        current=OriginalMapFinalOutputBloomStarFlare.ApplySourceCreatedCameraParameters(Graph,camera,current,actualFrameOwner,
            trueHdr,resolvedBloomMaxLum??0).ToDictionary(p=>p.Key,p=>p.Value);
        var shaft=OriginalMapFinalOutputShaft.PrepareSourceGroupZero(Graph,camera,(uint)width,(uint)height,current,actualFrameOwner);
        current=shaft.CurrentParameters.ToDictionary(p=>p.Key,p=>p.Value);
        var nextShadow=shadow.ToDictionary(p=>p.Key,p=>p.Value.ToArray());var nextKnown=known.ToDictionary(p=>p.Key,p=>p.Value.ToArray());
        var templates=new Dictionary<int,IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>>>();
        var writes=new List<string>();var unread=new List<string>();byte[]? retainedStock=null;
        byte[] Store((uint,uint) key,int size){if(!nextShadow.TryGetValue(key,out var b)){b=new byte[(size+1023)&~1023];nextShadow.Add(key,b);nextKnown.Add(key,new bool[b.Length]);}return b;}
        IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> Retained(int index)
        {
            var result=Graph.Passes[index].ConstantBuffers.ToDictionary(b=>(b.StageCode,b.Register),b=>(ReadOnlyMemory<byte>)Store((b.StageCode,b.Register),b.ByteSize).AsMemory(0,b.ByteSize).ToArray());
            return templates[index]=result;
        }
        void CommitDraw(int index,IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> constants)
        {
            foreach(var buffer in Graph.Passes[index].ConstantBuffers)
            {
                var key=(buffer.StageCode,buffer.Register);var storage=Store(key,buffer.ByteSize);constants[key].Span.CopyTo(storage);
                var mask=nextKnown[key];
                foreach(var slice in buffer.SourceParameterSlices)Array.Fill(mask,true,slice.Offset,slice.ByteSize);
                var rdef=DxbcResourceDefinitionReader.Read(buffer.StageCode==1?Graph.Passes[index].Program.VertexBytes.Span:Graph.Passes[index].Program.PixelBytes.Span);
                var original=rdef.Buffers.Single(b=>b.ByteSize==buffer.ByteSize);
                if(buffer.StageCode==1)
                {
                    Array.Fill(mask,true,0,16);int uv=VertexUvCount(Graph.Passes[index].Program.VertexProgramIndex);
                    if(uv>0)Array.Fill(mask,true,16,uv*16);
                    Array.Fill(mask,true,buffer.ByteSize==32?16:272,16);
                }
                foreach(var field in original.Variables.Where(v=>(v.Flags&2)!=0))
                {
                    int size=field.Name.Text=="g_vUVOffsetSampling"?VertexUvCount(Graph.Passes[index].Program.VertexProgramIndex)*16:field.ByteSize;
                    if(mask.AsSpan(field.StartOffset,size).Contains(false))throw new InvalidDataException($"Source pass{index} {buffer.StageCode}/{buffer.Register} reads unwritten {field.Name.Text}.");
                }
                for(int i=0;i<buffer.ByteSize;){if(mask[i]){i++;continue;}int begin=i;while(i<buffer.ByteSize&&!mask[i])i++;unread.Add($"pass{index}:stage{buffer.StageCode}:b{buffer.Register}:{begin}+{i-begin}");}
                writes.Add($"pass{index}:stage{buffer.StageCode}:b{buffer.Register}:native named parameters+wrapper/functor writes");
            }
        }
        var finalBefore=Retained(28);var final=Graph.BuildConstants(28,finalBefore,current);
        var cameraWrites=OriginalMapFinalOutputConstants.ApplySourceCreatedCamera(Graph.Passes[28].Program,final[(0,0)].Span,camera,currentTargetIsSrgb,false,1);
        var sceneWrites=OriginalMapFinalOutputSceneConstants.ApplySourceCreatedCamera(Graph.Passes[28].Program,cameraWrites.Bytes,camera,
            new(camera.FrameNumber,camera.Prefix,(uint)width,(uint)height,0,0,0,shaft.Power,applyLuminance,.2f,trueHdr,0,tangent,actualFrameOwner+";source-node1-reference18-writer"));
        foreach(var slice in Graph.Passes[28].ConstantBuffers.Single(b=>b.StageCode==0&&b.Register==0).SourceParameterSlices)
            current[slice.ParameterIndex]=sceneWrites.Bytes.AsMemory(slice.Offset,slice.ByteSize).ToArray();
        foreach(int index in OrdinaryPasses)
        {
            var retained=Retained(index);IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> constants;
            if(index is 0 or 1 or 2 or 27)
            {
                var a=Graph.ResolveAllocation(Graph.Passes[index].Outputs[0].TextureIndex,width,height);
                var cb=Graph.BuildConstants(index,retained,current).ToDictionary(p=>p.Key,p=>p.Value);
                cb[(1,0)]=Graph.BuildRequiredPassVertexConstants(index,a.Width,a.Height,cb[(1,0)],new(0,0,index==27?radialUv:ReadOnlyMemory<byte>.Empty),current);constants=cb;
            }
            else if(index==3)constants=OriginalMapFinalOutputHexDof.Create(Graph,width,height,retained,current,new(0,0,ReadOnlyMemory<byte>.Empty)).Draws.Last().Constants;
            else if(index==16)constants=OriginalMapFinalOutputBloomStarFlare.CreateStar(Graph,width,height,retained,current,new(0,0,ReadOnlyMemory<byte>.Empty),camera.Prefix,OriginalMapFinalOutputShaft.CopyNativeQuad96()).Draws.Last().Constants;
            else if(index is >=19 and <=24)constants=OriginalMapFinalOutputShaft.BuildDrawConstants(Graph,index,width,height,retained,shaft,new(0,0,ReadOnlyMemory<byte>.Empty));
            else constants=OriginalMapFinalOutputBloomStarFlare.CreateOrdinary(Graph,index,width,height,retained,current,new(0,0,ReadOnlyMemory<byte>.Empty),OriginalMapFinalOutputShaft.CopyNativeQuad96()).Constants;
            CommitDraw(index,constants);
            if(index is 3 or 16)
                foreach(var slice in Graph.Passes[index].ConstantBuffers.Where(b=>b.StageCode==0).SelectMany(b=>b.SourceParameterSlices)
                    .Where(s=>s.ParameterIndex is 9 or 10 or 24))
                    current[slice.ParameterIndex]=constants[(0,0)].Slice(slice.Offset,slice.ByteSize).ToArray();
            if(index==17)
            {
                retainedStock=Store((0,0),16).AsSpan(0,16).ToArray();
                var flare=OriginalMapFinalOutputBloomStarFlare.CreateFlare(Graph,width,height,Store((0,0),16).AsMemory(0,16),Vector2.One,Vector4.One);
                foreach(var cb in flare.Draws.Last().Constants){cb.Value.Span.CopyTo(Store((cb.Key.Stage,cb.Key.Register),cb.Value.Length));
                    Array.Fill(nextKnown[(cb.Key.Stage,cb.Key.Register)],true,0,cb.Key.Stage==1?32:4);}
                // The active dedicated LightScene follows stock Flare in this
                // dependency batch. Its VS writes the full source projection
                // and color vectors; PS keeps the disabled alpha threshold.
                Floats(2f/width,-2f/height,-1,1,1,1,1,1).CopyTo(Store((1,0),32),0);
                Array.Fill(nextKnown[(1,0)],true,0,32);
                writes.Add("stock-Flare18+LightScene23:source constructor scale/color and actual rectangle/alpha writers");
            }
        }
        var finalRetained=Retained(28);var finalCurrent=Graph.BuildConstants(28,finalRetained,current);
        byte[] vertex=finalRetained[(1,0)].ToArray(),pixel=finalCurrent[(0,0)].ToArray();
        vertex.AsSpan(0,16).Clear();Floats(1,1,0,0).CopyTo(vertex,272);
        var offset=current[79];byte[] nextUv=new byte[48];
        for(int i=0;i<12;i++)BinaryPrimitives.WriteSingleLittleEndian(nextUv.AsSpan(i*4),BinaryPrimitives.ReadSingleLittleEndian(offset.Span[(i*4)..])/(i%2==0?width:height));
        nextUv.CopyTo(vertex,16);
        CommitDraw(28,new Dictionary<(uint,uint),ReadOnlyMemory<byte>>{{(1,0),vertex},{(0,0),pixel}});
        byte[] stock=retainedStock??throw new InvalidDataException("Source stock Flare retained state was not prepared.");
        var result=new OriginalMapFinalOutputProductionPrepared(camera.FrameNumber,camera,exposure,weight,current,templates,
            vertex,pixel,radialUv.ToArray(),stock,applyLuminance,writes.AsReadOnly(),unread.AsReadOnly(),actualFrameOwner+";"+currentSeason.SourceProvenance+
            ";source-current80;source-Camera/functor-CB-writes;unread-shadow-storage-not-observed-native-bytes"+
            (selectedExposure is null?"":";"+selectedExposure.SourceProvenance)+
            (selectedHdr is null?"":";"+selectedHdr.SourceEvidence+";explicit-owned-Bloom-caller-value")+
            (sourceBloom is null?"":";"+sourceBloom.SourceEvidence));
        pending=result;pendingShadow=nextShadow;pendingKnown=nextKnown;pendingRadial=nextUv;return result;
    }
    public void Complete(OriginalMapFinalOutputProductionPrepared prepared,bool succeeded)
    {
        if(!ReferenceEquals(pending,prepared))throw new InvalidDataException("Final state completion must acknowledge its exact pending source frame.");
        if(succeeded){parameters=prepared.CurrentParameters.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
            shadow.Clear();known.Clear();foreach(var p in pendingShadow!)shadow.Add(p.Key,p.Value);foreach(var p in pendingKnown!)known.Add(p.Key,p.Value);
            radialUv=pendingRadial!;SourceApplyLuminance=prepared.ApplyLuminance;CommittedFrameNumber=prepared.FrameNumber;}
        pending=null;pendingShadow=null;pendingKnown=null;pendingRadial=null;
    }
    private static int VertexUvCount(int index)=>index switch{0 or 6=>0,1=>2,2=>8,3=>4,4=>7,5=>4,7=>3,_=>throw new InvalidDataException("Unproved source VS wrapper read range.")};
    private static byte[] Floats(params float[] values){byte[] b=new byte[values.Length*4];for(int i=0;i<values.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(i*4),values[i]);return b;}
}
