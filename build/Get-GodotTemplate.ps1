param([string]$TemplateDirectory)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $TemplateDirectory) {
    $TemplateDirectory = Join-Path $projectRoot '.tools/godot/san14-windows-x64'
}
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
$receiptPath = Join-Path $TemplateDirectory 'template-build.json'
$receipt = $null
if (Test-Path -LiteralPath $receiptPath -PathType Leaf) {
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if ($receipt.recipeVersion -ne $pin.recipeVersion -or $receipt.sourceCommit -ne $pin.sourceCommit -or
        ($receipt.buildArguments -join "`n") -ne ($pin.buildArguments -join "`n")) {
        throw "Godot template build receipt differs from the required source/patch/options: $receiptPath"
    }
    if (@($receipt.patches).Count -ne @($pin.patches).Count) {
        throw "Godot template receipt lacks the required ownership, format and BGRA view patches: $receiptPath"
    }
    foreach ($patch in $pin.patches) {
        $record = @($receipt.patches | Where-Object { $_.file -eq $patch.file })
        if ($record.Count -ne 1 -or $record[0].sha256 -ne $patch.sha256) {
            throw "Godot template receipt differs from the required patch: $($patch.file)"
        }
    }
}
$files = @()
foreach ($entry in $pin.verifiedFiles) {
    $path = Join-Path $TemplateDirectory $entry.name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Patched Godot template file is missing: $path. Run build/Build-GodotTemplate.ps1 using local inputs; no stock template fallback is permitted."
    }
    $expectedHash = $entry.sha256
    if ($receipt) {
        $record = @($receipt.files | Where-Object { $_.name -eq $entry.name })
        if ($record.Count -ne 1 -or $record[0].sha256 -notmatch '^[0-9a-fA-F]{64}$') {
            throw "Godot template receipt is missing a unique file identity: $($entry.name)"
        }
        $expectedHash = $record[0].sha256
        # The engine can be rebuilt locally. Its Agility dependency stays version-pinned.
        if ($entry.name -like '*.dll' -and $expectedHash -ne $entry.sha256) {
            throw "Godot Agility SDK differs from pinned $($pin.agilitySdkVersion): $($entry.name)"
        }
    }
    $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($actualHash -ne $expectedHash) { throw "Patched Godot template identity differs: $path" }
    $files += [pscustomobject]@{ name = $entry.name; sha256 = $actualHash }
}
[pscustomobject]@{
    Directory = (Resolve-Path -LiteralPath $TemplateDirectory).Path
    TemplateName = $pin.templateName
    RecipeVersion = $pin.recipeVersion
    Patches = $pin.patches
    Files = $files
}
