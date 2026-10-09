using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>The project's compiled GGX ambient integral.</summary>
public sealed record LoadedOriginalEmbeddedGgxShader(byte[] Dxbc, string ResourceUri, string Locale,
    string ShaderSha256);

/// <summary>Compiles the project's HLSL once. No original executable or executable cache is used.</summary>
public static class OriginalEmbeddedGgxShader
{
    private const string ResourceName = "San14.Data.OriginalEmbeddedGgxShader.hlsl";
    private static readonly Lazy<(byte[] Bytes, string Sha256)> Program = new(Compile);

    public static LoadedOriginalEmbeddedGgxShader Load(string locale)
    {
        if (locale is not ("SC" or "TC"))
            throw new ArgumentOutOfRangeException(nameof(locale), locale, "The map source locale must be SC or TC.");
        var program = Program.Value;
        return new(program.Bytes, "san14://project/shaders/ggx-ambient-lut.ps", locale, program.Sha256);
    }

    private static (byte[] Bytes, string Sha256) Compile()
    {
        using Stream stream = typeof(OriginalEmbeddedGgxShader).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The project GGX HLSL source is missing from the assembly.");
        using var source = new MemoryStream();
        stream.CopyTo(source);
        byte[] sourceBytes = source.ToArray();
        int status = D3DCompile(sourceBytes, (nuint)sourceBytes.Length, "project-ggx-ambient-lut",
            IntPtr.Zero, IntPtr.Zero, "main", "ps_5_0", 1u << 11, 0, out IntPtr code, out IntPtr errors);
        try
        {
            if (status < 0)
                throw new InvalidDataException("Project GGX HLSL compilation failed: " +
                    (errors == IntPtr.Zero ? $"HRESULT 0x{status:X8}" : ReadBlobText(errors)));
            byte[] bytes = ReadBlob(code);
            return (bytes, Convert.ToHexString(SHA256.HashData(bytes)));
        }
        finally
        {
            if (errors != IntPtr.Zero) Release(errors);
            if (code != IntPtr.Zero) Release(code);
        }
    }

    private static byte[] ReadBlob(IntPtr blob)
    {
        IntPtr table = Marshal.ReadIntPtr(blob);
        var pointer = Marshal.GetDelegateForFunctionPointer<BlobPointer>(Marshal.ReadIntPtr(table, 3 * IntPtr.Size));
        var size = Marshal.GetDelegateForFunctionPointer<BlobSize>(Marshal.ReadIntPtr(table, 4 * IntPtr.Size));
        byte[] bytes = new byte[checked((int)size(blob))];
        Marshal.Copy(pointer(blob), bytes, 0, bytes.Length);
        return bytes;
    }

    private static string ReadBlobText(IntPtr blob)
        => System.Text.Encoding.UTF8.GetString(ReadBlob(blob)).TrimEnd('\0', '\r', '\n');

    private static void Release(IntPtr blob)
    {
        IntPtr table = Marshal.ReadIntPtr(blob);
        Marshal.GetDelegateForFunctionPointer<BlobRelease>(Marshal.ReadIntPtr(table, 2 * IntPtr.Size))(blob);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr BlobPointer(IntPtr blob);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nuint BlobSize(IntPtr blob);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint BlobRelease(IntPtr blob);

    [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int D3DCompile(byte[] source, nuint sourceBytes, string sourceName,
        IntPtr defines, IntPtr include, string entryPoint, string target, uint flags, uint effectFlags,
        out IntPtr code, out IntPtr errors);
}
