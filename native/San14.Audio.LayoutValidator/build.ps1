[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin/x64/Release'),
    [string]$MSBuildPath
)

$ErrorActionPreference = 'Stop'

# These are vendored public headers: a build never fetches or updates dependencies.
$headerRoot = Join-Path $PSScriptRoot 'third_party/ffmpeg/include'
$headerLock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'third_party/ffmpeg/headers.lock.json') -Raw | ConvertFrom-Json
if ($headerLock.format -ne 'san14-ffmpeg-public-headers' -or $headerLock.version -ne 1 -or
    $headerLock.count -ne 28 -or $headerLock.bytes -ne 440254 -or @($headerLock.headers).Count -ne 28) {
    throw 'The fixed FFmpeg public-header identity is not supported.'
}
foreach ($header in $headerLock.headers) {
    $file = Get-Item -LiteralPath (Join-Path $headerRoot $header.path)
    if ($file.Length -ne $header.bytes -or (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $header.sha256) {
        throw "The fixed FFmpeg public header changed: $($header.path)"
    }
}

if (-not $MSBuildPath) {
    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($command) { $MSBuildPath = $command.Source }
    else {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
            -find 'MSBuild/Current/Bin/MSBuild.exe' | Select-Object -First 1
    }
}
if (-not $MSBuildPath) { throw 'Visual Studio MSBuild, the v143 C++ x64 tools and a Windows SDK are required.' }

$output = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $MSBuildPath (Join-Path $PSScriptRoot 'San14.Audio.LayoutValidator.vcxproj') /nologo /m:1 /v:minimal `
    /p:Configuration=Release /p:Platform=x64 "/p:OutDir=$output" "/p:IntDir=${output}obj\"
if ($LASTEXITCODE -ne 0) { throw "Layout validator build failed ($LASTEXITCODE)." }

$executableName = 'San14.Audio.LayoutValidator.exe'
$executable = Join-Path $output $executableName
$manifest = [ordered]@{
    format = 'san14-ktac-layout-validator'
    version = 1
    executable = $executableName
    sha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
}
$json = ($manifest | ConvertTo-Json) + [Environment]::NewLine
[IO.File]::WriteAllText((Join-Path $output 'layout-validator.json'), $json, [Text.UTF8Encoding]::new($false))

$licenseOutput = Join-Path $output 'licenses/San14.Audio.LayoutValidator'
New-Item -ItemType Directory -Path $licenseOutput -Force | Out-Null
foreach ($name in @('NOTICE.md', 'COPYING.LGPLv2.1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "third_party/ffmpeg/$name") -Destination (Join-Path $licenseOutput $name) -Force
}

[pscustomobject]@{
    Executable = $executable
    Manifest = (Join-Path $output 'layout-validator.json')
    SHA256 = $manifest.sha256
    Configuration = 'Release'
    Platform = 'x64'
    VerifiedPublicHeaders = 28
} | ConvertTo-Json
