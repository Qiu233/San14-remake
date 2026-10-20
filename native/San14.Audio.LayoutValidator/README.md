# San14.Audio.LayoutValidator

A Windows x64 auxiliary tool that verifies the actual channel layout of every decoded frame in one supported KTAC stream. It discards decoded samples. It does not write PCM, reorder channels, change a WAV channel mask, or play audio.

## Build

Install Visual Studio 2022 with the v143 C++ x64 tools and a Windows SDK. The project follows the native MSBuild structure used by the renderer. It is independent of the C# product projects. Its `Directory.Build.props` and `Directory.Build.targets` prevent unrelated parent build settings from being imported.

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File native/San14.Audio.LayoutValidator/build.ps1 -OutputDirectory .artifacts/audio-layout-validator
```

The script locates MSBuild using the existing command or Visual Studio Installer's `vswhere`. An explicit `-MSBuildPath` is also accepted. It verifies the 28 vendored public-header identities and builds Release/x64, C++20, with `/W4 /WX` for the project source. Unmodified third-party headers use `/external:W0`. The build is offline and does not acquire dependencies or install files.

The output directory contains `San14.Audio.LayoutValidator.exe`, its PDB, `layout-validator.json`, and `licenses/San14.Audio.LayoutValidator/{NOTICE.md,COPYING.LGPLv2.1}`. MSBuild intermediates remain under `obj`. The EXE, auxiliary manifest and notices are the runtime deployment files; the PDB is an optional debug artifact. Do not package the intermediate directory.

The generated auxiliary manifest has exactly this version-1 shape, with the actual newly built EXE hash:

```json
{
  "format": "san14-ktac-layout-validator",
  "version": 1,
  "executable": "San14.Audio.LayoutValidator.exe",
  "sha256": "<actual SHA256>"
}
```

The helper and its manifest may be placed alongside an existing audio runtime's `PROVENANCE.json`. They have their own identity; they are not another official upstream runtime entry. This project never edits that manifest or copies/replaces its twelve runtime files. The build script only writes its own output directory. C# deployment and caller integration are separate.

## Runtime input

Use absolute paths for the input, runtime directory and a new output JSON file:

```text
San14.Audio.LayoutValidator.exe --input <absolute selected.ktac> --runtime-dir <absolute audio runtime directory> --expected-pcm-frames <positive final frame count> --output <absolute new proof.json>
```

The helper loads only the existing `avcodec-vgmstream-59.dll` and `avutil-vgmstream-57.dll` with explicit absolute paths and resolves their public APIs. It requires the pinned fixed runtime identities:

| Library | SHA256 | Queried API version |
|---|---|---|
| avcodec-vgmstream-59.dll | 4D6CF54E7B3C26EF06E95BDB515A10D1E1BED3EC1FFD87EC3E5C4FBCD2A883F6 | 59.37.100 |
| avutil-vgmstream-57.dll | A999856C17CFDCE3C22B92B6640A7ADA6C4AB4C3C6841A863770B72FA0BC19B4 | 57.28.100 |

No FFmpeg import library is needed. Windows BCrypt supplies SHA256, and the executable uses the MSVC runtime. The supported profile is KTAC type 0 or 2, six channels, 48000 Hz, 1024 samples per frame, AAC LC ASC `11B0`, with the strict table, payload and trimming accounting expressed by the source. Other valid KTAC formats can be unsupported by this deliberately finite tool.

## Result contract

The helper reads and hashes its actual input bytes, then supplies the same immutable buffer to the decoder. It does not accept a caller's input hash as proof. Every returned AVFrame must have native order, six channels, mask `0x3F`, 48000 Hz and positive samples. Same-count side layouts and unspecified layouts fail. Every packet must complete; the decoder must flush to EOF; actual raw and trimmed sample counts must agree with the header and the expected production PCM count. No result is inferred from an initial container layout or channel count alone.

Consume a proof only after process exit zero and a complete JSON with `mode="whole-stream-ktac-layout"`, `result="PASS"`, and `canonicalLayoutAttested=true`. Match its actual input, helper and runtime identities and final accounting to the decoded object. A failure, timeout, cancellation, incomplete JSON, or failed output write makes the layout unavailable. Existing output files are never overwritten. The caller owns temporary input/output lifetime and process timeout/cancellation. Ctrl-C/Break also requests cooperative cancellation between packets and frames.

The proof is an independent observation using the same raw packets, ASC and fixed decoder/default policy. It is not an observation inside another CLI process. It provides lane order `FL FR FC LFE BL BR` for its successfully observed exact input. Keep a classic WAV's missing channel mask as a separate unchanged fact.

An optional `--self-test --output <absolute new result.json>` checks synthetic frame predicates, metadata and SHA256 without loading FFmpeg or decoding media. Its result always has `canonicalLayoutAttested=false`; it is not a real-stream layout proof.

## Dependency and source boundaries

`third_party/ffmpeg` contains only the 28 fixed public headers, their origin lock and the preserved license/notice. No original game resource, executable, disassembly, development observation, resource-extraction program or recorded stream proof belongs to this source project. The single translation unit keeps packet/profile validation, decoder lifetime, layout/accounting checks and JSON/CLI boundaries as separate functions and types for this finite executable.
