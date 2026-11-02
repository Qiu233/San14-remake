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
    throw "Godot 4.7.2 .NET console executable is missing: $GodotConsole. Supply -GodotConsole with your installed Godot .NET console executable; install the patched template using build/Build-GodotTemplate.ps1."
}
$GodotConsole = (Resolve-Path -LiteralPath $GodotConsole).Path
$godotTemplate = & (Join-Path $PSScriptRoot 'Get-GodotTemplate.ps1')
$presetPath = Join-Path $projectRoot 'game/export_presets.cfg'
$preset = Get-Content -LiteralPath $presetPath -Raw
$expectedTemplate = '../.tools/godot/san14-windows-x64/' + $godotTemplate.TemplateName
$customTemplate = [regex]::Match($preset, '(?ms)^\[preset\.0\.options\]\r?\n(?<options>.*?)(?=^\[|\z)')
$templateOption = [regex]::Matches($customTemplate.Groups['options'].Value, '(?m)^custom_template/release="(?<path>[^"]*)"\r?$')
if ($templateOption.Count -ne 1 -or $templateOption[0].Groups['path'].Value -ne $expectedTemplate) {
    throw "Windows Desktop must select the installed patched template in export_presets.cfg: $expectedTemplate"
}
if (-not $BuildDirectory) { $BuildDirectory = Join-Path $projectRoot '.local/build-windows' }
$buildFolder = [IO.Path]::GetFullPath($BuildDirectory)
$exportExe = Join-Path $buildFolder 'San14Remake.exe'
$nativeDestination = Join-Path $buildFolder 'native/windows-x64'

& (Join-Path $PSScriptRoot 'Install-WindowsRuntime.ps1') -InstallDirectory $nativeDestination | Out-Host
& (Join-Path $projectRoot 'native/San14.Render.D3D11/build-product.ps1') -MSBuildPath $MSBuildPath `
    -InstallDirectory $nativeDestination | Out-Host
& (Join-Path $projectRoot 'native/San14.Audio.Decoder/build.ps1') -MSBuildPath $MSBuildPath `
    -InstallDirectory (Join-Path $nativeDestination 'audio') | Out-Host
& (Join-Path $projectRoot 'native/San14.Audio.Speakers/build.ps1') -MSBuildPath $MSBuildPath `
    -InstallDirectory (Join-Path $nativeDestination 'audio') | Out-Host
& (Join-Path $projectRoot 'native/San14.DisplayMode.Guard/build.ps1') -MSBuildPath $MSBuildPath `
    -InstallDirectory (Join-Path $nativeDestination 'display') | Out-Host
& (Join-Path $projectRoot 'native/San14.Media.Decoder/build.ps1') -MSBuildPath $MSBuildPath `
    -InstallDirectory $nativeDestination | Out-Host
& dotnet build (Join-Path $projectRoot 'game/San14.Client.sln') -c Release -nologo -v minimal | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Client build failed ($LASTEXITCODE)." }

New-Item -ItemType Directory -Path $buildFolder -Force | Out-Null
& $GodotConsole --headless --path (Join-Path $projectRoot 'game') --export-release 'Windows Desktop' $exportExe `
    *> (Join-Path $buildFolder 'export-release.log')
if ($LASTEXITCODE -ne 0) { throw "Godot export failed ($LASTEXITCODE); see export-release.log." }
# The custom template has un-suffixed Agility files, which the stock exporter does not discover.
$agilityDestination = Join-Path $buildFolder 'x86_64'
New-Item -ItemType Directory -Path $agilityDestination -Force | Out-Null
foreach ($name in @('D3D12Core.dll', 'd3d12SDKLayers.dll')) {
    Copy-Item -LiteralPath (Join-Path $godotTemplate.Directory $name) -Destination $agilityDestination -Force
}
$engineNotices = Join-Path $buildFolder 'licenses'
New-Item -ItemType Directory -Path $engineNotices -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'godot/LICENSE.txt') -Destination (Join-Path $engineNotices 'Godot-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'godot/Agility-LICENSE.txt') -Destination (Join-Path $engineNotices 'Agility-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY.md') -Destination $buildFolder -Force
Write-Output "Windows client, renderer and third-party runtime built: $exportExe"
