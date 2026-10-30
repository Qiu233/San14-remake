#define NOMINMAX
#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <mferror.h>
#include <propvarutil.h>
#include <wrl/client.h>
#include <algorithm>
#include <cstddef>
#include <cstdlib>
#include <limits>
#include <memory>
#include <new>
#include <vector>
#include "MovieDecoder.h"

using Microsoft::WRL::ComPtr;
static_assert(sizeof(San14MovieInfo) == 56);
static_assert(sizeof(San14MoviePacket) == 88);
namespace {
struct Decoder {
    DWORD owner = GetCurrentThreadId();
    bool com_started = false;
    bool mf_started = false;
    bool ended = false;
    DWORD stream = 0;
    LONG stride = 0;
    San14MovieInfo info{};
    ComPtr<IMFSourceReader> reader;
    std::vector<uint8_t> bytes;
    ~Decoder() {
        reader.Reset();
        if (mf_started) MFShutdown();
        if (com_started) CoUninitialize();
    }

    HRESULT ReadFormat() {
        ComPtr<IMFMediaType> type;
        HRESULT hr = reader->GetCurrentMediaType(stream, &type);
        if (FAILED(hr)) return hr;
        GUID subtype{};
        if (FAILED(hr = type->GetGUID(MF_MT_SUBTYPE, &subtype))) return hr;
        if (info.kind == 1) {
            if (subtype != MFVideoFormat_RGB32) return MF_E_INVALIDMEDIATYPE;
            UINT32 width = 0, height = 0;
            if (FAILED(hr = MFGetAttributeSize(type.Get(), MF_MT_FRAME_SIZE, &width, &height))) return hr;
            if (!width || !height || uint64_t(width) * height * 4 > INT32_MAX) return MF_E_INVALIDMEDIATYPE;
            info.width = static_cast<int32_t>(width);
            info.height = static_cast<int32_t>(height);
            UINT32 numerator = 0, denominator = 0;
            if (SUCCEEDED(MFGetAttributeRatio(type.Get(), MF_MT_FRAME_RATE, &numerator, &denominator)) && denominator) {
                info.frame_rate_numerator = static_cast<int32_t>(numerator);
                info.frame_rate_denominator = static_cast<int32_t>(denominator);
            }
            numerator = denominator = 1;
            MFGetAttributeRatio(type.Get(), MF_MT_PIXEL_ASPECT_RATIO, &numerator, &denominator);
            info.pixel_aspect_numerator = static_cast<int32_t>(numerator);
            info.pixel_aspect_denominator = static_cast<int32_t>(denominator);
            UINT32 raw_stride = 0;
            if (SUCCEEDED(type->GetUINT32(MF_MT_DEFAULT_STRIDE, &raw_stride))) stride = static_cast<LONG>(raw_stride);
            else if (FAILED(hr = MFGetStrideForBitmapInfoHeader(MFVideoFormat_RGB32.Data1, width, &stride))) return hr;
            if (std::abs(int64_t(stride)) < int64_t(width) * 4) return MF_E_INVALIDMEDIATYPE;
        } else {
            if (subtype != MFAudioFormat_PCM) return MF_E_INVALIDMEDIATYPE;
            UINT32 channels = 0, rate = 0, bits = 0, alignment = 0;
            if (FAILED(hr = type->GetUINT32(MF_MT_AUDIO_NUM_CHANNELS, &channels)) ||
                FAILED(hr = type->GetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, &rate)) ||
                FAILED(hr = type->GetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, &bits)) ||
                FAILED(hr = type->GetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, &alignment))) return hr;
            if ((channels != 1 && channels != 2) || !rate || rate > INT32_MAX || bits != 16 || alignment != channels * 2)
                return MF_E_INVALIDMEDIATYPE;
            info.sample_rate = static_cast<int32_t>(rate);
            info.channels = static_cast<int32_t>(channels);
        }
        return S_OK;
    }

    HRESULT Open(const wchar_t* path, int32_t kind) {
        HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        if (FAILED(hr)) return hr;
        com_started = true;
        if (FAILED(hr = MFStartup(MF_VERSION, MFSTARTUP_FULL))) return hr;
        mf_started = true;
        info.kind = kind;
        ComPtr<IMFAttributes> attributes;
        if (FAILED(hr = MFCreateAttributes(&attributes, 1))) return hr;
        if (kind == 1 && FAILED(hr = attributes->SetUINT32(MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING, TRUE))) return hr;
        if (FAILED(hr = MFCreateSourceReaderFromURL(path, attributes.Get(), &reader))) return hr;
        if (FAILED(hr = reader->SetStreamSelection(MF_SOURCE_READER_ALL_STREAMS, FALSE))) return hr;
        stream = kind == 1 ? MF_SOURCE_READER_FIRST_VIDEO_STREAM : MF_SOURCE_READER_FIRST_AUDIO_STREAM;
        ComPtr<IMFMediaType> native;
        hr = reader->GetNativeMediaType(stream, 0, &native);
        if (hr == MF_E_INVALIDSTREAMNUMBER && kind == 2) { ended = true; return S_OK; }
        if (FAILED(hr)) return hr;
        ComPtr<IMFMediaType> requested;
        if (FAILED(hr = MFCreateMediaType(&requested))) return hr;
        if (FAILED(hr = requested->SetGUID(MF_MT_MAJOR_TYPE, kind == 1 ? MFMediaType_Video : MFMediaType_Audio)) ||
            FAILED(hr = requested->SetGUID(MF_MT_SUBTYPE, kind == 1 ? MFVideoFormat_RGB32 : MFAudioFormat_PCM))) return hr;
        if (kind == 2) {
            UINT32 channels = 0, rate = 0;
            if (FAILED(hr = native->GetUINT32(MF_MT_AUDIO_NUM_CHANNELS, &channels)) ||
                FAILED(hr = native->GetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, &rate))) return hr;
            if (!channels || channels > INT32_MAX || !rate) return MF_E_INVALIDMEDIATYPE;
            info.source_channels = static_cast<int32_t>(channels);
            native->GetUINT32(MF_MT_AUDIO_CHANNEL_MASK, &info.source_channel_mask);
            // Windows 8+ Source Reader supports audio conversion. Request a
            // real system stereo fold-down for surround sources; Windows owns
            // the decoder/channel mapping. No channels are dropped here and no
            // invented mixing coefficients enter the game.
            channels = std::min(channels, UINT32(2));
            if (rate > UINT32_MAX / (channels * 2)) return MF_E_INVALIDMEDIATYPE;
            if (FAILED(hr = requested->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, channels)) ||
                FAILED(hr = requested->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, rate)) ||
                FAILED(hr = requested->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16)) ||
                FAILED(hr = requested->SetUINT32(MF_MT_AUDIO_CHANNEL_MASK, channels == 1 ? 4 : 3)) ||
                FAILED(hr = requested->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, channels * 2)) ||
                FAILED(hr = requested->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, rate * channels * 2))) return hr;
        }
        if (FAILED(hr = reader->SetCurrentMediaType(stream, nullptr, requested.Get())) ||
            FAILED(hr = reader->SetStreamSelection(stream, TRUE)) || FAILED(hr = ReadFormat())) return hr;
        info.has_stream = 1;
        PROPVARIANT duration{};
        PropVariantInit(&duration);
        if (SUCCEEDED(reader->GetPresentationAttribute(MF_SOURCE_READER_MEDIASOURCE, MF_PD_DURATION, &duration)) &&
            duration.vt == VT_UI8 && duration.uhVal.QuadPart <= INT64_MAX)
            info.duration_100ns = static_cast<int64_t>(duration.uhVal.QuadPart);
        PropVariantClear(&duration);
        return S_OK;
    }

    HRESULT CopyVideo(IMFSample* sample) {
        ComPtr<IMFMediaBuffer> buffer;
        HRESULT hr = sample->ConvertToContiguousBuffer(&buffer);
        if (FAILED(hr)) return hr;
        ComPtr<IMF2DBuffer> buffer2d;
        buffer.As(&buffer2d);
        BYTE* scanline = nullptr;
        LONG pitch = stride;
        DWORD current_length = 0;
        if (buffer2d) hr = buffer2d->Lock2D(&scanline, &pitch);
        else {
            hr = buffer->Lock(&scanline, nullptr, &current_length);
            if (SUCCEEDED(hr)) {
                uint64_t required = uint64_t(std::abs(int64_t(pitch))) * (info.height - 1) + uint64_t(info.width) * 4;
                if (required > current_length) { buffer->Unlock(); return MF_E_BUFFERTOOSMALL; }
                if (pitch < 0) scanline += std::abs(int64_t(pitch)) * (info.height - 1);
            }
        }
        if (FAILED(hr)) return hr;
        if (std::abs(int64_t(pitch)) < int64_t(info.width) * 4) {
            if (buffer2d) buffer2d->Unlock2D(); else buffer->Unlock();
            return MF_E_INVALIDMEDIATYPE;
        }
        // Reuse the previous frame's allocation, including across padded rows.
        // An allocation failure must still release the media-buffer lock.
        try {
            bytes.resize(size_t(info.width) * info.height * 4);
            for (int y = 0; y < info.height; ++y) {
                const BYTE* input = scanline + ptrdiff_t(y) * pitch;
                BYTE* output = bytes.data() + size_t(y) * info.width * 4;
                for (int x = 0; x < info.width; ++x) {
                    output[x * 4] = input[x * 4 + 2];
                    output[x * 4 + 1] = input[x * 4 + 1];
                    output[x * 4 + 2] = input[x * 4];
                    output[x * 4 + 3] = 255; // RGB32's fourth byte is unspecified.
                }
            }
        } catch (...) {
            if (buffer2d) buffer2d->Unlock2D(); else buffer->Unlock();
            throw;
        }
        return buffer2d ? buffer2d->Unlock2D() : buffer->Unlock();
    }

    HRESULT CopyAudio(IMFSample* sample) {
        ComPtr<IMFMediaBuffer> buffer;
        HRESULT hr = sample->ConvertToContiguousBuffer(&buffer);
        if (FAILED(hr)) return hr;
        BYTE* data = nullptr;
        DWORD count = 0;
        if (FAILED(hr = buffer->Lock(&data, nullptr, &count))) return hr;
        if (count > INT32_MAX || count % (info.channels * 2)) { buffer->Unlock(); return MF_E_INVALIDMEDIATYPE; }
        try { bytes.assign(data, data + count); }
        catch (...) { buffer->Unlock(); throw; }
        return buffer->Unlock();
    }

    HRESULT Read(San14MoviePacket& output) {
        if (ended) return S_FALSE;
        for (;;) {
            DWORD flags = 0;
            LONGLONG timestamp = 0;
            ComPtr<IMFSample> sample;
            HRESULT hr = reader->ReadSample(stream, 0, nullptr, &flags, &timestamp, &sample);
            if (FAILED(hr)) return hr;
            if (flags & MF_SOURCE_READERF_ERROR) return E_FAIL;
            if (flags & MF_SOURCE_READERF_CURRENTMEDIATYPECHANGED) {
                if (FAILED(hr = ReadFormat())) return hr;
            }
            if (flags & MF_SOURCE_READERF_ENDOFSTREAM) ended = true;
            if (!sample) { if (ended) return S_FALSE; else continue; }
            if (FAILED(hr = info.kind == 1 ? CopyVideo(sample.Get()) : CopyAudio(sample.Get()))) return hr;
            LONGLONG duration = 0;
            if (FAILED(sample->GetSampleDuration(&duration))) {
                if (info.kind == 2) duration = static_cast<LONGLONG>(bytes.size() / (info.channels * 2)) * 10000000 / info.sample_rate;
                else if (info.frame_rate_numerator > 0 && info.frame_rate_denominator > 0) {
                    UINT64 average = 0;
                    if (SUCCEEDED(MFFrameRateToAverageTimePerFrame(info.frame_rate_numerator,
                        info.frame_rate_denominator, &average)) && average <= INT64_MAX)
                        duration = static_cast<LONGLONG>(average);
                }
            }
            output.data = bytes.data();
            output.bytes = static_cast<int32_t>(bytes.size());
            output.timestamp_100ns = timestamp;
            output.duration_100ns = duration;
            output.format = info;
            return S_OK;
        }
    }
};
}

