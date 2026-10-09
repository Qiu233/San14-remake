using System.Runtime.InteropServices;

namespace San14.Data;

/// <summary>Compiles project HLSL embedded in the Data assembly with the Windows system compiler.</summary>
internal static class ProjectHlslCompiler
{
    public static byte[] CompileEmbedded(string filename, string entryPoint, string target)
    {
        using Stream stream = typeof(ProjectHlslCompiler).Assembly.GetManifestResourceStream("San14.Data." + filename)
            ?? throw new InvalidOperationException("Project HLSL source is missing from the assembly: " + filename);
        using var source = new MemoryStream();
        stream.CopyTo(source);
        byte[] sourceBytes = source.ToArray();
        int status = D3DCompile(sourceBytes, (nuint)sourceBytes.Length, filename,
            IntPtr.Zero, IntPtr.Zero, entryPoint, target, 1u << 11, 0, out IntPtr code, out IntPtr errors);
        try
        {
            if (status < 0)
                throw new InvalidDataException("Project HLSL compilation failed: " +
                    (errors == IntPtr.Zero ? $"HRESULT 0x{status:X8}" : System.Text.Encoding.UTF8.GetString(ReadBlob(errors)).TrimEnd('\0', '\r', '\n')));
            return ReadBlob(code);
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

    private static void Release(IntPtr blob)
    {
        IntPtr table = Marshal.ReadIntPtr(blob);
        Marshal.GetDelegateForFunctionPointer<BlobRelease>(Marshal.ReadIntPtr(table, 2 * IntPtr.Size))(blob);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr BlobPointer(IntPtr blob);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate nuint BlobSize(IntPtr blob);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint BlobRelease(IntPtr blob);
    [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int D3DCompile(byte[] source, nuint sourceBytes, string sourceName,
        IntPtr defines, IntPtr include, string entryPoint, string target, uint flags, uint effectFlags,
        out IntPtr code, out IntPtr errors);
}
