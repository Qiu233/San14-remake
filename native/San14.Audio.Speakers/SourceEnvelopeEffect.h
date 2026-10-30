#pragma once
#include "SourceEnvelope.h"

// Included in SpeakerGate.cpp's existing private GDExtension namespace.
// Main-thread commands hold AudioServer.Lock; each actual output pair keeps
// its own identical mixer cursor, so channel iteration cannot advance twice.
namespace source_envelope {
struct Control {
    uint32_t sample_rate = 48000, ticks = 0, curve = 0;
    uint64_t generation = 0;
    float from = 1, target = 1;
    bool paused = false;
    std::atomic<float> value{1};
    std::atomic<bool> complete{true};
};
struct Effect {
    std::shared_ptr<Control> control = std::make_shared<Control>();
    uint64_t generation = 0;
    san14_audio::SourceEnvelope envelope;
};
Name effect_name, instance_name, silence_name, method_names[5], arguments[4];
enum Method { Format, Begin, Pause, ReadGain, IsComplete };
bool same_name(GDExtensionConstStringNamePtr left, const Name &right) {
    GDExtensionBool result = false; name_equal(left, &right, &result); return result != 0;
}
GDExtensionObjectPtr create(void *instance) {
    try {
        auto data = std::make_unique<Effect>();
        auto object = construct_object(instance ? &instance_base : &effect_base);
        if (!object) return nullptr;
        set_instance(object, instance ? &instance_name : &effect_name, data.release());
        return object;
    } catch (...) { return nullptr; }
}
void destroy(void *, GDExtensionClassInstancePtr data) { delete static_cast<Effect *>(data); }
void instantiate(GDExtensionClassInstancePtr owner, const GDExtensionConstTypePtr *, GDExtensionTypePtr result) {
    try {
        auto data = std::make_unique<Effect>();
        data->control = static_cast<Effect *>(owner)->control;
        auto object = construct_object(&instance_base);
        if (!object) { ref_set_object(result, nullptr); return; }
        set_instance(object, &instance_name, data.release());
        ref_set_object(result, object);
    } catch (...) { ref_set_object(result, nullptr); }
}
void silence(GDExtensionClassInstancePtr, const GDExtensionConstTypePtr *, GDExtensionTypePtr result) {
    *static_cast<GDExtensionBool *>(result) = true;
}
void process(GDExtensionClassInstancePtr value, const GDExtensionConstTypePtr *args, GDExtensionTypePtr) {
    auto &effect = *static_cast<Effect *>(value);
    auto source = *static_cast<const AudioFrame *const *>(args[0]);
    auto output = *static_cast<AudioFrame *const *>(args[1]);
    const int64_t frames = *static_cast<const int64_t *>(args[2]);
    const auto &control = *effect.control;
    if (effect.generation != control.generation) {
        effect.envelope.begin(control.from, control.target, control.ticks, control.curve);
        effect.generation = control.generation;
    }
    for (int64_t frame = 0; frame < frames; ++frame) {
        const float gain = effect.envelope.value;
        output[frame].left = source[frame].left * gain;
        output[frame].right = source[frame].right * gain;
        if (!control.paused) effect.envelope.advance_frame(control.sample_rate);
    }
    effect.control->value.store(effect.envelope.value, std::memory_order_release);
    effect.control->complete.store(effect.envelope.duration == 0, std::memory_order_release);
}
GDExtensionClassCallVirtual get_virtual(void *instance, GDExtensionConstStringNamePtr name) {
    if (!instance && same_name(name, instantiate_name)) return instantiate;
    if (instance && same_name(name, process_name)) return process;
    if (instance && same_name(name, silence_name)) return silence;
    return nullptr;
}
void call(void *operation, GDExtensionClassInstancePtr value, const GDExtensionConstVariantPtr *args,
    GDExtensionInt count, GDExtensionVariantPtr result, GDExtensionCallError *error) {
    make_nil(result);
    const auto method = static_cast<Method>(reinterpret_cast<intptr_t>(operation));
    const int expected = method == Begin ? 4 : method == ReadGain || method == IsComplete ? 0 : 1;
    auto reject = [&] { error->error = GDEXTENSION_CALL_ERROR_INVALID_ARGUMENT; };
    if (count != expected) { reject(); return; }
    int64_t integers[4]{}; double reals[2]{};
    for (int index = 0; index < expected; ++index) {
        const bool real = method == Begin && index < 2;
        const auto type = real ? GDEXTENSION_VARIANT_TYPE_FLOAT : GDEXTENSION_VARIANT_TYPE_INT;
        if (variant_type(args[index]) != type) { reject(); return; }
        if (real) read_real(&reals[index], const_cast<void *>(args[index]));
        else read_integer(&integers[index], const_cast<void *>(args[index]));
    }
    auto &control = *static_cast<Effect *>(value)->control;
    if (method == ReadGain) {
        double gain = control.value.load(std::memory_order_acquire);
        spread::write_real(result, &gain); return;
    }
    if (method == IsComplete) {
        int64_t complete = control.complete.load(std::memory_order_acquire) ? 1 : 0;
        spread::write_integer(result, &complete); return;
    }
    if (method == Format) {
        if (integers[0] < 1 || integers[0] > 384000) { reject(); return; }
        control.sample_rate = static_cast<uint32_t>(integers[0]);
    } else if (method == Pause) {
        if (integers[0] != 0 && integers[0] != 1) { reject(); return; }
        control.paused = integers[0] != 0;
    } else {
        if (!std::isfinite(reals[0]) || !std::isfinite(reals[1]) || reals[0] < 0 || reals[1] < 0 ||
            integers[2] < 0 || integers[2] >= INT32_MAX || integers[3] < 0 || integers[3] > 3) { reject(); return; }
        control.from = static_cast<float>(reals[0]); control.target = static_cast<float>(reals[1]);
        control.ticks = static_cast<uint32_t>(integers[2]); control.curve = static_cast<uint32_t>(integers[3]);
        control.value.store(control.ticks == 0 ? control.target : control.from, std::memory_order_release);
        control.complete.store(control.ticks == 0, std::memory_order_release);
        ++control.generation;
    }
}
void bind(Name &name, Method method) {
    GDExtensionPropertyInfo inputs[4]{};
    GDExtensionClassMethodArgumentMetadata metadata[4]{};
    const int count = method == Begin ? 4 : method == ReadGain || method == IsComplete ? 0 : 1;
    for (int index = 0; index < count; ++index) {
        bool real = method == Begin && index < 2;
        inputs[index].type = real ? GDEXTENSION_VARIANT_TYPE_FLOAT : GDEXTENSION_VARIANT_TYPE_INT;
        inputs[index].name = &arguments[index]; inputs[index].class_name = &empty_name;
        inputs[index].hint_string = &empty_string;
        metadata[index] = real ? GDEXTENSION_METHOD_ARGUMENT_METADATA_REAL_IS_DOUBLE :
            GDEXTENSION_METHOD_ARGUMENT_METADATA_INT_IS_INT64;
    }
    GDExtensionClassMethodInfo info{};
    info.name = &name; info.method_userdata = reinterpret_cast<void *>(static_cast<intptr_t>(method));
    info.call_func = call; info.method_flags = GDEXTENSION_METHOD_FLAG_NORMAL;
    info.argument_count = count; info.arguments_info = inputs; info.arguments_metadata = metadata;
    GDExtensionPropertyInfo returned{};
    returned.type = method == ReadGain ? GDEXTENSION_VARIANT_TYPE_FLOAT : GDEXTENSION_VARIANT_TYPE_INT;
    returned.name = &empty_name; returned.class_name = &empty_name; returned.hint_string = &empty_string;
    info.has_return_value = method == ReadGain || method == IsComplete;
    info.return_value_info = &returned;
    info.return_value_metadata = method == ReadGain ? GDEXTENSION_METHOD_ARGUMENT_METADATA_REAL_IS_DOUBLE :
        GDEXTENSION_METHOD_ARGUMENT_METADATA_INT_IS_INT64;
    register_method(library, &effect_name, &info);
}
void initialize() {
    make_name(&effect_name, "San14SourceEnvelope"); make_name(&instance_name, "San14SourceEnvelopeInstance");
    make_name(&silence_name, "_process_silence");
    const char *names[]{"set_format", "begin", "set_paused", "read_gain", "is_complete"};
    for (int index = 0; index < 5; ++index) make_name(&method_names[index], names[index]);
    const char *argument_names[]{"from", "target", "ticks", "curve"};
    for (int index = 0; index < 4; ++index) make_name(&arguments[index], argument_names[index]);
    GDExtensionClassCreationInfo3 definition{};
    definition.is_exposed = true; definition.is_runtime = true;
    definition.create_instance_func = create; definition.free_instance_func = destroy;
    definition.get_virtual_func = get_virtual;
    register_class(library, &effect_name, &effect_base, &definition);
    definition.class_userdata = reinterpret_cast<void *>(1);
    register_class(library, &instance_name, &instance_base, &definition);
    for (int index = 0; index < 5; ++index) bind(method_names[index], static_cast<Method>(index));
}
void uninitialize() {
    unregister_class(library, &instance_name); unregister_class(library, &effect_name);
    for (auto &name : arguments) destroy_name(&name);
    for (auto &name : method_names) destroy_name(&name);
    for (auto name : {&silence_name, &instance_name, &effect_name}) destroy_name(name);
}
}