int32_t __cdecl san14_movie_open_v1(const wchar_t* path, int32_t kind, void** handle, San14MovieInfo* info) noexcept {
    if (!handle || !info) return E_POINTER;
    *handle = nullptr;
    *info = {};
    if (!path || !*path || (kind != 1 && kind != 2)) return E_INVALIDARG;
    try {
        auto decoder = std::make_unique<Decoder>();
        HRESULT hr = decoder->Open(path, kind);
        if (FAILED(hr)) return hr;
        *info = decoder->info;
        *handle = decoder.release();
        return S_OK;
    } catch (const std::bad_alloc&) { return E_OUTOFMEMORY; }
    catch (...) { return E_FAIL; }
}

int32_t __cdecl san14_movie_read_v1(void* handle, San14MoviePacket* packet) noexcept {
    if (!handle || !packet) return E_POINTER;
    *packet = {};
    auto decoder = static_cast<Decoder*>(handle);
    if (decoder->owner != GetCurrentThreadId()) return RPC_E_WRONG_THREAD;
    try { return decoder->Read(*packet); }
    catch (const std::bad_alloc&) { return E_OUTOFMEMORY; }
    catch (...) { return E_FAIL; }
}

int32_t __cdecl san14_movie_close_v1(void* handle) noexcept {
    if (!handle) return S_OK;
    auto decoder = static_cast<Decoder*>(handle);
    if (decoder->owner != GetCurrentThreadId()) return RPC_E_WRONG_THREAD;
    delete decoder;
    return S_OK;
}
