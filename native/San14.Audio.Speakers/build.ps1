[CmdletBinding()]
param([string]$MSBuildPath, [string]$InstallDirectory)
$ErrorActionPreference = 'Stop'
if (-not $MSBuildPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'MSBuild/Current/Bin/MSBuild.exe' | Select-Object -First 1
}
if (-not $MSBuildPath) { throw 'Visual Studio C++ x64 tools and Windows SDK are required.' }
& $MSBuildPath (Join-Path $PSScriptRoot 'San14.Audio.Speakers.vcxproj') /nologo /m:1 /v:minimal /p:Configuration=Release /p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Speaker bridge build failed ($LASTEXITCODE)." }
if ($InstallDirectory) {
    $destination = [IO.Path]::GetFullPath($InstallDirectory)
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($name in @('San14.Audio.Speakers.dll','San14.Audio.Speakers.gdextension')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot "bin/x64/Release/$name") -Destination $destination -Force
    }
    $notices = Join-Path $destination 'licenses/San14.Audio.Speakers'
    New-Item -ItemType Directory -Path $notices -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'NOTICE.md') -Destination $notices -Force
}
