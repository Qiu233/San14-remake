# Normal multichannel KTAC decoder

Build the Windows x64 helper with `powershell -NoProfile -File native/San14.Audio.Decoder/build.ps1`. The resulting executable is `bin/x64/Release/San14.Audio.Decoder.exe`. It uses the existing declared audio runtime's FFmpeg libraries through their vendored public 59.37.100 / 57.28.100 ABI. The build never fetches a dependency.

The application invokes it once for six-channel KTAC, supplying `--input`, `--output`, and `--runtime-dir`. AAC packet decoding, encoder-delay and padding removal, PCM16 conversion and interleaving happen in that one decode. The output is WAVEFORMATEXTENSIBLE: its speaker mask comes from the actual decoded AVFrame channel layout, in native ascending speaker-bit order. Unspecified layouts and layout changes within a resource are explicit format errors. A six-channel count or AAC initial configuration is never treated as a speaker mask. Other currently supported audio containers continue through the normal vgmstream CLI.

Deploy `San14.Audio.Decoder.exe` beside the normal `vgmstream-cli.exe`, FFmpeg DLLs and runtime provenance. Ship the FFmpeg notices and LGPL license from `native/third_party/ffmpeg` as `licenses/San14.Audio.Decoder`. The source checkout can directly use its standard native build output; an exported game uses the executable beside the normal installed audio runtime. `SAN14_MULTICHANNEL_AUDIO_DECODER` is an explicit executable override.

No original executable, private layout validator, layout proof manifest, second audio decode, output hash or sample comparison is required. Resource size, packet bounds, declared sample format, trimming and actual speaker-layout checks remain part of normal decoding.
