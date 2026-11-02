param(
    [string]$GodotSourceDirectory = $env:SAN14_GODOT_SOURCE,
    [string]$PythonPath = $env:SAN14_GODOT_PYTHON,
    [string]$MesaDirectory = $env:SAN14_GODOT_MESA,
    [string]$AgilitySdkDirectory = $env:SAN14_GODOT_AGILITY,
    [string]$AccessKitDirectory = $env:SAN14_GODOT_ACCESSKIT,
    [string]$ExistingTemplateDirectory,
    [string]$InstallDirectory,
    [ValidateRange(1, 128)][int]$Jobs = 8
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $InstallDirectory) { $InstallDirectory = Join-Path $projectRoot '.tools/godot/san14-windows-x64' }
$InstallDirectory = [IO.Path]::GetFullPath($InstallDirectory)
$pin = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'godot/template.json') -Raw | ConvertFrom-Json
if ($pin.recipeVersion -ne 3 -or @($pin.patches).Count -ne 3) {
    throw 'Godot template requires the ownership, typed-format preservation and BGRA sRGB view build recipe.'
}
foreach ($patch in $pin.patches) {
    $patchPath = Join-Path $PSScriptRoot "godot/$($patch.file)"
    if ((Get-FileHash -LiteralPath $patchPath -Algorithm SHA256).Hash -ne $patch.sha256) {
        throw "Godot build patch differs from its pinned source: $patchPath"
    }
}

if ($ExistingTemplateDirectory) {
    # Reuse an already verified template or a previous build receipt. No compilation or download.
    $verified = & (Join-Path $PSScriptRoot 'Get-GodotTemplate.ps1') -TemplateDirectory $ExistingTemplateDirectory
    $templateDirectory = $verified.Directory
    $files = $verified.Files
}
else {
    foreach ($inputDirectory in @($GodotSourceDirectory, $MesaDirectory, $AgilitySdkDirectory, $AccessKitDirectory)) {
        if (-not $inputDirectory -or -not (Test-Path -LiteralPath $inputDirectory -PathType Container)) {
            throw 'Supply local -GodotSourceDirectory, -MesaDirectory, -AgilitySdkDirectory and -AccessKitDirectory (or SAN14_GODOT_SOURCE/MESA/AGILITY/ACCESSKIT). This script does not fetch source or dependencies.'
        }
    }
    $GodotSourceDirectory = (Resolve-Path -LiteralPath $GodotSourceDirectory).Path
    $MesaDirectory = (Resolve-Path -LiteralPath $MesaDirectory).Path
    $AgilitySdkDirectory = (Resolve-Path -LiteralPath $AgilitySdkDirectory).Path
    $AccessKitDirectory = (Resolve-Path -LiteralPath $AccessKitDirectory).Path
    if (-not $PythonPath) { $PythonPath = 'python' }
    $headCommit = & git -C $GodotSourceDirectory rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $headCommit -ne $pin.sourceCommit) {
        throw "Use the local Godot $($pin.version) checkout at commit $($pin.sourceCommit)."
    }
    $changed = @(& git -C $GodotSourceDirectory diff --name-only HEAD --)
    # Each patch has its own input/output. A later patch may change the same
    # path again; only the last declared output is the final build input.
    $finalFiles = [ordered]@{}
    foreach ($patch in $pin.patches) {
        foreach ($file in $patch.files) { $finalFiles[$file.path] = $file }
    }
    $requiredPaths = @($finalFiles.Keys)
    if ($LASTEXITCODE -ne 0 -or @($changed | Where-Object { $_ -notin $requiredPaths }).Count -ne 0) {
        throw 'The local Godot checkout has unrelated tracked changes; use a separate checkout for the template.'
    }
    $alreadyFinal = $true
    foreach ($file in $finalFiles.Values) {
        if ((Get-FileHash -LiteralPath (Join-Path $GodotSourceDirectory $file.path) -Algorithm SHA256).Hash -ne $file.afterSha256) {
            $alreadyFinal = $false
            break
        }
    }
    if (-not $alreadyFinal) {
        foreach ($patch in $pin.patches) {
            $patchPath = Join-Path $PSScriptRoot "godot/$($patch.file)"
            $before = @($patch.files | Where-Object {
                (Get-FileHash -LiteralPath (Join-Path $GodotSourceDirectory $_.path) -Algorithm SHA256).Hash -ne $_.beforeSha256
            })
            if ($before.Count -eq 0) {
                & git -C $GodotSourceDirectory apply --check $patchPath
                if ($LASTEXITCODE -ne 0) { throw "Godot patch cannot be applied to the local checkout: $($patch.file)" }
                & git -C $GodotSourceDirectory apply $patchPath
                if ($LASTEXITCODE -ne 0) { throw "Godot patch application failed: $($patch.file)" }
            }
            foreach ($file in $patch.files) {
                if ((Get-FileHash -LiteralPath (Join-Path $GodotSourceDirectory $file.path) -Algorithm SHA256).Hash -ne $file.afterSha256) {
                    throw "Godot source is not at the required ordered patch state: $($patch.file) / $($file.path)"
                }
            }
        }
    }
    foreach ($file in $finalFiles.Values) {
        if ((Get-FileHash -LiteralPath (Join-Path $GodotSourceDirectory $file.path) -Algorithm SHA256).Hash -ne $file.afterSha256) {
            throw "Godot source lacks the final patched input: $($file.path)"
        }
    }
    foreach ($entry in @($pin.verifiedFiles | Where-Object { $_.name -like '*.dll' })) {
        $dllPath = Join-Path $AgilitySdkDirectory "build/native/bin/x64/$($entry.name)"
        if ((Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash -ne $entry.sha256) {
            throw "Local Agility SDK must match version $($pin.agilitySdkVersion): $dllPath"
        }
    }
    $sconsArguments = @('-m', 'SCons') + @($pin.buildArguments) + @(
        "mesa_libs=$MesaDirectory", "agility_sdk_path=$AgilitySdkDirectory", "accesskit_sdk_path=$AccessKitDirectory", "-j$Jobs")
    Push-Location -LiteralPath $GodotSourceDirectory
    try {
        & $PythonPath @sconsArguments | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Local Godot template build failed ($LASTEXITCODE)." }
    }
    finally { Pop-Location }
    foreach ($file in $finalFiles.Values) {
        if ((Get-FileHash -LiteralPath (Join-Path $GodotSourceDirectory $file.path) -Algorithm SHA256).Hash -ne $file.afterSha256) {
            throw "Godot patched source changed during compilation: $($file.path)"
        }
    }
    $templateDirectory = Join-Path $GodotSourceDirectory 'bin'
    $files = @()
    foreach ($entry in $pin.verifiedFiles) {
        $output = Join-Path $templateDirectory $entry.name
        $files += [pscustomobject]@{ name = $entry.name; sha256 = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash }
    }
}

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
foreach ($entry in $files) {
    $source = Join-Path $templateDirectory $entry.name
    $destination = Join-Path $installDirectory $entry.name
    if ([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($destination)) {
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
}
[ordered]@{
    sourceCommit = $pin.sourceCommit
    recipeVersion = $pin.recipeVersion
    patches = @($pin.patches | ForEach-Object { [ordered]@{ file = $_.file; sha256 = $_.sha256 } })
    buildArguments = $pin.buildArguments
    files = $files
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $installDirectory 'template-build.json') -Encoding UTF8
& (Join-Path $PSScriptRoot 'Get-GodotTemplate.ps1') -TemplateDirectory $InstallDirectory | Out-Host
Write-Output "Patched Windows x64 .NET template installed: $installDirectory"
