#pragma once
#include <stdint.h>

// Every handle, including its close, belongs to the thread that opened it.
// Times are signed 100 ns Media Foundation presentation times. No clock or
// audio/video output device is created by this library.
struct San14MovieInfo {
    int32_t has_stream;
    int32_t kind; // 1 = video RGBA32; 2 = audio interleaved PCM16
    int32_t width;
    int32_t height;
    int32_t frame_rate_numerator;
    int32_t frame_rate_denominator;
    int32_t pixel_aspect_numerator;
    int32_t pixel_aspect_denominator;
    int32_t sample_rate;
    int32_t channels;
    int64_t duration_100ns; // 0 if the source does not publish a duration
    int32_t source_channels; // before the system's negotiated stereo fold-down
    uint32_t source_channel_mask; // 0 if the source does not publish its mask
};

struct San14MoviePacket {
    const uint8_t* data; // valid only until the next read or close on this handle
    int32_t bytes;
    int32_t reserved;
    int64_t timestamp_100ns;
    int64_t duration_100ns;
    San14MovieInfo format;
};

#ifdef SAN14_MEDIA_DECODER_BUILD
#define SAN14_MOVIE_API extern "C" __declspec(dllexport)
#else
#define SAN14_MOVIE_API extern "C" __declspec(dllimport)
#endif
SAN14_MOVIE_API int32_t __cdecl san14_movie_open_v1(const wchar_t* path, int32_t kind,
    void** handle, San14MovieInfo* info) noexcept;
// S_OK = one packet; S_FALSE = end of stream; failed HRESULT = decoder error.
SAN14_MOVIE_API int32_t __cdecl san14_movie_read_v1(void* handle, San14MoviePacket* packet) noexcept;
SAN14_MOVIE_API int32_t __cdecl san14_movie_close_v1(void* handle) noexcept;
