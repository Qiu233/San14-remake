[CmdletBinding()]
param([string]$MSBuildPath, [string]$InstallDirectory)
$ErrorActionPreference = 'Stop'
if (-not $MSBuildPath) {
    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($command) { $MSBuildPath = $command.Source }
    else {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'MSBuild/Current/Bin/MSBuild.exe' | Select-Object -First 1
    }
}
if (-not $MSBuildPath) { throw 'Visual Studio C++ x64 tools and Windows SDK are required.' }
& $MSBuildPath (Join-Path $PSScriptRoot 'San14.Audio.Decoder.vcxproj') /nologo /m:1 /v:minimal /p:Configuration=Release /p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Audio decoder build failed ($LASTEXITCODE)." }
if ($InstallDirectory) {
    $destination = [IO.Path]::GetFullPath($InstallDirectory)
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin/x64/Release/San14.Audio.Decoder.exe') -Destination $destination -Force
    $notices = Join-Path $destination 'licenses/San14.Audio.Decoder'
    New-Item -ItemType Directory -Path $notices -Force | Out-Null
    foreach ($name in @('NOTICE.md', 'COPYING.LGPLv2.1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot "../third_party/ffmpeg/$name") -Destination $notices -Force
    }
}
