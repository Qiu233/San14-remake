using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed record D3D11CloudAnimationAxis(int SourcePhase,int PixelOffset,
    float Wind,float Period,float Position,float Scale);

/// <summary>Maps named noise layers to the existing Native animation contract.
/// Camera offsets and shader reflection stay at this encoding boundary.</summary>
public static class SkyCloudAnimationEncoding
{
    public static IReadOnlyList<D3D11CloudAnimationAxis> CreateAxes(SkyCloudMaterial material,
        DxbcResourceDefinitions definitions,bool includeReservedChannels=false)
    {
        var rows=new List<D3D11CloudAnimationAxis>();
        var buffer=definitions.Buffers.Single();
        Vector3 Expand(Vector2 value)=>new(value,0);
        foreach(var layer in new[]{
            (Name:"Base",Phase:0xE0,Period:material.Base.Period,Position:material.Base.Position,Velocity:material.Base.Velocity,Lanes:3),
            (Name:"Detail",Phase:0x120,Period:material.Detail.Period,Position:material.Detail.Position,Velocity:material.Detail.Velocity,Lanes:3),
            (Name:"Distort",Phase:0x160,Period:Expand(material.Distort.Period),Position:Expand(material.Distort.Position),Velocity:Expand(material.Distort.Velocity),Lanes:2),
            (Name:"Cirrus",Phase:0x190,Period:Expand(material.Cirrus.Period),Position:Expand(material.Cirrus.Position),Velocity:Expand(material.Cirrus.Velocity),Lanes:2),
            (Name:"CirrusDistort",Phase:0x1B0,Period:Expand(material.CirrusDistort.Period),Position:Expand(material.CirrusDistort.Position),Velocity:Expand(material.CirrusDistort.Velocity),Lanes:2)})
        {
            int offset=buffer.Variables.Single(value=>value.Name.Text=="g_v"+layer.Name+"TexOffset").StartOffset;
            for(int lane=0;lane<layer.Lanes;lane++)
                rows.Add(new(layer.Phase+lane*4,offset+lane*4,layer.Velocity[lane],layer.Period[lane],
                    layer.Position[lane],Reciprocal(layer.Period[lane])));
        }
        if(includeReservedChannels)
            foreach(int phase in new[]{0x1D8,0x1DC,0x1F8,0x1FC})
                rows.Add(new(phase,-1,0,SkyCloudMaterial.ReservedPeriod,0,
                    Reciprocal(SkyCloudMaterial.ReservedPeriod)));
        return rows.AsReadOnly();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Reciprocal(float period)=>1/period;
}
