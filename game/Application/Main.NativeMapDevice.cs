using Godot;
using System.Runtime.InteropServices;

public partial class Main
{
    private Task<D3D11RenderSession.Native11DeviceOwner>? applicationNativeMapDevicePreparation;
    private D3D11RenderSession.Native11DeviceOwner? applicationNativeMapDevice;

    private async Task PrepareApplicationNativeMapDeviceAsync()
    {
        // DisplayServer headless has no GPU Canvas. Dummy AUDIO is unrelated:
        // normal private-desktop D3D12 runs always execute the device creation.
        // No GPU-ready state or placeholder owner is published in headless.
        if (DisplayServer.GetName() == "headless") return;
        if (_shuttingDown || _failed || !IsInsideTree()) return;
        if (RenderingServer.GetCurrentRenderingDriverName() != "d3d12" || !RenderingServer.IsOnRenderThread())
            throw new NotSupportedException("Native11 application startup requires the synchronous D3D12 renderer.");
        var rd = RenderingServer.GetRenderingDevice()
            ?? throw new NotSupportedException("Native11 startup has no actual Godot RenderingDevice.");
        ulong device = rd.GetDriverResource(RenderingDevice.DriverResource.LogicalDevice, default, 0);
        if (device == 0) throw new InvalidOperationException("Godot did not expose its actual D3D12 device.");
        string library = D3D11RenderSession.ResolveNativeLibraryPath();
        if (applicationNativeMapDevicePreparation is null)
        {
            // GetDriverResource is borrowed. Retain the actual COM device on
            // Main before handing it to a worker that may outlive tree exit.
            var staticResources = (_applicationMapResources ?? throw new InvalidOperationException(
                "Native11 startup requires its complete CPU map resource declaration."));
            nint retainedDevice = unchecked((nint)(nuint)device);
            Marshal.AddRef(retainedDevice);
            try
            {
                applicationNativeMapDevicePreparation = Task.Run(() =>
                {
                    try { return D3D11RenderSession.Native11DeviceOwner.Create(library, device, staticResources.TerrainPrograms, staticResources.PrimaryGpuMedia, staticResources.RouteGpuMedia); }
                    finally { Marshal.Release(retainedDevice); }
                });
            }
            catch { Marshal.Release(retainedDevice); throw; }
        }
        var owner = await applicationNativeMapDevicePreparation;
        if (_shuttingDown || _failed || !IsInsideTree())
        {
            await owner.RetireAsync();
            return;
        }
        applicationNativeMapDevice = owner;
        // A real source replacement during startup must load its new category
        // before Title admission. Unchanged shader identities do no file work.
        _ = BorrowApplicationMapResources();
    }

    private D3D11RenderSession.Native11DeviceOwner RequireApplicationNativeMapDevice() =>
        applicationNativeMapDevice ?? throw new InvalidOperationException(
            "Native11 application device startup has not completed.");

    private async Task RetireApplicationNativeMapDeviceAsync()
    {
        var preparation = applicationNativeMapDevicePreparation;
        if (preparation is null) return;
        D3D11RenderSession.Native11DeviceOwner owner;
        try { owner = await preparation.ConfigureAwait(false); }
        catch { return; } // The startup task already owns and reports creation failure.
        await owner.RetireAsync().ConfigureAwait(false);
    }

    private async Task ObserveApplicationNativeMapDeviceRetirementAsync()
    {
        try { await RetireApplicationNativeMapDeviceAsync().ConfigureAwait(false); }
        catch (Exception error) { GD.PushError($"Application Native11 device retirement failed: {error}"); }
    }
}
