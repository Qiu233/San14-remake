#pragma once
// Six-face ambient sky, cloud and star constant publication.
// No cube allocation, phase advance, budget reset or history publication.
#include "AmbientCamera.h"
#include "AmbientCelestialMath.h"

namespace source_current_ambient {
using namespace source_camera;
using source_atmosphere_camera::F;
struct Profile {
    uint32_t width = 128;
    float cloudSunDirectionW{};
    std::array<float, 4> starClockScales{};
    std::array<std::array<uint8_t, 144>, 6> skyVs{};
    std::array<std::array<uint8_t, 336>, 6> skyPs{};
    std::array<std::array<uint8_t, 160>, 6> cloudVs{};
    std::array<std::array<uint8_t, 512>, 6> cloudPs{};
    std::array<std::array<uint8_t, 352>, 6> starVs{};
    std::array<std::array<uint8_t, 16>, 6> starGs{}, starPs{};
};
inline Profile readProfile(const std::filesystem::path &source) {
    Profile p;
    const auto initial = source / "spring";
    for (unsigned f = 0; f < 6; ++f) {
        std::string n = std::to_string(f);
        p.skyVs[f] = source_atmosphere_camera::readBytes<144>(initial / ("sky-face" + n + "-vs.bin"));
        p.skyPs[f] = source_atmosphere_camera::readBytes<336>(initial / ("sky-face" + n + "-ps.bin"));
        p.cloudVs[f] = source_atmosphere_camera::readBytes<160>(initial / ("cloud-face" + n + "-vs.bin"));
        p.cloudPs[f] = source_atmosphere_camera::readBytes<512>(initial / ("cloud-face" + n + "-ps.bin"));
        p.starVs[f] = source_atmosphere_camera::readBytes<352>(initial / ("star-face" + n + "-vs.bin"));
        p.starGs[f] = source_atmosphere_camera::readBytes<16>(initial / ("star-face" + n + "-gs.bin"));
        p.starPs[f] = source_atmosphere_camera::readBytes<16>(initial / ("star-face" + n + "-ps.bin"));
    }
    p.cloudSunDirectionW = get<float>(p.cloudPs[0].data() + 412);
    std::ifstream s(source / "star-clock-scales.bin", std::ios::binary);
    if (read<uint32_t>(s) != 4)
        throw std::runtime_error("Current WORK Star source seasons");
    for (int i = 0; i < 4; ++i) {
        uint32_t season = read<uint32_t>(s);
        if (season >= 4)
            throw std::runtime_error("Current WORK Star season range");
        p.starClockScales[season] = read<float>(s);
    }
    return p;
}
struct Face {
    CameraTransform camera{};
    std::array<uint8_t, 144> skyVs{};
    std::array<uint8_t, 336> skyPs{};
    std::array<uint8_t, 160> cloudVs{};
    std::array<uint8_t, 512> cloudPs{};
    std::array<uint8_t, 352> starVs{};
    std::array<uint8_t, 16> starGs{}, starPs{};
};
struct Frame {
    std::array<Face, 6> faces;
    Matrix controllerInverse{};
    float starClockScale{};
    bool skyEnabled{};
};
inline Frame prepareCurrent(const Profile &profile, const Environment &environment,
                            const source_current_season_atmosphere::CloudLightInput &cloudLight) {
    Frame out;
    out.controllerInverse = celestialToWorld(environment.clock, environment.sky.celestial.skyTransform);
    out.starClockScale = profile.starClockScales.at(environment.season);
    out.skyEnabled = environment.skyEnabled;
    const auto &celestial = environment.sky.celestial;
    const auto &sky = celestial.skyTransform;
    const auto &localToGlobal = celestial.localToGlobal;
    const auto projectedSun = celestial.sunProjection, moon = celestial.moonPosition,
               altitude = celestial.altitude;
    const float offset = environment.sky.skyHeightOffset, sunSize = celestial.sunSize,
                moonSize = celestial.moonSize;
    const float sz = celestial.sunIntensity, mw = celestial.moonIntensity;
    const auto faceGeometry = prepareFaceGeometry(environment.camera, environment.sky.direct.altitude);
    const auto eye = faceGeometry.eye;
    auto shared = source_current_season_atmosphere::sharedMode7Profile2(environment.selectedFog,
                                                                        environment.selectedRadiance);
    auto moonMatrix = moonFace(environment.sky.moonFaceDirection);
    source_atmosphere_camera::Packet lightPacket;
    lightPacket.profile.cloudSunDirectionW = profile.cloudSunDirectionW;
    source_current_season_atmosphere::publishCloudLights(lightPacket, environment.sky, cloudLight);
    for (unsigned face = 0; face < 6; ++face) {
        auto &f = out.faces[face];
        f.camera = prepareFaceCamera(environment.camera, faceGeometry, face);
        const auto &faceView = f.camera.view;
        const auto &faceProjection = f.camera.projection;
        const auto &faceWorldToClip = f.camera.worldToClip;
        f.skyVs = profile.skyVs[face];
        f.skyPs = profile.skyPs[face];
        f.cloudVs = profile.cloudVs[face];
        f.cloudPs = profile.cloudPs[face];
        f.starVs = profile.starVs[face];
        f.starGs = profile.starGs[face];
        f.starPs = profile.starPs[face];
        // Rigid inverse and translation cancellation use this cube face, not ordinary-camera rays.
        const auto &v = faceView;
        Matrix rigid{v[0],
                     v[4],
                     v[8],
                     0,
                     v[1],
                     v[5],
                     v[9],
                     0,
                     v[2],
                     v[6],
                     v[10],
                     0,
                     flip(A(A(M(v[13], v[1]), M(v[12], v[0])), M(v[14], v[2]))),
                     flip(A(A(M(v[13], v[5]), M(v[12], v[4])), M(v[14], v[6]))),
                     flip(A(A(M(v[13], v[9]), M(v[12], v[8])), M(v[14], v[10]))),
                     1};
        for (int row = 0; row < 4; ++row) {
            int at = row * 4;
            float w = rigid[at + 3];
            rigid[at] = A(M(w, M(eye.X, -1)), rigid[at]);
            rigid[at + 1] = A(M(w, M(eye.Y, -1)), rigid[at + 1]);
            rigid[at + 2] = A(M(w, M(eye.Z, -1)), rigid[at + 2]);
        }
        put(f.skyVs.data(), compose(compose(source_matrix::inverse(faceProjection), rigid), sky));
        put(f.skyVs.data() + 64, 0.f);
        put(f.skyVs.data() + 80, localToGlobal);
        float x = M(eye.X, .01f), y = A(A(M(eye.Y, .01f), altitude.Z), offset), e = M(eye.Z, .01f), w = eye.W;
        V4 pos{A(A(A(M(y, sky[4]), M(x, sky[0])), M(e, sky[8])), M(w, sky[12])),
               A(A(A(M(x, sky[1]), M(y, sky[5])), M(e, sky[9])), M(w, sky[13])),
               A(A(A(M(x, sky[2]), M(y, sky[6])), M(e, sky[10])), M(w, sky[14])),
               A(A(A(M(x, sky[3]), M(y, sky[7])), M(e, sky[11])), M(w, sky[15]))};
        put(f.skyPs.data(), shared.fog);
        put(f.skyPs.data() + 16, shared.scat);
        put(f.skyPs.data() + 80, pos);
        float pixel = D(F(0x3fc90fdb), float(profile.width * 2)),
              sunRadius = M(M(sunSize, F(0x3c187400)), .5f), moonRadius = M(M(moonSize, F(0x3c142917)), .5f);
        put(f.skyPs.data() + 96,
            V4{pixel > sunRadius ? pixel : sunRadius, pixel > moonRadius ? pixel : moonRadius, sz, mw});
        V4 package = environment.selectedDirection;
        put(f.skyPs.data() + 124,
            package.X == moon.X && package.Y == moon.Y && package.Z == moon.Z && package.W == moon.W ? 1.f
                                                                                                     : 0.f);
        put(f.skyPs.data() + 128, environment.sky.skyLights);
        put(f.skyPs.data() + 192, moonMatrix);
        put(f.skyPs.data() + 256,
            source_atmosphere_camera::screenPosition(projectedSun, faceWorldToClip, F(0x3fc90fdb), -1,
                                                     profile.width, profile.width, sunSize, F(0x3c187400)));
        put(f.skyPs.data() + 272,
            source_atmosphere_camera::screenPosition(
                {M(moon.X, -1), M(moon.Y, -1), M(moon.Z, -1), M(moon.W, -1)}, faceWorldToClip, F(0x3fc90fdb),
                -1, profile.width, profile.width, moonSize, F(0x3c142917)));
        put(f.skyPs.data() + 288, altitude);
        put(f.skyPs.data() + 332, M(M(environment.sky.opacityClamp, 100), .01f));
        put(f.cloudVs.data() + 16, source_matrix::inverse(faceWorldToClip));
        put(f.cloudPs.data(), package);
        put(f.cloudPs.data() + 16, eye);
        put(f.cloudPs.data() + 32, V4{});
        put(f.cloudPs.data() + 48, shared.scat);
        std::memcpy(f.cloudPs.data() + 384, lightPacket.cloudPixel.data() + 448, 64);
        put(f.cloudPs.data() + 508, M(environment.sky.opacityClamp, 100));
        put(f.starVs.data(), compose(out.controllerInverse, faceWorldToClip));
        float geometryThreshold = D(F(0x3b000000), cloudLight.geometry40);
        put(f.starVs.data() + 344, geometryThreshold);
        put(f.starPs.data() + 8, geometryThreshold);
    }
    return out;
}
} // namespace source_current_ambient

