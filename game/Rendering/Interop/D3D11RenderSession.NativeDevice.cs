using San14.Data;
using System.Runtime.InteropServices;
using Godot;

public sealed partial class D3D11RenderSession
{
    internal static string ResolveNativeLibraryPath()
    {
        string fallback = OS.HasFeature("editor")
            ? "res://../native/San14.Render.D3D11/bin/x64/Release/San14.Render.D3D11.dll"
            : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!, "native", "windows-x64", "San14.Render.D3D11.dll");
        string setting = System.Environment.GetEnvironmentVariable("SAN14_RENDER_NATIVE_LIBRARY")
            ?? ProjectSettings.GetSetting("san14/render/native_library", fallback).AsString();
        return Path.GetFullPath(ProjectSettings.GlobalizePath(setting));
    }

    // Main owns one real device generation. Output bridges borrow it and keep
    // their own output slots, fences and retirement. No process-global lookup.
    internal sealed class Native11DeviceOwner
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct DeviceInfo
        {
            internal uint StructBytes, Version;
            internal ulong GodotDevice12, Device11, Context11, AdapterLuid;
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate nint CreateCall(nint godotDevice12, out DeviceInfo info, uint bytes);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int DestroyCall(nint owner);

        private readonly object gate = new();
        private readonly DestroyCall destroy;
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TerrainProgramSource
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string LogicalKey;
            [MarshalAs(UnmanagedType.LPWStr)] public string Identity;
            [MarshalAs(UnmanagedType.LPWStr)] public string Directory;
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int LoadTerrainProgramsCall(nint owner,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] TerrainProgramSource[] sources, uint count);
        private readonly LoadTerrainProgramsCall loadTerrainPrograms;
        private readonly LoadTerrainProgramsCall loadPrimaryModels;
        private readonly LoadTerrainProgramsCall loadRouteMedia;
        private readonly ErrorCall error;
        private nint module, handle;
        private int bridges;
        private bool closing;
        private Task? retirement;
        private TaskCompletionSource<bool>? noBridges;
        internal string LibraryPath { get; }
        internal DeviceInfo Info { get; }

        private Native11DeviceOwner(string library, ulong godotDevice12, IReadOnlyList<D3D11TerrainPreparedPrograms>? terrainPrograms,
            IReadOnlyList<D3D11PrimaryGpuMediaSource>? primaryModels, IReadOnlyList<MapRoutePreparedMedia>? routeMedia)
        {
            LibraryPath = NativeFileSystemPath(library);
            if (Marshal.SizeOf<DeviceInfo>() != 40 || godotDevice12 == 0)
                throw new InvalidOperationException("Native11 device description or actual Godot device is missing.");
            module = NativeLibrary.Load(LibraryPath);
            try
            {
                T Export<T>(string name) where T : Delegate =>
                    Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));
                destroy = Export<DestroyCall>("san14_render_native11_device_destroy_v1");
                error = Export<ErrorCall>("san14_render_error");
                loadTerrainPrograms = Export<LoadTerrainProgramsCall>("san14_render_native11_terrain_programs_load_v1");
                loadPrimaryModels = Export<LoadTerrainProgramsCall>("san14_render_native11_primary_models_load_v1");
                loadRouteMedia = Export<LoadTerrainProgramsCall>("san14_render_native11_route_media_load_v1");
                handle = Export<CreateCall>("san14_render_native11_device_create_v1")(
                    unchecked((nint)(nuint)godotDevice12), out var info, 40);
                if (handle == 0) throw Failure();
                if (info.StructBytes != 40 || info.Version != 1 || info.GodotDevice12 != godotDevice12 ||
                    info.Device11 == 0 || info.Context11 == 0)
                    throw new InvalidOperationException("Native11 device owner returned an incompatible description.");
                Info = info;
                if (terrainPrograms is not null) LoadTerrainPrograms(terrainPrograms);
                if (primaryModels is not null) LoadPrimaryModels(primaryModels);
                if (routeMedia is not null) LoadRouteMedia(routeMedia);
            }
            catch
            {
                // Creation has no output bridges. A failed native destroy
                // retains its handle/module, rather than unloading live code.
                if (handle != 0)
                {
                    if (destroy(handle) != 0) throw Failure();
                    handle = 0;
                }
                NativeLibrary.Free(module); module = 0;
                throw;
            }
        }

        internal static Native11DeviceOwner Create(string library, ulong actualGodotDevice12,
            IReadOnlyList<D3D11TerrainPreparedPrograms>? terrainPrograms = null,
            IReadOnlyList<D3D11PrimaryGpuMediaSource>? primaryModels = null,
            IReadOnlyList<MapRoutePreparedMedia>? routeMedia = null) =>
            new(library, actualGodotDevice12, terrainPrograms, primaryModels, routeMedia);

        // Runs on the existing startup/resource worker, never from Draw. The
        // native transaction replaces only the current declared shader closure.
        internal void LoadTerrainPrograms(IReadOnlyList<D3D11TerrainPreparedPrograms> sources)
        {
            ArgumentNullException.ThrowIfNull(sources);
            if (sources.Count == 0 || Marshal.SizeOf<TerrainProgramSource>() != 24)
                throw new InvalidDataException("The declared Landscape program closure or ABI layout is absent.");
            var inputs = sources.Select(source => new TerrainProgramSource
            {
                LogicalKey = source.LogicalKey, Identity = source.Identity,
                Directory = NativeFileSystemPath(source.Directory)
            }).ToArray();
            lock (gate)
            {
                if (closing || handle == 0) throw new ObjectDisposedException(nameof(Native11DeviceOwner));
                if (loadTerrainPrograms(handle, inputs, checked((uint)inputs.Length)) != 0) throw Failure();
            }
        }

        internal void LoadPrimaryModels(IReadOnlyList<D3D11PrimaryGpuMediaSource> sources)
        {
            ArgumentNullException.ThrowIfNull(sources);
            if (sources.Count == 0 || Marshal.SizeOf<TerrainProgramSource>() != 24)
                throw new InvalidDataException("The declared Primary immutable closure or ABI layout is absent.");
            var inputs = sources.Select(source => new TerrainProgramSource
            {
                LogicalKey = source.LogicalKey, Identity = source.Identity,
                Directory = NativeFileSystemPath(source.Directory)
            }).ToArray();
            lock (gate)
            {
                if (closing || handle == 0) throw new ObjectDisposedException(nameof(Native11DeviceOwner));
                if (loadPrimaryModels(handle, inputs, checked((uint)inputs.Length)) != 0) throw Failure();
            }
        }

        internal void LoadRouteMedia(IReadOnlyList<MapRoutePreparedMedia> sources)
        {
            ArgumentNullException.ThrowIfNull(sources);
            if (sources.Count == 0 || Marshal.SizeOf<TerrainProgramSource>() != 24)
                throw new InvalidDataException("The declared route media closure or ABI layout is absent.");
            var inputs = sources.Select(source => new TerrainProgramSource
            {
                LogicalKey = source.LogicalKey, Identity = source.Identity,
                Directory = NativeFileSystemPath(source.Directory)
            }).ToArray();
            lock (gate)
            {
                if (closing || handle == 0) throw new ObjectDisposedException(nameof(Native11DeviceOwner));
                if (loadRouteMedia(handle, inputs, checked((uint)inputs.Length)) != 0) throw Failure();
            }
        }

        internal BridgeLease BorrowForBridge(string library)
        {
            lock (gate)
            {
                if (closing || handle == 0) throw new ObjectDisposedException(nameof(Native11DeviceOwner));
                if (!StringComparer.OrdinalIgnoreCase.Equals(LibraryPath, NativeFileSystemPath(library)))
                    throw new InvalidOperationException("A Native11 bridge must use its device owner's actual library.");
                ++bridges;
                return new BridgeLease(this, handle);
            }
        }

        internal sealed class BridgeLease : IDisposable
        {
            private Native11DeviceOwner? owner;
            internal nint Handle { get; }
            internal DeviceInfo Info { get; }
            internal BridgeLease(Native11DeviceOwner owner, nint handle)
            { this.owner = owner; Handle = handle; Info = owner.Info; }
            public void Dispose() => Interlocked.Exchange(ref owner, null)?.ReturnBridge();
        }

        private void ReturnBridge()
        {
            TaskCompletionSource<bool>? signal = null;
            lock (gate)
            {
                if (bridges <= 0) throw new InvalidOperationException("Native11 device bridge lease is not live.");
                if (--bridges == 0) signal = noBridges;
            }
            signal?.TrySetResult(true);
        }

        internal Task RetireAsync()
        {
            lock (gate)
            {
                closing = true;
                if (handle == 0) return Task.CompletedTask;
                if (retirement is { IsCompleted: false } || retirement is { IsCompletedSuccessfully: true })
                    return retirement;
                // Failed retirement retains the same handle and may be retried
                // by the application's existing retry-on-window-close path.
                Task drained = bridges == 0 ? Task.CompletedTask :
                    (noBridges ??= new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
                retirement = Task.Run(async () =>
                {
                    await drained.ConfigureAwait(false);
                    nint nativeHandle;
                    lock (gate) nativeHandle = handle;
                    if (destroy(nativeHandle) != 0) throw Failure();
                    lock (gate)
                    {
                        handle = 0;
                        NativeLibrary.Free(module); module = 0;
                    }
                });
                return retirement;
            }
        }

        private InvalidOperationException Failure() => new(
            Marshal.PtrToStringUTF8(error()) ?? "Native11 application device operation failed.");
    }
}
