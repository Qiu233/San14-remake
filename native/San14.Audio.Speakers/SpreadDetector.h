#pragma once

// Included inside SpeakerGate.cpp's private namespace. Only the retained
// GDExtension interface is used; the bank is owned by an actual Resource.
namespace spread {
struct Detector {
    float threshold = 1.0f, hang_seconds = 0.0f;
    int32_t hold = 0;
    bool active = false;
};
struct Control {
    float maximum = 0.0f, current = 0.0f;
    uint32_t attack = 0, release = 0;
    std::vector<int> triggers;
    std::atomic<float> published{0.0f};
};
struct Bank {
    uint32_t sample_rate = 0;
    int locked_frames = 0;
    bool sealed = false;
    std::atomic<bool> valid{true};
    std::vector<Detector> detectors;
    std::vector<std::unique_ptr<Control>> controls;

    bool accept_frames(int frames) {
        if (frames < 1 || !valid.load(std::memory_order_relaxed)) return false;
        if (locked_frames == 0) locked_frames = frames;
        if (frames > locked_frames) {
            valid.store(false, std::memory_order_release);
            return false;
        }
        return true;
    }
    void detect(int index, float peak) {
        auto &detector = detectors[static_cast<size_t>(index)];
        if (peak <= detector.threshold) {
            // The original XAPO subtracts LockForProcess.MaxFrameCount,
            // independently of the currently supplied input frame count.
            const int64_t remaining = static_cast<int64_t>(detector.hold) - locked_frames;
            if (remaining < 1) { detector.hold = 0; peak = -peak; }
            else detector.hold = static_cast<int32_t>(remaining);
        } else {
            detector.hold = static_cast<int32_t>(static_cast<float>(sample_rate) * detector.hang_seconds);
        }
        // Positive and negative zero deliberately have different activity.
        detector.active = !std::signbit(peak);
    }
    void advance(int frames) {
        for (auto &entry : controls) {
            auto &control = *entry;
            bool active = false;
            for (int index : control.triggers)
                active = active || detectors[static_cast<size_t>(index)].active;
            float next;
            if (active) {
                next = control.maximum;
                if (control.attack != 0)
                    next = std::min(next, (static_cast<float>(frames) * next) /
                        static_cast<float>(control.attack) + control.current);
            } else if (control.release == 0) next = 0.0f;
            else {
                // Actual 12BCB30 tests release but divides by attack.
                // A zero attack yields the original IEEE infinity here.
                const float candidate = control.current -
                    (static_cast<float>(frames) * control.maximum) / static_cast<float>(control.attack);
                next = std::max(0.0f, candidate);
            }
            control.current = next;
            control.published.store(next, std::memory_order_release);
        }
    }
};
struct BankObject { std::shared_ptr<Bank> bank; };
struct Role {
    std::shared_ptr<Bank> bank;
    int role = -1, detector = -1, pairs = 0;
    uint32_t seen = 0;
    int block_frames = 0;
    float peak = 0.0f;
};
struct Effect { std::shared_ptr<Role> role; int pair = -1; };

Name bank_name, effect_name, instance_name, resource_base, silence_name;
Name method_names[7], argument_names[4];
GDExtensionInterfaceObjectSetInstanceBinding set_binding;
GDExtensionInterfaceObjectGetInstanceBinding get_binding;
GDExtensionTypeFromVariantConstructorFunc read_object;
GDExtensionVariantFromTypeConstructorFunc write_integer, write_real;

void *binding_create(void *, void *) { return nullptr; }
void binding_free(void *, void *, void *) { }
GDExtensionBool binding_reference(void *, void *, GDExtensionBool) { return true; }
const GDExtensionInstanceBindingCallbacks binding_callbacks{
    binding_create, binding_free, binding_reference
};

bool load(GDExtensionInterfaceGetProcAddress get) {
    set_binding = reinterpret_cast<GDExtensionInterfaceObjectSetInstanceBinding>(get("object_set_instance_binding"));
    get_binding = reinterpret_cast<GDExtensionInterfaceObjectGetInstanceBinding>(get("object_get_instance_binding"));
    auto from = reinterpret_cast<GDExtensionInterfaceGetVariantFromTypeConstructor>(get("get_variant_from_type_constructor"));
    auto to = reinterpret_cast<GDExtensionInterfaceGetVariantToTypeConstructor>(get("get_variant_to_type_constructor"));
    if (!set_binding || !get_binding || !from || !to) return false;
    read_object = to(GDEXTENSION_VARIANT_TYPE_OBJECT);
    write_integer = from(GDEXTENSION_VARIANT_TYPE_INT);
    write_real = from(GDEXTENSION_VARIANT_TYPE_FLOAT);
    return read_object && write_integer && write_real;
}

GDExtensionObjectPtr create_bank(void *) {
    try {
        auto data = std::make_unique<BankObject>();
        data->bank = std::make_shared<Bank>();
        auto object = construct_object(&resource_base);
        if (!object) return nullptr;
        set_instance(object, &bank_name, data.get());
        set_binding(object, library, data.get(), &binding_callbacks);
        data.release();
        return object;
    } catch (...) { return nullptr; }
}
void free_bank(void *, GDExtensionClassInstancePtr value) { delete static_cast<BankObject *>(value); }
GDExtensionObjectPtr create_effect(void *instance) {
    auto data = new (std::nothrow) Effect;
    if (!data) return nullptr;
    auto object = construct_object(instance ? &instance_base : &effect_base);
    if (!object) { delete data; return nullptr; }
    set_instance(object, instance ? &instance_name : &effect_name, data);
    return object;
}
void free_effect(void *, GDExtensionClassInstancePtr value) { delete static_cast<Effect *>(value); }
void instantiate(GDExtensionClassInstancePtr value, const GDExtensionConstTypePtr *, GDExtensionTypePtr result) {
    auto data = new (std::nothrow) Effect;
    if (!data) { ref_set_object(result, nullptr); return; }
    data->role = static_cast<Effect *>(value)->role;
    auto object = construct_object(&instance_base);
    if (!object) { delete data; ref_set_object(result, nullptr); return; }
    set_instance(object, &instance_name, data);
    ref_set_object(result, object);
}
void process_silence(GDExtensionClassInstancePtr, const GDExtensionConstTypePtr *, GDExtensionTypePtr result) {
    *static_cast<GDExtensionBool *>(result) = true;
}
void process(GDExtensionClassInstancePtr value, const GDExtensionConstTypePtr *args, GDExtensionTypePtr) {
    auto effect = static_cast<Effect *>(value);
    auto source = *static_cast<const AudioFrame *const *>(args[0]);
    auto target = *static_cast<AudioFrame *const *>(args[1]);
    const int64_t frame_count = *static_cast<const int64_t *>(args[2]);
    if (frame_count <= 0 || frame_count > INT32_MAX) return;
    const int frames = static_cast<int>(frame_count);
    // Detection never changes PCM. The actual control bus is silenced by
    // its host bus volume after this effect, not by its source send.
    if (target != source) std::memcpy(target, source, static_cast<size_t>(frames) * sizeof(AudioFrame));
    auto role = effect->role.get();
    if (!role || effect->pair < 0 || effect->pair >= role->pairs || !role->bank->sealed) return;
    auto &bank = *role->bank;
    if (!bank.accept_frames(frames)) return;
    const uint32_t bit = 1u << effect->pair;
    if ((role->seen & bit) != 0 || (role->seen != 0 && role->block_frames != frames)) {
        // Pair duplication or inconsistent frame counts is a changed actual
        // output format, not a reason to reuse an earlier detector result.
        bank.valid.store(false, std::memory_order_release);
        return;
    }
    if (role->seen == 0) { role->block_frames = frames; role->peak = 0.0f; }
    if (role->role == 0) {
        float peak = role->peak;
        for (int index = 0; index < frames; ++index) {
            peak = std::max(peak, std::fabs(source[index].left));
            peak = std::max(peak, std::fabs(source[index].right));
        }
        role->peak = peak;
    }
    role->seen |= bit;
    if (role->seen == ((1u << role->pairs) - 1u)) {
        if (role->role == 0) bank.detect(role->detector, role->peak);
        else bank.advance(frames);
        role->seen = 0;
    }
}
GDExtensionClassCallVirtual get_virtual(void *instance, GDExtensionConstStringNamePtr name) {
    // Name equality does not require the old gate's virtual dispatch helper.
    GDExtensionBool matched = false;
    name_equal(name, instance ? static_cast<const void *>(&process_name) : &instantiate_name, &matched);
    if (matched) return instance ? process : instantiate;
    if (instance) {
        name_equal(name, &silence_name, &matched);
        if (matched) return process_silence;
    }
    return nullptr;
}

enum Method { ConfigureBank, AddDetector, AddControl, AddTrigger, Seal, ReadControl, ConfigureEffect, BindPair };
struct Arguments {
    const GDExtensionConstVariantPtr *values;
    GDExtensionInt count;
    GDExtensionCallError *error;
    bool ok = true;
    bool expect(int index, GDExtensionVariantType type) {
        if (!ok) return false;
        if (index >= count || variant_type(values[index]) != type) {
            error->error = GDEXTENSION_CALL_ERROR_INVALID_ARGUMENT;
            error->argument = index; error->expected = type; ok = false;
        }
        return ok;
    }
    int64_t integer(int index) {
        int64_t value = 0;
        if (expect(index, GDEXTENSION_VARIANT_TYPE_INT)) read_integer(&value, const_cast<void *>(values[index]));
        return value;
    }
    float real(int index) {
        double value = 0.0;
        if (expect(index, GDEXTENSION_VARIANT_TYPE_FLOAT)) read_real(&value, const_cast<void *>(values[index]));
        if (!std::isfinite(value) || !std::isfinite(static_cast<float>(value))) reject();
        return static_cast<float>(value);
    }
    void reject() {
        if (!ok) return;
        error->error = GDEXTENSION_CALL_ERROR_INVALID_ARGUMENT;
        error->argument = 0; error->expected = GDEXTENSION_VARIANT_TYPE_NIL; ok = false;
    }
};
void call(void *method, GDExtensionClassInstancePtr value, const GDExtensionConstVariantPtr *args,
    GDExtensionInt count, GDExtensionVariantPtr result, GDExtensionCallError *error) {
    const auto operation = static_cast<Method>(reinterpret_cast<intptr_t>(method));
    static constexpr int counts[]{1, 2, 3, 2, 0, 1, 4, 1};
    if (count != counts[operation]) {
        make_nil(result);
        error->error = count < counts[operation] ? GDEXTENSION_CALL_ERROR_TOO_FEW_ARGUMENTS :
            GDEXTENSION_CALL_ERROR_TOO_MANY_ARGUMENTS;
        error->expected = counts[operation]; return;
    }
    error->error = GDEXTENSION_CALL_OK;
    Arguments input{args, count, error};
    bool has_integer = false, has_real = false;
    int64_t integer_result = 0;
    double real_result = 0.0;
    try {
        if (operation <= ReadControl) {
            auto &bank = *static_cast<BankObject *>(value)->bank;
            if (operation != ReadControl && bank.sealed) input.reject();
            if (operation != ConfigureBank && bank.sample_rate == 0) input.reject();
            if (input.ok) switch (operation) {
                case ConfigureBank: {
                    const int64_t rate = input.integer(0);
                    if (rate < 1 || rate > UINT32_MAX || bank.sample_rate != 0) input.reject();
                    if (input.ok) bank.sample_rate = static_cast<uint32_t>(rate);
                    break;
                }
                case AddDetector: {
                    Detector detector;
                    detector.threshold = input.real(0); detector.hang_seconds = input.real(1);
                    const float hold = static_cast<float>(bank.sample_rate) * detector.hang_seconds;
                    if (detector.threshold < 0.0f || detector.threshold > 1.0f ||
                        detector.hang_seconds < 0.0f || detector.hang_seconds > 10.0f || !std::isfinite(hold) ||
                        static_cast<double>(hold) > INT32_MAX) input.reject();
                    if (input.ok) {
                        integer_result = static_cast<int64_t>(bank.detectors.size()); has_integer = true;
                        bank.detectors.push_back(detector);
                    }
                    break;
                }
                case AddControl: {
                    const float maximum = input.real(0);
                    const int64_t attack = input.integer(1), release = input.integer(2);
                    if (attack < 0 || release < 0 || attack > UINT32_MAX ||
                        release > UINT32_MAX || bank.controls.size() >= 32) input.reject();
                    if (input.ok) {
                        auto control = std::make_unique<Control>();
                        control->maximum = maximum;
                        control->attack = static_cast<uint32_t>(attack); control->release = static_cast<uint32_t>(release);
                        integer_result = static_cast<int64_t>(bank.controls.size()); has_integer = true;
                        bank.controls.push_back(std::move(control));
                    }
                    break;
                }
                case AddTrigger: {
                    const int64_t control = input.integer(0), detector = input.integer(1);
                    if (control < 0 || static_cast<uint64_t>(control) >= bank.controls.size() || detector < 0 ||
                        static_cast<uint64_t>(detector) >= bank.detectors.size()) input.reject();
                    if (input.ok) bank.controls[static_cast<size_t>(control)]->triggers.push_back(static_cast<int>(detector));
                    break;
                }
                case Seal: bank.sealed = true; break;
                case ReadControl: {
                    const int64_t control = input.integer(0);
                    if (!bank.sealed || control < 0 || static_cast<uint64_t>(control) >= bank.controls.size()) input.reject();
                    if (input.ok) {
                        has_real = true;
                        real_result = bank.valid.load(std::memory_order_acquire) ?
                            bank.controls[static_cast<size_t>(control)]->published.load(std::memory_order_acquire) :
                            std::numeric_limits<double>::quiet_NaN();
                    }
                    break;
                }
                default: break;
            }
        } else if (operation == ConfigureEffect) {
            GDExtensionObjectPtr object = nullptr;
            if (input.expect(0, GDEXTENSION_VARIANT_TYPE_OBJECT)) read_object(&object, const_cast<void *>(args[0]));
            auto source = object ? static_cast<BankObject *>(get_binding(object, library, &binding_callbacks)) : nullptr;
            const int64_t kind = input.integer(1), detector = input.integer(2), pairs = input.integer(3);
            auto effect = static_cast<Effect *>(value);
            if (!source || !source->bank->sealed || effect->role || kind < 0 || kind > 1 || pairs < 1 || pairs > 4 ||
                (kind == 0 && (detector < 0 || static_cast<uint64_t>(detector) >= source->bank->detectors.size()))) input.reject();
            if (input.ok) {
                auto role = std::make_shared<Role>();
                role->bank = source->bank; role->role = static_cast<int>(kind);
                role->detector = static_cast<int>(detector); role->pairs = static_cast<int>(pairs);
                effect->role = std::move(role);
            }
        } else {
            const int64_t pair = input.integer(0);
            auto effect = static_cast<Effect *>(value);
            if (!effect->role || pair < 0 || pair >= effect->role->pairs || effect->pair != -1) input.reject();
            if (input.ok) effect->pair = static_cast<int>(pair);
        }
    } catch (...) { input.reject(); }
    if (!input.ok || (!has_integer && !has_real)) make_nil(result);
    else if (has_integer) write_integer(result, &integer_result);
    else write_real(result, &real_result);
}

void bind(const Name &owner, Name &name, Method operation,
    std::initializer_list<GDExtensionVariantType> types, GDExtensionVariantType returns = GDEXTENSION_VARIANT_TYPE_NIL) {
    GDExtensionPropertyInfo arguments[4]{};
    GDExtensionClassMethodArgumentMetadata metadata[4]{};
    size_t index = 0;
    for (auto type : types) {
        arguments[index].type = type; arguments[index].name = &argument_names[index];
        arguments[index].class_name = type == GDEXTENSION_VARIANT_TYPE_OBJECT ? &bank_name : &empty_name;
        arguments[index].hint_string = &empty_string;
        metadata[index] = type == GDEXTENSION_VARIANT_TYPE_INT ? GDEXTENSION_METHOD_ARGUMENT_METADATA_INT_IS_INT64 :
            type == GDEXTENSION_VARIANT_TYPE_FLOAT ? GDEXTENSION_METHOD_ARGUMENT_METADATA_REAL_IS_DOUBLE :
            GDEXTENSION_METHOD_ARGUMENT_METADATA_NONE;
        ++index;
    }
    GDExtensionPropertyInfo returned{};
    returned.type = returns; returned.name = &empty_name;
    returned.class_name = &empty_name; returned.hint_string = &empty_string;
    GDExtensionClassMethodInfo method{};
    method.name = &name; method.method_userdata = reinterpret_cast<void *>(static_cast<intptr_t>(operation));
    method.call_func = call; method.method_flags = GDEXTENSION_METHOD_FLAG_NORMAL;
    method.argument_count = static_cast<uint32_t>(types.size());
    method.arguments_info = arguments; method.arguments_metadata = metadata;
    method.has_return_value = returns != GDEXTENSION_VARIANT_TYPE_NIL; method.return_value_info = &returned;
    method.return_value_metadata = returns == GDEXTENSION_VARIANT_TYPE_INT ? GDEXTENSION_METHOD_ARGUMENT_METADATA_INT_IS_INT64 :
        returns == GDEXTENSION_VARIANT_TYPE_FLOAT ? GDEXTENSION_METHOD_ARGUMENT_METADATA_REAL_IS_DOUBLE :
        GDEXTENSION_METHOD_ARGUMENT_METADATA_NONE;
    register_method(library, &owner, &method);
}
void initialize() {
    make_name(&bank_name, "San14SpatialSpreadBank"); make_name(&effect_name, "San14SpatialSpreadEffect");
    make_name(&instance_name, "San14SpatialSpreadEffectInstance"); make_name(&resource_base, "Resource");
    make_name(&silence_name, "_process_silence");
    const char *methods[]{"configure", "add_detector", "add_control", "add_trigger", "seal", "read_control", "bind_output_pair"};
    for (int index = 0; index < 7; ++index) make_name(&method_names[index], methods[index]);
    const char *arguments[]{"arg0", "arg1", "arg2", "arg3"};
    for (int index = 0; index < 4; ++index) make_name(&argument_names[index], arguments[index]);
    GDExtensionClassCreationInfo3 definition{};
    definition.is_exposed = true; definition.is_runtime = true;
    definition.create_instance_func = create_bank; definition.free_instance_func = free_bank;
    register_class(library, &bank_name, &resource_base, &definition);
    definition.create_instance_func = create_effect; definition.free_instance_func = free_effect;
    definition.get_virtual_func = get_virtual;
    register_class(library, &effect_name, &effect_base, &definition);
    definition.class_userdata = reinterpret_cast<void *>(1);
    register_class(library, &instance_name, &instance_base, &definition);
    bind(bank_name, method_names[0], ConfigureBank, {GDEXTENSION_VARIANT_TYPE_INT});
    bind(bank_name, method_names[1], AddDetector, {GDEXTENSION_VARIANT_TYPE_FLOAT, GDEXTENSION_VARIANT_TYPE_FLOAT}, GDEXTENSION_VARIANT_TYPE_INT);
    bind(bank_name, method_names[2], AddControl, {GDEXTENSION_VARIANT_TYPE_FLOAT, GDEXTENSION_VARIANT_TYPE_INT, GDEXTENSION_VARIANT_TYPE_INT}, GDEXTENSION_VARIANT_TYPE_INT);
    bind(bank_name, method_names[3], AddTrigger, {GDEXTENSION_VARIANT_TYPE_INT, GDEXTENSION_VARIANT_TYPE_INT});
    bind(bank_name, method_names[4], Seal, {});
    bind(bank_name, method_names[5], ReadControl, {GDEXTENSION_VARIANT_TYPE_INT}, GDEXTENSION_VARIANT_TYPE_FLOAT);
    bind(effect_name, method_names[0], ConfigureEffect,
        {GDEXTENSION_VARIANT_TYPE_OBJECT, GDEXTENSION_VARIANT_TYPE_INT, GDEXTENSION_VARIANT_TYPE_INT, GDEXTENSION_VARIANT_TYPE_INT});
    bind(instance_name, method_names[6], BindPair, {GDEXTENSION_VARIANT_TYPE_INT});
}
void uninitialize() {
    unregister_class(library, &instance_name); unregister_class(library, &effect_name); unregister_class(library, &bank_name);
    for (auto &name : argument_names) destroy_name(&name);
    for (auto &name : method_names) destroy_name(&name);
    for (auto name : {&silence_name, &resource_base, &instance_name, &effect_name, &bank_name}) destroy_name(name);
}
}
