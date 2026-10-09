# 第三方来源与运行依赖

正式 Windows 导出使用 `runtime/windows-x64` 中已有、版本固定的第三方运行依赖。它们包含13个二进制：一个 vkd3d-shader DLL，以及 vgmstream CLI 和11个配套音频 DLL。首次源码集合纳入vkd3d-shader DLL（Git LFS）、来源、哈希与已取得的许可文本；12个音频二进制暂留本地，待下述G.719依赖的许可边界明确后再决定发布。`build/Install-WindowsRuntime.ps1`按清单核对本地依赖的尺寸和SHA-256，再复制到程序旁的 `native/windows-x64`；`build/Export-Windows.ps1`调用此安装步骤并随包复制本文件。首个源码clone缺少音频二进制时会明确报错，安装脚本不下载二进制。

## vkd3d-shader 2.1

旧着色器后端使用 `libvkd3d-shader-1.dll` 将本机原资源中的 DXBC 转换为 SPIR-V。当前原生 D3D11 主地图直接使用 DXBC。DLL来自未修改的 [Wine vkd3d 2.1源码发布包](https://dl.winehq.org/vkd3d/source/vkd3d-2.1.tar.xz)，采用 LGPL-2.1-or-later；项目保存的 DLL SHA-256 为 `37885422AD210365C8FFCDA9F40A7F321842139698C6E1E6134DB20BA5D65780`。源码包哈希和相关源码文件哈希保存在 `runtime/windows-x64/source-provenance.json`。

原版权／作者名单、LGPL全文及构建工具链的运行库声明保存在 `runtime/windows-x64/notices`；导出后位于 `native/windows-x64/notices`。其中完整 GCC／MinGW声明是保留的工具链许可材料，不表示其列出的每个组件都链接进此 DLL。此库由 Debian MinGW-w64 x86-64 POSIX C 工具链构建，使用 static libgcc；本项目经其公开 C API调用，并使用匹配的库释放函数。

## vgmstream r2117 音频运行依赖

普通界面音频使用 [vgmstream r2117 Windows x64发布包](https://github.com/vgmstream/vgmstream/releases/download/r2117/vgmstream-win64.zip) 的 `vgmstream-cli.exe` 和11个配套 DLL。版本、上游 source commit、发布包哈希及每个二进制的尺寸／SHA-256保存在 `runtime/windows-x64/audio/PROVENANCE.json`；导出后位于 `native/windows-x64/audio/PROVENANCE.json`。客户端从该清单验证完整运行依赖，媒体仅从用户自己的原版安装读取和临时解码。

原发布包的vgmstream版权许可保存在 `audio/COPYING`；四个FFmpeg DLL实际报告的LGPL-2.1-or-later许可及编译选项记录在同一provenance，FFmpeg许可全文在 `audio/licenses`。其余codec的许可材料按固定r2117上游来源逐项记录；这些source许可材料不代表已核对每个DLL的完整源码revision。

固定r2117源码的 [构建说明](https://github.com/vgmstream/vgmstream/blob/71e2361042531fe767fb98300cf8c1ee95e539a0/doc/BUILD.md) 将 `libg719_decode` 的许可标为未知，并引用 [kode54/libg719_decode](https://github.com/kode54/libg719_decode/tree/da90ad8a676876c6c47889bcea6a753f9bbf7a73)。CLI直接导入该DLL，因此当前12个音频二进制整体暂不纳首次源码提交或公开分发。已确认的codec许可全文及固定上游映射保存在 `runtime/windows-x64/audio/licenses`，详情见该目录 `CODEC-NOTICE.md`。本地导出验证也不构成完整许可证审计。

开发源码使用 `runtime/windows-x64`，导出程序使用相邻 `native/windows-x64`。也可通过 `SAN14_AUDIO_DECODER_MANIFEST`指定外置音频 runtime清单。正式导出不要求私有 `tools/audio`、原版转换缓存或离线资源工具。

## 格式研究参考

本项目独立实现了 RDB 索引、G1T 纹理等读取代码，参考了 [Cethleann](https://github.com/neptuwunium/Cethleann) 对相关格式的公开研究及其《三国志14》文件名表。Cethleann 版权所有 © 2019 HealingBrew，采用 MIT License：

> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

原版游戏文件由本机导入，仅用于与本机安装版本对照；提取资源和生成文件不纳入源码目录。
