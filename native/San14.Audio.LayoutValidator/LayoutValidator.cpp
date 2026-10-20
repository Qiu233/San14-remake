#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <bcrypt.h>
#include <algorithm>
#include <array>
#include <atomic>
#include <climits>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <limits>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>
extern "C" {
#include "libavcodec/avcodec.h"
#include "libavutil/error.h"
#include "libavutil/mem.h"
}

namespace fs = std::filesystem;
static std::atomic<bool> cancelled = false;
static BOOL WINAPI OnControl(DWORD event) {
    if (event == CTRL_C_EVENT || event == CTRL_BREAK_EVENT) {
        cancelled.store(true); return TRUE;
    }
    return FALSE;
}
struct Failure : std::runtime_error {
    std::string code;
    Failure(std::string c, std::string detail) : std::runtime_error(std::move(detail)), code(std::move(c)) {}
};
static void Require(bool ok, const char* code, const char* detail) {
    if (!ok) throw Failure(code, detail);
}
static void CheckCancellation(bool value = cancelled.load()) {
    if (value) throw Failure("cancelled", "Cancellation requested; no attestation was issued.");
}
static std::string Utf8(const std::wstring& s) {
    if (s.empty()) return {};
    int n = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, s.data(), static_cast<int>(s.size()), nullptr, 0, nullptr, nullptr);
    Require(n > 0, "path_encoding", "Could not encode a path.");
    std::string result(static_cast<size_t>(n), '\0');
    WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, s.data(), static_cast<int>(s.size()), result.data(), n, nullptr, nullptr);
    return result;
}
static std::string Q(const std::string& s) {
    static constexpr char hex[] = "0123456789ABCDEF";
    std::string out = "\"";
    for (unsigned char c : s) {
        if (c == '\\' || c == '"') { out += '\\'; out += static_cast<char>(c); }
        else if (c < 0x20) { out += "\\u00"; out += hex[c >> 4]; out += hex[c & 15]; }
        else out += static_cast<char>(c);
    }
    return out + "\"";
}
struct FileHandle {
    HANDLE handle = INVALID_HANDLE_VALUE;
    explicit FileHandle(const fs::path& path) {
        handle = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
        Require(handle != INVALID_HANDLE_VALUE, "file_open", "Could not open the requested read-only file.");
    }
    ~FileHandle() { if (handle != INVALID_HANDLE_VALUE) CloseHandle(handle); }
    FileHandle(const FileHandle&) = delete;
    FileHandle& operator=(const FileHandle&) = delete;
};
static std::vector<uint8_t> ReadAll(const fs::path& path) {
    FileHandle file(path); LARGE_INTEGER length{};
    Require(GetFileSizeEx(file.handle, &length) != FALSE, "file_size", "Could not read file length.");
    Require(length.QuadPart >= 0 && length.QuadPart <= 64LL * 1024 * 1024, "file_budget", "File exceeds this helper's 64 MiB profile.");
    std::vector<uint8_t> data(static_cast<size_t>(length.QuadPart));
    size_t offset = 0;
    while (offset < data.size()) {
        CheckCancellation();
        DWORD received = 0, request = static_cast<DWORD>(std::min<size_t>(data.size() - offset, 1024 * 1024));
        Require(ReadFile(file.handle, data.data() + offset, request, &received, nullptr) != FALSE && received > 0,
                "file_read", "Could not read the complete file.");
        offset += received;
    }
    return data;
}
static void WriteNew(const fs::path& path, const std::string& bytes) {
    HANDLE file = CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) throw std::runtime_error("Proof output must be a new file; no existing file was overwritten.");
    size_t offset = 0; bool ok = true;
    while (offset < bytes.size()) {
        DWORD written = 0, requested = static_cast<DWORD>(std::min<size_t>(bytes.size() - offset, 1024 * 1024));
        if (!WriteFile(file, bytes.data() + offset, requested, &written, nullptr) || written == 0) { ok = false; break; }
        offset += written;
    }
    if (!FlushFileBuffers(file)) ok = false;
    CloseHandle(file);
    if (!ok) throw std::runtime_error("Could not write the complete new proof JSON; result is unavailable.");
}
struct Sha256 {
    BCRYPT_ALG_HANDLE algorithm = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
    std::vector<uint8_t> object;
    ~Sha256() { if (hash) BCryptDestroyHash(hash); if (algorithm) BCryptCloseAlgorithmProvider(algorithm, 0); }
    std::string Compute(const std::vector<uint8_t>& bytes) {
        Require(BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) >= 0, "sha_provider", "BCrypt SHA256 provider failed.");
        ULONG length = 0, got = 0;
        Require(BCryptGetProperty(algorithm, BCRYPT_OBJECT_LENGTH, reinterpret_cast<PUCHAR>(&length), sizeof(length), &got, 0) >= 0,
                "sha_property", "BCrypt hash-object length failed.");
        object.resize(length);
        Require(BCryptCreateHash(algorithm, &hash, object.data(), length, nullptr, 0, 0) >= 0, "sha_create", "BCrypt hash creation failed.");
        Require(BCryptHashData(hash, const_cast<PUCHAR>(bytes.data()), static_cast<ULONG>(bytes.size()), 0) >= 0,
                "sha_data", "BCrypt input hashing failed.");
        std::array<uint8_t, 32> digest{};
        Require(BCryptFinishHash(hash, digest.data(), static_cast<ULONG>(digest.size()), 0) >= 0, "sha_finish", "BCrypt hash finalization failed.");
        static constexpr char hex[] = "0123456789ABCDEF";
        std::string result; result.reserve(64);
        for (uint8_t x : digest) { result += hex[x >> 4]; result += hex[x & 15]; }
        return result;
    }
};
static std::string Hash(const std::vector<uint8_t>& bytes) { Sha256 hash; return hash.Compute(bytes); }
static fs::path ModulePath(HMODULE module = nullptr) {
    std::wstring path(32768, L'\0');
    DWORD n = GetModuleFileNameW(module, path.data(), static_cast<DWORD>(path.size()));
    Require(n > 0 && n < path.size(), "module_path", "Could not resolve the loaded module path.");
    path.resize(n); return fs::canonical(path);
}
struct Module {
    HMODULE handle = nullptr;
    ~Module() { if (handle) FreeLibrary(handle); }
    void Load(const fs::path& path) {
        handle = LoadLibraryExW(path.c_str(), nullptr, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        Require(handle != nullptr, "runtime_load", "Could not load the pinned runtime library.");
        Require(ModulePath(handle) == fs::canonical(path), "runtime_path", "Loaded module path differs from the requested pinned path.");
    }
};
#define CODEC_APIS(X) X(avcodec_find_decoder) X(avcodec_alloc_context3) X(avcodec_parameters_alloc) \
    X(avcodec_parameters_free) X(avcodec_parameters_to_context) X(avcodec_open2) X(avcodec_free_context) \
    X(avcodec_send_packet) X(avcodec_receive_frame) X(avcodec_version) X(avcodec_configuration) \
    X(av_packet_alloc) X(av_new_packet) X(av_packet_unref) X(av_packet_free)
#define UTIL_APIS(X) X(av_frame_alloc) X(av_frame_unref) X(av_frame_free) X(avutil_version) X(av_mallocz)
struct Api {
    Module util, codec;
#define MEMBER(name) decltype(&name) name##_p = nullptr;
    CODEC_APIS(MEMBER) UTIL_APIS(MEMBER)
#undef MEMBER
    template<class T> static void Get(HMODULE module, T& target, const char* name) {
        target = reinterpret_cast<T>(GetProcAddress(module, name));
        Require(target != nullptr, "runtime_export", "A required pinned runtime export is missing.");
    }
    void Load(const fs::path& directory) {
        util.Load(directory / "avutil-vgmstream-57.dll");
        codec.Load(directory / "avcodec-vgmstream-59.dll");
#define CODEC_GET(name) Get(codec.handle, name##_p, #name);
#define UTIL_GET(name) Get(util.handle, name##_p, #name);
        CODEC_APIS(CODEC_GET) UTIL_APIS(UTIL_GET)
#undef CODEC_GET
#undef UTIL_GET
    }
};
struct DecoderState {
    Api& api;
    AVCodecContext* context = nullptr;
    AVCodecParameters* parameters = nullptr;
    AVPacket* packet = nullptr;
    AVFrame* frame = nullptr;
    explicit DecoderState(Api& a) : api(a) {}
    ~DecoderState() {
        if (frame) api.av_frame_free_p(&frame);
        if (packet) api.av_packet_free_p(&packet);
        if (parameters) api.avcodec_parameters_free_p(&parameters);
        if (context) api.avcodec_free_context_p(&context);
    }
};
static uint16_t U16(const std::vector<uint8_t>& b, size_t p) {
    Require(p <= b.size() && b.size() - p >= 2, "ktac_bounds", "KTAC scalar is outside the input.");
    return static_cast<uint16_t>(b[p] | (static_cast<unsigned>(b[p + 1]) << 8));
}
static uint32_t U32(const std::vector<uint8_t>& b, size_t p) {
    Require(p <= b.size() && b.size() - p >= 4, "ktac_bounds", "KTAC scalar is outside the input.");
    return static_cast<uint32_t>(b[p]) | (static_cast<uint32_t>(b[p + 1]) << 8) |
           (static_cast<uint32_t>(b[p + 2]) << 16) | (static_cast<uint32_t>(b[p + 3]) << 24);
}
struct Profile {
    uint32_t version = 0, type = 0, rate = 0, numSamples = 0, streamOffset = 0, streamSize = 0, tableOffset = 0, packetCount = 0;
    uint16_t channels = 0, frameSamples = 0, delay = 0, padding = 0;
    uint64_t pcmFrames = 0;
    std::vector<uint32_t> sizes;
};
static Profile Parse(const std::vector<uint8_t>& bytes) {
    Require(bytes.size() >= 0x40 && std::memcmp(bytes.data(), "KTAC", 4) == 0, "ktac_magic", "Input is not a complete KTAC header.");
    Require(U32(bytes, 8) == bytes.size(), "ktac_length", "KTAC declared length differs from the actual bytes hashed by this helper.");
    Profile p;
    p.version = U32(bytes, 4); p.streamOffset = U32(bytes, 12); p.streamSize = U32(bytes, 16);
    p.type = U32(bytes, 20); p.rate = U32(bytes, 24); p.numSamples = U32(bytes, 28);
    p.channels = U16(bytes, 32); p.frameSamples = U16(bytes, 34); p.delay = U16(bytes, 36); p.padding = U16(bytes, 38);
    p.tableOffset = U32(bytes, 56); p.packetCount = U32(bytes, 60);
    Require(p.type == 0 || p.type == 2, "ktac_type_profile", "Only KTAC types 0 and 2 are supported; no type-1 doubling is applied.");
    Require(p.channels == 6 && p.rate == 48000 && p.frameSamples == 1024, "ktac_shape_profile", "Only AAC LC six-channel/48000/1024 KTAC is supported.");
    Require(p.packetCount > 0 && 0x800ULL + static_cast<uint64_t>(p.packetCount) * 4 <= 0x100000,
            "ktac_packet_profile", "Packet count exceeds the retained fake-MP4 header profile.");
    uint64_t tableEnd = static_cast<uint64_t>(p.tableOffset) + static_cast<uint64_t>(p.packetCount) * 4;
    uint64_t streamEnd = static_cast<uint64_t>(p.streamOffset) + p.streamSize;
    Require(p.tableOffset >= 0x40 && tableEnd <= bytes.size() && p.streamOffset >= 0x40 && streamEnd <= bytes.size(),
            "ktac_ranges", "KTAC table or stream is outside its exact input.");
    Require(tableEnd <= p.streamOffset || streamEnd <= p.tableOffset, "ktac_overlap_profile", "Overlapping table and stream are outside this helper profile.");
    Require(static_cast<uint64_t>(p.packetCount) * p.frameSamples == p.numSamples,
            "ktac_header_accounting", "Header samples do not equal packet count times frame samples in this profile.");
    Require(static_cast<uint64_t>(p.delay) + p.padding < p.numSamples, "ktac_trim", "Encoder delay and padding leave no PCM frames.");
    p.pcmFrames = static_cast<uint64_t>(p.numSamples) - p.delay - p.padding;
    uint64_t total = 0;
    for (uint32_t i = 0; i < p.packetCount; ++i) {
        CheckCancellation();
        uint32_t size = U32(bytes, static_cast<size_t>(p.tableOffset) + static_cast<size_t>(i) * 4);
        Require(size > 0 && size <= static_cast<uint32_t>(INT_MAX - AV_INPUT_BUFFER_PADDING_SIZE),
                "ktac_packet_size", "Zero or oversized AAC packets are outside this helper profile.");
        total += size;
        Require(total <= p.streamSize, "ktac_packet_range", "Declared packets exceed the stream range.");
        p.sizes.push_back(size);
    }
    Require(total == p.streamSize, "ktac_payload_coverage_profile", "Declared packet lengths do not consume the exact stream range.");
    return p;
}
struct Layout {
    int order = -1, channels = 0, rate = 0, format = -1;
    uint64_t mask = 0;
    static Layout From(const AVFrame& frame) {
        return {static_cast<int>(frame.ch_layout.order), frame.ch_layout.nb_channels, frame.sample_rate, frame.format,
                frame.ch_layout.order == AV_CHANNEL_ORDER_NATIVE ? frame.ch_layout.u.mask : 0};
    }
};
static std::string LayoutJson(const Layout& l) {
    return "{\"order\":" + std::to_string(l.order) + ",\"channels\":" + std::to_string(l.channels) +
        ",\"mask\":" + std::to_string(l.mask) + ",\"sampleRate\":" + std::to_string(l.rate) + ",\"sampleFormat\":" + std::to_string(l.format) + "}";
}
struct Ledger {
    uint64_t observedFrames = 0, acceptedFrames = 0, rawSamples = 0;
    uint64_t packetIndex = 0;
    bool inFlush = false, flushSent = false, eof = false;
    Layout first, last;
    void Observe(const AVFrame& frame) {
        CheckCancellation();
        last = Layout::From(frame);
        if (observedFrames++ == 0) first = last;
        Require(frame.nb_samples > 0, "frame_samples", "A decoded frame has no samples.");
        Require(last.rate == 48000, "frame_rate", "A decoded frame has a different sample rate.");
        Require(last.order == AV_CHANNEL_ORDER_NATIVE, "frame_order", "A decoded frame does not have native channel order.");
        Require(last.channels == 6, "frame_channels", "A decoded frame does not have six channels.");
        Require(last.mask == 0x3F, "frame_mask", "A decoded frame is not FL FR FC LFE BL BR; no lane remap is attempted.");
        Require(rawSamples <= UINT64_MAX - static_cast<uint64_t>(frame.nb_samples), "sample_overflow", "Decoded sample count overflow.");
        rawSamples += static_cast<uint64_t>(frame.nb_samples); ++acceptedFrames;
    }
    void Complete(const Profile& p, uint64_t expectedPcmFrames, uint64_t completedPackets) const {
        Require(flushSent && eof, "decode_incomplete", "Decoder flush did not finish with EOF.");
        Require(completedPackets == p.packetCount, "packet_incomplete", "Not every declared AAC packet was accepted.");
        Require(observedFrames > 0 && observedFrames == acceptedFrames, "frames_incomplete", "No complete uniform frame observation exists.");
        Require(rawSamples == p.numSamples, "decoded_raw_accounting", "Actual raw decoded samples differ from the KTAC header accounting.");
        Require(rawSamples >= static_cast<uint64_t>(p.delay) + p.padding && rawSamples - p.delay - p.padding == expectedPcmFrames,
                "decoded_pcm_accounting", "Actual raw samples after header delay/padding differ from the expected production PCM count.");
    }
};
struct Proof {
    bool selfTest = false, attested = false;
    std::string status = "FAIL", code, detail, helperSha, inputSha, codecSha, utilSha, codecConfiguration;
    std::string helperPath, inputPath, runtimeDirectory;
    uint64_t inputBytes = 0, completedPackets = 0, expectedPcmFrames = 0;
    unsigned codecVersion = 0, utilVersion = 0;
    Profile profile;
    Ledger ledger;
    std::vector<std::string> selfChecks;
    std::string Json() const {
        std::ostringstream o;
        o << "{\n\"proofVersion\":1,\"mode\":" << Q(selfTest ? "synthetic-frame-self-test" : "whole-stream-ktac-layout")
          << ",\"result\":" << Q(status) << ",\"canonicalLayoutAttested\":" << (attested ? "true" : "false")
          << ",\"failureCode\":" << Q(code) << ",\"detail\":" << Q(detail)
          << ",\n\"helper\":{\"path\":" << Q(helperPath) << ",\"sha256\":" << Q(helperSha) << "}"
          << ",\n\"input\":{\"path\":" << Q(inputPath) << ",\"bytes\":" << inputBytes << ",\"sha256\":" << Q(inputSha) << "}"
          << ",\n\"runtime\":{\"directory\":" << Q(runtimeDirectory) << ",\"avcodecSha256\":" << Q(codecSha)
          << ",\"avutilSha256\":" << Q(utilSha) << ",\"avcodecVersion\":" << codecVersion
          << ",\"avutilVersion\":" << utilVersion << ",\"avcodecConfiguration\":" << Q(codecConfiguration) << "}"
          << ",\n\"profile\":{\"ktacVersion\":" << profile.version << ",\"ktacType\":" << profile.type
          << ",\"sampleRate\":" << profile.rate << ",\"channels\":" << profile.channels
          << ",\"frameSamples\":" << profile.frameSamples << ",\"headerRawSamples\":" << profile.numSamples
          << ",\"encoderDelay\":" << profile.delay << ",\"endPadding\":" << profile.padding
          << ",\"headerPcmFrames\":" << profile.pcmFrames << ",\"expectedProductionPcmFrames\":" << expectedPcmFrames
          << ",\"declaredPackets\":" << profile.packetCount << ",\"audioSpecificConfigHex\":\"11B0\"}"
          << ",\n\"observation\":{\"completedPackets\":" << completedPackets << ",\"observedFrames\":" << ledger.observedFrames
          << ",\"acceptedCanonicalFrames\":" << ledger.acceptedFrames << ",\"rawSamples\":" << ledger.rawSamples
          << ",\"currentPacketIndex\":" << ledger.packetIndex << ",\"inFlush\":" << (ledger.inFlush ? "true" : "false")
          << ",\"flushSent\":" << (ledger.flushSent ? "true" : "false") << ",\"eof\":" << (ledger.eof ? "true" : "false")
          << ",\"firstLayout\":" << LayoutJson(ledger.first) << ",\"lastLayout\":" << LayoutJson(ledger.last) << "}"
          << ",\n\"syntheticChecks\":[";
        for (size_t i = 0; i < selfChecks.size(); ++i) { if (i) o << ','; o << Q(selfChecks[i]); }
        o << "],\n\"waveChannelMaskWasNotChanged\":true,\"pcmWasNotWrittenOrReordered\":true}\n";
        return o.str();
    }
};
static void CheckAv(int value, const char* code) {
    if (value < 0) throw Failure(code, std::string("FFmpeg API returned ") + std::to_string(value));
}
static void Decode(const std::vector<uint8_t>& bytes, const fs::path& runtime, Proof& proof) {
    proof.runtimeDirectory = Utf8(runtime.wstring());
    proof.codecSha = Hash(ReadAll(runtime / "avcodec-vgmstream-59.dll"));
    proof.utilSha = Hash(ReadAll(runtime / "avutil-vgmstream-57.dll"));
    Require(proof.codecSha == "4D6CF54E7B3C26EF06E95BDB515A10D1E1BED3EC1FFD87EC3E5C4FBCD2A883F6" &&
            proof.utilSha == "A999856C17CFDCE3C22B92B6640A7ADA6C4AB4C3C6841A863770B72FA0BC19B4",
            "runtime_identity", "Decoder libraries differ from the retained fixed r2117 runtime.");
    CheckCancellation(); Api api; api.Load(runtime);
    proof.codecVersion = api.avcodec_version_p(); proof.utilVersion = api.avutil_version_p();
    Require(proof.codecVersion == LIBAVCODEC_VERSION_INT && proof.utilVersion == LIBAVUTIL_VERSION_INT,
            "runtime_version", "Runtime versions differ from the fixed public header ABI profile.");
    const char* config = api.avcodec_configuration_p(); proof.codecConfiguration = config ? config : "";
    DecoderState state(api);
    const AVCodec* codec = api.avcodec_find_decoder_p(AV_CODEC_ID_AAC);
    Require(codec != nullptr, "aac_decoder", "Pinned runtime has no AAC decoder.");
    state.context = api.avcodec_alloc_context3_p(nullptr);
    state.parameters = api.avcodec_parameters_alloc_p();
    state.packet = api.av_packet_alloc_p(); state.frame = api.av_frame_alloc_p();
    Require(state.context && state.parameters && state.packet && state.frame, "decode_allocation", "Could not allocate decoder objects.");
    auto& par = *state.parameters;
    par.codec_type = AVMEDIA_TYPE_AUDIO; par.codec_id = AV_CODEC_ID_AAC; par.sample_rate = 48000;
    par.ch_layout.order = AV_CHANNEL_ORDER_UNSPEC; par.ch_layout.nb_channels = 6;
    // Keep the deprecated representation coherent with this exact 59/57 API, without inventing a mask.
#pragma warning(push)
#pragma warning(disable:4996)
    par.channels = 6; par.channel_layout = 0;
#pragma warning(pop)
    par.extradata = static_cast<uint8_t*>(api.av_mallocz_p(2 + AV_INPUT_BUFFER_PADDING_SIZE));
    Require(par.extradata != nullptr, "extradata_allocation", "Could not allocate padded ASC.");
    par.extradata_size = 2; par.extradata[0] = 0x11; par.extradata[1] = 0xB0;
    CheckAv(api.avcodec_parameters_to_context_p(state.context, state.parameters), "parameters_to_context");
    CheckAv(api.avcodec_open2_p(state.context, codec, nullptr), "aac_open");
    auto receive = [&]() -> int {
        for (;;) {
            CheckCancellation();
            int result = api.avcodec_receive_frame_p(state.context, state.frame);
            if (result == AVERROR(EAGAIN) || result == AVERROR_EOF) return result;
            CheckAv(result, "aac_receive");
            // DecoderState guarantees frame destruction even if observation throws.
            proof.ledger.Observe(*state.frame); api.av_frame_unref_p(state.frame);
        }
    };
    size_t offset = proof.profile.streamOffset;
    for (uint32_t i = 0; i < proof.profile.packetCount; ++i) {
        CheckCancellation(); proof.ledger.packetIndex = i;
        int size = static_cast<int>(proof.profile.sizes[i]);
        CheckAv(api.av_new_packet_p(state.packet, size), "packet_allocation");
        std::memcpy(state.packet->data, bytes.data() + offset, static_cast<size_t>(size));
        for (;;) {
            int result = api.avcodec_send_packet_p(state.context, state.packet);
            if (result != AVERROR(EAGAIN)) { CheckAv(result, "aac_send"); break; }
            uint64_t before = proof.ledger.observedFrames; int received = receive();
            Require(received != AVERROR_EOF && proof.ledger.observedFrames > before, "send_receive_stalled", "Send/receive made no progress.");
        }
        ++proof.completedPackets; offset += static_cast<size_t>(size); api.av_packet_unref_p(state.packet);
        Require(receive() == AVERROR(EAGAIN), "premature_eof", "Decoder reached EOF before explicit flush.");
    }
    proof.ledger.inFlush = true; proof.ledger.packetIndex = proof.profile.packetCount;
    for (;;) {
        CheckCancellation(); int result = api.avcodec_send_packet_p(state.context, nullptr);
        if (result != AVERROR(EAGAIN)) { CheckAv(result, "aac_flush_send"); break; }
        uint64_t before = proof.ledger.observedFrames; int received = receive();
        Require(received != AVERROR_EOF && proof.ledger.observedFrames > before, "flush_stalled", "Flush made no progress.");
    }
    proof.ledger.flushSent = true;
    Require(receive() == AVERROR_EOF, "flush_not_eof", "Decoder did not reach EOF after NULL-packet flush.");
    proof.ledger.eof = true;
    proof.ledger.Complete(proof.profile, proof.expectedPcmFrames, proof.completedPackets);
    CheckCancellation(); proof.attested = true;
}
static AVFrame SyntheticFrame(int order = AV_CHANNEL_ORDER_NATIVE, int channels = 6, uint64_t mask = 0x3F,
                              int rate = 48000, int samples = 1024) {
    AVFrame frame{}; frame.ch_layout.order = static_cast<AVChannelOrder>(order); frame.ch_layout.nb_channels = channels;
    frame.ch_layout.u.mask = mask; frame.sample_rate = rate; frame.nb_samples = samples; frame.format = AV_SAMPLE_FMT_FLTP;
    return frame;
}
static void SelfTest(Proof& proof) {
    auto check = [&](bool value, const char* name) { Require(value, "self_test", name); proof.selfChecks.emplace_back(name); };
    auto rejects = [&](auto action, const char* code, const char* name) {
        bool caught = false;
        try { action(); } catch (const Failure& f) { Require(f.code == code, "self_test_wrong_failure", name); caught = true; }
        check(caught, name);
    };
    check(Hash(std::vector<uint8_t>{'a','b','c'}) == "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", "BCrypt SHA256 known vector");
    Ledger valid; auto frame = SyntheticFrame(); valid.Observe(frame); valid.Observe(frame);
    check(valid.observedFrames == 2 && valid.acceptedFrames == 2 && valid.rawSamples == 2048, "synthetic canonical frames counted");
    Profile profile; profile.packetCount = 2; profile.numSamples = 2048; profile.delay = 16; profile.padding = 32;
    valid.flushSent = true; valid.eof = true; valid.Complete(profile, 2000, 2);
    check(true, "synthetic full completion and trim accounting accepted");
    rejects([&] { Ledger l; auto f = SyntheticFrame(AV_CHANNEL_ORDER_NATIVE,6,0x60F); l.Observe(f); }, "frame_mask", "six-channel SIDE layout rejected");
    rejects([&] { Ledger l; l.Observe(frame); auto f = SyntheticFrame(AV_CHANNEL_ORDER_NATIVE,6,0x60F); l.Observe(f); }, "frame_mask", "later synthetic layout change rejected");
    rejects([&] { Ledger l; auto f = SyntheticFrame(AV_CHANNEL_ORDER_UNSPEC); l.Observe(f); }, "frame_order", "UNSPEC order rejected");
    rejects([&] { Ledger l; auto f = SyntheticFrame(AV_CHANNEL_ORDER_CUSTOM); l.Observe(f); }, "frame_order", "CUSTOM order rejected");
    rejects([&] { Ledger l; auto f = SyntheticFrame(AV_CHANNEL_ORDER_AMBISONIC); l.Observe(f); }, "frame_order", "AMBISONIC order rejected");
    rejects([&] { Ledger l; auto f = SyntheticFrame(AV_CHANNEL_ORDER_NATIVE,5); l.Observe(f); }, "frame_channels", "wrong channel count rejected");
    rejects([&] { Ledger l; auto f = SyntheticFrame(AV_CHANNEL_ORDER_NATIVE,6,0x3F,44100); l.Observe(f); }, "frame_rate", "wrong sample rate rejected");
    rejects([&] { Ledger l; auto f = SyntheticFrame(AV_CHANNEL_ORDER_NATIVE,6,0x3F,48000,0); l.Observe(f); }, "frame_samples", "zero samples rejected");
    rejects([&] { auto l = valid; l.eof = false; l.Complete(profile,2000,2); }, "decode_incomplete", "missing EOF rejected");
    rejects([&] { auto l = valid; l.flushSent = false; l.Complete(profile,2000,2); }, "decode_incomplete", "missing flush rejected");
    rejects([&] { valid.Complete(profile,2000,1); }, "packet_incomplete", "incomplete packet stream rejected");
    rejects([&] { Ledger l; l.eof = true; l.flushSent = true; l.Complete(profile,2000,2); }, "frames_incomplete", "no observed frames cannot attest");
    rejects([&] { auto l = valid; ++l.rawSamples; l.Complete(profile,2000,2); }, "decoded_raw_accounting", "raw sample mismatch rejected");
    rejects([&] { valid.Complete(profile,1999,2); }, "decoded_pcm_accounting", "production PCM count mismatch rejected");
    rejects([&] { CheckCancellation(true); }, "cancelled", "cancellation refuses attestation");
    // Metadata-only synthetic KTAC packets: these bytes are never sent to the AAC decoder.
    std::vector<uint8_t> bytes(0x4A); std::memcpy(bytes.data(),"KTAC",4);
    auto w16 = [&](size_t i, uint16_t v) { bytes[i]=static_cast<uint8_t>(v); bytes[i+1]=static_cast<uint8_t>(v>>8); };
    auto w32 = [&](size_t i, uint32_t v) { for (int j=0;j<4;++j) bytes[i+j]=static_cast<uint8_t>(v>>(j*8)); };
    w32(8,static_cast<uint32_t>(bytes.size())); w32(12,0x48); w32(16,2); w32(20,2); w32(24,48000); w32(28,2048);
    w16(32,6); w16(34,1024); w16(36,16); w16(38,32); w32(56,0x40); w32(60,2); w32(64,1); w32(68,1);
    auto parsed = Parse(bytes); check(parsed.sizes == std::vector<uint32_t>{1,1} && parsed.pcmFrames == 2000, "synthetic KTAC table/accounting parsed without decoding");
    auto bad = bytes; bad[0] = 0;
    rejects([&] { Parse(bad); }, "ktac_magic", "bad magic rejected before runtime loading");
    bad = bytes; bad[8] = 0;
    rejects([&] { Parse(bad); }, "ktac_length", "declared byte length mismatch rejected");
    bad = bytes; bad[20] = 1;
    rejects([&] { Parse(bad); }, "ktac_type_profile", "type 1 profile rejected");
    bad = bytes; bad[32] = 2;
    rejects([&] { Parse(bad); }, "ktac_shape_profile", "non-six-channel profile rejected");
    bad = bytes; bad[64] = 0;
    rejects([&] { Parse(bad); }, "ktac_packet_size", "zero packet length rejected");
    bad = bytes; bad[64] = 3;
    rejects([&] { Parse(bad); }, "ktac_packet_range", "packet payload overrun rejected");
    // Self-tests are deliberately never a decoded-stream attestation.
    check(!proof.attested && proof.inputSha.empty(), "self-test does not claim a real input or stream proof");
}
struct Options {
    bool selfTest = false;
    fs::path input, runtime, output;
    uint64_t expectedPcmFrames = 0;
};
static Options Arguments(int argc, wchar_t** argv) {
    Options result;
    for (int i = 1; i < argc; ++i) {
        std::wstring a = argv[i];
        if (a == L"--self-test") { Require(!result.selfTest, "arguments", "Duplicate self-test flag."); result.selfTest = true; continue; }
        Require(i+1 < argc, "arguments", "A command option is missing its value.");
        std::wstring value = argv[++i];
        if (a == L"--input") { Require(result.input.empty(), "arguments", "Duplicate input option."); result.input = value; }
        else if (a == L"--runtime-dir") { Require(result.runtime.empty(), "arguments", "Duplicate runtime option."); result.runtime = value; }
        else if (a == L"--output") { Require(result.output.empty(), "arguments", "Duplicate output option."); result.output = value; }
        else if (a == L"--expected-pcm-frames") {
            Require(result.expectedPcmFrames == 0 && !value.empty() && value.find_first_not_of(L"0123456789") == std::wstring::npos,
                    "arguments", "Expected PCM frames must be one positive integer.");
            try { result.expectedPcmFrames = std::stoull(value); } catch (...) { throw Failure("arguments", "Expected PCM frame count exceeds uint64."); }
        } else throw Failure("arguments", "Unknown command option.");
    }
    Require(!result.output.empty() && result.output.is_absolute(), "arguments", "An absolute output JSON path is required.");
    if (result.selfTest) Require(result.input.empty() && result.runtime.empty() && result.expectedPcmFrames == 0, "arguments", "Self-test does not accept audio inputs.");
    else Require(result.input.is_absolute() && result.runtime.is_absolute() && result.expectedPcmFrames > 0,
                 "arguments", "Absolute input/runtime paths and expected PCM frames are required.");
    return result;
}
int wmain(int argc, wchar_t** argv) {
    SetConsoleCtrlHandler(OnControl, TRUE);
    Proof proof; Options options; int exitCode = 1;
    try {
        options = Arguments(argc,argv); proof.selfTest = options.selfTest;
        auto helper = ModulePath(); proof.helperPath = Utf8(helper.wstring()); proof.helperSha = Hash(ReadAll(helper));
        if (options.selfTest) SelfTest(proof);
        else {
            auto input = fs::canonical(options.input); proof.inputPath = Utf8(input.wstring());
            auto bytes = ReadAll(input); proof.inputBytes = bytes.size(); proof.inputSha = Hash(bytes); CheckCancellation();
            proof.profile = Parse(bytes); proof.expectedPcmFrames = options.expectedPcmFrames;
            Require(proof.profile.pcmFrames == proof.expectedPcmFrames, "expected_pcm_profile", "Header trimming differs from the supplied production PCM count.");
            Decode(bytes,fs::canonical(options.runtime),proof);
        }
        proof.status = "PASS"; exitCode = 0;
    } catch (const Failure& error) { proof.code = error.code; proof.detail = error.what(); proof.attested = false; exitCode = error.code == "cancelled" ? 2 : 1; }
      catch (const std::exception& error) { proof.code = "operation_error"; proof.detail = error.what(); proof.attested = false; }
    std::string json = proof.Json();
    try {
        if (!options.output.empty()) WriteNew(options.output, json);
        std::cout << json;
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; exitCode = 3; }
    SetConsoleCtrlHandler(OnControl, FALSE); return exitCode;
}
