#pragma once
// Six-face ambient sky, cloud and star constant publication.
// No cube allocation, phase advance, budget reset or history publication.
#include "AmbientCamera.h"
#include "AmbientCelestialMath.h"
#include "SkyMaterialState.h"

namespace source_current_ambient {
using namespace source_camera;
using source_atmosphere_camera::F;
struct Profile {
    uint32_t width{};
    bool milkyTexturePresent{};
    std::array<San14::Render::SkyMaterialState,6> skySeed;
    float cloudSunDirectionW{};
    std::array<std::array<uint8_t, 160>, 6> cloudVs{};
    std::array<std::array<uint8_t, 512>, 6> cloudPs{};
};
inline Profile readProfile(const std::filesystem::path &source) {
    Profile p;
    const auto initial = source / "spring";
    for (unsigned f = 0; f < 6; ++f) {
        std::string n = std::to_string(f);
        p.cloudVs[f] = source_atmosphere_camera::readBytes<160>(initial / ("cloud-face" + n + "-vs.bin"));
        p.cloudPs[f] = source_atmosphere_camera::readBytes<512>(initial / ("cloud-face" + n + "-ps.bin"));
    }
    p.cloudSunDirectionW = get<float>(p.cloudPs[0].data() + 412);
    return p;
}
struct Face {
    CameraTransform camera{};
    San14::Render::SkyMaterialState sky;
    std::array<uint8_t, 160> cloudVs{};
    std::array<uint8_t, 512> cloudPs{};
};
struct Frame {
    std::array<Face, 6> faces;
    bool skyEnabled{};
};
inline Frame prepareCurrent(const Profile &profile, const Environment &environment,
                            const source_current_season_atmosphere::CloudLightInput &cloudLight) {
    Frame out;
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
    auto shared = source_current_season_atmosphere::sharedSkyCloud(environment.selectedFog,
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
        f.sky = profile.skySeed[face];
        f.cloudVs = profile.cloudVs[face];
        f.cloudPs = profile.cloudPs[face];
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
        f.sky.cameraToLocal = compose(compose(source_matrix::inverse(faceProjection), rigid), sky);
        f.sky.projectionFarZ = 0.f;
        f.sky.localToGlobal = localToGlobal;
        float x = M(eye.X, .01f), y = A(A(M(eye.Y, .01f), altitude.Z), offset), e = M(eye.Z, .01f), w = eye.W;
        V4 pos{A(A(A(M(y, sky[4]), M(x, sky[0])), M(e, sky[8])), M(w, sky[12])),
               A(A(A(M(x, sky[1]), M(y, sky[5])), M(e, sky[9])), M(w, sky[13])),
               A(A(A(M(x, sky[2]), M(y, sky[6])), M(e, sky[10])), M(w, sky[14])),
               A(A(A(M(x, sky[3]), M(y, sky[7])), M(e, sky[11])), M(w, sky[15]))};
        f.sky.fog = shared.fog;
        f.sky.scattering = shared.scat;
        f.sky.viewPosition = pos;
        float pixel = D(F(0x3fc90fdb), float(profile.width * 2)),
              sunRadius = M(M(sunSize, F(0x3c187400)), .5f), moonRadius = M(M(moonSize, F(0x3c142917)), .5f);
        f.sky.skyInfo0 = {pixel > sunRadius ? pixel : sunRadius, pixel > moonRadius ? pixel : moonRadius, sz, mw};
        V4 package = environment.selectedDirection;
        f.sky.skyInfo1.W = package.X == moon.X && package.Y == moon.Y && package.Z == moon.Z && package.W == moon.W ? 1.f : 0.f;
        f.sky.skyInfo1.Z = profile.milkyTexturePresent ? environment.sky.milkyIntensity : 0.f;
        f.sky.stableDimensions = environment.sky.stableDimensions;
        std::memcpy(&f.sky.sunRgb, environment.sky.skyLights.data(), 16);
        std::memcpy(&f.sky.sunDirection, environment.sky.skyLights.data() + 4, 16);
        std::memcpy(&f.sky.moonRgb, environment.sky.skyLights.data() + 8, 16);
        std::memcpy(&f.sky.moonDirection, environment.sky.skyLights.data() + 12, 16);
        f.sky.moonFace = moonMatrix;
        f.sky.sunScreenPosition = source_atmosphere_camera::screenPosition(
            projectedSun, faceWorldToClip, F(0x3fc90fdb), -1, profile.width, profile.width, sunSize, F(0x3c187400));
        f.sky.moonScreenPosition = source_atmosphere_camera::screenPosition(
            {M(moon.X, -1), M(moon.Y, -1), M(moon.Z, -1), M(moon.W, -1)}, faceWorldToClip,
            F(0x3fc90fdb), -1, profile.width, profile.width, moonSize, F(0x3c142917));
        f.sky.altitudeInfo = altitude;
        f.sky.clampValue.W = M(M(environment.sky.opacityClamp, 100), .01f);
        put(f.cloudVs.data() + 16, source_matrix::inverse(faceWorldToClip));
        put(f.cloudPs.data(), package);
        put(f.cloudPs.data() + 16, eye);
        put(f.cloudPs.data() + 32, V4{});
        put(f.cloudPs.data() + 48, shared.scat);
        std::memcpy(f.cloudPs.data() + 384, lightPacket.cloudPixel.data() + 448, 64);
        put(f.cloudPs.data() + 508, M(environment.sky.opacityClamp, 100));
    }
    return out;
}
} // namespace source_current_ambient

#ifndef SOURCE_CURRENT_AMBIENT_CPU_ONLY
struct NativeCurrentAmbientFrame {
    source_current_ambient::Profile profile;
    source_current_ambient::Frame current;
};
static NativeCurrentAmbientFrame prepareNativeCurrentAmbientFrame(ID3D11Device *device,
                                                                  const fs::path &source,
                                                                  NativeAmbient &ambient,
                                                                  NativeAmbientCloudContinuous &cloud) {
    NativeCurrentAmbientFrame r;
    r.profile = source_current_ambient::readProfile(source);
    r.profile.width = ambient.width;
    r.profile.milkyTexturePresent = ambient.skySampling.milky;
    for (unsigned face = 0; face < 6; ++face) r.profile.skySeed[face] = ambient.skyFaces[face].state;
    auto make = [&](const auto &bytes) {
        std::vector<char> data(bytes.size());
        std::memcpy(data.data(), bytes.data(), bytes.size());
        return nativeMainCloudBuffer(device, data);
    };
    for (UINT face = 0; face < 6; ++face) {
        // Keep the actual shared WORK Cloud byte cache/buffer used by its phase
        // updater. Only its immutable VS needs to become a current DEFAULT buffer.
        cloud.vertexConstants[face] = make(cloud.vertexBytes[face]);
        ambient.cloudConstants[face].vs = cloud.vertexConstants[face];
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
    ambient.skyEnabled = owner.current.skyEnabled ? 1 : 0;
    for (UINT face = 0; face < 6; ++face) {
        const auto &current = owner.current.faces[face];
        publishNativeAmbientSkyFace(context, ambient, face, current.sky);
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
        ambient.starCameras.faces[face] = San14::Render::StarDrawCamera::from(current.camera);
    }
}
#endif
