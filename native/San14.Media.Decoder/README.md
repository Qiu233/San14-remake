# Windows movie decode backend

This project builds `San14.Media.Decoder.dll`, an x64 Windows Media Foundation
decoder. It reads the caller's original movie resource through independent video
and audio Source Readers. It does not open a window, create an audio device,
execute original game code, or transcode a movie into a cache. Dependencies are
Windows system libraries and the normal Visual C++ runtime only.

Build with `build.ps1`. `-InstallDirectory` can place the DLL in the application's
`native/windows-x64` directory. The managed caller supplies the absolute movie
path and DLL path to `WindowsMovieDecoder.OpenAsync`; no CWD search is performed.

Each native handle must be opened, read and closed on the same thread. COM and
Media Foundation startup/shutdown are balanced there. Read packet memory belongs
to that handle until its next read or close. The managed wrapper copies packets
before issuing another read, using three-video/eight-audio bounded queues. It
cancels bounded writes, joins both actual reader threads and then unloads the DLL.
A synchronous local-file read is allowed to finish before its owner closes it.
`MovieVideoFrame` owns an exact-size reusable RGBA buffer. The consumer disposes
each frame after uploading/copying it, including pending frames discarded on skip.
Its pixel array must not be retained after disposal. At most five returned buffers
are retained by the session; teardown disposes queued frames and releases the pool.

Video output is top-down, tightly packed RGBA32 with opaque alpha. Source stride,
including negative bottom-up stride, is handled while copying. Audio is decoded
at the source sample rate to signed PCM16, then exposed as interleaved floats.
Mono and stereo layouts are retained. For surround sources, the Windows Source
Reader negotiates a real stereo fold-down; Windows owns its codec/converter and
channel mapping. The backend does not discard channels or invent a mixing matrix.
The original channel count and published channel mask are retained in AudioInfo.
Unsupported system conversion remains an explicit error. A file without audio
completes its audio stream normally.
Actual timestamps and sample durations use 100 ns units. A missing video sample
duration uses the negotiated frame rate; missing audio duration uses its actual
sample count. Unknown duration remains zero. Format changes appear in each packet.
The caller owns timestamp scheduling, audio/video synchronization, pause/skip and
presentation acceptance. Decoder EOS means all decoded queued data was consumed;
it is not a display or audible-completion receipt.

Windows Media Foundation must be available (Windows N installations may need the
system Media Feature Pack). Decoder failures remain explicit completion errors.

Implementation references: [Source Reader configuration](https://learn.microsoft.com/en-us/windows/win32/medfound/processing-media-data-with-the-source-reader),
[uncompressed buffer stride](https://learn.microsoft.com/en-us/windows/win32/medfound/uncompressed-video-buffers),
and [IMF2DBuffer](https://learn.microsoft.com/en-us/windows/win32/api/mfobjects/nn-mfobjects-imf2dbuffer).
Audio conversion is part of [Windows 8+ Source Reader media-type negotiation](https://learn.microsoft.com/en-us/windows/win32/api/mfreadwrite/nf-mfreadwrite-imfsourcereader-setcurrentmediatype).
