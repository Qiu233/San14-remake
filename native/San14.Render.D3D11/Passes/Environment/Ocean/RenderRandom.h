#pragma once
#include <cmath>
#include <cstdint>
#include <mutex>
#include <stdexcept>
#include <vector>

namespace San14::Render {
// Float CRT adapter boundaries; original dispatched CRT bit parity is unproven.
inline float renderRandomLogarithm(float value){return std::log(value);}
inline float renderRandomRoot(float value){return std::sqrt(value);}
// An Ocean owns its LCG for its lifetime. Startup supplies the raw clock seed.
struct RenderRandomObject {
    explicit RenderRandomObject(std::uint32_t startupSeed):state(startupSeed){}
    std::uint32_t state;
    float uniform() {
        state=state*0x10DCDu+1u;
        return float(state>>16)/65535.0f;
    }
};
// The source spare belongs to the render context, shared across random objects.
// A new remake session starts empty; explicit state also supports retained replay.
class RenderRandomContext {
public:
    RenderRandomContext()=default;
    RenderRandomContext(float spare,bool hasSpare):spare_(spare),hasSpare_(hasSpare){}
    float gaussian(RenderRandomObject& object) {
        const std::scoped_lock lock(mutex_);return drawGaussian(object);
    }
    void fillGaussian(RenderRandomObject& object,std::vector<float>& real,std::vector<float>& imaginary) {
        if(real.size()!=imaginary.size())throw std::runtime_error("Gaussian lane sizes differ");
        const std::scoped_lock lock(mutex_);
        for(std::size_t i=0;i<real.size();++i) {
            real[i]=drawGaussian(object);imaginary[i]=drawGaussian(object);
        }
    }
private:
    float drawGaussian(RenderRandomObject& object) {
        if(hasSpare_){hasSpare_=false;return spare_;}
        float a,b,radiusSquared;
        do {
            const float u=object.uniform(),v=object.uniform();
            a=(u+u)-1.0f;b=(v+v)-1.0f;radiusSquared=b*b+a*a;
        }while(radiusSquared>=1.0f);
        const float factor=renderRandomRoot((renderRandomLogarithm(radiusSquared)*-2.0f)/radiusSquared);
        spare_=factor*b;hasSpare_=true;return factor*a;
    }
    float spare_{};
    bool hasSpare_{};
    std::mutex mutex_;
};
} // namespace San14::Render
