# vkd3d-shader 2.1 Windows x64

This package uses the vkd3d-shader library, copyright the vkd3d project authors, under LGPL-2.1-or-later. The original copyright notice and author list are reproduced in `VKD3D-COPYING.txt` and `VKD3D-AUTHORS.txt`; the complete license is in `LGPL-2.1.txt`.

Official source release: https://dl.winehq.org/vkd3d/source/vkd3d-2.1.tar.xz

The retained source archive SHA256 is `7510146AFF2ADFB4AE07AB890701A607E5FF7C66E57100CFC9F630EC92EEDA6A`. The reviewed Windows DLL SHA256 is `37885422AD210365C8FFCDA9F40A7F321842139698C6E1E6134DB20BA5D65780`.

The Windows library was built from that unmodified release with the Debian MinGW-w64 x86-64 POSIX C toolchain, with static libgcc. The supplied `GCC-RUNTIME-COPYRIGHT.txt` includes the GCC Runtime Library Exception and licenses; `MINGW-W64-COPYRIGHT.txt` preserves the MinGW-w64 copyright and per-file license information. These are complete toolchain runtime notices, not a claim that every file listed in those packages is linked into the DLL.

The source release URL, source archive SHA256, source-file hashes and retained build-record hashes are documented in the adjacent `source-provenance.json`. The source archive and build records are development inputs; the application does not open them. The application calls the library through its exported C API and uses the matching library free functions for returned allocations.

This notice covers the shader translation library and its toolchain runtime components. It does not assign rights to any original game resources.
