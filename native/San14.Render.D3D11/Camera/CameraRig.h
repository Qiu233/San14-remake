#pragma once
#include "CameraMotion.h"
#include "CameraClock.h"
#include "../Scene/Models/FieldDisplayFrame.h"
#include "../Runtime/RenderSession.h"

namespace San14::Render {
// One retained camera rig per render session. Population selection, seasonal
// Main publication and pass scheduling remain with the frame coordinator.
class CameraRig {
public:
    CameraRig()=default;
    CameraRig(const CameraRig&)=delete;
    CameraRig& operator=(const CameraRig&)=delete;

    void prepare(const std::filesystem::path& cameraDirectory,const std::filesystem::path& distantDirectory,
        const std::filesystem::path& motionDirectory) {
        profile_=source_camera::loadProfile(cameraDirectory/"profile.bin");
        prefixProfile_=source_camera::loadRenderModePrefixProfile(cameraDirectory/"render-mode-prefix-profile.bin");
        prefixState_=source_camera::createRenderModePrefixState(prefixProfile_);
        distantProfile_=source_far_main::loadProfile(distantDirectory/"profile.bin");
        distantOwner_=source_far_main::createOwner(distantProfile_);
        motionProfile_=source_camera_motion::loadProfile(motionDirectory/"motion-profile.bin");
        motion_=source_camera_motion::createRuntime(profile_);
        ground_=source_camera::loadGroundHeightProfile(cameraDirectory/"ground-height-current");
        terrainQualifier_=[this](int band,const source_camera::State& candidate,double minimum,double maximum,double requested,double& pitch){
            auto result=source_camera::qualifyTerrainPitch(ground_,{band,candidate,minimum,maximum,requested});pitch=result.pitch;return result.accepted;
        };
        clock_=prepareNativeCameraClock();
    }

    void accept(const RenderInput& input) {
        if(input.contains(FrameInputFlag::PublishedCamera)) {
            motion_.camera.pose=input.controller;
            motion_.camera.zoom={input.band,input.fraction,
                source_camera::A(float(input.band),source_camera::D(float(input.fraction),10000))};
            frame_=source_camera::preparePublished(profile_,input.camera,input.controller);
        }else {
        const int32_t inertiaClock=sampleNativeCameraClock(clock_);
        const int32_t animationClock=sampleNativeCameraClock(clock_);
        source_camera_motion::tick(profile_,motionProfile_,motion_,inertiaClock,animationClock,motionProfile_.inertiaTimeout,terrainQualifier_);
        frame_=source_camera::prepareCurrent(profile_,motion_.camera);
        }
        display_=source_far_main::prepareCurrent(distantProfile_,distantOwner_,motion_.camera.zoom,frame_.prefix);
    }

    source_camera::Prefix prepareDirectPrefix() {
        return source_camera::prepareCurrentRenderModeDirectPrefix(prefixProfile_,prefixState_,
            source_camera::prepareCurrentRenderMode(motion_.camera.zoom),frame_.prefix);
    }

    source_camera::Frame publishedEventCamera(const RenderInput& input)const {
        return source_camera::preparePublished(profile_,input.camera,input.controller);
    }

    // Read-only views are valid while the session lives; accept replaces their
    // contents. No caller can mutate the retained motion or profile owners.
    const source_camera::Frame& frame()const noexcept {return frame_;}
    const source_far_main::Current& display()const noexcept {return display_;}
    const source_far_main::Profile& distantProfile()const noexcept {return distantProfile_;}
    source_camera::Zoom zoom()const noexcept {return motion_.camera.zoom;}
    double distance()const noexcept {return motion_.camera.pose.distance;}

private:
    source_camera::Profile profile_;
    source_camera_motion::Profile motionProfile_;
    source_camera_motion::Runtime motion_;
    source_camera::GroundHeightProfile ground_;
    source_camera_motion::TerrainQualifier terrainQualifier_;
    NativeCameraClock clock_;
    source_camera::RenderModePrefixProfile prefixProfile_;
    source_camera::RenderModePrefixState prefixState_;
    source_far_main::Profile distantProfile_;
    source_far_main::Owner distantOwner_;
    source_camera::Frame frame_;
    source_far_main::Current display_;
};
} // namespace San14::Render
