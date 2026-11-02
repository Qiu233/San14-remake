# Windows x64 Godot 模板

正式导出使用固定提交 `ed1daf0bf001b61586d9930840f2f1394092c079` 的 Godot `4.7.2-stable` .NET 模板，必须同时具有三份补丁。`imported-texture-lifetime.patch` 修正 D3D12 导入资源所有权；`preserve-rd-format.patch` 为显式 RenderingDevice 格式增加可选的默认采样视图保留。后者使画像的原生 sRGB BC3 纹理在 SDR Canvas 中由硬件先解码再过滤，避免手动近似双线性采样。

`Texture2DRD.preserve_format` 默认为 false。现有 `Texture2DRD`、RenderingServer 方法签名与默认行为保持不变；普通图片导入、地图最终 SDR 输出和 LayeredRD 沿原路径。画像在绑定首个 RD RID 前设置此属性，已绑定的 wrapper 不允许改变该语义。现有固定 `4.7.2` GodotSharp 可以通过 `Object.Set("preserve_format", true)` 访问新增属性，不要求替换项目 SDK 或其他既有绑定。

`template.json` 的构建配方版本为 3，按顺序记录三份补丁的身份和共 11 个源文件的 before/after 身份。同一文件可有多个补丁中间态；构建前后只把最后一份补丁声明的输出作为该路径的最终身份。`Build-GodotTemplate.ps1` 允许未修改或完整应用这些补丁的同提交 checkout，拒绝其它 tracked 改动或半应用状态，并在编译后确认这些源文件保持不变。`Get-GodotTemplate.ps1` 在构建阶段验证配方、补丁和四个模板文件；旧配方、旧模板或 stock 模板不能静默回退使用。这些检查不会进入游戏运行时。

源码构建需要本机独立 checkout、SCons、Mesa NIR x64 MSVC 静态库、Microsoft Direct3D Agility SDK `1.618.5`、AccessKit `0.22.3`、Visual Studio 2022 C++ 与 Windows SDK。保持 `template.json` 中原有 release/Mono/D3D12 构建选项。示例路径需要替换为实际本机目录：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File build/Build-GodotTemplate.ps1 `
  -GodotSourceDirectory D:\deps\godot-4.7.2 `
  -PythonPath D:\deps\godot-tools\Scripts\python.exe `
  -MesaDirectory D:\deps\mesa-x86_64-msvc `
  -AgilitySdkDirectory D:\deps\agility_sdk `
  -AccessKitDirectory D:\deps\accesskit -Jobs 8
```

这些目录也可由 `SAN14_GODOT_SOURCE`、`SAN14_GODOT_PYTHON`、`SAN14_GODOT_MESA`、`SAN14_GODOT_AGILITY`、`SAN14_GODOT_ACCESSKIT` 配置。构建脚本只用本机输入，不下载依赖。AccessKit 作为上游默认功能的依赖显式提供，避免重装后因目录缺失而静默关闭该驱动。生成的模板默认安装到当前项目树 `.tools/godot/san14-windows-x64`；用 `-InstallDirectory` 可以选择独立验证目录。

持有同配方已验证模板时，可以只导入而不重新编译：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File build/Build-GodotTemplate.ps1 `
  -ExistingTemplateDirectory D:\deps\verified-san14-template
powershell.exe -NoProfile -ExecutionPolicy Bypass -File build/Get-GodotTemplate.ps1
```

无构建收据时只接受 `template.json` 已验证的精确二进制。源码重建或导入后生成版本 3 的 `template-build.json`，记录同提交、三补丁、原构建选项及实际二进制；Agility DLL 仍必须匹配固定版本。旧收据不适用于新增 BGRA sRGB 视图修复。

`Export-Windows.ps1` 仍通过完整 Core、Data、Native、Client 与 PCK 正常导出，`game/export_presets.cfg` 选择当前树的已验证模板。模板文件缺失或身份不符时在产品构建前失败。两个 Agility DLL 放在导出目录 `x86_64`，并附 Godot 与微软许可；引擎源码、研究资料和构建缓存不进入发布集合。来源与 MIT 许可见根目录 `THIRD_PARTY.md` 和本目录许可文件。

`bgra-srgb-view.patch` 修复 D3D12 原格式表中 BGRA sRGB 的错误 SRV 映射：`B8G8R8A8_TYPELESS` 必须使用同族的 `B8G8R8A8_UNORM_SRGB` 视图。它不转码、不交换源字节、不改 alpha 或采样器，也不改变 `preserve_format` 的默认行为。配方 2 的旧模板保留，但新导出必须通过配方 3 的检查。
