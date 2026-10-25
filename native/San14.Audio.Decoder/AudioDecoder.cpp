#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <algorithm>
#include <array>
#include <bit>
#include <cmath>
#include <climits>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <vector>
extern "C" {
#include "libavcodec/avcodec.h"
#include "libavutil/error.h"
#include "libavutil/mem.h"
}

namespace fs = std::filesystem;
static void Need(bool value, const char* message) { if (!value) throw std::runtime_error(message); }
static void CheckAv(int result) {
    if (result < 0) throw std::runtime_error("AAC decoder error " + std::to_string(result));
}
struct Module {
    HMODULE handle = nullptr;
    ~Module() { if (handle) FreeLibrary(handle); }
    void Load(const fs::path& path) {
        handle = LoadLibraryExW(path.c_str(), nullptr, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        Need(handle != nullptr, "Cannot load the selected audio decoder library.");
    }
};
#define CODEC_APIS(X) X(avcodec_find_decoder) X(avcodec_alloc_context3) X(avcodec_parameters_alloc) \
    X(avcodec_parameters_free) X(avcodec_parameters_to_context) X(avcodec_open2) X(avcodec_free_context) \
    X(avcodec_send_packet) X(avcodec_receive_frame) X(avcodec_version) \
    X(av_packet_alloc) X(av_new_packet) X(av_packet_unref) X(av_packet_free)
#define UTIL_APIS(X) X(av_frame_alloc) X(av_frame_unref) X(av_frame_free) X(avutil_version) X(av_mallocz)
struct Api {
    Module util, codec;
#define MEMBER(name) decltype(&name) name##_p = nullptr;
    CODEC_APIS(MEMBER) UTIL_APIS(MEMBER)
#undef MEMBER
    template<class T> static void Get(HMODULE module, T& target, const char* name) {
        target = reinterpret_cast<T>(GetProcAddress(module, name));
        Need(target != nullptr, "Required audio decoder API is missing.");
    }
    explicit Api(const fs::path& directory) {
        util.Load(directory / "avutil-vgmstream-57.dll");
        codec.Load(directory / "avcodec-vgmstream-59.dll");
#define CODEC_GET(name) Get(codec.handle, name##_p, #name);
#define UTIL_GET(name) Get(util.handle, name##_p, #name);
        CODEC_APIS(CODEC_GET) UTIL_APIS(UTIL_GET)
#undef CODEC_GET
#undef UTIL_GET
        Need(avcodec_version_p() == LIBAVCODEC_VERSION_INT && avutil_version_p() == LIBAVUTIL_VERSION_INT,
            "Audio decoder libraries do not match the supported public ABI.");
    }
};
struct Decoder {
    Api& api;
    AVCodecContext* context = nullptr;
    AVCodecParameters* parameters = nullptr;
    AVPacket* packet = nullptr;
    AVFrame* frame = nullptr;
    explicit Decoder(Api& a) : api(a) {}
    ~Decoder() {
        if (frame) api.av_frame_free_p(&frame);
        if (packet) api.av_packet_free_p(&packet);
        if (parameters) api.avcodec_parameters_free_p(&parameters);
        if (context) api.avcodec_free_context_p(&context);
    }
};
static uint16_t U16(const std::vector<uint8_t>& b, size_t p) {
    Need(p <= b.size() && b.size() - p >= 2, "KTAC scalar is truncated.");
    return static_cast<uint16_t>(b[p] | (static_cast<unsigned>(b[p + 1]) << 8));
}
static uint32_t U32(const std::vector<uint8_t>& b, size_t p) {
    Need(p <= b.size() && b.size() - p >= 4, "KTAC scalar is truncated.");
    return static_cast<uint32_t>(b[p]) | (static_cast<uint32_t>(b[p + 1]) << 8) |
        (static_cast<uint32_t>(b[p + 2]) << 16) | (static_cast<uint32_t>(b[p + 3]) << 24);
}
struct Ktac {
    uint32_t rate, totalSamples, streamOffset, streamBytes, packetCount, tableOffset;
    uint16_t channels, frameSamples, delay, padding;
    uint64_t PlayableSamples() const { return static_cast<uint64_t>(totalSamples) - delay - padding; }
    explicit Ktac(const std::vector<uint8_t>& bytes) {
        Need(bytes.size() >= 0x40 && std::memcmp(bytes.data(), "KTAC", 4) == 0 && U32(bytes, 8) == bytes.size(),
            "Input is not a complete KTAC container.");
        Need(U32(bytes, 20) == 0 || U32(bytes, 20) == 2, "This KTAC decoder does not support type1 channel/rate doubling.");
        streamOffset = U32(bytes, 12); streamBytes = U32(bytes, 16); rate = U32(bytes, 24); totalSamples = U32(bytes, 28);
        channels = U16(bytes, 32); frameSamples = U16(bytes, 34); delay = U16(bytes, 36); padding = U16(bytes, 38);
        tableOffset = U32(bytes, 56); packetCount = U32(bytes, 60);
        Need(channels == 6 && rate == 48000 && frameSamples == 1024, "Unsupported multichannel KTAC sample format.");
        const uint64_t tableEnd = static_cast<uint64_t>(tableOffset) + static_cast<uint64_t>(packetCount) * 4;
        const uint64_t streamEnd = static_cast<uint64_t>(streamOffset) + streamBytes;
        Need(packetCount > 0 && tableOffset >= 0x40 && tableEnd <= bytes.size() && streamOffset >= 0x40 &&
            streamEnd <= bytes.size() && (tableEnd <= streamOffset || streamEnd <= tableOffset), "KTAC packet ranges are invalid.");
        Need(static_cast<uint64_t>(packetCount) * frameSamples == totalSamples &&
            static_cast<uint64_t>(delay) + padding < totalSamples && PlayableSamples() * channels * 2 <= 256u * 1024u * 1024u - 128u,
            "KTAC sample accounting or PCM size is invalid.");
    }
};
static void Write16(std::ostream& out, uint16_t value) {
    const char b[] = { static_cast<char>(value), static_cast<char>(value >> 8) }; out.write(b, 2);
}
static void Write32(std::ostream& out, uint32_t value) {
    const char b[] = { static_cast<char>(value), static_cast<char>(value >> 8), static_cast<char>(value >> 16), static_cast<char>(value >> 24) };
    out.write(b, 4);
}
// WAVEFORMATEXTENSIBLE describes the actual decoder lanes in ascending native
// speaker-bit order. No channel count is converted into an invented mask.
static void WriteHeader(std::ostream& out, const Ktac& k, uint32_t mask) {
    const auto dataBytes = static_cast<uint32_t>(k.PlayableSamples() * k.channels * 2);
    out.write("RIFF", 4); Write32(out, dataBytes + 60); out.write("WAVEfmt ", 8); Write32(out, 40);
    Write16(out, 0xfffe); Write16(out, k.channels); Write32(out, k.rate); Write32(out, k.rate * k.channels * 2);
    Write16(out, static_cast<uint16_t>(k.channels * 2)); Write16(out, 16); Write16(out, 22); Write16(out, 16); Write32(out, mask);
    const char pcmGuid[] = { 1, 0, 0, 0, 0, 0, 0x10, 0, static_cast<char>(0x80), 0, 0, static_cast<char>(0xaa), 0, 0x38, static_cast<char>(0x9b), 0x71 };
    out.write(pcmGuid, 16); out.write("data", 4); Write32(out, dataBytes);
}
struct WaveWriter {
    const Ktac& ktac;
    std::ofstream output;
    uint64_t rawSamples = 0, writtenSamples = 0;
    uint64_t mask = 0;
    WaveWriter(const fs::path& path, const Ktac& k) : ktac(k), output(path, std::ios::binary | std::ios::trunc) {
        Need(output.good(), "Cannot create the selected PCM output.");
    }
    void Append(const AVFrame& frame) {
        Need(frame.sample_rate == static_cast<int>(ktac.rate) && frame.nb_samples > 0 &&
            frame.ch_layout.nb_channels == ktac.channels && frame.ch_layout.order == AV_CHANNEL_ORDER_NATIVE &&
            frame.ch_layout.u.mask != 0 && frame.ch_layout.u.mask <= UINT32_MAX &&
            std::popcount(frame.ch_layout.u.mask) == ktac.channels,
            "AAC output has an unsupported or unspecified channel format.");
        if (mask == 0) { mask = frame.ch_layout.u.mask; WriteHeader(output, ktac, static_cast<uint32_t>(mask)); }
        else Need(mask == frame.ch_layout.u.mask, "AAC speaker layout changes inside one sound resource.");
        Need(frame.format == AV_SAMPLE_FMT_FLTP && frame.extended_data != nullptr, "AAC output is not planar float PCM.");
        for (int channel = 0; channel < ktac.channels; ++channel)
            Need(frame.extended_data[channel] != nullptr, "AAC PCM plane is absent.");
        const uint64_t end = rawSamples + static_cast<uint64_t>(frame.nb_samples);
        Need(end <= ktac.totalSamples, "AAC output exceeds the declared sample range.");
        const uint64_t first = std::max<uint64_t>(rawSamples, ktac.delay);
        const uint64_t last = std::min<uint64_t>(end, static_cast<uint64_t>(ktac.totalSamples) - ktac.padding);
        // Conversion/interleave is the single normal decode output operation;
        // there is no second decode, proof scan, hash, or PCM comparison.
        std::vector<int16_t> pcm;
        if (first < last) {
            pcm.resize(static_cast<size_t>(last - first) * ktac.channels);
            size_t at = 0;
            for (uint64_t sample = first; sample < last; ++sample) {
                const size_t offset = static_cast<size_t>(sample - rawSamples);
                for (int channel = 0; channel < ktac.channels; ++channel) {
                    const float value = reinterpret_cast<const float*>(frame.extended_data[channel])[offset] * 32767.0f;
                    Need(std::isfinite(value), "AAC output contains a non-finite PCM sample.");
                    const float clamped = std::clamp(value, -32768.0f, 32767.0f);
                    pcm[at++] = static_cast<int16_t>(clamped);
                }
            }
            output.write(reinterpret_cast<const char*>(pcm.data()), static_cast<std::streamsize>(pcm.size() * sizeof(int16_t)));
            Need(output.good(), "Could not write the decoded PCM range.");
            writtenSamples += last - first;
        }
        rawSamples = end;
    }
    void Complete() {
        Need(mask != 0 && rawSamples == ktac.totalSamples && writtenSamples == ktac.PlayableSamples(),
            "AAC output sample count differs from its KTAC header.");
        output.flush(); Need(output.good(), "Decoded PCM output could not be completed.");
    }
};
static void Decode(const std::vector<uint8_t>& bytes, const fs::path& runtime, const fs::path& output) {
    Ktac k(bytes); Api api(runtime); Decoder state(api);
    const AVCodec* codec = api.avcodec_find_decoder_p(AV_CODEC_ID_AAC);
    Need(codec != nullptr, "AAC decoder is unavailable.");
    state.context = api.avcodec_alloc_context3_p(nullptr); state.parameters = api.avcodec_parameters_alloc_p();
    state.packet = api.av_packet_alloc_p(); state.frame = api.av_frame_alloc_p();
    Need(state.context && state.parameters && state.packet && state.frame, "Cannot allocate the AAC decoder.");
    auto& p = *state.parameters;
    p.codec_type = AVMEDIA_TYPE_AUDIO; p.codec_id = AV_CODEC_ID_AAC; p.sample_rate = static_cast<int>(k.rate);
    p.ch_layout.order = AV_CHANNEL_ORDER_UNSPEC; p.ch_layout.nb_channels = k.channels;
#pragma warning(push)
#pragma warning(disable:4996)
    p.channels = k.channels; p.channel_layout = 0;
#pragma warning(pop)
    p.extradata = static_cast<uint8_t*>(api.av_mallocz_p(2 + AV_INPUT_BUFFER_PADDING_SIZE));
    Need(p.extradata != nullptr, "Cannot allocate AAC configuration.");
    // KTAC's AAC-LC, 48000Hz, channel configuration6; AVFrame later supplies
    // the actual result, including any in-band program configuration change.
    p.extradata_size = 2; p.extradata[0] = 0x11; p.extradata[1] = 0xb0;
    CheckAv(api.avcodec_parameters_to_context_p(state.context, state.parameters));
    CheckAv(api.avcodec_open2_p(state.context, codec, nullptr));
    WaveWriter writer(output, k);
    auto receive = [&]() -> int {
        for (;;) {
            const int result = api.avcodec_receive_frame_p(state.context, state.frame);
            if (result == AVERROR(EAGAIN) || result == AVERROR_EOF) return result;
            CheckAv(result); writer.Append(*state.frame); api.av_frame_unref_p(state.frame);
        }
    };
    uint64_t offset = k.streamOffset;
    for (uint32_t i = 0; i < k.packetCount; ++i) {
        const uint32_t size = U32(bytes, static_cast<size_t>(k.tableOffset) + static_cast<size_t>(i) * 4);
        Need(size > 0 && size <= static_cast<uint32_t>(INT_MAX - AV_INPUT_BUFFER_PADDING_SIZE) &&
            offset + size <= static_cast<uint64_t>(k.streamOffset) + k.streamBytes, "KTAC AAC packet is outside its payload.");
        CheckAv(api.av_new_packet_p(state.packet, static_cast<int>(size)));
        std::memcpy(state.packet->data, bytes.data() + static_cast<size_t>(offset), size);
        for (;;) {
            const int result = api.avcodec_send_packet_p(state.context, state.packet);
            if (result != AVERROR(EAGAIN)) { CheckAv(result); break; }
            const uint64_t before = writer.rawSamples;
            Need(receive() != AVERROR_EOF && writer.rawSamples > before, "AAC decoder made no progress.");
        }
        offset += size; api.av_packet_unref_p(state.packet);
        Need(receive() == AVERROR(EAGAIN), "AAC decoder ended before its packet stream.");
    }
    Need(offset == static_cast<uint64_t>(k.streamOffset) + k.streamBytes, "KTAC packet lengths do not consume its payload.");
    for (;;) {
        const int result = api.avcodec_send_packet_p(state.context, nullptr);
        if (result != AVERROR(EAGAIN)) { CheckAv(result); break; }
        const uint64_t before = writer.rawSamples;
        Need(receive() != AVERROR_EOF && writer.rawSamples > before, "AAC decoder flush made no progress.");
    }
    Need(receive() == AVERROR_EOF, "AAC decoder did not finish its delayed output.");
    writer.Complete();
}
int wmain(int argc, wchar_t** argv) {
    try {
        fs::path input, output, runtime;
        for (int i = 1; i < argc; i += 2) {
            Need(i + 1 < argc, "Audio decoder argument has no value.");
            const std::wstring key(argv[i]);
            if (key == L"--input") input = argv[i + 1];
            else if (key == L"--output") output = argv[i + 1];
            else if (key == L"--runtime-dir") runtime = argv[i + 1];
            else throw std::runtime_error("Unknown audio decoder argument.");
        }
        Need(!input.empty() && !output.empty() && !runtime.empty(), "Audio decoder needs input, output and runtime-dir.");
        const auto length = fs::file_size(input);
        Need(length >= 0x40 && length <= 64u * 1024u * 1024u, "KTAC input exceeds its resource size limit.");
        std::ifstream stream(input, std::ios::binary); std::vector<uint8_t> bytes(static_cast<size_t>(length));
        stream.read(reinterpret_cast<char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
        Need(stream.good(), "Cannot read the selected KTAC input.");
        Decode(bytes, runtime, output); return 0;
    }
    catch (const std::exception& error) { std::cerr << error.what() << '\n'; return 1; }
}
