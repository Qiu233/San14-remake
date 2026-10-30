#include "gdextension_interface.h"
#include <cstdint>
#include <cstring>
#include <new>
#include <initializer_list>
#include <cmath>
#include <vector>
#include <memory>
#include <atomic>
#include <algorithm>
#include <limits>

// Godot's public GDExtension AudioEffectInstance callback is required here:
// the managed API does not expose its native AudioFrame buffer virtual.
// No PCM, source decoder, clock, device or application registry is owned here.
namespace {
struct alignas(8) Name { unsigned char bytes[64]{}; };
struct Gate { int target = -1; int pair = -1; bool matrix = false; float coefficients[4]{1,0,0,1}; };
struct AudioFrame { float left, right; };
GDExtensionClassLibraryPtr library;
GDExtensionInterfaceClassdbConstructObject construct_object;
GDExtensionInterfaceObjectSetInstance set_instance;
GDExtensionInterfaceRefSetObject ref_set_object;
GDExtensionInterfaceStringNameNewWithUtf8Chars make_name;
GDExtensionInterfaceStringNewWithUtf8Chars make_string;
GDExtensionInterfaceVariantNewNil make_nil;
GDExtensionInterfaceVariantGetType variant_type;
GDExtensionTypeFromVariantConstructorFunc read_integer, read_real;
GDExtensionPtrOperatorEvaluator name_equal;
GDExtensionPtrDestructor destroy_name, destroy_string;
GDExtensionInterfaceClassdbRegisterExtensionClass3 register_class;
GDExtensionInterfaceClassdbRegisterExtensionClassMethod register_method;
GDExtensionInterfaceClassdbUnregisterExtensionClass unregister_class;
Name effect_name, instance_name, effect_base, instance_base, instantiate_name, process_name;
Name set_target_name, bind_pair_name, set_matrix_name, empty_name, argument_name;
Name matrix_arguments[4];
alignas(8) unsigned char empty_string[64]{};

#include "SpreadDetector.h"
#include "SourceEnvelopeEffect.h"

bool equal(GDExtensionConstStringNamePtr left, const Name &right) {
    GDExtensionBool result = false; name_equal(left, &right, &result); return result != 0;
}
GDExtensionObjectPtr create(void *kind) {
    auto data = new (std::nothrow) Gate;
    if (!data) return nullptr;
    bool instance = kind != nullptr;
    auto object = construct_object(instance ? &instance_base : &effect_base);
    if (!object) { delete data; return nullptr; }
    set_instance(object, instance ? &instance_name : &effect_name, data);
    return object;
}
void free_instance(void *, GDExtensionClassInstancePtr value) { delete static_cast<Gate *>(value); }

void instantiate(GDExtensionClassInstancePtr value, const GDExtensionConstTypePtr *, GDExtensionTypePtr result) {
    auto owner = static_cast<Gate *>(value);
    auto data = new (std::nothrow) Gate;
    if (!data) { ref_set_object(result, nullptr); return; }
    data->target = owner->target;
    auto object = construct_object(&instance_base);
    if (!object) { delete data; ref_set_object(result, nullptr); return; }
    set_instance(object, &instance_name, data);
    ref_set_object(result, object);
}
void process(GDExtensionClassInstancePtr value, const GDExtensionConstTypePtr *args, GDExtensionTypePtr) {
    auto gate = static_cast<const Gate *>(value);
    // Godot's make_virtuals.py stores each EncodeT in argval and passes
    // &argval. Native pointer EncodeT is AudioFrame*, so unwrap one slot.
    auto source = *static_cast<const AudioFrame *const *>(args[0]);
    auto target = *static_cast<AudioFrame *const *>(args[1]);
    int frames = static_cast<int>(*static_cast<const int64_t *>(args[2]));
    if (frames <= 0) return;
    size_t bytes = static_cast<size_t>(frames) * sizeof(AudioFrame);
    if (gate->pair >= 0 && gate->pair == gate->target) {
        if (!gate->matrix) std::memcpy(target, source, bytes);
        else for (int index = 0; index < frames; ++index) {
            const float left = source[index].left, right = source[index].right;
            target[index].left = left * gate->coefficients[0] + right * gate->coefficients[2];
            target[index].right = left * gate->coefficients[1] + right * gate->coefficients[3];
        }
    }
    else std::memset(target, 0, bytes);
}
GDExtensionClassCallVirtual get_virtual(void *kind, GDExtensionConstStringNamePtr name) {
    if (!kind && equal(name, instantiate_name)) return instantiate;
    if (kind && equal(name, process_name)) return process;
    return nullptr;
}
void assign(void *kind, GDExtensionClassInstancePtr value, const GDExtensionConstVariantPtr *args,
    GDExtensionInt count, GDExtensionVariantPtr result, GDExtensionCallError *error) {
    make_nil(result);
    if (count != 1 || variant_type(args[0]) != GDEXTENSION_VARIANT_TYPE_INT) {
        error->error = GDEXTENSION_CALL_ERROR_INVALID_ARGUMENT; error->argument = 0;
        error->expected = GDEXTENSION_VARIANT_TYPE_INT; return;
    }
    int64_t pair; read_integer(&pair, const_cast<void *>(args[0]));
    if (pair < 0 || pair > 3) {
        error->error = GDEXTENSION_CALL_ERROR_INVALID_ARGUMENT; error->argument = 0;
        error->expected = GDEXTENSION_VARIANT_TYPE_INT; return;
    }
    auto gate = static_cast<Gate *>(value);
    if (kind) gate->pair = static_cast<int>(pair); else gate->target = static_cast<int>(pair);
    error->error = GDEXTENSION_CALL_OK;
}
void assign_ptr(void *kind, GDExtensionClassInstancePtr value, const GDExtensionConstTypePtr *args,
    GDExtensionTypePtr) {
    int64_t pair = *static_cast<const int64_t *>(args[0]);
    if (pair < 0 || pair > 3) return;
    auto gate = static_cast<Gate *>(value);
    if (kind) gate->pair = static_cast<int>(pair); else gate->target = static_cast<int>(pair);
}
void set_matrix(void *, GDExtensionClassInstancePtr value, const GDExtensionConstVariantPtr *args,
    GDExtensionInt count, GDExtensionVariantPtr result, GDExtensionCallError *error) {
    make_nil(result);
    if (count != 4) { error->error = count < 4 ? GDEXTENSION_CALL_ERROR_TOO_FEW_ARGUMENTS : GDEXTENSION_CALL_ERROR_TOO_MANY_ARGUMENTS; error->expected = 4; return; }
    float coefficients[4];
    for (int index = 0; index < 4; ++index) {
        double coefficient = 0;
        if (variant_type(args[index]) != GDEXTENSION_VARIANT_TYPE_FLOAT) {
            error->error = GDEXTENSION_CALL_ERROR_INVALID_ARGUMENT; error->argument = index;
            error->expected = GDEXTENSION_VARIANT_TYPE_FLOAT; return;
        }
        read_real(&coefficient, const_cast<void *>(args[index]));
        coefficients[index] = static_cast<float>(coefficient);
        if (!std::isfinite(coefficients[index])) {
            error->error = GDEXTENSION_CALL_ERROR_INVALID_ARGUMENT; error->argument = index;
            error->expected = GDEXTENSION_VARIANT_TYPE_FLOAT; return;
        }
    }
    auto gate = static_cast<Gate *>(value);
    std::memcpy(gate->coefficients, coefficients, sizeof(coefficients)); gate->matrix = true;
    error->error = GDEXTENSION_CALL_OK;
}
void set_matrix_ptr(void *, GDExtensionClassInstancePtr value, const GDExtensionConstTypePtr *args,
    GDExtensionTypePtr) {
    float coefficients[4];
    for (int index = 0; index < 4; ++index) {
        coefficients[index] = static_cast<float>(*static_cast<const double *>(args[index]));
        if (!std::isfinite(coefficients[index])) return;
    }
    auto gate = static_cast<Gate *>(value);
    std::memcpy(gate->coefficients, coefficients, sizeof(coefficients)); gate->matrix = true;
}
void bind_matrix_method() {
    GDExtensionPropertyInfo arguments[4]{};
    GDExtensionClassMethodArgumentMetadata metadata[4];
    for (int index = 0; index < 4; ++index) {
        arguments[index].type = GDEXTENSION_VARIANT_TYPE_FLOAT; arguments[index].name = &matrix_arguments[index];
        arguments[index].class_name = &empty_name; arguments[index].hint_string = &empty_string;
        metadata[index] = GDEXTENSION_METHOD_ARGUMENT_METADATA_REAL_IS_DOUBLE;
    }
    GDExtensionClassMethodInfo method{};
    method.name = &set_matrix_name; method.call_func = set_matrix; method.ptrcall_func = set_matrix_ptr;
    method.method_flags = GDEXTENSION_METHOD_FLAG_NORMAL; method.argument_count = 4;
    method.arguments_info = arguments; method.arguments_metadata = metadata;
    register_method(library, &instance_name, &method);
}
void bind_method(const Name &owner, Name &name, void *kind) {
    GDExtensionPropertyInfo argument{};
    argument.type = GDEXTENSION_VARIANT_TYPE_INT; argument.name = &argument_name;
    argument.class_name = &empty_name; argument.hint_string = &empty_string;
    GDExtensionClassMethodArgumentMetadata metadata = GDEXTENSION_METHOD_ARGUMENT_METADATA_INT_IS_INT32;
    GDExtensionClassMethodInfo method{};
    method.name = &name; method.method_userdata = kind; method.call_func = assign; method.ptrcall_func = assign_ptr;
    method.method_flags = GDEXTENSION_METHOD_FLAG_NORMAL; method.argument_count = 1;
    method.arguments_info = &argument; method.arguments_metadata = &metadata;
    register_method(library, &owner, &method);
}
void initialize(void *, GDExtensionInitializationLevel level) {
    if (level != GDEXTENSION_INITIALIZATION_SCENE) return;
    make_name(&effect_name, "San14SpeakerPairGate"); make_name(&instance_name, "San14SpeakerPairGateInstance");
    make_name(&effect_base, "AudioEffect"); make_name(&instance_base, "AudioEffectInstance");
    make_name(&instantiate_name, "_instantiate"); make_name(&process_name, "_process");
    make_name(&set_target_name, "set_target_pair"); make_name(&bind_pair_name, "bind_output_pair");
    make_name(&set_matrix_name, "set_matrix");
    make_name(&matrix_arguments[0], "left_to_left"); make_name(&matrix_arguments[1], "left_to_right");
    make_name(&matrix_arguments[2], "right_to_left"); make_name(&matrix_arguments[3], "right_to_right");
    make_name(&empty_name, ""); make_name(&argument_name, "pair"); make_string(empty_string, "");
    GDExtensionClassCreationInfo3 definition{};
    definition.is_exposed = true; definition.is_runtime = true;
    definition.create_instance_func = create; definition.free_instance_func = free_instance;
    definition.get_virtual_func = get_virtual;
    definition.class_userdata = reinterpret_cast<void *>(1);
    register_class(library, &instance_name, &instance_base, &definition);
    definition.class_userdata = nullptr;
    register_class(library, &effect_name, &effect_base, &definition);
    bind_method(effect_name, set_target_name, nullptr);
    bind_method(instance_name, bind_pair_name, reinterpret_cast<void *>(1));
    bind_matrix_method();
    spread::initialize();
    source_envelope::initialize();
}
void uninitialize(void *, GDExtensionInitializationLevel level) {
    if (level != GDEXTENSION_INITIALIZATION_SCENE) return;
    source_envelope::uninitialize();
    spread::uninitialize();
    unregister_class(library, &effect_name); unregister_class(library, &instance_name);
    for (auto &name : matrix_arguments) destroy_name(&name);
    destroy_name(&set_matrix_name);
    for (auto name : { &argument_name, &empty_name, &bind_pair_name, &set_target_name, &process_name,
        &instantiate_name, &instance_base, &effect_base, &instance_name, &effect_name }) destroy_name(name);
    destroy_string(empty_string);
}
}

