using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record DeferredHeightFogLayer(Vector4 Color,Vector4 Parameters);

/// <summary>Original9B6D80/9B6E50 finite setters, separate from module discovery
/// and fluctuation generation. Prepared packet helpers retain every other layer
/// and the existing fluctuation cache; they do not reset disabled layers.</summary>
public static class DeferredHeightFogMath
{
    public static DeferredHeightFogLayer PrepareLayer(Vector4 color,float start,float end,
        bool distanceEnabled,float distanceStart,float distanceEnd)
    {
        if(!Finite(color)||!float.IsFinite(start)||!float.IsFinite(end))
            throw new InvalidDataException("Height fog requires explicit finite color and height interval.");
        float x=Divide(1,Subtract(end,start)),y=SignFlip(Multiply(x,start));
        float z=0,w=1;
        if(distanceEnabled)
        {
            if(!float.IsFinite(distanceStart)||!float.IsFinite(distanceEnd))
                throw new InvalidDataException("Enabled fog distance requires finite source values.");
            if(distanceStart!=distanceEnd)
            {z=Divide(1,Subtract(distanceEnd,distanceStart));w=SignFlip(Multiply(z,distanceStart));}
        }
        var parameters=new Vector4(x,y,z,w);
        if(!Finite(parameters))throw new InvalidDataException("Height fog setter produced a nonfinite result; no epsilon is substituted.");
        return new(color,parameters);
    }

    /// <summary>Equivalent prepared output after the source layer setter enables
    /// and updates this layer. uint indexes above2 follow the original no-op gate.</summary>
    public static DeferredHeightFogInputs ApplyLayer(DeferredHeightFogInputs previous,uint layerIndex,
        DeferredHeightFogLayer layer)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if(layerIndex>2)return previous;
        ArgumentNullException.ThrowIfNull(layer);ValidateShape(previous);
        if(!Finite(layer.Color)||!Finite(layer.Parameters))throw new InvalidDataException("Height fog layer is nonfinite.");
        var colors=previous.Colors.ToArray();var parameters=previous.Parameters.ToArray();
        colors[(int)layerIndex]=layer.Color;parameters[(int)layerIndex]=layer.Parameters;
        return new(Array.AsReadOnly(previous.Types.ToArray()),Array.AsReadOnly(colors),Array.AsReadOnly(parameters),
            Array.AsReadOnly(previous.FluctuationParameters.ToArray()));
    }

    /// <summary>Raw signed type setter. No semantic enum or enabled-layer state
    /// is inferred; source accepts every int32 type at an index0..2.</summary>
    public static DeferredHeightFogInputs SetLayerType(DeferredHeightFogInputs previous,uint layerIndex,int type)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if(layerIndex>2)return previous;
        ValidateShape(previous);var types=previous.Types.ToArray();types[(int)layerIndex]=type;
        return new(Array.AsReadOnly(types),Array.AsReadOnly(previous.Colors.ToArray()),Array.AsReadOnly(previous.Parameters.ToArray()),
            Array.AsReadOnly(previous.FluctuationParameters.ToArray()));
    }

    private static void ValidateShape(DeferredHeightFogInputs input)
    {
        if(input.Types is null||input.Types.Count!=3||input.Colors is null||input.Colors.Count!=3||
            input.Parameters is null||input.Parameters.Count!=3||input.FluctuationParameters is null||input.FluctuationParameters.Count!=60)
            throw new InvalidDataException("Height fog cache requires three layers and60 retained fluctuation scalars.");
    }
    private static bool Finite(Vector4 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z)&&float.IsFinite(v.W);
    private static float SignFlip(float value)=>BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(value)^0x80000000u);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Multiply(float left,float right)=>left*right;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float left,float right)=>left/right;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Subtract(float left,float right)=>left-right;
}