#ifndef SOURCE_CURRENT_AMBIENT_CPU_ONLY
struct NativeCurrentAmbientFrame {
    source_current_ambient::Profile profile;
    source_current_ambient::Frame current;
    std::array<std::vector<char>, 6> skyVertex, skyPixel;
};
static NativeCurrentAmbientFrame prepareNativeCurrentAmbientFrame(ID3D11Device *device,
                                                                  const fs::path &source,
                                                                  NativeAmbient &ambient,
                                                                  NativeAmbientCloudContinuous &cloud) {
    NativeCurrentAmbientFrame r;
    r.profile = source_current_ambient::readProfile(source);
    auto make = [&](const auto &bytes) {
        std::vector<char> data(bytes.size());
        std::memcpy(data.data(), bytes.data(), bytes.size());
        return nativeMainCloudBuffer(device, data);
    };
    for (UINT face = 0; face < 6; ++face) {
        r.skyVertex[face].assign(reinterpret_cast<const char *>(r.profile.skyVs[face].data()),
                                 reinterpret_cast<const char *>(r.profile.skyVs[face].data() + 144));
        r.skyPixel[face].assign(reinterpret_cast<const char *>(r.profile.skyPs[face].data()),
                                reinterpret_cast<const char *>(r.profile.skyPs[face].data() + 336));
        ambient.skyConstants[face].vs = make(r.profile.skyVs[face]);
        ambient.skyConstants[face].ps = make(r.profile.skyPs[face]);
        // Keep the actual shared WORK Cloud byte cache/buffer used by its phase
        // updater. Only its immutable VS needs to become a current DEFAULT buffer.
        cloud.vertexConstants[face] = make(cloud.vertexBytes[face]);
        ambient.cloudConstants[face].vs = cloud.vertexConstants[face];
        ambient.starConstants[face].ps = make(ambient.starAnimation.faces[face].ps);
    }
    return r;
}
static void
prepareNativeCurrentAmbientInputs(NativeCurrentAmbientFrame &owner,
                                  const source_current_season::Frame &current,
                                  const source_current_season_atmosphere::CloudLightInput &cloudLight) {
    owner.current = source_current_ambient::prepareCurrent(
        owner.profile, source_current_ambient::decodeEnvironment(current), cloudLight);
}
static void
publishNativeCurrentAmbientFrame(ID3D11DeviceContext *context, NativeCurrentAmbientFrame &owner,
                                 NativeAmbient &ambient, NativeAmbientCloudContinuous &cloud,
                                 const std::array<source_camera::V4, 6> *currentCloudClip = nullptr) {
    ambient.starAnimation.clockScale = owner.current.starClockScale;
    ambient.skyEnabled = owner.current.skyEnabled ? 1 : 0;
    for (UINT face = 0; face < 6; ++face) {
        const auto &current = owner.current.faces[face];
        std::memcpy(owner.skyVertex[face].data(), current.skyVs.data(), 144);
        std::memcpy(owner.skyPixel[face].data(), current.skyPs.data(), 336);
        context->UpdateSubresource(ambient.skyConstants[face].vs.Get(), 0, nullptr,
                                   owner.skyVertex[face].data(), 0, 0);
        context->UpdateSubresource(ambient.skyConstants[face].ps.Get(), 0, nullptr,
                                   owner.skyPixel[face].data(), 0, 0);
        std::memcpy(cloud.vertexBytes[face].data() + 16, current.cloudVs.data() + 16, 144);
        // 1196650 is supplied by its independently closed current face producer.
        // Absent input preserves the existing clip cache explicitly; it is not a
        // claim that the old plane is current after the camera changes.
        if (currentCloudClip && (face == 0 || face == 1 || face == 4 || face == 5))
            std::memcpy(cloud.vertexBytes[face].data(), &(*currentCloudClip)[face], 16);
        std::memcpy(cloud.pixelBytes[face].data(), current.cloudPs.data(), 112);
        std::memcpy(cloud.pixelBytes[face].data() + 384, current.cloudPs.data() + 384, 64);
        std::memcpy(cloud.pixelBytes[face].data() + 508, current.cloudPs.data() + 508, 4);
        context->UpdateSubresource(cloud.vertexConstants[face].Get(), 0, nullptr,
                                   cloud.vertexBytes[face].data(), 0, 0);
        // Existing updateNativeAmbientCloudContinuous uploads this same PCB after
        // consuming node3's already advanced offsets. Do not overwrite weather/phase.
        auto &star = ambient.starAnimation.faces[face];
        std::memcpy(star.vs.data(), current.starVs.data(), 64);
        std::memcpy(star.vs.data() + 336, current.starVs.data() + 336, 16);
        std::memcpy(star.ps.data(), current.starPs.data(), 16);
        ambient.starAnimation.faceHeight[face] = current.camera.eye.Y;
        context->UpdateSubresource(ambient.starConstants[face].ps.Get(), 0, nullptr, star.ps.data(), 0, 0);
        // StarVS64 phase/table80 remain owned; its normal actual-face draw uploads
        // the updated completeVS after precisely one11902E0 phase step.
    }
}
#endif
