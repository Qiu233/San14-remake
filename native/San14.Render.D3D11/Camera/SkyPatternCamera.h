#pragma once
#include "CameraState.h"
#include "CameraMatrix.h"

// Pattern generation uses the current map camera and selected View base light.
// The published world-to-pattern transform is independent of the temporary
// generation camera. These pure producers neither select resources nor draw.
namespace source_sky_pattern {
using source_camera::A;
using source_camera::S;
using source_camera::M;
using source_camera::D;
using source_camera::V3;
using source_camera::V4;
using source_camera::Matrix;
using source_camera::CameraTransform;

struct ProjectionInputs {
    float eyeX{},eyeZ{},farPlane{},verticalFov{},aspect{},height{};
    V3 ambientDirection{};
};
struct Projection {
    Matrix worldToPattern{};
    V4 eye{},target{},up{};
    float radius{};
    bool affineInverseWrote{};
};
struct Cameras {
    CameraTransform temporary{},directionOnly{};
    std::uint32_t clipProfile{};
};
struct VertexMaterial {
    V4 clipPlane{};
    Matrix projectionToWorld{};
    std::array<V4,4> directions{};
    std::array<float,2> projectionFarNear{};
};

inline float fromBits(std::uint32_t bits) {
    float value;
    std::memcpy(&value,&bits,sizeof value);
    return value;
}
inline float squareRoot(float value) {
    return _mm_cvtss_f32(_mm_sqrt_ss(_mm_set_ss(value)));
}
inline Matrix translate(Matrix value,V4 position) {
    for (unsigned row=0;row<4;++row) {
        const unsigned i=row*4;
        value[i]=A(M(value[i+3],position.X),value[i]);
        value[i+1]=A(M(value[i+3],position.Y),value[i+1]);
        value[i+2]=A(M(value[i+3],position.Z),value[i+2]);
    }
    return value;
}

// The affine inverse retains its input when the determinant is zero. This
// differs from the full camera inverse and matters for degenerate directions.
inline Matrix affineInverse(const Matrix& value,bool& wrote) {
    const float a=value[0],b=value[1],c=value[2],d=value[4],e=value[5],f=value[6],
        g=value[8],h=value[9],i=value[10];
    const float c00=S(M(e,i),M(f,h)),c01=S(M(c,h),M(b,i)),c02=S(M(b,f),M(c,e));
    const float determinant=A(A(M(a,c00),M(d,c01)),M(g,c02));
    wrote=determinant!=0;
    if (!wrote) return value;
    const float c10=S(M(g,f),M(d,i)),c11=S(M(a,i),M(g,c)),c12=S(M(d,c),M(a,f)),
        c20=S(M(d,h),M(g,e)),c21=S(M(g,b),M(a,h)),c22=S(M(a,e),M(d,b)),
        factor=D(1,determinant);
    const float tx=source_camera::flip(value[12]),ty=source_camera::flip(value[13]),
        tz=source_camera::flip(value[14]);
    return {M(factor,c00),M(factor,c01),M(factor,c02),0,
        M(c10,factor),M(c11,factor),M(c12,factor),0,
        M(c20,factor),M(c21,factor),M(c22,factor),0,
        M(A(A(M(ty,c10),M(tx,c00)),M(tz,c20)),factor),
        M(A(A(M(ty,c11),M(tx,c01)),M(tz,c21)),factor),
        M(A(A(M(ty,c12),M(tx,c02)),M(tz,c22)),factor),1};
}

inline Projection prepareProjection(const ProjectionInputs& current,float actualHalfFovTangent) {
    for (float value:{current.eyeX,current.eyeZ,current.farPlane,current.verticalFov,current.aspect,
        current.height,current.ambientDirection.X,current.ambientDirection.Y,
        current.ambientDirection.Z,actualHalfFovTangent})
        if (!std::isfinite(value)) throw std::runtime_error("Pattern projection requires finite current camera and light inputs");
    const float vertical=M(actualHalfFovTangent,current.farPlane),horizontal=M(vertical,current.aspect);
    const float radius=squareRoot(A(A(M(horizontal,horizontal),M(vertical,vertical)),M(current.farPlane,current.farPlane)));
    const float dx=source_camera::flip(current.ambientDirection.X),
        dy=source_camera::flip(current.ambientDirection.Y),dz=source_camera::flip(current.ambientDirection.Z);
    const float height=M(current.height,100000),threshold=fromBits(0x37480000);
    const V4 eye{current.eyeX,height,current.eyeZ,0};
    V4 up{};
    const float horizontalLength=squareRoot(A(A(M(dx,dx),0),M(dz,dz)));
    if (horizontalLength>threshold) {
        const float reciprocal=D(1,horizontalLength);
        const float hx=M(reciprocal,dx),hy=M(reciprocal,0),hz=M(reciprocal,dz);
        const float cx=S(M(hz,dy),M(hy,dz)),cy=S(M(hx,dz),M(hz,dx)),cz=S(M(hy,dx),M(hx,dy));
        const float crossLength=squareRoot(A(A(M(cy,cy),M(cx,cx)),M(cz,cz)));
        if (crossLength>threshold)
            up={S(M(cz,dy),M(cy,dz)),S(M(cx,dz),M(cz,dx)),S(M(cy,dx),M(cx,dy)),0};
        else up=dx>0||dz>0?V4{0,1,0,0}:V4{0,-1,0,0};
    } else up=dy>0?V4{1,0,0,0}:V4{0,0,-1,0};
    const V4 target{A(eye.X,M(dx,radius)),A(eye.Y,M(dy,radius)),A(eye.Z,M(dz,radius)),0};
    const float width=M(radius,2),flippedHeight=M(radius,-2);
    Matrix affine{
        M(S(M(up.Z,dy),M(up.Y,dz)),width),M(S(M(up.X,dz),M(up.Z,dx)),width),M(S(M(up.Y,dx),M(up.X,dy)),width),0,
        M(flippedHeight,up.X),M(flippedHeight,up.Y),M(flippedHeight,up.Z),0,
        M(dx,-1),M(dy,-1),M(dz,-1),0,0,0,0,1};
    affine=translate(affine,{eye.X,S(height,M(current.height,100000)),eye.Z,0});
    bool wrote{};
    const Matrix inverse=affineInverse(affine,wrote);
    const Matrix texture=translate({.5f,0,0,0,0,.5f,0,0,0,0,1,0,0,0,0,1},{.5f,.5f,0,0});
    return {source_camera::compose(inverse,texture),eye,target,up,radius,wrote};
}

// Generation overrides FOV to pi/2, aspect and scale to one, and sets the
// orthographic flag. Its pi/4 half-FOV tangent is exactly one in both Data paths.
inline CameraTransform completeGenerationCamera(CameraTransform camera,std::uint32_t clipProfile) {
    const auto& lens=camera.optics;
    if (!std::isfinite(lens.nearPlane)||!std::isfinite(lens.farPlane)||
        lens.nearPlane<=0||lens.farPlane<=0||lens.farPlane==lens.nearPlane)
        throw std::runtime_error("Pattern camera needs distinct positive near and far planes");
    for (const V4& value:{camera.eye,camera.target,camera.up})
        for (float lane:{value.X,value.Y,value.Z,value.W})
            if (!std::isfinite(lane)) throw std::runtime_error("Pattern camera pose is not finite");
    if (!std::isfinite(lens.roll)) throw std::runtime_error("Pattern camera view offset is not finite");
    camera.unrolledView=source_camera::lookTo(camera.eye,camera.target,camera.up);
    camera.view=camera.unrolledView;
    if (lens.roll!=0) {
        const auto trigonometry=source_camera::sinCos(source_camera::flip(lens.roll));
        for (unsigned row=0;row<4;++row) {
            const unsigned i=row*4;
            const float x=camera.unrolledView[i],y=camera.unrolledView[i+1];
            camera.view[i]=S(M(x,trigonometry[1]),M(y,trigonometry[0]));
            camera.view[i+1]=A(M(x,trigonometry[0]),M(y,trigonometry[1]));
        }
    }
    const float dx=S(camera.target.X,camera.eye.X),dy=S(camera.target.Y,camera.eye.Y),dz=S(camera.target.Z,camera.eye.Z);
    const float distance=squareRoot(A(A(M(dx,dx),M(dy,dy)),M(dz,dz)));
    const float product=M(distance,1),height=A(product,product),width=M(height,1);
    if (!std::isfinite(width)||width<=0||!std::isfinite(height)||height<=0)
        throw std::runtime_error("Pattern orthographic camera has no finite positive dimensions");
    const float nearArgument=lens.reversedDepth?lens.farPlane:lens.nearPlane,
        farArgument=lens.reversedDepth?lens.nearPlane:lens.farPlane,range=S(nearArgument,farArgument);
    float depthScale{},depthOffset{};
    if ((clipProfile&2)!=0) {
        depthScale=D(1,range);
        depthOffset=M(depthScale,nearArgument);
    } else {
        const float inverse=D(1,M(range,farArgument));
        const float twiceNear=A(nearArgument,nearArgument),sum=A(nearArgument,farArgument);
        depthScale=M(sum,inverse);
        depthOffset=M(M(twiceNear,farArgument),inverse);
    }
    camera.projection={D(2,width),0,0,0,0,D(2,height),0,0,0,0,depthScale,0,0,0,depthOffset,1};
    camera.worldToClip=source_camera::compose(camera.view,camera.projection);
    return camera;
}

inline Cameras prepareCameras(const CameraTransform& ordinary,const Projection& projection,
    std::uint32_t clipProfile) {
    CameraTransform temporary=ordinary;
    temporary.eye=projection.eye;temporary.target=projection.target;temporary.up=projection.up;
    temporary.optics.viewSpan=fromBits(0x3fc90fdb);
    temporary.optics.aspectRatio=1;
    temporary.optics.projectionKind=source_camera::ProjectionKind::Orthographic;
    temporary=completeGenerationCamera(temporary,clipProfile);
    CameraTransform direction=temporary;
    direction.target=source_camera::normalizeDirection({S(temporary.target.X,temporary.eye.X),
        S(temporary.target.Y,temporary.eye.Y),S(temporary.target.Z,temporary.eye.Z),temporary.target.W});
    direction.eye={0,0,0,1};direction.optics.nearPlane=1;direction.optics.farPlane=100;
    direction=completeGenerationCamera(direction,clipProfile);
    return {temporary,direction,clipProfile};
}

inline V4 homogeneousPoint(float x,float y,float z,const Matrix& matrix) {
    const auto lane=[&](unsigned column) {
        return A(A(A(M(matrix[column],x),M(matrix[4+column],y)),M(matrix[8+column],z)),matrix[12+column]);
    };
    const float w=lane(3),inverse=D(1,w);
    return {M(lane(0),inverse),M(inverse,lane(1)),M(inverse,lane(2)),M(inverse,w)};
}

inline VertexMaterial prepareVertex(const Cameras& cameras,V4 actualClipPlane) {
    float farZ{},nearZ{};
    switch (cameras.clipProfile) {
        case 0:farZ=1;nearZ=-1;break;
        case 2:farZ=1;nearZ=0;break;
        case 4:farZ=0;nearZ=-1;break;
        default:throw std::runtime_error("Pattern camera clip profile has no supported endpoint branch");
    }
    if (cameras.temporary.optics.reversedDepth) std::swap(farZ,nearZ);
    const Matrix inverse=source_matrix::inverse(cameras.directionOnly.worldToClip);
    VertexMaterial result{actualClipPlane,source_matrix::inverse(cameras.temporary.worldToClip),{}, {farZ,nearZ}};
    for (unsigned corner=0;corner<4;++corner) {
        const float x=static_cast<float>(corner>>1)*2-1,y=1-static_cast<float>(corner&1)*2;
        const V4 a=homogeneousPoint(x,y,farZ,inverse),b=homogeneousPoint(x,y,nearZ,inverse);
        result.directions[corner]={S(b.X,a.X),S(b.Y,a.Y),S(b.Z,a.Z),S(b.W,a.W)};
    }
    return result;
}
} // namespace source_sky_pattern
