param(
    [string]$GodotConsole,
    [string]$BuildDirectory,
    [string]$MSBuildPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $GodotConsole) {
    $GodotConsole = Join-Path $projectRoot '.tools/godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
}
if (-not (Test-Path -LiteralPath $GodotConsole -PathType Leaf)) {
    throw "Godot 4.7.2 .NET console executable is missing: $GodotConsole. Supply -GodotConsole with your installed Godot .NET console executable; install its matching Windows x64 .NET export templates."
}
$GodotConsole = (Resolve-Path -LiteralPath $GodotConsole).Path
if (-not $BuildDirectory) { $BuildDirectory = Join-Path $projectRoot '.local/build-windows' }
$buildFolder = [IO.Path]::GetFullPath($BuildDirectory)
$exportExe = Join-Path $buildFolder 'San14Remake.exe'
$nativeDestination = Join-Path $buildFolder 'native/windows-x64'

& (Join-Path $PSScriptRoot 'Install-WindowsRuntime.ps1') -InstallDirectory $nativeDestination | Out-Host
& (Join-Path $projectRoot 'native/San14.Render.D3D11/build-product.ps1') -MSBuildPath $MSBuildPath `
    -InstallDirectory $nativeDestination | Out-Host
& dotnet build (Join-Path $projectRoot 'game/San14.Client.sln') -c Release -nologo -v minimal | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Client build failed ($LASTEXITCODE)." }

New-Item -ItemType Directory -Path $buildFolder -Force | Out-Null
& $GodotConsole --headless --path (Join-Path $projectRoot 'game') --export-release 'Windows Desktop' $exportExe `
    *> (Join-Path $buildFolder 'export-release.log')
if ($LASTEXITCODE -ne 0) { throw "Godot export failed ($LASTEXITCODE); see export-release.log." }
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY.md') -Destination $buildFolder -Force
Write-Output "Windows client, renderer and third-party runtime built: $exportExe"
