using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public static partial class D3D11AtmosphereInitialConstantPreparation
{
    private static HeightFogInitialFrame PrepareFog(
        GameResourceRepository repository,D3D11AtmosphereInitialInputs current,byte[] main,CameraLens lens,
        HeightFogProgramResources resources)
    {
        var package=current.Present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage;
        resources.RequireCurrentMask(current.Present);
        var settings=current.HeightFog ?? HeightFogSettingsEncoding.ApplyPreparedOverrides(
            HeightFogResourceImport.Load(package),current.CurrentMainWords);
        int[] types=new int[3];
        Vector4[] colors=new Vector4[3],parameters=new Vector4[3];
        float[] fluctuations=new float[60],velocities=new float[60];
        for(int layer=0;layer<3;layer++)
        {
            var source=settings.Layer(layer);uint mode=source.Mode;
            int type=(int)CommonRenderEncoding.ClassifyAmbient(mode);
            types[layer]=type;
            if(type<0)continue;
            float start=source.HeightStart,end=source.HeightEnd;
            if(unchecked((int)mode)<0){start=A(start,F(main,0x18));end=A(end,F(main,0x18));}
            Vector4 color=source.Color;
            bool distance=source.DistanceFadeEnabled!=0&&source.DistanceEnd>=source.DistanceStart;
            var prepared=DeferredHeightFogMath.PrepareLayer(color,start,end,distance,source.DistanceStart,source.DistanceEnd);
            colors[layer]=prepared.Color;parameters[layer]=prepared.Parameters;
            for(int wave=0;wave<4;wave++)
            {
                var value=source.Wave(wave);int index=layer*20+wave*5;
                fluctuations[index]=value.Amplitude;fluctuations[index+1]=value.FrequencyX;fluctuations[index+3]=value.FrequencyY;
                velocities[index+2]=M(source.Velocity,value.VelocityX);velocities[index+4]=M(source.Velocity,value.VelocityY);
                fluctuations[index+2]=FogPhase(velocities[index+2],current.Options.CommonClock);
                fluctuations[index+4]=FogPhase(velocities[index+4],current.Options.CommonClock);
            }
        }
        float near=lens.ReverseDepth?lens.Far:lens.Near,far=lens.ReverseDepth?lens.Near:lens.Far;
        float tangent=NativeCameraTangent.Tangent1020A90(M(lens.VerticalFovRadians,.5f),NativeCameraTangentDispatch.Sse);
        Vector4 projection=new(D(far,S(near,far)),D(M(far,near),S(far,near)),M(tangent,lens.Aspect),tangent);
        Matrix4x4 ordinaryView=new(F(main,0x80),F(main,0x84),F(main,0x88),F(main,0x8C),
            F(main,0x90),F(main,0x94),F(main,0x98),F(main,0x9C),F(main,0xA0),F(main,0xA4),F(main,0xA8),F(main,0xAC),
            F(main,0xB0),F(main,0xB4),F(main,0xB8),F(main,0xBC));
        Matrix4x4 inverseView=NativeMatrixMath.Invert(ordinaryView,out _);
        float maskX=D(1,settings.Mask.X),maskY=D(1,settings.Mask.Y);
        Vector4 mask=new(maskX,maskY,Flip(M(settings.Mask.Z,maskX)),Flip(M(settings.Mask.W,maskY)));
        var result=new List<D3D11AtmosphereInitialFogConstants>();
        foreach(uint group in new uint[]{1,2,4,8,0})
        {
            bool background=group==0;
            string name=background?"fog-background":"fog-mask"+group.ToString("D2");
            // Actual population belongs to the prepared native View. Without
            // that publication a local pass has declarations, not a guessed
            // initial GPU choice. First native admission supplies its light.
            if(!background && (current.ActiveFogGroups is not uint active || (active&group)==0))
            {
                result.Add(new(name,null,[],[],Array.Empty<(int,float)>(),group));
                continue;
            }
            var fields=new Dictionary<HeightFogFieldSemantic,float[]>
            {
                [HeightFogFieldSemantic.HeightColors]=colors.SelectMany(Words).ToArray(),
                [HeightFogFieldSemantic.HeightParameters]=parameters.SelectMany(Words).ToArray(),
                [HeightFogFieldSemantic.Fluctuations]=fluctuations,
                [HeightFogFieldSemantic.Mask]=Words(mask),
                [HeightFogFieldSemantic.Projection]=Words(projection),
                [HeightFogFieldSemantic.InverseView]=Words(inverseView)
            };
            int feature=0;
            if(!background)
            {
                var light=current.Lights.First(value=>value.GroupMask==group).CompleteData;
                uint mode=BinaryPrimitives.ReadUInt32LittleEndian(light.Span[0x160..]);
                feature=mode switch {0=>0,7=>1,_=>throw new NotSupportedException("Height fog needs its actual raw-light mode producer.")};
                if(feature==1)
                {
                    var shared=DeferredSceneConstants.ReadSharedFog(light.Span[..0x1B0],true);
                    fields.Add(HeightFogFieldSemantic.LightDirection,Words(V(light.Span,0)));
                    fields.Add(HeightFogFieldSemantic.LightColor,Words(V(light.Span,0x10)));
                    fields.Add(HeightFogFieldSemantic.Eye,Words(V(main,0x10)));
                    fields.Add(HeightFogFieldSemantic.Fog,Words(shared.Coefficients));
                    fields.Add(HeightFogFieldSemantic.FogColor,Words(shared.Color));
                    fields.Add(HeightFogFieldSemantic.Scattering,shared.Scattering.SelectMany(Words).ToArray());
                }
            }
            var program=resources.Select(new(feature,background,types[0],types[1],types[2]));
            var fluctuation=program.PixelLayout.Fields.SingleOrDefault(field=>field.Semantic==HeightFogFieldSemantic.Fluctuations);
            var phases=new List<(int Offset,float Velocity)>();
            if(fluctuation is not null)
            for(int layer=0;layer<3;layer++)for(int wave=0;wave<4;wave++)foreach(int component in new[]{2,4})
            {int scalar=layer*20+wave*5+component;phases.Add((fluctuation.Offset+scalar*fluctuation.Stride,velocities[scalar]));}
            result.Add(new(name,program,program.VertexLayout.Pack(fields),program.PixelLayout.Pack(fields),phases.AsReadOnly(),group));
        }
        var samplerSource=RenderSamplerSourceAdapters.Load(repository.SourceDirectory,
            current.Present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale);
        var sampler=RenderSamplerSourceAdapters.DecodeKey(samplerSource,
            RenderSamplerSourceAdapters.RenderTextureKey(samplerSource,1)).EncodeD3D11Descriptor();
        return new(resources,settings,result.AsReadOnly(),current.Options.Width,current.Options.Height,
            RenderTextureFormatMath.Resolve(LoadFreshMainTarget(repository,current.Present)),sampler);
    }
    private static float Flip(float value)=>BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(value)^0x80000000);
    private static float FogPhase(float velocity,float commonClock)
    {
        float cycles=MathF.Floor(D(commonClock,3600)),local=S(commonClock,M(cycles,3600));
        float Fraction(float value)=>(float)((double)value-Math.Truncate(value));
        return Fraction(A(M(velocity,local),M(Fraction(M(3600,velocity)),cycles)));
    }
}
