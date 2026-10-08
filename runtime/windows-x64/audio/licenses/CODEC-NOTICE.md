# vgmstream r2117 codec source notices

These notices accompany the pinned local vgmstream r2117 Windows x64 runtime. The primary source is the vgmstream commit `71e2361042531fe767fb98300cf8c1ee95e539a0`: its [build documentation](https://github.com/vgmstream/vgmstream/blob/71e2361042531fe767fb98300cf8c1ee95e539a0/doc/BUILD.md) and [retained license directory](https://github.com/vgmstream/vgmstream/tree/71e2361042531fe767fb98300cf8c1ee95e539a0/ext_libs/licenses).

The following copyright and license texts are preserved verbatim from that commit. The versions below identify its documented dependency sources and notice artifacts; they do not assert a separately verified source revision for each DLL.

| Local runtime component | Preserved notice | Primary dependency source |
|---|---|---|
| `libatrac9.dll` | `LibAtrac9.LICENSE` (MIT) | [LibAtrac9, documented commit](https://github.com/Thealexbarney/LibAtrac9/tree/6a9e00f6c7abd74d037fd210b6670d3cdb313049) |
| `libcelt-0061.dll` | `celt-0.6.1.COPYING` (BSD 3-clause) | [CELT 0.6.1](https://downloads.xiph.org/releases/celt/celt-0.6.1.tar.gz) |
| `libcelt-0110.dll` | `celt-0.11.0.COPYING` (BSD 3-clause) | [CELT 0.11.0](https://downloads.xiph.org/releases/celt/celt-0.11.0.tar.gz) |
| `libmpg123-0.dll` | `mpg123-1.31.1.COPYING` (LGPL 2.1) | [mpg123 source archive documented by upstream](https://sourceforge.net/projects/mpg123/files/mpg123/1.31.1/mpg123-1.31.1.tar.bz2) |
| `libspeex-1.dll` | `speex-1.2.1.COPYING` (BSD 3-clause variant) | [Speex 1.2.1](https://downloads.xiph.org/releases/speex/speex-1.2.1.tar.gz) |
| `libvorbis.dll` includes Vorbis/Vorbisfile and Ogg | `libvorbis-1.3.7.COPYING`, `libogg-1.3.5.COPYING` (BSD 3-clause) | [Vorbis 1.3.7](https://downloads.xiph.org/releases/vorbis/libvorbis-1.3.7.zip), [Ogg 1.3.5](https://downloads.xiph.org/releases/ogg/libogg-1.3.5.zip) |
| Four FFmpeg DLLs include Opus | `FFmpeg-COPYING.LGPLv2.1`, `FFmpeg-LICENSE.md`, `opus-1.3.1.COPYING` | [FFmpeg n5.1.2 documented by upstream](https://github.com/FFmpeg/FFmpeg/tree/n5.1.2), [Opus 1.3.1](https://github.com/xiph/opus/tree/v1.3.1) |

For CELT, the retained COPYING texts have three BSD conditions; those texts are used here rather than the build document's MIT shorthand. The four FFmpeg binaries' actual reported license and configuration, including `--enable-libopus`, remain in `../PROVENANCE.json`.

## G.719 boundary

The same upstream build document labels `libg719_decode.dll` as having an unknown license, and points to [kode54/libg719_decode at da90ad8a676876c6c47889bcea6a753f9bbf7a73](https://github.com/kode54/libg719_decode/tree/da90ad8a676876c6c47889bcea6a753f9bbf7a73). That source has reference-code copyright headers and does not supply a LICENSE/COPYING file establishing redistribution terms.

The local CLI imports this DLL directly. All 12 audio binaries remain outside the first source snapshot until this dependency's redistribution boundary is resolved. Retaining the above notices or verifying a local installation does not resolve that boundary.
