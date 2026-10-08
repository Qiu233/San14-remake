using System.Runtime.InteropServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalShaderSpirv(byte[] Bytes, string SourceProgramSha256,
    string SpirvSha256, string CompilerVersion, string CompilerLibrarySha256, string Diagnostics)
{
    public uint? ExplicitDescriptorSet { get; init; }
    public bool BufferUavsAsStorageBuffers { get; init; }
}

/// <summary>Windows x64 shader translation, without launching the original game or WSL.
/// Default vkd3d descriptor assignment is retained; callers must inspect the output
/// interface and supply its complete resources before drawing.</summary>
public sealed class Vkd3dShaderCompiler : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct CompileOption { public int Name; public uint Value; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Code { public IntPtr Pointer; public nuint Size; }
    [StructLayout(LayoutKind.Sequential)]
    private struct CompileInfo
    {
        public int Type;
        public IntPtr Next;
        public Code Source;
        public int SourceType, TargetType;
        public IntPtr Options;
        public uint OptionCount;
        public int LogLevel;
        public IntPtr SourceName;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ResourceBinding
    {
        public int Type;
        public uint RegisterSpace, RegisterIndex;
        public int Visibility;
        public uint Flags, Set, Binding, Count;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct InterfaceInfo
    {
        public int Type;
        public IntPtr Next, Bindings;
        public uint BindingCount;
        public IntPtr PushConstantBuffers;
        public uint PushConstantBufferCount;
        public IntPtr CombinedSamplers;
        public uint CombinedSamplerCount;
        public IntPtr UavCounters;
        public uint UavCounterCount;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CompileDelegate(ref CompileInfo info, out Code output, out IntPtr messages);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr VersionDelegate(out uint major, out uint minor);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeCodeDelegate(ref Code code);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeMessagesDelegate(IntPtr messages);

    private readonly object _gate = new();
    private IntPtr _library;
    private readonly CompileDelegate _compile;
    private readonly FreeCodeDelegate _freeCode;
    private readonly FreeMessagesDelegate _freeMessages;
    public string Version { get; }
    public string LibraryPath { get; }
    public string LibrarySha256 { get; }

    public Vkd3dShaderCompiler(string libraryPath)
    {
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("Original runtime shader translation requires the Windows x64 compiler library.");
        LibraryPath = Path.GetFullPath(libraryPath);
        LibrarySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(LibraryPath)));
        _library = NativeLibrary.Load(LibraryPath);
        try
        {
            T Symbol<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, name));
            _compile = Symbol<CompileDelegate>("vkd3d_shader_compile");
            _freeCode = Symbol<FreeCodeDelegate>("vkd3d_shader_free_shader_code");
            _freeMessages = Symbol<FreeMessagesDelegate>("vkd3d_shader_free_messages");
            IntPtr version = Symbol<VersionDelegate>("vkd3d_shader_get_version")(out uint major, out uint minor);
            Version = Marshal.PtrToStringUTF8(version) ?? throw new InvalidDataException("Shader compiler has no version string.");
            if (major != 2 || minor < 1)
                throw new NotSupportedException("Original shader translation requires verified vkd3d 2.1 or a compatible later 2.x API.");
            if (Marshal.SizeOf<CompileInfo>() != 64 || Marshal.SizeOf<Code>() != 16 ||
                Marshal.SizeOf<ResourceBinding>() != 32 || Marshal.SizeOf<InterfaceInfo>() != 80)
                throw new InvalidDataException("Shader compiler native x64 ABI differs from the source header.");
            if (Marshal.OffsetOf<InterfaceInfo>(nameof(InterfaceInfo.Bindings)).ToInt32() != 16 ||
                Marshal.OffsetOf<InterfaceInfo>(nameof(InterfaceInfo.BindingCount)).ToInt32() != 24 ||
                Marshal.OffsetOf<InterfaceInfo>(nameof(InterfaceInfo.UavCounterCount)).ToInt32() != 72 ||
                Marshal.OffsetOf<ResourceBinding>(nameof(ResourceBinding.Set)).ToInt32() != 20 ||
                Marshal.OffsetOf<ResourceBinding>(nameof(ResourceBinding.Count)).ToInt32() != 28)
                throw new InvalidDataException("Shader compiler binding-chain field offsets differ from the official x64 ABI.");
        }
        catch { NativeLibrary.Free(_library); _library = IntPtr.Zero; throw; }
    }

    public OriginalShaderSpirv Compile(ReadOnlySpan<byte> dxbc, uint? explicitDescriptorSet = null,
        bool bufferUavsAsStorageBuffers = false)
    {
        _ = DxbcContainerReader.Read(dxbc);
        byte[] input = dxbc.ToArray();
        ResourceBinding[]? remapping = null;
        if (explicitDescriptorSet is uint set)
        {
            if (set > 3) throw new ArgumentOutOfRangeException(nameof(explicitDescriptorSet), "Own renderer descriptor set must be in 0..3.");
            DxbcResourceDefinitions definitions = DxbcResourceDefinitionReader.Read(input);
            remapping = definitions.Bindings.Select((row, index) =>
            {
                if (row.BindCount != 1 || row.InputType is not (0 or 2 or 3))
                    throw new InvalidDataException("Explicit shader mapping requires single CBV, SRV or sampler declarations.");
                return new ResourceBinding {
                    Type = row.InputType switch { 0 => 2, 2 => 0, _ => 3 },
                    RegisterSpace = 0, RegisterIndex = row.BindPoint, Visibility = 0,
                    Flags = row.InputType == 0 ? 1u : row.InputType == 2 ? row.Dimension == 1 ? 1u : 2u : 0,
                    Set = set, Binding = checked((uint)index), Count = 1 };
            }).ToArray();
        }
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_library == IntPtr.Zero, this);
            GCHandle pinned = GCHandle.Alloc(input, GCHandleType.Pinned);
            GCHandle pinnedBindings = default;
            GCHandle pinnedOptions = default;
            IntPtr interfacePointer = IntPtr.Zero;
            Code output = default;
            IntPtr messages = IntPtr.Zero;
            try
            {
                // Exact vkd3d 2.1 header enums: COMPILE_INFO=0, DXBC_TPF=1,
                // SPIRV_BINARY=1, LOG_WARNING=2. Optional INTERFACE_INFO=1
                // changes descriptor coordinates only, never original constants.
                if (remapping is not null)
                {
                    pinnedBindings = GCHandle.Alloc(remapping, GCHandleType.Pinned);
                    interfacePointer = Marshal.AllocHGlobal(Marshal.SizeOf<InterfaceInfo>());
                    Marshal.StructureToPtr(new InterfaceInfo { Type = 1,
                        Bindings = pinnedBindings.AddrOfPinnedObject(), BindingCount = checked((uint)remapping.Length) },
                        interfacePointer, false);
                }
                var info = new CompileInfo { Type = 0, Source = new() { Pointer = pinned.AddrOfPinnedObject(), Size = (nuint)input.Length },
                    SourceType = 1, TargetType = 1, LogLevel = 2, Next = interfacePointer };
                if(bufferUavsAsStorageBuffers)
                {
                    // Official vkd3d_shader.h BUFFER_UAV name2/value1.
                    // Changes backend resource representation, not DXBC bytes.
                    var options=new[]{new CompileOption{Name=2,Value=1}};
                    pinnedOptions=GCHandle.Alloc(options,GCHandleType.Pinned);
                    info.Options=pinnedOptions.AddrOfPinnedObject();info.OptionCount=1;
                }
                int status = _compile(ref info, out output, out messages);
                string diagnostics = messages == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(messages) ?? "";
                if (status < 0)
                    throw new InvalidDataException($"Original DXBC translation failed ({status}): {diagnostics}");
                if (output.Pointer == IntPtr.Zero || output.Size < 20 || output.Size > int.MaxValue || output.Size % 4 != 0)
                    throw new InvalidDataException("Shader compiler returned an invalid SPIR-V byte range.");
                byte[] bytes = new byte[(int)output.Size];
                Marshal.Copy(output.Pointer, bytes, 0, bytes.Length);
                if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x07230203)
                    throw new InvalidDataException("Shader compiler output is not binary SPIR-V.");
                return new(bytes, Convert.ToHexString(SHA256.HashData(input)), Convert.ToHexString(SHA256.HashData(bytes)),
                    Version, LibrarySha256, diagnostics) { ExplicitDescriptorSet = explicitDescriptorSet,
                        BufferUavsAsStorageBuffers=bufferUavsAsStorageBuffers };
            }
            finally
            {
                if (output.Pointer != IntPtr.Zero) _freeCode(ref output);
                if (messages != IntPtr.Zero) _freeMessages(messages);
                if (interfacePointer != IntPtr.Zero) Marshal.FreeHGlobal(interfacePointer);
                if (pinnedBindings.IsAllocated) pinnedBindings.Free();
                if (pinnedOptions.IsAllocated) pinnedOptions.Free();
                pinned.Free();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_library == IntPtr.Zero) return;
            NativeLibrary.Free(_library);
            _library = IntPtr.Zero;
        }
    }
}
