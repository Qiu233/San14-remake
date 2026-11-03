param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin/x64/Release'),
    [string]$MSBuildPath
)

$ErrorActionPreference = 'Stop'
if (-not $MSBuildPath) {
    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($command) { $MSBuildPath = $command.Source }
    else {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
            -find 'MSBuild/Current/Bin/MSBuild.exe' | Select-Object -First 1
    }
}
if (-not $MSBuildPath) { throw 'Visual Studio MSBuild and the C++ x64 tools are required for the resource codec.' }
$output = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
& $MSBuildPath (Join-Path $PSScriptRoot 'San14.Compression.Zlib.vcxproj') /nologo /m:2 /nr:false /v:minimal `
    /p:Configuration=Release /p:Platform=x64 "/p:OutDir=$output" | ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) { throw "Resource codec build failed ($LASTEXITCODE)." }
if (-not (Test-Path -LiteralPath (Join-Path $output 'San14.Compression.Zlib.dll') -PathType Leaf)) {
    throw 'Resource codec build did not produce its library.'
}
