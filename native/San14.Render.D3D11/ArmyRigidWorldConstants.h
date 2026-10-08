#pragma once
#include "Pipeline/city-accessory-pose-native.h"
#include <span>

// TC97C920 -> 97B490: transpose mL2W's upper3x3, then the source scalar
// inverse. Each operation keeps its float32 order; determinant +/-0 performs
// no destination stores. Translation and quaternion normalization are absent.
inline bool tryArmyRigidNormalRows(std::span<const float,12> positionRows,
        std::span<float,12> normalRows) {
    using namespace native_accessory_pose_detail;
    const float a=positionRows[0],b=positionRows[4],c=positionRows[8];
    const float d=positionRows[1],e=positionRows[5],f=positionRows[9];
    const float g=positionRows[2],h=positionRows[6],i=positionRows[10];
    const float ei=mul(e,i),fh=mul(f,h),c0=sub(ei,fh);
    const float ch=mul(c,h),bi=mul(b,i),c1=sub(ch,bi);
    const float bf=mul(b,f),ce=mul(c,e),c2=sub(bf,ce);
    float determinant=mul(a,c0);
    const float second=mul(d,c1);determinant=add(determinant,second);
    const float third=mul(g,c2);determinant=add(determinant,third);
    if(determinant==0.f)return false;

    const float reciprocal=divide(1.f,determinant);
    const float gf=mul(g,f),di=mul(d,i),c3=mul(sub(gf,di),reciprocal);
    const float ai=mul(a,i),gc=mul(g,c),c4=mul(sub(ai,gc),reciprocal);
    const float dc=mul(d,c),af=mul(a,f),c5=mul(sub(dc,af),reciprocal);
    const float dh=mul(d,h),ge=mul(g,e),c6=mul(sub(dh,ge),reciprocal);
    const float gb=mul(g,b),ah=mul(a,h),c7=mul(sub(gb,ah),reciprocal);
    const float ae=mul(a,e),db=mul(d,b),c8=mul(sub(ae,db),reciprocal);
    const std::array<float,12> result{mul(reciprocal,c0),mul(reciprocal,c1),mul(reciprocal,c2),0.f,
        c3,c4,c5,0.f,c6,c7,c8,0.f};
    std::copy(result.begin(),result.end(),normalRows.begin());
    return true;
}

// Rigid160's palette rows are the completed reference*joint rows from the
// current pose frame. They already contain World; do not apply root again.
// vertexTemplate160 is the retained CB value, including its previous normal
// rows, so the source determinant-zero branch preserves that exact state.
inline std::array<char,160> packArmyRigid160(std::span<const char,160> vertexTemplate160,
        std::span<const char,64> camera64,const NativeAccessoryPosePalette& worldIncludedPalette) {
    std::array<char,160> result;
    std::memcpy(result.data(),vertexTemplate160.data(),result.size());
    std::memcpy(result.data(),camera64.data(),64);
    std::array<float,12> position,normal;
    std::memcpy(position.data(),worldIncludedPalette.rows.data(),48);
    std::memcpy(result.data()+112,position.data(),48);
    if(tryArmyRigidNormalRows(position,normal))std::memcpy(result.data()+64,normal.data(),48);
    return result;
}
