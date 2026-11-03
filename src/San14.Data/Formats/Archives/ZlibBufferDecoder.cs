using System.Runtime.InteropServices;

namespace San14.Data.Formats;

/// <summary>Decodes each complete zlib stream through zlib's public, size_t-based buffer API.</summary>
internal static class ZlibBufferDecoder
{
    internal static unsafe void Decode(ReadOnlySpan<byte> compressed, Span<byte> output, int chunkIndex)
    {
        nuint consumed = (nuint)compressed.Length, written = (nuint)output.Length;
        fixed (byte* inputPointer = compressed)
        fixed (byte* outputPointer = output)
        {
            Result result = Uncompress(outputPointer, ref written, inputPointer, ref consumed);
            if (result == Result.MemoryError)
                throw new OutOfMemoryException("Cannot allocate the Zp1 zlib decoder.");
            if (result != Result.Success)
                throw new InvalidDataException($"Zp1 chunk {chunkIndex} cannot be decoded to its declared length ({result}).");
            if (consumed != (nuint)compressed.Length)
                throw new InvalidDataException($"Zp1 chunk {chunkIndex} contains bytes after its zlib stream.");
            if (written != (nuint)output.Length)
                throw new InvalidDataException($"Zp1 chunk {chunkIndex} decoded length differs from its declaration.");
        }
    }

    private enum Result { Success = 0, StreamError = -2, DataError = -3, MemoryError = -4, BufferError = -5, VersionError = -6 }

    [DllImport("San14.Compression.Zlib", EntryPoint = "uncompress2_z", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    private static extern unsafe Result Uncompress(byte* output, ref nuint outputLength,
        byte* input, ref nuint inputLength);
}
