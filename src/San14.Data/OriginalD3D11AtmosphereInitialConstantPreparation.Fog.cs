using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public static partial class OriginalD3D11AtmosphereInitialConstantPreparation
{
    private static IReadOnlyList<OriginalD3D11AtmosphereInitialFogConstants> PrepareFog(
        OriginalGameRepository repository,OriginalD3D11AtmosphereInitialInputs current,byte[] main,OriginalCameraLens lens)
    {
        var package=current.Present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage;
        string locale=current.Present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale;
        var selected=OriginalMapSsFogCurrentFields.ReadSelectedWordWindow(package,
            OriginalMainMapRawCameraNumericConstructionSource.Load(repository,locale),
            OriginalMainMapRawCameraPreparationSource.Load(repository,locale));
        uint U(int offset)=>current.CurrentMainWords.TryGetValue(offset,out uint value)?value:
            BinaryPrimitives.ReadUInt32LittleEndian(selected.AsSpan(offset-OriginalMapSsFogCurrentFields.PreparedWindowBegin));
        float R(int offset)=>BitConverter.UInt32BitsToSingle(U(offset));
        var height=OriginalDeferredHeightFogInputs.SuccessfulThreeLayerDefaults();
        float[] fluctuations=height.FluctuationParameters.ToArray(),velocities=new float[60];
        var layers=new[]{(Mode:0x84C,Fade:0x840,Distance:0x844,Height:0x850,Velocity:0x88C,Waves:0x890),
            (Mode:0x868,Fade:0x8E0,Distance:0x8E4,Height:0x86C,Velocity:0x8EC,Waves:0x8F0),
            (Mode:0x94C,Fade:0x940,Distance:0x944,Height:0x950,Velocity:0x968,Waves:0x96C)};
        for(int layer=0;layer<3;layer++)
        {
            var source=layers[layer];uint mode=U(source.Mode);
            int type=OriginalMapFreshCommonResources.ClassifyE41710(mode);
            height=OriginalDeferredHeightFogMath.SetLayerType(height,(uint)layer,type);
            if(type<0)continue;
            float start=R(source.Height),end=R(source.Height+4);
            if(unchecked((int)mode)<0){start=A(start,F(main,0x18));end=A(end,F(main,0x18));}
            Vector4 color=new(R(source.Height+8),R(source.Height+12),R(source.Height+16),R(source.Height+20));
            bool distance=U(source.Fade)!=0&&R(source.Distance+4)>=R(source.Distance);
            height=OriginalDeferredHeightFogMath.ApplyLayer(height,(uint)layer,
                OriginalDeferredHeightFogMath.PrepareLayer(color,start,end,distance,R(source.Distance),R(source.Distance+4)));
            for(int wave=0;wave<4;wave++)
            {
                int at=source.Waves+wave*20,index=layer*20+wave*5;
                fluctuations[index]=R(at);fluctuations[index+1]=R(at+4);fluctuations[index+3]=R(at+12);
                velocities[index+2]=M(R(source.Velocity),R(at+8));velocities[index+4]=M(R(source.Velocity),R(at+16));
                fluctuations[index+2]=FogPhase(velocities[index+2],current.Options.CommonClock);
                fluctuations[index+4]=FogPhase(velocities[index+4],current.Options.CommonClock);
            }
        }
        float near=lens.ReverseDepth?lens.Far:lens.Near,far=lens.ReverseDepth?lens.Near:lens.Far;
        float tangent=OriginalNativeCameraTangent.Tangent1020A90(M(lens.VerticalFovRadians,.5f),OriginalNativeCameraTangentDispatch.Sse);
        Vector4 projection=new(D(far,S(near,far)),D(M(far,near),S(far,near)),M(tangent,lens.Aspect),tangent);
        Matrix4x4 ordinaryView=new(F(main,0x80),F(main,0x84),F(main,0x88),F(main,0x8C),
            F(main,0x90),F(main,0x94),F(main,0x98),F(main,0x9C),F(main,0xA0),F(main,0xA4),F(main,0xA8),F(main,0xAC),
            F(main,0xB0),F(main,0xB4),F(main,0xB8),F(main,0xBC));
        Matrix4x4 inverseView=OriginalNativeMatrixMath.Invert(ordinaryView,out _);
        float maskX=D(1,R(0x9BC)),maskY=D(1,R(0x9C0));
        Vector4 mask=new(maskX,maskY,Flip(M(R(0x9C4),maskX)),Flip(M(R(0x9C8),maskY)));
        var objects=repository.LoadObjects("system",0x718CCB8B);
        var module=objects.Document.Objects.Single(value=>value.ObjectId==0x74B12946);
        uint file=BinaryPrimitives.ReadUInt32LittleEndian(module.Properties.Single(value=>value.NameHash==0x193025DF).RawValue);
        var shader=repository.LoadShaderContainer("system",file);var tables=shader.ReadG1Bindings();
        var result=new List<OriginalD3D11AtmosphereInitialFogConstants>();
        foreach(var pass in new[]{(Name:"fog-mask02",Mask:2u,Background:false),(Name:"fog-mask08",Mask:8u,Background:false),(Name:"fog-background",Mask:0u,Background:true)})
        {
            uint feature=0;
            if(!pass.Background)
            {
                var light=current.Lights.First(value=>value.GroupMask==pass.Mask).CompleteData;
                uint mode=BinaryPrimitives.ReadUInt32LittleEndian(light.Span[0x160..]);
                if(mode!=0)throw new NotSupportedException("Initial full SSFog requires the selected nonzero raw-light named fields and program layout.");
            }
            int[] variantWords=[1,1,0,pass.Background?1:0,3,height.Types[0],height.Types[1],height.Types[2],3,1];
            byte[] variantBytes=new byte[40];for(int i=0;i<variantWords.Length;i++)BinaryPrimitives.WriteInt32LittleEndian(variantBytes.AsSpan(i*4),variantWords[i]);
            var variant=tables.Variants.Match(variantBytes)??throw new NotSupportedException("Current SSFog layer variant is absent.");
            var combination=tables.ResolveCombination([feature,0,0,0,0,0,(uint)variant.Index],0);
            var programs=Programs(combination);var definitions=shader.ReadResourceDefinitions(programs.PixelProgram);
            var fields=new Fields();fields.Set("cHFog",height.Colors.SelectMany(Words).ToArray());
            fields.Set("hFogParam",height.Parameters.SelectMany(Words).ToArray());fields.Set("hFogFlucPrms",fluctuations);
            fields.Set("hFogMaskPrm",Words(mask));fields.Set("vP2V",Words(projection));fields.Set("mV2W",Words(inverseView));
            var fluctuation=definitions.Buffers.Single().Variables.Single(value=>value.Name.Text=="hFogFlucPrms");
            var phases=new List<(int Offset,float Velocity)>();
            for(int layer=0;layer<3;layer++)for(int wave=0;wave<4;wave++)foreach(int component in new[]{2,4})
            {int scalar=layer*20+wave*5+component;phases.Add((fluctuation.StartOffset+scalar*16,velocities[scalar]));}
            byte[] vertex=new byte[16];float[] vector=Words(projection);
            for(int i=0;i<4;i++)BinaryPrimitives.WriteSingleLittleEndian(vertex.AsSpan(i*4),vector[i]);
            result.Add(new(pass.Name,programs,vertex,Pack(definitions,1152,fields),phases));
        }
        return result.AsReadOnly();
    }
    private static float Flip(float value)=>BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(value)^0x80000000);
    private static float FogPhase(float velocity,float commonClock)
    {
        float cycles=MathF.Floor(D(commonClock,3600)),local=S(commonClock,M(cycles,3600));
        float Fraction(float value)=>(float)((double)value-Math.Truncate(value));
        return Fraction(A(M(velocity,local),M(Fraction(M(3600,velocity)),cycles)));
    }
}
