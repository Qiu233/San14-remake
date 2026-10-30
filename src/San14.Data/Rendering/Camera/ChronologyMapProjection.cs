using System.Numerics;
using F=San14.Data.UnitWorldGroundHeightFloat;

namespace San14.Data;

/// <summary>The chronology flare's original normalized-first float projection.
/// Distinct from ordinary map controls' coefficient-before-reciprocal primitive.
/// A clipped result has no written source output; the caller retains that frontier.</summary>
public static class ChronologyMapProjection
{
    public static MapControlScreenPoint? Project(Vector4 position,Matrix4x4? currentCamera,
        Matrix4x4 viewport,bool clipBypass,MapControlSurfaceExtent? applicationSurface)
    {
        if(currentCamera is not {} camera)return null;
        //00F430 uses Y-first clip sums in every column, for both passes.
        float w=Column(position,camera.M14,camera.M24,camera.M34,camera.M44);
        float inverse=F.Divide(1,w);
        var normalized=new Vector4(
            F.Multiply(Column(position,camera.M11,camera.M21,camera.M31,camera.M41),inverse),
            F.Multiply(Column(position,camera.M12,camera.M22,camera.M32,camera.M42),inverse),
            F.Multiply(Column(position,camera.M13,camera.M23,camera.M33,camera.M43),inverse),
            F.Multiply(inverse,w));
        if(!clipBypass&&!(normalized.X>-1&&normalized.X<1&&normalized.Y>-1&&normalized.Y<1&&normalized.Z>0&&normalized.Z<1))return null;
        int x=BattleUnitSourceMath.TruncateSingleToInt32(Viewport(normalized,viewport.M11,viewport.M21,viewport.M31,viewport.M41,true));
        int y=BattleUnitSourceMath.TruncateSingleToInt32(Viewport(normalized,viewport.M12,viewport.M22,viewport.M32,viewport.M42,false));
        if(applicationSurface is {} surface)
        {
            if(surface.Width==0||surface.Height==0)throw new InvalidDataException("Chronology projection has no positive actual surface.");
            x=unchecked((int)(unchecked((uint)(x*1920))/surface.Width));
            y=unchecked((int)(unchecked((uint)(y*1080))/surface.Height));
        }
        return new(x,y);
    }
    private static float Column(Vector4 p,float x,float y,float z,float w)=>
        F.Add(F.Add(F.Add(F.Multiply(y,p.Y),F.Multiply(x,p.X)),F.Multiply(z,p.Z)),F.Multiply(w,p.W));
    private static float Viewport(Vector4 p,float x,float y,float z,float w,bool yFirst)
    {
        float a=F.Multiply(p.X,x),b=F.Multiply(p.Y,y);
        return F.Add(F.Add(yFirst?F.Add(b,a):F.Add(a,b),F.Multiply(p.Z,z)),F.Multiply(p.W,w));
    }
}
