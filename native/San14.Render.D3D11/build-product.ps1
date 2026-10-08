param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin/x64/Release'),
    [string]$MSBuildPath,
    [string]$InstallDirectory
)

$ErrorActionPreference = 'Stop'
if (-not $MSBuildPath) {
    $msbuildCommand = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($msbuildCommand) {
        $MSBuildPath = $msbuildCommand.Source
    }
    else {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
            -find 'MSBuild/Current/Bin/MSBuild.exe' | Select-Object -First 1
    }
}
if (-not $MSBuildPath) { throw 'Visual Studio MSBuild and the C++ x64 tools are required.' }

$output = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
& $MSBuildPath (Join-Path $PSScriptRoot 'San14.Render.D3D11.vcxproj') /nologo /m /v:minimal `
    /p:Configuration=Release /p:Platform=x64 "/p:OutDir=$output" | ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) { throw "Native renderer build failed ($LASTEXITCODE)." }

$library = Join-Path $output 'San14.Render.D3D11.dll'
$installedLibrary = $null
if ($InstallDirectory) {
    $installedLibrary = Join-Path ([IO.Path]::GetFullPath($InstallDirectory)) 'San14.Render.D3D11.dll'
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($installedLibrary)) -Force | Out-Null
    Copy-Item -LiteralPath $library -Destination $installedLibrary -Force
}

[pscustomobject]@{
    Library = $library
    InstalledLibrary = $installedLibrary
    Configuration = 'Release'
    Platform = 'x64'
}
