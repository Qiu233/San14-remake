# FFmpeg public-header notice

San14.Audio.Decoder uses FFmpeg through its public headers and dynamically loaded libraries. FFmpeg is licensed under the GNU Lesser General Public License version 2.1 or later. A verbatim copy of LGPL 2.1 accompanies this notice as `COPYING.LGPLv2.1`. All original copyright and license notices in the supplied headers are preserved.

The 28 unmodified public headers in `include` are taken from the vgmstream repository at commit `71e2361042531fe767fb98300cf8c1ee95e539a0`, directory `ext_includes/ffmpeg`:

https://github.com/vgmstream/vgmstream/tree/71e2361042531fe767fb98300cf8c1ee95e539a0/ext_includes/ffmpeg

They declare libavcodec 59.37.100 and libavutil 57.28.100. Their per-file source URLs, lengths and SHA256 identities are supplied in `headers.lock.json` in the source project. Twenty-seven headers contain the LGPL 2.1-or-later notice; `libavutil/avconfig.h` is the unmodified generated configuration header distributed with that header set. No additional license grant is inferred from its generated status.

The corresponding declared upstream FFmpeg source is n5.1.2, commit `eacfcbae690f914a4b1b4ad06999f138540cc3d8`:

https://github.com/FFmpeg/FFmpeg/tree/eacfcbae690f914a4b1b4ad06999f138540cc3d8

The retained license text is identical to:

https://github.com/FFmpeg/FFmpeg/blob/eacfcbae690f914a4b1b4ad06999f138540cc3d8/COPYING.LGPLv2.1

This source project supplies only the public-header dependency. It does not build or install a new FFmpeg runtime or the vgmstream CLI. The runtime's binary provenance and accompanying FFmpeg licenses remain separate.