extern "C" __declspec(dllexport) GDExtensionBool san14_speakers_init(
    GDExtensionInterfaceGetProcAddress get, GDExtensionClassLibraryPtr owner, GDExtensionInitialization *initialization) {
    library = owner;
#define LOAD(name, type, key) name = reinterpret_cast<type>(get(key)); if (!name) return false
    LOAD(construct_object, GDExtensionInterfaceClassdbConstructObject, "classdb_construct_object");
    LOAD(set_instance, GDExtensionInterfaceObjectSetInstance, "object_set_instance");
    LOAD(ref_set_object, GDExtensionInterfaceRefSetObject, "ref_set_object");
    LOAD(make_name, GDExtensionInterfaceStringNameNewWithUtf8Chars, "string_name_new_with_utf8_chars");
    LOAD(make_string, GDExtensionInterfaceStringNewWithUtf8Chars, "string_new_with_utf8_chars");
    LOAD(make_nil, GDExtensionInterfaceVariantNewNil, "variant_new_nil");
    LOAD(variant_type, GDExtensionInterfaceVariantGetType, "variant_get_type");
    LOAD(register_class, GDExtensionInterfaceClassdbRegisterExtensionClass3, "classdb_register_extension_class3");
    LOAD(register_method, GDExtensionInterfaceClassdbRegisterExtensionClassMethod, "classdb_register_extension_class_method");
    LOAD(unregister_class, GDExtensionInterfaceClassdbUnregisterExtensionClass, "classdb_unregister_extension_class");
    auto integer = reinterpret_cast<GDExtensionInterfaceGetVariantToTypeConstructor>(get("get_variant_to_type_constructor"));
    auto evaluate = reinterpret_cast<GDExtensionInterfaceVariantGetPtrOperatorEvaluator>(get("variant_get_ptr_operator_evaluator"));
    auto destruct = reinterpret_cast<GDExtensionInterfaceVariantGetPtrDestructor>(get("variant_get_ptr_destructor"));
    if (!integer || !evaluate || !destruct) return false;
    read_integer = integer(GDEXTENSION_VARIANT_TYPE_INT);
    read_real = integer(GDEXTENSION_VARIANT_TYPE_FLOAT);
    name_equal = evaluate(GDEXTENSION_VARIANT_OP_EQUAL, GDEXTENSION_VARIANT_TYPE_STRING_NAME, GDEXTENSION_VARIANT_TYPE_STRING_NAME);
    destroy_name = destruct(GDEXTENSION_VARIANT_TYPE_STRING_NAME); destroy_string = destruct(GDEXTENSION_VARIANT_TYPE_STRING);
    if (!read_integer || !read_real || !name_equal || !destroy_name || !destroy_string) return false;
    if (!spread::load(get)) return false;
    initialization->minimum_initialization_level = GDEXTENSION_INITIALIZATION_SCENE;
    initialization->userdata = nullptr; initialization->initialize = initialize; initialization->deinitialize = uninitialize;
    return true;
}
