param(
    [string]$InstallDirectory,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$runtimeRoot = Join-Path $projectRoot 'runtime/windows-x64'
$shaderManifestPath = Join-Path $runtimeRoot 'source-provenance.json'
$audioManifestPath = Join-Path $runtimeRoot 'audio/PROVENANCE.json'
foreach ($manifestPath in @($shaderManifestPath, $audioManifestPath)) {
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Runtime provenance is missing: $manifestPath"
    }
}
$shaderManifest = Get-Content -LiteralPath $shaderManifestPath -Raw | ConvertFrom-Json
$audioManifest = Get-Content -LiteralPath $audioManifestPath -Raw | ConvertFrom-Json
if ($shaderManifest.upstream -ne 'Wine vkd3d 2.1' -or
    $shaderManifest.dllSha256 -ne '37885422AD210365C8FFCDA9F40A7F321842139698C6E1E6134DB20BA5D65780') {
    throw 'Shader runtime provenance differs from the pinned vkd3d 2.1 build.'
}
if ($audioManifest.upstream -ne 'https://github.com/vgmstream/vgmstream' -or
    $audioManifest.release -ne 'r2117') {
    throw 'Audio runtime provenance differs from the pinned vgmstream r2117 release.'
}

$audioNames = @(
    'avcodec-vgmstream-59.dll', 'avformat-vgmstream-59.dll', 'avutil-vgmstream-57.dll',
    'libatrac9.dll', 'libcelt-0061.dll', 'libcelt-0110.dll', 'libg719_decode.dll',
    'libmpg123-0.dll', 'libspeex-1.dll', 'libvorbis.dll', 'swresample-vgmstream-4.dll',
    'vgmstream-cli.exe'
)
$manifestNames = @($audioManifest.runtimeFiles | ForEach-Object { $_.name })
if ($manifestNames.Count -ne $audioNames.Count -or
    (Compare-Object ($audioNames | Sort-Object) ($manifestNames | Sort-Object))) {
    throw 'Audio runtime provenance must contain exactly the 12 pinned release binaries.'
}
$binaries = @([pscustomobject]@{
    Path = 'libvkd3d-shader-1.dll'
    Bytes = 7093554
    Sha256 = $shaderManifest.dllSha256
})
foreach ($entry in $audioManifest.runtimeFiles) {
    if ($entry.sha256 -notmatch '^[0-9a-fA-F]{64}$' -or $entry.bytes -le 0) {
        throw "Audio runtime provenance has invalid size or hash: $($entry.name)"
    }
    $binaries += [pscustomobject]@{
        Path = "audio/$($entry.name)"
        Bytes = $entry.bytes
        Sha256 = $entry.sha256
    }
}
$notices = @(
    'source-provenance.json', 'notices/NOTICE.md', 'notices/VKD3D-COPYING.txt',
    'notices/VKD3D-AUTHORS.txt', 'notices/LGPL-2.1.txt',
    'notices/GCC-RUNTIME-COPYRIGHT.txt', 'notices/MINGW-W64-COPYRIGHT.txt',
    'audio/PROVENANCE.json', 'audio/COPYING',
    'audio/licenses/FFmpeg-COPYING.LGPLv2.1', 'audio/licenses/FFmpeg-LICENSE.md',
    'audio/licenses/CODEC-NOTICE.md', 'audio/licenses/LibAtrac9.LICENSE',
    'audio/licenses/celt-0.6.1.COPYING', 'audio/licenses/celt-0.11.0.COPYING',
    'audio/licenses/mpg123-1.31.1.COPYING', 'audio/licenses/speex-1.2.1.COPYING',
    'audio/licenses/libvorbis-1.3.7.COPYING', 'audio/licenses/libogg-1.3.5.COPYING',
    'audio/licenses/opus-1.3.1.COPYING'
)
foreach ($entry in $binaries) {
    $source = Join-Path $runtimeRoot $entry.Path
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Runtime binary is missing: $source. Restore the pinned local dependency described in THIRD_PARTY.md; this source checkout may intentionally omit third-party binaries."
    }
    if ((Get-Item -LiteralPath $source).Length -ne $entry.Bytes -or
        (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.Sha256) {
        throw "Runtime binary size or hash differs from its provenance: $($entry.Path). Restore the pinned local dependency described in THIRD_PARTY.md; a Git LFS pointer is not a runnable binary."
    }
}
foreach ($notice in $notices) {
    if (-not (Test-Path -LiteralPath (Join-Path $runtimeRoot $notice) -PathType Leaf)) {
        throw "Runtime license or provenance is missing: $notice"
    }
}

$destination = $null
if (-not $VerifyOnly) {
    if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
        throw 'Supply -InstallDirectory for the application native/windows-x64 folder, or use -VerifyOnly.'
    }
    $destination = [IO.Path]::GetFullPath($InstallDirectory)
    $runtimeAbsolute = [IO.Path]::GetFullPath($runtimeRoot).TrimEnd('\', '/')
    if ($destination.TrimEnd('\', '/') -eq $runtimeAbsolute -or
        $destination.StartsWith($runtimeAbsolute + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'InstallDirectory must be outside the source runtime folder.'
    }
    foreach ($relativePath in @($binaries.Path) + $notices) {
        $target = Join-Path $destination $relativePath
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $runtimeRoot $relativePath) -Destination $target -Force
    }
    foreach ($entry in $binaries) {
        if ((Get-FileHash -LiteralPath (Join-Path $destination $entry.Path) -Algorithm SHA256).Hash -ne $entry.Sha256) {
            throw "Installed runtime binary differs from its provenance: $($entry.Path)"
        }
    }
}
[pscustomobject]@{
    BinaryCount = $binaries.Count
    BinaryBytes = ($binaries | Measure-Object -Property Bytes -Sum).Sum
    NoticeCount = $notices.Count
    InstalledDirectory = $destination
    VerifiedOnly = [bool]$VerifyOnly
}
