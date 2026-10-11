#pragma once
#include "OceanSurfaceSettings.h"
#include "OceanTextureMaterialSettings.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>

namespace San14::Render {
struct OceanBubbleSpawnSettings {
    float pointScale{}, emissionPeriod{}, speedMultiplier{}, amplitude{}, envelopeDuration{};
};
struct OceanBubbleFrameRanges {
    std::array<std::uint32_t,3> starts{0,1,2}, counts{};
};

// The ordinary Ocean constructs this CPU wrapper independently of its nullable
// GPU child. It owns accepted displacement lengths and admits spawn/visibility.
struct OceanHorizontalDisplacementBinding {
    std::array<float,2> lengths{3000,3000};
    void install(std::array<float,2> requested) {
        for (auto& value : requested) value = oceanMaterialScale(value);
        lengths = requested;
    }
};

class OceanBubbleRuntime {
public:
    using Point = std::array<float,4>;
    const std::array<Point,16>& points() const { return points_; }
    const OceanBubbleFrameRanges& frameRanges() const { return ranges_; }
    std::uint32_t emissionCount() const { return emissionCount_; }
    float emissionTimer() const { return timer_; }

    void installFrameRanges(const OceanBubbleFrameRanges& ranges) { ranges_ = ranges; }
    void installGeometryCamera(Point eye, Point target) {
        eye_ = eye;
        target_ = target;
    }

    // This operation belongs to the accepted Ocean update, before the current
    // geometry camera replaces the previously prepared eye and target.
    void update(float delta, const OceanBubbleSpawnSettings& settings,
        const OceanSurfaceSettings& surface, const OceanTextureWaveSettings& wave) {
        if (timer_ > settings.emissionPeriod) {
            const float x = target_[0] - eye_[0], y = target_[1] - eye_[1], z = target_[2] - eye_[2];
            const float inverseLength = 1.0f / std::sqrt(x*x + y*y + z*z);
            const float vertical = y * inverseLength;
            if (std::abs(vertical) > 0.0011920929f && eye_[1] - surface.waterHeight > 0.0011920929f) {
                constexpr std::array<std::array<float,2>,24> directions{{
                    {1,0},{1,0},{1,1.2f},{1,-1.5f},{1,0},{1,-1},{1,1},{1,-1},
                    {1,2},{1,-1.2f},{1,1.5f},{1,-1.2f},{1,2},{1,0},{1,1},{1,-2},
                    {1,3},{1,-1.5f},{1,3},{1,-2},{1,0},{1,-1},{1,2},{1,-1.2f}}};
                auto direction = directions[emissionCount_ % directions.size()];
                const float inverseDirection = 1.0f / std::sqrt(direction[0]*direction[0] + direction[1]*direction[1]);
                direction[0] *= inverseDirection;
                direction[1] *= inverseDirection;
                const auto waveDirection = oceanTextureWaveDirection(surface);
                const float angularVelocity = wave.kurtosis * (6.2831855f / wave.length);
                const float speed = (wave.length * angularVelocity / 6.2831855f) * settings.speedMultiplier;
                const float velocityX = (direction[0]*waveDirection[0] - direction[1]*waveDirection[1]) * speed;
                const float velocityZ = (direction[0]*waveDirection[1] + direction[1]*waveDirection[0]) * speed;
                const float intersection = (surface.waterHeight - eye_[1]) / y;
                const auto index = emissionCount_ & 15u;
                points_[index] = {eye_[0] + intersection*x - 0.5f*settings.envelopeDuration*velocityX,
                    eye_[2] + intersection*z - 0.5f*settings.envelopeDuration*velocityZ, settings.pointScale, 0};
                velocities_[index] = {velocityX, velocityZ, settings.envelopeDuration, 0};
                ++emissionCount_;
                if (emissionCount_ > 384) emissionCount_ = emissionCount_ % 384 + 16;
            }
            // One timer subtraction is retained even when the ray rejects an
            // emission. A large delta does not introduce a catch-up loop.
            timer_ -= settings.emissionPeriod;
        }
        for (std::uint32_t i = 0; i < std::min(emissionCount_,16u); ++i) {
            auto& point = points_[i];
            auto& velocity = velocities_[i];
            point[0] += delta * velocity[0];
            point[1] += delta * velocity[1];
            if (velocity[3] < 0 || velocity[3] > velocity[2]) point[3] = 0;
            else {
                const float t = velocity[3] / velocity[2] - 0.5f;
                const float squared = t*t;
                const float envelope = std::max(0.0f,1.0f - 64.0f*squared*squared);
                point[3] = ((envelope + squared + 0.25f) / 1.25f) * settings.amplitude;
            }
            velocity[3] += delta;
        }
        timer_ += delta;
    }

private:
    std::array<Point,16> points_{};
    // Each entry is initialized by emission before it is consumed.
    std::array<Point,16> velocities_{};
    Point eye_{0,100,0,0}, target_{0,0,0,1};
    OceanBubbleFrameRanges ranges_;
    float timer_{};
    std::uint32_t emissionCount_{};
};
} // namespace San14::Render
