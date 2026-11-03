param(
    [Parameter(Mandatory=$true)][string]$SourceDirectory,
    [Parameter(Mandatory=$true)][string]$OutputFile
)

$ErrorActionPreference = 'Stop'

# This build-only compiler uses the same public D3DCompile entry point and flags
# as the original native allocation path. No game resources or device are read.
$nativeLibraryPath = $env:LIB
try {
    # Add-Type compiles managed P/Invoke declarations only. Native MSBuild's LIB
    # search path is irrelevant here and can contain optional SDK components.
    $env:LIB = $null
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class DirectProgramCompiler
{
    [ComImport, Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface Blob
    {
        [PreserveSig] IntPtr GetBufferPointer();
        [PreserveSig] UIntPtr GetBufferSize();
    }
    [DllImport("d3dcompiler_47.dll", CallingConvention=CallingConvention.Winapi)]
    static extern int D3DCompile([In] byte[] source, UIntPtr size,
        [MarshalAs(UnmanagedType.LPStr)] string sourceName, IntPtr defines, IntPtr include,
        [MarshalAs(UnmanagedType.LPStr)] string entry, [MarshalAs(UnmanagedType.LPStr)] string profile,
        uint flags, uint effectFlags, out Blob code, out Blob errors);

    public static byte[] Compile(string source, string entry)
    {
        byte[] input = Encoding.UTF8.GetBytes(source);
        Blob code = null, errors = null;
        try
        {
            // D3DCOMPILE_ENABLE_STRICTNESS, implicit optimization level 1.
            int result = D3DCompile(input, (UIntPtr)input.Length, "project-direct-effect",
                IntPtr.Zero, IntPtr.Zero, entry, "cs_5_0", 1u << 11, 0, out code, out errors);
            if (result < 0)
            {
                string message = errors == null ? "" : Marshal.PtrToStringAnsi(errors.GetBufferPointer(), checked((int)errors.GetBufferSize().ToUInt64()));
                throw new InvalidOperationException("Direct Effect build compilation failed for " + entry + ": " + message);
            }
            byte[] bytes = new byte[checked((int)code.GetBufferSize().ToUInt64())];
            Marshal.Copy(code.GetBufferPointer(), bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            if (errors != null) Marshal.ReleaseComObject(errors);
            if (code != null) Marshal.ReleaseComObject(code);
        }
    }
}
'@
}
finally { $env:LIB = $nativeLibraryPath }

$directory = [IO.Path]::GetFullPath($SourceDirectory)
$output = [IO.Path]::GetFullPath($OutputFile)
$utf8 = New-Object Text.UTF8Encoding($false)
function Read-Source([string]$name) { return [IO.File]::ReadAllText((Join-Path $directory ('G1EffectDirect.' + $name + '.hlsl'))) }
$common = Read-Source 'Common'
$storage = Read-Source 'Storage'
$programs = @(
    @{ Entry = 'Initialize'; Source = $storage },
    @{ Entry = 'Birth'; Source = $common + (Read-Source 'Birth') },
    @{ Entry = 'Update'; Source = $common + (Read-Source 'Update') },
    @{ Entry = 'ResetDraw'; Source = $storage },
    @{ Entry = 'ExpandDraw'; Source = (Read-Source 'Expand') },
    @{ Entry = 'LinkedBirth'; Source = $common + (Read-Source 'LinkedBirth') },
    @{ Entry = 'LinkedUpdate'; Source = $common + (Read-Source 'LinkedUpdate') },
    @{ Entry = 'Motion'; Source = $common + (Read-Source 'Motion') },
    @{ Entry = 'Postprocess'; Source = $common + (Read-Source 'Postprocess') }
)
$text = New-Object Text.StringBuilder
[void]$text.AppendLine('// Generated from project HLSL by build-direct-programs.ps1; do not edit.')
[void]$text.AppendLine('#pragma once')
[void]$text.AppendLine('namespace current_effect::built_direct {')
foreach ($program in $programs) {
    $entry = $program.Entry
    $source = $program.Source
    $bytes = [DirectProgramCompiler]::Compile($source, $entry)
    # Numeric UTF-8 bytes retain CRLF exactly; C++ raw string literals normalize
    # physical line endings and would miss the unchanged managed declaration.
    $sourceBytes = $utf8.GetBytes($source)
    [void]$text.Append(('inline constexpr unsigned char Source{0}[] = {{' -f $entry))
    [void]$text.Append(($sourceBytes | ForEach-Object { $_.ToString() }) -join ',')
    [void]$text.AppendLine('};')
    [void]$text.Append(('inline constexpr unsigned char Code{0}[] = {{' -f $entry))
    [void]$text.Append(($bytes | ForEach-Object { $_.ToString() }) -join ',')
    [void]$text.AppendLine('};')
}
[void]$text.AppendLine('struct Program { const unsigned char* source; std::size_t sourceBytes; const char* entry; const unsigned char* code; std::size_t codeBytes; };')
[void]$text.AppendLine('inline constexpr Program Programs[] = {')
foreach ($program in $programs) {
    $entry = $program.Entry
    [void]$text.AppendLine(('{{Source{0},sizeof(Source{0}),"{0}",Code{0},sizeof(Code{0})}},' -f $entry))
}
[void]$text.AppendLine('};')
[void]$text.AppendLine('}')
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
# Write only after all nine real compilations succeed. MSBuild invalidates this
# generated input whenever its script or any project direct HLSL input changes.
[IO.File]::WriteAllText($output, $text.ToString(), $utf8)
